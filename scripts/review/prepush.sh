#!/usr/bin/env bash
# =============================================================================
# 찬반 리뷰 → 자동 수정 파이프라인
#
#   반대파(공격) 세션 → 찬성파(방어) 세션 → 재반박 … → 살아남은 지적만 수정 세션
#   → 수정 커밋 → 새 세션으로 재리뷰 → 통과하면 승인 표시 후 push
#
# 모든 단계는 `claude -p`로 띄운 "새 세션"이라 작성 세션의 대화 맥락이 섞이지 않는다.
# 세션끼리는 파일(diff, 지적 목록, 토론 기록)로만 주고받는다.
#
# 사용:
#   bash scripts/review/prepush.sh              # 변경분 리뷰 → 수정 → 통과 시 git push
#   bash scripts/review/prepush.sh --no-push    # 리뷰·수정·승인까지만
#   bash scripts/review/prepush.sh --accept     # '합의 안 된 쟁점만 남음'을 사람이 확인 후 승인
#   bash scripts/review/prepush.sh --all        # 이미 있는 코드 전체를 폴더별로 리뷰·수정 (push 안 함)
#   bash scripts/review/prepush.sh --all MuffinProject/Assets/Scripts/Network   # 특정 폴더만
#   그 밖의 인자는 git push에 그대로 넘어감 (예: origin feature/x)
# =============================================================================
set -uo pipefail

# ----- 설정 (환경변수로 덮어쓰기) -------------------------------------------
BASE_BRANCH="${REVIEW_BASE:-origin/develop}"                    # 비교 기준 브랜치 (작업 브랜치는 develop으로 PR)
REVIEW_PATHS="${REVIEW_PATHS:-MuffinProject/Assets/Scripts}"    # 리뷰할 경로 (쉼표 구분)
MAX_ROUNDS="${REVIEW_MAX_ROUNDS:-3}"                            # 리뷰 라운드 수 (마지막 라운드는 검증만)
DEBATE_ROUNDS="${REVIEW_DEBATE_ROUNDS:-2}"                      # 재반박 주고받는 상한
DESIGN_INTENT="${DESIGN_INTENT:-docs/DESIGN_INTENT.md}"         # 리뷰용 설계 의도 문서
PROTECTED_RE="${REVIEW_PROTECTED:-^(MuffinProject/(ProjectSettings|Packages|Library|Temp)/|MuffinProject/Assets/(Photon|GPM|ParrelSync|Plugins|TextMesh Pro)/|.*_clone_.*/|docs/|\.claude/|CLAUDE\.md$|.*\.meta$)}"
COMPILE_CMD="${REVIEW_COMPILE_CMD:-}"                           # 수정 후 실행할 컴파일 체크 (선택)
CLAUDE_BIN="${CLAUDE_BIN:-claude}"
CON_MODEL="${REVIEW_CON_MODEL:-}"                               # 반대파 모델 (비우면 기본값)
PRO_MODEL="${REVIEW_PRO_MODEL:-}"                               # 찬성파 모델
FIX_MODEL="${REVIEW_FIX_MODEL:-}"                               # 수정 담당 모델
# -----------------------------------------------------------------------------

log()  { printf '\033[1;36m[review]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[review]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[review]\033[0m %s\n' "$*" >&2; exit 1; }

# ----- 인자 ------------------------------------------------------------------
ACCEPT=0; PUSH=1; ALL=0; PUSH_ARGS=(); ALL_TARGETS=()
for a in "$@"; do
  case "$a" in
    --accept)  ACCEPT=1 ;;
    --no-push) PUSH=0 ;;
    --all)     ALL=1; PUSH=0 ;;
    *)         if (( ALL )); then ALL_TARGETS+=("$a"); else PUSH_ARGS+=("$a"); fi ;;
  esac
done

# ----- 위치 ------------------------------------------------------------------
ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || die "git 저장소 안에서 실행하세요."
cd "$ROOT" || die "저장소 루트로 이동 실패"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROMPTS="${SCRIPT_DIR#"$ROOT"/}/prompts"
STATE_DIR=".review"
mkdir -p "$STATE_DIR"

