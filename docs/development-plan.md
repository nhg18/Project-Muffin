# 개발 작업 플랜 (2인 · 로직 / UI)

**작성일**: 2026-09-22
**대상**: 프로그래밍 파트 2인 (게임 로직 1 · UI 1)
**기준 문서**: [`systems/`](systems/) · [`refactoring-plan.md`](refactoring-plan.md)

---

## 0. 결론 먼저

남은 순서는 [`refactoring-plan.md`](refactoring-plan.md) 그대로다.

```
Phase 2(마스터 권한) → Phase 3(카드 파이프라인) → Phase 4·5(체력 · 턴/승리) → Phase 7(카드 59장)
```

단, **Phase 6(UI)은 별도 단계가 아니라 전 구간에 병렬로 깐다.** 2인이 서로를 막지 않으려면
Phase 를 순서대로 한 명씩 맡는 방식으로는 안 된다.

병렬화의 핵심 장치는 하나다.

> **경계 계약 3종을 M0 에서 확정하고, UI 담당은 `FakeGameServer`(로컬 모의 마스터)에 붙여 개발한다.**

이게 없으면 UI 담당은 Phase 2 가 끝날 때까지 2~3주를 대기하거나,
나중에 다시 붙여야 할 코드를 쓰게 된다.

---

## 1. 역할 분담과 경계

| | **로직 담당 (A)** | **UI 담당 (B)** |
| --- | --- | --- |
| 소유 폴더 | `Game/`, `Network/`, `Core/` | `Presentation/`, `UI/` |
| 소유 씬 · 프리팹 | 없음 (`.cs` 만) | 모든 `.unity` / `.prefab` **단독** |
| 책임 | 마스터 권한, 상태 원본, 검증, 카드 효과 | 표시, 입력, 연출, 팝업, 씬 배선 |
| 금지 | `Presentation` 타입 직접 참조 | 규칙 판정 · 상태 변경 코드 작성 |

* 씬 파일은 병합이 사실상 불가능하므로(`CLAUDE.md` 14절) **B 가 전부 소유**한다.
  A 가 씬 오브젝트가 필요하면 B 에게 요청한다.
* A 의 `Presentation` 참조 금지는 취향이 아니라 **asmdef 분리(Phase 1-5)의 전제**다.
  현재 `CardPlayManager` → `CardPresenter` 참조가 순환을 만들어 어셈블리를 못 나누고 있다.

---

## 2. 경계 계약 3종 (공동 소유)

아래 세 곳만 양쪽 리뷰 필수. 나머지는 각자 머지한다.

| 파일 | 역할 |
| --- | --- |
| `Core/PlayerProps.cs` · `Core/RoomProps.cs` | Photon 프로퍼티 키 상수 (`hp`, `handCount`, `trapCount`, `lifeState`, `chapChu`, `deckCount`) |
| `Game/GameEvents.cs` | 마스터 → UI **단방향 통지**. 모든 이벤트에 `actorNumber` 포함 |
| `Game/IGameRequests.cs` | UI → 마스터 **단방향 요청** (`RequestDraw` / `RequestPlayCard` / `RequestSetTrap` / `RequestDeclareChapChu` …) |

현재 `GameEvents.OnHPChanged(float)` 에는 actorNumber 가 없어 **누구의 HP 인지 알 수 없다.**
좌석 UI 를 연결할 수 없는 근본 원인이므로 M0 에서 반드시 바꾼다.

---

## 3. 마일스톤

각 마일스톤 끝에서 **ParrelSync 4클론으로 실제 플레이 가능**해야 한다. 기간은 2인 기준 추정치.

### M0 — 계약 확정 (같이, 3일)

