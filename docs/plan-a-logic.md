# A 트랙 — 게임 로직 작업 플랜

**작성일**: 2026-09-24 · **갱신**: 2026-09-25 (v2 플랜 · `IGameState` 리뷰 항목 반영)
**담당**: 로직 담당 (A)
**소유 폴더**: `Game/`, `Network/`, `Core/` — **`.cs` 만.** 씬 · 프리팹은 만지지 않는다.
**상위 문서**: [`development-plan.md`](development-plan.md) v3 (6인 팀 · M1~M8 단계 · 게이트 · 동기화 지점 · 기획 마감 담당) · [`refactoring-plan.md`](refactoring-plan.md) (진단 번호 A-/B-/C-)
**짝 문서**: [`plan-b-ui.md`](plan-b-ui.md)

---

## 0. 결론 먼저

A 의 일은 한 줄이다. **게임 상태 원본을 `GameServer`(마스터 전용) 하나로 모으고, UI 에는 `GameEvents` 로만 알린다.**

```
M0 잔여(네트워크 2건 + 계약 보강) → M1 GameServer · 요청 파이프라인 → M2 체인 · 함정
→ M3 체력 · 턴 · 승리 → M4 복잡한 카드 효과 → M5 방 · 재접속 · asmdef
```

B 를 막지 않기 위한 A 의 규칙은 세 가지다.

1. **계약 3종(`PlayerProps`/`RoomProps`, `GameEvents`, `IGameRequests`)은 쓰기 전에 먼저 PR 로 올린다.** B 는 계약만 보고 `FakeGameServer` 로 개발한다.
2. **`Presentation/` 파일은 고치지 않는다.** 지금 `Presentation` 에 들어 있는 로직(아래 1-3)은 A 가 `Game/` 에 새 경로를 만들고, **옛 호출 제거는 B 가 교체 PR 에서 한다.**
3. **`Presentation` 타입을 참조하지 않는다.** (asmdef 분리의 전제)

---

## 1. 현재 코드 상태 (2026-09-24 확인)

### 1-1. M0 에서 끝난 것 (PR #10, `3f84952`)

`PlayerProps`/`RoomProps`, `GameEvents` 전 이벤트 `actorNumber` 포함, `IGameRequests` 스텁, `CardInstance`, `LifeState`, HP `int` 통일, 초기 손패 5장, `03-turn.md` 노션 반영.

### 1-2. 계약에서 새로 발견된 구멍

| # | 위치 | 문제 | 처리 |
| --- | --- | --- | --- |
| K-1 | `GameEvents.OnDrawn(actorNumber, cardId)` + `DeckPresenter.RPC_BroadcastDrawnCard` | **뽑은 카드 ID 를 `RpcTarget.All` 로 전파** — 남의 손패 내용이 전원에게 보인다 (`CLAUDE.md` 11-6 위반) | 공개 통지(누가 1장 뽑음)와 비공개 통지(내 손에 들어온 `CardInstance`)로 분리 |
| K-2 | 위와 동일 | 이벤트에 `InstanceId` 가 없다 → UI 가 `RequestPlayCard(cardInstanceId, …)` 를 **호출할 수 없다** | K-1 의 비공개 통지가 `CardInstance` 를 넘긴다 |
| K-3 | 손패에서 카드가 빠지는 통지 없음 | 사용 · 버림 · 강탈 시 UI 가 어떤 카드를 지울지 모른다 (C-8 의 UI 측) | 비공개 통지 `내 손에서 InstanceId 제거` 추가 (M2 전) |
| K-4 | `Game/IGameState.cs` (B 가 2026-09-25 추가) | 늦게 켜진 UI 가 현재 턴 주인을 읽는 읽기 전용 계약. 지금은 `CurrentTurnActor` 하나 | **A 리뷰 필요** (A1-1 과 함께). Photon 구현체는 `RoomProps.TurnActor` 를 읽어 돌려주면 된다. 판정에 쓰지 않는다 |

