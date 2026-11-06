# 21. 글꼴 — 목록 · 라이선스 · 통일 계획

**최종 수정일**: 2026-11-06
**분류**: MVP (출시 빌드에 들어가는 글꼴의 라이선스는 출시 조건 — [`store-release.md`](../store-release.md) 4절)
**출처**: 2026-11-03 사용자 요청 「흩어진 글꼴 정보를 한곳에, 상업 이용 가능 여부 포함, 통일」. 그 전까지는 `10-ui` 2-1 · `12-title-ui` 9-2 · `14` · `16` · `17` · `store-release` 에 따로 적혀 있었다. 글꼴 **전환 방향**(Jua)은 `worklog` 2026-11-02 결정.

> 이 문서가 글꼴의 **유일한 기준**이다. 화면 문서(10 · 12 · 14 · 16 · 17 · 20)는 글꼴 이름을 쓰되 라이선스 · 에셋 경로 · 교체 계획은 여기만 본다. 글꼴을 추가 · 교체할 때 이 문서의 2절과 3절을 먼저 고친다.

---

## 1. 목적

1. 프로젝트에 **어떤 글꼴이 있고, 어디에 쓰이며, 상업 이용이 되는지** 한 표로 본다.
2. 화면마다 다른 글꼴(인게임 Galmuri · 타이틀/로비 둥근미소)을 **한 벌로 통일**한다.
3. 출시 빌드에 라이선스가 불분명한 글꼴이 섞이지 않게 한다.

## 2. 핵심 규칙

| # | 규칙 | 구분 |
| --- | --- | --- |
| 1 | 게임에 쓰는 글꼴은 **상업 이용 가능 라이선스**(OFL · Apache 2.0 등)만. 라이선스 원문 파일을 글꼴 파일 옆에 둔다 | 확정 — store-release 4절 |
| 2 | 글꼴은 **용도별 한 종**: 제목 · 버튼 · 이름(둥근 글꼴 1) / 본문 · 설명(본문 글꼴 1) / 코드 · 숫자(고정폭 1). 화면마다 다른 글꼴을 쓰지 않는다 | 확정 (2026-11-03, 사용자 「통일」) |
| 3 | 둥근 글꼴 = **Jua** 로 바꾼다 (「찹츄 테이블」 디자인 언어) | 확정 (2026-11-02, 사용자) — 적용 시점은 5절 |
| 4 | 본문 글꼴 = **고운돋움(Gowun Dodum)** — Jua 는 굵기가 하나뿐이라 긴 설명 · 작은 글자에는 맞지 않는다. 시안(아티팩트)이 이미 이 조합을 쓴다 | 제안 |
| 5 | 고정폭 = **IBM Plex Mono** (방 코드 · 친구 코드 · 버전 · 카운터) | 확정 (12-title-ui 9-2, 기존) |
| 6 | TMP 글꼴 에셋은 **동적(Dynamic) · SDFAA · 아틀라스 2048 · 여백 5** 로 만든다. 한글 11,172자를 미리 굽지 않는다 (아틀라스가 커진다). 출시 전 정적으로 바꿀지는 미정 #3 | 제안 (지금 Galmuri11 SDF 설정) |
| 7 | TMP 기본 글꼴(`TMP Settings`)과 **폴백**에 한글 글꼴을 넣는다 — 지금은 LiberationSans(한글 없음) · 폴백 비어 있음 → 글꼴을 안 고른 텍스트는 □ 로 깨진다 | 제안 — 통일 때 같이 |
| 8 | Photon 데모 폴더의 글꼴은 게임에 쓰지 않는다. 데모 폴더째 빌드에서 뺀다 (라이선스 불명 글꼴 포함) | 제안 |
| 9 | 새 글꼴은 이 문서 3절에 **라이선스 · 출처 · 확인 방법**을 적은 뒤 넣는다 | 확정 (2026-11-03) |

## 3. 글꼴 목록 · 라이선스