| 작업 |
| --- |
| 노션 9/18 갱신분을 `systems/03-turn.md` 에 반영 (20초 확정, **자동 제출 폐기**, 강제 퇴장 N 결정) |
| `PlayerProps` / `RoomProps` 상수화, `"PlayerHP"` / `"HP"` / `"CardsCount"` / `"HandCount"` 혼재 제거 |
| `GameEvents` 재정의 — 전 이벤트에 `actorNumber` 추가, `RaiseHandCountChanged` 오호출 수정(C-1) |
| `IGameRequests` 인터페이스 스텁 + `CardInstance` 구조체 정의 |
| `GameStatus` 정리 (초기 손패 **5장**, HP `int`) |

> 이 단계는 **한 PR 로 둘이 같이** 끝낸다. 여기서 갈라지면 이후 모든 병합이 충돌한다.

### M1 — 마스터 권한 / UI 재연결 (2주, 병렬)

| A (로직) | B (UI) |
| --- | --- |
| `GameServer` 도입: 덱 · 손패 · HP · 턴순서 · 버림 더미 원본 소유 | `FakeGameServer` 작성 — `IGameRequests` 구현 + `GameEvents` 발행 (오프라인 개발용, 이후에도 유지) |
| 요청-검증-전파 파이프라인 (`if (!PhotonNetwork.IsMasterClient) return;` 강제) | `PlayerSeat` HP 게이지 · 손패 장수 실제 연결 (C-13, C-14) |
| `Deck.Shuffle()` 구현, 소진 시 버림 더미 재생성 (C-6, C-7) | 좌석 계산을 `SeatManager` 한 곳으로 통합 (C-16), `OnEnable` 싱글톤 접근 제거 (C-15) |
| `"RoomDeck"` 제거 → `deckCount` 만 공개 (B-1) | `PutAwayMyCards` 중복 45줄 공통화 |
| 게임 시작 1회 시퀀스: 셔플 → 인스턴스 ID → HP 100 → 5장 배분 → 턴 순서 무작위 | 입력 재작성: 우클릭 취소 → 모바일 입력 (C-21), `TargetSelectionManager` 취소 토큰 (C-12, C-19) |

**게이트**: 4인 입장 → 마스터가 셔플한 덱에서 각자 5장 → 전원 화면에 서로의 HP 100 · 손패 장수 · 턴 외곽선이 정상 표시.

### M2 — 카드 파이프라인 + 함정 슬롯 (2주, 병렬)

| A (로직) | B (UI) |
| --- | --- |
| 체인을 `GameServer` 단독 소유로, `isCanceled` 마스터 전용 (A-2) | 반응 타이머 UI (5초, 전원 동일) |
| `Invoke` 제거 → `PhotonNetwork.Time` 기준 5초 마감, 카운터 등록 시 재시작 (B-4, B-5) | 체인 표시 UI (**기획 확정 필요**) |
| `EffectContext` 도입 (대상 0개도 1회 실행, 다중 대상 일괄 계산 — C-9, C-10) | 카운터 사용 UI (**기획 확정 필요**) |
| 함정 슬롯 데이터 3칸 (설치 / 발동 / 파괴 / 공개, 개수만 공개) | 함정 슬롯 UI + 설치 · 발동 인터랙션 |
| 사용한 카드 손패에서 제거 → 버림 더미 (C-8) | 카드 사용 거절 사유 표시 (`ToastPopup` 재사용) |

**게이트**: A09(피해) → C05(무효화) → 체인 역순 처리가 4인 전원 화면에서 동일하게 보인다.

### M3 — 체력 · 사망 / 턴 · 승리 (1.5주, 병렬)

| A (로직) | B (UI) |
| --- | --- |
| `HealthService` (감소 · 무효 · 전환 전부 계산 후 1회 반영), 처리 ID 중복 방지 | HP 위험색(1~20), 사망 대기 카운트, 최종 사망 좌석 비활성 |
| `LifeState` (Alive / DeathPending / Dead), 동시 사망 일괄 처리 | 턴 타이머 20초 표시 (로컬 계산, 판정은 마스터) |
| 메인 행동 1회 제한, 턴 타이머 마스터 판정 | 찹츄 버튼 (손패 정확히 10장일 때만 활성) |
| 찹츄 선언 · 해제 · 판정, 처치 승리, 무승부 | 결과 화면 (**기획 확정 필요**) |

