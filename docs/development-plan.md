# 개발 작업 플랜 (2인 · 로직 / UI)

**작성일**: 2026-09-22
**대상**: 프로그래밍 파트 2인 (게임 로직 1 · UI 1)
**기준 문서**: [`systems/`](systems/) · [`refactoring-plan.md`](refactoring-plan.md)
**트랙 문서**: [`plan-a-logic.md`](plan-a-logic.md) (A · 로직) · [`plan-b-ui.md`](plan-b-ui.md) (B · UI) — **2026-09-24 분리.** 작업 목록은 트랙 문서, 이 문서는 공통 규칙 · 게이트 · 동기화 지점

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

작업 목록은 트랙 문서로 나눴다. **이 절은 게이트(합류 조건)와 동기화 지점만 가진다.**

| 트랙 | 문서 |
| --- | --- |
| **A — 게임 로직** | [`plan-a-logic.md`](plan-a-logic.md) |
| **B — UI** | [`plan-b-ui.md`](plan-b-ui.md) (아웃게임 세부: `docs/title-ui` 브랜치의 `title-ui-plan.md` · `ui-refactoring-plan.md`) |

각 마일스톤 끝에서 **ParrelSync 4클론으로 실제 플레이 가능**해야 한다. 기간은 2인 기준 추정치.

| 마일스톤 | 기간 | A | B | 게이트 (양쪽 합류 조건) |
| --- | --- | --- | --- | --- |
| **M0** 계약 확정 | 3일 | 네트워크 2건 (A0-1, A0-2) ✅ | 계약 리뷰, 로딩 팝업 | ✅ 계약 3종 머지(PR #10). 잔여: 접속 실패 시 안내 없이 정지하는 문제 해소 |
| **M1** 마스터 권한 / UI 재연결 | 2주 | `GameServer`, 요청 파이프라인, 시작 시퀀스 | `FakeGameServer`, 좌석 연결, 입력, 팝업 인프라, 타이틀 | 4인 입장 → 마스터가 셔플한 덱에서 각자 5장 → 전원 화면에 HP 100 · 손패 장수 · 턴 외곽선 정상 |
| **M2** 카드 파이프라인 + 함정 | 2주 | 체인 마스터 소유, 5초 마감, `EffectContext`, 함정 슬롯 | 반응 타이머, 체인 · 카운터 · 함정 UI, 거절 사유 | A09 → C05 → 체인 역순이 4인 전원 화면에서 동일 |
| **M3** 체력 · 턴 · 승리 | 1.5주 | `HealthService`, `LifeState`, 턴 타이머, 찹츄 | HP 위험색, 사망 표시, 턴 타이머, 찹츄 버튼, 결과 화면 | 게임이 **끝난다.** 처치 승리 · 찹츄 승리 모두 |
| **M4** 카드 대량 구현 | 2주 | 복잡한 효과, MVP 6장 검증 | **로직 투입** — 단순 효과 + 59장 SO | MVP 6장 통과 후 59장 |
| **M5** MVP 마감 | 2주 | 방 시스템, 재접속, asmdef, 로그인 | 모바일 빌드 · 해상도 · 터치 | 모바일 기기에서 4인 1판 완주 (제안) |

### 3-1. 동기화 지점 (A ↔ B 가 만나는 곳)

두 트랙은 아래 지점에서만 서로를 기다린다. 나머지 기간에는 각자 머지한다.

| # | 시점 | 먼저 끝내는 쪽 | 내용 | 기다리는 쪽 |
| --- | --- | --- | --- | --- |
| S1 | M1 첫 PR | A (A1-1) | **[계약]** `OnDrawn` 공개/비공개 분리 + `CardInstance` 전달. 현재 카드 ID 가 전원에게 새고, UI 가 `InstanceId` 를 몰라 `RequestPlayCard` 를 부를 수 없다 | B 의 `FakeGameServer` (B1-1) |
| S2 | M1 끝 | A (A1-5) | `IGameRequests` Photon 구현체 머지 → B 가 **교체 PR** (`FakeGameServer` → 실제, `Presentation` 의 옛 로직 제거) | M1 게이트 |
| S3 | M2 첫 PR | A (A2-1) | **[계약]** 체인 · 반응 마감 · 함정 · 손패 제거 이벤트 | B 의 M2 전체 |
| S4 | M3 초 | A (A3-4) | 턴 마감 시각 이벤트 | B 의 턴 타이머 (B3-2) |
| S5 | M4 시작 | A (A4-1) | 효과 SO 기반 클래스 동결 | B 의 단순 효과 (B4-1) |
| — | 수시 | A | ✅ B 의 요청 R-1 ~ R-3 (`plan-a-logic.md` 3절 · 3-1 인계) | B 의 PR4 · PR6 |

> **S1 이 늦어지면 B 의 M1 인게임이 전부 멈춘다.** A 는 M1 첫날 S1 부터 올린다.

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

| # | 항목 | 편입 (트랙 문서 번호) |
| --- | --- | --- |
| 1 | `PhotonConnection.Initialize()` 가 인터넷 없을 때 조기 반환 → `AutomaticallySyncScene = true` 설정을 건너뛴다. 이 값이 꺼진 채 연결되면 `PhotonNetwork.LoadLevel` 이 동기화되지 않아 **인게임 진입이 실패**한다 | **M0** · A0-1 ✅ |
| 2 | `OnDisconnected` 의 처리 분기가 전부 주석. 접속 실패 시 안내도 복구도 없다. 연결 타임아웃도 없음 | **M0** · A0-2 ✅ (표시는 B) |
| 3 | 미사용 `Resources/Popups/LoadingPopup.prefab` 을 접속 대기 표시에 연결 | M0 · B0-2 |
| 4 | 모든 Canvas 가 `ConstantPixelSize` — 모바일 해상도 대응 없음. `ProjectSettings` 는 4방향 자동회전 | **M1 (기획 4-3 확정 후)** · B1-14 |
| 5 | `PopupManager.CloseAllModals` 가 `.gameObject` 대신 컴포넌트를 `Destroy` → 씬 전환 시 팝업이 화면에 남는다 | M1 · B1-9 (U-1) |
| 6 | 닉네임 구현 2벌 (`NicknameInput` vs `NicknameInputLogic`) — 검증 규칙이 서로 다름. `NicknameValidator` 로 단일화 | M1 · B1-11 (U-19) |
| 7 | 저장된 닉네임 복원 미동작 — `PhotonConnection.SetupInitNickname()` 은 호출처 없는 죽은 코드 | M1 · B1-13 + A 요청 R-2 ✅ |
| 8 | `NicknameInput.LoadScene()` 이 `async void` (`CLAUDE.md` 12절 위반), 취소 처리 없음 | M1 · B1-12 (U-14) |
| 9 | `PlayerSettings` 의 `productName: CardGame` / `companyName: DefaultCompany` 미설정 | M5 |
| 10 | `RoomPanel` 의 나가기가 `DebugLobbyScene` 으로 이동 (개발 편의. 정식 흐름은 `LobbyScene`) | M5 · B5-2 ✅ (들어온 씬으로 복귀) |

1 · 2번은 접속 실패 시 사용자가 **아무 안내 없이 정지**하는 문제라 M0 에서 같이 처리한다.
