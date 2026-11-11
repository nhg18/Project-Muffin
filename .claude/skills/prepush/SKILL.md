---
name: prepush
description: 푸시 전 찬반 리뷰 파이프라인을 별도 세션으로 실행하고 결과를 보고한다. 사용자가 /prepush를 입력했을 때만 사용.
disable-model-invocation: true
---

# 푸시 전 찬반 리뷰

리뷰는 이 세션이 아니라 `claude -p`로 띄운 **별도 세션들**이 한다 (`scripts/review/README.md`).
이 세션은 실행하고 결과를 전달하는 역할만 한다. 리뷰 판단에 끼어들지 않는다.

## 순서
1. `git status -- MuffinProject/Assets/Scripts`로 리뷰 경로에 커밋 안 된 변경이 있는지 확인한다. 있으면 사용자에게 커밋할지 묻는다.
   (그 밖의 변경 — Unity 가 저장한 폰트 아틀라스 등 — 은 그대로 두어도 된다. 파이프라인이 건드리지도 커밋하지도 않는다.)
   - `/prepush --all [폴더]`는 이미 있는 코드 전체 리뷰다. main·develop이면 실행 전에 develop에서 작업 브랜치를 만들지 묻는다.
2. `bash scripts/review/prepush.sh $ARGUMENTS` 를 **백그라운드로** 실행한다 (여러 세션을 순서대로 띄워 몇 분 걸림).
3. 끝나면 `.review/latest/report.md`(`--all`이면 `summary.md`와 폴더별 `report.md`)를 읽고 결과를 사용자에게 그대로 전달한다.
   - 통과(PASS): 수정 커밋이 없으면 이미 push 됐다. 수정 커밋이 있었다면 push 하지 않은 상태다 — 각 라운드의 `fix.md`를 요약해 무엇이 바뀌었는지 알린다.
   - 합의 안 됨(UNRESOLVED): report.md 가 가리키는 라운드별 `unresolved.md`의 쟁점을 **찬반 양쪽 근거 그대로** 보여주고 사용자 판단을 기다린다.
   - 실패(FIX_LIMIT / FIX_FAILED) 나 형식 오류로 멈춤: 남은 지적 · 경고와 로그 위치를 알린다.
   - 설계 권고: report.md 에 「설계 권고」가 있으면 결과와 상관없이 `quality.md` 내용을 그대로 보여준다. 고칠지는 사용자가 고른다.
4. 수정 커밋(`fix(review): …`)이 생겼으면 skill `multi-test`의 「무엇을 언제」 표에 따라 바뀐 영역을 실행해서 확인한다.
   통과하면 `git push` (승인은 이미 그 커밋에 되어 있다). 에디터가 안 열려 있으면(종료 2) 확인하지 못했다고 말하고 push 하지 않는다.

## 하지 말 것
- 리뷰 지적을 이 세션이 "문제 아님"이라고 기각하거나 축소하지 않는다. 이 세션은 코드를 작성한 쪽이라 판단이 치우친다.
- `--accept`, `SKIP_REVIEW=1`, `git push --no-verify`를 스스로 실행하지 않는다. 사용자가 직접 한다.
- 합의 안 된 쟁점을 사용자 확인 없이 고치지 않는다.
- 설계 권고(`Q-`)를 사용자 확인 없이 고치지 않는다. 걸러서 일부만 전달하지도 않는다.
