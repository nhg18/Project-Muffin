# A 트랙 — 게임 로직 작업 플랜

**작성일**: 2026-09-24 · **갱신**: 2026-10-06 (v8 — 테스트는 멀티 실행 · Console 로그, Test Runner 안 씀. 기능 1 · 수정 3 진행 반영)
**담당**: 로직 담당 (A)
**소유 폴더**: `Game/`, `Network/`, `Core/` — **`.cs` 만.** 씬 · 프리팹은 만지지 않는다.
**상위 문서**: [`development-plan.md`](development-plan.md) v5 (기능 13개 · 기능별 작업 표 · 서로 기다리는 곳 · 기획 결정 마감) · [`refactoring-plan.md`](refactoring-plan.md) (진단 번호 A-/B-/C-)
**짝 문서**: [`plan-b-ui.md`](plan-b-ui.md)

---

## 0. 결론 먼저

A 의 일은 한 줄이다. **게임 상태 원본을 `GameServer`(마스터 전용) 하나로 모으고, UI 에는 `GameEvents` 로만 알린다.**

순서는 `development-plan.md` 0절의 기능 13개 (게임 시작 → 턴 → 뽑기 → 행동 카드 → 카운터 → 함정 → 체력 → 찹츄 · 승리 → 59장 → 첫 완성판 → 재접속 → 로그인 → 출시).

B 를 막지 않기 위한 A 의 규칙은 세 가지다.

1. **약속 파일 4종(`PlayerProps`/`RoomProps`, `GameEvents`, `IGameRequests`, `IGameState`)은 기능을 개발하다 필요해질 때, 그 기능 작업 안에서 고친다.** 약속만 먼저 올리는 단계를 따로 두지 않는다 (2026-09-30 결정). 약속 파일이 바뀌는 PR 은 B 리뷰를 받는다 (`CLAUDE.md` 14절). ⚠ `development-plan.md` 2절 공통 순서(약속 먼저)와 다르다 — B 와 맞출 것.
2. **`Presentation/` 파일은 고치지 않는다.** 지금 `Presentation` 에 들어 있는 로직(아래 1-4)은 A 가 `Game/` 에 새 경로를 만들고, **옛 호출 제거는 B 가 교체 PR 에서 한다.**
3. **`Presentation` 타입을 참조하지 않는다.** (asmdef 분리의 전제)

---

## 1. 현재 코드 상태 (2026-10-06 확인)

### 1-1. 끝난 것

