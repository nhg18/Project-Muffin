# 찬반 리뷰 파이프라인

Claude Code 기본 스킬 `code-review` 가 문제 후보를 찾고, **반대파(공격)와 찬성파(방어)가 서로 반박**하며 검증하고,
끝까지 살아남은 지적만 자동으로 고친 뒤, 새 세션으로 다시 리뷰한다. 버그(`C-`)와 코드 품질(`Q-`)을 같이 본다.

기본 스킬은 「찾기 → 바로 고치기」를 한 번에 하지만(`code-review --fix` · `simplify`), 여기서는 둘을 떼어 그 사이에 찬반 토론을 둔다.

| 스킬의 부분 | 여기서 쓰는 곳 |
|---|---|
| 찾기 — `code-review` 의 버그 3관점 + 품질 4관점(재사용 · 단순화 · 효율 · 깊이, `simplify` 와 같은 관점) + `CLAUDE.md` 규칙 | ① 찾기 세션이 스킬을 `--fix` 없이 그대로 호출 |
| 고치기 — `code-review --fix` · `simplify` 의 적용 단계 | ⑥ 수정 담당. 스킬에는 「이 목록만 고치기」 호출이 없어서 그 규칙을 `prompts/fixer.md` 에 옮겨 적고, 토론에서 확정된 목록에만 적용 |

모든 단계는 `claude -p`로 띄우는 **새 세션**이라 코드를 쓴 세션의 맥락(자기 코드 편향)이 섞이지 않는다.
세션끼리는 파일(diff, 지적 목록, 토론 기록)로만 주고받는다.

| 모드 | 명령 | 대상 | push |
|---|---|---|---|
| 푸시 전 | `bash scripts/review/prepush.sh` (Claude Code: `/prepush`) | `origin/develop` 이후 바뀐 코드 | 통과하면 함 |
| 전체 | `bash scripts/review/prepush.sh --all [폴더]` | 이미 있는 코드 전체 (`Scripts/` 하위 폴더별) | 안 함 |

**문서 작업은 리뷰하지 않는다** — push 하는 커밋들이 `docs/` · `*.md` 만 바꿨으면 pre-push 훅이 리뷰 없이 통과시킨다. 코드 · 에셋 · 설정 파일이 하나라도 섞이면 평소대로 리뷰한다.

## 흐름

```
 ① 찾기        기본 스킬 code-review (고치기 없이) 가 후보를 넓게 찾음 (읽기 전용)
 ② 반대파      후보를 코드 + 프로젝트 문서로 확인해 문제(C-) · 설계 지적(Q-) 으로 올림. 스킬이 모르는 docs/ 규칙 위반을 더함
 ③ 찬성파      각 지적에 ACCEPT(인정) / REBUT(근거로 반박)
 ④ 반대파      반박된 항목에 WITHDRAW(철회) / MAINTAIN(새 근거로 유지)
 ⑤ 찬성파      유지된 항목에 다시 판정         … ④⑤ 최대 2번
 ⑥ 수정 담당    인정된 항목(C- · Q-)만 수정 → 자동 커밋 fix(review): …
 ⑦ 새 세션들이 방금 고친 diff 만 확인          … 최대 3라운드 (마지막은 확인만)
    — 고친 지적이 실제로 해결됐나 · 수정이 새 문제를 만들었나. 기존 코드를 처음부터 다시 보지 않는다

 결과
   PASS        → (푸시 전 모드) 승인 표시 후 git push — 수정 커밋이 생겼으면 push 하지 않음 (multi-test 로 확인 → 직접 push)
   UNRESOLVED  → 찬반이 끝까지 합의 못한 쟁점이 남음 (수정 뒤 재리뷰에서 다시 안 나와도) → 사람이 판단
   FIX_LIMIT   → 라운드 상한까지 고쳤는데 확정 문제가 남음 → 멈춤
```

판정 규칙
- 찬성파가 **반박하지 않은** 지적 = 확정 → 수정 대상
- 반대파가 **철회(WITHDRAW)한** 지적만 철회 — 판정 줄을 빠뜨린 지적은 유지로 본다
- 상한까지 공방이 이어진 지적 = 합의 안 됨 → 사람 몫
- 고친 지적 · 철회된 지적은 다음 라운드 세션에 「앞 라운드 기록」(`roundN/prior.md`)으로 넘긴다 — 철회된 지적을 새 근거 없이 다시 꺼내지 않게
- 반박·유지는 코드 위치나 문서 근거가 있어야 함 (추측은 근거로 인정 안 됨)
- **설계 지적(`Q-`)** — 버그가 아니라 코드가 잘 짜였는지. 스킬의 품질 관점 넷: 재사용(이미 있는 것을 다시 만듦) · 단순화(중복 상태 · 복붙 · 깊은 중첩 · 죽은 코드) · 효율(낭비되는 일) · 깊이(증상만 덮은 임시방편). 공방을 똑같이 거치고, 살아남으면 **`C-` 와 같이 고친다**. 동작을 바꾸지 않고 리뷰한 코드 근처에서 끝나는 정리만 대상이다. 문서 규칙 위반(계층 · `CODE_CONVENTION.md`)은 `Q-` 가 아니라 문제(`C-`)다
- `Q-` 는 **push 를 막지 않는다.** 아래는 고치지 않고 `roundN/quality.md` 권고로 사람에게 넘긴다 — 찬반이 합의하지 못한 것, 수정 담당이 건너뛴 것(동작이 바뀜 · 범위를 크게 벗어남), 마지막 라운드(확인만)에 확정된 것
- 출력 형식이 깨지면(찾기 결과가 스킬의 JSON 배열이 아님, 지적 블록도 `NO_FINDINGS` 도 없음, 판정 줄이 하나도 없음) 통과로 넘기지 않고 멈춤

