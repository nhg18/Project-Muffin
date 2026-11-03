# 작업 기록

세션이 끝날 때 **한 것 · 결정 · 다음 할 일**을 남긴다. 최신 항목이 위. 이전 기록은 git 이력(`c67a110` 이전).

---

## 2026-11-03 · 상대 손패 뒷면 부채꼴 (캐릭터 그림 보류)

### 결정 (사용자 요청)
- 시안 `.fan` 대로: 캐릭터 상자(141 × 188) 아래 가운데에서 38 위, 42 × 45 상자, 뒷면 45 × 31(9:13) 파랑 바탕 · 흰 테두리 · 검정 링. 캐릭터 그림은 계속 보류.
- 배치는 내 손패와 같은 **스플라인 부채꼴**로 통일 — 양 끝 (±8, −4) · 가운데 (0, 0), 1/10 간격(한 칸 약 10°). 10-ui 4절 「상대 손패」에 수치를 적음.
- 뒷면은 서로 구분되지 않으므로 `handCount` 만 맞춘다 — 늘면 덱 자리에서 날아오고(1초 OutQuart) 줄면 맨 뒤를 지운다. 상대의 뽑기도 큐 단계에서 `dealInterval`(0.15초)만 기다린다 — 첫 분배 때 한 장씩 날아온다.

### 한 것
- 새 파일 `OpponentHandView.cs`(SplineContainer · `SetCount`). `OpponentSeatView.SetHandCount` 가 숫자 상자와 부채꼴을 같이 맞춘다. `OpponentSeatsPresenter.DrawRoutine(actor, handCount)` 추가, `GameScreenPresenter` 의 CardDrawn 단계가 상대면 이것을 기다린다.
- 빌더: `BuildOpponentHand`(캐릭터 상자 아래 앵커 · 스플라인 3점), `EnsureBackCardPrefab` → `Assets/Prefab/UI/GameScreen/HandCard_Back.prefab`(있으면 덮어쓰지 않음). GameCanvas 다시 조립 · 저장.
- 확인(2클론): 시작 때 정면 좌석 `hand=5 backs=5`, 상대가 카드를 내면 `4 / 4`, 상대 쪽에서 본 내 좌석도 5장. 퇴장하면 좌석과 함께 숨김. 콘솔 에러 0. 캡처로 좌석 2 아래 파란 뒷면 5장 → 4장 부채꼴 확인.
- 3클론(좌석 1 · 2) 확인은 못 함 — clone_1 이 준비되지 않았다.

### 다음 할 일
- [ ] 3클론으로 좌석 1 · 3 의 부채꼴 위치 확인
- [ ] 제안: `OpponentHandView.Layout` 이 `HandView.Layout` 과 같다 — 세 번째 부채꼴이 생기면 배치 계산을 한곳으로. `dealInterval` 도 `HandPresenter` · `OpponentSeatsPresenter` 두 곳
- [ ] 옛 `Assets/Prefab/OtherPlayerHand` · `CardBack`(없는 스크립트) 은 안 쓴다 — 지울지 결정
- [ ] 캐릭터 그림 · 함정 슬롯 · 대상 선택 탭 (보류 중)

---

## 2026-11-03 · 상대 좌석 — 닉네임 · HP · 손패 수 · 턴 외곽선 (캐릭터 · 함정 · 부채꼴 보류)

### 결정 (사용자 요청)
- 좌석은 이름 줄 · HP 줄 · 턴 외곽선까지. 캐릭터 · 함정 슬롯 · 뒷면 부채꼴 · 대상 선택 탭은 보류, 자리만 비워 둔다. 찹츄 배지는 자리만 두고 꺼 둔다(선언 이벤트 없음, 기능 8).
- 턴 외곽선은 이름 줄 + HP 줄(위 82)만 감싼다 — 캐릭터를 덮지 않는다 (사용자 요청). 10-ui 2-1 좌석 슬롯에 반영.
- 좌석 인덱스 ↔ actorNumber 계산은 `OpponentSeatsPresenter` 한 곳 (02-player 3절). 퇴장은 `RoomEvents.OnPlayerLeft` 로 받아 좌석을 숨김 (02-player 6절) — 게임 이벤트가 아니라 큐 밖.
- HP 20 이하 주황(10-ui 2-1 HP 줄 **제안**)은 예전 뷰(e49831f)대로 구현해 둠. 확정되면 제안 표기를 지운다.

