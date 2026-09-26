# 작업 기록 (구현 세션)

> 구현 세션이 **완료한 것 · 계획과 달라진 것 · 다음 할 일**을 남긴다. 플랜 세션과 다음 구현 세션은 여기서 이어간다.
> 계획 자체는 `title-ui-plan.md` 등 플랜 문서가 기준이다. 이 문서는 진행 상황과 결정 기록만 가진다.
> 최신 항목이 위.

---

## 2026-09-26 · 되돌림 — 브랜치 정리 때 바꾼 씬 · 코드

사용자 지시로 브랜치 정리(`a0aa2e1`)에서 AI 가 바꾼 씬 · 코드 3가지를 되돌림: `GameScene` develop 버전 복원 → 이전(`ui`, 턴 표시만 있는 씬)으로, 되살린 `TurnUI.cs` 삭제, `Scenes/Dev/TurnPractice.unity` 삭제. 문서의 연습 씬 언급도 지움. `FakeGameServer` · `TurnView` · `TurnPresenter` · `IGameState` 는 2026-09-25 `ui` 커밋(`979effa` · `6746c54`) 그대로.

⚠ 이대로 PR #36 이 develop 에 머지되면 develop 의 옛 인게임 `GameScene` 이 비워진다.

---

## 2026-09-26 · 브랜치 정리 — logic · ui 두 트랙으로

| 항목 | 값 |
| --- | --- |
| 브랜치 | `ui` (로컬 v5 + `changhwan.exe` 병합) |
| 범위 | 브랜치 구조 · `CLAUDE.md` 14절 · 문서 v5 정합 · `GameScene` 복원 |

### 결정 (사용자, 2026-09-26)

- 구조: `main` ← `develop` ← `logic` · `ui` 두 트랙. UI 브랜치 이름은 `ui` (`changhwan.exe` 폐기). 규칙은 `CLAUDE.md` 14절.
- `GameScene` 은 develop 의 옛 인게임 배치로 복원 — 로직 담당이 쓰는 씬을 지우지 않기 위해. 턴 표시 연습(`TurnView` · `TurnPresenter` · `FakeGameServer`)은 `Scenes/Dev/TurnPractice.unity` 로 옮김 (빌드 설정 밖). 씬이 쓰는 `TurnUI.cs` 도 develop 에서 되살림.
- `logic` 은 develop + `db change`(`refactor/logic4` 의 남은 1커밋)로 새로 만듦.

### 한 것

- 로컬 `ui` (플랜 v5 커밋 3개, 미푸시) + `changhwan.exe` (문서 재검토 · develop 스프라이트 정리) 병합. 플랜 문서 충돌은 **v5 를 기준**으로 두고 재검토 내용 중 유효한 것만 다시 얹음: 약속 파일 4종, 끝난 인계 ✅, C-11 해결, 타이틀 S8 을 `plan-b-ui` 2절 "아웃게임 남은 것"으로, 처치 승리 판정 시점을 기능 8 · 11/4 마감에 추가. `18-sandbox.md` 는 v5 대로 삭제.
- 문서의 v4 작업 번호(B1-x · A1-x · M1 ~ M8)를 v5 기능 번호로. `refactoring-plan` 5부의 "샌드박스로 해제" 주석 철회 ("Phase 2 이전" = 기능 1 서버 코어 이전).
- `versioning`: v5 에 마일스톤이 없어 **MINOR = 점검일 순번** 으로 (12/12 첫 완성판 = `0.5`, 사용자가 고른 "첫 완성판 0.5" 유지).

---

## 2026-09-26 · 플랜 v5 승인 — 마감 +10일, 기능 4 = 행동 카드 내기

- 사용자가 v5 를 승인했다. 승인 조건 두 가지를 반영했다.
- 기능 4 이름을 "행동 카드 내기"로 바꾸고 범위를 행동 카드로 못 박았다 (카운터는 기능 5, 함정은 기능 6).
- 마감일 전체를 +10일. 시작일 9/28 은 그대로. 첫 약속 10/11 · 첫 점검 10/21 · 카드 59장 11/28 · 첫 완성판 12/12 · 출시 2/4.

---

## 2026-09-26 · 플랜 v5 — 기능별로 재구성, 샌드박스 철회

| 항목 | 값 |
| --- | --- |
| 브랜치 | `ui` (worktree `../Project-Muffin-plan`) |
| 범위 | 문서만 — `development-plan.md` 재작성, 트랙 A · B 작업 목록을 본 문서로 일원화, `systems/18-sandbox.md` 삭제 |

### 결정 (사용자)

- 샌드박스는 만들지 않는다 — 개발 작업이 어차피 같다. v4 철회.
- "상태 먼저 → 검사 → 자동 처리" 흐름은 유지하고, **기능 단위**로 쪼갠다.

### 바뀐 것

- 기능 13개: 게임 시작하기 · 턴 넘기기 · 카드 뽑기 · 카드 내기 · 카운터로 막기 · 함정 쓰기 · 체력과 사망 · 찹츄와 승리 · 카드 59장 · 첫 완성판 · 재접속과 다듬기 · 로그인과 친구 · 출시 준비. 끝 날짜(12/2 · 1/25) 그대로.
- 기능마다: 플레이어가 겪는 것 · 기준 문서 · 기획 결정 필요 · 로직 / UI 순서표 · 끝났다는 기준.
- 약속을 한 번에 크게 올리지 않고 기능마다 작게 먼저.
- 점검일 10/11 · 10/25 · 11/4 · 11/18 · 12/2 · 12/16 · 12/30 · 1/25.
- 로직 · UI 작업 목록은 `development-plan.md` 3절 한 곳. 트랙 문서는 코드 상태 · 진단 번호 → 기능 매핑만.
## 2026-09-26 · 기획서 · 개발 문서 재검토 (코드 대조 · 낡은 곳 정리)

| 항목 | 값 |
| --- | --- |
| 브랜치 | `docs/review-0926` (`ui` 플랜 v4 + `develop` 병합 — worklog 충돌만 수동 해결) |
| 범위 | 문서만. 코드 · 씬 변경 없음 |

### 한 것