리뷰 세션이 기준으로 삼는 문서 (기본 스킬은 `CLAUDE.md` 만 읽으므로, 나머지는 반대파 · 찬성파가 댄다)
- `CLAUDE.md`, `docs/systems/*.md` — 코드와 다르면 문서가 옳음. `미정`·`제안` 값은 지적하지 않음
- `docs/CODE_CONVENTION.md`, skill `network-rpc`
- `docs/DESIGN_INTENT.md` — 리뷰할 때 문제로 보지 말 것 (수치 밸런스, 테스트용 덱 등)
- `docs/worklog.md` — 「다음 할 일」·「확인 못 한 것」에 이미 적힌 문제는 지적하지 않음

## 기존 hook · skill 과의 관계

- 수정 세션도 Claude Code 세션이라 `.claude/hooks/`가 그대로 돈다. `.cs` BOM 자동 추가, `async void`·문자열 RPC·C# 10+ 문법 차단(`check-cs.sh`), 클론·Library·`.meta` 수정 차단(`guard-paths.sh`).
- hook은 Claude가 고친 파일에만 걸리므로, 사람이 고친 코드의 같은 문제는 리뷰가 잡는다.
- 수정 커밋이 생기면 CLAUDE.md 「바꾼 것은 직접 실행해서 확인」에 따라 skill `multi-test`로 확인한 뒤 push 한다. 스크립트는 이때 push 하지 않고, `/prepush`는 Unity 에디터가 열려 있으면 확인까지 이어서 돌린다.

## 설치 (팀원마다 한 번)

Git Bash에서:
```bash
bash scripts/review/install.sh
```
- pre-push hook 설치. **설치한 사람만** push가 리뷰 통과를 요구받는다 (git hook은 저장소에 같이 올라가지 않음)
- 기존 pre-push(Git LFS 등)는 지우지 않고 이어서 실행됨

`.claude/settings.json`의 `permissions.deny`가 Claude Code 작성 세션이 `--accept`, `SKIP_REVIEW`, `--no-verify`로 게이트를 우회하지 못하게 막는다.

## 사용

```bash
# 푸시 전 (작업 브랜치에서)
bash scripts/review/prepush.sh                       # 리뷰 → 수정 → 통과 시 push
bash scripts/review/prepush.sh origin feature/xxx
bash scripts/review/prepush.sh --no-push             # push는 직접
bash scripts/review/prepush.sh --accept              # UNRESOLVED를 사람이 확인하고 그대로 승인
SKIP_REVIEW=1 git push                               # 긴급 우회 (사람만)

# 이미 있는 코드 전체 (수정 커밋이 쌓이므로 develop에서 새 브랜치로 — main·develop에선 실행 거부)
git switch -c fix/review-full-scan origin/develop
bash scripts/review/prepush.sh --all                                        # Scripts 하위 폴더별로 전부
bash scripts/review/prepush.sh --all MuffinProject/Assets/Scripts/Network   # 한 폴더만
```

결과는 `.review/latest/`에 남는다.

| 파일 | 내용 |
|---|---|
| `report.md` | 최종 결과 요약 (`--all`이면 `summary.md`와 폴더별 `report.md`) |
| `roundN/candidates.md` | 기본 스킬 `code-review` 가 찾은 후보 원문 (JSON) |
| `roundN/con-review.md` | 반대파 지적 원문 — 후보 중 올린 것 · 올리지 않은 것과 이유 |
| `roundN/debate.md` | 찬반 공방 전체 |
| `roundN/fix-list.md` | 확정되어 수정 담당에게 넘긴 항목 (`C-` · `Q-`) |
| `roundN/fix.md` | 수정 담당이 무엇을 바꿨는지 · 무엇을 건너뛰었는지 |
| `roundN/unresolved.md` | 합의 안 된 쟁점 (`C-`) |
| `roundN/quality.md` | 설계 권고 — 고치지 않은 `Q-` (합의 못 함 · 수정 담당이 건너뜀 · 마지막 라운드). 고칠지는 사람이 정한다 |
| `roundN/prior.md` | 그 라운드 세션에 넘긴 앞 라운드 기록 (수정 담당에게 넘긴 지적 · 수정 결과 · 철회된 지적) |
| `withdrawn.md` | 라운드마다 철회된 지적 |

