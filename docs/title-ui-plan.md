# 타이틀 화면 UI 구현 플랜 (프론트엔드 전용)

**작성일**: 2026-09-23
**대상 씬**: `Assets/Scenes/TitleScene.unity` **단독**
**범위**: **화면(뷰)만.** 네트워크·씬 전환 등 실제 기능은 연결하지 않는다.
**기준 문서**: [`systems/12-title-ui.md`](systems/12-title-ui.md) — 원본은 Claude 디자인 아티팩트 `https://claude.ai/artifact/7hXwuwxuZfL7ZXW7VSyoRv` (버전 `1790067707-dea6`)

> **기준 흐름 (2026-09-23)**: 아티팩트 → `docs/systems/12-title-ui.md` → 노션 「타이틀 화면 개발 문서」 순으로 반영했다.
> 셋이 다르면 **`12-title-ui.md`가 옳다** (`CLAUDE.md` 0절). 아래 2절 환산표는 구현용 상세이며, 수치는 12번 문서와 같다.
>
> 같은 계정의 다른 아티팩트: 로비 `87PcE8t7ic5Ei5VveoZXYf`, 룸 `XYKxMfH3pVh5KNS8hQJscE` — 이번 범위 아님.

---

## 0. 결론 먼저

**타이틀 씬과 타이틀 UI 코드를 전부 새로 만들어, 아티팩트 화면을 1920×1080에서 픽셀 단위로 재현한다. 기능은 붙이지 않는다.**

| 단계 | 내용 | 산출물 | 예상 |
| --- | --- | --- | --- |
| **S1** | 폰트 · 스프라이트 준비 | Jua / IBM Plex Mono SDF, 둥근 사각 9-slice, 그림자 스프라이트 | 1d |
| **S2** | 공용 UI 프리팹 3종 | `PillButton` · `PillInputField` · `IconButton` | 0.5d |
| **S3** | `TitleScene` 재구성 | `TitleScene.unity` | 1d |
| **S4** | 뷰 스크립트 · 기존 코드 삭제 | `.cs` 5개 | 0.5d |
| **S5** | 버튼 눌림 연출 | 프레스 이동 · 호버 색 | 0.5d |
| **S6** | 아티팩트 대조 QA | 스크린샷 비교 | 0.5d |

**총 4일.** S1 → S2 → S3 → S4 순서 의존. S5·S6은 S4 뒤.

⚠ 끝나면 **타이틀 → 로비 진입이 일시적으로 끊긴다.** (1절)

---

## 1. 범위

### 하는 것

| 항목 | 아티팩트 동작 |
| --- | --- |
| 전체 레이아웃·비주얼 | 2절 스펙 그대로 |
| 닉네임 입력 | 최대 16자, 카운터 `n / 16` 실시간 |
| 입력 시 | 에러 문구 지움 |
| 접속 클릭 | `NicknameValidator`로 검증 → 실패 시 해당 문구 표시, 통과 시 에러 지움 + `ConnectRequested` 발행 |
| 버튼 연출 | 호버: 색 `#BF9CE9` + 위로 2px / 눌림: 아래로 4px + 그림자 3px로 축소 |

### 하지 않는 것 (로직 담당 몫)

Photon 연결 · 씬 전환 · 닉네임 저장/복원 · 타임아웃 · 사운드/설정 버튼 기능 · BGM/SFX.

### ⚠ 일시적으로 깨지는 것

기존 `NicknameInput`의 **접속 → 닉네임 저장 → 로비 로드**가 사라진다. 뷰는 검증 통과 시 `ConnectRequested` 이벤트만 발행하고 **구독자는 없다.** 인게임 테스트는 `DebugLobbyScene`을 쓴다. PR 설명에 명시한다.

### 아티팩트에 없는 것 (추가하지 않음)

