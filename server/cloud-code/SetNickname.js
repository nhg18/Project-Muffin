/*
 * Cloud Code 「닉네임 저장」 — 닉네임을 정하거나 바꾸는 유일한 길. 클라이언트는 자기 닉네임을 Cloud Save 에 직접 쓰지 않는다.
 * 기준 문서: docs/systems/23-login.md 3절 #5 · 7-8 · 8절, 12-title-ui.md 4-2
 *
 * 순서: 길이 · 문자(12 4-2) → (바꾸기면) 쿠폰 · 쿨다운 → 금칙어(23 8-2) → [이름 자물쇠] 이름 장부에 새 이름 → 내 보호 칸 → [풀기] → 장부에서 옛 이름 지우기.
 *
 * 동시에 같은 이름 (23 7-8): Cloud Save 에는 UNIQUE 제약도 「없을 때만 만들기」 도 없다. 있는 것은 쓰기 잠금(writeLock) —
 * 「내가 읽은 판일 때만 써진다, 아니면 409」 인 낙관적 잠금(CAS)이다. 그것으로 자물쇠 칸을 만들어 이름 하나를 「확인 → 쓰기」 하는 동안
 * 그 칸을 한 스크립트만 쥐게 한다. 같은 칸을 동시에 잡으면 하나만 성공하고, 나머지는 기다렸다 다시 — 그땐 이름이 이미 장부에 있어 taken.
 * 스크립트가 도중에 죽어도 자물쇠는 LOCK_LEASE_MS 뒤 저절로 풀린다.
 * 결과는 예외가 아니라 { result } 로 돌려준다 — 클라이언트가 갈래마다 다른 문구를 띄운다.
 *   ok · same(지금 이름과 똑같음, 쓰지 않음) · invalid · banned · taken · cooldown(+remainingSeconds) · noCoupon · error
 *
 * 배포: ugs deploy server/cloud-code/SetNickname.js (server/README.md)
 */
const { DataApi } = require("@unity-services/cloud-save-1.4");
const { SettingsApi } = require("@unity-services/remote-config-1.1");

// 12-title-ui 4-2
const MIN_LENGTH = 2;  // 글자 수
const MAX_WIDTH = 16;  // 폭 — 영문 · 숫자 1, 그 밖의 글자 2
const ALLOWED = /^[가-힣a-zA-Z0-9ぁ-ゟァ-ヺー-ヿ々㐀-䶿一-鿿豈-﫿]+$/u;

// 23 3절 #5 · 4절
const COOLDOWN_MS = 168 * 60 * 60 * 1000;
const FREE_COUPONS = 1; // 칸이 없으면 계정 생성 때 받은 1장 (7-8)

// 내 계정의 보호 칸 (서버만 쓰고 나는 읽기만, 23 7-8) — 클라이언트 CloudKeys 와 같은 이름
const KEY_NICKNAME = "nickname";
const KEY_COUPONS = "nicknameCoupons";
const KEY_CHANGED_AT = "nicknameChangedAt";

// 이름 장부 — 이름 하나에 비공개 게임 데이터 묶음 하나, 칸 owner = 플레이어 ID (23 7-8)
const REGISTRY_PREFIX = "nick_";
const REGISTRY_KEY = "owner";

// 이름 자물쇠 — 비공개 게임 데이터 묶음 하나에 칸 LOCK_BUCKETS 개. 이름을 해시로 칸에 나눠, 다른 이름끼리는 덜 기다리게 한다
const LOCK_ID = "nicklock";
const LOCK_BUCKETS = 16;
const LOCK_LEASE_MS = 10000;   // 이 시간이 지나면 쥔 쪽이 죽은 것으로 보고 빼앗는다 — Cloud Code 실행 제한 15초 안
const LOCK_TRIES = 25;         // 기다리며 다시 잡는 횟수 — 약 5초
const LOCK_WAIT_MS = 200;

