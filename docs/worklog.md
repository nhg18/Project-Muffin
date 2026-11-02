# 작업 기록

세션이 끝날 때 **한 것 · 결정 · 다음 할 일**을 남긴다. 최신 항목이 위. 이전 기록은 git 이력(`c67a110` 이전).

---

## 2026-11-02 · 타이틀 코드 컨벤션 (`feature/title-screen`)

### 결정
- ♪ · ≡ 버튼은 동작이 정해질 때까지 숨긴다 (`12-title-ui` 5 · 7 · 13절).
- 금지 문자는 지금처럼 접속 때 에러로 막는다 (입력 순간 막지 않음).
- 기획에 없는 게임성 보완(화면 연출 · PC 입력 편의 · 실패 흔들림 · 모바일 터치 영역)은 HTML 아티팩트로 안을 여러 개 만들어 고른 뒤 구현한다.

### 한 것
- `TitleScene` 에 남아 있던 Unity 6 재저장 변경을 따로 커밋.
- 타이틀 스크립트 컨벤션: 한 줄 한 문장(`if … return;` · `SafeArea` 나눗셈), `NicknameFieldView` 선언 순서, public XML 주석, 닉네임 상수 기준 문서.
- `TitleScene` 의 `SystemButtons` 비활성.
- 확인: `multi-test ui` 통과, 닉네임 검사 10가지 입력이 10-3 문구대로.

### 다음 할 일
- [ ] 타이틀 연결 중 · 연결 실패 상태 (`12-title-ui` 3절 6~8 · 6절 · 9-3 · 10-5) — 지금 코드는 폐기된 "접속 중…" 흐름이고 실패 문구도 옛 문구. 씬에 로딩 묶음 · 링 · 다시 시도 버튼이 필요
- [x] 게임성 보완 4가지 HTML 아티팩트 시안 — 캔버스 「찹츄 타이틀 게임성 시안」 `https://claude.ai/artifact/Fxy6WDjLUDq4pjvpMEhKEp` 의 "연출 (나중에)" 페이지에 보관. 연출은 UI 를 고른 뒤 얹는다
- [ ] UI 시안 고르기 — 같은 캔버스 "UI 시안" 페이지: 타이틀 A 가운데 · B 왼쪽 카드 · C 아래 펠트 띠 / 게임 A 테이블형 · B 보드형
- [ ] 디자인 언어를 「찹츄 테이블」의 스티커 · Jua 로 정함 (2026-11-02). `12-title-ui` (학교안심 둥근미소 · 보라)와 지금 TitleScene 은 옛 판 — 시안을 고른 뒤 기획서부터 고친다. 타이틀 · 로비 · 룸 · 인게임 아티팩트(Galmuri 픽셀)도 옛 판

---

## 2026-11-02 · 실행 확인 skill (`docs/multi-test-skill`)

### 결정
- AI 전용. 평소 2클라이언트, 기능을 마칠 때만 4클라이언트.
- 화면 조작은 컴포넌트의 private 처리 함수를 리플렉션으로 부른다. 이름이 바뀌면 `MultiTest.cs` 맨 위 표만 고친다 (테스트용 진입점을 코드에 따로 두지 않는다).

### 한 것
- `.claude/skills/multi-test/` — `SKILL.md` · `run.sh`(rules / ui / multi) · `MultiTest.cs`(`unity command script.run` 진입점, 프로젝트 밖).
- 저장 안 한 씬이 있으면 멈춤 · Refresh 와 컴파일 대기 · 끝나면 시작 씬 · Run In Background 되돌림.
- `CLAUDE.md` "직접 실행해서 확인" 에 skill 연결.
- 확인: `rules 4 6` · `ui` · `multi 2 4` · `multi 4 4`(clone_1 · clone_2 를 스크립트가 열어서) 통과. 4클라이언트 첫 턴 줄(♥100 · 손5 · 첫 턴 주인)이 네 화면 같음.

---

## 2026-11-02 · 코드를 컨벤션에 맞추기 (`fix/code-convention`)

### 한 것
- 요청 이벤트 7개 `On` 접두사 (`LobbyView` 3 · `RoomView` 2 · `TitleView` · `NicknameFieldView`). `DebugScript` 구독 함수 `OnJoinedRoom` → `HandleJoinedRoom`.
- 로그 접두사를 `[{nameof(클래스)}]` 로, 영어 로그 7곳 한국어로 (`PhotonConnection` · `PhotonRoom` · `DebugScript` 외 문자열 접두사 10곳).
- `var` 3곳 (`DebugScript` · `GameBootstrap`).
- 화면 문구를 한 곳에: `LobbyPresenter` 상수, `RoomPresenter` 로그 문구 함수.
- `[SerializeField]` 참조 누락 `Awake` LogError — 10 파일.
- 기록에 없던 위반도 고침: `Network/Events/` 네임스페이스 `Chapchu.Network` → `Chapchu.Network.Events` (폴더 경로), 클래스 요약 없던 9 파일, 값 필드 `[Tooltip]` 10개, `PhotonConnection` 선언 순서.
- 확인: 컴파일 · 타이틀 → 로비 → 없는 방 코드(경고 팝업) → 토스트 → 방 만들기 → 방 → 나가기, 2클론 디버그 로비 → 게임 → 카드 피해 두 화면 같음. 콘솔 에러 0.