approve() { git rev-parse HEAD > "$STATE_DIR/approved"; log "승인 표시: $(git rev-parse --short HEAD)"; }
do_push() {
  if (( PUSH )); then
    log "git push ${PUSH_ARGS[*]:-}"
    git push "${PUSH_ARGS[@]}"
  else
    log "push는 하지 않았습니다."
  fi
}

# ----- --accept: 사람이 확인한 뒤 승인 ---------------------------------------
if (( ACCEPT )); then
  read -r last_sha last_status < "$STATE_DIR/last-status" 2>/dev/null || die "이전 리뷰 결과가 없습니다."
  [[ "$last_sha" == "$(git rev-parse HEAD)" ]] || die "리뷰 이후 커밋이 바뀌었습니다. 다시 리뷰하세요."
  [[ "$last_status" == "UNRESOLVED" ]] || die "--accept는 '합의 안 된 쟁점만 남음' 상태에서만 쓸 수 있습니다 (현재: $last_status)."
  warn "합의 안 된 쟁점을 사람이 확인하고 그대로 승인합니다."
  approve; do_push; exit $?
fi

# ----- 사전 점검 -------------------------------------------------------------
command -v "$CLAUDE_BIN" >/dev/null 2>&1 || die "'$CLAUDE_BIN' 명령을 찾을 수 없습니다."
[[ -z "$(git status --porcelain)" ]] || die "커밋 안 된 변경이 있습니다. 커밋하거나 stash한 뒤 실행하세요."
[[ -f "$DESIGN_INTENT" ]] || warn "$DESIGN_INTENT 가 없습니다. 의도된 동작이 버그로 보고될 수 있어요."
export REVIEW_PIPELINE=1   # 프로젝트 hook이 이 값을 보고 재귀 실행을 피할 수 있게

IFS=',' read -r -a PATHS_ARR <<< "$REVIEW_PATHS"

