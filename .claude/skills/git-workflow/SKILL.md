---
name: git-workflow
description: 찹츄 저장소의 브랜치 구조(main · develop · logic · ui)와 커밋 · 머지 · PR 규칙. 커밋, 브랜치 생성 · 전환, develop 머지, PR 작성, 씬 · 프리팹 · .meta 를 커밋할 때 먼저 읽는다.
---

# Git 작업 규칙

## 브랜치 구조

```
main      ← 점검일 · 출시 빌드 때만 develop 을 머지 (항상 빌드 가능)
develop   ← 통합. logic · ui 가 PR 로 들어온다
 ├─ logic ← 로직 담당 전용 — Game/ · Network/ · Core/ 의 .cs
 └─ ui    ← UI 담당 전용 — Presentation/ · UI/ · DebugTools/ · 씬 · 프리팹 · 스프라이트
     └─ ui/<작업> · logic/<작업>   ← 필요할 때만 쓰는 짧은 브랜치. 끝나면 트랙 브랜치로 머지 후 삭제
```

| 규칙 | 내용 |
| --- | --- |
| 작업 시작 | 자기 트랙 브랜치(`logic` / `ui`)에서. 시작 전에 `develop` 을 머지해 최신으로 |
| develop 반영 | 트랙 → `develop` **PR**. 기능 한 덩어리가 끝날 때마다(최소 주 1회). 약속 파일(`GameEvents` · `IGameRequests` · `IGameState` · `PlayerProps`/`RoomProps`) PR 은 양쪽 리뷰 |
| 상대 트랙 받기 | 상대 PR 이 `develop` 에 머지되면 내 트랙에 `develop` 을 머지한다. `logic` ↔ `ui` 를 직접 머지하지 않는다 |
| 문서 | `docs/` · `CLAUDE.md` · `.claude/` 는 **모든 브랜치가 같은 내용을 공유**한다. 어느 트랙에서 고치든 그 브랜치에 커밋하고, 바로 `develop` 에 같은 내용을 반영한다(PR 없이). 상대 트랙은 `develop` 을 머지해 받는다 |
| 씬 | `.unity` · `.prefab` 은 `ui` 에서만 바꾼다 (`development-plan` 7절 1). `logic` 에서는 hook 이 막는다 |
| 개발 전용 씬 | `DebugLobbyScene`(개발용 즉시 입장) · `TempGameScene`(멀티 테스트용 임시 게임 씬)은 멀티 테스트에서 `LoadLevel` 로 넘어가야 해서 빌드 설정에 둔다. 그 밖의 연습 씬은 빌드 설정에 넣지 않는다 |

## 커밋

* 기능 단위로 커밋한다. 씬 변경과 코드 변경은 가능하면 커밋을 나눈다.
* 메시지: `type(scope): 요약` — type 은 `feat` · `fix` · `refactor` · `chore` · `docs`, 요약은 한국어.
  예: `refactor(deck): 덱을 GameServer 권위로 이전 — 드로우 요청 · 검증 · 셔플`
* 커밋되면 안 되는 것: ParrelSync 클론 디렉터리(`*_clone_*`), `Library/`, `Temp/`, 빌드 산출물.
* `.meta` 는 에셋과 한 몸이다. 새 파일은 Unity 가 만든 `.meta` 와 함께 커밋하고, 이동 · 이름 변경 때는 `.meta` 도 같이 옮긴다. `.meta` 를 손으로 만들거나 지우지 않는다(GUID 가 바뀌면 씬 · 프리팹 참조가 끊긴다).
  Claude 가 새 `.cs` 를 만든 경우 `.meta` 는 Unity 를 열어야 생긴다 — 커밋 전에 Unity 에서 한 번 열었는지 사용자에게 확인한다.
* 씬(`.unity`)과 프리팹(`.prefab`)은 병합 충돌이 어렵다. 같은 씬을 동시에 편집하지 않는다.
* revert · reset · 강제 푸시처럼 되돌리는 작업은 실행 전에 사용자 허락을 받는다.
