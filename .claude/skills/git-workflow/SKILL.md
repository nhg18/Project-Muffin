---
name: git-workflow
description: 찹츄 저장소의 브랜치 · 커밋 · 태그 · PR 규칙. 커밋, 브랜치 생성 · 전환, PR 작성, release · hotfix · 버전 태그, 씬 · 프리팹 · .meta 를 커밋할 때 먼저 읽는다.
---

# Git 작업 규칙

git-flow 를 쓴다. 테스트 빌드(내부 점검 · 비공개 테스트)도 정식 출시와 똑같이 「배포」 로 친다 (PM 2026-11-21).

```
main               ← 배포한 빌드만. release/* · hotfix/* 만 머지한다. 머지할 때마다 v<버전> 태그
 └─ hotfix/<버전>   ← main 에서. 배포한 빌드의 급한 버그만 → main + develop
develop            ← 개발 중인 것을 모은다
 ├─ release/<버전>  ← develop 에서. 배포 전 빌드 확인 → main + develop
 └─ 작업 브랜치      ← develop 에서. feature/* · fix/* · docs/* → develop
```

| 브랜치 | 어디서 | 어디로 | 하는 일 |
| --- | --- | --- | --- |
| `main` | — | — | 배포한 빌드. 직접 커밋하지 않고, develop 도 바로 머지하지 않는다 |
| `develop` | — | — | 개발 중인 것을 모은다 |
| `feature/<기능>` | `develop` | `develop` | 기능 개발 |
| `fix/<내용>` | `develop` | `develop` | 버그 수정 |
| `docs/<내용>` | `develop` | `develop` | 문서(`docs/` · `CLAUDE.md` · `.claude/`) — 우리만의 예외 |
| `release/<버전>` | `develop` | `main` → `develop` | 배포 전 빌드 확인. 들어가는 것은 버전 커밋과 확인 중 나온 버그 수정뿐 — 새 기능은 develop 에 넣어 다음 release 로 |
| `hotfix/<버전>` | `main` | `main` → `develop` | 배포한 빌드의 급한 버그만, develop 의 다른 작업 없이 내보낼 때. 그때 열린 release 가 있으면 develop 대신 그 release 로 |

* 이름은 영문 소문자와 하이픈 (예: `feature/counter-chain`). 버전 브랜치는 숫자 세 자리 (예: `release/0.2.0` · `hotfix/1.0.1`). 한 브랜치 = 한 가지 일. 오래 쓰는 개인 브랜치는 두지 않는다.
* 작업 시작 전에 출발 브랜치(보통 `develop`, hotfix 는 `main`)를 받아 최신으로 만든다.
* 모든 브랜치는 PR 로 들어간다. `main` · `release/*` · `develop` 머지, `release/*` · `hotfix/*` 만들기, 버전 태그는 PM 만.
* 머지가 끝난 브랜치는 원격 · 로컬 모두 지운다. `main` · `develop` 은 지우지 않는다.

## release · hotfix 순서

1. PM 이 `release/<버전>` 을 develop 에서 딴다 (hotfix 는 `hotfix/<버전>` 을 main 에서). 버전은 `docs/versioning.md` 표로 정하고, 이 브랜치에서 버전 커밋을 따로 한다.
2. 이 브랜치로 빌드해 확인한다. 확인 중 나온 버그는 이 브랜치에서 `fix/<내용>` 을 따서 이 브랜치로 PR 한다 — develop 에서 따면 이번 빌드에 안 들어간다.
3. 이 브랜치 → `main` PR 을 **Create a merge commit** 으로 머지한다. Squash · Rebase 는 쓰지 않는다 — develop 으로 되돌릴 때 같은 수정이 두 번 들어와 충돌난다.
4. `main` 의 그 머지 커밋에 태그를 단다: `git tag -a v<버전> -m "<빌드 이름>"` → `git push origin v<버전>`. 배포 빌드는 이 태그 커밋으로 만든다. `v*` 태그를 push 하면 CI 가 서명된 Android AAB 를 만든다 (`.github/workflows/unity-ci.yml`).
5. 같은 브랜치 → `develop` PR 로 되돌린다. 빼먹으면 이번에 고친 버그가 다음 배포에 다시 나온다.
6. 브랜치를 지운다.