| 항목 | 커밋 |
| --- | --- |
| M0 약속 — `PlayerProps`/`RoomProps`, `GameEvents` 전 이벤트 `actorNumber`, `IGameRequests` 스텁, `CardInstance`, `LifeState`, HP `int`, 초기 손패 5장 | PR #10 `3f84952` |
| `GameServer`(순수 C#) · `IServerOutbox` · `PunGameServer` — 턴 종료 요청 · 검증 · 거절 | `6b70e99` |
| 덱을 `GameServer` 권위로 — 인스턴스 ID 부여 · 셔플 · 마스터 일괄 배분 · 드로우 · 버림 더미 회수, 손패 장수 마스터 기록, `"RoomDeck"` 삭제 (B-1 · B-2 · B-7) | PR #40 `921de03` |
| 뽑은 카드는 주인에게만 · 인스턴스 ID 포함 (K-1 해결, K-2 서버 쪽) | `41673c7` |
| 드로우 후 턴 종료 (메인 행동 중 드로우 쪽) | `80ea520` |
| `Muffin.Game.Server` asmdef — 컴파일 속도 (옛 1-4 해결). 함께 만든 EditMode 테스트는 쓰지 않는다 (10/6, 테스트는 멀티 실행) | `1462ae0` |
| `PlayerState` — 방장이 손패 내용 · 체력 원본 보관, 버림 요청 소유 검증 | `aaee0c2` |
| 체력 100 방장 기록 → `GameEvents.OnHpChanged` (수정 1 의 A 쪽) | `1b18c3e` |
| 첫 턴 무작위 — 시작 때 턴 순서 셔플 | `6224d6e` |
| 턴 주인이 나가면 다음 사람에게 턴, 나간 사람은 순서에서 제외 (수정 3) | `ead0c40` |
| 위 작업 develop 반영 · `TempGameScene` 서버 오브젝트 하나로 정리(`GameServer` — `PunGameServer` · `PhotonView` · `ServerDebugLog`, Presenter `server` 연결) | PR #48 · #47 (10/6) |

### 1-2. 수정 목록 — 급한 순

기존 코드의 버그 · 규약 위반만 모았다. **먼저 만드는 기능에서 풀리는 것이 위.** 따로 고치지 않고, 적힌 기능을 만들 때 함께 고친다. 새 기능은 1-3.

> **0. 먼저 확인** — Unity 컴파일, `TempGameScene` 멀티 테스트로 손패 5장 · 체력 100 · 드로우 → 턴 넘김 · 클론 종료 시 턴 이동 (PR #48 · #47 머지분, 아직 미확인)

| # | 문제 | 위치 | 고치는 기능 |
| --- | --- | --- | --- |
| 1 | HP 초기값을 **각 클라이언트가** 기록한다 (`09` 3절 위반). A 가 마스터 기록을 만들고 B 가 옛 코드를 지운다 | `PlayerPresenter.Init` (B 파일) | 1 게임 시작하기 |
| 2 | 옛 턴 관리자가 요청자를 클라가 보낸 값으로 믿고, 다음 턴 계산이 인자를 무시한다 (C-2). `PunGameServer` 로 교체하면 삭제 — 따로 고치지 않는다 | `TurnManager` (`GameScene` 사용 중) | 1 게임 시작하기 |
| ~~3~~ | ~~턴 주인이 나가면 턴이 멈춘다~~ ✅ `ead0c40` — `OnPlayerLeftRoom` → `RemoveFromTurnOrder` | `PunGameServer` · `GameServer.Turn.cs` | 2 턴 넘기기 |
| 4 | 버림 요청이 클라가 보낸 **카드 종류 ID** 를 믿는다 (소유 검증 없음) → 없는 카드가 버림 더미를 거쳐 덱에 섞인다. 버릴 때 인스턴스 ID 재발급. UI 는 인스턴스 ID 를 받지 못한다 (`OnDrawn`, K-2). 버림 요청은 PR #40 의 임시 경로 — 카드 사용 요청으로 대체하면서 삭제 | `GameServer.Deck.cs` `Discard` · `GameEvents` | 4 행동 카드 내기 |
| 5 | 카드 사용 시 **승인 전에** 손패에서 지운다. 옛 카드 관리자는 검증 없이 전원이 체인을 로컬 실행 | `PlayerHandPresenter` (B) · `CardPlayManager` | 4 행동 카드 내기 |
| 6 | 버림을 **자신의 턴에만** 받는다 → 강제 버림 · 카운터(남의 턴)가 막힌다 | `GameServer.Deck.cs` `Discard` | 4 행동 카드 내기 · 5 카운터 |
| 7 | RPC 이름 문자열 리터럴 3곳 · `Invoke("…")`, 반응 시간 4.5초 하드코딩 (기획 5초). 옛 카드 관리자 교체 시 삭제 | `CardPlayManager` | 4 행동 카드 내기 · 5 카운터 |
| 8 | 뽑을 때 빈 덱 방어 없음 (덱 < 인원 × 5 면 예외). 모든 뽑기가 `DrawOne` 에서 덱이 비면 버림 더미로 재생성 · `OnDeckRefilled` 알림 (10/6, #49 리뷰). 덱 · 버림 더미 모두 0장은 ⛔ (거절 · 배분 중단) | `GameServer.Deck.cs` `DrawOne` | 덱 구성 확정 때 |
| 9 | 문서 — Notion 「개발 기획서」에 `05` · `09` 갱신분 미반영, 강퇴 턴 수 "2~3" 이 `03-turn` 에 없음 | Notion · `03-turn.md` | 틈날 때 |
| 10 | 카드 조건 `MyTurnCondition` 이 옛 `TurnManager.Instance` 를 읽는다 → `TempGameScene`(TurnManager 없음)에서 카드를 드롭하면 NRE (10/6 발견 — #47 의 `CardDropArea` 로 드롭 경로가 처음 실행됨). 08-17 프로토타입 코드. `TurnUI` 도 같은 참조 2곳. 최소 수정은 `RoomProps.TurnActor` 읽기, 최종 판정은 방장 | `Game/Cards/CardCondition/MyTurnCondition.cs` · `Presentation/Turn/TurnUI.cs` | 4 행동 카드 내기 |

### 1-3. 기능 개발 순서 (로직)

**기능 하나가 한 줄이다.** `development-plan.md` 3절의 기능을 순서대로 만든다. 서버 코어 · Photon 연결 같은 내부 단계와 테스트는 따로 세지 않는다 — 기능을 만드는 과정이고, 테스트는 "끝났다는 기준"이다.
**테스트는 멀티 실행으로 한다** (2026-10-06): Test Runner(EditMode) 는 쓰지 않는다. 4클론(`DebugLobbyScene` → `TempGameScene`)을 돌리고 화면과 Console 로그(`ServerDebugLog`)로 본다. 사용자 입력 영역이 아직 없으니 확인용 로그는 `Start` 에서 찍는다.
약속 파일은 그 기능을 만들다 필요해질 때 고친다 (0절 규칙 1). ✅ 끝남 · ⛔ 기획 결정 대기 · (수정 #) 은 1-2 에서 같이 해결되는 것.

| 순서 | 기능 | 남은 일 (로직) | 끝났다는 기준 |
| --- | --- | --- | --- |
| **1** | **게임 시작하기** (~ 10/19) | `GameScene` 의 옛 서버를 `PunGameServer` 로 교체(씬은 B, 수정 2). 첫 턴 무작위 · 섞기 · 카드 번호 · 5장 배분 · 체력 100 마스터 기록(수정 1 의 A 쪽) · 손패 원본 `PlayerState` 는 ✅. 전원 로드 대기는 넣지 않음 — 기능 2 의 20초 마감 때 테스트로 결정 (worklog 10/1) | 4클론 — 손패가 서로 다르다 · 체력 100 · 장수 5 · 첫 턴 주인이 같다 · Console — 판마다 턴 순서가 다르다 |
| 2 | 턴 넘기기 (~ 10/26) | 턴 순서 알림 · 다음 사람 찾기(나간 사람 빼기 ✅ 수정 3) · 턴 넘김 한 곳으로(`AdvanceTurn` ✅ 10/6 도입 결정) · 20초 마감 ✅ · 전원 로드 대기 필요 여부 측정. 턴 주인 검사는 ✅. 강퇴 ⛔ | 턴 종료 → 다음 사람 · 남의 턴이면 거절 · 20초 뒤 자동 넘김 · 네 화면 동일 |
| 3 | 카드 뽑기 (~ 10/21) | 요청 · 덱 재생성 · 뽑으면 턴 종료 ✅. 덱 · 버림 더미가 모두 0장일 때 ⛔ | 4클론 — 뽑기 · 소진 재생성 · 두 번 뽑기 거절 (Console 로그) |
| 4 | 행동 카드 내기 (~ 10/30) | 카드 사용 요청(인스턴스 ID) · 검사(내 턴 · 이번 턴 행동 안 함 · **손패에 있음** · 카드 조건 · 대상) · 효과 실행 틀 · 사용 후 턴 종료 · 옛 카드 관리자와 임시 버림 요청 삭제 (수정 4 · 5 · 6 · 7) | 공격 카드(A09)로 대상 체력 감소가 네 화면 동일 · 남의 턴에 내면 거절 · 한 턴에 뽑기와 카드 내기를 둘 다 못 한다 |
| 5 | 카운터로 막기 (~ 11/4) | 반응 5초(카운터가 오면 재시작) · 검사 · 역순 처리 · 무효(C05) | 4클론 — A09 ← C05, 5초 지나 온 카운터 거절 (Console 로그) |
| 6 | 함정 쓰기 (~ 11/9) | 설치 · 검사 · 수동 발동 · T06 | 함정 효과 1개(T06) 경로 검증 |
| 7 | 체력과 사망 (~ 11/11) | 한 번에 반영 · 처리 번호 · 사망 대기 5초 · 죽은 사람 제외. 나간 사람 처리 ⛔ | 4클론 — 동시 사망 · 사망 대기 중 회복 (Console 로그) |
| 8 | 찹츄와 승리 (~ 11/14) | 선언 · 해제 · 판정. 판정 시점 · 방장 이탈 ⛔ | 4클론 — 세 가지 결말 |
| 9 ~ 13 | 카드 59장 · 첫 완성판 · 재접속 · 로그인 · 출시 | `development-plan` 참고 | |

> 손패 원본 보관은 `development-plan` 에 기능 1 로 적혀 있어 1 에 뒀다. 처음 실제로 쓰이는 곳은 기능 4 의 "손패에 있음" 검사다.

> 10/6: logic → develop (PR #48), HeeGeon UI(#47) 머지 완료. `logic` · `develop` 같은 상태. 멀티 테스트 씬의 서버는 `TempGameScene` 의 `GameServer` 오브젝트 하나.

### 1-4. `Presentation` 에 남은 로직 (A 가 대체, B 가 제거)

| 현재 위치 (B 소유) | 하고 있는 일 | 상태 |
| --- | --- | --- |
| `Presentation/Deck/DeckPresenter` | 요청 · 이벤트만 | ✅ PR #40 |
| `Presentation/Hand/PlayerHandPresenter` | `handCount` 직접 기록 | ✅ 마스터가 기록 (PR #40) |
| `Presentation/Hand/PlayerHandPresenter` | 카드 사용 시 **승인 전에** 손패에서 먼저 지움 | 기능 4 |
| `Presentation/Player/PlayerPresenter.Init` | HP 초기값을 **클라이언트가** 기록 | 기능 1 시작 순서 |
| `Game/Rules/CardPlayManager` ↔ `Presentation/Card/CardPresenter` | 체인 · 무효화를 전원이 로컬 실행(A-2), 양방향 참조 | 기능 4 · 5 |

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

1-2 수정 목록 · 1-3 개발 순서와 진단 번호(K- · A- · B- · C-)는 각 기능 작업에서 함께 해결한다. 예: K-2 · K-3 은 기능 4, C-2(옛 다음 턴 계산 버그)는 기능 1 의 옛 턴 관리자 교체로 사라진다.

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