### 한 것
- 새 파일 `OpponentSeatView.cs`(e49831f 의 것에서 캐릭터 · 함정 · 부채꼴 · 탭 제거) · `OpponentSeatsPresenter.cs`. `GameScreenPresenter` 가 HpChanged · CardDrawn · CardPlayed · TurnStarted 를 내 것이면 내 패널, 아니면 좌석으로 보낸다. `GameScreenView` 에 좌석 배열.
- 빌더: `BuildOpponentSeats` 되살림(이름 줄 · HP 줄 · 턴 외곽선만), `WireArray` 복구. GameCanvas 다시 조립 · 저장.
- 확인(2클론): 상대가 정면(2번, 주황) 좌석에 닉네임 · HP 100 · 손패 5, 1 · 3번 숨김. 공격 뒤 상대 쪽에서 본 내 좌석 HP 80 · 바 0.80, 카드 낸 쪽 손패 4. 초록 외곽선이 턴 주인 좌석에만. 클론을 멈추자(퇴장) 좌석이 사라짐. 콘솔 에러 0.
- 좌석 영역(위 124 ~ 470)과 덱(중심 540, 높이 259 → 위 410)이 60 겹친다 — 11-03 "덱 위로" 조정 때 생긴 것. 지금은 좌석 아래쪽이 비어 있어 턴 외곽선만 덱 위를 지난다. 캐릭터 · 함정을 넣을 때 덱 위치나 좌석 높이를 다시 정해야 한다.

### 다음 할 일
- [ ] 좌석 영역과 덱 겹침 60 — 10-ui 2-1 「배치」 덱 중심 y 또는 좌석 높이 결정
- [ ] 보류한 캐릭터 · 함정 슬롯 · 뒷면 부채꼴(`HandFanAnchor`) · 대상 선택 탭 — e49831f 의 `OpponentSeatView` · 빌더 코드 참고
- [ ] 찹츄 선언 이벤트가 생기면 `OpponentSeatView.SetChapchu` 연결

---

## 2026-11-03 · 연출 큐(`GameScreenPresenter`) · 내 패널에 닉네임 · HP · 턴 · 남은 초 · 손패 수 반영

### 결정
- 영역 프레젠터가 각자 `GameEvents` 를 구독하던 것을 **중앙 프레젠터 하나가 구독 → 연출 큐 → 영역 프레젠터 호출**로 바꿈 (CODE_CONVENTION 9절, 사용자 결정). 순서는 받은 순서 그대로(GameServer 가 쌓은 순서 = PunGameServer 가 보낸 순서 = 같은 보낸이의 RPC 도착 순서)라 큐가 따로 정하지 않는다. 묶음은 하나만: 내가 뽑을 때 `OnMyCardDrawn`(내용) 다음 `OnCardDrawn`(장수) → 한 단계.
- 큐를 타지 않는 것: 내 요청 거절 토스트 · 남은 초(매 프레임) · 늦게 열렸을 때의 CustomProperties 복구. CODE_CONVENTION 9절에 적음.
- 뽑기 단계는 카드가 날아오는 1초를 다 기다리지 않고 `dealInterval`(0.15초) 만 기다린다 — 첫 5장에 5초가 걸리는 것을 피함. 연출을 끝까지 기다리는 단계는 아직 없다.
- 남은 초는 **내 턴에만** (03-turn 7절대로, 사용자 결정). 남의 턴엔 턴 태그를 「상대 턴!」 파랑 바탕으로 — 10-ui 2-1 턴 줄에 반영. 닉네임이 비면 `P{actorNumber}` (사용자 결정).
- 찹츄 버튼은 손패 10장이면 활성 모양까지만. 탭하면 경고 로그 — 선언 요청 API 는 기능 8.