* 태그는 옮기거나 지우지 않는다. 태그 빌드에 문제가 있으면 버전을 올려 새 태그를 단다.
* 태그 이름: 배포 = `v<버전>` (예: `v0.2.0`), 접은 작업 = `archive/<이름>`.
* 찬반 리뷰(`/prepush`)는 develop 과 비교한다. release · hotfix 와 거기서 딴 `fix/*` 는 비교 기준을 바꿔 돌린다 — `REVIEW_BASE=origin/main` 또는 `REVIEW_BASE=origin/release/<버전>`.

## CI · CD (`.github/workflows/unity-ci.yml`)

Unity 빌드는 오래 걸리고(무료 러너로 한 번에 수십 분) 라이선스 좌석을 쓴다. 그래서 **내보낼 코드가 지나가는 길에만** 둔다 (2026-11-22 PM 결정).

| 언제 | 무엇 | 왜 |
| --- | --- | --- |
| PR → `release/*` · `hotfix/*` · `main` | Android 빌드 검사. **통과해야 머지**(필수 검사) | 내보낼 브랜치에 깨진 빌드가 들어가지 않게 |
| push → `release/*` · `hotfix/*` | 확인용 APK 빌드 → 아티팩트 | release · hotfix 확인 단계(위 순서 2)에서 기기에 깔아 본다 |
| `v*` 태그 push | 서명된 AAB (CD) → 아티팩트. Play Console 내부 테스트 트랙 올리기는 나중에 붙인다 | 배포 빌드 = 태그 커밋 (`docs/versioning.md` 「태그」) |
| `develop` | 하루 한 번 밤 빌드 (schedule) — 그날 바뀐 것이 없으면 건너뛴다 | feature PR 마다 돌리면 비싸다. 깨진 날을 하루 안에 안다 |
| 수동 | `workflow_dispatch` | 아무 브랜치나 필요할 때 |

* `docs/**` · `*.md` 만 바꾼 것은 돌지 않는다.
* feature · fix · docs PR(→ develop)에는 Unity 빌드를 돌리지 않는다 — 대신 `/prepush` 리뷰와 skill `multi-test` 가 막는다.
* **지금 파일과 다르다** — 지금 yaml 은 원격에 없는 `test_build` 브랜치로 가는 PR 에만 돌아, PR 검사가 한 번도 돈 적이 없다. 위 표대로 고치는 것은 따로 브랜치를 따서 `/prepush` 를 거쳐 넣는다 (2026-11-22 기준 미반영).

## 그 밖의 규칙

* 접은 작업은 브랜치 대신 `archive/<이름>` 태그로 원격에 남기고 브랜치는 지운다 (예: `archive/upgrade-unity6`).
* 같은 씬 · 프리팹을 두 사람이 동시에 고치지 않는다.
* 커밋 메시지: `type(scope): 한국어 요약` — `feat` · `fix` · `refactor` · `chore` · `docs`. 기능 단위로 커밋한다.
* `.meta` 는 에셋과 같이 커밋 · 이동한다. 손으로 만들거나 지우지 않는다. 새 `.cs` · 에셋은 Unity 가 `.meta` 를 만든 뒤 함께 커밋한다.
* push 전에는 별도 세션들의 찬반 리뷰를 거친다 → `/prepush` (`scripts/review/README.md`). **문서 작업은 제외** — push 하는 커밋들이 `docs/` · `*.md` 만 바꿨으면 리뷰 없이 push 한다 (`.githooks/pre-push` 가 알아서 통과시킨다). 리뷰 지적을 작성 세션이 스스로 기각하지 않는다. 리뷰가 고친 커밋은 skill `multi-test` 로 확인한 뒤 push 한다.
* 커밋하지 않는 것: `*_clone_*/` · `Library/` · `Temp/` · 빌드 산출물.
* revert · reset · 강제 푸시는 실행 전에 허락을 받는다.