**게이트**: 게임이 **끝난다.** 처치 승리와 찹츄 승리 양쪽 모두.

### M4 — 카드 대량 구현 (2주, 분담 재조정)

A 의 부하가 B 의 3배가 되는 구간이므로 **B 도 로직에 투입**한다.

1. A: 구조가 까다로운 효과 — `Negate`, `Redirect`, `Choice`, `Peek`, 사후 트리거(T06~T16)
2. B: 단순 파라미터 효과 — `Damage`, `Heal`, `Draw`, `Discard`, `SkipTurn` … + 카드 59장 SO 에셋 작성
3. **MVP 6장(A09 / A08 / A06 / A05 / C05 / T06)** 으로 구조 검증 → 통과 후에만 나머지 53장

> 카드 수치 N / M 확정이 전제다. 미확정이면 M4 전체가 멈춘다.

### M5 — MVP 마감 (2주)

방 시스템 확정 · 구현, 로그인, 재접속 / 마스터 이탈, `asmdef` 분리(Phase 1-5 — M2 에서 순환이 풀린 뒤),
모바일 빌드 · 해상도 · 터치 검증.

---

## 4. 기획 확정이 필요한 항목 (막는 순서대로)

| 순위 | 항목 | 막는 시점 | 데드라인 |
| --- | --- | --- | --- |
| 1 | 덱 구성 (카드별 매수, 총 장수) | M1 초기화 | **M0 중** |
| 2 | 카운터 사용 UI / 체인 표현 / 함정 설치 · 발동 UI | M2 **UI 트랙 전체** | **M1 중** |
| 3 | 화면 방향 고정 (세로 / 가로) + 기준 해상도 | M1 UI | **M1 중** |
| 4 | 연속 미제출 N (2턴 vs 3턴) | M3 | M1 중 |
| 5 | 턴 진행 방향 개념 | M2 (A07, A10) | M2 중 |
| 6 | 카드 수치 N / M 59장 전부 | M4 **전체** | M3 중 |
| 7 | 결과 화면 / 방 시작 조건 / 마스터 이탈 | M3~M5 | M3 중 |

2번이 특히 급하다. M2 에서 B 가 할 일의 대부분이 여기 걸려 있고,
`systems/10-ui.md` 11절에 미정 7건이 그대로 남아 있다.

### 노션 ↔ `systems/` 불일치 (M0 에서 해소)

노션 「개발 기획서」(9/18)가 `systems/`(9/17)보다 최신이다. `CLAUDE.md` 0절에 따라
**노션 변경을 `systems/` 에 먼저 반영한 뒤** 구현한다.

| 항목 | 노션 (9/18) | `systems/` (9/17) |
| --- | --- | --- |
| 턴 제한 시간 | 20초 **확정** | 20초 **제안** |
| 시간 초과 처리 | **자동 제출 폐기**, 그냥 턴 넘김 | `AutoSubmit` 확정 / 대상 결정 미정 |
| 연속 미제출 | 강제 퇴장 (N 미정) | AFK vs 강제 퇴장 택1 미정 |

반영하면 `refactoring-plan.md` 4부의 막힌 항목 중 #3 · #4 가 사라진다.

---

## 5. 운영 규칙

1. **씬 · 프리팹은 B 단독.** A 는 `.cs` 만 만진다.
2. **브랜치는 마일스톤이 아니라 작업 단위.** 한 PR 에 폴더 이동과 로직 변경을 섞지 않는다.
3. **계약 3종 변경 PR 은 양쪽 리뷰 필수**, 나머지는 셀프 머지 허용.
4. **`GameServer` 는 Photon 없이 테스트 가능하게 설계한다.**
   셔플 · 드로우 · 체인 역순 · 다중 대상 일괄 계산은 EditMode 테스트로 검증한다.
   2인이 매번 4클론을 띄우는 비용을 없애는 것이 가장 큰 생산성 이득이다.
