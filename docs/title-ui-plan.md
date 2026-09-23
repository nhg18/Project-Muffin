# 타이틀 화면 UI 구현 플랜

**작성일**: 2026-09-23
**대상 씬**: `Assets/Scenes/TitleScene.unity` **단독** (로비·룸·인게임은 이 문서의 범위가 아니다)
**기준 문서**: 노션 「UI 개발 문서 / 🎬 타이틀 화면 개발 문서(UI 구현 스펙)」 (최종 수정 2026-09-22)
**관련**: [`development-plan.md`](development-plan.md) 6절 · [`systems/01-game-flow.md`](systems/01-game-flow.md) · [`CLAUDE.md`](../CLAUDE.md) 0·12절

---

## 0. 결론 먼저

노션 스펙은 **비주얼·레이아웃 스펙으로만 쓴다.**
스펙에 있던 프레젠터 예시 코드(구 9절)는 이 프로젝트 구조와 맞지 않아 **2026-09-23에 노션 페이지에서 삭제했다.** 그 코드는 참고하지 않는다. 사유와 올바른 대응은 3절에 기록해 둔다(다시 끌어오지 않기 위한 기록).

타이틀의 역할은 **연결을 시작하는 것이 아니라, 연결 완료를 기다리며 닉네임을 확정하고 로비로 넘기는 것**이다. Photon 연결은 이미 `NetworkManager`가 `Start()`에서 자동으로 시작한다.

작업 순서는 6단계다.

| 단계 | 내용 | 산출물 | 예상 |
| --- | --- | --- | --- |
| **S0** | 노션 스펙을 `docs/systems/12-title-ui.md`로 이관 | 문서 1개 | 0.5d |
| **S1** | 플레이스홀더 에셋 + 임포트 설정 | 스프라이트 3종·폰트 정리 | 0.5d |
| **S2** | Canvas·하이어라키 재구성 | `TitleScene.unity` | 1d |
| **S3** | `TitlePresenter` 작성 / `NicknameInput` 대체 | `.cs` 2~3개 | 1d |
| **S4** | 인터랙션·상태(버튼/포커스/에러/카운터) | 애니·트윈 | 0.5d |
| **S5** | 접속 실패·타임아웃 처리 + QA | | 0.5d |

**총 4일 규모. S0 → S1 → S2 → S3 순서 의존이고, S4·S5는 S3 뒤에 붙는다.**

---

## 1. 이번에 확정한 것 (2026-09-23)

| 항목 | 결정 | 영향 |
| --- | --- | --- |
| 기준 해상도 | **TitleScene만 1920×1080 / Scale With Screen Size** 적용. 화면 방향 고정(가로/세로)과 나머지 4개 씬·`ProjectSettings`는 **보류** | `development-plan.md` 4절 3번은 여전히 미확정 |
| 에셋 | **플레이스홀더로 먼저 배선.** 구조·수치는 노션 스펙대로, 그림만 나중에 교체 | S1 |
| 닉네임 규칙 | **기존 `NicknameValidator` 유지** (2~16자, 한글·영문·숫자, 공백 불가, 특수문자 전부 차단) | 노션 8절은 이 결정대로 갱신 완료. S0에서 그대로 옮긴다 |
| 에러 표시 | **노션대로 인라인 `ErrorText`.** `WarningPopup` 모달 호출은 제거 | S3·S4 |

> 화면 방향을 보류했으므로 `Screen Match Mode`는 **Match Width Or Height / Match 1(Height)** 로 두되, 값은 씬의 Canvas Scaler에만 둔다. 방향이 확정되면 5개 씬을 한 PR로 일괄 변경한다.

---

## 2. 현재 상태 ↔ 노션 스펙 차이

`TitleScene`에 실제로 붙어 있는 프로젝트 스크립트는 `NicknameInput` **하나뿐**이다. (`NicknameInputLogic`은 어느 씬에서도 참조되지 않는 죽은 코드)

### 2-1. 하이어라키

| 노션 스펙 | 현재 씬 | 조치 |
| --- | --- | --- |
| `BG_Illust` | `BackGround` (`TitleBackground.png`) | 이름·앵커 정리 |
| `BG_Vignette` | **없음** | 신규 (플레이스홀더) |
| `TitleGroup` (Vertical Layout Group) | **없음** — 개별 절대 배치 | **신규. 이번 작업의 핵심** |
| `Logo_Chapchu` | `TitleText` | 이동·서식 적용 |
| `Subtitle` | `MainText` / `SubText` (2개가 혼재) | 1개로 정리 |
| `NicknameField` + `Placeholder` + `Text` + `CountText` | `NicknameInputPanel` 하위에 존재 | 재배치 + 9-slice |
| `ConnectButton` + `Shadow` + `Label` | `Submit Button` (Shadow 없음) | Shadow 자식 추가 |
| `ErrorText` | **없음** | 신규 |
| `Ver_Text` | **없음** | 신규 |
| `SystemButtons` (`Btn_Sound`, `Btn_Settings`) | **없음** | 신규 (기능은 스텁) |

