/*
 * Cloud Code 「닉네임 저장」 — 닉네임을 정하거나 바꾸는 유일한 길. 클라이언트는 자기 닉네임을 Cloud Save 에 직접 쓰지 않는다.
 * 기준 문서: docs/systems/23-login.md 3절 #5 · 7-8 · 8절, 12-title-ui.md 4-2
 *
 * 순서: 길이 · 문자(12 4-2) → (바꾸기면) 쿠폰 · 쿨다운 → 금칙어(23 8-2) → 이름 장부에 새 이름 → 내 보호 칸 → 장부에서 옛 이름 지우기.
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

  const cloudSave = new DataApi(context);

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
    if (newId !== oldId && !(await claim(cloudSave, projectId, newId, playerId)))
      return { result: "taken" };

    // 닉네임을 먼저 — 뒤의 쓰기가 실패하면 쿠폰을 덜 쓰는 쪽으로 남는다
    const coupons = isFirst ? mine.coupons : mine.coupons - 1;
    const changedAt = isFirst ? mine.changedAt : Date.now();
    await cloudSave.setProtectedItem(projectId, playerId, { key: KEY_NICKNAME, value: nickname });
    if (!isFirst) {
      await cloudSave.setProtectedItem(projectId, playerId, { key: KEY_COUPONS, value: coupons });
      await cloudSave.setProtectedItem(projectId, playerId, { key: KEY_CHANGED_AT, value: changedAt });
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
  for (const item of res.data.results ?? [])
    values[item.key] = item.value;

  return {
    nickname: typeof values[KEY_NICKNAME] === "string" ? values[KEY_NICKNAME] : null,
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

// 비어 있으면 내 것으로 쓰고, 다시 읽어 내 것인지 확인한다 — 동시에 같은 이름을 쓰면 나중에 쓴 쪽이 남고 먼저 쓴 쪽은 여기서 진다
async function claim(cloudSave, projectId, id, playerId) {
  const owner = await ownerOf(cloudSave, projectId, id);
  if (owner === playerId)
    return true;
  if (owner)
    return false;

  await cloudSave.setPrivateCustomItem(projectId, id, { key: REGISTRY_KEY, value: playerId });
  return (await ownerOf(cloudSave, projectId, id)) === playerId;
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