### 한 것
- 새 파일 `UI/GameScreen/GameScreenPresenter.cs`(큐 · 6개 이벤트 구독) · `MyPanelPresenter.cs`(닉네임 · HP · 손패 수 · 찹츄 상태 · 턴 태그 · 남은 초 · Start 스냅샷). `DeckPresenter` · `HandPresenter` 는 구독을 떼고 `ShowDeckCount` · `DrawRoutine` · `ShowCardPlayed` 로. `RejectToastPresenter` 는 그대로(큐 밖).
- 머지 후 컴파일 오류 — develop 에서 `GameEvents` 네임스페이스가 `Chapchu.Network.Events` 로 옮겨져 프레젠터 3개의 using 수정.
- 빌더: `BuildMyPanel` 에 `MyPanelPresenter`, 캔버스 루트에 `GameScreenPresenter` 를 붙이고 영역 프레젠터 연결. `GameScene` 의 GameCanvas 를 지우고 다시 조립해 저장.
- 확인: `multi-test multi 2` 통과(콘솔 에러 0). 별도로 2클론 플레이 중 `MyPanelView` 값을 리플렉션으로 읽고 캡처 — 닉네임 `|Player#1` / `|Player`(Photon NickName 그대로), HP 100 → 맞은 쪽 80(바 0.80), 턴 주인은 「나의 턴!」 주황 + 초 14 → 8, 상대는 「상대 턴!」 파랑 + 초 숨김, 턴이 바뀌면 서로 바뀜, 카드 사용 뒤 5/10장 → 4/10장.
- 큐 동작 확인: GameScene 을 혼자 플레이로 띄워 뽑기 3장 → HP 50 → 턴 시작을 한 프레임에 주입하고 뽑기 간격을 2초로 늘림 — 손패가 0 · 2 · 4초에 1 → 2 → 3장, HP 는 세 장이 다 나온 뒤(6초)에야 50. 앞 연출이 끝날 때까지 뒤 단계를 안 꺼내는 것 확인 (스크래치 `QueueProbe.cs`, 커밋 안 함).
- 참고: 캡처를 `Assets/Temp` 에 저장하면 클론 에디터가 임포트에 걸려 Photon 이 타임아웃된다 — 캡처는 메인에서만, 저장 뒤 바로 지울 것. `docs/mockups` 의 인게임 HTML 시안에는 「상대 턴!」 이 없다 — 시안을 손볼 때 같이.

### 다음 할 일
- [ ] 버림 더미 · 히스토리가 생기면 `GameScreenPresenter` 에 단계 추가 (상대 좌석은 11-03 에 들어감)
- [ ] 큐가 N개 이상 밀리면 연출 건너뛰기 — 끝까지 기다리는 연출(피해 등)이 생길 때
- [ ] `MultiTest.Join` 이 `IsConnectedAndReady` 만 보고 눌러 NameServer 단계에서 `JoinOrCreateRoom failed` 가 가끔 난다 — `NetworkClientState == ConnectedToMasterServer` 로 바꿀 것

---

## 2026-11-03 · 인게임 배치 조정 — 내 패널 축소 · 히스토리 연장 · 손패 가운데 · 카드 확대 · 덱 위로

### 결정 (사용자 요청)
- 내 패널을 히스토리(폭 240)보다 조금 크게: 시안 기준 수치를 × 0.65 → 300 × 248. 구성은 그대로.
- 히스토리 아래 끝을 내 패널 위 28 로 → 위아래로 길어짐 (아래 444 → 312).
- 손패를 화면 가운데로: 손패 영역 좌우 356 (내 패널 오른쪽 끝 336 + 20). 부채꼴 반폭 664 → 520, 양 끝 낙차 43 → 34 (한 칸 1.5° 유지).
- 카드 188 × 280 → 226 × 336 (× 1.2), 손패 영역 높이도 336. 덱 중심 y 621 → 540 (손패 위 약 46 여백).