### 2-2. 캔버스

| 항목 | 현재 | 목표 |
| --- | --- | --- |
| UI Scale Mode | `Constant Pixel Size` | `Scale With Screen Size` |
| Reference Resolution | 800 × 600 | 1920 × 1080 |
| Screen Match Mode | Match Width Or Height | 동일 |
| Match | 0 (Width) | **1 (Height)** |
| Render Mode | Screen Space - Overlay | 동일 (유지) |

### 2-3. 에셋

| 노션 요구 | 현재 | S1 대체 |
| --- | --- | --- |
| `Fonts/Jua-Regular.ttf` + SDF | 없음 (`NeoDunggeunmoPro`, `Hakgyoansim Dunggeunmiso` SDF 존재) | 기존 SDF로 배선, TMP 참조만 나중에 교체 |
| `ui_pill_20.png` (80×80, border 24) | 없음 | `Sprites/Input Bar.png` 재사용 + 9-slice border 지정 |
| `ui_pill_22.png` (88×88, border 26) | 없음 | `Sprites/Buttons/Button1.png` 재사용 |
| `ui_square_14.png` (56×56, border 18) | 없음 | `Sprites/Icons/Icon1.png` 또는 흰 사각 |
| `bg_titleart.png` (1920×1080) | `Sprites/TitleBackground.png` | 그대로 사용 (비율 확인 필요) |
| `bg_vignette.png` (1024×1024) | 없음 | 중앙 투명 → 외곽 `#2E2440` 임시 PNG |
| `Audio/SFX/ui_click.wav`, `ui_error.wav`, `BGM/title_loop.ogg` | 없음 | **이번 범위 제외** (배선 지점만 주석으로 남긴다) |

---

## 3. 프레젠터 구현 기준 — 삭제된 예시 코드를 되살리지 않는다

> 노션의 예시 프레젠터 코드는 **2026-09-23에 페이지에서 삭제됐다.** 아래는 "왜 지웠는가 / 대신 무엇을 쓰는가"의 기록이며, 구현 시 오른쪽 열만 따른다.

| 삭제된 예시 코드가 하던 것 | 문제 | 이 프로젝트에서의 처리 |
| --- | --- | --- |
| `PhotonNetwork.ConnectUsingSettings()` | `NetworkManager.Start()`가 이미 연결을 시작한다. 중복 호출 | 호출하지 않는다. `NetworkManager.IsReady` / `ConnectionEvents.OnConnected`로 **대기**만 한다 |
| `MonoBehaviourPunCallbacks` 상속 | 이 프로젝트는 PUN 콜백을 `NetworkManager` 한 곳에서 받아 `ConnectionEvents`로 재발행한다 | `MonoBehaviour` + 이벤트 구독 |
| `SceneLoader.Load("MainMenu")` | 존재하지 않는 클래스·씬 | `ScenePaths.Get(SceneType.Lobby)` |
| `PlayerPrefs.SetString("nickname", ...)` | 키 문자열 리터럴 | `PlayerPrefsKeys.PlayerName` (`NetworkManager.SetNickname`이 이미 저장한다) |
| `PhotonNetwork.NickName` 직접 대입 | 저장 로직 중복 | `NetworkManager.Instance.SetNickname(nick)` |
| 검증이 `IsNullOrEmpty`만 | 규칙이 `NicknameValidator`와 다름 | `NicknameValidator.Validate()` 사용 |
| (현재 코드) `async void LoadScene()` | `CLAUDE.md` 12절 위반 | **코루틴**으로 교체, `OnDisable`에서 중단 |

---

## 4. 단계별 작업

### S0. 문서 이관 — `docs/systems/12-title-ui.md` 신설

`CLAUDE.md` 0절 규칙 2(노션 변경은 `docs/systems/`에 반영한 뒤 구현)와 `GDD_GUIDE.md` 20절 규칙 1에 따라, **노션 내용을 저장소로 내리기 전에는 S1 이후를 시작하지 않는다.** `docs/systems/README.md`는 이미 12번을 타이틀 UI 이관용으로 예약해 두었다.

#### S0-1. 산출물