> K-1 · K-2 는 **M1 첫 PR** 로 계약만 먼저 바꾼다. 이름 · 시그니처는 A 가 제안하고 B 가 리뷰한다.

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
나머지 전체 asmdef 분리는 그대로 M5.

| 결정 필요 (A) | 제안 |
| --- | --- |
| asmdef 이름 · 범위 | `Muffin.Game.Server` — `GameServer`, `Deck`, `DiscardPile`, `CardInstance`, `LifeState`. Photon · UnityEngine UI 참조 없음 |
| `CardInstance` · `LifeState` 이동 | 이 asmdef 로 옮기면 `Assembly-CSharp` 쪽은 자동 참조된다 (asmdef → Assembly-CSharp 방향만 불가) |

---

## 2. 마일스톤별 작업

표기: **[계약]** = 양쪽 리뷰 필수 PR · **→B** = 끝나면 B 에게 알려야 하는 것 · **⛔** = 기획 미정으로 막힘

### M0 잔여 (이번 주)

| # | 작업 | 위치 | 비고 |
| --- | --- | --- | --- |
| A0-1 | ✅ `Initialize()` 가 인터넷 없을 때 조기 반환 → `AutomaticallySyncScene = true` 누락 | `Network/PhotonConnection.cs` | 설정은 항상 하고, 인터넷 확인은 `Connect()` 로 이동 |
| A0-2 | ✅ `OnDisconnected` 분기 구현 + 연결 타임아웃 | `Network/PhotonConnection.cs` · `NetworkManager.cs` | 사유만 `ConnectionEvents.OnDisconnected` 로 넘긴다. 규칙은 `09-network.md` 11절 **→B** (3-1 인계) |
| A0-3 | `RoomPanel.Awake` 의 `AutomaticallySyncScene` 중복 제거 요청 처리 | — | A0-1 머지 후 B 가 지운다 (U-23) **→B** (3-1 인계) |

### M1 — 마스터 권한 (2주)

