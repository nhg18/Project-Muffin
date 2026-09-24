# 작업 기록 (구현 세션)

> 구현 세션이 **완료한 것 · 계획과 달라진 것 · 다음 할 일**을 남긴다. 플랜 세션과 다음 구현 세션은 여기서 이어간다.
> 계획 자체는 `title-ui-plan.md` 등 플랜 문서가 기준이다. 이 문서는 진행 상황과 결정 기록만 가진다.
> 최신 항목이 위.

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
| S6 QA | 🔶 일부 | 아래 "S6 결과". **남음**: Android 실기(세이프에어리어), 실제 플레이 중 IME · 붙여넣기 입력 |

### S6 결과 (아티팩트 Edge 1920×1080 렌더 ↔ Unity 캡처)

| 항목 | 결과 |
| --- | --- |
| 입력창 · 버튼 · 부제 · 카운터 · 버튼 글자 · 버전 | 위치 ±1px |
| 로고 | 글자 면 ±1~2px, 외곽선 5px · 그림자 위치 일치 |
| 외곽선 · 그림자 색 | 일치 (`#3A2246`) |
| 1280×720 · 1600×900 · 21:9 · 2400×1080 | 겹침 · 잘림 없음. 에러 표시 시 버튼 위치 불변 |
| 배경 밝기 | **Unity 가 평균 9/255, 최대 24/255 밝다** — 아래 "결정 필요 1" |
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

### 결정 필요

1. **색 공간 (Linear vs Gamma).** 프로젝트는 Linear 다. Unity 는 반투명 UI 를 선형 공간에서 섞고 브라우저는 sRGB 로 섞어서, 반투명 레이어(배경 오버레이 · 그림자 · 반투명 박스)가 아티팩트보다 밝게 나온다(평균 9/255).
   - Gamma 로 바꾸면 UI 가 디자인과 정확히 같아진다. 대신 **프로젝트 전체 설정**이라 인게임 스프라이트 · 이펙트 · 조명도 영향을 받는다.
   - Linear 유지 시: 오버레이 알파를 눈으로 올려 맞추는 근사만 가능(배경마다 오차가 다름).
2. **16:9 가 아닌 해상도의 크기 규칙.** 아티팩트는 `clamp(… vh/vw …)` 반응형이라 1280×720 에서 로고가 158px(화면 높이의 22%). 계획은 1080 기준 균일 스케일이라 127px. 지금은 계획대로.
3. **안내문 색** `#A08FB5` 는 스펙상 `제안`(아티팩트는 브라우저 기본 회색). 확정 필요.
4. **세이프에어리어.** 노치 대응 스크립트가 계획에 없다. Android 실기 확인 뒤 필요하면 추가.

### 도구 — S6 재확인 절차

1. 아티팩트 기준 이미지: `Artifact read` 로 HTML 저장 → Edge headless
   `msedge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --window-size=1920,1080 --virtual-time-budget=15000 --screenshot=ref.png file:///<저장한 html>`
2. Unity 캡처 (에디터가 **열려 있지 않은** 프로젝트 경로에서만):
   `unity run <MuffinProject> -- -executeMethod Chapchu.EditorTools.TitleSceneBuilder.CaptureOnly -captureDir <폴더>`
   → 1920×1080(기본 · 회색 배경 · 에러 · 호버 · 누름), 1600×900, 1280×720, 21:9, 2400×1080 PNG.
   `BuildAndCapture` 는 에셋 셋업 → 프리팹 · 씬 **재생성** → 캡처. 씬을 손으로 고친 뒤에는 쓰지 않는다.
3. 주의: batchmode 는 화면을 640×480 으로 보고 TMP 셰이더 픽셀 크기를 잘못 계산한다. 캡처 코드가 머티리얼 인스턴스 `_ScaleX/Y` 로 보정한다(실제 게임과 무관).
4. `TitleSceneBuilder.cs` 는 개발용. **S6 끝나면 삭제.**

### 다음 할 일

- [ ] 플랜 세션: 위 "달라진 것" 1 · 2 · 4 · 5 를 `12-title-ui.md` · `title-ui-plan.md` 에 반영, S1~S5 체크
- [ ] 사용자: 결정 필요 1~3
- [ ] S6 남은 것: Android 실기(세이프에어리어), 실제 입력(IME 조합 · 붙여넣기 16자)
- [ ] B PR4(접속 실패 안내 · 재시도, 닉네임 복원)는 `TitleView.ConnectRequested` · `ShowError` · `SetConnecting` · `SetNickname` 에 붙인다. 지금은 구독자가 없어 "접속 중…"에서 멈추는 게 정상
- [ ] S6 종료 후 `TitleSceneBuilder.cs` 삭제