| 파일 | 작업 |
| --- | --- |
| `docs/systems/12-title-ui.md` | 신규 작성 (아래 골격) |
| `docs/systems/README.md` | 예약 문구를 실제 행으로 교체 |
| `docs/systems/01-game-flow.md` | 53행 `TitleScene` 행에 `12-title-ui.md` 링크 추가 |
| `docs/systems/10-ui.md` | 1절에 "타이틀 화면은 12번" 한 줄 교차 참조 |

#### S0-2. `12-title-ui.md` 골격

`GDD_GUIDE.md` 17절(문서 상단 배치 순서)을 따른다.

| 절 | 내용 | 출처 |
| --- | --- | --- |
| 머리말 | `최종 수정일` / `분류: MVP` | 규칙(GDD 20-2) |
| 1. 목적 | 닉네임을 확정하고 서버 연결을 기다렸다가 로비로 보낸다 | — |
| 2. 판정 주체 | **타이틀의 모든 판정은 로컬이다.** 마스터 판정 없음. 닉네임은 검증 후 Photon에 전달될 뿐, 서버가 검사하지 않는다 | 규칙(GDD 20-3) |
| 3. 핵심 규칙 | 닉네임 없이는 진입 불가 / 연결 완료 전에는 접속 버튼 비활성 / 실패는 인라인으로만 표시 | 결정(1절) |
| 4. 주요 수치 | 기준 해상도·Canvas Scaler / 레이아웃 px / 닉네임 길이 / 타임아웃 | 노션 1·6절 |
| 5. 입력 | 닉네임 입력, 접속 버튼, 사운드·설정 버튼 → 동작·조건 표 | 노션 2절 + GDD 11절 |
| 6. 상태 흐름 | `Connecting → Ready → Entering` 전이 표 (조건·결과·화면 변화) | 신규 (GDD 6절 형식) |
| 7. 화면 구성 | 하이어라키 + UI 표(표시 조건 / 표시 내용 / 동작) | 노션 2절 + GDD 10절 |
| 8. 닉네임 검증 규칙 | 2~16자, 한글·영문·숫자, 공백 불가, 특수문자 차단, PlayerPrefs 저장·복원 | 노션 8절(확정 반영본) |
| 9. 비주얼 스펙 | 컬러 8종 · 타이포 8종 · 9-slice 3종 | 노션 3·4·5절 |
| 10. 인터랙션 · 연출 | 버튼 4상태, 입력창 포커스, 에러 페이드, 카운터 한계색 | 노션 7절 |
| 11. 예외 | 인터넷 없음 / 연결 실패 / 타임아웃 / 연타 / 씬 로드 실패 | 신규 (GDD 4-5) |
| 12. 현재 구현 상태 | 이 플랜 2절의 갭 표를 요약 | 이 문서 |
| 13. 미정 · 결정 필요 | 5절 표 | GDD 7절 |
| 14. 변경 이력 | `2026-09-23 노션에서 이관` | GDD 15절 |

#### S0-3. 옮기지 않는 것 (중요)

| 노션 | 처리 | 사유 |
| --- | --- | --- |
| 구 9절 프레젠터 코드 | **이미 노션에서 삭제됨. 어디에도 옮기지 않는다** | `GDD_GUIDE.md` 8절 — 기획서가 클래스 구조를 강제하지 않는다. 내용도 틀렸다(3절) |
| 에셋 체크리스트 (현 9절) | `title-ui-plan.md` S1에만 둔다 | 기획이 아니라 작업 목록 |
| QA 체크 (현 10절) | `title-ui-plan.md` S5에만 둔다 | 기획이 아니라 검수 목록 |

> `12-title-ui.md`에는 **클래스 이름·메서드 이름을 쓰지 않는다.** 단, `GDD_GUIDE.md` 8절 단서에 해당하는 요구사항("닉네임은 저장되어야 함", "연결 완료 전에는 입력을 받지 않음")은 문장으로 남긴다.

#### S0-4. 표기 정리 (`GDD_GUIDE.md` 7절)

| 항목 | 구분 | 비고 |
| --- | --- | --- |
| 기준 해상도 1920×1080 / Match 1 | **확정** | 타이틀 한정 |
| 화면 방향 고정(가로/세로) | **미정** | 나머지 씬 Canvas를 막고 있음 |
| 닉네임 2~16자 · 한·영·숫자 · 공백 불가 · 특수문자 차단 | **확정** | 노션 8절 갱신 완료 |
| 레이아웃 px (560×66, 300×74 등) | **확정** | 노션 6절 |
| 컬러 8종 / 타이포 8종 | **확정** | 노션 3·4절 |
| 연결 타임아웃 10초 | **제안** | 승인 필요 |
| 사운드·설정 버튼의 동작 | **미정** | 배치만 확정 |
| 중복 닉네임 허용 | **미정** | 로그인 시스템(기획서 14-5) 보류 |
| 배경 키 아트 | **미정** | PNG 미수령 |

