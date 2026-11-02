# 작업 기록

세션이 끝날 때 **한 것 · 결정 · 다음 할 일**을 남긴다. 최신 항목이 위. 이전 기록은 git 이력(`c67a110` 이전).

---

## 2026-11-02 · 게임 화면 방 코드 표시 (`RoomCodePresenter`)

### 결정
- 방 코드는 방장 판정 · 게임 상태를 거치지 않고 UI 가 방 이름을 직접 읽는다 (`10-ui.md` 2절). `GameScreenPresenter` 에 넣지 않고 따로 둔다 — 게임 이벤트와 무관.
- `origin/upgrade/unity6` 의 `RoomCodePresenter`(`751e05e`)를 현재 `RoomCodeView` 에 맞춰 옮겼다.
- 씬은 다시 조립하지 않고 기존 `RoomCodeTag` 에 컴포넌트만 붙였다 — 재조립하면 fileID 가 전부 바뀌어 diff 가 1만 줄이 된다. 빌더에도 같은 줄을 넣어 재조립 결과는 같다.

### 한 것
- `UI/GameScreen/RoomCodePresenter.cs` — `Start` 에서 `PhotonNetwork.CurrentRoom.Name` → `RoomCodeView`, 방 밖이면 `----`.
- 빌더 `BuildRoomCodeTag` 에 프레젠터 부착, `GameScene.unity` 의 `RoomCodeTag` 에 부착.
- 플레이 모드 확인: 방 밖 `| ROOM ----`, Photon 접속 · 시험 방 생성 후 프레젠터를 다시 붙여 `| ROOM DIPK` · 로그 `[RoomCodePresenter] 방 코드 DIPK`.

### 다음 할 일
- [ ] 로비 → 방 → 게임 시작 정상 흐름(2클론)에서 방 코드가 대기실과 같은지 확인

---

## 2026-11-02 · 상대 좌석 · 덱 · 버림 더미 UI 일단 제거

### 결정
- 사용자 요청으로 상대 좌석 UI(이름 줄 · 손패 수 · 찹츄 배지 · HP 바 · 캐릭터 자리 · 함정 슬롯 · 턴 외곽선 · 대상 강조)와 덱 · 버림 더미 UI 를 **우선** 뺐다. `10-ui.md` 2-1 은 되어야 할 상태라 그대로 둔다 — 다시 만들 때 기준.
- 게임 로직의 덱 · 버림 더미(`GameServer.Deck.cs` 등)는 화면과 무관해 그대로.

### 한 것
- `OpponentSeatView` · `DeckView` · `DiscardPileView` 삭제. `GameScreenView` 의 해당 참조, 빌더의 `BuildOpponentSeat(s)` · `BuildDeck` · `BuildDiscardPile` · `BuildBackCard` · `WireArray` · 안 쓰게 된 색 상수 삭제.
- Unity CLI 1.0.0-beta.15 설치 (`%LOCALAPPDATA%\unity\bin\unity.exe`). 열린 에디터에서 컴파일 확인 → `GameCanvas` 삭제 → `Chapchu/Game Screen/Build UI` 재조립 → 씬 저장 → 플레이 모드 캡처로 방 코드 · 히스토리 · 내 패널만 남은 것 확인.
- 폰트 SDF 에셋 2개가 재조립 · 플레이 중 동적 글리프 추가로 바뀌었다 — 이번 변경과 무관.

### 다음 할 일
- [ ] 상대 좌석 · 덱 · 버림 더미 UI 를 다시 만들 때 `10-ui.md` 2-1 기준으로 빌더에 되살린다 (`e49831f` 참고)

---

## 2026-11-02 · 인게임 화면 UI 조립 (HTML 시안 → GameScene, 표시만)

브랜치 `feature/game-screen`. 요청 · 이벤트는 잇지 않았다 — 뷰는 값을 받아 그리고 입력을 C# 이벤트로만 내보낸다.

