# 작업 기록 (구현 세션)

> 구현 세션이 **완료한 것 · 계획과 달라진 것 · 다음 할 일**을 남긴다. 플랜 세션과 다음 구현 세션은 여기서 이어간다.
> 계획 자체는 `title-ui-plan.md` 등 플랜 문서가 기준이다. 이 문서는 진행 상황과 결정 기록만 가진다.
> 최신 항목이 위.

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
