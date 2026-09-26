# B 트랙 — UI 작업 플랜

**작성일**: 2026-09-24 · **갱신**: 2026-09-26 (v5 — 작업 목록을 `development-plan.md` 3절 기능별 표로 옮김)
**담당**: UI 담당 (B)
**소유 폴더**: `Presentation/`, `UI/` + **모든 `.unity` / `.prefab` 단독**
**상위 문서**: [`development-plan.md`](development-plan.md) v3 (6인 팀 · M1~M8 단계 · 게이트 · 동기화 지점 · 디자인 납품 D1~D12) · [`refactoring-plan.md`](refactoring-plan.md) (진단 번호 C-)
**짝 문서**: [`plan-a-logic.md`](plan-a-logic.md)
**세부 문서** (⚠ `docs/title-ui` 브랜치, develop 미머지):
[`title-ui-plan.md`](title-ui-plan.md) (타이틀 S1~S6) · [`ui-refactoring-plan.md`](ui-refactoring-plan.md) (인게임 외 UI 코드 PR1~PR6, 진단 번호 U-)

---

## 0. 결론 먼저

B 의 일은 두 갈래다.

| 갈래 | 범위 | 기준 문서 | A 의존 |
| --- | --- | --- | --- |
| **B-아웃게임** | 타이틀 · 로비 · 방 · 팝업 | `title-ui-plan.md`, `ui-refactoring-plan.md` | 거의 없음 (요청 3건) |
| **B-인게임** | 좌석 · 손패 · 카드 · 덱 · 턴 · 입력 · 함정 · 체인 | 이 문서 2절 | **`FakeGameServer` 로 끊는다** |

B 를 A 로부터 떼어내는 장치는 하나다.

> **인게임 UI 는 전부 로컬 서버(`FakeGameServer`)에 붙여 만든다.** (v4: 서버 1층이 나오면 흉내를 버리고 진짜 1층을 에디터에서 돌린다 — 규칙이 두 벌이 되지 않는다)
> `FakeGameServer` 는 `IGameRequests` 를 구현하고 `GameEvents` 를 발행한다. Photon 없이 에디터 1개로 4인 상황을 재현한다.
> A 의 실제 구현체가 머지되면 **교체 PR 한 번**으로 갈아끼운다. `FakeGameServer` 는 이후에도 버리지 않는다.
>
> **2026-09-25 현황**: `DebugTools/FakeGameServer.cs` 골격 완료 — 실제 `IGameRequests` · `IGameState` 구현, Start 에서 4인 HP 100 · 손패 5 · Alive · 첫 턴을 `GameEvents` 로 전파, 턴 종료 순환. 드로우 · 카드 사용은 A1-1 계약 후.
> `Practice/` 폴더(계약 복사본)는 접었다. `TurnView` · `TurnPresenter` 는 `Presentation/Turn/` 으로 옮겼고 옛 `TurnUI` 는 삭제. `IGameState` 는 `Game/` 에 계약으로 추가 (A 리뷰 대기).

B 가 지키는 규칙:

1. **규칙 판정 · 상태 변경 코드를 쓰지 않는다.** "이 카드를 낼 수 있나?"를 UI 가 계산하지 않고, 요청 후 `OnRequestRejected` 로 받는다.
   (예외: 찹츄 버튼 활성처럼 **표시용** 조건. 최종 판정은 마스터)
2. **`GameEvents` 로 받은 값만 표시한다.** `CustomProperties` 를 직접 읽는 코드는 교체 PR 에서 정리한다.
3. 계약 3종 변경이 필요하면 직접 고치지 않고 A 에게 PR 리뷰를 요청한다.

---

## 1. 현재 코드 상태 (2026-09-24 확인)

### 1-1. B 소유 파일에 남아 있는 로직 (교체 PR 에서 제거)

A 가 `Game/` 에 새 경로를 만들면 B 가 옛 호출을 지운다 (`plan-a-logic.md` 1-3).