5. `FakeGameServer` 는 M1 이후에도 버리지 않는다. B 의 UI 작업마다 4클론이 필요 없어진다.
6. 하지 말아야 할 것은 [`refactoring-plan.md`](refactoring-plan.md) 5부를 그대로 따른다.

---

## 6. 부트스트랩 · 타이틀 구간 점검 (2026-09-22)

### 이번에 처리한 것

| 항목 | 처리 |
| --- | --- |
| 빌드 세팅에 삭제된 `GameScene.unity` 항목 잔존 | 제거 |
| 빌드 인덱스 0 이 `DebugLobbyScene` (빌드가 부트스트랩을 건너뜀) | `TitleScene` 을 0번으로. `DebugLobbyScene` 은 개발용으로 유지(마지막) |
| `Refactor/RefactorGameScene` | `Scenes/GameScene` 으로 이름 변경 (GUID 유지) |
| `ScenePaths.SceneType.Game` 이 없는 씬을 가리킴 | 이름 변경으로 해소. `RoomPanel` 의 하드코딩 문자열 제거 |
| `ScenePaths.SceneType.BootStrap` (파일명 대소문자 불일치) | 제거 |
| `BootstrapScene` 의존 | **삭제.** `Core/GameBootstrap.cs` 가 `Resources/Bootstrap/` 프리팹을 `BeforeSceneLoad` 에 생성 |
| 접속 시작 책임 | `NetworkManager.Start()` 로 이동 (`Core` → `Network` 의존 제거) |
| 타이틀에서 접속 완료 보장 상실 | `NicknameInput` 이 `ConnectionEvents.OnConnected` 까지 제출 버튼 비활성 |

### 남은 항목 (마일스톤에 편입)

| # | 항목 | 편입 |
| --- | --- | --- |
| 1 | `PhotonConnection.Initialize()` 가 인터넷 없을 때 조기 반환 → `AutomaticallySyncScene = true` 설정을 건너뛴다. 이 값이 꺼진 채 연결되면 `PhotonNetwork.LoadLevel` 이 동기화되지 않아 **인게임 진입이 실패**한다 | **M0** |
| 2 | `OnDisconnected` 의 처리 분기가 전부 주석. 접속 실패 시 안내도 복구도 없다. 연결 타임아웃도 없음 | **M0** |
| 3 | 미사용 `Resources/Popups/LoadingPopup.prefab` 을 접속 대기 표시에 연결 | M0 |
| 4 | 모든 Canvas 가 `ConstantPixelSize` — 모바일 해상도 대응 없음. `ProjectSettings` 는 4방향 자동회전 | **M1 (기획 4-3 확정 후)** |
| 5 | `PopupManager.CloseAllModals` 가 `.gameObject` 대신 컴포넌트를 `Destroy` → 씬 전환 시 팝업이 화면에 남는다 | M1 |
| 6 | 닉네임 구현 2벌 (`NicknameInput` vs `NicknameInputLogic`) — 검증 규칙이 서로 다름. `NicknameValidator` 로 단일화 | M1 |
| 7 | 저장된 닉네임 복원 미동작 — `PhotonConnection.SetupInitNickname()` 은 호출처 없는 죽은 코드 | M1 |
| 8 | `NicknameInput.LoadScene()` 이 `async void` (`CLAUDE.md` 12절 위반), 취소 처리 없음 | M1 |
| 9 | `PlayerSettings` 의 `productName: CardGame` / `companyName: DefaultCompany` 미설정 | M5 |
| 10 | `RoomPanel` 의 나가기가 `DebugLobbyScene` 으로 이동 (개발 편의. 정식 흐름은 `LobbyScene`) | M5 |

1 · 2번은 접속 실패 시 사용자가 **아무 안내 없이 정지**하는 문제라 M0 에서 같이 처리한다.