### 한 것
- 빌더 수치 반영(`MyPanelScale` · `MyPanelSize` · `HandAreaSideInset`), `BuildChapchuBadge` 에 크기 비율 인자. 씬은 `GameScene` 을 추가로 열어 내 패널 · 손패 · 카드 프리팹만 다시 만들고 위치값만 고쳐 저장 (사용자가 연 `DebugLobbyScene` 은 그대로).
- 플레이 모드 확인: 5 · 10장 캡처. 10장일 때 카드 왼쪽 끝 ≈ 350 > 내 패널 오른쪽 336 — 겹치지 않음.

### 다음 할 일
- [x] 카드 지글거림 — 밉맵은 효과가 없어 되돌림(사용자 확인). 가장 큰 원인은 에디터 Game 뷰가 1920 × 1080 을 0.54 배로 줄여 보여 주는 것(1x 로 볼 것). 1:1 에서도 남던 기울어진 가는 선 계단은 카드 틀 3장 · `Sprites/Cards/Images` 26장의 **최대 크기 2048 → 512** 로 줄임(틀 344 × 512 · 그림 512 × 512, 화면 축소 3.6 → 1.5 배). 같은 장면 1:1 전후 캡처로 테두리 · 외곽선이 고르게 된 것 확인. 카드를 크게 보여 줄 일이 생기면 다시 올린다
- [ ] 내 패널 글자(13 ~ 34)가 더 작아졌다 — 15-screen 최소 30 과 함께 QA

---

## 2026-11-02 · 내 손패 표시 — 스플라인 부채꼴 · 종류별 카드 틀 프리팹

### 결정
- 범위는 표시(뷰)와 서버 결과 연결(프레젠터)까지. 입력(HandMode · 호버 · 드래그 · 드롭)은 다음 작업 — 드롭 영역 수치가 없고 대상 선택용 상대 좌석을 빼 둔 상태.
- 카드 종류 색은 `CardType` 기준: 행동 파랑 · 카운터 초록 · 함정 주황 (사용자 결정). `10-ui.md` 2-1 색표의 「공격 · 칠와와」를 고쳤다.
- 카드는 `Assets/Prefab/PlayerActionCard Dummy` 와 같은 구성(틀 → 그림 → 이름 · 설명)을 UGUI 로 옮긴 프리팹 3개(`Assets/Prefab/UI/GameScreen/HandCard_Action|Counter|Trap`). 종류마다 틀 이미지(`Sprites/Cards/ActionCard|CounterCard|TrapCard.png`)만 다르고, 그 위에 `CardData` 의 이름 · 그림 · 설명을 입힌다. 위치는 더미의 월드 좌표를 틀 크기 비율로 옮긴 값. 처음 만든 임의 디자인(색 테두리 · 띠)은 사용자 요청으로 버렸다.
- 더미의 설명 글자는 흰색인데 흰 바탕이라 잉크로 바꿨다. 글꼴은 `10-ui` 2-1 대로 Galmuri (더미는 둥근미소).
- 카드 188 × 280(틀 비율 · 손패 영역 높이), 부채꼴은 시안처럼 한 칸 약 1.5° 인 원호 — 수치는 `10-ui.md` 2-1 「손패」. 옛 `origin/upgrade/unity6` `PlayerHandView.PutAwayMyCards` 의 배치 계산(1/10 간격, 10장 초과 시 축소, 1초 OutQuart)을 UGUI 2D(z 회전 · 형제 순서)로 옮겼다.
- 옛 `Assets/Prefab/PlayerActionCard` 등은 SpriteRenderer · 없는 스크립트 기반이라 쓰지 않고 그대로 뒀다.