| 파일 | 제거할 것 |
| --- | --- |
| `Presentation/Deck/DeckPresenter` | 드로우 RPC 전부, `"RoomDeck"` 동기화, `OnRoomPropertiesUpdate`, `Start()` 의 초기 배분 → `IGameRequests.RequestDraw()` 호출만 남긴다 |
| `Presentation/Hand/PlayerHandPresenter` | `handCount` `SetCustomProperties` 기록 |
| `Presentation/Player/PlayerPresenter` | `Init()` 의 HP 프로퍼티 기록 |
| `Presentation/Card/CardPresenter` | `CardPlayManager` 직접 호출 → `IGameRequests.RequestPlayCard()`. `async void OnCardDropped` (C-11) |

### 1-2. 인게임 UI 버그 (B 가 바로 고칠 수 있는 것)

| # | 위치 | 내용 |
| --- | --- | --- |
| C-11 | `CardPresenter.OnCardDropped` | `async void` — 예외가 삼켜짐 |
| C-12 | `TargetSelectionManager.SelectPlayer` | 취소 토큰 없는 `Task.Yield` 루프 |
| C-13 | `PlayerSeat.SetHpGauge` / `SetCardCountUI` | 호출처 없음 → HP · 손패 UI 미동작 |
| C-14 | `PlayerSeat.OnEnable` | 비활성 상태에서 `rect.width` 측정 |
| C-16 | `SeatManager` + `HandSeatManager` | 둘 다 `Start()` 에서 좌석 계산 |
| C-17 | `OtherPlayerHandPresenter` | `handCount > realCount` 보정 없음 |
| C-19 | `TargetSelectionManager` · `ClickManager` | 자체 싱글톤 구현 |
| C-21 | `ClickManager` | 취소가 마우스 우클릭 → 모바일 불가 |
| D | `PlayerHandView` ↔ `OtherPlayerHandView` | `PutAwayMyCards` 45줄 중복 |

---

## 2. 작업 목록

**`development-plan.md` 3절 "기능별 작업"의 UI 열이 원본이다** (v5, 2026-09-26). 기능 13개를 순서대로.

**지금 바로 할 수 있는 것** (로직을 기다리지 않음): 기능 1 의 인게임 씬 재구성 · 영역 배치 · 배치 문서 `systems/17-game-ui.md`.

1-2절의 인게임 버그(C- · D)는 해당 기능에서 함께 고친다.

| 진단 | 고치는 기능 |
| --- | --- |
| C-13 · C-14 좌석 체력 · 장수 미연결 · 게이지 폭 | 1 게임 시작하기 (좌석) |
| C-16 좌석 계산 2중 · C-17 장수 보정 · D 손패 정렬 중복 | 1 게임 시작하기 (좌석 · 손패) |
| C-11 `async void` · C-12 취소 토큰 · C-19 자체 싱글톤 · C-21 우클릭 취소 | 4 카드 내기 (입력) |

**아웃게임** — ✅ 전부 완료: 팝업 인프라 · 타이틀 · 코드 정리 · 씬 전환 · 타이틀 접속 · 화면 규격 통일 · 로비 · 방. 남은 것은 디자인 입히기(기능 10)와 모바일 실기 확인.

## 2-1. B 재기획 (2026-09-25 제안)

타이틀 · 로비 · 방 · 팝업의 **코드 정리(PR1~PR6)는 끝났다.** 남은 것은 (1) 화면 규격 통일, (2) 방 화면 재구성, (3) 디자인 교체 — 순서대로 간다. 인게임(M1 이후)은 A1-1 대기 그대로.