if (( ALL )); then
  # 전체 리뷰: 빈 트리와 비교하면 기존 코드 전체가 '추가된 코드'로 나온다
  BASE="$(git hash-object -t tree /dev/null)"
  BRANCH="$(git rev-parse --abbrev-ref HEAD)"
  [[ "$BRANCH" != "main" && "$BRANCH" != "develop" && "$BRANCH" != "HEAD" ]] \
    || die "--all은 수정 커밋을 여러 개 만듭니다. develop에서 작업 브랜치를 만든 뒤 실행하세요 (예: git switch -c fix/review-full-scan origin/develop)."
  if (( ${#ALL_TARGETS[@]} == 0 )); then
    # 대상을 안 주면 리뷰 경로의 하위 폴더 단위로 나눠서 돈다 (한 세션이 너무 많은 코드를 보지 않게)
    for p in "${PATHS_ARR[@]}"; do
      while IFS= read -r d; do ALL_TARGETS+=("$d"); done < <(find "$p" -mindepth 1 -maxdepth 1 -type d | sort)
      # 하위 폴더 없이 바로 있는 .cs 파일들은 폴더 자체를 대상으로
      if compgen -G "$p/*.cs" > /dev/null; then ALL_TARGETS+=("$p/*.cs"); fi
    done
  fi
  SCOPE="이 범위에 있는 기존 코드 전체가 리뷰 대상이다. 단, 설계 의도 문서의 '알려진 한계'와 docs/worklog.md의 '다음 할 일'·'확인 못 한 것'에 이미 적힌 문제는 보고하지 않는다."
else
  if ! git rev-parse --verify -q "$BASE_BRANCH" >/dev/null; then
    if git rev-parse --verify -q '@{u}' >/dev/null; then BASE_BRANCH='@{u}'
    else die "기준 브랜치 '$BASE_BRANCH'가 없습니다. REVIEW_BASE=브랜치 로 지정하세요."; fi
  fi
  BASE="$(git merge-base "$BASE_BRANCH" HEAD)" || die "merge-base 계산 실패"
  SCOPE="이번 diff가 새로 만들거나 건드린 문제만 보고한다. diff와 무관한 기존 코드의 문제는 보고하지 않는다."
fi

RUN_ROOT="$STATE_DIR/runs/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$RUN_ROOT"

# ----- 도우미 ----------------------------------------------------------------
# 템플릿의 {{KEY}}를 값으로 바꿔 작업 지시 파일을 만든다
render() {
  local tpl="$1" out="$2"; shift 2
  cp "$tpl" "$out"
  local kv k v
  for kv in "$@"; do
    k="${kv%%=*}"; v="${kv#*=}"
    sed "s|{{$k}}|$v|g" "$out" > "$out.tmp" && mv "$out.tmp" "$out"
  done
}

# 새 세션으로 claude 실행. mode: read(읽기 전용) | edit(파일 수정 허용)
run_claude() {
  local label="$1" task="$2" out="$3" mode="$4" model="$5"
  local args=(-p "$task 파일을 읽고 그 지시를 그대로 수행해. 최종 결과 본문만 출력해.")
  if [[ "$mode" == "edit" ]]; then
    args+=(--permission-mode acceptEdits --allowedTools "Read,Grep,Glob,Edit,Write" --disallowedTools "Bash")
  else
    args+=(--permission-mode dontAsk --allowedTools "Read,Grep,Glob" --disallowedTools "Bash,Edit,Write")
  fi
  [[ -n "$model" ]] && args+=(--model "$model")
  log "  · $label"
  if ! "$CLAUDE_BIN" "${args[@]}" > "$out" 2> "$out.err" < /dev/null; then
    die "$label 세션 실패 (로그: $out.err)"
  fi
  [[ -s "$out" ]] || die "$label 세션 출력이 비었습니다 (로그: $out.err)"
}

clean_list() { printf '%s\n' "$@" | tr ' ' '\n' | sed '/^$/d' | sort -u; }
minus() { comm -23 <(clean_list "$1") <(clean_list "$2"); }   # $1 - $2
inter() { comm -12 <(clean_list "$1") <(clean_list "$2"); }   # $1 ∩ $2
count() { clean_list "$1" | sed '/^$/d' | wc -l | tr -d ' '; }
oneline() { clean_list "$1" | tr '\n' ' ' | sed 's/ $//'; }

finding_ids() { grep -oE '^### C-[0-9]+' "$1" | sed 's/^### //' | sort -u; }

# 판정 줄(VERDICT C-01 ACCEPT)에서 특정 판정의 ID만 뽑는다
verdicts() {
  sed -E 's/[*`>]//g' "$1" \
    | grep -E '^[[:space:]-]*VERDICT[[:space:]]+C-[0-9]+[[:space:]]+[A-Z]+' \
    | sed -E 's/^[[:space:]-]*VERDICT[[:space:]]+(C-[0-9]+)[[:space:]]+([A-Z]+).*/\1 \2/' \
    | awk -v w="$2" '$2==w {print $1}' | sort -u
}

# 지적 목록 파일에서 특정 ID 블록만 뽑는다
extract_blocks() {
  awk -v ids=" $(oneline "$2") " '
    /^### C-[0-9]+/ { id=$2; keep = (index(ids, " " id " ") > 0) }
    /^### / && !/^### C-[0-9]+/ { keep = 0 }
    keep { print }' "$1"
}

# ----- 리뷰 라운드: 반대파 → 찬성파 → 재반박 --------------------------------
# 입력: RUN, TARGET_PATHS / 결과: STATUS(PASS|ISSUES), ACCEPTED, UNRESOLVED
review_round() {
  local r="$1" dir="$RUN/round$r"
  mkdir -p "$dir"
  ACCEPTED=""; UNRESOLVED=""; STATUS="ISSUES"

  git diff "$BASE" HEAD -- "${TARGET_PATHS[@]}" ':(exclude,glob)**/*.meta' > "$dir/diff.patch"
  if [[ ! -s "$dir/diff.patch" ]]; then
    log "라운드 $r: 리뷰할 코드 없음"
    STATUS="PASS"; return
  fi
  log "라운드 $r: $(grep -c '^+++ ' "$dir/diff.patch")개 파일 리뷰"

  local transcript="$dir/debate.md"
  printf '# 라운드 %s 토론 기록\n\n' "$r" > "$transcript"
  local common=("DIFF=$dir/diff.patch" "INTENT=$DESIGN_INTENT" "TRANSCRIPT=$transcript" "SCOPE=$SCOPE")

  # 1) 반대파: 문제 찾기
  render "$PROMPTS/con.md" "$dir/con.task.md" "${common[@]}"
  run_claude "반대파 리뷰" "$dir/con.task.md" "$dir/con.md" read "$CON_MODEL"
  local all; all="$(finding_ids "$dir/con.md")"
  if [[ -z "$all" ]]; then
    grep -q 'NO_FINDINGS' "$dir/con.md" || warn "반대파 출력에서 지적 형식을 찾지 못해 '문제 없음'으로 처리합니다: $dir/con.md"
    log "라운드 $r: 지적 없음"
    STATUS="PASS"; return
  fi
  { printf '## 반대파 지적\n\n'; cat "$dir/con.md"; printf '\n\n'; } >> "$transcript"
  log "  반대파 지적 $(count "$all")건: $(oneline "$all")"

  # 2) 찬성파: 1차 방어 (판정 안 한 항목은 '반박 없음' = 인정으로 본다)
  render "$PROMPTS/pro.md" "$dir/pro1.task.md" "${common[@]}" "FINDINGS=$dir/con.md" "IDS=$(oneline "$all")"
  run_claude "찬성파 방어 1차" "$dir/pro1.task.md" "$dir/pro1.md" read "$PRO_MODEL"
  { printf '## 찬성파 1차\n\n'; cat "$dir/pro1.md"; printf '\n\n'; } >> "$transcript"
  local contested accepted
  contested="$(inter "$all" "$(verdicts "$dir/pro1.md" REBUT)")"
  accepted="$(minus "$all" "$contested")"

  # 3) 재반박 주고받기 (상한 DEBATE_ROUNDS)
  local d maintained rebut_again
  for (( d=1; d<=DEBATE_ROUNDS; d++ )); do
    [[ -z "$contested" ]] && break
    render "$PROMPTS/con-rebut.md" "$dir/con-r$d.task.md" "${common[@]}" "FINDINGS=$dir/con.md" "IDS=$(oneline "$contested")"
    run_claude "반대파 재반박 $d차" "$dir/con-r$d.task.md" "$dir/con-r$d.md" read "$CON_MODEL"
    { printf '## 반대파 재반박 %s차\n\n' "$d"; cat "$dir/con-r$d.md"; printf '\n\n'; } >> "$transcript"
    # 유지(MAINTAIN)라고 안 한 항목은 철회로 본다
    maintained="$(inter "$contested" "$(verdicts "$dir/con-r$d.md" MAINTAIN)")"
    if [[ -z "$maintained" ]]; then contested=""; break; fi

    render "$PROMPTS/pro.md" "$dir/pro$((d+1)).task.md" "${common[@]}" "FINDINGS=$dir/con.md" "IDS=$(oneline "$maintained")"
    run_claude "찬성파 방어 $((d+1))차" "$dir/pro$((d+1)).task.md" "$dir/pro$((d+1)).md" read "$PRO_MODEL"
    { printf '## 찬성파 %s차\n\n' "$((d+1))"; cat "$dir/pro$((d+1)).md"; printf '\n\n'; } >> "$transcript"
    rebut_again="$(inter "$maintained" "$(verdicts "$dir/pro$((d+1)).md" REBUT)")"
    accepted="$(clean_list "$accepted" "$(minus "$maintained" "$rebut_again")")"
    contested="$rebut_again"
  done

  ACCEPTED="$(clean_list "$accepted")"
  UNRESOLVED="$(clean_list "$contested")"
  [[ -n "$ACCEPTED" ]] && extract_blocks "$dir/con.md" "$ACCEPTED" > "$dir/fix-list.md"
  if [[ -n "$UNRESOLVED" ]]; then
    { printf '# 합의 안 된 쟁점 (사람 판단 필요)\n\n찬반 공방 전체는 같은 폴더의 debate.md에 있습니다.\n\n'
      extract_blocks "$dir/con.md" "$UNRESOLVED"; } > "$dir/unresolved.md"
  fi
  log "  결과 → 수정 대상 $(count "$ACCEPTED")건, 합의 안 됨 $(count "$UNRESOLVED")건"
  [[ -z "$ACCEPTED" && -z "$UNRESOLVED" ]] && STATUS="PASS"
}

# ----- 수정 라운드 -----------------------------------------------------------
fix_round() {
  local r="$1" dir="$RUN/round$r"
  render "$PROMPTS/fixer.md" "$dir/fix.task.md" "FIXLIST=$dir/fix-list.md" "INTENT=$DESIGN_INTENT"
  run_claude "수정 담당" "$dir/fix.task.md" "$dir/fix.md" edit "$FIX_MODEL"

  local changed; changed="$(git status --porcelain | cut -c4- | tr -d '"')"
  if [[ -z "$changed" ]]; then
    warn "수정 세션이 아무것도 바꾸지 않았습니다 ($dir/fix.md)"
    return 1
  fi
  if grep -Eq "$PROTECTED_RE" <<< "$changed"; then
    git stash push -u -q -m "review-pipeline: 보호 파일 수정으로 보류됨"
    die "수정 세션이 보호 파일을 건드려 변경을 stash에 보관했습니다 (git stash list 확인)."
  fi
  if [[ -n "$COMPILE_CMD" ]]; then
    log "  · 컴파일 체크"
    if ! bash -c "$COMPILE_CMD" > "$dir/compile.log" 2>&1; then
      git stash push -u -q -m "review-pipeline: 컴파일 실패로 보류됨"
      die "수정 후 컴파일 실패. 변경은 stash에 보관했습니다 (로그: $dir/compile.log)."
    fi
  fi
  git add -A
  git commit -q -m "fix(review): 찬반 리뷰 확정 지적 수정 — $TARGET_LABEL r$r $(oneline "$ACCEPTED")" || die "수정 커밋 실패"
  log "  수정 커밋: $(git rev-parse --short HEAD)"
}

# ----- 대상 하나에 대해 리뷰 → 수정 → 재리뷰 ---------------------------------
# 결과: FINAL(PASS|UNRESOLVED|FIX_LIMIT|FIX_FAILED), LAST_ROUND, RUN/report.md
run_target() {
  FINAL=""; LAST_ROUND=0
  local r
  for (( r=1; r<=MAX_ROUNDS; r++ )); do
    LAST_ROUND=$r
    review_round "$r"
    if [[ "$STATUS" == "PASS" ]]; then FINAL="PASS"; break; fi
    if [[ -z "$ACCEPTED" ]];      then FINAL="UNRESOLVED"; break; fi
    if (( r == MAX_ROUNDS ));     then FINAL="FIX_LIMIT"; break; fi
    fix_round "$r" || { FINAL="FIX_FAILED"; break; }
  done

  local i d
  {
    printf '# %s: %s\n\n' "$TARGET_LABEL" "$FINAL"
    printf -- '- 라운드: %s\n\n' "$LAST_ROUND"
    for (( i=1; i<=LAST_ROUND; i++ )); do
      d="$RUN/round$i"
      printf '## 라운드 %s\n' "$i"
      [[ -f "$d/fix-list.md" ]]   && printf -- '- 수정 대상: %s\n' "$(oneline "$(finding_ids "$d/fix-list.md")")"
      [[ -f "$d/fix.md" ]]        && printf -- '- 수정 결과: round%s/fix.md\n' "$i"
      [[ -f "$d/unresolved.md" ]] && printf -- '- 합의 안 됨: round%s/unresolved.md\n' "$i"
      printf -- '- 토론 기록: round%s/debate.md\n\n' "$i"
    done
  } > "$RUN/report.md"
}

# =============================================================================
# --all: 이미 있는 코드 전체를 폴더별로 리뷰·수정
# =============================================================================
if (( ALL )); then
  log "전체 리뷰: 대상 ${#ALL_TARGETS[@]}개 (브랜치 $BRANCH)"
  SUMMARY="$RUN_ROOT/summary.md"
  printf '# 전체 리뷰 결과\n\n| 대상 | 결과 | 보고서 |\n|---|---|---|\n' > "$SUMMARY"
  n=0; problems=0
  for t in "${ALL_TARGETS[@]}"; do
    n=$((n+1))
    TARGET_LABEL="$(basename "${t%/\*.cs}")"
    [[ "$t" == *'/*.cs' ]] && TARGET_LABEL="${TARGET_LABEL}-root"
    RUN="$RUN_ROOT/$(printf '%02d' "$n")-$TARGET_LABEL"
    mkdir -p "$RUN"
    # 폴더 바로 아래 .cs만 (하위 폴더는 따로 돈다). glob 매직이 없으면 *가 하위 폴더까지 매칭됨
    if [[ "$t" == *'/*.cs' ]]; then TARGET_PATHS=(":(glob)${t%/\*.cs}/*.cs"); else TARGET_PATHS=("$t"); fi
    log "━━ [$n/${#ALL_TARGETS[@]}] $t"
    run_target
    [[ "$FINAL" == "PASS" ]] || problems=$((problems+1))
    printf '| %s | %s | %s |\n' "$t" "$FINAL" "${RUN#"$RUN_ROOT"/}/report.md" >> "$SUMMARY"
  done
  rm -rf "$STATE_DIR/latest" && cp -r "$RUN_ROOT" "$STATE_DIR/latest"
  log "전체 리뷰 끝: 대상 $n개 중 확인 필요 $problems개"
  log "요약: $STATE_DIR/latest/summary.md"
  log "수정 커밋은 현재 브랜치($BRANCH)에 쌓였습니다. 확인 후 PR로 올리세요."
  (( problems == 0 )) || exit 1
  exit 0
fi

# =============================================================================
# 기본: push 전 변경분 리뷰
# =============================================================================
log "기준: $BASE_BRANCH ($(git rev-parse --short "$BASE")) → HEAD ($(git rev-parse --short HEAD))"
TARGET_LABEL="changes"; TARGET_PATHS=("${PATHS_ARR[@]}"); RUN="$RUN_ROOT"
run_target
rm -rf "$STATE_DIR/latest" && cp -r "$RUN" "$STATE_DIR/latest"
echo "$(git rev-parse HEAD) $FINAL" > "$STATE_DIR/last-status"

case "$FINAL" in
  PASS)
    log "✅ 통과"; approve; do_push ;;
  UNRESOLVED)
    warn "⚠️  찬반이 끝까지 합의하지 못한 쟁점이 남았습니다."
    warn "   확인: $STATE_DIR/latest/round$LAST_ROUND/unresolved.md"
    warn "   문제없다고 판단되면: bash scripts/review/prepush.sh --accept"
    exit 1 ;;
  FIX_LIMIT)
    warn "❌ 수정 라운드 상한($MAX_ROUNDS)에 도달했는데 확정된 문제가 남아 있습니다."
    warn "   확인: $STATE_DIR/latest/round$LAST_ROUND/fix-list.md"
    exit 1 ;;
  FIX_FAILED)
    warn "❌ 수정 세션이 변경을 만들지 못했습니다. $STATE_DIR/latest/round$LAST_ROUND/fix.md 확인"
    exit 1 ;;
esac