### 한 것
- `UI/GameScreen/`: `HandCardView`(한 장 표시) · `HandView`(SplineContainer 부채꼴, 추가 · 제거 · 배치) · `HandPresenter`(`OnMyCardDrawn` → 덱 에셋으로 CardData → 덱 더미 자리에서 추가, 내 `OnCardPlayed` → 제거).
- 빌더: `BuildHand`(스플라인 · 뷰 · 프레젠터, 덱 에셋은 씬의 `PunGameServer.deck`), `EnsureHandCardPrefab(이름, 틀 경로)`(있으면 덮어쓰지 않음 — 다시 만들려면 프리팹을 지우고 실행). 씬은 `BuildHand` 만 CLI 로 실행.
- 플레이 모드 확인(혼자 방장): 첫 5장이 덱에서 날아와 부채꼴, 덱 탭으로 10장(1/10 간격) · 13장(간격 축소) 캡처. 내 카드 사용 승인 이벤트(흉내)로 13 → 5장, 남의 이벤트는 무시. 카운터(카드 2) · 함정(메모리 임시 데이터) 틀 이미지 확인.
- 카드 4(「찹츄 야호~」)의 그림 참조(guid `18436c33…`)는 프로젝트에 없는 파일이라 그림 칸이 빈다 — 데이터 문제.

### 다음 할 일
- [ ] 손패 입력 — HandMode · 호버 · 드래그 · 드롭 → `RequestPlayCard` (10-ui 4 · 6 · 9절). 드롭 영역 수치 · 상대 좌석 먼저
- [ ] 2클론에서 실제 카드 사용 승인 → 손패에서 빠지는지 (혼자서는 대상 카드가 거절됨)
- [ ] 씬이 늦게 열리면 첫 5장을 놓친다 — 내 손패 내용은 스냅샷이 없다 (09-network 6.1 확인 필요)
- [ ] `Assets/Cards/Prototype_004_ACT` 그림 파일 다시 연결

---

## 2026-11-02 · 덱 더미 탭 → 드로우 요청 · 덱 잔여 장수 · 거절 토스트

### 결정
- 버튼 대신 시안의 덱 더미를 누르면 드로우를 요청한다. `DeckView`(`e49831f`)를 되살렸다 — 더미 전체가 `Button`.
- 요청과 표시는 따로 흐른다. 탭 → `PunGameServer.RequestDraw()`(매개변수 없음, 요청자는 방장이 `info.Sender` 로). `DRAW n` 의 n = 덱 잔여 장수(05-deck 9절)로, `OnCardDrawn`(누가 뽑든) · `OnDeckRefilled` · 시작 때 `RoomProps.DeckCount` 로만 갱신 — 요청 뒤 미리 줄이지 않는다.
- `IGameRequests` 는 2026-10-29(`8b0ab0a`) 옛 코드와 함께 빠졌다. 지금은 `PunGameServer` 를 `[SerializeField]` 로 직접 받는다 (`GameDebugInput` 과 같음).
- 거절 문구는 옛 `RejectText` 그대로 → `10-ui.md` 8-2 로 확정, 11절 #7 닫음. 요청 종류와 무관한 `RejectToastPresenter` 하나가 받는다.
- 덱 위치는 시안대로(중심 972, 621) — 오른쪽에 버림 더미 자리를 비워 둔다. 나중에 바꿔도 된다.
- 씬은 재조립하지 않고 빌더의 `BuildDeck` 만 CLI 로 실행해 기존 캔버스에 넣었다.

### 한 것
- `UI/GameScreen/`: `DeckView` 복구, `DeckPresenter` · `RejectText` · `RejectToastPresenter` 추가. `GameScreenView.deck` 복구.
- 빌더: `BuildDeck`(프레젠터 · 씬의 `PunGameServer` 연결), `GameCanvas` 에 `RejectToastPresenter`. 덱 글자 기본값 `DRAW -`.
- 플레이 모드 확인(혼자 방을 만들고 `LoadLevel` 로 방장 시작): 덱 40 → 분배 뒤 `DRAW 35` → 덱 클릭 → `DRAW 34` · 손패 6장. 손패에 없는 카드 요청 → 방장 거절 코드 2 → 토스트 오브젝트가 「손패에 없는 카드입니다.」로 생성됨.
- **확인 못 함**: 토스트가 화면에 보이는지. CLI 캡처에 팝업 캔버스(`DontDestroyOnLoad`)가 찍히지 않았다 — 오브젝트 · 위치 · 알파는 정상.

