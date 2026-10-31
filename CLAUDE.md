# CLAUDE.md — 찹츄

2~4인 모바일 파티 카드 대전 게임. 팀: PM(기획 · UI 디자인 · 코딩) · 개발 · 아트(손그림).

## 기준 문서

| 대상 | 위치 |
| --- | --- |
| 게임 규칙 · 수치 | `docs/systems/*.md` |
| 화면 · 연출 | HTML 시안 → Unity 로 옮긴다 |
| 기능 목록 | `docs/development-plan.md` |
| 작업 기록 | `docs/worklog.md` — 세션 시작 때 읽고, 끝날 때 쓴다 |
| 코드 컨벤션 · 기획서 작성 · 버전 · 출시 | `docs/CODE_CONVENTION.md` · `docs/GDD_GUIDE.md` · `docs/versioning.md` · `docs/store-release.md` |
| 참고 코드 (옮기지 않음) | `Assets/Scripts old/` · `upgrade/unity6` 브랜치의 `Assets/Scripts/_Game/` |

## 규칙

1. 코드와 문서가 다르면 문서가 옳다. 노션과 다르면 `docs/systems/` 가 옳다.
2. 문서에 없는 규칙, `미정` 값은 구현하지 않고 묻는다.
3. 게임 규칙 판정은 방장(마스터 클라이언트)만 한다 → skill `network-rpc`.
4. 요청 범위만 고친다. 더 좋은 구조는 제안까지만. 지금 필요 없는 분리 · 추상화는 만들지 않는다.
5. Claude 는 Unity 결과를 볼 수 없다. 코드를 바꾸면 Unity 에서 확인할 항목(컴파일 · 씬 동작 · 인스펙터 연결)을 보고한다.
6. 답변은 한국어.

## 환경

| 항목 | 값 |
| --- | --- |
| Unity | 6000.3.27f1 (6.3 LTS) · Built-in 렌더 파이프라인 · 프로젝트 루트 `MuffinProject/` |
| 언어 | C# 9 — C# 10+ 문법 금지 |
| 패키지 | Photon PUN2 · ParrelSync · DOTween · uGUI 2.0(TMP 포함) · splines 2.9.1 |
| 플랫폼 | Android / iOS |
| 색 공간 | Gamma — 반투명 UI 를 HTML 시안과 같게 섞기 위해 |

## Unity

* 규칙 수치는 코드 · ScriptableObject, 연출값 · 참조는 Inspector.
* `Awake` 자기 초기화, `Start` 다른 오브젝트 참조, `OnEnable` 이벤트 구독(다른 `Instance` 접근 금지).
* 싱글톤은 `Core/Singleton.cs` · `Core/SingletonPun.cs` 를 쓴다.
* RPC 이름은 `nameof(RPC_Xxx)`. `async void` · `[SerializeField] static` 금지.
* 테스트는 Test Runner 없이 멀티 실행: `DebugLobbyScene` → 게임 씬을 ParrelSync 클론과 돌리고 Console 로그로 본다.
* 씬을 스크립트로 다시 열기 전에 `isDirty` 를 확인한다(저장 안 한 편집이 사라진다). `.unity` 를 직접 고치기 전에는 Unity 에서 저장해 달라고 하고, 사용자가 추가한 오브젝트를 지우지 않는다.
* `.cs` 는 UTF-8 BOM, 파일명은 영문.
* `MuffinProject_clone_*/` 는 ParrelSync 복제본이다. 읽지도 고치지도 않는다.

## Git

`main` ← `develop` ← `feature/<기능>`. `develop` 머지는 PM 만 → skill `git-workflow`.

`.claude/hooks/` 가 금지 경로 · BOM · 금지 문법을 자동 검사한다. 막히면 규칙을 우회하지 말고 코드를 고친다.
