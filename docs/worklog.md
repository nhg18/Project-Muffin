# 작업 기록

세션이 끝날 때 **한 것 · 결정 · 다음 할 일**을 남긴다. 최신 항목이 위. 이전 기록은 git 이력(`c67a110` 이전).

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
- [ ] 코드를 `CODE_CONVENTION.md` 에 맞추기 — 요청 이벤트 7개 `On` 접두사, 로그 접두사 · 한국어 7곳, `DebugScript:62` `var`, `RoomPresenter` 문구 모으기, `[SerializeField]` 참조 누락 LogError (11 파일)
- [ ] 방 나가기는 항상 `LobbyScene` — `SceneFlow.ReturnSceneAfterRoom` 과 관련 코드 삭제, `docs/diagrams/ui-flow.workflow.json` 의 "들어온 씬" 흐름도 고치고 도식 다시 생성
- [ ] 테스트 절차 skill 만들기 (Unity CLI 로 멀티 실행이 되는지 먼저 확인)
- [ ] 기능 1 멀티 확인을 4클론으로 — 2클론은 통과(아래). DebugLobbyScene → 입장 → 방장 시작 → 각 콘솔의 `[GameDebugInput]` 턴 줄 비교. D = 뽑기, P = 첫 카드 사용
- [ ] 인게임 화면 — 새 `GameScene` 에 UI 를 조립한다. 값은 `Network/Events/GameEvents` 로만 받는다 (`OnMy*` 는 나에게만 온다)
- [ ] 카드 효과를 `[SerializeReference]` 조립식으로 (기능 4 에서, `CODE_CONVENTION` 9절). 지금은 옛 `CardEffect` 구조체 그대로
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