### 다음 할 일
- [ ] public 멤버 XML 주석 없는 곳 약 98개 (Photon 콜백 · 단순 필드 포함) — 손댈지 결정
- [x] 테스트 절차 skill — 위 항목에서 만듦

---

## 2026-11-02 · 카드 효과를 조립식으로 (`feature/card-effect-compose`)

### 결정
- 효과 클래스는 `Scripts/Game/Effects/` (`Chapchu.Game.Effects`), 이름은 `종류 + Effect`. 에셋에 클래스 이름으로 저장되므로 만든 뒤 바꾸지 않는다 (`CODE_CONVENTION` 9절에 추가).
- 효과는 자기 `Apply` 로 실행하고, 상태는 `GameServer` 의 internal 메서드(`DealDamage`)로만 바꾼다. `EffectType` · `switch` 는 없앴다.
- 지금 효과는 `Damage` 하나. 다른 종류는 수치(N)가 정해지는 기능에서 추가한다.

### 한 것
- `CardEffect`(추상) · `DamageEffect` · `EffectSubject` → `Game/Effects/`. 옛 `CardEffect` 구조체 · `EffectType` 삭제.
- `CardData.effects` 를 `[SerializeReference]` 로. `DeckData.CreateCards` 는 빈 효과 칸을 빼고 LogError.
- `Editor/CardDataEditor`: 효과 목록 + 메뉴(상속 클래스 자동 나열) · 끌어서 순서 바꾸기.
- 카드 에셋 4개를 새 저장 형식으로 옮김 (값 그대로). 없는 에셋을 가리키던 옛 `conditions` 필드 제거.
- `11-card-list` 5절을 조립식 기준으로 고침 (옛 구조체 설명이 남아 있었음).
- 확인: 에셋 4개 효과 로드 · 덱 생성 · 에디터 추가 동작(메모리 임시 카드) · 규칙 계층 4인(대상 30 + 본인 7) · 2클론 멀티(카드 1 피해 20 · 카드 4 피해 5 가 두 화면 같음, 남의 턴 거절 코드 1). 인스펙터 화면 자체는 눈으로 보지 못했다.

### 다음 할 일
- [ ] 인스펙터에서 카드 에셋을 열어 효과 목록이 제대로 그려지는지 확인 (사람)
- [ ] `11-card-list` 4절 "현재 상태" 열 · 15행의 클래스 이름은 "되어야 할 상태" 문서 규칙과 맞지 않음 — 정리 제안

---

## 2026-11-01 · 지침 · 문서를 "되어야 할 상태" 기준으로

### 결정
- 코딩은 AI 가 맡고, 사람은 결과물을 고치고 조합한다. `CLAUDE.md` 는 원칙만, 코드 규칙은 `docs/CODE_CONVENTION.md` 한 곳에.
- 모든 문서는 현재 코드가 아니라 되어야 할 상태를 적는다. 코드가 다르면 코드를 고친다 (`GDD_GUIDE` 4-5).
- 플랫폼: PC / Android / iOS.
- 아키텍처: 규칙은 순수 C# (`Scripts/Game/`, 별도 asmdef) 으로 방장만 실행 → 일어난 일 목록 전파 → 클라이언트 연출 큐. 카드 효과는 `[SerializeReference]` 조립식 (`CODE_CONVENTION` 9절 · `00` · `09` 6.1).
- 브랜치: 개인 브랜치(`logic`) 대신 `feature/<기능>` 작업 브랜치 → PR → develop, 머지 후 삭제. 접은 작업은 `archive/<이름>` 태그 (`git-workflow` skill). 희건 브랜치는 그대로.
- `_Project/` 로 폴더 모으기는 보류. 방장 이주 복원 방식은 보류 유지 — 상태를 한 객체에 모으는 것까지만 지금 한다.

