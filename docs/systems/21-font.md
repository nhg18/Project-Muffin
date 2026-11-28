# 21. 글꼴 — 목록 · 라이선스 · 통일 계획

**최종 수정일**: 2026-11-28 (3-1 「지금 쓰는 곳」 을 지금 프로젝트대로 — 인게임 남은 Galmuri · 입력칸 둥근미소를 태백체 · Pretendard 로 바꿈, 5절 3단계 끝남). 2026-11-28 (3-3 — GOAT 컷인 제목은 글자 그림). 2026-11-22 (글꼴을 **태백체 + Pretendard** 로 — 2026-11-21 PM 확정 「찹츄 글꼴 조합」 추천 ④ https://claude.ai/artifact/MAF5rMnxXmszjiuQYXtNdr. Jua 한 종 · 고정폭 IBM Plex Mono 는 폐기. 2절 · 3절 · 4절 · 5절 · 6절). 이전: 2026-11-15 (2절 #10 · #11 · 7절 #5 — 일본어 · 중국어 화면 글꼴 · 이름 폴백, `25-localization`). 이전: 2026-11-11 (본문 글꼴도 **Jua** — 글꼴은 Jua 한 종 + 고정폭, 2절 #2 · #4 · 4절 — PM). 이전: 2026-11-07 (7절 미정 4건 확정 — PM)
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
| 2 | 글꼴은 **둘뿐**: 제목 · 버튼 · 이름(카드 이름 · 닉네임) = **태백체** / 문구 · 본문 · 설명 · 코드 · 숫자 = **Pretendard** (카드 문구 · 강조 = Bold, UI 본문 = Regular). 화면마다 다른 글꼴을 쓰지 않는다. 예외는 일본어 · 중국어 화면(#10)과 이름 폴백(#11) — 2026-11-15 | 확정 (2026-11-03 「통일」 / 2026-11-21 PM — 「찹츄 글꼴 조합」 추천 ④. 2026-11-11 「Jua 한 종 + 고정폭」 폐기) |
| 3 | ~~둥근 글꼴 = Jua~~ — 2026-11-21 폐기. 제목 글꼴 = 태백체 (#2) | 확정 (2026-11-21, PM) — 적용 순서는 5절 |
| 4 | 본문 · 설명 · 작은 글자(카드 설명 등 캔버스 30 안팎)는 **Pretendard** — 작은 글자에서도 읽힌다. 강약은 굵기(Regular / Bold) · 크기 · 색(잉크 / 잉크2)으로 나눈다. TMP 의 가짜 굵게는 쓰지 않고 Bold 글꼴 에셋을 따로 둔다. 고운돋움 · Noto Sans KR · Jua 는 **버림** | 확정 (2026-11-21, PM — 2026-11-11 「본문도 Jua」 를 바꿈) |
| 5 | 고정폭 글꼴은 **두지 않는다** — 방 코드 · 친구 코드 · 버전 · 카운터 · 오류 코드도 **Pretendard Bold**. 방 코드는 영어 대문자뿐이라 헷갈리는 글자가 적고, 바뀌는 숫자(카운터)는 가운데 · 오른쪽 정렬로 폭 흔들림을 가린다 | 확정 (2026-11-22, PM — IBM Plex Mono 폐기) |
| 6 | 개발 중 TMP 글꼴 에셋은 **동적(Dynamic) · SDFAA · 아틀라스 2048 · 여백 5** 로 만든다. **출시 전에 정적(Static)으로 바꾼다** — 한글 2,350자(KS X 1001 완성형) + 영문 · 숫자 · 기호를 미리 굽는다. 11,172자 전부는 굽지 않는다 (아틀라스가 커진다). 일본어 · 중국어 글꼴(#10)은 그 언어의 상용 글자를 굽는다 — 일본 상용한자 2,136 + 가나, 간체 통용규범한자 1급 3,500, 번체 상용 4,808 (제안, 2026-11-15). 동적은 첫 글자 표시 때 잠깐 멈출 수 있어 모바일 출시 빌드에 두지 않는다 | 제안 (동적 설정값) / 확정 (출시 전 정적 전환, 2026-11-07) |
| 7 | TMP 기본 글꼴(`TMP Settings`)과 **폴백**에 한글 글꼴을 넣는다 — 지금은 LiberationSans(한글 없음) · 폴백 비어 있음 → 글꼴을 안 고른 텍스트는 □ 로 깨진다 | 제안 — 통일 때 같이 |
| 8 | Photon 데모 폴더의 글꼴은 게임에 쓰지 않는다. 데모 폴더째 빌드에서 뺀다 (라이선스 불명 글꼴 포함) | 제안 |
| 9 | 새 글꼴은 이 문서 3절에 **라이선스 · 출처 · 확인 방법**을 적은 뒤 넣는다 | 확정 (2026-11-03) |
| 10 | **일본어 · 중국어 화면**: 태백체 · Pretendard 에는 가나 · 한자가 다 있지 않다. 화면 언어가 日本語 · 简体中文 · 繁體中文 이면 그 언어용 글꼴로 바꾼다 — 언어마다 1종(제목 · 본문을 나누지 않는다), 상업 이용 가능(#1). 무엇으로 할지는 7절 #5. English 는 태백체 · Pretendard 그대로 | 확정 (언어마다 글꼴, 2026-11-15 — `25-localization` 7-5) / 미정 (글꼴 선택) |
| 11 | **이름 칸 폴백**: 닉네임에 가나 · 한자가 들어올 수 있고(`12-title-ui` 4-2) 다른 언어 화면에도 보인다. 닉네임이 나오는 글자(좌석 이름표 · 턴 띠 · 기록 · 방 칸 · 친구 목록 · 초대 배너 · 결과 화면)는 한중일 글자를 모두 가진 글꼴(예: Noto Sans CJK, OFL)을 **동적 폴백**으로 둔다 — 정적 굽기(#6)의 예외 | 제안 (2026-11-15) |

## 3. 글꼴 목록 · 라이선스

**상업 이용**: OFL(SIL Open Font License 1.1)은 상업 이용 · 임베드 · 재배포 허용, 글꼴 자체를 **단독 판매**만 금지. Apache 2.0 도 상업 이용 허용. **확인** 열은 라이선스를 어디서 확인했는지 — `저장소`는 라이선스 파일이 프로젝트 안에 있음, `문서`는 기획 문서에 출처가 적혀 있음, `외부`는 공식 배포처 기준이며 **라이선스 파일을 아직 저장소에 넣지 않았다**(6절 할 일).

### 3-1. 게임에 들어가는 글꼴 (`MuffinProject/Assets/`)

| 글꼴 | 파일 | TMP 에셋 | 라이선스 | 상업 이용 | 확인 | 지금 쓰는 곳 | 계획 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Galmuri11** Regular | `Fonts/GlaMuri/Galmuri11.ttf` | `Galmuri11 SDF` (동적) | OFL 1.1 | ○ | 외부 — quiple.dev/galmuri · github quiple/galmuri | 없음 (2026-11-28 마지막 인게임 프리팹 · 카드 연출 시험 씬까지 바꿈) | **삭제** (5절 4단계) |
| **Galmuri11** Bold | `Fonts/GlaMuri/Galmuri11-Bold.ttf` | `Galmuri11-Bold SDF` (동적) | OFL 1.1 | ○ | 외부 | 없음 (2026-11-28 마지막 인게임 프리팹 · 카드 연출 시험 씬까지 바꿈) | **삭제** (5절 4단계) |
| Galmuri9 | `Fonts/GlaMuri/Galmuri9.ttf` | `Galmuri9 SDF` | OFL 1.1 | ○ | 외부 | 없음 | **삭제** |
| Galmuri7 | `Fonts/GlaMuri/Galmuri7.ttf` | `Galmuri7 SDF` | OFL 1.1 | ○ | 외부 | 없음 | **삭제** |
| GalmuriMono 7 · 9 · 11 | `Fonts/GlaMuri/GalmuriMono*.ttf` | 없음 | OFL 1.1 | ○ | 외부 | 없음 | **삭제** |
| **학교안심 둥근미소** B | `TextMesh Pro/Fonts/Hakgyoansim Dunggeunmiso OTF B.otf` | `…OTF B SDF` + 머티리얼 Logo · Subtitle · Error · `SDF 1` | OFL | ○ | 문서 — store-release 4절 (KERIS 교육저작권지원센터) | `OldGameScene` 과 그 씬만 쓰는 옛 카드 프리팹 4종(`PlayerActionCard` · `… Dummy` · `PlayerCounterCard` · `PlayerTrapCard`) | **삭제** — `OldGameScene` 과 함께 (5절 4단계) |
| **학교안심 둥근미소** R | `TextMesh Pro/Fonts/Hakgyoansim Dunggeunmiso OTF R.otf` | `…OTF R SDF` | OFL | ○ | 문서 | `PlayerSeatUI`(`OldGameScene` 만 씀) | **삭제** — `OldGameScene` 과 함께 |
| **IBM Plex Mono** Regular | `Fonts/IBMPlexMono-Regular.ttf` | `IBMPlexMono SDF` | OFL 1.1 | ○ | 저장소 — `Fonts/IBMPlexMono-OFL.txt` | 없음 | **삭제** |
| NeoDunggeunmo Pro | `TextMesh Pro/Fonts/NeoDunggeunmoPro-Regular.ttf` | 없음 | OFL 1.1 | ○ | 외부 — github neodgm | 없음 | **삭제** |
| Liberation Sans | `TextMesh Pro/Fonts/LiberationSans.ttf` | `LiberationSans SDF` + Fallback · 머티리얼 2 | OFL 1.1 | ○ | 저장소 — `TextMesh Pro/Fonts/LiberationSans - OFL.txt` | OldGameScene · **TMP 기본 글꼴** | TMP 패키지 기본. 기본 글꼴 자리는 한글 글꼴로 바꾼다 (2절 #7) |
| **Jua** | `Fonts/Jua/Jua-Regular.ttf` | `Jua SDF` (동적 · 여백 10) · `Jua Logo SDF` (로고 3글자 정적) · 머티리얼 `Jua SDF - Outline` | OFL 1.1 | ○ | 저장소 — `Fonts/Jua/OFL.txt` (Google Fonts, BM Jua · 우아한형제들) | 없음 | **삭제** |
| **태백체** | `Fonts/Taebaek/` | `Taebaek SDF` | 태백시 무료 글꼴 — 인쇄 · 웹 · 영상 · 임베딩 · 로고 허용, 글꼴 파일 판매 금지 | ○ | 외부 — 산돌구름 무료 글꼴 안내(저작권자 태백시). 원문을 받아 넣을 것 (6절) | 화면 전부의 제목 · 버튼 · 이름 · 스티커 글자 · 터지는 글자. `Taebaek Logo SDF` = 타이틀 로고 · 4 단계 결과 글자 | 쓴다 (2절 #2) |
| **Pretendard** Regular · Bold | `Fonts/Pretendard/` | `Pretendard SDF` · `Pretendard Bold SDF` | OFL 1.1 | ○ | 외부 — github orioncactus/pretendard | 화면 전부의 문구 · 본문 · 수치 · 입력칸 · 토스트. `Pretendard SDF` = TMP 기본 글꼴 | 쓴다 (2절 #2 · #5) |

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
| Jua · IBM Plex Mono | 2026-11-21 전 아티팩트 시안 (Google Fonts CDN) | 옛 판 — 새 시안 · 통합 v3 는 태백체 · Pretendard |
| 고운돋움 · Noto Sans KR | 옛 시안 본문 · 「찹츄 3D 카드 연출」 글꼴 비교 | 게임에 넣지 않는다 (2절 #4, 2026-11-11). 옛 시안을 고칠 때 Jua 로 바꾼다 |
| IBM Plex Sans KR · Yuji Boku | 규칙 시험용 아티팩트 「프로토타입」 — 본문 · 지옥참마도 컷인 한자 | 본문은 4절대로 Jua. Yuji Boku 는 컷인 한 곳뿐 — 넣을지는 그 연출을 만들 때 정한다 (**미정**) |
| Noto Serif CJK JP Black | 「카드 이펙트 v2」 GOAT 컷인의 제목 「GOAT」 | 글꼴로 넣지 않는다 — 네 글자를 그림 한 장(`Sprites/CardFx/Goat/goat_title.png`)으로 두었다 (2026-11-28, `10-ui` 2-1 「GOAT 컷인」). SIL OFL 1.1 |
| Galmuri11 | 옛 인게임 시안 (2026-09-28 판) | 옛 판 — 인게임 시안은 2026-11-06 「찹츄 인게임 UI」(`10-ui` 2-1)로 바뀌었고 Jua 를 쓴다 |
| 학교안심 둥근미소 | `12-title-ui` 9-2 · `14` 7-1 · `16` · `17` 4-3 | 옛 판 — 5절 순서로 고친다 |

## 4. 용도별 글꼴 (통일 뒤)

| 용도 | 글꼴 | TMP 에셋 이름 | 예 | 구분 |
| --- | --- | --- | --- | --- |
| 제목 · 버튼 · 카드 이름 · 닉네임 · 팝업 머리 띠 · 탭 · 묶음 제목 | **태백체** | `Taebaek SDF` | 랜덤 매칭 · 찹츄 · 「물어뜯기」 · 찹츄왕 2 | 확정 (2026-11-21, PM) |
| 카드 문구 · 강조 숫자(HP · 장수) · 본문 첫 줄 · 입력칸 글자 · 방 코드 · 친구 코드 · 버전 · 카운터 · 타이머 · 오류 코드 | **Pretendard Bold** | `Pretendard Bold SDF` | 20 피해를 준다 · HP 56 · ABCDE · ver 0.1.0 · 0/16 | 확정 (2026-11-21 · 코드 · 숫자 2026-11-22, PM) |
| UI 본문 · 설명 · 안내문 · 목록 보조 글자 · 토스트 · 에러 줄 | **Pretendard** Regular | `Pretendard SDF` | 「방 코드를 입력해 주세요.」 · 친구 상태 · 토스트 | 확정 (2026-11-21, PM) |
| 로고 | 최종 로고 그림이 오면 **그림 한 장** — 그 전에는 지금 글자 그대로 (`20` 7-2) | — | 찹츄! | 확정 (2026-11-22, PM) |
| TMP 기본 · 폴백 | 기본 = `Pretendard SDF`, 폴백 = `Taebaek SDF` → `LiberationSans SDF` | — | 글꼴을 안 고른 텍스트 · 없는 글자 | 제안 |

- 굵기: 태백체는 한 굵기, Pretendard 는 Regular · Bold 두 에셋을 쓴다. 옛 문서의 Jua · 둥근미소 B · R · IBM Plex Mono 는 위 표의 용도대로 바꾼다. TMP 의 가짜 굵게(Bold 스타일)는 쓰지 않는다.
- 크기 · 색은 각 화면 문서가 정한다. 이 문서는 **어떤 글꼴인지**만.

## 5. 통일 순서 (실행 계획)

한 번에 바꾸지 않는다. **문서 → 공용 프리팹 → 씬** 순서로, 한 단계가 끝나면 `multi-test ui` 로 글자 깨짐(□)을 확인한다. **시작 시점 = 타이틀 디자인 시안을 고른 뒤** (확정 2026-11-07, 7절 #2) — 그 전에는 지금 글꼴을 그대로 둔다.

| 단계 | 할 일 | 끝났다는 기준 | 구분 |
| --- | --- | --- | --- |
| 0 | 태백체 · Pretendard(Regular · Bold) 파일 + 라이선스 원문을 `Assets/Fonts/Taebaek/` · `Assets/Fonts/Pretendard/` 에 넣고 TMP 동적 SDF 생성 (2절 #6 설정). `TMP Settings` 기본 · 폴백 변경 (4절) | 빈 씬에 TMP 텍스트를 놓고 한글 · 영문 · 숫자가 나온다 | 제안 |
| 1 | 화면 문서의 글꼴 이름 교체: `10-ui` · `12-title-ui` · `14` · `16` · `17` · `20` · `22-popup-ui` — "Jua" · "IBM Plex Mono" · "학교안심 둥근미소" · "Galmuri11" → 4절 용도대로 태백체 / Pretendard. 화면 문서에 글꼴 이름이 나오면 이 문서가 옳다 | 문서에 옛 글꼴 이름이 3-3 외에 남지 않는다 | 제안 |
| 2 | 공용 프리팹: 버튼 · 입력칸 · 팝업 · 좌석 이름표 | 타이틀 · 로비 · 룸 · 팝업 글자가 4절대로 태백체 · Pretendard | 제안 |
| 3 | 씬: TitleScene → LobbyScene → RoomScene → GameScene(손패 카드 프리팹 3종 포함) → CardFxTestScene | 각 씬 텍스트 컴포넌트에 둥근미소 · Galmuri 참조 0 | **끝남** (2026-11-28) — 옛 글꼴 참조는 `OldGameScene` 과 그 씬만 쓰는 옛 프리팹에만 남는다 → 4단계 |
| 4 | 삭제: Jua · IBM Plex Mono · Galmuri 전부 · 둥근미소 otf · SDF · 머티리얼 · NeoDunggeunmo · `OldGameScene` 의 참조 정리 | 3-1 에서 "폐기 예정 · 삭제" 행이 사라진다. 프로젝트 전체 검색에 글꼴 GUID 참조 0 | 제안 — 삭제 전에 `archive/` 태그 불필요 (글꼴은 다시 받을 수 있다) |
| 5 | 이 문서 3절 갱신 · `store-release` 4절 폰트 링크를 태백체 · Pretendard 로 | — | 제안 |
| 6 (출시 전) | `Taebaek SDF` · `Pretendard SDF` · `Pretendard Bold SDF` 를 **정적**으로 다시 굽는다 — 한글 2,350자 + ASCII + 사용하는 기호(… · — · 「」 등). 아틀라스 크기는 글자가 전부 들어가는 최소 2의 거듭제곱. 동적 설정은 개발용 복사본에만 남긴다 | 출시 빌드의 TMP 에셋에 Dynamic 이 없다. 모든 씬 · 팝업에서 □ 가 없다 (`multi-test ui`). 문서에 없는 글자(예: 희귀 한자)가 쓰이면 그 글자를 문자 집합에 더한다 | 확정 (전환, 2026-11-07 · 2절 #6) / 제안 (문자 집합 세부) |

- 2 · 3 단계는 **UI 담당**이 Unity 에서, 0 · 1 · 4 · 5 는 문서 · 에셋 작업이라 누구나. 씬 · 프리팹 동시 수정 금지 규칙(`git-workflow`)을 지킨다.
- 디자인(스프라이트 · 색)이 나중에 바뀌어도 글꼴은 이 문서대로 유지한다 — 글꼴은 디자인 언어의 일부로 이미 정했다.

## 6. 할 일 (라이선스 파일)

`외부` 확인 글꼴은 공식 배포처의 OFL 원문을 글꼴 옆에 넣는다. 넣으면 3-1 의 확인 열을 `저장소`로 바꾼다.

- [ ] 태백체 — 태백시 배포처의 사용 조건 원문 (상업 이용 · 임베딩 허용, 파일 판매 금지 — 산돌구름 안내 기준, 원문으로 다시 확인)
- [ ] Pretendard — github orioncactus/pretendard `LICENSE` (OFL 1.1)
- [x] Jua — `Fonts/Jua/OFL.txt` (폐기 전까지)
- [ ] 학교안심 둥근미소 — KERIS 배포 페이지의 라이선스 안내 (폐기 전까지는 빌드에 들어가므로 필요)
- [ ] Galmuri — github quiple/galmuri `LICENSE` (폐기 전까지)
- [ ] Photon 데모 폴더를 빌드에서 제외하는 방법 확인 (삭제 · `~` 접미 · Editor-only 중 하나) — 2절 #8

## 7. 미정 / 결정 필요

| # | 항목 | 막는 작업 | 어디서 정하나 |
| --- | --- | --- | --- |
| 1 | ~~본문 글꼴을 고운돋움으로 할지 (Jua 한 종만으로 갈지)~~ — 2026-11-07 확정: 본문 = 고운돋움, 제목 · 버튼 · 숫자 = Jua → **2026-11-11 바꿈: Jua 한 종** (2절 #4 · 4절) | | |
| 2 | ~~통일 시점 — 지금 바로(5절 전부) / 타이틀 시안 선택 뒤 / 로그인(기능 12) 때~~ — 2026-11-07 확정: **타이틀 디자인 시안 선택 뒤** (5절) | | |
| 3 | ~~출시 전 TMP 에셋을 정적(한글 2,350자 미리 굽기)으로 바꿀지 — 동적은 첫 글자 표시 때 잠깐 멈출 수 있다~~ — 2026-11-07 확정: 출시 전 정적으로 전환 (2절 #6 · 5절 6단계) | | |
| 4 | ~~카드 이름 · 설명처럼 작은 글자(캔버스 30 안팎)에서 Jua 가 읽히는지 — 안 읽히면 그 자리만 고운돋움~~ — 2026-11-07 확정: 작은 글자(카드 설명 등)는 고운돋움 → **2026-11-11 바꿈: 작은 글자도 Jua** (#1 · 4절) | | |
| 5 | 일본어 · 简体中文 · 繁體中文 화면 글꼴 — 후보는 둥근 고딕 계열 OFL 글꼴(예: 일본어 M PLUS Rounded 1c, 번체 jf open 粉圓) · 없으면 Noto Sans JP / SC / TC. 라이선스 · 글자 수 · 태백체 · Pretendard 와 섞였을 때의 느낌을 보고 고른다 | 일본어 · 중국어 화면 (2절 #10) | 디자인 · PM |