| # | 작업 | 진단 | 비고 |
| --- | --- | --- | --- |
| A1-1 | **[계약]** `OnDrawn` 공개/비공개 분리, 비공개 통지에 `CardInstance` + `IGameState` 리뷰 | K-1, K-2, K-4 | **M1 첫 PR (v2: 9/28 시작).** B 의 `FakeGameServer` 가 이 계약을 쓴다 **→B**. 2026-09-25 현재 미착수 — B 의 인게임 카드 작업이 전부 여기 걸려 있다 |
| A1-2 | `Muffin.Game.Server` asmdef + EditMode 테스트 asmdef | 1-4 | |
| A1-3 | `GameServer` — 덱 · 손패 · HP · 턴 순서 · 버림 더미 원본 소유 | A-1 | 순수 C#. Photon 은 바깥 어댑터에서만 |
| A1-4 | `Deck.Shuffle()` 구현, `DrawAt` 범위 검사, 소진 시 버림 더미 재생성 | C-6, C-7 | 테스트: 셔플 분포 · 소진 재생성 |
| A1-5 | `IGameRequests` · `IGameState` Photon 구현체 — 요청 → 검증 → 전파. 거절은 `RaiseRequestRejected` 를 **요청자에게만** | A-1, B-2 | RPC 첫 줄 `if (!PhotonNetwork.IsMasterClient) return;`. B 의 `TurnPresenter` 는 `MonoBehaviour` 로 `server` 를 받으므로 구현체도 `MonoBehaviour` 여야 씬에서 교체된다 |
| A1-6 | 게임 시작 1회 시퀀스: 셔플 → 인스턴스 ID → HP 100 → 5장 배분 → 턴 순서 무작위 | B-7, C-3, C-5 | ⛔ 덱 구성(기획 #1). 확정 전엔 기존 `DeckRecipe` 로 테스트만 |
| A1-7 | `"RoomDeck"` 제거 → `RoomProps.deckCount` 만 공개 | B-1 | |
| A1-8 | `handCount` · `hp` 를 **마스터만** 기록 | 1-3 | 클라이언트 기록 코드 제거는 B (교체 PR) |
| A1-9 | `TurnManager.GetNextActor` 인자 무시 버그 | C-2 | |

**A 의 M1 완료 기준**: EditMode 테스트로 셔플 · 드로우 · 소진 재생성 · 시작 시퀀스 통과 + 4클론에서 요청 RPC 가 마스터에서만 처리됨.
**→B**: Photon 구현체가 머지되면 B 가 `FakeGameServer` → 실제 구현체 교체 PR 을 연다 (동기화 지점 S2).

### M2 — 카드 파이프라인 · 함정 (2주)

| # | 작업 | 진단 | 비고 |
| --- | --- | --- | --- |
| A2-1 | **[계약]** 체인 · 반응 마감 · 함정 · 손패 제거 이벤트 추가 | K-3 | **M2 첫 PR.** 반응 마감은 `PhotonNetwork.Time` 기준 절대 시각으로 **→B** |
| A2-2 | 체인을 `GameServer` 단독 소유, `isCanceled` 마스터 전용 | A-2 | `CardPlayManager` → `CardPresenter` 참조 제거 (asmdef 순환 해소) |
| A2-3 | `Invoke` 제거 → 5초 마감, 카운터 등록 시 재시작, `isResolutioning` 가드 수정 | B-4, B-5 | |
| A2-4 | `EffectContext` — 대상 0개도 1회 실행, 다중 대상 변경 전 HP 기준 일괄 계산 | C-9, C-10 | 테스트 필수 |
| A2-5 | 함정 슬롯 3칸 (설치 / 발동 / 파괴 / 공개). 개수만 공개 | — | `trapCount` 프로퍼티 |
| A2-6 | 사용한 카드 손패 제거 → 버림 더미 | C-8 | |
| A2-7 | 대상 타입 확장 (조건부 대상, 카드 대상) | `refactoring-plan` 3-5 | ⛔ 턴 진행 방향(기획 #5) — A07, A10 만 보류 |

**A 의 M2 완료 기준**: A09 → C05 → 체인 역순이 EditMode 테스트 + 4클론에서 동일.

### M3 — 체력 · 턴 · 승리 (1.5주)

| # | 작업 | 비고 |
| --- | --- | --- |
| A3-1 | `HealthService` — 감소 · 무효 · 전환 전부 계산 후 1회 반영, 처리 ID 중복 방지 | |
| A3-2 | `LifeState` 전이 (Alive → DeathPending 5초 → Dead), 동시 사망 일괄 처리, 사망자 손패 · 함정 버림 | |
| A3-3 | 메인 행동 1회 제한 | 현재 무제한 드로우 가능 |
| A3-4 | 턴 타이머 20초 마스터 판정, 시간 초과 시 턴 넘김 | 턴 마감 시각을 이벤트로 **→B** (표시는 B 가 로컬 계산) |
| A3-5 | 연속 미제출 강제 퇴장 | ⛔ N 미정(기획 #4) |
| A3-6 | 찹츄 선언 · 해제 · 판정, 처치 승리, 무승부 | 결과 이벤트 **→B** |

**A 의 M3 완료 기준**: 처치 승리 · 찹츄 승리 · 무승부가 테스트로 재현된다.

### M4 — 카드 대량 구현 (2주)

| # | 작업 | 비고 |
| --- | --- | --- |
| A4-1 | **효과 SO 기반 클래스 동결** — B 가 단순 효과를 쓰기 시작하는 조건 | **→B** (동기화 지점 S5) |
| A4-2 | 구조가 까다로운 효과: `Negate`, `Redirect`, `Choice`, `Peek`, 사후 트리거(T06~T16) | |
| A4-3 | MVP 6장(A09 / A08 / A06 / A05 / C05 / T06) 구조 검증 → 통과 후 나머지 | ⛔ 카드 수치 N / M(기획 #6) |
| A4-4 | B 가 쓴 단순 효과 리뷰 | 규칙 판정 코드는 A 가 최종 책임 |

### M5 — MVP 마감 (2주)

| # | 작업 |
| --- | --- |
| A5-1 | 방 시스템 확정분 구현 (시작 조건 · 방장 이탈) ⛔ 기획 #7 |
| A5-2 | 재접속 / 마스터 이탈 — 새 마스터가 상태를 이어받는 방식 결정 필요 |
| A5-3 | 전체 asmdef 분리 (`refactoring-plan.md` 1-5 — DOTween Modules asmdef 포함) |
| A5-4 | EditMode 테스트를 한 번에 돌리는 메뉴 (CI 없음) |

### M6 ~ M8 — 안정화 · 로그인 · 완성도

[`development-plan.md`](development-plan.md) 2절 M6 · M7 · M8 의 A 열을 따른다 (재접속 · 마스터 이탈 · 로그인 · 친구 · 치트 로그 · 빌드 파이프라인). 트랙 세부는 M5 게이트 통과 후 이 문서에 내려쓴다. 로그인은 v1 의 A5-4 에서 M7 로 옮겼다 — 문서가 없어 M5 안에 들어갈 수 없다.

---

## 3. B 가 A 에게 요청한 항목

`docs/title-ui` 브랜치의 [`ui-refactoring-plan.md`](ui-refactoring-plan.md) 5절에서 넘어온 것. 파일 소유가 A 다.

| # | 요청 | 원 번호 | 시한 | 상태 |
| --- | --- | --- | --- | --- |
| R-1 | `RoomEvents.OnMasterClientSwitched` 추가 | U-12 | B 의 PR6 전 | ✅ 2026-09-24 |
| R-2 | `PhotonConnection.SetupInitNickname()` 삭제 (호출처 0) | U-7 | B 의 PR4 와 함께 | ✅ 2026-09-24 |
| R-3 | `Initialize()` 조기 반환 + `OnDisconnected` 분기 | U-6, U-23 | = A0-1, A0-2. **B 의 PR6 전 필수** | ✅ 2026-09-24 |

### 3-1. B 에게 인계 (브랜치 `temp/a-network-outgame` → `changhwan.exe`)

| B 작업 | 이제 할 수 있는 것 | 주의 |
| --- | --- | --- |
| A0-3 (U-23) | `RoomPanel.Awake` 의 `PhotonNetwork.AutomaticallySyncScene = true` 삭제 | 위 브랜치 **머지 후에만**. 먼저 지우면 인게임 진입이 깨진다 |
| PR4 · B1-13 (U-6) | `ConnectionEvents.OnDisconnected(DisconnectCause)` 구독 → 안내 문구 + 재시도 버튼(`NetworkManager.Connect()`) | 인게임에서 끊겨도 같은 이벤트가 온다. `ExceptionOnConnect` = 인터넷 없음 포함 접속 실패, `ClientTimeout` = 15초 초과. 접속 중에 `Connect()` 를 다시 불러도 무시된다 |
| PR6 (U-12) | `RoomInfoPanel` · `RoomPanel` 에서 `RoomEvents.OnMasterClientSwitched(Player)` 구독 → `(Host)` 표기 · 시작 버튼 갱신 | `OnPlayerLeft` 와의 호출 순서는 보장되지 않는다. 방장 이탈 처리(`08-room.md` 5절 #5)는 미정 |

---

## 4. A 가 하지 않는 것

* `.unity` / `.prefab` 편집 — 필요하면 B 에게 요청
* `Presentation/` 의 옛 로직 제거 — 새 경로만 만들고 B 에게 넘긴다 (1-3)
* 문서에 `미정` 인 수치 하드코딩 — 기획 확정 전엔 테스트용 값을 테스트 코드 안에만 둔다
