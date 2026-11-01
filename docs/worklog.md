# 작업 기록

세션이 끝날 때 **한 것 · 결정 · 다음 할 일**을 남긴다. 최신 항목이 위. 이전 기록은 git 이력(`c67a110` 이전).

---

## 2026-11-01 · 지침 · 문서를 "되어야 할 상태" 기준으로

### 결정
- 코딩은 AI 가 맡고, 사람은 결과물을 고치고 조합한다. `CLAUDE.md` 는 원칙만, 코드 규칙은 `docs/CODE_CONVENTION.md` 한 곳에.
- 모든 문서는 현재 코드가 아니라 되어야 할 상태를 적는다. 코드가 다르면 코드를 고친다 (`GDD_GUIDE` 4-5).
- 플랫폼: PC / Android / iOS.
- 아키텍처: 규칙은 순수 C# (`Scripts/Game/`, 별도 asmdef) 으로 방장만 실행 → 일어난 일 목록 전파 → 클라이언트 연출 큐. 카드 효과는 `[SerializeReference]` 조립식 (`CODE_CONVENTION` 9절 · `00` · `09` 6.1).
- `_Project/` 로 폴더 모으기는 보류. 방장 이주 복원 방식은 보류 유지 — 상태를 한 객체에 모으는 것까지만 지금 한다.

### 한 것
- `CLAUDE.md`: Unity 결과 보고 규칙 · Unity 작업 방식 섹션 삭제 → `작업 방식` 5줄로 교체.
- `CODE_CONVENTION.md` 전면 개정 (8절): 이벤트 이름 통일(`OnXxx` · `HandleXxx`), 컴포넌트 단위, 참조 누락 LogError, 문자열 키 상수화, 로그 접두사 등.
- 기획서에서 구현 위치(클래스 · 메서드 이름 · "현재 구현") 제거: 01 · 08 · 10 · 14 · 15 · 16.
- "방 나가기 → 들어온 씬으로 복귀" 규칙 삭제 (디버그 로비 테스트용이었음). 나가기는 항상 로비.
- `store-release.md` 를 Unity 6000.3.27f1 기준으로 (결정 7 완료).

### 다음 할 일
- [ ] 코드를 `CODE_CONVENTION.md` 에 맞추기 — 요청 이벤트 7개 `On` 접두사, 로그 접두사 · 한국어 7곳, `DebugScript:62` `var`, `RoomPresenter` 문구 모으기, `[SerializeField]` 참조 누락 LogError (11 파일)
- [ ] 방 나가기는 항상 `LobbyScene` — `SceneFlow.ReturnSceneAfterRoom` 과 관련 코드 삭제, `docs/diagrams/ui-flow.workflow.json` 의 "들어온 씬" 흐름도 고치고 도식 다시 생성
- [ ] 테스트 절차 skill 만들기 (Unity CLI 로 멀티 실행이 되는지 먼저 확인)
- [ ] `Scripts old/Editor/GameViewSizeSetup.cs` 를 `Scripts/Editor/` 로 옮기기 (15-screen 등록 메뉴)
- [ ] `Scripts/Game/` + `Chapchu.Game.asmdef` 는 규칙 코드를 처음 만들 때 함께 만든다
- [ ] `Scripts old/` 삭제 — 아직 참조가 남아 있다: `CardData` (`Cards/*.asset` 4개), `DeckData` (`Deck_*.asset` 2개), 카드 · 손패 프리팹 5개, `GameScene` · `TempGameScene`. 새 카드 정의로 옮기고 게임 씬을 다시 만든 뒤 지운다

---

## 2026-10-31 · 지침 정리

### 결정
- `develop` 머지는 PM.
- 만드는 순서: HTML 시안 → Unity.
- 브랜치: `main` ← `develop` ← `feature/<기능>`.
- Unity 6000.3.27f1 로 업그레이드. 타이틀 · 로비 · 방 외 스크립트는 `Assets/Scripts old/` 로 옮김(참고만).

### 한 것
- `CLAUDE.md` · skill · hook · `docs/` 를 위 기준으로 줄임. 끝난 플랜 · 트랙 문서 삭제.

### 다음 할 일
- [ ] `logic` → `develop` PR, 개인 브랜치 남은 작업 확인 후 머지, 옛 브랜치 삭제
- [ ] 팀원 PC: Unity 6000.3.27f1 설치, hook 동작 확인
- [ ] 인게임 HTML 프로토타입 → Unity
- [ ] 타이틀 연결 중 · 연결 실패 상태 (`12-title-ui` 6절 · 9-3 · 10-5) 미구현
- [ ] 테스트 빌드 전 Build Settings 시작 씬을 `TitleScene` 으로 (지금 `DebugLobbyScene` 이 0번)
