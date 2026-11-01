# 작업 기록

세션이 끝날 때 **한 것 · 결정 · 다음 할 일**을 남긴다. 최신 항목이 위. 이전 기록은 git 이력(`c67a110` 이전).

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