### 결정
- HTML 시안 CSS px × 2 = 캔버스 px. `vh` · `vw` 는 1920 × 1080 기준 (`10-ui.md` 2-1).
- 시안에 있지만 넣지 않은 것: `ROUND` · `?` · `≡` (기획에 없음), 화살꼬리 태그 · 점선 (직사각형 · 실선으로 대체).
- 이번 범위 밖: 내 손패 카드, 상대 뒷면 부채꼴, 히스토리 카드 항목. 영역 · 앵커만 둠 (`HandArea` · `HandFanAnchor` · `HistoryView.Content`).
- 아티팩트에 없던 그림(치와와 3색 · 카드 앞면 3종)은 빈 자리로. `CharacterImage` 는 Image 비활성, 스프라이트를 끼우면 `SetCharacter` 로 켠다.
- 씬은 손으로 고치지 않고 에디터 메뉴(`Chapchu/Game Screen/Build UI`, `Scripts/Editor/GameScreenBuilder.cs`)가 조립한다. `GameCanvas` 가 이미 있으면 멈춘다 — 지우고 다시 실행. 수치를 바꿀 때는 빌더를 고치고 재조립하는 쪽이 Inspector 에서 손보는 것보다 재현이 쉽다.
- Unity CLI(`com.unity.pipeline`)로 열린 에디터를 조종해 컴파일 · 메뉴 실행 · 저장 · 플레이 모드 캡처까지 확인했다. CLI 는 `%LOCALAPPDATA%\unity\bin\unity.exe` (새 셸에서는 `unity`). 캡처 `save_path` 는 프로젝트 안이어야 하고 `Assets/` 아래로 떨어지므로 끝나면 지운다.

### 한 것
- `docs/systems/10-ui.md` 2-1 「주요 수치」 — 색 · 글꼴 · 배치 · 좌석 슬롯 · 내 패널 · 승리 오버레이 · 넣지 않는 것.
- `docs/mockups/ingame.html` — 아티팩트 「인게임 화면 UI」 정적 사본 (글꼴 · 배경은 프로젝트 에셋 상대 경로).
- `Scripts/UI/GameScreen/` 8개: `GameScreenView`(영역 모음) · `RoomCodeView` · `HistoryView` · `OpponentSeatView` ×3 슬롯 · `MyPanelView`(`ChapchuButtonState`) · `DeckView` · `DiscardPileView` · `VictoryOverlayView`.
- `Assets/Fonts/GlaMuri/Galmuri11 SDF` · `Galmuri11-Bold SDF` — 빌더가 TMP 동적 폰트로 생성 (SDFAA · 여백 5 · 2048). ttf 는 사용자가 넣음.
- `GameScene.unity` — `GameCanvas`(1920 × 1080 · Expand) + `SafeArea` + `EventSystem`(StandaloneInputModule). 캡처로 확인: 기본 상태, 상태 변경(턴 외곽선 · 찹츄 활성 · 배지 · 함정 2/3 · 저체력 · 대상 강조 · 버림 비움), 승리 오버레이.

### 다음 할 일
- [ ] `GameScreenPresenter` — `GameEvents` 7개 구독 → 연출 큐 → 뷰. 좌석 인덱스 ↔ actorNumber 는 `02-player` 3절대로 한 곳에서. 초기값은 `PlayerProps` · `RoomProps`. 방 코드는 `PhotonNetwork.CurrentRoom.Name`
- [ ] 입력 연결: `DeckView.DrawClicked` → `RequestDraw`, 좌석 `Clicked` → 대상 선택, `OnMyRequestRejected` → `MessageToast`. 찹츄 버튼 · 승리 오버레이는 요청 API 가 생기면 (기능 8)
- [ ] 내 손패(`HandArea`) · 상대 뒷면 부채꼴(`HandFanAnchor`) · 히스토리 항목 프리팹(`HistoryView.AddEntry`)
- [ ] 글자 22 ~ 28 의 모바일 가독성 보정 (`15-screen` 최소 30) — `12-title-ui` 4-3 방식으로 결정
- [ ] 디자인 그림이 오면 `CharacterImage` · 카드 앞면 스프라이트 연결
- [ ] `GameDebugInput`(D · P 키)과 화면이 같은 값을 보이는지 2클론으로 확인

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
