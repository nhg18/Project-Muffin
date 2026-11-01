---
name: git-workflow
description: 찹츄 저장소의 브랜치 · 커밋 · PR 규칙. 커밋, 브랜치 생성 · 전환, PR 작성, 씬 · 프리팹 · .meta 를 커밋할 때 먼저 읽는다.
---

# Git 작업 규칙

```
main     ← 점검 · 출시 빌드 때만 develop 을 머지
develop  ← 통합. PR 로만 들어온다. 머지는 PM 만
 └─ 작업 브랜치  ← develop 에서 만들고, 끝나면 PR. 머지 후 원격 · 로컬 모두 삭제
```

* 작업 브랜치 이름: `feature/<기능>` · `fix/<내용>` · `docs/<내용>` — 영문 소문자와 하이픈 (예: `feature/counter-chain`). 한 브랜치 = 한 가지 일. 오래 쓰는 개인 브랜치는 두지 않는다.
* 작업 시작 전에 `develop` 을 받아 최신으로 만든다.
* 접은 작업은 브랜치 대신 `archive/<이름>` 태그로 원격에 남기고 브랜치는 지운다 (예: `archive/upgrade-unity6`).
* 문서(`docs/` · `CLAUDE.md` · `.claude/`)도 같은 흐름으로 PR 한다.
* 같은 씬 · 프리팹을 두 사람이 동시에 고치지 않는다.
* 커밋 메시지: `type(scope): 한국어 요약` — `feat` · `fix` · `refactor` · `chore` · `docs`. 기능 단위로 커밋한다.
* `.meta` 는 에셋과 같이 커밋 · 이동한다. 손으로 만들거나 지우지 않는다. 새 `.cs` · 에셋은 Unity 가 `.meta` 를 만든 뒤 함께 커밋한다.
* 커밋하지 않는 것: `*_clone_*/` · `Library/` · `Temp/` · 빌드 산출물.
* revert · reset · 강제 푸시는 실행 전에 허락을 받는다.