### 한 것
- `CLAUDE.md`: Unity 결과 보고 규칙 · Unity 작업 방식 섹션 삭제 → `작업 방식` 5줄로 교체.
- `CODE_CONVENTION.md` 전면 개정 (8절): 이벤트 이름 통일(`OnXxx` · `HandleXxx`), 컴포넌트 단위, 참조 누락 LogError, 문자열 키 상수화, 로그 접두사 등.
- 기획서에서 구현 위치(클래스 · 메서드 이름 · "현재 구현") 제거: 01 · 08 · 10 · 14 · 15 · 16.
- "방 나가기 → 들어온 씬으로 복귀" 규칙 삭제 (디버그 로비 테스트용이었음). 나가기는 항상 로비.
- `store-release.md` 를 Unity 6000.3.27f1 기준으로 (결정 7 완료).
- 인게임 규칙을 새 구조로: 옛 `GameServer` 를 `Scripts/Game/`(asmdef `Chapchu.Game`, 엔진 참조 없음)로 옮기고 결과를 "일어난 일 목록"(`GameEvent`)으로 돌려주게 바꿈. `PunGameServer` 가 받을 사람별로 RPC + Props 스냅샷. 카드 에셋 스크립트는 `Scripts/Cards/`.
- `Scripts old/` · `Tests/EditMode` · `TempGameScene` 삭제. `GameViewSizeSetup` 은 `Scripts/Editor/` 로. 옛 GameScene 은 `OldGameScene` 으로 복사해 두고, `GameScene` 은 카메라 + GameServer 오브젝트만 남김.
- 찹츄 선언(옛 코드는 "내 턴에만", 07 4.1 은 "언제든")과 `Discard` 요청(문서에 없음)은 옮기지 않음 — 기능 8 에서 문서대로.
- 확인 (2클론, Unity CLI 로 직접 실행): 손패 서로 다름 · 상대 손패 내용 안 보임 · 모두 손5 ♥100 · 첫 턴 같음 / 20초 자동 넘김 · 뽑기 후 턴 넘김 · 남의 턴 뽑기는 요청자에게만 거절(코드 1) · 카드 사용 피해가 두 화면 같음. 규칙 계층만 4인으로 따로 돌려 대상 거절(코드 5) · 나감 처리도 확인.
- RPC 를 새로 만들면 `PhotonServerSettings` 의 RpcList 가 바뀐다 — 이 파일을 같이 커밋해야 클라이언트끼리 RPC 번호가 맞는다 (안 맞으면 "Could not find RPC with index").
- Unity CLI 용 `com.unity.pipeline` 패키지 설치 — 에디터를 명령줄로 조종해 멀티 테스트를 직접 돌린다.

### 다음 할 일
- [x] 코드를 `CODE_CONVENTION.md` 에 맞추기 — 요청 이벤트 7개 `On` 접두사, 로그 접두사 · 한국어 7곳, `DebugScript:62` `var`, `RoomPresenter` 문구 모으기, `[SerializeField]` 참조 누락 LogError (11 파일)
- [ ] 방 나가기는 항상 `LobbyScene` — `SceneFlow.ReturnSceneAfterRoom` 과 관련 코드 삭제, `docs/diagrams/ui-flow.workflow.json` 의 "들어온 씬" 흐름도 고치고 도식 다시 생성
- [x] 테스트 절차 skill 만들기 (Unity CLI 로 멀티 실행이 되는지 먼저 확인)
- [ ] 기능 1 멀티 확인을 4클론으로 — 2클론은 통과(아래). DebugLobbyScene → 입장 → 방장 시작 → 각 콘솔의 `[GameDebugInput]` 턴 줄 비교. D = 뽑기, P = 첫 카드 사용
- [ ] 인게임 화면 — 새 `GameScene` 에 UI 를 조립한다. 값은 `Network/Events/GameEvents` 로만 받는다 (`OnMy*` 는 나에게만 온다)
- [x] 카드 효과를 `[SerializeReference]` 조립식으로 (기능 4 에서, `CODE_CONVENTION` 9절). 지금은 옛 `CardEffect` 구조체 그대로
- [ ] 옛 카드 · 손패 프리팹 5개(`Assets/Prefab/Player*Card*` · `OtherPlayerHand`)와 `OldGameScene` 은 스크립트가 Missing — 화면을 새로 만들면 지운다

---

## 2026-10-31 · 지침 정리

### 결정
- `develop` 머지는 PM.
- 만드는 순서: HTML 시안 → Unity.
- 브랜치: `main` ← `develop` ← `feature/<기능>`.
- Unity 6000.3.27f1 로 업그레이드. 타이틀 · 로비 · 방 외 스크립트는 `Assets/Scripts old/` 로 옮김(참고만).

### 한 것
- `CLAUDE.md` · skill · hook · `docs/` 를 위 기준으로 줄임. 끝난 플랜 · 트랙 문서 삭제.

### 다음 할 일
- [ ] `logic` → `develop` PR, 개인 브랜치 남은 작업 확인 후 머지, 옛 브랜치 삭제
- [ ] 팀원 PC: Unity 6000.3.27f1 설치, hook 동작 확인
- [ ] 인게임 HTML 프로토타입 → Unity
- [ ] 타이틀 연결 중 · 연결 실패 상태 (`12-title-ui` 6절 · 9-3 · 10-5) 미구현
- [ ] 테스트 빌드 전 Build Settings 시작 씬을 `TitleScene` 으로 (지금 `DebugLobbyScene` 이 0번)
