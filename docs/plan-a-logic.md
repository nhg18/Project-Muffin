# A 트랙 — 게임 로직 작업 플랜

**작성일**: 2026-09-24 · **갱신**: 2026-09-30 (v6 — 1절 현재 상태 · 기능별 남은 일 갱신)
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

## 1. 현재 코드 상태 (2026-09-30 확인)

### 1-1. 끝난 것

| 항목 | 커밋 |
| --- | --- |
| M0 약속 — `PlayerProps`/`RoomProps`, `GameEvents` 전 이벤트 `actorNumber`, `IGameRequests` 스텁, `CardInstance`, `LifeState`, HP `int`, 초기 손패 5장 | PR #10 `3f84952` |
| `GameServer`(순수 C#) · `IServerOutbox` · `PunGameServer` — 턴 종료 요청 · 검증 · 거절 | `6b70e99` |
| 덱을 `GameServer` 권위로 — 인스턴스 ID 부여 · 셔플 · 마스터 일괄 배분 · 드로우 · 버림 더미 회수, 손패 장수 마스터 기록, `"RoomDeck"` 삭제 (B-1 · B-2 · B-7) | PR #40 `921de03` |
| 뽑은 카드는 주인에게만 · 인스턴스 ID 포함 (K-1 해결, K-2 서버 쪽) | `41673c7` |
| 드로우 후 턴 종료 (메인 행동 중 드로우 쪽) | `80ea520` |
| `Muffin.Game.Server` asmdef + EditMode 테스트 환경 (옛 1-4 해결) | `1462ae0` |

### 1-2. 남은 일 — 기능별

| 기능 | 할 일 | 근거 |
| --- | --- | --- |
| 1 약속 | `GameEvents.OnDrawn` 에 인스턴스 ID (K-2 UI 쪽) · 손패에서 빠지는 통지 (K-3). **약속 파일 — B 리뷰** | K-2 · K-3 |
| 1 서버 코어 | 손패 **내용** 원본을 마스터로 (`_handCounts` → 플레이어별 `CardInstance` 목록). 버림 요청을 인스턴스 ID 로 받아 소유 검증, 인스턴스 ID 재발급 제거 | `05-deck` 10절 · `09` 7절 |
| 1 시작 순서 | 턴 순서 무작위 (지금 `PlayerList` 순서), HP 100 을 마스터가 기록 (지금 `PlayerPresenter.Init` 이 각 클라에서 기록) | `01` 3절 |
| 1 테스트 | EditMode — 셔플 · 5장 배분 · 드로우 · 소진 재생성 · 드로우 후 턴 종료 · 거절 (지금 연결 확인 1개뿐). 셔플 난수를 주입할 수 있어야 결정적 | `development-plan` 기능 1 · 3 |
| 2 턴 | 턴 넘김을 한 곳으로 모으기 (`AdvanceTurn` — **제안, 미결정**). 생존자만 · 나간 사람 건너뛰기, 20초 마감 | `03` |
| 3 뽑기 | 덱 · 버림 더미가 모두 0장일 때 — **기획 미정**. 지금은 임시로 거절만 | `05-deck` 5절 |
| 4 행동 카드 | 카드 사용 후 턴 종료. 버림 요청의 "내 턴" 검증 재설계 (강제 버림 · 카운터는 남의 턴에 일어난다) | `03` 3절 · `05-deck` 7절 |

> `develop` 에서 아직 안 받은 것: `CLAUDE.md` · `.claude/` 변경, `TempGameScene` 에서 GameServer 오브젝트 제거(`2981199`). 머지할 때 멀티 테스트 씬에 `PunGameServer` 가 남는지 확인한다.

### 1-3. `Presentation` 에 남은 로직 (A 가 대체, B 가 제거)

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

1-2 의 기능별 남은 일과 진단 번호(K- · A- · B- · C-)는 각 기능 작업에서 함께 해결한다. 예: K-2 · K-3 은 기능 1 약속, C-2(다음 턴 계산 버그)는 기능 2.

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
