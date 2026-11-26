# 서버 (UGS Cloud Code)

게임 밖 서버는 UGS 뿐이다 (`docs/systems/23-login.md` 3절 #4). 여기는 **Cloud Code 스크립트** 원본을 둔다 — Unity 프로젝트(`MuffinProject/`) 밖이라 빌드에 들어가지 않는다.

| 스크립트 | 하는 일 | 기준 문서 |
| --- | --- | --- |
| `cloud-code/SetNickname.js` | 닉네임 정하기 · 바꾸기 — 길이 · 문자 · 금칙어 · 중복(이름 장부) · 변경 쿠폰 · 7일 쿨다운을 보고 보호 칸에 쓴다. 클라이언트가 자기 닉네임을 쓰는 유일한 길 | `23-login` 3절 #5 · 7-8 · 8절 |

## 데이터 (Cloud Save)

| 어디 | 키 | 값 | 쓰는 쪽 |
| --- | --- | --- | --- |
| 플레이어 보호 칸 | `nickname` · `nicknameCoupons` · `nicknameChangedAt`(유닉스 ms) | 내 닉네임 · 쿠폰 수(없으면 1장) · 마지막 변경 시각 | `SetNickname` 만. 클라이언트는 읽기만 (`AccountService.LoadNicknameAsync`) |
| 비공개 게임 데이터 `nick_<이름>` | `owner` | 그 이름을 쓰는 플레이어 ID. `<이름>` = NFKC · 소문자로 바꾼 이름의 UTF-8 을 base64url (묶음 ID 는 영문 · 숫자 · `_` · `-` 50자까지) | `SetNickname` 만 |
| 비공개 게임 데이터 `nicklock` | `b0` ~ `b15` | 이름 자물쇠 `{ holder, until }` — 같은 이름을 둘이 동시에 저장할 때 한 명씩 장부를 보게 한다 (`23` 7-8). **배포 때 미리 만든다** (아래) | `SetNickname` 만 |

읽는 Remote Config: `bannedWords` · `allowedWords` (`23` 8-1).

## 배포

UGS CLI (`npm install -g ugs`) 와 서비스 계정 키가 있어야 한다 — 키는 저장소에 넣지 않는다.

```bash
ugs login --service-key-id <키 ID> --secret-key-stdin < <비밀 키 파일>
ugs config set project-id 36edb1bd-5445-4554-8ed7-d7c526a59a6d   # ProjectSettings 의 cloudProjectId
ugs config set environment-name production
ugs deploy server/cloud-code/SetNickname.js
ugs cloud-code scripts list                                     # 올라갔는지
```

### 이름 자물쇠 만들기 (환경마다 한 번)

`SetNickname` 은 자물쇠 칸이 없으면 저장하지 않는다 (「잠시 후 다시 시도해 주세요.」). 저장 중에 칸을 만들면 둘이 동시에 만들 때 늦게 만든 쪽이 이미 잡힌 자물쇠를 덮어써 둘 다 잡으므로, 환경을 새로 만들 때 **한 번** 미리 만든다. 이미 있으면 다시 돌려도 된다 (빈 자물쇠로 덮는다 — 저장이 몰리는 때는 피한다).

```bash
P=36edb1bd-5445-4554-8ed7-d7c526a59a6d
E=<환경 ID>                                                     # ugs env list
for b in $(seq 0 15); do
  curl -sS -u "<키 ID>:<비밀 키>" -H "Content-Type: application/json"     -d "{\"key\":\"b$b\",\"value\":{\"holder\":null,\"until\":0}}"     "https://services.api.unity.com/cloud-save/v1/data/projects/$P/environments/$E/custom/nicklock/private-items"
done
```

칸 수(16)를 바꾸면 `SetNickname.js` 의 `LOCK_BUCKETS` 와 같이 바꾼다.

서비스 계정 역할: 프로젝트에 Cloud Code 편집 역할 + Cloud Save 편집 역할(자물쇠 만들기) + 환경 보기 역할.

## 고칠 때

- 규칙(길이 · 문자 · 금칙어 정규화)은 클라이언트 `NicknameValidator` 와 **같이** 고친다 (`23` 8-2).
- 돌려주는 `result` 값을 바꾸면 클라이언트 `NicknameSave` 상수와 맞춘다.
- 배포한 뒤 `bash .claude/skills/multi-test/run.sh ui` 로 실제 로그인 · 닉네임 저장을 돌려 본다.
