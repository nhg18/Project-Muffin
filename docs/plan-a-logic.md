# A 트랙 — 게임 로직 작업 플랜

**작성일**: 2026-09-24 · **갱신**: 2026-09-26 (v5 — 작업 목록을 `development-plan.md` 3절 기능별 표로 옮김)
**담당**: 로직 담당 (A)
**소유 폴더**: `Game/`, `Network/`, `Core/` — **`.cs` 만.** 씬 · 프리팹은 만지지 않는다.
**상위 문서**: [`development-plan.md`](development-plan.md) v5 (기능 13개 · 기능별 작업 표 · 서로 기다리는 곳 · 기획 결정 마감) · [`refactoring-plan.md`](refactoring-plan.md) (진단 번호 A-/B-/C-)
**짝 문서**: [`plan-b-ui.md`](plan-b-ui.md)

---

## 0. 결론 먼저

A 의 일은 한 줄이다. **게임 상태 원본을 `GameServer`(마스터 전용) 하나로 모으고, UI 에는 `GameEvents` 로만 알린다.**

순서는 `development-plan.md` 0절의 기능 13개 (게임 시작 → 턴 → 뽑기 → 행동 카드 → 카운터 → 함정 → 체력 → 찹츄 · 승리 → 59장 → 첫 완성판 → 재접속 → 로그인 → 출시).

B 를 막지 않기 위한 A 의 규칙은 세 가지다.

1. **약속 파일 4종(`PlayerProps`/`RoomProps`, `GameEvents`, `IGameRequests`, `IGameState`)은 쓰기 전에 먼저 PR 로 올린다.** B 는 약속만 보고 로컬 서버(`FakeGameServer`)로 개발한다.
2. **`Presentation/` 파일은 고치지 않는다.** 지금 `Presentation` 에 들어 있는 로직(아래 1-3)은 A 가 `Game/` 에 새 경로를 만들고, **옛 호출 제거는 B 가 교체 PR 에서 한다.**
3. **`Presentation` 타입을 참조하지 않는다.** (asmdef 분리의 전제)

---

## 1. 현재 코드 상태 (2026-09-26 확인)

### 1-1. M0 에서 끝난 것 (PR #10, `3f84952`)

`PlayerProps`/`RoomProps`, `GameEvents` 전 이벤트 `actorNumber` 포함, `IGameRequests` 스텁, `CardInstance`, `LifeState`, HP `int` 통일, 초기 손패 5장, `03-turn.md` 노션 반영.

### 1-2. 계약에서 새로 발견된 구멍

| # | 위치 | 문제 | 처리 |
| --- | --- | --- | --- |
| K-1 | `GameEvents.OnDrawn(actorNumber, cardId)` + `DeckPresenter.RPC_BroadcastDrawnCard` | **뽑은 카드 ID 를 `RpcTarget.All` 로 전파** — 남의 손패 내용이 전원에게 보인다 (`CLAUDE.md` 11-6 위반) | 공개 통지(누가 1장 뽑음)와 비공개 통지(내 손에 들어온 `CardInstance`)로 분리 |
| K-2 | 위와 동일 | 이벤트에 `InstanceId` 가 없다 → UI 가 `RequestPlayCard(cardInstanceId, …)` 를 **호출할 수 없다** | K-1 의 비공개 통지가 `CardInstance` 를 넘긴다 |
| K-3 | 손패에서 카드가 빠지는 통지 없음 | 사용 · 버림 · 강탈 시 UI 가 어떤 카드를 지울지 모른다 (C-8 의 UI 측) | 비공개 통지 `내 손에서 InstanceId 제거` 추가 (M2 전) |
| K-4 | `Game/IGameState.cs` (B 가 2026-09-25 추가) | 늦게 켜진 UI 가 현재 턴 주인을 읽는 읽기 전용 계약. 지금은 `CurrentTurnActor` 하나 | **A 리뷰 필요** (A1-1 과 함께). Photon 구현체는 `RoomProps.TurnActor` 를 읽어 돌려주면 된다. 판정에 쓰지 않는다 |

> K-1 · K-2 는 **기능 1 약속 PR** 로 계약만 먼저 바꾼다. 이름 · 시그니처는 A 가 제안하고 B 가 리뷰한다.

### 1-3. `Presentation` 에 들어 있는 로직 (A 가 대체, B 가 제거)

| 현재 위치 (B 소유) | 하고 있는 일 | A 가 만들 대체 |
| --- | --- | --- |
| `Presentation/Deck/DeckPresenter` | 드로우 RPC, 마스터 가드 없음(B-2), 덱 전체를 `"RoomDeck"` 로 동기화(B-1), 클라이언트별 초기 배분(B-7) | `GameServer.Draw` + 요청 RPC + `deckCount` |
| `Presentation/Hand/PlayerHandPresenter` | 자기 `handCount` 를 `SetCustomProperties` 로 **직접** 기록 | 마스터가 `handCount` 기록 |
| `Presentation/Player/PlayerPresenter.Init` | HP 초기값을 **클라이언트가** 프로퍼티에 기록 | 마스터가 게임 시작 시퀀스에서 기록 |
| `Game/Rules/CardPlayManager` ↔ `Presentation/Card/CardPresenter` | 체인 · 무효화를 전원이 로컬 실행(A-2), 양방향 참조 | M2 에서 `GameServer` 로 이동 |