// 23 8-1 Remote Config 키
const RC_BANNED = "bannedWords";
const RC_ALLOWED = "allowedWords";

// 23 8-2 3단계 — 숫자 → 영문 유사문자. 1 은 두 갈래(l · ㅣ)라 따로 만든다
const DIGIT_LOOK_ALIKES = [["0", "o"], ["5", "s"], ["8", "b"], ["3", "e"], ["4", "a"], ["7", "t"]];

module.exports = async ({ params, context, logger }) => {
  const { projectId, playerId, environmentId } = context;
  const nickname = String(params.nickname ?? "").trim();

  if (!isValid(nickname))
    return { result: "invalid" };

  // 이름 장부 · 자물쇠(비공개 게임 데이터) · 보호 칸은 서버 권한으로만 쓴다 — 플레이어 토큰(context 그대로)은 403
  const cloudSave = new DataApi({ accessToken: context.serviceToken });

  try {
    const mine = await loadMine(cloudSave, projectId, playerId);
    const isFirst = !mine.nickname;

    if (!isFirst && mine.nickname === nickname)
      return { result: "same" };

    if (!isFirst) {
      const remaining = COOLDOWN_MS - (Date.now() - mine.changedAt);
      if (mine.changedAt > 0 && remaining > 0)
        return { result: "cooldown", remainingSeconds: Math.ceil(remaining / 1000) };
      if (mine.coupons < 1)
        return { result: "noCoupon" };
    }

    const words = await loadWords(context, projectId, environmentId, playerId);
    if (containsBannedWord(nickname, words.banned, words.allowed))
      return { result: "banned" };

    // 대소문자만 바꾸면 같은 이름 — 장부는 그대로 (23 7-8)
    const newId = registryId(nickname);
    const oldId = isFirst ? null : registryId(mine.nickname);
    const coupons = isFirst ? mine.coupons : mine.coupons - 1;
    const changedAt = isFirst ? mine.changedAt : Date.now();

    const lock = newId === oldId ? null : await acquire(cloudSave, projectId, newId, playerId, logger);
    if (newId !== oldId && !lock)
      return { result: "error" }; // 자물쇠를 못 잡음 — 「잠시 후 다시」

    try {
      if (lock) {
        const owner = await ownerOf(cloudSave, projectId, newId);
        if (owner && owner !== playerId)
          return { result: "taken" };
        if (!owner)
          await cloudSave.setPrivateCustomItem(projectId, newId, { key: REGISTRY_KEY, value: playerId });
      }

      // 닉네임을 먼저 — 뒤의 쓰기가 실패하면 쿠폰을 덜 쓰는 쪽으로 남는다.
      // 바꾸기면 읽은 판의 쓰기 잠금을 건다 — 같은 사람의 요청 둘이 겹치면 늦은 쪽이 409 로 멈춰 쿠폰을 두 번 쓰지 않는다
      await cloudSave.setProtectedItem(projectId, playerId, { key: KEY_NICKNAME, value: nickname, writeLock: mine.nicknameLock ?? undefined });
      if (!isFirst) {
        await cloudSave.setProtectedItem(projectId, playerId, { key: KEY_COUPONS, value: coupons });
        await cloudSave.setProtectedItem(projectId, playerId, { key: KEY_CHANGED_AT, value: changedAt });
      }
    } finally {
      if (lock)
        await unlock(cloudSave, projectId, lock, logger);
    }

    if (oldId && oldId !== newId)
      await release(cloudSave, projectId, oldId, playerId, logger);

    return { result: "ok", nickname, coupons, changedAt };
  } catch (err) {
    logger.error(`SetNickname 실패: ${describe(err)}`);
    return { result: "error" };
  }
};

module.exports.params = {
  nickname: { type: "String", required: true },
};

// ----- 길이 · 문자 (12 4-2) --------------------------------------------------