**상업 이용**: OFL(SIL Open Font License 1.1)은 상업 이용 · 임베드 · 재배포 허용, 글꼴 자체를 **단독 판매**만 금지. Apache 2.0 도 상업 이용 허용. **확인** 열은 라이선스를 어디서 확인했는지 — `저장소`는 라이선스 파일이 프로젝트 안에 있음, `문서`는 기획 문서에 출처가 적혀 있음, `외부`는 공식 배포처 기준이며 **라이선스 파일을 아직 저장소에 넣지 않았다**(6절 할 일).

### 3-1. 게임에 들어가는 글꼴 (`MuffinProject/Assets/`)

| 글꼴 | 파일 | TMP 에셋 | 라이선스 | 상업 이용 | 확인 | 지금 쓰는 곳 | 계획 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Galmuri11** Regular | `Fonts/GlaMuri/Galmuri11.ttf` | `Galmuri11 SDF` (동적) | OFL 1.1 | ○ | 외부 — quiple.dev/galmuri · github quiple/galmuri | GameScene · 손패 카드 프리팹 3종 | **폐기 예정** → Jua / 고운돋움 |
| **Galmuri11** Bold | `Fonts/GlaMuri/Galmuri11-Bold.ttf` | `Galmuri11-Bold SDF` (동적) | OFL 1.1 | ○ | 외부 | GameScene · CardFxTestScene · 손패 카드 프리팹 3종 | **폐기 예정** |
| Galmuri9 | `Fonts/GlaMuri/Galmuri9.ttf` | `Galmuri9 SDF` | OFL 1.1 | ○ | 외부 | RoomScene | **폐기 예정** |
| Galmuri7 | `Fonts/GlaMuri/Galmuri7.ttf` | `Galmuri7 SDF` | OFL 1.1 | ○ | 외부 | 없음 | **삭제** |
| GalmuriMono 7 · 9 · 11 | `Fonts/GlaMuri/GalmuriMono*.ttf` | 없음 | OFL 1.1 | ○ | 외부 | 없음 | **삭제** |
| **학교안심 둥근미소** B | `TextMesh Pro/Fonts/Hakgyoansim Dunggeunmiso OTF B.otf` | `…OTF B SDF` + 머티리얼 Logo · Subtitle · Error · `SDF 1` | OFL | ○ | 문서 — store-release 4절 (KERIS 교육저작권지원센터) | TitleScene · LobbyScene · RoomScene · DebugLobbyScene · OldGameScene · PillButton · 팝업 3종(Input · Warning · Toast) · 옛 카드 프리팹 4종 | **폐기 예정** → Jua |
| **학교안심 둥근미소** R | `TextMesh Pro/Fonts/Hakgyoansim Dunggeunmiso OTF R.otf` | `…OTF R SDF` | OFL | ○ | 문서 | RoomScene · PlayerSeatUI · PillInputField · WarningPopup | **폐기 예정** → 고운돋움 |
| **IBM Plex Mono** Regular | `Fonts/IBMPlexMono-Regular.ttf` | `IBMPlexMono SDF` | OFL 1.1 | ○ | 저장소 — `Fonts/IBMPlexMono-OFL.txt` | TitleScene (카운터) | **유지** |
| NeoDunggeunmo Pro | `TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular.ttf` | 없음 | OFL 1.1 | ○ | 외부 — github neodgm | 없음 | **삭제** |
| Liberation Sans | `TextMesh Pro/Fonts/LiberationSans.ttf` | `LiberationSans SDF` + Fallback · 머티리얼 2 | OFL 1.1 | ○ | 저장소 — `TextMesh Pro/Fonts/LiberationSans - OFL.txt` | OldGameScene · **TMP 기본 글꼴** | TMP 패키지 기본. 기본 글꼴 자리는 한글 글꼴로 바꾼다 (2절 #7) |
| **Jua** | 없음 — 아직 안 넣음 | 없음 | OFL 1.1 | ○ | 외부 — Google Fonts (BM Jua, 우아한형제들) | 아티팩트 시안(타이틀 · 테이블 · 로비 · 카드 이펙트) | **넣는다** — 둥근 글꼴 |
| **고운돋움** (Gowun Dodum) | 없음 | 없음 | OFL 1.1 | ○ | 외부 — Google Fonts | 아티팩트 시안 본문 | **넣는다** — 본문 글꼴 (제안) |

### 3-2. 게임에 쓰지 않는 글꼴 (Photon 데모 — 빌드에서 뺀다)

| 글꼴 | 위치 | 라이선스 | 비고 |
| --- | --- | --- | --- |
| Jura Medium | `Photon/PhotonChat/Demos/…` · `PhotonRealtime/Demos/…` · `PhotonUnityNetworking/Demos/Shared Assets/Skin/` | OFL | 데모 전용 |
| Open Sans 4종 | `Photon/PhotonChat/Demos/Demo Chat/Resources/OpenSans/` | Apache 2.0 | 데모 전용 |
| Orbitron Black | `Photon/PhotonUnityNetworking/Demos/Shared Assets/Skin/` | OFL | 데모 전용 |
| eurof35 | `Photon/PhotonUnityNetworking/Demos/Shared Assets/Skin/` | **불명** | 데모 전용. 게임 어디에도 쓰지 않는다 (2절 #8) |

### 3-3. 문서 · 시안에만 나오는 글꼴

| 글꼴 | 어디 | 비고 |
| --- | --- | --- |
| Jua · 고운돋움 · IBM Plex Mono | 아티팩트 시안 전부 (Google Fonts CDN) | 3-1 의 Jua · 고운돋움을 넣으면 시안과 게임이 같아진다 |
| IBM Plex Sans KR · Yuji Boku | 인게임 시안 아티팩트 「프로토타입」 (`10-ui` 2-1) — 본문 · 지옥참마도 컷인 한자 | 본문은 4절대로 고운돋움. Yuji Boku 는 컷인 한 곳뿐 — 넣을지는 그 연출을 만들 때 정한다 (**미정**) |
| Galmuri11 | `10-ui` 2-1 옛 시안 수치 (2026-09-28 판) | 옛 판 — 인게임 시안은 2026-11-06 「프로토타입」으로 바뀌었고 Jua 를 쓴다 |
| 학교안심 둥근미소 | `12-title-ui` 9-2 · `14` 7-1 · `16` · `17` 4-3 | 옛 판 — 5절 순서로 고친다 |

## 4. 용도별 글꼴 (통일 뒤)

| 용도 | 글꼴 | TMP 에셋 이름 | 예 | 구분 |
| --- | --- | --- | --- | --- |
| 제목 · 로고 · 버튼 · 닉네임 · 카드 이름 · 숫자 강조 | **Jua** | `Jua SDF` | 랜덤 매칭 · 찹츄 · 체력 수 | 확정 (글꼴) / 제안 (범위) |
| 본문 · 설명 · 안내문 · 목록 보조 글자 · 카드 설명 | **고운돋움** | `GowunDodum SDF` | "방 코드 입력" · 친구 상태 · 토스트 | 제안 |
| 코드 · 버전 · 카운터 · 타이머 | **IBM Plex Mono** | `IBMPlexMono SDF` | AB12 · ver 0.1.0 · 0/16 | 확정 (기존) |
| TMP 기본 · 폴백 | 기본 = `GowunDodum SDF`, 폴백 = `Jua SDF` → `IBMPlexMono SDF` → `LiberationSans SDF` | — | 글꼴을 안 고른 텍스트 · 없는 글자 | 제안 |

- 굵기: Jua · 고운돋움 모두 **한 굵기**다. "B" 가 필요한 자리는 Jua, "R" 은 고운돋움으로 나눈다 (옛 문서의 둥근미소 B → Jua, R → 고운돋움). TMP 의 가짜 굵게(Bold 스타일)는 쓰지 않는다.
- 크기 · 색은 각 화면 문서가 정한다. 이 문서는 **어떤 글꼴인지**만.

## 5. 통일 순서 (실행 계획)

한 번에 바꾸지 않는다. **문서 → 공용 프리팹 → 씬** 순서로, 한 단계가 끝나면 `multi-test ui` 로 글자 깨짐(□)을 확인한다.

| 단계 | 할 일 | 끝났다는 기준 | 구분 |
| --- | --- | --- | --- |
| 0 | Jua · 고운돋움 ttf + OFL 파일을 `Assets/Fonts/Jua/` · `Assets/Fonts/GowunDodum/` 에 넣고 TMP 동적 SDF 생성 (2절 #6 설정). `TMP Settings` 기본 · 폴백 변경 (4절) | 빈 씬에 TMP 텍스트를 놓고 한글 · 영문 · 숫자가 나온다 | 제안 |
| 1 | 화면 문서의 글꼴 이름 교체: `12-title-ui` 9-2 · `14` 7-1 · `16` 4절 · `17` 4-3 · `20` · `10-ui` 2-1 — "학교안심 둥근미소 B" → "Jua", "R" → "고운돋움", "Galmuri11" → 용도에 따라 | 문서에 둥근미소 · Galmuri 가 3-3 외에 남지 않는다 | 제안 — 타이틀 디자인 시안 선택(worklog 다음 할 일)과 같이 |
| 2 | 공용 프리팹: `PillButton` · `PillInputField` · 팝업 3종(Input · Warning · Toast) · `PlayerSeatUI` | 타이틀 · 로비 · 룸 · 팝업 글자가 전부 Jua / 고운돋움 | 제안 |
| 3 | 씬: TitleScene → LobbyScene → RoomScene → GameScene(손패 카드 프리팹 3종 포함) → CardFxTestScene | 각 씬 텍스트 컴포넌트에 둥근미소 · Galmuri 참조 0 | 제안 — 씬은 사람이 Unity 에서 작업 중인 것을 덮어쓰지 않게 저장 확인 뒤 |
| 4 | 삭제: Galmuri 전부 · 둥근미소 otf · SDF · 머티리얼 · NeoDunggeunmo · `OldGameScene` 의 참조 정리 | 3-1 에서 "폐기 예정 · 삭제" 행이 사라진다. 프로젝트 전체 검색에 글꼴 GUID 참조 0 | 제안 — 삭제 전에 `archive/` 태그 불필요 (글꼴은 다시 받을 수 있다) |
| 5 | 이 문서 3절 갱신 · `store-release` 4절 폰트 링크를 Jua · 고운돋움 · IBM Plex Mono 로 | — | 제안 |

- 2 · 3 단계는 **UI 담당**이 Unity 에서, 0 · 1 · 4 · 5 는 문서 · 에셋 작업이라 누구나. 씬 · 프리팹 동시 수정 금지 규칙(`git-workflow`)을 지킨다.
- 디자인(스프라이트 · 색)이 나중에 바뀌어도 글꼴은 이 문서대로 유지한다 — 글꼴은 디자인 언어의 일부로 이미 정했다.

## 6. 할 일 (라이선스 파일)

`외부` 확인 글꼴은 공식 배포처의 OFL 원문을 글꼴 옆에 넣는다. 넣으면 3-1 의 확인 열을 `저장소`로 바꾼다.

- [ ] Jua — Google Fonts 배포 zip 의 `OFL.txt`
- [ ] 고운돋움 — 같음
- [ ] 학교안심 둥근미소 — KERIS 배포 페이지의 라이선스 안내 (폐기 전까지는 빌드에 들어가므로 필요)
- [ ] Galmuri — github quiple/galmuri `LICENSE` (폐기 전까지)
- [ ] Photon 데모 폴더를 빌드에서 제외하는 방법 확인 (삭제 · `~` 접미 · Editor-only 중 하나) — 2절 #8

## 7. 미정 / 결정 필요

| # | 항목 | 막는 작업 | 어디서 정하나 |
| --- | --- | --- | --- |
| 1 | 본문 글꼴을 고운돋움으로 할지 (Jua 한 종만으로 갈지) | 4절 · 5절 0단계 | 타이틀 디자인 시안 선택 때 같이 — 사용자 |
| 2 | 통일 시점 — 지금 바로(5절 전부) / 타이틀 시안 선택 뒤 / 로그인(기능 12) 때 | 5절 | PM |
| 3 | 출시 전 TMP 에셋을 정적(한글 2,350자 미리 굽기)으로 바꿀지 — 동적은 첫 글자 표시 때 잠깐 멈출 수 있다 | 2절 #6 | 모바일 실기 확인 뒤 |
| 4 | 카드 이름 · 설명처럼 작은 글자(캔버스 30 안팎)에서 Jua 가 읽히는지 — 안 읽히면 그 자리만 고운돋움 | 4절 범위 | 인게임 시안 다시 만들 때 |