| 항목 | 처리 |
| --- | --- |
| 입력창 포커스 연출 | 없음 (아티팩트 `outline:none`) |
| 버튼 비활성(연결 중) 상태 | 없음. 로직 연결 시 추가 |
| 에러 페이드 | 없음. 즉시 표시/해제 |
| 카운터 16자 한계색 | 없음. 항상 `#A08FB5` |

### 검증 규칙 (확정 2026-09-23)

아티팩트 검증은 빈 값 검사뿐이지만, **검증 규칙은 기존 `NicknameValidator`를 유지한다** (2~16자, 한·영·숫자, 공백 불가).
**표시 방식만 아티팩트대로** 한다: `ErrorText` 자리(높이 26 고정)에 Jua 18 `#FF6E8A` 문구를 즉시 표시하고, 입력이 바뀌면 지운다.
문구는 `NicknameValidator.GetErrorMessage()` 결과를 그대로 쓴다.

---

## 2. 아티팩트 스펙 → Unity 변환 (1920×1080 기준)

아티팩트는 `clamp()/vh/vw` 반응형이다. Canvas를 **Scale With Screen Size 1920×1080 / Match 1(Height)** 로 두면 세로 기준 값이 고정되므로, **1080 높이에서 계산한 값**을 쓴다.

### 2-1. 캔버스

| 항목 | 값 |
| --- | --- |
| Render Mode | Screen Space - Overlay |
| UI Scale Mode | Scale With Screen Size |
| Reference Resolution | 1920 × 1080 |
| Match | 1 (Height) |
| 루트 배경색 | `#2E2440` |

> 현재 씬은 Constant Pixel Size 800×600. 화면 방향 고정은 미정이라 **타이틀 Canvas만** 바꾼다.

### 2-2. 하이어라키

```
TitleCanvas
├ BG_Illust        Image (TitleBackground.png) · AspectRatioFitter Envelope Parent  ← CSS cover
├ BG_Overlay       Image · 방사형 그라데이션 스프라이트 (2-3)
├ Content          Stretch · VerticalLayoutGroup(Middle Center)
│  ├ TitleGroup    VerticalLayoutGroup · Spacing 6
│  │  ├ Logo       TMP "찹츄"
│  │  └ Subtitle   TMP "치와와개판오분전언찹츄찹찹츄"
│  └ FormGroup     560 폭 · VerticalLayoutGroup · Spacing 16
│     ├ NicknameField   [PillInputField 프리팹]
│     ├ ConnectButton   [PillButton 프리팹]
│     └ ErrorText       TMP · 높이 26 고정
├ VersionText      좌하단 (26, 22)
└ SystemButtons    우하단 (26, 22) · Spacing 10
   ├ Btn_Sound     [IconButton] "♪"
   └ Btn_Menu      [IconButton] "≡"
```

> 아티팩트 좌상단의 "배경 일러스트 슬롯 · 1920×1080" 점선 박스는 **플레이스홀더 라벨**이다. 만들지 않는다.

### 2-3. 배경

| 레이어 | 아티팩트 | Unity |
| --- | --- | --- |
| 일러스트 | 슬롯(원뿔 그라데이션 플레이스홀더) | `Sprites/TitleBackground.png` (2021×1138, 로비·룸 아티팩트의 키 아트와 동일 그림) · 중앙 기준 cover |
| 오버레이 | `radial-gradient(circle at 52% 44%, rgba(255,226,186,.72), rgba(210,96,160,.35) 46%, rgba(46,36,64,.72) 100%)` | 같은 그라데이션을 PNG로 구워 Stretch (중심 52%/44%) |

> 이 오버레이는 아티팩트에서 플레이스홀더 위에 얹힌 것이라, 실제 일러스트 위에서는 밝게 뜰 수 있다. **S6에서 눈으로 확인**하고, 과하면 로비·룸 아티팩트의 선형 오버레이(`rgba(46,36,64,.3) → .08 @42% → .36`)로 교체를 제안한다.

### 2-4. 레이아웃 수치