| 순서 | 작업 | 선행 조건 | 규모 | 내용 |
| --- | --- | --- | --- | --- |
| 1 | **화면 규격 확정** | — | ✅ 2026-09-25 승인 | 가로 고정 · Expand (`15-screen.md`) |
| 2 | **B1-14 캔버스 통일** | ✅ `feature/canvas-unify` | 0.5d | `ProjectSettings` 방향 · Safe Area, 씬 5곳 Canvas 를 15-screen 3절로. 타이틀 · 로비는 Match 1 → Expand 만, 방 · 팝업 · 인게임은 Constant Pixel Size → Scale With Screen Size. 공용 `SafeArea` 컴포넌트 1개 |
| 3 | **방 화면 재구성 (뷰)** | ✅ `feature/room-rework` — `16-room-ui.md` · `RoomView` + `RoomPresenter` | 1d | 로비와 같은 방식 — 배치 규칙 문서(`16-room-ui.md`) 신설 → 디자인 없이 기본 스프라이트로 씬 재구성. 요소: 방 코드(크게, 복사 가능) · 플레이어 목록 4칸(닉네임 · 방장 표시) · 인원 · 시작(방장) · 나가기 · 로그. U-24 `RoomPlayerListView` 도 여기서 |
| 4 | **팝업 재구성 (뷰)** | 3 | 0.5d | `WarningPopup` · `InputPopup` · `LoadingPopup` · `MessageToast` 를 15-screen 규격 · 타이틀 박스 스타일(둥근 사각 9-slice · 학교안심 폰트)로. 기능 코드는 그대로 |
| 5 | **디자인 교체** | 로비 · 방 · 팝업 디자인 아티팩트 게시 | 씬당 0.5d | 크기 · 배치 유지, 색 · 스프라이트 · 폰트 효과만 교체 (타이틀 S6 방식으로 대조) |
| 6 | 모바일 실기 확인 | 기기 확보 | — | 타이틀 · 로비 · 방 Safe Area, 터치 크기, 비행기 모드 접속 실패 흐름 |
| — | 친구 시스템 (`13-friend`) | — | — | **MVP 밖 유지** (README 분류 Optional). M5 이후 별도 마일스톤 |
| — | 설정 · 사운드 · 메뉴 버튼 | — | — | 기능 미정 → 계속 제외 (`14-lobby-ui` 3절 #1) |

> 2026-09-25 사용자 승인으로 1 · 3 의 결정 완료. 1 · 2 · 3 완료. 남은 순서 4 → 5 → 6. 단, **인게임 B1-0 · B1-2 · B1-3 이 4 보다 먼저다** (M1 게이트가 인게임이다).

## 3. B 가 A 에게 요청한 항목

| # | 요청 | 원 번호 | 필요 시점 | 상태 |
| --- | --- | --- | --- | --- |
| R-1 | `RoomEvents.OnMasterClientSwitched` 추가 | U-12 | PR6 | ✅ A 완료 (2026-09-24) |
| R-2 | `PhotonConnection.SetupInitNickname()` 삭제 | U-7 | PR4 | ✅ A 완료 (2026-09-24) |
| R-3 | `Initialize()` 조기 반환 + `OnDisconnected` 분기 | U-6, U-23 | PR6 **전 필수** (A0-1, A0-2) | ✅ A 완료 (2026-09-24) |

> **B 가 이어서 할 것** — 상세는 [`plan-a-logic.md`](plan-a-logic.md) 3-1.
> * **A0-3**: A 브랜치(`temp/a-network-outgame`) **머지 후** `RoomPanel.Awake` 의 `AutomaticallySyncScene` 삭제
> * **PR4**: `ConnectionEvents.OnDisconnected` 구독 → 안내 + 재시도(`NetworkManager.Connect()`). 인게임 끊김도 같은 이벤트로 온다
> * **PR6**: `RoomEvents.OnMasterClientSwitched` 구독

---

## 4. B 가 하지 않는 것

* 규칙 판정 · 상태 변경 (M4 효과 구현 제외)
* 계약 3종을 단독으로 수정
* `FakeGameServer` 안에 진짜 규칙 구현 — 흉내만 낸다. 규칙이 두 벌이 되면 반드시 갈라진다
* 씬 레이아웃 변경과 코드 리팩토링을 한 PR 에 섞기
* 계약을 복사한 연습용 네임스페이스 만들기 (옛 `Chapchu.Practice` — 2026-09-25 접음)
