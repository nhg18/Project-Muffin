# B 트랙 — UI 작업 플랜

**작성일**: 2026-09-24 · **갱신**: 2026-09-26 (v4 — M1 을 샌드박스로, 가짜 서버를 로컬 서버로 · 문서 정리)
**담당**: UI 담당 (B)
**소유 폴더**: `Presentation/`, `UI/`, `DebugTools/` + **모든 `.unity` / `.prefab` / 스프라이트 반영 단독**
**상위 문서**: [`development-plan.md`](development-plan.md) v4 (6인 팀 · M1~M8 단계 · 게이트 · 동기화 지점 · 디자인 납품 D1~D12) · [`refactoring-plan.md`](refactoring-plan.md) (진단 번호 C-)
**짝 문서**: [`plan-a-logic.md`](plan-a-logic.md)
**세부 문서** (아웃게임, 둘 다 완료 — 기록용):
[`title-ui-plan.md`](title-ui-plan.md) (타이틀 S1~S8) · [`ui-refactoring-plan.md`](ui-refactoring-plan.md) (인게임 외 UI 코드 PR1~PR6, 진단 번호 U-)

---

## 0. 결론 먼저

B 의 일은 두 갈래다.

| 갈래 | 범위 | 기준 문서 | A 의존 |
| --- | --- | --- | --- |
| **B-아웃게임** | 타이틀 · 로비 · 방 · 팝업 | `12` · `14` · `15` · `16` 기획서, 2-1절 순서 | 없음 (요청 3건 완료) |
| **B-인게임** | 좌석 · 손패 · 카드 · 덱 · 턴 · 입력 · 함정 · 체인 | `18-sandbox` · 이 문서 2절 | **로컬 서버(`FakeGameServer`)로 끊는다** |

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
3. 계약 4종(`PlayerProps`/`RoomProps` · `GameEvents` · `IGameRequests` · `IGameState`) 변경이 필요하면 직접 고치지 않고 A 에게 PR 리뷰를 요청한다.

---

## 1. 현재 코드 상태 (2026-09-26 확인)

### 1-1. B 소유 파일에 남아 있는 로직 (교체 PR 에서 제거)

A 가 `Game/` 에 새 경로를 만들면 B 가 옛 호출을 지운다 (`plan-a-logic.md` 1-3).

| 파일 | 제거할 것 |
| --- | --- |
| `Presentation/Deck/DeckPresenter` | 드로우 RPC 전부, `"RoomDeck"` 동기화, `OnRoomPropertiesUpdate`, `Start()` 의 초기 배분 → `IGameRequests.RequestDraw()` 호출만 남긴다 |
| `Presentation/Hand/PlayerHandPresenter` | `handCount` `SetCustomProperties` 기록 |
| `Presentation/Player/PlayerPresenter` | `Init()` 의 HP 프로퍼티 기록 |
| `Presentation/Card/CardPresenter` | `CardPlayManager` 직접 호출 → `IGameRequests.RequestPlayCard()` |

### 1-2. 인게임 UI 버그 (B 가 바로 고칠 수 있는 것)

| # | 위치 | 내용 |
| --- | --- | --- |
| ~~C-11~~ | `CardPresenter.OnCardDropped` | ✅ `async void` 제거 — `PlayerHandPresenter.PlayCardAsync` 가 직렬화 · 예외 로그 (2026-09-25) |
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
| B0-2 | 접속 대기 중 `LoadingPopup` 표시 | ✅ 해소 — `LoadingPopup` 은 로비의 방 참가 대기(PR5)에 쓴다. 타이틀 대기 표시는 B1-15(로딩 링)로 바뀐다 |

### M1 — 샌드박스 화면 (9/28 ~ 10/11)

기준 문서: [`systems/18-sandbox.md`](systems/18-sandbox.md). 규칙 없는 카드 테이블의 **화면 · 입력 전부**. 여기서 만든 화면은 M2 에서 규칙이 붙어도 그대로 쓴다.