function isValid(nickname) {
  const chars = Array.from(nickname);
  return chars.length >= MIN_LENGTH && widthOf(chars) <= MAX_WIDTH && ALLOWED.test(nickname);
}

function widthOf(chars) {
  return chars.reduce((sum, c) => sum + (/[a-zA-Z0-9]/.test(c) ? 1 : 2), 0);
}

// ----- 내 보호 칸 --------------------------------------------------------------

async function loadMine(cloudSave, projectId, playerId) {
  const res = await cloudSave.getProtectedItems(projectId, playerId, [KEY_NICKNAME, KEY_COUPONS, KEY_CHANGED_AT]);
  const values = {};
  let nicknameLock = null;
  for (const item of res.data.results ?? []) {
    values[item.key] = item.value;
    if (item.key === KEY_NICKNAME)
      nicknameLock = item.writeLock ?? null;
  }

  return {
    nickname: typeof values[KEY_NICKNAME] === "string" ? values[KEY_NICKNAME] : null,
    nicknameLock,
    coupons: Number.isFinite(values[KEY_COUPONS]) ? values[KEY_COUPONS] : FREE_COUPONS,
    changedAt: Number.isFinite(values[KEY_CHANGED_AT]) ? values[KEY_CHANGED_AT] : 0,
  };
}

// ----- 이름 장부 (23 7-8) ------------------------------------------------------

// 같은 이름 = NFKC → 소문자. 묶음 ID 는 영문 · 숫자 · _ · - 50자까지라 UTF-8 을 base64url 로 (폭 16 = 최대 24바이트 = 32자)
function registryId(nickname) {
  const normalized = nickname.normalize("NFKC").toLowerCase();
  return REGISTRY_PREFIX + Buffer.from(normalized, "utf8").toString("base64url");
}

async function ownerOf(cloudSave, projectId, id) {
  const res = await cloudSave.getPrivateCustomItems(projectId, id, [REGISTRY_KEY]);
  const item = (res.data.results ?? []).find(i => i.key === REGISTRY_KEY);
  return item ? item.value : null;
}

// ----- 이름 자물쇠 (CAS) -----------------------------------------------------

// 이름 → 자물쇠 칸. 같은 이름은 늘 같은 칸
function lockKey(id) {
  let h = 0;
  for (const c of id)
    h = (h * 31 + c.charCodeAt(0)) >>> 0;
  return `b${h % LOCK_BUCKETS}`;
}

// 칸이 비었거나(holder 없음) 쥔 쪽의 시간이 지났으면, 읽은 판의 쓰기 잠금으로 내 이름을 쓴다 — 그 사이 누가 먼저 썼으면 409 로 지고 다시.
// 잡으면 { key, writeLock }, 못 잡으면 null.
// 칸은 배포 때 미리 만들어 둔다 (server/README.md) — 여기서 만들면 둘이 동시에 만들 때 늦게 만든 쪽이 이미 잡힌 자물쇠를 덮어써 둘 다 잡는다
async function acquire(cloudSave, projectId, id, playerId, logger) {
  const key = lockKey(id);
  const token = `${playerId}:${Date.now()}:${Math.random().toString(36).slice(2, 8)}`;

  for (let i = 0; i < LOCK_TRIES; i++) {
    const res = await cloudSave.getPrivateCustomItems(projectId, LOCK_ID, [key]);
    const item = (res.data.results ?? []).find(it => it.key === key);
    if (!item) {
      logger.error(`이름 자물쇠 칸 ${LOCK_ID}/${key} 가 없다 — server/README.md 「배포」 의 자물쇠 만들기를 돌린다`);
      return null;
    }

    const held = item.value && item.value.holder && item.value.until > Date.now();
    if (!held) {
      try {
        const set = await cloudSave.setPrivateCustomItem(projectId, LOCK_ID, {
          key, value: { holder: token, until: Date.now() + LOCK_LEASE_MS }, writeLock: item.writeLock,
        });
        return { key, writeLock: set.data.writeLock };
      } catch (err) {
        if (err?.response?.status !== 409)
          throw err;
      }
    }

    await sleep(LOCK_WAIT_MS + Math.floor(Math.random() * LOCK_WAIT_MS));
  }

  logger.warning(`이름 자물쇠를 못 잡음 ${key}`);
  return null;
}