- **개발 문서**: 트랙 A · B 를 v4 에 맞춤 — 상위 문서 v3 → v4, 계약 3종 → 4종(`IGameState`), M3 ~ M5 기간을 날짜로, 동기화 지점 번호(S5 → S6, S8 → S7), M5 작업 번호를 트랙 문서와 `development-plan` 에서 일치(B5-0 디자인 교체 신설). 끝난 인계(A0-3 · PR4 · PR6) ✅. `development-plan` 주간 리듬의 "M2 까지 종이" → 10/9 부터 샌드박스. C · D 트랙 머리말에 "v3 기준, v4 미반영 · 보류" 표시.
- **`refactoring-plan`**: 머리말에 "1부 진단 번호만 유효, 순서 · 우선순위 · 기획 목록은 development-plan 으로 대체". 5부 "새 카드 에셋 · 함정 슬롯 UI 금지"는 샌드박스로 해제 표시.
- **B 누락 작업**: 타이틀 S8(연결 중 · 실패 상태)이 미구현인데 "아웃게임 전부 완료"로 적혀 있었다 → `plan-b-ui` **B1-15** 신설, README · 12 상태 정정, 아티팩트 기준 Version 13 으로 통일.
- **`ui-refactoring-plan`** 완료 · 기록 문서 표시(없는 `ui-plan.md` 링크 제거). `title-ui-plan` 은 S8 남음 표시.
- **시스템 기획서 코드 대조** (00 ~ 18): 초기 손패 7 → 5, 없는 `GameRule` 참조 삭제, `CardCondition` · `MyTurnCondition` 이름, 손패 제거 · `handCount` 기록 주체, `(Host)` → `(방장)`, `RoomPanel` → `RoomPresenter`, 방장 교체 로그 구현됨, `LoadingPopup` 사용처, SafeArea 적용 범위, 로비 캔버스 Expand, 친구 = M7, README 상태 열(04 ~ 09 "작성 완료" → "일부 미정"), 18 에 현재 구현 상태 절. 함정 **설치** 시점 문구를 03 · 04 확정(자신의 턴)에 맞춤.
- README 에 "인게임 구현 상태는 코드 기준 — 지금 GameScene 은 비어 있다" 안내.

### 발견만 하고 고치지 않은 것

- ⚠ `EditorBuildSettings` 씬 순서가 `DebugLobby 0 · Title 1 …` (`bbf48bc`). 빌드 시작 씬이 디버그 로비다 — 01 에 경고만 적음.
- 코드 주석이 낡음: `RandomCode.cs` · `PhotonRoom.cs` 의 "08-room 5절 미정" (이미 확정).
- `GameStatus` 규칙 수치가 MonoBehaviour public 필드 (씬 값이 덮어씀) — 05 에 적음, 서버 1층에서 해소.
- 게임 중 이탈 표기가 01 제안 · 02 확정 · 01 §7 미정으로 섞임 — 4-1 #7 (11/18) 결정 때 정리.

### 결정 (사용자, 2026-09-26)

- 처치 승리 판정 시점(02 "즉시" vs 07 "모든 연쇄 뒤") → **미정**으로 되돌림. 4-1 #4 에 추가 (10/25), README 전역 미정 #10.
- 찹츄 첫 선언 시점("언제든" 가정 vs 재선언 "자신의 턴") → **미정**. 4-1 #4, README #11.
- 버전: **MINOR = 마일스톤 번호** (M5 MVP = `0.5.x`). `versioning.md` 변경, 지금 값 `0.1.0` 그대로.
- 이 정리는 `changhwan.exe` 로 올림 (`ui` 의 플랜 v4 · 코드 변경 포함).
- GDD_GUIDE 형식 위반 다수(변경 이력 절 없음, 구분 열 없는 표, 분류 값) — 문서별 내용 수정 때 같이.

---

## 2026-09-26 · 플랜 v4 — 샌드박스 먼저

| 항목 | 값 |
| --- | --- |
| 브랜치 | `ui` (worktree `../Project-Muffin-plan` — 메인 폴더는 다른 세션이 `refactor/ui-sprites` 작업 중) |
| 범위 | 문서만 — `systems/18-sandbox.md` 신설, `development-plan.md` v4, 트랙 A · B 의 M1 · M2, README |

### 결정 (사용자, 2026-09-26)

- 로직 규칙 없이 카드를 뽑고 내는 **실제 게임 화면의 종이 테스트 환경(샌드박스)** 을 1단계로. 로직이 되면 이어붙인다.
- 조건: 샌드박스 = 실제 서버의 첫 층(상태 원본 · 비공개 전달). 검사층 · 효과층을 2단계부터 켠다. 별도 임시 서버를 만들지 않는다.
- 기획 · 디자인 트랙(C · D)은 사용자 검토 전 보류.

### 바뀐 것