| 요소 | 아티팩트 CSS | 1080 기준 값 |
| --- | --- | --- |
| Content 패딩 | top `clamp(24,7vh,90)` / 좌우 24 / bottom `clamp(60,10vh,110)` | **T 76 / L·R 24 / B 108** |
| TitleGroup ↔ FormGroup 간격 | `clamp(20,4vh,56)` | **43** |
| Logo ↔ Subtitle | gap 6 | **6** |
| FormGroup 폭 | `min(560, 80vw)` | **560** |
| FormGroup 내부 간격 | `clamp(10,2vh,16)` | **16** |
| 입력창 | 높이 66 · 좌우 패딩 22 · 반경 20 · 테두리 4 · 입력↔카운터 gap 14 | 560 × 66 |
| 접속 버튼 | `min(300,60vw)` × 74 · 반경 22 · 테두리 5 | 300 × 74 |
| 에러 영역 | 높이 26 고정 | 26 (빈 문자열이어도 자리 유지) |
| 버전 텍스트 | left 26 / bottom 22 | 좌하단 (26, 22) |
| 시스템 버튼 | 46×46 · 반경 14 · 테두리 2 · gap 10 | 우하단 (26, 22) |

### 2-5. 텍스트

| 요소 | 폰트 | 크기 | 색 | 효과 |
| --- | --- | --- | --- | --- |
| Logo 「찹츄」 | Jua | 190 | `#FFFDF8` | 외곽선 5px `#3A2246` (stroke 10 + paint-order → 바깥 5), 하드 그림자 (6, −6) `rgba(58,34,70,.9)`, 자간 .04em, 행간 .95 |
| Subtitle | Jua | 36 | `#FFFDF8` | 외곽선 2.5px `#3A2246`, 그림자 (0, −3) `rgba(58,34,70,.85)` |
| 입력 텍스트 | Jua | 22 | `#5B3E8C` | — |
| 플레이스홀더 | Jua | 22 | 브라우저 기본(흐린 `#5B3E8C`) → **`#A08FB5` 제안** | — |
| 카운터 | Jua | 20 | `#A08FB5` | 우측 정렬 |
| 버튼 「접속」 | Jua | 30 | `#FFFDF8` | — |
| 에러 | Jua | 18 | `#FF6E8A` | 그림자 (0, −2) `rgba(58,34,70,.5)` |
| 버전 | IBM Plex Mono | 12 | `#FFFDF8` 72% | 문구 `ver 0.1.0 · Project Muffin` |
| 시스템 아이콘 | Jua | 18 | `#FFFDF8` | ♪ / ≡ |

TMP 변환 규칙:
- 외곽선 → 머티리얼 **Outline**. 두께는 폰트 에셋 Padding에 따라 달라지므로 **S6에서 아티팩트와 겹쳐 보며 맞춘다.**
- 하드 그림자 → **Underlay** (Offset, Softness 0). TMP는 Underlay가 1개라 로고의 부드러운 그림자(0 14 30)는 **생략**한다.
- 로고·부제는 효과가 다르므로 **머티리얼 프리셋 2개**(`Jua_Logo`, `Jua_Subtitle`), 에러용 1개(`Jua_Error`).

### 2-6. 박스 스타일

| 요소 | 배경 | 테두리 | 그림자 |
| --- | --- | --- | --- |
| 입력창 | `rgba(255,253,248,.92)` | 4px `#B18AE0` | 0 10 24 `rgba(60,25,60,.35)` + 안쪽 상단 하이라이트 2px `rgba(255,255,255,.8)` |
| 접속 버튼 | `#B18AE0` (호버 `#BF9CE9`) | 5px `#FFFDF8` | 하드 0 7 0 `rgba(84,50,140,.55)` + 소프트 0 14 26 `rgba(50,20,55,.4)` |
| 시스템 버튼 | `rgba(46,36,64,.45)` | 2px `rgba(255,253,248,.5)` | 없음 |