| # | 작업 | 진단 | 비고 |
| --- | --- | --- | --- |
| B1-0 | `GameScene` 재구성 + 영역 배치 — 덱 · 가운데 더미 · 버림 더미 · 좌석 4(체력 · 손패 장수 · 함정 칸 3 · 찹츄 표시) · 내 손패 · 턴 표시 · 타이머 · 행동 로그 · "샌드박스" 표시. 배치 문서 `systems/17-game-ui.md` | — | **지금 시작 가능.** develop 의 옛 GameScene(인게임 클래스 14개 배치)을 가져와 고치는 것이 빠르다 |
| B1-1 | **로컬 서버** — 약속(A1-1) 직후엔 영역 이동을 흉내(에디터 1개 · 4인 · 시점 전환), 서버 1층(A1-3)이 나오면 **흉내를 지우고 `GameServer` 를 로컬로 돌리는 껍데기**로 | — | ←A1-1 (10/1) · ←A1-3 (10/6). 이름은 `FakeGameServer` 유지 가능 |
| B1-2 | **영역 화면** — 내 손패(앞면) · 상대 손패(뒷면 · 장수) · 가운데 더미 · 버림 더미 · 함정 칸(뒷면 / 공개) · 좌석 체력 게이지 · 손패 장수 · 찹츄 표시 · 대상 화살표. 전부 알림 구독 | C-13, C-14, C-16, C-17, D | 옛 B1-2 ~ B1-5(좌석 연결 · 좌석 계산 단일화 · 손패 정렬 공통화 · 장수 보정)를 여기서 같이 |
| B1-3 | **조작 입력 (모바일)** — 드래그(가운데 · 함정 칸 · 좌석 · 버림), 탭(덱 뽑기 · 가운데 치우기), 길게 누르기 메뉴(덱: 섞기 · 버림 되돌리기 / 좌석: 체력 · 가져오기 · 찹츄 · 함정 칸), 턴 넘기기 · 5초 타이머 버튼, **행동 로그** | C-12, C-19, C-21 | 우클릭 제거, 취소 토큰(옛 B1-6 · B1-7). **규칙 판정 금지** — 어떤 조작도 막지 않는다 |
| B1-4 | **카드 표 가져오기 도구** (에디터) — 표(CSV) → 카드 데이터 에셋. 처음엔 `11-card-list.md` 59장 문구를 N · M 그대로, 효과 없이 | — | 카드 숫자가 확정되면 같은 도구로 다시 가져온다 (M4 B4-2 의 앞당김) |
| B1-5 | 방 화면 "샌드박스" 토글 — 방장만, 개발 빌드만 | — | ←A1-6 |
| B1-6 | **교체** — 씬의 서버를 로컬 ↔ Photon 으로 바꿀 수 있게. 프레젠터 옛 로직 제거(1-1 표: 드로우 RPC · `handCount` · HP 기록 · `CardPlayManager` 직접 호출) | — | ←A1-5 (10/9). 동기화 지점 S2 |

**B 의 M1 완료 기준**: 로컬 서버로 에디터 1개에서 18-sandbox 5절 조작 16가지가 전부 동작 + 4클론 샌드박스 게이트(`development-plan.md` M1) 통과.

**B-아웃게임** (`ui-refactoring-plan.md` · `title-ui-plan.md`) — 6건 완료, **타이틀 S8 1건 남음**

| # | 작업 | 원 번호 | 비고 |
| --- | --- | --- | --- |
| B1-9 | 팝업 인프라 복구 | PR1 | ✅ #15 · #17 |
| B1-10 | 타이틀 화면 재구성 | `title-ui-plan` S1~S6 | ✅ #14 (모바일 실기 보류) |
| B1-11 | 죽은 코드 · 폴더 · 네임스페이스 정리 | PR2 | ✅ |
| B1-12 | 씬 전환 형식 통일 | PR3 | ✅ |
| B1-13 | 타이틀 로직 연결 | PR4 | ✅ #16 (실기 보류) |
| B1-14 | Canvas 규격 통일 | `15-screen.md` 9절 | ✅ |
| B1-15 | 타이틀 연결 중 · 연결 실패 상태 (로딩 링 · 다시 시도 · 오류 코드) — `SetConnecting` · "접속 중…" 폐기 | `title-ui-plan` S8 · `12-title-ui` 3절 #6~8 · 6절 | ⛔ 아티팩트 Version 13 사용자 검증 후. 연결 전 버튼 탭 · 연결 없이 로비 이동 버그를 막는다 |

### M2 — 규칙 붙이기 화면 (10/12 ~ 10/25)

샌드박스 화면을 규칙 모드에 연결한다. 새 화면보다 **기존 화면에 규칙 상태를 입히는 일**이 대부분이다.

