# B 트랙 — UI 작업 플랜

**작성일**: 2026-09-24
**담당**: UI 담당 (B)
**소유 폴더**: `Presentation/`, `UI/` + **모든 `.unity` / `.prefab` 단독**
**상위 문서**: [`development-plan.md`](development-plan.md) (마일스톤 · 게이트 · 동기화 지점) · [`refactoring-plan.md`](refactoring-plan.md) (진단 번호 C-)
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

> **인게임 UI 는 전부 `FakeGameServer`(로컬 모의 마스터)에 붙여 만든다.**
> `FakeGameServer` 는 `IGameRequests` 를 구현하고 `GameEvents` 를 발행한다. Photon 없이 에디터 1개로 4인 상황을 재현한다.
> A 의 실제 구현체가 머지되면 **교체 PR 한 번**으로 갈아끼운다. `FakeGameServer` 는 이후에도 버리지 않는다.

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

## 2. 마일스톤별 작업

표기: **[계약]** = A 와 공동 리뷰 · **←A** = A 의 산출물이 있어야 시작 · **⛔** = 기획 미정으로 막힘

### M0 잔여 (이번 주)

| # | 작업 | 비고 |
| --- | --- | --- |
| B0-1 | **[계약]** A 의 `OnDrawn` 분리 PR(A1-1) 리뷰 | `FakeGameServer` 가 이 계약을 쓴다 |
| B0-2 | 접속 대기 중 `LoadingPopup` 표시 | ✅ 해소 — 타이틀은 버튼 글자 "접속 중…"으로 대기 표시 (`12-title-ui.md` 6절). `LoadingPopup` 은 방 참가 대기(PR5)에 쓴다 |

### M1 — UI 재연결 (2주)

인게임과 아웃게임을 섞어 진행한다. **PR1(팝업 인프라)은 M1 안에 반드시 끝낸다** — M2 의 "카드 사용 거절 사유" 토스트가 그 위에 올라간다.

**B-인게임**

| # | 작업 | 진단 | 비고 |
| --- | --- | --- | --- |
| B1-1 | `FakeGameServer` — `IGameRequests` 구현 + `GameEvents` 발행, 가짜 4좌석 | — | ←A1-1(계약). 셔플 · 판정은 흉내만 (진짜 규칙 구현 금지) |
| B1-2 | `PlayerSeat` HP 게이지 · 손패 장수 연결, 게이지 폭 측정 시점 | C-13, C-14 | `OnHpChanged` · `OnHandCountChanged` 구독 |
| B1-3 | 좌석 계산을 `SeatManager` 한 곳으로 | C-16 | |
| B1-4 | `PutAwayMyCards` 공통화 | D | |
| B1-5 | 손패 장수 초과 보정 | C-17 | |
| B1-6 | 입력 재작성: 우클릭 취소 → 모바일 입력, `TargetSelectionManager` 를 `Singleton<T>` + 취소 토큰 | C-12, C-19, C-21 | |
| B1-7 | `CardPresenter.OnCardDropped` `async void` 제거 | C-11 | |
| B1-8 | **교체 PR** — `FakeGameServer` → A 의 실제 구현체, 1-1 의 옛 로직 제거 | — | ←A1-5. 동기화 지점 S2 |

**B-아웃게임** (`ui-refactoring-plan.md` · `title-ui-plan.md`)

| # | 작업 | 원 번호 | 비고 |
| --- | --- | --- | --- |
| B1-9 | 팝업 인프라 복구 (모달 잔재, `Open()` 미호출, 토스트 불가) | PR1 (U-1, U-2, U-3, U-11, U-20) | ✅ #15 · #17 |
| B1-10 | 타이틀 화면 재구성 | `title-ui-plan` S1~S6 | ✅ #14 (PC 확인 완료, 모바일 실기 보류) |
| B1-11 | 죽은 코드 · 폴더 · 네임스페이스 정리 | PR2 | ✅ `refactor/ui-cleanup` |
| B1-12 | 씬 전환 형식 통일, `async void` 제거 | PR3 | ✅ `feature/scene-loader` — `ScenePaths` 이름 상수, 래퍼 없음 (`async void` 는 `NicknameInput` 삭제로 이미 없음) |
| B1-13 | 타이틀 로직 연결 (접속 실패 재시도, 닉네임 복원) | PR4 | 세부 플랜 `title-ui-plan.md` S7 — ✅ #16 (실기기 확인 보류) |
| B1-14 | Canvas 규격 통일 (Scale With Screen Size · Expand) | `15-screen.md` 9절 | ✅ `feature/screen-spec` (2026-09-25) — 씬 4 + 팝업 프리팹 Expand, `SafeArea` 컴포넌트, 가로 고정 |

> ⚠ **M1 에서 B 의 부하가 가장 크다.** 인게임 8건 + 아웃게임 6건. `development-plan.md` 원안은 인게임만 계산했다.
> 권장 순서: B1-9 → B1-1 → B1-2~B1-3 → B1-10 → 나머지. PR5 · PR6(로비 · 방)은 M2 로 넘긴다.

**B 의 M1 완료 기준**: `FakeGameServer` 로 4좌석 HP · 손패 장수 · 턴 외곽선 표시 + 교체 PR 후 4클론에서 동일 표시.

### M2 — 체인 · 함정 UI (2주)