Unity 구현:
- 테두리 있는 둥근 사각 = **9-slice 2장 겹침**(바깥 = 테두리색, 안쪽 = 배경색, 테두리 두께만큼 inset). 반경별로 스프라이트 1쌍.
- 하드 그림자 = 같은 모양 Image를 **Y −7로 뒤에** 배치.
- 소프트 그림자 = **미리 블러 처리한 9-slice 그림자 스프라이트** 1장을 공용으로 사용.
- 입력창 안쪽 하이라이트 = 상단 2px 흰 선 Image.

### 2-7. 버튼 인터랙션

| 상태 | 본체 | 하드 그림자 |
| --- | --- | --- |
| Normal | Y 0, `#B18AE0` | Y −7 |
| Hover | Y +2, `#BF9CE9` | Y −7 |
| Pressed | Y −4 | Y −3 |

Unity `Selectable`의 Color Tint로는 이동을 못 하므로 **포인터 이벤트로 본체 RectTransform을 이동하는 작은 스크립트**로 처리한다. (모바일에선 Hover가 없으므로 Pressed만 체감됨)

---

## 3. 에셋 (S1)

| 에셋 | 출처 | 비고 |
| --- | --- | --- |
| **Jua** TTF | Google Fonts (OFL) | SDF: Atlas 2048, 한글 KS X 1001 2350자 + ASCII + `♪ ≡` |
| **IBM Plex Mono** TTF | Google Fonts (OFL) | SDF: ASCII + `·` 만 (버전 텍스트 전용) |
| `♪ ≡` 글리프 | Jua에 없으면 | 아이콘 스프라이트 2장으로 대체 |
| `TitleBackground.png` | 프로젝트에 있음 | Max Size 2048, Compression Normal |
| 둥근 사각 9-slice | 직접 생성 | 반경 20(입력창) · 22(버튼) · 14(시스템 버튼) — 흰색 단색, Image 색으로 칠함 |
| 소프트 그림자 9-slice | 직접 생성 | 가우시안 블러 둥근 사각 |
| 방사형 오버레이 PNG | 직접 생성 | 2-3 그라데이션 |

폴더: `Assets/Fonts/`, `Assets/Sprites/UI/`. 9-slice는 Texture Type `Sprite (2D and UI)`, Mesh Type `Full Rect`, **Compression None**, Image Type **Sliced**.

---

## 4. 코드 구조 (S4)

```
Scripts/UI/Title/
├ TitleView.cs            화면 루트. 검증 → 에러 표시 / ConnectRequested 발행
├ NicknameFieldView.cs    입력창: characterLimit 16, 카운터 "n / 16", 변경 시 이벤트
├ PressableButton.cs      2-7 호버/눌림 이동·색 (공용, PillButton 프리팹에 부착)
├ ErrorLabel.cs           문자열만 교체 (자리 고정, SetActive 사용 안 함)
└ VersionLabel.cs         "ver {Application.version} · Project Muffin"

재사용: Scripts/UI/NickName/NicknameValidator.cs  (검증 규칙 · 에러 문구)

삭제:
├ Scripts/UI/NickName/NicknameInput.cs        네트워크·씬로드 섞임
├ Scripts/UI/NickName/NicknameInputLogic.cs   참조 0
└ Scripts/UI/Interfaces/ISubmitLogic.cs       참조 0
```

`TitleView` 공개 API — 로직이 붙을 자리. 뷰는 네트워크를 모른다.

| 멤버 | 용도 |
| --- | --- |
| `event Action<string> ConnectRequested` | 검증 통과 닉네임 전달. **지금은 구독자 없음** |
| `void ShowError(string message)` | 외부 실패 사유 표시 (연결 실패 등) |
| `void SetNickname(string nickname)` | 저장된 닉네임 복원용. 카운터도 갱신 |