### 다음 할 일
- [ ] 에디터 Game 뷰에서 거절 토스트가 보이는지 눈으로 확인
- [ ] 2클론: 남의 턴에 덱 탭 → 「내 턴이 아닙니다.」, 상대가 뽑을 때 내 `DRAW n` 감소

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
- [x] 상대 좌석 · 덱 · 버림 더미 UI 를 다시 만들 때 `10-ui.md` 2-1 기준으로 빌더에 되살린다 (`e49831f` 참고) — 덱 11-02, 좌석 11-03. 버림 더미는 아직

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
- [x] `GameScreenPresenter` — `GameEvents` 구독 → 연출 큐 → 영역 프레젠터 (11-03). 좌석 인덱스 ↔ actorNumber 는 상대 좌석을 만들 때
- [ ] 입력 연결: `DeckView.DrawClicked` → `RequestDraw`, 좌석 `Clicked` → 대상 선택, `OnMyRequestRejected` → `MessageToast`. 찹츄 버튼 · 승리 오버레이는 요청 API 가 생기면 (기능 8)
- [ ] 내 손패(`HandArea`) · 상대 뒷면 부채꼴(`HandFanAnchor`) · 히스토리 항목 프리팹(`HistoryView.AddEntry`)
- [ ] 글자 22 ~ 28 의 모바일 가독성 보정 (`15-screen` 최소 30) — `12-title-ui` 4-3 방식으로 결정
- [ ] 디자인 그림이 오면 `CharacterImage` · 카드 앞면 스프라이트 연결
- [ ] `GameDebugInput`(D · P 키)과 화면이 같은 값을 보이는지 2클론으로 확인

---

## 2026-11-02 · 로비 코드 컨벤션 (`feature/lobby-screen`)

### 한 것
- `LobbyPresenter`: 한 줄 `if … return;` 2곳, 실패 문구를 switch 식으로, 코드 입력 팝업 구독을 람다 → `HandleSubmitted` (`+=` · `-=` 짝).
- `LobbyView`: 요청 이벤트 3개 XML 주석.
- 로비가 쓰는 공용 팝업 5개(`InputPopup` · `WarningPopup` · `LoadingPopup` · `Popup` · `PopupManager`): 기준 문서(`10-ui` 8절), public 멤버 주석, 한 줄 `if` 3곳 · 한 줄 `{ }`. `LoadingPopup` 요약의 옛 계획서 문구 정리.
- `LobbyScene` 하이어라키는 `14-lobby-ui` 7-1 과 같아 씬은 그대로.
- 확인: `multi-test ui` 통과 (없는 방 코드 경고 문구 08-room 7절대로, 콘솔 에러 0).

### 다음 할 일
- [ ] `LobbyPresenter.Start` 가 아직 `SceneFlow.ReturnSceneAfterRoom` 을 정한다 — 11-01 할 일 "방 나가기는 항상 LobbyScene" 과 같이 지운다
- [ ] `ProjectSettings.asset` · `EditorPipelineConfig.json` 이 테스트마다 변경으로 뜬다 — Unity 는 LF 로 쓰는데 `.gitattributes` 가 CRLF 를 요구해서 (내용은 같음). `ProjectSettings/` 를 `eol=lf` 로 둘지 결정

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
- [ ] 타이틀 디자인 시안 고르기 — 같은 캔버스 "UI 시안" 페이지. 배치는 가운데 묶음으로 정하고, 현재안(스티커) + 생김새가 다른 6안: A 아케이드 간판 · B 카드 테이블 · C 만화 컷 · D 콜로세움 깃발 · E 출입증 · F 큰 글자 미니멀 (색만 바꾼 5안은 비슷해서 지움). 게임 화면은 따로 작업한다
- [ ] 로비 시안 확인 — 같은 페이지 아래 「로비」 1장 (① 디자인). `14-lobby-ui` 범위 안: 닉네임 이름표 · 버튼 3개(아이콘 + 한 줄 설명) · 방 코드 팝업 · 요청 중 · 실패 안내(08-room 7절 문구) · 방으로 이동. 버튼 안 한 줄 설명은 기획서에 없는 문구 — 확정 시 14 에 넣는다
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