| # | 작업 | 비고 |
| --- | --- | --- |
| B2-1 | **[계약]** A 의 체인 · 반응 마감 · 함정 · 손패 제거 이벤트 PR(A2-1) 리뷰 → `FakeGameServer` 에 반영 | |
| B2-2 | 반응 타이머 UI (5초, `PhotonNetwork.Time` 기준 마감 시각으로 로컬 계산) | ←A2-1 |
| B2-3 | 체인 표시 UI | ⛔ 기획 #2 |
| B2-4 | 카운터 사용 UI | ⛔ 기획 #2 |
| B2-5 | 함정 슬롯 UI + 설치 · 발동 인터랙션 | ⛔ 기획 #2 (UI 부분) |
| B2-6 | 카드 사용 거절 사유 표시 (`OnRequestRejected` → `MessageToast`) | ←B1-9 |
| B2-7 | 로비 · 방 정리 | `ui-refactoring-plan` PR5 · PR6 | PR6 코드 정리(U-12 · U-22 · U-23) ✅ #22. PR5 ✅ `feature/lobby-logic`. U-24 는 방 디자인 후 |

> 기획 #2 가 M1 중에 안 나오면 **M2 의 B 작업 대부분이 멈춘다.** 그 경우 B 는 PR5 · PR6 과 M3 의 확정 항목(B3-1, B3-2)을 당겨온다.

### M3 — 체력 · 턴 · 승리 UI (1.5주)

| # | 작업 | 비고 |
| --- | --- | --- |
| B3-1 | HP 위험색(1~20), 사망 대기 카운트, 최종 사망 좌석 비활성 | `OnLifeStateChanged` (이미 선언됨) |
| B3-2 | 턴 타이머 20초 표시 — 로컬 계산, 판정은 마스터 | ←A3-4 (턴 마감 시각 이벤트) |
| B3-3 | 찹츄 버튼 (손패 정확히 10장일 때만 활성 — 표시용) | `OnChapChuChanged` |
| B3-4 | 결과 화면 | ⛔ 기획 #7 |

### M4 — 카드 대량 구현 (2주, 로직 투입)

A 의 부하가 B 의 3배가 되는 구간이라 B 가 `Game/` 에 들어간다. **이 구간만 예외로 B 가 규칙 코드를 쓴다.**

| # | 작업 | 비고 |
| --- | --- | --- |
| B4-1 | 단순 파라미터 효과 — `Damage`, `Heal`, `Draw`, `Discard`, `SkipTurn` … | ←A4-1 (효과 기반 클래스 동결). **A 리뷰 필수** |
| B4-2 | 카드 59장 SO 에셋 작성 | ⛔ 카드 수치 N / M (기획 #6) |

### M5 — MVP 마감 (2주)

| # | 작업 |
| --- | --- |
| B5-1 | 모바일 빌드 · 해상도 · 터치 검증 |
| B5-2 | `RoomPanel` 나가기 → 들어온 씬으로 복귀 (`Lobby` → `Lobby`, `DebugLobby` → `DebugLobby`). `SceneFlow.ReturnSceneAfterRoom` ✅ (2026-09-25) |
| B5-3 | 설정 · 사운드 화면 ⛔ 기획 미정 |

---

## 2-1. B 재기획 (2026-09-25 제안)

타이틀 · 로비 · 방 · 팝업의 **코드 정리(PR1~PR6)는 끝났다.** 남은 것은 (1) 화면 규격 통일, (2) 방 화면 재구성, (3) 디자인 교체 — 순서대로 간다. 인게임(M1 이후)은 A1-1 대기 그대로.

| 순서 | 작업 | 선행 조건 | 규모 | 내용 |
| --- | --- | --- | --- | --- |
| 1 | **화면 규격 확정** | — | ✅ 2026-09-25 승인 | 가로 고정 · Expand (`15-screen.md`) |
| 2 | **B1-14 캔버스 통일** ✅ | 1 | 0.5d | `ProjectSettings` 방향 · Safe Area, 씬 5곳 Canvas 를 15-screen 3절로. 타이틀 · 로비는 Match 1 → Expand 만, 방 · 팝업 · 인게임은 Constant Pixel Size → Scale With Screen Size. 공용 `SafeArea` 컴포넌트 1개 |
| 3 | **방 화면 재구성 (뷰)** | ✅ `08-room` 5절 확정 (2026-09-25) | 1d | 로비와 같은 방식 — 배치 규칙 문서(`16-room-ui.md`) 신설 → 디자인 없이 기본 스프라이트로 씬 재구성. 요소: 방 코드(크게, 복사 가능) · 플레이어 목록 4칸(닉네임 · 방장 표시) · 인원 · 시작(방장) · 나가기 · 로그. U-24 `RoomPlayerListView` 도 여기서 |
| 4 | **팝업 재구성 (뷰)** | 3 | 0.5d | `WarningPopup` · `InputPopup` · `LoadingPopup` · `MessageToast` 를 15-screen 규격 · 타이틀 박스 스타일(둥근 사각 9-slice · 학교안심 폰트)로. 기능 코드는 그대로 |
| 5 | **디자인 교체** | 로비 · 방 · 팝업 디자인 아티팩트 게시 | 씬당 0.5d | 크기 · 배치 유지, 색 · 스프라이트 · 폰트 효과만 교체 (타이틀 S6 방식으로 대조) |
| 6 | 모바일 실기 확인 | 기기 확보 | — | 타이틀 · 로비 · 방 Safe Area, 터치 크기, 비행기 모드 접속 실패 흐름 |
| — | 친구 시스템 (`13-friend`) | — | — | **MVP 밖 유지** (README 분류 Optional). M5 이후 별도 마일스톤 |
| — | 설정 · 사운드 · 메뉴 버튼 | — | — | 기능 미정 → 계속 제외 (`14-lobby-ui` 3절 #1) |

> 2026-09-25 사용자 승인으로 1 · 3 의 결정 완료. 2 → 3 → 4 순서로 진행한다.

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