#### S0-5. 검수 (이걸 만족해야 S1 시작)

- [ ] `12-title-ui.md`만 읽고 타이틀 화면을 구현할 수 있는가 (`GDD_GUIDE.md` 19절)
- [ ] 모든 수치에 `확정 / 제안 / 미정` 구분이 붙어 있는가
- [ ] `미정` 항목이 문서 하단 목록과 `README.md` 전역 미정 목록에 모두 있는가
- [ ] 클래스·메서드 이름이 본문에 없는가
- [ ] 노션 8절과 `12-title-ui.md` 8절이 서로 다르지 않은가
- [ ] 금지 표현(`GDD_GUIDE.md` 14절)이 없는가

**예상 0.5d. 커밋 1개** — `docs: 타이틀 화면 UI 스펙을 systems/12-title-ui.md 로 이관`

### S1. 에셋 준비 (플레이스홀더)

- [ ] `Sprites/UI/` 폴더 생성
- [ ] 9-slice 3종을 2-3표대로 배선. 각 `.meta`에 **Border 지정**, Texture Type `Sprite (2D and UI)`, Mesh Type `Full Rect`, **Compression None**
- [ ] `TitleBackground.png`: Max Size 2048, Compression Normal, 16:9 여부 확인
- [ ] `bg_vignette` 임시 PNG (1024×1024)
- [ ] 폰트: 기존 SDF 중 하나를 타이틀 전용으로 지정. **Jua 교체를 한 번에 하도록 TMP 참조 지점을 일관되게 둔다**
- [ ] 컬러 8종(노션 3절): **`Scripts/UI/UIPalette.cs` 정적 클래스 1개**를 만들고, 코드가 색을 바꾸는 곳(카운터 한계색, 에러색)만 참조. 나머지는 인스펙터 직접 입력

### S2. 씬 재구성

- [ ] Canvas Scaler를 2-2표대로 변경
- [ ] `TitleGroup` 생성: Vertical Layout Group (Middle Center, Spacing 40, Padding T90 / B110), Child Force Expand 끔
- [ ] 노션 2절 하이어라키대로 오브젝트 정리. `MainText`/`SubText` 중복 해소
- [ ] 노션 6절 레이아웃 수치 적용 (NicknameField 560×66, ConnectButton 300×74, CountText Right 22, ErrorText Min Height 26)
- [ ] `ErrorText`는 **항상 활성 상태**로 두고 문자열만 비운다 (`SetActive(false)` 금지 — 레이아웃 점프)
- [ ] `Ver_Text` (Bottom-Left 26,22), `SystemButtons` (Bottom-Right 26,22, Spacing 10) 추가
- [ ] `ConnectButton` 하위에 `Shadow`(Y −7) → `Label` 순서로 배치
- [ ] 노션 4절 타이포 설정 적용 (로고 Outline 0.2 / Underlay X6 Y−6 등)

> 씬 파일은 병합이 어렵다(`CLAUDE.md` 14절). S2 진행 중에는 다른 사람이 `TitleScene`을 열지 않는다.

### S3. 프레젠터

**`NicknameInput`을 `TitlePresenter`로 대체한다.** (씬에 붙은 컴포넌트 교체이므로 S2와 같은 PR로 묶는다)

```
Scripts/UI/Title/
├ TitlePresenter.cs      뷰 배선 + 상태 전이 (신규)
├ NicknameField.cs       입력창 1개의 표시 책임: 카운터·포커스 아웃라인·한계색 (신규)
└ (재사용) Scripts/UI/NickName/NicknameValidator.cs   규칙
```

`TitlePresenter` 책임:

| 항목 | 처리 |
| --- | --- |
| 상태 | `Connecting`(연결 대기) / `Ready`(입력 가능) / `Entering`(씬 전환 중) 3개. 버튼 `interactable`은 `Ready`에서만 true |
| 구독 | `ConnectionEvents.OnConnected` → `Ready`, `ConnectionEvents.OnDisconnected` → 에러 + `Connecting` |
| 진입 시 | 정적 프로퍼티 `NetworkManager.IsReady`로 초기 상태 결정 (`OnEnable`에서 `Instance` 접근 금지 — `CLAUDE.md` 12절) |
| 닉네임 복원 | `Start()`에서 `PlayerPrefs.GetString(PlayerPrefsKeys.PlayerName)`로 채우고 카운터 갱신. 죽은 코드 `PhotonConnection.SetupInitNickname()`은 삭제 |
| 접속 클릭 | `NicknameValidator.Validate()` → 실패 시 `ErrorText` / 성공 시 `NetworkManager.Instance.SetNickname()` → 씬 전환 |
| 씬 전환 | **코루틴** `LoadSceneRoutine()` (`async void` 금지). `allowSceneActivation` 패턴 유지, `OnDisable`에서 `StopCoroutine` |
| 삭제 | `WarningPopup` 호출 제거, `NicknameInputLogic.cs` / `ISubmitLogic.cs` 삭제 (참조 0) |

