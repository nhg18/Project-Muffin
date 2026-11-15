# CLAUDE.md — 찹츄

2~4인 파티 카드 대전 게임 (PC · 모바일).

## 기준 문서

| 대상 | 위치 |
| --- | --- |
| 게임 규칙 · 수치 | `docs/systems/*.md` |
| 화면 · 연출 | 각 UI 문서(`docs/systems/`)가 가리키는 HTML 시안 아티팩트 — 적힌 게시본(버전 ID)을 따른다 |
| 기능 목록 | `docs/development-plan.md` |
| 작업 기록 | `docs/worklog.md` — 세션 시작 때 읽고, 끝날 때 쓴다 |
| 코드 규칙 | `docs/CODE_CONVENTION.md` — 새 규칙도 여기에만 적는다 |
| 기획서 작성 · 버전 · 출시 | `docs/GDD_GUIDE.md` · `docs/versioning.md` · `docs/store-release.md` |

## 규칙

1. 코드와 문서가 다르면 문서가 옳다. 노션과 다르면 `docs/systems/` 가 옳다.
2. 문서에 없는 규칙, `미정` 값은 구현하지 않고 묻는다.
3. 게임 규칙 판정은 방장(마스터 클라이언트)만 한다 → skill `network-rpc`.
4. 요청 범위만 고친다. 같은 작업에서 드러난 문서 위반은 함께 고치고 보고한다. 더 좋은 구조는 제안까지만. 지금 필요 없는 분리 · 추상화는 만들지 않는다.
5. 답변은 한국어.

## 환경

Unity 6 · Built-in 렌더 파이프라인 · C# 9 · Photon PUN2 · PC / Android / iOS.
프로젝트 루트 `MuffinProject/`, 정확한 버전은 `ProjectVersion.txt` · `Packages/manifest.json`.
색 공간 Gamma — 반투명 UI 를 HTML 시안과 같게 섞기 위해.

## 작업 방식

* 바꾼 것은 직접 실행해서 확인한다 → skill `multi-test`. 확인하지 못한 것은 못 했다고 말한다.
* 다음 작업이 이어받아 고치고 조합할 수 있게 만든다.
* 사람이 Unity 에서 하고 있는 작업(저장 안 한 씬 · 에셋)을 덮어쓰지 않는다.
* 막히면 우회하지 말고 멈추고 묻는다.

## Git

`main` ← `develop` ← 작업 브랜치. `develop` 머지는 PM 만 → skill `git-workflow`.