### 1-4. EditMode 테스트 전제 조건

운영 규칙 4(`GameServer` 는 Photon 없이 EditMode 테스트)는 **지금 구조로는 불가능하다.**
프로젝트 코드에 asmdef 가 하나도 없고, 테스트 asmdef 는 `Assembly-CSharp` 를 참조할 수 없다.

→ **M1 에서 `GameServer` 계열 순수 C# 코드만 담는 작은 asmdef 를 먼저 만든다.**
이 코드는 새로 쓰는 것이라 `Presentation` · DOTween 의존이 없어 `refactoring-plan.md` 1-5 의 보류 사유 두 가지에 걸리지 않는다.
나머지 전체 asmdef 분리는 기능 10.

| 결정 필요 (A) | 제안 |
| --- | --- |
| asmdef 이름 · 범위 | `Muffin.Game.Server` — `GameServer`, `Deck`, `DiscardPile`, `CardInstance`, `LifeState`. Photon · UnityEngine UI 참조 없음 |
| `CardInstance` · `LifeState` 이동 | 이 asmdef 로 옮기면 `Assembly-CSharp` 쪽은 자동 참조된다 (asmdef → Assembly-CSharp 방향만 불가) |

---

## 2. 작업 목록

**`development-plan.md` 3절 "기능별 작업"의 로직 열이 원본이다** (v5, 2026-09-26). 기능 13개를 순서대로, 기능마다 약속 → 상태 바꾸기 → 검사 → 자동 처리 · 테스트.

| 기능 | 로직이 먼저 넘길 것 (날짜) |
| --- | --- |
| 1 게임 시작하기 | **약속 10/11** · 서버 코어 10/16 · Photon 연결 10/19 |
| 2 턴 넘기기 | 턴 마감 시각 10/22 |
| 4 행동 카드 내기 | 약속 10/22 |
| 5 카운터로 막기 | 약속 10/29 |
| 6 함정 쓰기 | 약속 11/1 |
| 7 체력과 사망 | 약속 11/5 |
| 8 찹츄와 승리 | 약속 11/8 |
| 9 카드 59장 | 효과 기본 틀 11/15 |
| 11 재접속 | 복원 알림 12/13 |

1절의 진단 번호(K- · A- · B- · C-)는 각 기능 작업에서 함께 해결한다. 예: K-1 · K-2(뽑은 카드가 전원에게 보임)는 기능 1 약속, C-2(다음 턴 계산 버그)는 기능 2.

M0 에서 끝난 것: 인터넷 없을 때 씬 동기화 누락(A0-1) ✅ · 연결 끊김 사유 · 타임아웃(A0-2) ✅ · 방 화면 중복 설정 제거(A0-3, B 가 처리) ✅.

## 3. B 가 A 에게 요청한 항목

[`ui-refactoring-plan.md`](ui-refactoring-plan.md) 5절에서 넘어온 것. 파일 소유가 A 다. **전부 완료.**

| # | 요청 | 원 번호 | 시한 | 상태 |
| --- | --- | --- | --- | --- |
| R-1 | `RoomEvents.OnMasterClientSwitched` 추가 | U-12 | B 의 PR6 전 | ✅ 2026-09-24 |
| R-2 | `PhotonConnection.SetupInitNickname()` 삭제 (호출처 0) | U-7 | B 의 PR4 와 함께 | ✅ 2026-09-24 |
| R-3 | `Initialize()` 조기 반환 + `OnDisconnected` 분기 | U-6, U-23 | = A0-1, A0-2. **B 의 PR6 전 필수** | ✅ 2026-09-24 |

### 3-1. B 에게 인계 — ✅ 전부 반영됨

| B 작업 | 반영 | 남은 주의 |
| --- | --- | --- |
| A0-3 (U-23) | ✅ `RoomPanel` 삭제로 해소 (설정은 `PhotonConnection` 한 곳) | — |
| PR4 (U-6) | ✅ `TitlePresenter` 가 `ConnectionEvents.OnDisconnected` 구독 → 실패 안내 + 다시 시도 | 인게임에서 끊겨도 같은 이벤트가 온다 — 인게임 끊김 화면은 기능 11 |
| PR6 (U-12) | ✅ `RoomPresenter` 가 `RoomEvents.OnMasterClientSwitched` 구독 | `OnPlayerLeft` 와의 호출 순서는 보장되지 않는다 |

---

## 4. A 가 하지 않는 것

* `.unity` / `.prefab` 편집 — 필요하면 B 에게 요청
* `Presentation/` 의 옛 로직 제거 — 새 경로만 만들고 B 에게 넘긴다 (1-3)
* 문서에 `미정` 인 수치 하드코딩 — 기획 확정 전엔 테스트용 값을 테스트 코드 안에만 둔다