// 내가 쥔 판일 때만 푼다 — 시간이 지나 남이 가져갔으면 409, 그대로 둔다
async function unlock(cloudSave, projectId, lock, logger) {
  try {
    await cloudSave.setPrivateCustomItem(projectId, LOCK_ID, { key: lock.key, value: { holder: null, until: 0 }, writeLock: lock.writeLock });
  } catch (err) {
    logger.warning(`이름 자물쇠 풀기 실패 ${lock.key}: ${describe(err)}`);
  }
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

// 옛 이름 풀기 — 내 것일 때만. 실패해도 닉네임은 이미 바뀌었으니 기록만 남긴다
async function release(cloudSave, projectId, id, playerId, logger) {
  try {
    if ((await ownerOf(cloudSave, projectId, id)) === playerId)
      await cloudSave.deletePrivateCustomItem(REGISTRY_KEY, projectId, id);
  } catch (err) {
    logger.warning(`옛 이름 풀기 실패 ${id}: ${describe(err)}`);
  }
}

// ----- 금칙어 (23 8-2) ---------------------------------------------------------

async function loadWords(context, projectId, environmentId, playerId) {
  const remoteConfig = new SettingsApi(context);
  const res = await remoteConfig.assignSettings({
    projectId,
    environmentId,
    userId: playerId,
    key: [RC_BANNED, RC_ALLOWED],
    attributes: { unity: {}, app: {}, user: {} }, // 없으면 400 「Invalid request value at path: …["attributes"]」 (실서버 2026-11-27)
  });

  const settings = res.data?.configs?.settings ?? {};
  return { banned: toList(settings[RC_BANNED]), allowed: toList(settings[RC_ALLOWED]) };
}

// JSON 형은 배열로, 문자열 형은 JSON 을 풀어서
function toList(value) {
  if (Array.isArray(value))
    return value.filter(v => typeof v === "string" && v.length > 0);
  if (typeof value === "string" && value.length > 0)
    return toList(JSON.parse(value));
  return [];
}

// 2 소문자 · 2-1 NFKC · 가타카나 → 히라가나 · 3 숫자 유사문자 → 5 통과 목록 지우기 → 6 부분 일치.
// 1 · 4 · 7단계(공백 · 특수문자 · 자모 · 초성)는 위 문자 검사가 먼저 막아 남는 입력이 없다. 2-1 번체 → 간체는 아직 없다 (목록에 두 글꼴을 다 넣는다)
function containsBannedWord(nickname, banned, allowed) {
  if (banned.length === 0)
    return false;

  let s = normalize(nickname);
  for (const [from, to] of DIGIT_LOOK_ALIKES)
    s = s.split(from).join(to);

  const bannedN = banned.map(normalize);
  const allowedN = allowed.map(normalize);
  return containsAny(s.split("1").join("l"), bannedN, allowedN) || containsAny(s.split("1").join("ㅣ"), bannedN, allowedN);
}

function normalize(text) {
  return text.normalize("NFKC").toLowerCase().replace(/[ァ-ヶ]/g, c => String.fromCharCode(c.charCodeAt(0) - 0x60));
}

function containsAny(s, banned, allowed) {
  for (const word of allowed)
    s = s.split(word).join("");
  return banned.some(word => s.includes(word));
}

function describe(err) {
  const status = err?.response?.status;
  const detail = err?.response?.data ? JSON.stringify(err.response.data) : err?.message;
  return status ? `${status} ${detail}` : String(detail ?? err);
}
