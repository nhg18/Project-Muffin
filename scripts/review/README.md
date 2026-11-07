# 찬반 리뷰 파이프라인

**반대파(공격)와 찬성파(방어)가 서로 반박**하며 코드를 검증하고,
끝까지 살아남은 지적만 자동으로 고친 뒤, 새 세션으로 다시 리뷰한다.

모든 단계는 `claude -p`로 띄우는 **새 세션**이라 코드를 쓴 세션의 맥락(자기 코드 편향)이 섞이지 않는다.
세션끼리는 파일(diff, 지적 목록, 토론 기록)로만 주고받는다.

두 가지 모드가 있다.

| 모드 | 명령 | 대상 | push |
|---|---|---|---|
| 푸시 전 | `bash scripts/review/prepush.sh` | `origin/main` 이후 바뀐 코드 | 통과하면 함 |
| 전체 | `bash scripts/review/prepush.sh --all` | 이미 있는 코드 전체 (폴더별) | 안 함 |

## 흐름

```
 ① 반대파      코드 + 프로젝트 문서만 보고 문제 찾기 (읽기 전용)
 ② 찬성파      각 지적에 ACCEPT(인정) / REBUT(근거로 반박)
 ③ 반대파      반박된 항목에 WITHDRAW(철회) / MAINTAIN(새 근거로 유지)
 ④ 찬성파      유지된 항목에 다시 판정         … ③④ 최대 2번
 ⑤ 수정 담당    인정된 항목만 최소 수정 → 자동 커밋
 ⑥ 새 세션들이 처음부터 재리뷰                … 최대 3라운드 (마지막은 검증만)

 결과
   PASS        → (푸시 전 모드) 승인 표시 후 git push
   UNRESOLVED  → 찬반이 끝까지 합의 못한 쟁점만 남음 → 사람이 판단
   FIX_LIMIT   → 라운드 상한까지 고쳤는데 확정 문제가 남음 → 멈춤
```

판정 규칙
- 찬성파가 **반박하지 않은** 지적 = 확정 → 수정 대상
- 반대파가 **유지하지 않은** 지적 = 철회
- 상한까지 공방이 이어진 지적 = 합의 안 됨 → 사람 몫
- 반박·유지는 코드 위치나 문서(`CLAUDE.md`, `docs/systems/`) 근거가 있어야 함

리뷰 세션이 참고하는 문서
- `CLAUDE.md` 10~13절 (환경·네트워크 권한·Unity 작업·인코딩)
- `docs/systems/*.md` — 코드와 다르면 문서가 옳음. `미정`·`제안` 값은 지적하지 않음
- `docs/CODE_CONVENTION.md`
- `docs/DESIGN_INTENT.md` — 리뷰할 때 문제로 보지 말 것 (수치 밸런스, 의도된 난장판 효과 등)
- `docs/refactoring-plan.md` — 이미 알려진 문제는 지적하지 않음

## 설치 (팀원마다 한 번)

Git Bash에서:
```bash
bash scripts/review/install.sh
```
- pre-push hook 설치. 설치한 사람만 push가 리뷰 통과를 요구받는다 (git hook은 저장소에 같이 올라가지 않음)
- 기존 pre-push(Git LFS 등)는 지우지 않고 이어서 실행됨
- `.gitignore`에 `.review/` 추가

`.claude/settings.json`의 차단 규칙은 저장소에 포함되어 있다. Claude Code 작성 세션이 `--accept`, `SKIP_REVIEW`, `--no-verify`로 게이트를 우회하지 못하게 막는다.

## 사용

```bash
# 푸시 전
bash scripts/review/prepush.sh                 # 리뷰 → 수정 → 통과 시 push
bash scripts/review/prepush.sh origin my-branch
bash scripts/review/prepush.sh --no-push       # push는 직접
bash scripts/review/prepush.sh --accept        # UNRESOLVED를 사람이 확인하고 그대로 승인
SKIP_REVIEW=1 git push                         # 긴급 우회 (사람만)

# 이미 있는 코드 전체
git switch -c review/full-scan                 # 수정 커밋이 쌓이므로 작업 브랜치에서 (main에선 실행 거부)
bash scripts/review/prepush.sh --all                                     # Scripts 하위 폴더별로 전부
bash scripts/review/prepush.sh --all MuffinProject/Assets/Scripts/Network  # 특정 폴더만
```