규칙:
- `Scripts/UI/Title/` 안에 `Photon.*`, `UnityEngine.SceneManagement`, `NetworkManager`, `ScenePaths` **금지**. (합격 기준)
- `async void` 금지, `Awake` 자기 초기화 / `OnEnable` 구독 (`CLAUDE.md` 12절).
- `.cs` 는 UTF-8 with BOM (`CLAUDE.md` 13절).

---

## 5. 단계별 작업

### S1. 에셋 (1d)
- [ ] Jua · IBM Plex Mono 다운로드 → `Assets/Fonts/`, TMP SDF 생성 (3절 문자셋)
- [ ] `♪ ≡` 글리프 포함 여부 확인 → 없으면 아이콘 스프라이트
- [ ] 둥근 사각 9-slice 3종 + 소프트 그림자 + 방사형 오버레이 PNG 생성, 임포트 설정
- [ ] TMP 머티리얼 프리셋 3개 (`Jua_Logo`, `Jua_Subtitle`, `Jua_Error`)

### S2. 공용 프리팹 (0.5d)
| 프리팹 | 구성 |
| --- | --- |
| `UI/Prefabs/PillButton.prefab` | SoftShadow · HardShadow(Y −7) · Body(테두리+배경) · Label · `PressableButton` |
| `UI/Prefabs/PillInputField.prefab` | SoftShadow · Border · Fill · TopHighlight · `TMP_InputField`(Text/Placeholder) · CountText · `NicknameFieldView` |
| `UI/Prefabs/IconButton.prefab` | Border · Fill · 아이콘 TMP/Image · `Button` |

- [ ] 루트 크기는 `LayoutElement`로 고정

### S3. 씬 재구성 (1d)
- [ ] 기존 Canvas 하위 **전부 삭제** 후 2-2 하이어라키로 새로 구성
- [ ] 2-1 캔버스 설정, 2-4 수치, 2-5 텍스트 적용
- [ ] `ErrorText`는 빈 문자열로 두고 높이 26 유지

> 씬 파일은 병합이 어렵다(`CLAUDE.md` 14절). S3 동안 다른 사람은 `TitleScene`을 열지 않는다.

### S4. 뷰 스크립트 (0.5d)
- [ ] 4절 5개 작성, 씬·프리팹에 배선
- [ ] 기존 3개 파일 삭제
- [ ] `Scripts/UI/Title/`에 금지 `using` 없는지 확인

### S5. 버튼 연출 (0.5d)
- [ ] 2-7 표대로 `PressableButton` 동작 (Hover/Pressed/Exit 복귀)

### S6. 아티팩트 대조 QA (0.5d)
- [ ] Game 뷰 1920×1080 스크린샷 ↔ 아티팩트 브라우저 1920×1080 스크린샷을 겹쳐 비교 (위치 ±2px)
- [ ] 로고·부제 외곽선 두께, 그림자 위치 맞춤
- [ ] 방사형 오버레이 밝기 확인 (2-3 메모)
- [ ] 1600×900 / 1280×720 / 21:9 에서 겹침·잘림 없음
- [ ] 16자 초과 차단(붙여넣기 포함), IME 한글 조합 중 카운터 정상
- [ ] 에러 표시/해제 시 버튼 위치 불변
- [ ] Android 실기 1회 — 하단 버전/버튼이 세이프에어리어 안

---

## 6. 커밋 분할

1. `chore(ui): Jua·IBM Plex Mono 폰트 및 타이틀 UI 스프라이트 추가` (S1)
2. `feat(ui): 공용 UI 프리팹 3종 추가` (S2)
3. `feat(title): 디자인 아티팩트 기준 TitleScene 재구성 + 뷰 스크립트` (S3+S4, 씬·스크립트 상호 참조라 한 커밋)
4. `feat(title): 접속 버튼 눌림 연출` (S5)

PR 설명에 **"접속 → 로비 흐름이 일시적으로 끊긴다"** 를 적는다.