| # | 작업 | 비고 |
| --- | --- | --- |
| B2-1 | **[계약]** A2-1(반응 마감 · 체인 상태 · 거절 사유) 리뷰 | |
| B2-2 | 반응 타이머를 서버 마감 시각에 연결 | 샌드박스 5초 타이머 화면 재사용 |
| B2-3 | 가운데 더미 → 체인 표시 (쌓인 순서 · 처리 중 강조 · 무효 표시) | ⛔ 기획 #2 흐름 |
| B2-4 | 카운터 내기 — 반응 시간 중 손패 → 가운데 드래그 | 샌드박스 입력 재사용 · ⛔ 기획 #2 |
| B2-5 | 함정 발동 — 함정 칸 탭 → 발동 요청 | 샌드박스 함정 칸 재사용 |
| B2-6 | 거절 사유 토스트 (`OnRequestRejected` → `MessageToast`) | |
| B2-7 | 규칙 모드에서 샌드박스 전용 조작(체력 · 가져오기 · 치우기 …) 숨기기 | 개발 빌드에서는 디버그 메뉴로 남긴다 |
| B2-8 | 로비 · 방 정리 | ✅ PR5 · PR6 |

### M3 — 체력 · 턴 · 승리 UI (10/26 ~ 11/4)

| # | 작업 | 비고 |
| --- | --- | --- |
| B3-1 | HP 위험색(1~20), 사망 대기 카운트, 최종 사망 좌석 비활성 | `OnLifeStateChanged` (이미 선언됨) |
| B3-2 | 턴 타이머 20초 표시 — 로컬 계산, 판정은 마스터 | ←A3-4 (턴 마감 시각 이벤트) |
| B3-3 | 찹츄 버튼 (손패 정확히 10장일 때만 활성 — 표시용) | `OnChapChuChanged` |
| B3-4 | 결과 화면 (승자 · 승리 유형 · 재대전/나가기) | ⛔ 기획 #3 (`development-plan` 4-1, 10/25). ←A3-6 결과 이벤트 |

### M4 — 카드 59장 (11/5 ~ 11/18, 로직 투입)

A 의 부하가 B 의 3배가 되는 구간이라 B 가 `Game/` 에 들어간다. **이 구간만 예외로 B 가 규칙 코드를 쓴다.**

| # | 작업 | 비고 |
| --- | --- | --- |
| B4-1 | 단순 파라미터 효과 — `Damage`, `Heal`, `Draw`, `Discard`, `SkipTurn` … | ←A4-1 (효과 기반 클래스 동결). **A 리뷰 필수** |
| B4-2 | 카드 59장 SO 에셋 — M1 가져오기 도구로 확정 숫자를 다시 가져오고 이미지 연결 | ⛔ 카드 수치 N / M (기획 #6, 11/4) |
| B4-3 | 카드 프리팹 3종(행동 · 카운터 · 함정) 앞면 · 뒷면 통일 | 현재 `PlayerActionCard` · `PlayerCounterCard` · `PlayerTrapCard` · `Dummy` 4개 |

### M5 — 첫 완성판 `0.5.0` (11/19 ~ 12/2)

| # | 작업 |
| --- | --- |
| B5-0 | 디자인 교체 — 로비 · 방 · 팝업 · 인게임 최종 시안(D8) 반영. 크기 · 배치 유지, 색 · 스프라이트 · 폰트만 (2-1절 순서 5) |
| B5-1 | 안드로이드 빌드 · 실기 — 해상도 · 터치 · Safe Area 검증 (2-1절 순서 6) |
| B5-2 | `RoomPanel` 나가기 → 들어온 씬으로 복귀 (`Lobby` → `Lobby`, `DebugLobby` → `DebugLobby`). `SceneFlow.ReturnSceneAfterRoom` ✅ (2026-09-25) |
| B5-3 | 인게임 연출 1차 (드로우 · 사용 · 피해 · 무효화 · 승리) |
| B5-4 | `PlayerSettings` productName · 아이콘 · 빌드 번호 |

### M6 ~ M8 — 다듬기 · 로그인 · 출시 준비

[`development-plan.md`](development-plan.md) 3절 M6 · M7 · M8 의 UI 개발 행을 따른다. 트랙 세부는 M5 게이트 통과 후 이 문서에 내려쓴다.

---

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

> 이어받은 작업(A0-3 · PR4 · PR6)도 전부 반영됐다 — [`plan-a-logic.md`](plan-a-logic.md) 3-1.

---

## 4. B 가 하지 않는 것

* 규칙 판정 · 상태 변경 (M4 효과 구현 제외)
* 계약 4종을 단독으로 수정
* `FakeGameServer` 안에 진짜 규칙 구현 — 흉내만 낸다. 규칙이 두 벌이 되면 반드시 갈라진다
* 씬 레이아웃 변경과 코드 리팩토링을 한 PR 에 섞기
* 계약을 복사한 연습용 네임스페이스 만들기 (옛 `Chapchu.Practice` — 2026-09-25 접음)