### S4. 인터랙션 · 상태 (노션 7절)

- [ ] 버튼 Normal/Hover/Pressed — **Transition: Animation**. 클립 3종 또는 DOTween으로 본체·섀도 Y 이동
- [ ] Disabled: 알파 60% (`CanvasGroup`)
- [ ] 입력창 Focus: 테두리 오브젝트 On/Off (`onSelect` / `onDeselect`)
- [ ] 에러 문구: `CanvasGroup` 알파 0→1, 0.15s (DOTween). **자리 고정**
- [ ] 카운터: 16자 도달 시 `#FF6E8A`, 미만은 `#A08FB5`
- [ ] SFX 호출 지점만 주석 표시 (오디오 시스템 미구축)

### S5. 접속 실패 처리 + QA

`development-plan.md` 6절 1·2번(접속 실패 시 안내·복구 없음, 타임아웃 없음)이 이 화면에서 드러난다. 타이틀 범위에서만 닫는다.

- [ ] `PhotonConnection.Initialize()`가 인터넷 없을 때 조기 반환하며 `AutomaticallySyncScene = true`를 건너뛰는 문제 수정 (**대입을 조기 반환보다 앞으로**)
- [ ] 인터넷 없음: `ErrorText`에 "인터넷 연결을 확인해주세요." + 재시도 동작
- [ ] 연결 타임아웃 N초(**미정 — 10초 제안**) 후 실패 처리
- [ ] 연결 중 버튼 연타는 상태 3종으로 이미 차단 — 테스트로 확인

QA (노션 11절 + 추가):

- [ ] 1920×1080 / 1600×900 / 1280×720 — 로고·입력창 겹침 없음
- [ ] 16:10, 21:9 — 배경 잘림 범위
- [ ] 에러 표시/해제 시 버튼이 움직이지 않음
- [ ] 16자 초과 차단 (붙여넣기 포함)
- [ ] IME 한글 조합 중 카운터가 튀지 않음
- [ ] 연결 시도 중 연타로 중복 진입 없음
- [ ] 실패 시 버튼 재활성 + 에러 문구
- [ ] 이전 실행 닉네임 복원 + 카운터 반영
- [ ] **(추가)** 로비 진입 후 되돌아왔을 때 상태 꼬임 없음
- [ ] **(추가)** Android 실기 1회 — 세이프에어리어 밖으로 `Ver_Text`·`SystemButtons`가 나가지 않는지

---

## 5. 미정 — 구현 전에 답이 필요한 것

| # | 항목 | 막는 작업 | 현재 처리 |
| --- | --- | --- | --- |
| 1 | 화면 방향 고정(가로/세로) | 나머지 4개 씬의 Canvas | 보류. 타이틀만 1920×1080 |
| 2 | 배경 키 아트 PNG 수령 | 최종 비주얼 | `TitleBackground.png`로 대체 |
| ~~3~~ | ~~특수문자 차단 범위~~ | — | **해소(2026-09-23)** — 전부 차단으로 확정, 노션 8절 반영 완료 |
| 4 | 중복 닉네임 허용 (기획서 14-5) | 로그인 시스템 | 미구현 |
| 5 | 사운드·설정 버튼의 실제 기능 | `SystemButtons` | 버튼만 배치, `onClick` 비움 |
| 6 | 연결 타임아웃 시간 | S5 | 10초 제안 |

---

## 6. 커밋 분할

1. `docs: 타이틀 화면 UI 스펙을 systems/12-title-ui.md 로 이관` (S0)
2. `chore: 타이틀 UI 플레이스홀더 스프라이트 및 임포트 설정` (S1)
3. `feat(title): 캔버스 1920x1080 전환 및 하이어라키 재구성` (S2) — 씬 단독 소유 확인 후
4. `refactor(title): NicknameInput 을 TitlePresenter 로 대체, 인라인 에러 표시` (S3)
5. `feat(title): 버튼/입력창 상태 연출` (S4)
6. `fix(network): 인터넷 미연결 시 AutomaticallySyncScene 누락 수정 + 접속 실패 안내` (S5)