Claude Code 안에서는 `/prepush`로 실행. 이 스킬은 실행·결과 전달만 하고 리뷰 판단에는 끼지 않는다.

결과는 `.review/latest/`에 남는다.

| 파일 | 내용 |
|---|---|
| `report.md` | 최종 결과 요약 (`--all`이면 `summary.md`와 폴더별 `report.md`) |
| `roundN/con.md` | 반대파 지적 원문 |
| `roundN/debate.md` | 찬반 공방 전체 |
| `roundN/fix-list.md` | 확정되어 수정한 항목 |
| `roundN/fix.md` | 수정 담당이 무엇을 바꿨는지 |
| `roundN/unresolved.md` | 합의 안 된 쟁점 |

### 전체 리뷰(`--all`)를 처음 돌릴 때
- 폴더 하나(`Network` 등)로 먼저 돌려서 지적 품질을 확인한 뒤 전체로 넓히는 걸 추천
- 지적이 많이 나오면 `docs/DESIGN_INTENT.md`와 `docs/refactoring-plan.md`를 보강해 오탐을 줄이기
- 수정 커밋은 폴더별로 따로 쌓이므로, PR에서 커밋 단위로 검토하거나 필요 없는 커밋은 되돌리면 됨

## 설정 (환경변수)

| 변수 | 기본값 | 설명 |
|---|---|---|
| `REVIEW_BASE` | `origin/main` | 비교 기준 브랜치 |
| `REVIEW_PATHS` | `MuffinProject/Assets/Scripts` | 리뷰할 경로 (쉼표 구분) |
| `REVIEW_MAX_ROUNDS` | `3` | 리뷰 라운드 수 (마지막은 검증만) |
| `REVIEW_DEBATE_ROUNDS` | `2` | 재반박 주고받는 상한 |
| `REVIEW_PROTECTED` | ProjectSettings, Packages, 서드파티, docs, CLAUDE.md, .meta | 수정 세션이 건드리면 중단할 경로 (정규식) |
| `REVIEW_COMPILE_CMD` | 없음 | 수정 후 실행할 컴파일 체크 명령 |
| `REVIEW_CON_MODEL` / `REVIEW_PRO_MODEL` / `REVIEW_FIX_MODEL` | 기본 모델 | 역할별 모델. 찬반을 다른 모델로 두면 맹점이 덜 겹침 |

## 안전장치

- 리뷰 세션은 **읽기 전용**(Read·Grep·Glob만). 리뷰하다가 코드를 고칠 수 없음
- 수정 세션은 Bash 금지, 새 파일 생성 금지. 보호 경로를 건드리면 변경을 `git stash`에 보관하고 중단
- 컴파일 체크(`REVIEW_COMPILE_CMD`)가 실패해도 변경을 stash에 보관하고 중단 — 깨진 코드는 커밋되지 않음
- 승인은 **커밋 해시 단위**. 리뷰 후 한 줄이라도 커밋하면 다시 리뷰해야 push됨
- 커밋 안 된 변경이 있으면 시작하지 않음
- `--all`은 main 브랜치에서 실행을 거부함

## 주의

- **Windows**: Git Bash에서 실행. `claude`가 Git Bash PATH에 잡혀 있어야 함. 스크립트 줄바꿈은 `.gitattributes`로 LF 고정
- **시간·비용**: 라운드당 세션 2~7개를 순서대로 띄우므로 몇 분 걸림. `--all`은 폴더 수만큼 반복되니 훨씬 오래 걸림
- **재귀 방지**: 파이프라인은 `REVIEW_PIPELINE=1`을 설정함. 나중에 Stop hook 등을 만들 때 이 값이 있으면 건너뛰게 하기
- **Unity 컴파일 체크**: 배치모드 컴파일은 느리고 에디터가 열려 있으면 실패함. 처음엔 비워두는 걸 추천
- `claude` CLI 옵션은 버전에 따라 바뀔 수 있음. 동작이 이상하면 `claude --help`로 `--permission-mode`, `--allowedTools`, `--disallowedTools` 확인