- M1 = 샌드박스 (10/1 약속 · 10/6 서버 1층 · 10/9 Photon · **10/9 첫 샌드박스 플레이테스트**), M2 = 규칙 붙이기. 일정 끝(1/25)은 그대로.
- 가짜 서버 → 로컬 서버: 서버 1층(순수 C#)이 나오면 흉내를 지우고 에디터에서 그대로 돌린다.
- 요청 약속 2종: 게임 요청 + 샌드박스 요청(수동 조작 16가지).
- 카드 표 가져오기 도구를 M1 로 앞당김 — 59장 문구만으로 샌드박스에서 시험.
- 카드 이동 알림을 "영역 이동" 하나로 묶자는 제안 (A 결정).

### 다음 할 일

- [ ] 로직 담당과 순서 합의 (약속 10/1)
- [ ] B1-0 GameScene 재구성 — develop 의 옛 씬을 가져올지 결정

---

## 2026-09-26 · 플랜 v3 — 6인 팀 역할 · 트랙 C(기획) · D(디자인) 신설

| 항목 | 값 |
| --- | --- |
| 브랜치 | `ui` |
| 범위 | 문서만 — `development-plan.md` 재작성, `plan-c-planning.md` · `plan-d-art.md` 신설, `CLAUDE.md` 0절 표, 트랙 A · B 머리말 |

### 한 것

- 팀 구성(로직 1 · UI 개발 1 · 기획 2 · 카드 디자인 1 · UI 디자인 1)에 맞춰 플랜을 v3 로. 일정(M1 9/28 ~ M8 1/25)은 v2 그대로.
- 핵심 규칙: 기획 · 디자인은 **한 마일스톤 앞서** 결정 · 시안을 넘긴다. 게이트 확인은 기획 X.
- 기획 2인을 R(규칙 · 카드) · X(화면 · 경험)로 나눠 미정 11건 전부 담당 지정. 초보용 온보딩 2주 · 결정 요청서 양식 · 종이 프로토타입 · 플레이테스트 기록 · 카드 시트 · 카드 테스트 케이스 표 · 게이트 체크리스트 · GitHub 웹 편집법 · 용어집.
- 디자인 납품 D1 ~ D12, 공유 폴더 전달 규칙, 규격표 양식. 카드 규격 820 × 1222(지금 임시 프레임과 같음, 제안), 글자는 이미지에 굽지 않음(카드 프리팹이 TMP 로 이름 · 문구를 그림).

### 열린 것 (사용자 · 팀 결정 필요)

- 팀장 · 최종 결정권자 미지정 → M1 첫 월요일
- 기획 R · X 배정
- 카드 일러스트 범위(59장 고유 vs 계열 공용) → 10/25 MVP 6장 속도 보고 뒤
- 사운드 담당 없음 → 기획 X 가 에셋 선정
- 전원 가용 시간 모름 → 날짜는 가정, M1 회고에서 재조정

---

## 2026-09-26 · UI 스프라이트 정리 (미사용 삭제 · 폴더 · 이름)

| 항목 | 값 |
| --- | --- |
| 브랜치 | `refactor/ui-sprites` (develop 기준) |
| 범위 | `Sprites/` 중 UI 만. `Sprites/Cards` · `Hands` · `sample.png` 는 손대지 않음. 씬 · 프리팹 수정 없음 |

### 한 것

- **삭제 15개**: develop 과 살아 있는 브랜치 어디서도 GUID 참조가 없는 것. 옛 로비 버튼 6(`Button1` · `Cancel Button` · `Confirm Button2` · `Create/Join Room` · `Random Match`) · 아이콘 5(`Door` · `Home` · `Icon1` · `card icon` · `circle`) · `Profile Panel1/2` · `Profile circle` · `WarningPopupConfirmButton`. 옛 main · 닫힌 PR #28 의 씬만 쓰던 것들.
- **이동 · 이름 변경 25개** (`.meta` 같이 이동 → GUID 유지, 참조 그대로). 규칙은 기존 `Sprites/UI` 의 snake_case.
  - `UI/Common/` — `ui_round64` · `ui_round64_top_highlight` · `ui_ring_r14_w2` · `ui_ring_r20_w4` · `ui_shadow_soft` · `icon_menu` · `icon_sound` (이름 유지)
  - `UI/Background/` — `bg_title` (← `TitleBackground`) · `bg_overlay_vertical` (← `title_overlay_vertical`) · `bg_ingame` (← `In-GameBackground`, develop GameScene 이 사용)
  - `UI/Popup/` — `popup_input_panel/field/submit/close` · `popup_warning_panel/button` · `popup_toast_panel/check` · `popup_loading_icon`
  - `UI/Seat/` — `seat_player_icon` · `seat_turn_display` · `seat_profile` (← `Rectangle 212`) · `seat_hp_bar` · `seat_hp_gauge` (← `Player Hp Guage`)
- 빈 폴더 `Buttons` · `Icons` · `Player` · `WarningPopup` 삭제.
- `TitleUIAssetSetup` 경로 상수 (`SpriteDir` → `UI/Common/`, `BackgroundDir` 추가). `12-title-ui` · `14-lobby-ui` · `title-ui-plan` 의 경로 갱신.

### 검증

- 씬 · 프리팹 · 에셋이 참조하는 옛 스프라이트 GUID 29개 전부 새 위치에 존재, 중복 GUID 없음.
- Unity 2022.3.62f3 batchmode 임포트: 컴파일 에러 0, 옮긴 스프라이트가 같은 GUID 로 임포트, Unity 가 수정한 추적 파일 없음.

### 남긴 것

- `PlayerSeatUI` 프리팹의 오브젝트 이름 오타 `HP Guage Image` — 로직 쪽 GameScene 이 쓰는 프리팹이라 그대로 둠.
- 팝업 스프라이트(`UI/Popup/`)는 `plan-b-ui` 의 팝업 재구성 때 공용 9-slice 로 교체 예정.

---

## 2026-09-25 · 코드 전수 대조 → 낡은 문서 정리 · Practice 접기 · 플랜 v2

| 항목 | 값 |
| --- | --- |
| 브랜치 | `ui` (prac 위) |
| 범위 | `.cs` 이동 3 · 삭제 5 · 신규 1, 씬 변경 없음(GUID 유지), 문서 13개 |

### 한 것

1. **코드 vs 기획서 전수 대조** — 결과는 `development-plan.md` v2 0절 표. 아웃게임 약 90%, 인게임 약 25%, 카드 5%, 로그인 · 재접속 · 결과 0%.
2. **낡은 문서 7곳 갱신** — 코드가 앞서 갔는데 문서가 옛 경고를 유지하던 곳: 01(방 확정 · `RoomPresenter`), 02 · 06(HP int 통일, HP 이벤트는 오지만 구독자 없음), 04(`DamageEffect` 실제 동작, 손패 제거가 승인 전 로컬), 05(초기 손패 5장 한 곳), 08(5절 8건 확정에 맞춰 머리말 · 3절 · 4절 · 8절), 09(키 상수 통일, `RoomDeck` 만 잔존), README(12 · 08 상태), development-plan §4 노션 표 해소.
3. **Practice 접음** — `Practice/` 의 계약 복사본(`GameEvents` · `IGameRequests` · `IGameState`, 빈 `GameServer`) 삭제. `TurnView` · `TurnPresenter` → `Presentation/Turn/` (네임스페이스 `Chapchu.Presentation`, `.meta` GUID 유지라 씬 참조 그대로). 옛 `Presentation/Turn/TurnUI.cs` 삭제(`TurnManager` 직접 참조, 씬 없음). `FakeGameServer` → `DebugTools/`, 실제 `Chapchu.Game.IGameRequests` · `IGameState` 구현. Start 에서 4인 HP 100 · 손패 5 · Alive · 첫 턴 전파, `RequestEndTurn` 순환, 나머지 요청은 거절 이벤트. `Game/IGameState.cs` 신설 (계약 4종째, **A 리뷰 대기**).
4. **플랜 v2** — `development-plan.md` 전면 재작성: M1~M5(MVP `0.5.0`) + M6 안정화 · M7 로그인 · 친구 · M8 완성도(`1.0.0`), 단계별 게이트 · 날짜 · 기획 마감 11건 · 동기화 지점 S0~S9. `plan-b-ui.md` 에 B1-0(`17-game-ui` + GameScene 재구성) · B2-7 · B4-3 · B5-3 · B5-4 추가, 지금 시작 가능 항목 표시. `plan-a-logic.md` 에 K-4(`IGameState` 리뷰) · A1-5 구현체 조건 추가, 로그인을 M7 로 이동.

### 검증

- MSBuild 로 `Assembly-CSharp` 컴파일 통과 (Unity 가 생성한 csproj 가 낡아 파일 목록만 고친 임시 사본으로 빌드). 경고 4건은 기존 파일.
- 에디터 실행은 안 함. 확인할 것: GameScene Play → "플레이어 1의 턴" 표시 → 턴 종료 탭 → "플레이어 2의 턴" → 다시 탭하면 콘솔에 거절 로그.

### 정정

- 직전 세션 보고에서 "턴 종료 버튼이 씬에 없다"고 했는데 **틀렸다.** `PillButton` 프리팹 인스턴스(stripped)라 grep 에 안 잡혔을 뿐 참조는 정상이다.

### 다음 할 일

- [ ] A: `IGameState` 리뷰 (K-4), A1-1 착수 (S1 병목)
- [ ] B: B1-0 `17-game-ui.md` + GameScene 재구성 → B1-2 좌석 HP · 손패 장수 (FakeGameServer 로 확인 가능)
- [ ] 기획: 덱 구성(10/4) · 카운터 · 체인 · 함정 UI(10/11) — v2 4절 마감

---

## 2026-09-25 · 대상 선택 중 다른 카드 입력 차단 (버그 수정)

| 항목 | 값 |
| --- | --- |
| 브랜치 | `refactor/logic4` |
| 범위 | `.cs` 6개 + 문서 1개. 씬 · 프리팹 수정 없음 |

### 버그

카드를 드롭해 대상을 고르는 동안 다른 카드를 또 드래그 · 드롭할 수 있었다. `CardView` 가 `isHandMode` 만 보고, "처리 중인 카드가 있다"는 상태를 아무도 갖지 않았다. 두 번째 `TargetSelectionManager.SelectPlayer` 가 같은 필드를 덮어써 좌석 한 번 탭에 두 카드가 같은 대상으로 `RequestPlayCard` 됐다.

### 한 것

- `10-ui.md` §6 에 "선택 중 \| 손패의 다른 카드 입력과 손패 올리기/내리기 차단" 행 추가 (확정). 잠금 범위는 드롭 → 대상 확정까지. 반응 5초는 카운터를 내야 하므로 잠그지 않는다.
- `PlayerHandPresenter.IsCardPlayInProgress` + `PlayCardAsync(CardPresenter)`: 손패가 카드 처리를 직렬화. 처리 중이면 두 번째 카드는 `ReturnToOrigin`. 잠금 해제는 `finally` (카드는 처리 끝에 Destroy 되므로 손패가 책임).
- `CardPresenter.Hand` (Setup 3번째 인자, 손패 밖 카드는 null). `OnCardDropped` 는 `Hand.PlayCardAsync(this)` 로 위임, 본문은 `PlayAsync()` 로 분리.
- `CardView.CanInteract` (HandMode && !Hand.IsCardPlayInProgress) 로 Enter · Down · Drag · Up 게이트. `CancelInteraction()` 으로 진행 중 드래그 · 호버 되돌림. `OnPointerExit` 는 잠금과 무관하게 확대된 카드를 되돌린다.
- `PlayerHandView.CancelAllInteractions(except)`: 잠금 시작 시 다른 카드 전부 되돌림 (멀티터치 대비).
- `ClickManager`: 처리 중엔 좌클릭의 HandsUp/Down 건너뜀. 좌석 탭이 "Card" 레이캐스트에 안 잡혀 대상 선택 중 손패가 내려가던 문제도 함께 막힘.
- `TargetSelectionManager.SelectPlayer`: 이미 대기 중이면 경고 후 0 반환 (재진입 방어선).

### 검증

- MSBuild 로 `Assembly-CSharp.csproj` 컴파일 통과. 에디터 실행 테스트는 아직 안 함 (아래 시나리오).

### 남긴 것

- `async void` → 취소 토큰 전면 재작성은 B1-6 (C-12 · C-19) 범위. 지금은 `_ = Hand.PlayCardAsync(this)` + 내부 catch/로그.
- 대상 선택 중 카드 흐리게 · 좌석 하이라이트 연출, 우클릭 취소의 모바일 대체 입력 (B1-6).

### 다음 할 일

- [ ] 에디터 확인: A 드롭 → 대기 중 B 드래그 무반응 → 좌석 탭 → `target :` 로그 1회
- [ ] 타임아웃 · 우클릭 취소 후 잠금이 풀려 B 드래그 가능한지
- [ ] 대상 선택 중 빈 곳 좌클릭에 손패가 내려가지 않는지
- [ ] `TargetType.None / AllEnemies / Me` 카드가 즉시 처리되는지
## 2026-09-25 · hotfix: 디버그 로비에서 마스터만 인게임으로 넘어가는 버그

| 항목 | 값 |
| --- | --- |
| 브랜치 | `changhwan.exe` (PR #31 에 포함) |
| 증상 | `DebugLobbyScene` → 방 → 시작하면 마스터만 `GameScene` 으로 가고 참가자는 `RoomScene` 에 남는다 |
| 원인 | `DebugScript.Start()` 의 `PhotonNetwork.AutomaticallySyncScene = false;` (`2442718`, 2026-08-04). `NetworkManager.Awake` 가 `true` 로 켠 값을 디버그 씬이 다시 꺼서 `RoomPresenter` 의 `LoadLevel` 이 동기화되지 않았다. 옛 `RoomPanel.Awake` 의 `= true` 땜질(`2f362ed`)이 이를 가려 왔고, 그 줄이 U-23(`3e1c333`)으로 삭제되자 드러났다 |
| 수정 | 그 줄 삭제. 설정은 `PhotonConnection.Initialize()` 한 곳만 담당 (A0-1 취지) |
| 확인 | 에디터 + ParrelSync 클론으로 디버그 로비 → 방 → 시작 시 두 클라이언트 모두 `GameScene` 진입 확인 필요 |

---

## 2026-09-25 · 로비를 타이틀 구조로 통일 (LobbyCanvas · LobbyView/Presenter · PillButton · 같은 배경)

| 항목 | 값 |
| --- | --- |
| 브랜치 | `feature/lobby-unify` (← `changhwan.exe` `33ee5be`, #24 머지 후) |
| 작업 위치 | worktree `../Project-Muffin-title` |
| 사유 | 사용자 지적: #24 뒤에도 로비가 타이틀과 구조가 달랐다(`Canvas`/`LobbyPanel` 단일 스크립트/기본 버튼/단색 배경). "갈아엎어" → 통일 |

### 한 것

| 항목 | 내용 |
| --- | --- |
| 씬 | `LobbyCanvas` [LobbyView · LobbyPresenter] › `BG_Illust` · `BG_Overlay`(타이틀과 같은 에셋) · `NicknameText`(B 48) · `MainButtons` › `PillButton` 프리팹 인스턴스 3개(480×200, 글자 56). 프리팹 연결 유지 |
| 코드 | `UI/Lobby/LobbyView.cs`(이벤트만) + `UI/Lobby/LobbyPresenter.cs`(옛 `LobbyPanel` 로직을 그대로 옮김 — 팝업 · 실패 문구 · 씬 전환 · `ReturnSceneAfterRoom`). `LobbyPanel` · `PlayerProfile/ProfileView` · `ProfilePresenter` 삭제 (참조: LobbyScene 뿐) |
| 문서 | `14-lobby-ui` 3절 #5 · 7-1(버튼 스타일 · 하이어라키 · 스크립트 표) · 8절(배경 = 타이틀) · 9 · 10 |
| 빌더 | `LobbySceneBuilder.cs` 로 생성 후 삭제 (이 브랜치 커밋 이력에 있음) |

### 캡처 (1920×1080 · 2400×1080 · 1280×720)

버튼 줄 정중앙, 닉네임 좌상단, 타이틀과 같은 배경 · 오버레이. 잘림 없음. 4:3 은 이전과 같이 양 끝 60 잘림(전역 미정 #9).

### 확인 필요 (에디터)

- [ ] 타이틀 → 로비(닉네임) → 방 만들기 → 방 → 나가기 → 로비
- [ ] 방 참가 → 팝업 → 없는 코드 → "방을 찾을 수 없습니다."
- [ ] 버튼 호버 · 누름 연출(PressableButton)이 480×200 에서도 자연스러운지

---

## 2026-09-25 · 방 화면 재구성 (B 트랙 2-1 순서 3, U-24)

| 항목 | 값 |
| --- | --- |
| 브랜치 | `feature/room-rework` (← `changhwan.exe` `5dae904`) |
| 작업 위치 | worktree `../Project-Muffin-connect` |
| 기준 | `docs/systems/16-room-ui.md` (신설), `08-room.md` 5절 확정, `15-screen.md` |

### 한 것

| 단계 | 내용 |
| --- | --- |
| 문서 | `16-room-ui.md` — 크기 · 배치 · 스타일(타이틀 · 로비와 동일) · 하이어라키 · 스크립트 역할 |
| 씬 | `RoomScene` Canvas 아래 재생성 (임시 빌더, 실행 후 삭제): 배경 키 아트 · 오버레이, 플레이어 칸 4개(400×400, 간격 40), `PillButton` 「게임 시작」(480×200, 하단) · 「나가기」(320×120, 좌상단), 방 코드(상단 중앙, 글자 96), 인원(우상단), 로그(좌하단 640×200). 가장자리 요소는 `SafeArea` 아래 |
| 코드 | `UI/RoomScreen/RoomView` (표시 setter + 버튼 이벤트) · `RoomPresenter` (`RoomEvents` · `PhotonNetwork` → 뷰, 시작 · 나가기). `RoomPanel` · `RoomInfoPanel` 삭제 |
| 폴더명 | `UI/RoomScreen` — `UI/Room` 은 `Photon.Realtime.Room` 과 이름이 겹쳐 금지 (`CODE_CONVENTION` 4.2) |

### 발견 · 수정

- **참가자 화면이 입장 직후 비어 있던 버그.** `AutomaticallySyncScene` 이 참가자를 방장의 씬으로 끌어오면 **`RoomScene` 이 `OnJoinedRoom` 보다 먼저 뜬다** (로그 순서로 확인). `Start()` 에서만 초기화하면 이때 `InRoom` 이 아직 아니라 건너뛴다. → `RoomEvents.OnJoinedRoom` 에서도 초기화하고 1회 가드. 옛 `RoomInfoPanel` 도 같은 조건이었다.
- 2클라이언트 테스트에서 가짜 방장이 `TitleScene` 에 머물면 참가자가 타이틀로 끌려간다 — 테스트 환경 문제(실제 방장은 방 씬에 있음). 방장 스크립트가 `RoomScene` 을 로드하도록 수정.

### 검증 (batchmode, 실제 Photon)

| 시나리오 | 결과 |
| --- | --- |
| bounds @ 1920×1080 · 2048×1536 · 2400×1080 · 2560×1600 | 8요소 전부 화면 안, 겹침 없음 |
| 방장 1명: 코드 4자 · `1 / 4` · 칸0 `이름 (방장)` · 시작 표시+비활성 · 입장 로그 · 비활성 시작 무시 · 나가기 → 로비 | 통과 |
| 참가자(2대): 입장 직후 `2 / 4` · 칸0 호스트(방장) · 칸1 본인 · 시작 숨김 · 로그 → 방장 퇴장 후 칸0 본인(방장) · `1 / 4` · 시작 표시+비활성 · 퇴장 · 방장 교체 로그 → 나가기 → 로비 | 통과 |

### 다음 할 일

- [ ] 디자인 게시 후 색 · 스프라이트 교체 (배치 유지)
- [ ] 실기기 Safe Area · 터치 크기 확인
- [ ] 2-1 순서 4: 팝업 재구성 (타이틀 박스 스타일)

---

## 2026-09-25 · 로비 개편 0~2단계 (문서 · 캔버스 초기화 · 배치)

| 항목 | 값 |
| --- | --- |
| 브랜치 | `feature/lobby-layout` (← `changhwan.exe` `8b4ff88`) · PR #21 |
| 작업 위치 | worktree `../Project-Muffin-title` (Library 재사용) |
| 기준 | `docs/systems/14-lobby-ui.md` (이번에 신설). 디자인 아티팩트는 개편 예정이라 참고하지 않음 |

### 한 것

| 단계 | 내용 |
| --- | --- |
| 0 | `14-lobby-ui.md` 신설(크기 · 배치 규칙), `08-room.md` 랜덤 매칭 확정, `01-game-flow.md` · `README.md` 연결 |
| 1 | `LobbyScene` CanvasScaler 800×600 고정 → 1920×1080 · 높이 기준. Canvas 아래 전부 삭제, 카메라 단색 배경 `#2E2440` |
| 2 | `Scripts/Editor/LobbySceneBuilder.cs` 로 Canvas 아래 재생성: `LobbyPanel` › `NicknameText`(48, 좌상단 48/48, 폭 800 말줄임) · `MainButtons`(1560×200, 간격 60) › `RandomMatchButton` · `CreateRoomButton` · `JoinRoomButton`(480×200, 글자 56). 기본 UISprite 회색 + 학교안심 R 폰트 |

### 캡처 결과 (`LobbySceneBuilder.BuildAndCapture`)

| 해상도 | 결과 |
| --- | --- |
| 1920×1080 · 1280×720 | 문서 7-1 그대로. 버튼 줄 정중앙, 닉네임 좌상단 |
| 2340×1080 · 2400×1080 (폰) | 좌우 여백만 늘어남. 잘림 없음 |
| 2048×1536 (iPad 4:3) | **양 끝 버튼 60씩 잘림** — 높이 기준 스케일의 한계. `14-lobby-ui` 7-1 · 10절에 기록. Expand 전환은 전역 미정 #9 |

### 결정 (사용자, 2026-09-25)

1. 랜덤 매칭 MVP 유지 → `08-room.md` 4절 · 6절에 확정으로 추가
2. 기능 없는 버튼(설정 · 친구 · 프로필 편집 · 사운드/메뉴)은 두지 않음 → `14-lobby-ui` 3절 #1
3. 문서 · 구현 모두 플랜 세션이 직접 수행

### 역할 분리 (사용자 지시, 2026-09-25)

**이 작업은 뷰만 만든다.** `LobbyPanel` · `ProfilePresenter` · `ProfileView` 를 씬에 붙이고 참조를 연결하는 것은 **로직 담당(옆 세션)** 몫.
빌더에 연결 코드를 넣었다가 뺐다 — 뷰 산출물에 로직 연결이 섞이지 않도록.

### 로직 담당에게 넘기는 것 — 연결 방법

씬 `LobbyScene` 의 오브젝트 이름이 `LobbyPanel` 의 필드명과 같다. 코드 수정 없이 인스펙터 연결만 하면 된다.

| 붙일 오브젝트 | 컴포넌트 | 필드 | 연결 대상 |
| --- | --- | --- | --- |
| `Canvas/LobbyPanel` | `LobbyPanel` | `randomMatchButton` | `MainButtons/RandomMatchButton` |
| | | `createRoomButton` | `MainButtons/CreateRoomButton` |
| | | `joinRoomButton` | `MainButtons/JoinRoomButton` |
| `Canvas/LobbyPanel` | `ProfileView` | `_nicknameText` | `NicknameText` |
| `Canvas/LobbyPanel` | `ProfilePresenter` | `_view` | 같은 오브젝트의 `ProfileView` |

연결 뒤 확인: 타이틀 → 로비(닉네임 표시) → 방 만들기 → 방 → 나가기 → 로비 / 방 참가 → 팝업 → 없는 코드 → 경고.

### 주의

- 연결 전까지 로비 버튼은 동작하지 않는다 (뷰만 있음).
- `LobbySceneBuilder.cs` 는 씬 생성 후 **삭제했다** (커밋 `88e0822` 에 있음. 손으로 고친 씬을 덮어쓰지 않도록). 다시 필요하면 그 커밋에서 꺼낸다.
- batchmode 실행 시 `Assets/Settings/Lit2DSceneTemplate.scenetemplate` 가 같이 바뀐다(URP 템플릿 의존성 정리). 무관한 변경이라 되돌렸다.

### 다음 할 일

- [x] (로직) 위 표대로 컴포넌트 연결 + 왕복 확인 — `feature/lobby-logic` (PR5 와 함께). 필드명은 #19 이후 `ProfileView.nicknameText` · `ProfilePresenter.view`
- [ ] (뷰) Game 뷰 1920×1080 · 2400×1080 에서 캡처와 같은지 눈으로 확인 — 에디터 GUI 필요
- [ ] 화면 방향 · Screen Match Mode(Expand) 결정 (전역 미정 #9)
- [ ] 디자인 확정 후 색 · 스프라이트 교체 (크기 · 배치 유지)

---

## 2026-09-25 · 방 나가기 → 들어온 씬으로 복귀 (B 트랙 B5-2)

| 항목 | 값 |
| --- | --- |
| 브랜치 | `changhwan.exe` (직접 작업, worktree 없음) |
| 범위 | `.cs` 4개 + 문서 3개. 씬 · 프리팹 수정 없음 |

### 한 것

- `RoomPanel.OnLeftRoom` 이 `DebugLobbyScene` 으로 고정돼 있던 것을 **들어온 씬으로 복귀**하도록 변경.
  `LobbyScene` → 방 → 나가기 → `LobbyScene`, `DebugLobbyScene` → 방 → 나가기 → `DebugLobbyScene`.
- `Core/ScenePaths.cs` 에 `SceneFlow.ReturnSceneAfterRoom` (정적 문자열, 기본값 `ScenePaths.Lobby`) 추가.
  `LobbyPanel.OnJoinedRoom` · `DebugScript.OnJoinedRoom` 이 Room 로드 직전에 설정, `RoomPanel.OnLeftRoom` 이 읽는다.
- 문서: `01-game-flow.md` 4절 씬 표 아래 규칙 추가, `plan-b-ui.md` B5-2 ✅, `development-plan.md` 6절 #10 ✅.

### 결정

- 매니저 · 싱글톤 대신 정적 필드 하나. 씬을 넘어 살아남는 값이 이것 하나뿐이라 YAGNI.
- `RoomScene` 을 에디터에서 바로 Play 하고 나가면 기본값이라 `LobbyScene` 으로 간다 (의도한 동작).
- 방장 퇴장 시 방 처리(위임 / 폭파)는 `08-room.md` **미정** → 범위 밖.

### 다음 할 일

- [ ] PC 에디터 확인: Lobby 경로 · DebugLobby 경로 두 가지 모두 나가기 후 씬 확인 (컴파일 에러 미확인)
- [ ] `GameScene` 에서 나가는 경우는 별도 항목. 필요하면 같은 값을 재사용

---

## 2026-09-24 · 팝업 인프라 복구 · 정리 (B 트랙 B1-9, `ui-refactoring-plan` PR1)

| 항목 | 값 |
| --- | --- |
| 브랜치 | `feature/popup-infra` (← `changhwan.exe` `95a785e`) |
| 작업 위치 | worktree `../Project-Muffin-title` |
| 범위 | PR1(U-1 · U-2 · U-3 · U-11 · U-20) + 팝업 코드 한정 정리(U-15 · U-17 일부). 사용자 요청: "코드도 안 좋게 썼던 것 같다" → 버그 수정과 함께 구조 정리 |

### 한 것

| # | 내용 |
| --- | --- |
| U-1 | 씬 전환 시 모달이 컴포넌트만 지워져 껍데기가 남던 문제 → 모든 닫기가 GameObject 째 지운다 |
| U-2 | `OnOpen` / `OnClose` 가 한 번도 안 불리던 문제 → 매니저가 생성 직후 · 파괴 직전에 부른다 (토스트 자동 닫힘이 이제 동작) |
| U-3 | 토스트 불가 → `MessageToast` 신설, `ToastPopup.prefab` → `MessageToast.prefab` (GUID 유지) + 컴포넌트 · 문구 연결. 옛 프리팹 루트의 **끊긴 스크립트 참조 1개 제거**(저장이 거부되던 원인) |
| U-11 | `WarningPopup` 확인 → `OnConfirmed` 알림 후 스스로 닫힘 |
| U-20 | 프리팹 규약 위반 시 원인 로그 (`Resources/Popups/{타입}.prefab 이 없거나 루트에 컴포넌트가 없다`) |
| U-15 일부 | `UI/Popup/` · `PopupScripts/` · `Components/LoadingPopup` → `UI/Popups/` 평탄화, 네임스페이스 `Chapchu.UI.Popups` (`Popup.Popup` 표기 해소). `.meta` 동반 이동 |
| U-17 일부 | 팝업 · `LobbyPanel` 의 안 쓰는 `using` 제거 |
| U-10 일부 | `InputPopup` 닫기 버튼이 스스로 닫힘. 제출 후 닫기 · 로딩 전환은 PR5 |

### API 변경 (호출하는 쪽)

| 전 | 후 |
| --- | --- |
| `OpenModal(PopupManager.Get<T>())` | `OpenModal<T>()` (`OpenNonModal<T>` · `OpenToast<T>` 동일). `Get<T>` 는 내부로 |
| `CloseModal()` / `CloseModal(p)` / `CloseNonModal(p)` / `CloseToast(p)` | `PopupManager.Close(p)` 하나 또는 `popup.Close()` |
| — | `PopupManager.ShowToast("문구")` |
| `popup.Close()` = 훅만 호출(닫히지 않음) | `popup.Close()` = 실제로 닫음. 훅은 매니저 전용 |
| `WarningPopup.OnClickedOkButton = …` (대입 · 직접 닫기 필요) | `OnConfirmed += …` (닫기 자동) |
| `InputPopup.OnClickedSubmitButton` / `OnClickedExitButton` / `InputText` | `OnSubmitted += text => …` / 닫기 자동 |

`LobbyPanel` 은 위에 맞춰 두 곳만 바꿨다(나머지는 PR5).

### 확인 방법 (PC 에디터) — 자동 테스트는 asmdef(A1-2) 전까지 불가

| # | 방법 | 기대 |
| --- | --- | --- |
| T1 | Play 중 Hierarchy `PopupManager` 선택 → 컴포넌트 ⋮ → **Debug/테스트 토스트** | 3초 뒤 사라짐 · 누르면 즉시 사라짐 |
| T2 | 같은 메뉴 → **Debug/경고 모달을 띄운 채 현재 씬 다시 로드** | 새 씬에 모달 · 반투명 차단막 없음 |
| T3 | 로비 → 방 참가 → 없는 코드 → 경고 확인 | 경고만 닫히고 입력 팝업은 남음. 경고 로그 없음 |
| T4 | 로비 → 방 참가 입력 팝업 열린 채 방 입장 | 방 씬에 잔재 없음 |
| T5 | 로비 → 방 참가 → 닫기 버튼 | 입력 팝업이 닫힘 |

디버그 메뉴는 `#if UNITY_EDITOR` · `[ContextMenu]` — 빌드에 안 들어가고 씬을 건드리지 않는다.

**2026-09-25 사용자 테스트 결과 → 후속 PR**
- T1 토스트가 화면 밖 — 프리팹 루트 앵커가 `(0,0)` · 피벗 가운데라 왼쪽 아래 모서리를 중심으로 놓였다(옛 프리팹 값, 띄운 적이 없어 미발견). **화면 아래 가운데, 48px 위**로 고정. 위치는 스펙에 없어 `제안` → `10-ui.md` 8절에 확정 요청
- T2 아무것도 안 뜸 — 모달을 열고 같은 프레임에 씬을 로드해 그려지기 전에 닫혔다(수정 자체는 정상). 2초 보여준 뒤 로드하고, 로드 후 남은 팝업 오브젝트 수를 로그로 찍는다

### 하지 않은 것

- 팝업 디자인(옛 모양 그대로) — 디자인 기준 없음 → 플랜 세션
- 팝업 캔버스 스케일(`PopupManager` 는 Constant Pixel Size, 타이틀만 1920×1080) — B1-14(화면 방향 미정)
- `LoadingPopup` 연결 — PR3

### 플랜 세션에 넘김

- `systems/10-ui.md` 8절: 토스트 규칙(3초 자동 닫힘 · 누르면 닫힘 · 위치) 명문화, `ToastPopup` 행을 "구현됨(`MessageToast`)" 으로
- `ui-refactoring-plan.md`: U-15 · U-17 의 팝업 부분, U-10 의 닫기 버튼 부분 완료 표시. PR2 에 남은 것: `UI/NickName` → `UI/Title`, U-16, 나머지 `using`
- 팝업 · 토스트 디자인 아티팩트 (새 UI 구조로 통일할 때)

### 타이틀 후속

- 사용자 PC 테스트 통과(2026-09-24). **모바일 실기는 기기가 없어 당분간 PC 로 대신** — S6 의 Android 항목 보류
- `TitleSceneBuilder` 삭제 — 플랜 세션이 에디터에서 타이틀 묶음을 키운 뒤(`02b4964`)라 재실행하면 덮어쓴다

---

## 2026-09-24 · 타이틀 화면 UI (B 트랙 B1-10) — S1~S5 구현, S6 일부

| 항목 | 값 |
| --- | --- |
| 브랜치 | `feature/title-ui` (← `changhwan.exe` `33ca037`) |
| 작업 위치 | git worktree `../Project-Muffin-title` — 플랜 세션이 같은 디렉터리에서 `changhwan.exe` 에 커밋하므로 분리. Library 는 메인에서 복사 |
| 기준 | `docs/systems/12-title-ui.md`, `docs/title-ui-plan.md`, 아티팩트 `7hXwuwxuZfL7ZXW7VSyoRv` (Version 8) |
| 역할 | 사용자 결정(2026-09-24): **UI 관련 작업과 게임씬 밖 작업은 전부 B** |

### 진행 상태

| 단계 | 상태 | 비고 |
| --- | --- | --- |
| S1 에셋 | ✅ | 세로 오버레이 · 링 스프라이트 생성, 방사형 오버레이 삭제, 학교안심 B 프리셋 3개 (`Assets/Fonts/…B SDF - Logo/Subtitle/Error.mat`) |
| S2 프리팹 | ✅ | `Assets/Prefab/UI/PillButton` · `PillInputField` · `IconButton` |
| S3 씬 | ✅ | `TitleScene` 의 Canvas 를 지우고 `TitleCanvas` 로 재구성. 카메라 · EventSystem 유지 |
| S4 스크립트 | ✅ | `Scripts/UI/Title/` 5개. 금지 `using`(Photon · SceneManagement · NetworkManager · ScenePaths) 없음 |
| S5 버튼 연출 | ✅ | 캡처로 확인: 호버 위로 2 · `#BF9CE9` / 누름 아래로 4 · 그림자 7→3 |
| S6 QA | 🔶 PC 완료 | 아래 "S6 결과" + 사용자 PC 테스트 통과. **보류**: Android 실기(기기 없음) |

### S6 결과 (아티팩트 Edge 1920×1080 렌더 ↔ Unity 캡처)

| 항목 | 결과 |
| --- | --- |
| 입력창 · 버튼 · 부제 · 카운터 · 버튼 글자 · 버전 | 위치 ±1px |
| 로고 | 글자 면 ±1~2px, 외곽선 5px · 그림자 위치 일치 |
| 외곽선 · 그림자 색 | 일치 (`#3A2246`) |
| 1280×720 · 1600×900 · 21:9 · 2400×1080 | 겹침 · 잘림 없음. 에러 표시 시 버튼 위치 불변 |
| 배경 밝기 | Linear 에선 평균 9/255 밝았음 → **Gamma 전환 후 평균 0.8/255** (아래 "결정") |
| 로고 번짐 그림자 `0 14px 30px` | 없음 (스펙대로 생략) |

### 계획 · 스펙과 달라진 것 — 플랜 세션이 문서에 반영해 주세요

1. **박스 크기는 content-box 였다.** 아티팩트 CSS 에 `box-sizing: border-box` 가 없다. 스펙의 크기는 **안쪽** 크기이고, 겉 크기는 테두리 · 여백을 더한 값이다. 실측(Edge 렌더)으로 확인.
   | 요소 | 스펙(안쪽) | 실제 겉 크기 (구현값) |
   | --- | --- | --- |
   | 닉네임 입력창 | 560 × 66 | **612 × 74** (여백 22 + 테두리 4). 입력 묶음(560)보다 넓어 양옆으로 26씩 넘친다 |
   | 접속 버튼 | 300 × 74 | **310 × 84** (테두리 5) |
   | 시스템 버튼 | 46 × 46 | **50 × 50** (테두리 2) |
   → `12-title-ui.md` 7-1 · 9-2, `title-ui-plan.md` 2-4 수정 필요.
2. **글자 그림자는 외곽선을 뺀 글자 면 모양이다.** Chrome 은 `text-shadow` 를 `-webkit-text-stroke` 없이 그린다(단면 비교로 확인). 로고 (6, 6) 그림자는 외곽선 5 바깥으로 1px 만 보인다. → `12-title-ui.md` 8절에 메모.
3. **반투명 배경 박스는 테두리를 링으로 그린다.** 계획 2-6 의 "9-slice 2장 겹침"은 배경이 반투명하면 테두리색이 비친다(CSS 는 배경 위에 테두리). 입력창 · 시스템 버튼은 `ui_ring_r20_w4` · `ui_ring_r14_w2`(셋업 스크립트가 생성)를 쓴다. 접속 버튼은 불투명이라 계획대로 2장.
4. **버튼 그림자는 Body 의 자식.** CSS `box-shadow` 는 버튼과 함께 움직인다. 스펙 10-1 의 "그림자 아래 7 / 3"은 몸체 기준. → 계획 S2 의 PillButton 구성: `Body(SoftShadow · HardShadow · Border · Fill · Label)`.
5. **프리팹 위치** `Assets/Prefab/UI/` (계획의 `UI/Prefabs/` 폴더는 없음. 기존 `Assets/Prefab` 을 따름).
6. **TMP 효과 수치는 px 를 직접 환산한다.** 머티리얼에 `RATIOS_OFF` 를 켜고 `SDF 1단위 = GradientScale × 글자크기 / 샘플링크기` px 로 계산(`TitleUIAssetSetup.ApplyTextEffects`). 계획의 "S6 에서 눈으로 맞춤"은 불필요해졌다.
7. **TMP 외곽선 · 언더레이 색은 `[HDR]` 속성이라 Linear 색 공간에서 자동 변환되지 않는다.** 스크립트로 넣을 땐 `.linear` 로 바꿔야 디자인 색이 나온다. 인스펙터 색 선택기는 알아서 처리한다. 앞으로 TMP 머티리얼을 코드로 만들 때 공통 주의.
8. **줄 높이.** 로고 박스 높이 = 190 × 0.95, 부제 = 폰트 상승+하강(36 × 1.06). TMP 정렬을 **Middle**(`Center`/`Left`/`Right`)로 두면 기준선이 CSS 와 같아진다. `Midline` 은 글자 외곽 기준이라 쓰지 않는다. 에러 문구는 CSS 처럼 **Top**.
9. **닉네임 앞뒤 공백 제거**는 `TitleView` 가 검증 전에 한다(스펙 4-2). `NicknameValidator` 는 그대로.

### 삭제한 코드 (사용자 지시: 구조에 안 맞는 기존 코드는 과감히 삭제)

| 파일 | 이유 |
| --- | --- |
| `UI/NickName/NicknameInput.cs` | 계획대로. 네트워크 · 씬 로드 섞임 → `TitleView` 로 교체 |
| `UI/NickName/NicknameInputLogic.cs` | 참조 0 |
| `UI/Interfaces/ISubmitLogic.cs` · `IButtonLogic.cs` (폴더째) | 참조 0 (`ISubmitLogic` 은 아래 죽은 코드만 구현) |
| `UI/JoinRoomSubmitLogic.cs` · `UI/JoinRoomPanel.cs` | 씬 · 프리팹 · 코드 참조 0 |
| `UI/Popup/ModalPopup.cs` | 참조 0 |
| `Sprites/UI/title_overlay_radial.png` | 오버레이가 세로 선형으로 바뀜 |

**남긴 것** — 씬에서 쓰는 로비 · 룸 · 팝업 코드(`LobbyPanel` · `RoomPanel` · `RoomInfoPanel` · `Popup*` · `ProfilePresenter/View` · `LoadingPopup`)는 지우면 흐름이 끊긴다. 각 화면을 새 구조로 다시 만들 때 교체한다.

### 버전

`docs/versioning.md` 신설. Player Settings Version `1.0` → **`0.1.0`** (M0). MINOR = 마일스톤, 테스트 빌드 재배포 = PATCH, Android 빌드 번호는 빌드마다 +1.

### 결정

1. **색 공간 Linear → Gamma (사용자 결정 2026-09-24).** Linear 에선 Unity 가 반투명 UI 를 선형 공간에서 섞어(브라우저는 sRGB) 오버레이 · 그림자 · 반투명 박스가 평균 9/255 밝았다. 전환 후 평균 0.8/255.
   - **프로젝트 전체 설정**이다. 인게임 스프라이트 · 이펙트의 반투명 섞임도 조금 어두워진다 → **B(인게임 UI) · 아트에 공유 필요.** `CLAUDE.md` 10절에 기록.
   - TMP 색 변환(`ToShaderColor`)은 색 공간을 보고 분기하므로 그대로 둔다. 프리셋은 Gamma 값으로 다시 생성했다.

### 플랜 세션에 넘김

1. **16:9 가 아닌 해상도의 크기 규칙.** 아티팩트는 `clamp(… vh/vw …)` 반응형이라 1280×720 에서 로고가 158px(화면 높이의 22%). 계획은 1080 기준 균일 스케일이라 127px. 지금은 계획대로 구현. 플랜 세션이 규칙을 정하면 반영한다.
2. **안내문 색** `#A08FB5` 는 스펙상 `제안`(아티팩트는 브라우저 기본 회색). 확정 필요.
3. **세이프에어리어.** 노치 대응 스크립트가 계획에 없다. Android 실기 확인 뒤 필요하면 추가.

### 브랜치 확인 방법 (사용자 안내)

`feature/title-ui` 는 worktree `../Project-Muffin-title` 에 체크아웃돼 있다. git 은 같은 브랜치를 두 곳에서 체크아웃할 수 없어 **메인 디렉터리에서는 이 브랜치로 바꿀 수 없다**(GitHub Desktop 에서 골라도 그대로). 결과는 Unity Hub 에서 `Project-Muffin-title/MuffinProject` 를 열어 본다.

### 도구 — S6 재확인 절차

1. 아티팩트 기준 이미지: `Artifact read` 로 HTML 저장 → Edge headless
   `msedge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --window-size=1920,1080 --virtual-time-budget=15000 --screenshot=ref.png file:///<저장한 html>`
2. Unity 캡처 (에디터가 **열려 있지 않은** 프로젝트 경로에서만):
   `unity run <MuffinProject> -- -executeMethod Chapchu.EditorTools.TitleSceneBuilder.CaptureOnly -captureDir <폴더>`
   → 1920×1080(기본 · 회색 배경 · 에러 · 호버 · 누름), 1600×900, 1280×720, 21:9, 2400×1080 PNG.
   `BuildAndCapture` 는 에셋 셋업 → 프리팹 · 씬 **재생성** → 캡처. 씬을 손으로 고친 뒤에는 쓰지 않는다.
3. 주의: batchmode 는 화면을 640×480 으로 보고 TMP 셰이더 픽셀 크기를 잘못 계산한다. 캡처 코드가 머티리얼 인스턴스 `_ScaleX/Y` 로 보정한다(실제 게임과 무관).
4. `TitleSceneBuilder.cs` 는 2026-09-24 삭제됨 (위 팝업 항목 참고). 다시 필요하면 `0fc3e71` 에서 꺼낸다.

### 다음 할 일

- [ ] 플랜 세션: 위 "달라진 것" 1 · 2 · 4 · 5 를 `12-title-ui.md` · `title-ui-plan.md` 에 반영, S1~S5 체크
- [ ] 플랜 세션: "플랜 세션에 넘김" 1~3
- [ ] B · 아트에 Gamma 전환 공유
- [ ] S6 남은 것: Android 실기(세이프에어리어), 실제 입력(IME 조합 · 붙여넣기 16자)
- [ ] B PR4(접속 실패 안내 · 재시도, 닉네임 복원)는 `TitleView.ConnectRequested` · `ShowError` · `SetConnecting` · `SetNickname` 에 붙인다. 지금은 구독자가 없어 "접속 중…"에서 멈추는 게 정상
- [x] `TitleSceneBuilder.cs` 삭제