### 전체 리뷰(`--all`)를 처음 돌릴 때
- 폴더 하나(`Network` 등)로 먼저 돌려서 지적 품질을 확인한 뒤 전체로 넓히는 걸 추천
- 오탐이 많으면 `docs/DESIGN_INTENT.md`를 보강
- 수정 커밋은 폴더별로 따로 쌓이므로 PR에서 커밋 단위로 검토하고, 필요 없는 커밋은 되돌리면 됨
- 2라운드부터는 수정만 확인하므로, 한 번 돌리면 1라운드에서 올라온 것(문제 최대 10건 · 설계 지적 최대 5건)까지만 고친다. 설계 지적은 1라운드에서만 나온다. 더 찾으려면 고친 뒤 다시 돌린다

## 설정 (환경변수)

| 변수 | 기본값 | 설명 |
|---|---|---|
| `REVIEW_BASE` | `origin/develop` | 비교 기준 브랜치 |
| `REVIEW_PATHS` | `MuffinProject/Assets/Scripts` | 리뷰할 경로 (쉼표 구분) |
| `REVIEW_MAX_ROUNDS` | `3` | 리뷰 라운드 수 (마지막은 검증만) |
| `REVIEW_DEBATE_ROUNDS` | `2` | 재반박 주고받는 상한 |
| `REVIEW_LEVEL` | `high` | 찾기 단계에서 기본 스킬 `code-review` 에 주는 강도. `high` = 관점 8개를 한 세션이 돌고 스킬 자체 검증 없이 후보를 넓게 냄(검증은 찬반이 한다). 다른 강도는 관점 구성이 달라 품질 관점이 빠질 수 있다 — 바꾸면 `candidates.md` 로 확인 |
| `REVIEW_COMPILE_CMD` | 없음 | 수정 직후 실행할 확인 명령. 예: `bash .claude/skills/multi-test/run.sh rules` (Unity 에디터가 열려 있어야 함 — 아니면 실패로 보고 수정 세션의 변경을 stash에 보관) |
| `REVIEW_CON_MODEL` / `REVIEW_PRO_MODEL` / `REVIEW_FIX_MODEL` | 기본 모델 | 역할별 모델 (찾기는 반대파 모델을 쓴다). 찬반을 다른 모델로 두면 맹점이 덜 겹침 |

## 안전장치

- 찾기 · 리뷰 세션은 **읽기 전용**(Read·Grep·Glob만, 찾기는 여기에 스킬 `code-review` 호출만 더함). 리뷰하다가 코드를 고칠 수 없음 — 스킬의 `--fix` 를 붙여도 고칠 도구가 없다
- 수정 세션은 Bash 금지, 새 파일 생성 금지(만들면 중단). 커밋은 **리뷰 경로 안에서 바뀐 추적 파일만** — 같은 폴더를 Unity · 다른 작업도 쓰므로 `git add -A` · 전체 stash 를 하지 않는다. 경로 밖에서 바뀐 파일은 커밋하지 않고 경고만
- `REVIEW_COMPILE_CMD`가 실패하면 수정 세션이 바꾼 파일만 stash에 보관하고 중단 — 깨진 코드는 커밋되지 않음
- 수정 커밋은 아직 아무도 실행해 보지 않은 코드라 자동 push 하지 않음 (`--accept` 도 같음)
- 승인은 **커밋 해시 단위**. 리뷰 후 한 줄이라도 커밋하면 다시 리뷰해야 push됨
- 리뷰 경로(`REVIEW_PATHS`)에 커밋 안 된 변경이 있으면 시작하지 않음 (수정 세션의 변경과 섞이지 않게). 경로 밖 변경(Unity 폰트 아틀라스 저장 등)은 그대로 두고 돈다

## 주의

- **Windows**: Git Bash에서 실행. `claude`가 Git Bash PATH에 잡혀 있어야 함. 스크립트 줄바꿈은 `.gitattributes`로 LF 고정
- **시간·비용**: 라운드당 세션 3~8개를 순서대로 띄우므로 몇 분 걸림. `--all`은 폴더 수만큼 반복 — 찾기 세션은 코드 양에 따라 길어진다 (파일 28개짜리 폴더 하나에 약 11분)
- **기본 스킬 버전**: 찾기는 설치된 Claude Code 의 `code-review` 스킬을 그대로 쓴다. Claude Code 를 올리면 찾는 관점 · 결과 형식이 바뀔 수 있다 — 찾기 결과가 JSON 배열이 아니면 파이프라인이 멈추므로, 그때 `prompts/find.md` 를 맞춘다
- **재귀 방지**: 파이프라인은 `REVIEW_PIPELINE=1`을 설정함. 나중에 Stop hook 등을 만들 때 이 값이 있으면 건너뛰게 하기
- `claude` CLI 옵션은 버전에 따라 바뀔 수 있음. 동작이 이상하면 `claude --help`로 `--permission-mode`, `--allowedTools`, `--disallowedTools` 확인
