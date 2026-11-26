#!/usr/bin/env bash
# 찹츄 멀티 테스트 — 열린 Unity 에디터를 Unity CLI 로 조종해 결과를 비교한다. 사용법은 SKILL.md.
#
#   run.sh rules [인원=4] [턴=8]      규칙 계층만 (네트워크 · 플레이 모드 없음)
#   run.sh ui                          한 클라이언트: 타이틀 → 로비 → 경고 팝업 · 토스트 → 방 → 나가기
#   run.sh multi [클라이언트=2] [턴=4]  메인 + 클론: 디버그 로비 → 방 → 게임, 턴마다 행동, 화면끼리 비교
#   run.sh full [클라이언트=2] [HP=20]   메인 + 클론: 체력을 낮춰 한 판을 끝까지 → 함정 · 사망 · 게임 종료 → 결과 화면 → 한 판 더 → 로비로
#   run.sh solo [HP=30]                 메인 혼자 + 봇 3: 혼자 하기 → 봇 제거 · 추가 → 한 판 끝까지 → 결과 화면 → 한 판 더 → 로비로
#   run.sh chapchu [클라이언트=2]       메인 + 클론: 뽑기만 → 손패 10장 자동 선언 → 표시 → 찹츄 승리 → 결과 화면
#   run.sh skip                         메인 + 클론: 반응 시간에 방장에게 카운터를 쥐여 줌 → 초록 빛 · 「스킵」 버튼 · 남은 초 없음 → 누르면 꺼짐 · 그 뒤 카운터 거절
#   run.sh counter pick                 2018 물귀신 — 카운터를 낼 때 화면에서 대상(상대 좌석)을 고름 → 나 · 고른 사람이 같은 피해
#   run.sh counter cost                 위에 이어 2021 으르렁의 대가 — 고르기 판의 카드 칸을 눌러 1장 버림
#   run.sh counter                      메인 + 클론: 방장에게 실제 카운터(2003)를 쥐여 줌 → 화면에서 냄 → 막힌 카드는 쓴 사람 손패로 · 화면 = 방장
#   run.sh timeout                      메인 + 클론: 클론의 턴만 시간 초과 → 자동 뽑기 2번 → 3번째에 강제 퇴장 → 클론은 로비 + 경고 팝업, 메인은 결과 화면
#   run.sh fx <카드 ID> [ms=300,800,1500] 메인 혼자, 연출 시험장(CardFxTestScene): 그 카드를 냄 → 3 단계 시작부터 ms 마다 찍기(SHOT_DIR) → 끝까지 → 콘솔 오류
#
# 종료 코드: 0 통과 · 1 실패 · 2 준비 안 됨 (저장 안 한 씬, 에디터 없음, 컴파일 실패)

set -u

SKILL_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SKILL_DIR/../../.." && pwd)"
MAIN="$ROOT/MuffinProject"
CS="$SKILL_DIR/MultiTest.cs"
LOBBY_SCENE="Assets/Scenes/DebugLobbyScene.unity"
FX_SCENE="Assets/Scenes/CardFxTestScene.unity"
TITLE_SCENE="Assets/Scenes/TitleScene.unity"
WAIT_TRIES=30   # 대기 단계 최대 시도 (2초 간격)

FAILED=0
EDITORS=()
PROBED=0   # multi 의 카운터 요청: 0 안 함 · 1 행동 카드로(코드 6 · 8 기대) · 2 카운터 카드로(무효 기대)

log()  { printf '%s\n' "$*"; }
fail() { log "  ✗ $*"; FAILED=1; }

# unity command <이름> ... — 에디터 하나에 명령. stdin 을 막아야 CLI 가 멈추지 않는다.
# 한 번의 CLI 호출은 2분을 넘기지 않는다 — 에디터가 리로드 중이면 CLI 가 돌아오지 않을 수 있어, 멈추면 WAIT 로 보고 다시 시도하게 한다
ucmd() {
    local project="$1"; shift
    timeout 120 unity command "$@" --project-path "$project" --caller plugin --skill multi-test </dev/null 2>&1         || { [ $? -eq 124 ] && printf '%s' '{"errors":[{"message":"CLI 응답 없음 (120초)"}],"success":false}'; }
}

# MultiTest.<entry>(args...) 를 실행하고 반환 문자열을 찍는다. CLI · 컴파일 오류면 "WAIT ..." / "FAIL ...".
call() {
    local project="$1" entry="$2"; shift 2
    local args="[]"
    if [ $# -gt 0 ]; then
        args=$(python -c 'import json,sys; print(json.dumps(sys.argv[1:]))' "$@")
    fi
    ucmd "$project" script.run --file "$CS" --entry "MultiTest.$entry" --args "$args" --result-only | python -c '
import json, sys
sys.stdin.reconfigure(encoding="utf-8", errors="replace"); sys.stdout.reconfigure(encoding="utf-8")
raw = sys.stdin.read()
try:
    r = json.loads(raw[raw.index("{"):])
except Exception:
    print("WAIT cli " + raw.strip().replace("\n", " ")[:200]); sys.exit()
errors = [d for d in r.get("diagnostics", []) if str(d.get("severity", "")).lower() == "error"]
if errors:
    print("FAIL MultiTest.cs 컴파일 — " + "; ".join(str(d.get("message", d)) for d in errors)[:400]); sys.exit()
if "errors" in r and not r.get("success"):
    # CLI 가 에디터에 못 붙음 — 플레이 진입 · 컴파일 중 도메인 리로드. 잠시 뒤 다시 시도한다.
    print("WAIT cli " + "; ".join(e.get("message", "") for e in r["errors"])[:200]); sys.exit()
if not r.get("success"):
    print("FAIL " + str(r.get("error") or r)[:400]); sys.exit()
print(str(r.get("result", "")).replace("\r\n", "\n"))
'
}

# OK 가 나올 때까지 되풀이한다. FAIL 이면 바로 실패.
until_ok() {
    local project="$1" entry="$2"; shift 2
    local out
    for _ in $(seq 1 $WAIT_TRIES); do
        out=$(call "$project" "$entry" "$@")
        case "$out" in
            OK*)   log "  $(name_of "$project"): $out"; return 0 ;;
            FAIL*) fail "$(name_of "$project"): $out"; return 1 ;;
        esac
        sleep 2
    done
    fail "$(name_of "$project"): 시간 초과 — 마지막 응답 $out"
    return 1
}

name_of() { basename "$1"; }

# 컴파일이 끝날 때까지 기다린다. 실패면 종료 2.
wait_compile() {
    local project="$1" status
    sleep 3
    for _ in $(seq 1 60); do
        status=$(ucmd "$project" console.status --result-only)
        if printf '%s' "$status" | grep -q '"compilationFailed": *true'; then
            log "✗ $(name_of "$project"): 컴파일 실패 — 에디터 콘솔을 본다"; exit 2
        fi
        printf '%s' "$status" | grep -q '"compiling": *false' && return 0
        sleep 3
    done
    log "✗ $(name_of "$project"): 컴파일이 끝나지 않는다"; exit 2
}

# 백그라운드 에디터는 새 코드를 스스로 가져오지 않는다 — MultiTest.cs 가 새 API 로 컴파일되도록 Refresh 하고 컴파일을 기다린다.
# 씬은 건드리지 않는다 (저장 안 한 씬 검사는 Preflight).
refresh_code() {
    ucmd "$1" script.eval --code 'UnityEditor.AssetDatabase.Refresh(); return "OK";' --result-only >/dev/null
    wait_compile "$1"
}

# 에디터가 떠 있지 않으면 연다 (클론). 메인이 없으면 종료 2.
ensure_editor() {
    local project="$1" win
    win=$(cygpath -w "$project" 2>/dev/null || printf '%s' "$project")
    if unity status --format json </dev/null 2>/dev/null | python -c '
import json, sys
sys.stdin.reconfigure(encoding="utf-8", errors="replace"); sys.stdout.reconfigure(encoding="utf-8")
want = sys.argv[1].replace("/", "\\").lower()
d = json.load(sys.stdin)["data"]["instances"]
sys.exit(0 if any(i["project"].lower() == want and i["state"] == "ready" for i in d) else 1)' "$win"; then
        return 0
    fi

    if [ "$project" = "$MAIN" ]; then
        log "✗ 메인 에디터가 열려 있지 않다 — 사람에게 열어 달라고 한다"; exit 2
    fi

    log "  $(name_of "$project") 에디터를 연다 (처음엔 몇 분 걸린다)"
    unity open "$project" --non-interactive </dev/null >/dev/null 2>&1
    for _ in $(seq 1 100); do
        sleep 6
        unity status --format json </dev/null 2>/dev/null | python -c '
import json, sys
sys.stdin.reconfigure(encoding="utf-8", errors="replace"); sys.stdout.reconfigure(encoding="utf-8")
want = sys.argv[1].replace("/", "\\").lower()
d = json.load(sys.stdin)["data"]["instances"]
sys.exit(0 if any(i["project"].lower() == want and i["state"] == "ready" for i in d) else 1)' "$win" && return 0
    done
    log "✗ $(name_of "$project") 에디터가 준비되지 않는다"; exit 2
}

# 준비: 저장 안 한 씬 검사 → Refresh → 컴파일 대기 → 시작 씬 · 백그라운드 실행 → 콘솔 비우기 → 플레이
prepare() {
    local scene="$1" project out
    for project in "${EDITORS[@]}"; do
        ensure_editor "$project"
        refresh_code "$project"
        out=$(call "$project" Preflight)
        case "$out" in
            OK*) log "  $(name_of "$project"): $out" ;;
            *)   log "✗ $(name_of "$project"): $out"; exit 2 ;;
        esac
    done
    for project in "${EDITORS[@]}"; do
        wait_compile "$project"
    done

    trap cleanup EXIT
    for project in "${EDITORS[@]}"; do
        call "$project" SetStartScene "$scene" >/dev/null
        ucmd "$project" set_run_in_background --enable true >/dev/null
        ucmd "$project" console.clear >/dev/null
        ucmd "$project" start >/dev/null
    done
}

# 뒷정리 — 실패해도 항상 돈다.
cleanup() {
    local project
    for project in "${EDITORS[@]}"; do
        ucmd "$project" stop >/dev/null
    done
    sleep 2
    for project in "${EDITORS[@]}"; do
        call "$project" SetStartScene "" >/dev/null
        ucmd "$project" set_run_in_background --enable false >/dev/null
    done
}

# 콘솔 줄을 "레벨<TAB>첫 줄" 로 받는다. 스택에 게임 · Photon 코드가 없는 에러는 에디터 내부 것이라
# (예: 에디터를 막 열었을 때 Unity Search 색인) "editor-error" 로 따로 둔다.
console_lines() {
    ucmd "$1" console.get --tail 300 --result-only | python -c '
import json, sys
sys.stdin.reconfigure(encoding="utf-8", errors="replace"); sys.stdout.reconfigure(encoding="utf-8")
raw = sys.stdin.read()
r = json.loads(raw[raw.index("{"):])
for e in r.get("entries", []):
    level = e["level"]
    stack = e.get("stackTrace", "")
    if level == "error" and stack and not any(k in stack for k in ("Chapchu", "Photon", "Assets/")):
        level = "editor-error"
    print(level + "\t" + e["message"].split("\n")[0])
'
}

check_errors() {
    local project="$1" errors
    errors=$(console_lines "$project" | awk -F'\t' '$1 == "error"')
    if [ -n "$errors" ]; then
        fail "$(name_of "$project") 콘솔 에러:"
        printf '%s\n' "$errors" | sed 's/^/      /'
    else
        log "  $(name_of "$project"): 콘솔 에러 0"
    fi
    console_lines "$project" | awk -F'\t' '$1 == "editor-error" { print "      (참고 · 에디터 내부 에러, 판정 제외) " $2 }'
}

# ── 시나리오 ──

run_rules() {
    local players="${1:-4}" rounds="${2:-8}" out
    EDITORS=("$MAIN")
    ensure_editor "$MAIN"
    refresh_code "$MAIN"
    log "▶ 규칙 계층 — ${players}인 ${rounds}턴"
    out=$(call "$MAIN" Rules "$players" "$rounds")
    printf '%s\n' "$out" | sed 's/^/  /'
    case "$out" in OK*) ;; *) FAILED=1 ;; esac
    local scenario
    for scenario in "Chain:체인 · 카운터" "Death:사망 · 빈 덱 피해 · 나감" "Win:찹츄 · 승리 · 종료" "Trap:함정" "Bot:봇" "Effects:카드 효과" "Stats:판 지표 집계" "Migrate:방장 이주 — 상태 담기 · 되살리기 · 이어 가기"; do
        log "▶ 규칙 계층 — ${scenario#*:} (3인, 메모리 카드)"
        out=$(call "$MAIN" "${scenario%%:*}")
        printf '%s\n' "$out" | sed 's/^/  /'
        case "$out" in OK*) ;; *) FAILED=1 ;; esac
    done
}

run_ui() {
    EDITORS=("$MAIN")
    log "▶ 준비"
    prepare "$TITLE_SCENE"
    log "▶ UI 흐름"
    until_ok "$MAIN" Ui title &&
    until_ok "$MAIN" Ui bad-code &&
    until_ok "$MAIN" Ui warning &&
    until_ok "$MAIN" Ui create &&
    until_ok "$MAIN" Ui leave
    local out=""
    for _ in $(seq 1 $WAIT_TRIES); do
        out=$(call "$MAIN" Ui scene)
        [ "$out" = "OK LobbyScene" ] && break
        sleep 2
    done
    [ "$out" = "OK LobbyScene" ] && log "  나간 뒤 로비로 돌아옴" || fail "나간 뒤 씬: $out"
    sleep 2
    log "▶ 판정"
    check_errors "$MAIN"
}

# 메인 + 클론 (clients - 1)개를 디버그 로비에서 방으로 모아 게임 화면까지. 실패면 1 을 돌려준다.
enter_game() {
    local clients="$1" i project out
    EDITORS=("$MAIN")
    for i in $(seq 0 $((clients - 2))); do
        EDITORS+=("$ROOT/MuffinProject_clone_$i")
    done

    log "▶ 준비 — 클라이언트 ${clients}개"
    prepare "$LOBBY_SCENE"

    log "▶ 입장 · 시작 (메인이 먼저 들어가 방장이 된다)"
    for project in "${EDITORS[@]}"; do
        until_ok "$project" Join || return 1
        sleep 2
    done
    until_ok "$MAIN" StartGame "$clients" || return 1
    wait_scene GameScene || return 1
    sleep 2
}

# 그 클라이언트가 그 씬에 올 때까지
wait_scene_one() {
    local project="$1" scene="$2" out
    for _ in $(seq 1 $WAIT_TRIES); do
        out=$(call "$project" Ui scene)
        [ "$out" = "OK $scene" ] && return 0
        sleep 2
    done
    fail "$(name_of "$project") $scene 에 오지 않음: $out"; return 1
}

# 모든 클라이언트가 그 씬에 올 때까지
wait_scene() {
    local project
    for project in "${EDITORS[@]}"; do
        wait_scene_one "$project" "$1" || return 1
    done
}

# 결과 화면 (07-win-condition.md 11절): 전원 ResultScene · 그려진 결과 줄이 모든 화면에서 같음 · 공개 HP 는 시작 값 · 방은 닫힌 채.
# 첫 인자가 있으면 결과 줄에 그 글자가 있어야 한다. SHOT_DIR 이 있으면 등장 연출이 끝난 뒤 result<꼬리>_<에디터>.png.
# 셋째 인자가 있으면 이모트 · 판 기록도 본다 (10초쯤 더 걸린다 — 뒤에 「한 판 더」 를 눌러야 하는 full 의 첫 결과 화면은 45초 자동 이동에 걸려 빼고, 둘째에서 본다).
check_result() {
    local want="${1:-}" tag="${2:-}" extras="${3:-}" who="" project out first=""
    wait_scene ResultScene || return 1
    for project in "${EDITORS[@]}"; do
        out=""
        for _ in $(seq 1 $WAIT_TRIES); do
            out=$(call "$project" ResultSummary)
            case "$out" in OK*|FAIL*) break ;; esac
            sleep 2
        done
        case "$out" in OK*) log "  $(name_of "$project"): $out" ;; *) fail "$(name_of "$project"): 결과 화면이 그려지지 않았다 — $out"; return 1 ;; esac
        if [ -z "$first" ]; then first="$out"; elif [ "$out" != "$first" ]; then fail "$(name_of "$project") 의 결과 화면이 메인과 다르다: $out"; fi
        if [ -n "$want" ] && [[ "$out" != *"$want"* ]]; then fail "$(name_of "$project") 결과 화면에 「$want」 이 없다"; fi
        until_ok "$project" ExpectHpReset || return 1   # 지난 판 HP 가 남지 않는다 — 다음 판 좌석 · 기록의 기준 값
        until_ok "$project" ExpectRoomClosed || return 1
    done
    [ -n "${SHOT_DIR:-}" ] && mkdir -p "$SHOT_DIR"
    # 말풍선(이모트 · 「준비!」)은 2.4초면 사라진다 — 뜨는 것을 에디터 안에서 적어 두게 한다. 이 부름들이 등장 연출(약 3초) 기다림을 겸한다
    for project in "${EDITORS[@]}"; do until_ok "$project" ArmBubbleLog || return 1; done
    if [ -n "$extras" ]; then
        # 이모트 — 메인이 누르면 방장을 거쳐 모든 화면의 메인 캐릭터 옆에 말풍선
        out=""
        for _ in $(seq 1 $WAIT_TRIES); do
            out=$(call "$MAIN" ResultEmote 0)
            case "$out" in OK*|FAIL*) break ;; esac
            sleep 1
        done
        log "  $(name_of "$MAIN"): $out"
        case "$out" in OK*) who="${out##*나 = }" ;; *) fail "이모트를 누르지 못했다"; return 1 ;; esac
        if [ -n "${SHOT_DIR:-}" ]; then log "  $(name_of "$MAIN"): $(call "$MAIN" Shot "$SHOT_DIR/result${tag}_emote_$(name_of "$MAIN").png")"; fi
    else
        sleep 2   # 등장 연출(약 3초) · 봇 준비(2.9초부터)
    fi
    for project in "${EDITORS[@]}"; do
        log "  $(name_of "$project"): $(call "$project" ResultState) · $(call "$project" Batches)"
        if [ -n "${SHOT_DIR:-}" ]; then
            log "  $(name_of "$project"): $(call "$project" Shot "$SHOT_DIR/result${tag}_$(name_of "$project").png")"
        fi
    done
    [ -n "$extras" ] || return 0
    for project in "${EDITORS[@]}"; do
        out=$(call "$project" BubbleLog)
        log "  $(name_of "$project"): $out"
        [[ "$out" == *" $who:GG"* ]] || fail "$(name_of "$project") 에 $who 의 말풍선 「GG」 가 뜨지 않았다"
    done
    # 판 기록 — 「판 기록」 버튼으로 열리고 ✕ 로 닫힌다
    until_ok "$MAIN" ResultTap board || return 1
    if [ -n "${SHOT_DIR:-}" ]; then log "  $(name_of "$MAIN"): $(call "$MAIN" Shot "$SHOT_DIR/result${tag}_board_$(name_of "$MAIN").png")"; fi
    until_ok "$MAIN" ResultBoard close || return 1
}

# 차례인 클라이언트가 행동하며 한 판을 끝까지 (full). 끝 알림은 2초 뒤 결과 씬으로 넘어가므로 「게임 화면 아님 (ResultScene)」 도 끝으로 본다.
# TRAP_SEEN 이 비어 있으면 화면 경로로 함정을 처음 설치했을 때 함정 칸 · 크게 보기를 한 번 확인한다.
TRAP_SEEN=""
play_to_end() {
    local clients="$1" project out ended=0
    for _ in $(seq 1 $((clients * 100))); do  # 4인은 판이 길다 — 인원에 비례
        for project in "${EDITORS[@]}"; do
            out=$(call "$project" ActIfMyTurn)
            case "$out" in
                OK*)   log "  $(name_of "$project"): $out"; sleep 2
                       # 화면 경로로 함정을 처음 설치했으면 — 모든 화면의 함정 칸 수가 방장 값과 같은지 한 번 본다 (연출 중이면 잠깐 다를 수 있어 몇 번 다시 본다)
                       if [ -z "$TRAP_SEEN" ] && [[ "$out" == *"함정 설치(화면)"* ]]; then
                           TRAP_SEEN=1
                           sleep 16   # 같이 낸 카드의 체인(연출 + 반응 10초 + 후처리)이 끝나 큰 카드가 내려간 뒤에 본다
                           check_table_ui "trap" || return 1
                           # 내 함정 칸을 누르면 그 카드가 크게 보이는가 (10-ui.md 4-1)
                           out=$(call "$project" TrapPreview)
                           case "$out" in
                               OK*)   log "  $(name_of "$project"): $out"
                                      if [ -n "${SHOT_DIR:-}" ]; then sleep 1; log "  $(call "$project" Shot "$SHOT_DIR/trap_preview_$(name_of "$project").png")"; fi ;;
                               FAIL*) fail "$(name_of "$project"): $out"; return 1 ;;
                               *)     log "  $(name_of "$project"): $out" ;;
                           esac
                       fi
                       break ;;
                FAIL*) fail "$(name_of "$project"): $out"; return 1 ;;
                *"게임 끝"*|*"(ResultScene)"*) ended=1 ;;
            esac
        done
        [ $ended -eq 1 ] && return 0
        sleep 1
    done
    fail "게임이 끝나지 않았다"; return 1
}

# 메인 혼자 + 봇 (solo): 봇은 방장 기기에서 스스로, 사람은 차례에 행동하며 끝까지.
play_solo_to_end() {
    local out
    for _ in $(seq 1 600); do
        out=$(call "$MAIN" ActIfMyTurn)
        case "$out" in
            OK*)   log "  $out" ;;
            FAIL*) fail "$out"; return 1 ;;
            *"게임 끝"*|*"(ResultScene)"*) return 0 ;;
        esac
        sleep 1
    done
    fail "게임이 끝나지 않았다"; return 1
}

# 모든 화면의 함정 칸 · 손패 장수가 방장 값과 같은가 (MultiTest.TableUiExpect). 연출 중엔 잠깐 다를 수 있어 6번까지 다시 본다. SHOT_DIR 이 있으면 <이름>_<에디터>.png 도 찍는다.
check_table_ui() {
    local shot="$1" other out try
    for other in "${EDITORS[@]}"; do
        for try in 1 2 3 4 5 6; do
            out=$(call "$other" TableUiExpect)
            case "$out" in OK*) break ;; esac
            sleep 2
        done
        case "$out" in
            OK*) log "  $(name_of "$other"): $out" ;;
            *)   fail "$(name_of "$other"): $out"; return 1 ;;
        esac
        if [ -n "${SHOT_DIR:-}" ]; then
            mkdir -p "$SHOT_DIR"
            log "  $(name_of "$other"): $(call "$other" Shot "$SHOT_DIR/${shot}_$(name_of "$other").png")"
        fi
    done
}

run_full() {
    local clients="${1:-2}" hp="${2:-20}" project out line first="" leaver
    enter_game "$clients" || return
    until_ok "$MAIN" SetAllHp "$hp" || return

    log "▶ 한 판 끝까지 — 체력 ${hp} 에서 시작, 차례인 클라이언트가 행동"
    # SHOT_DIR 이 있으면 결과 화면 등장 연출(색종이)을 미리 걸어 찍는다
    if [ -n "${SHOT_DIR:-}" ]; then mkdir -p "$SHOT_DIR"; log "  $(call "$MAIN" ArmResultShots "$SHOT_DIR/result_intro_$(name_of "$MAIN")" "2.2,2.8,3.6")"; fi
    play_to_end "$clients" || return

    log "▶ 판정"
    for project in "${EDITORS[@]}"; do
        line=$(console_lines "$project" | grep '\[GameDebugInput\].*게임 종료 — ' | tail -1 | sed -E 's/.*게임 종료 — ([^·]*· 승자 P-?[0-9]+).*/\1/')
        log "  $(name_of "$project"): 「${line:-없음}」 · 사망 $(console_lines "$project" | grep -c '\[GameDebugInput\].* 사망 · 버림')번 · 함정 설치 $(console_lines "$project" | grep -c '\[GameDebugInput\].* 함정 설치 — ')번 · 함정 발동 $(console_lines "$project" | grep -c '\[GameDebugInput\].* 함정 발동 — ')번"
        [ -n "$line" ] || fail "$(name_of "$project") 게임 종료를 받지 못했다"
        if [ -z "$first" ]; then first="$line"; elif [ "$line" != "$first" ]; then fail "$(name_of "$project") 의 결과가 메인과 다르다"; fi
    done
    compare_turns

    log "▶ 결과 화면 — 모든 화면이 같은 결과"
    check_result "" "" "" || return

    log "▶ 한 판 더 — 전원 준비 → 카운트다운 → 새 판 (방은 닫힌 채 · HP 시작 값)"
    for project in "${EDITORS[@]}"; do
        until_ok "$project" ResultTap again || return
    done
    wait_scene GameScene || return
    sleep 2
    for project in "${EDITORS[@]}"; do
        out=$(call "$project" BubbleLog)   # 다른 사람이 준비하면 그 캐릭터 옆에 「준비!」 (내 준비는 띄우지 않는다)
        if [[ "$out" == *":준비!"* ]]; then log "  $(name_of "$project"): $out"; else fail "$(name_of "$project") 에 다른 사람의 「준비!」 말풍선이 뜨지 않았다 — $out"; fi
        until_ok "$project" ExpectRoomClosed || return
    done
    until_ok "$MAIN" SetAllHp 5 || return   # 둘째 판은 빨리 끝낸다 — 결과 화면의 「로비로」 를 보려는 것
    play_to_end "$clients" || return
    check_result "" "2" 1 || return   # 이모트 · 판 기록은 여기서 — 뒤가 「로비로」 뿐이라 시간이 넉넉하다

    # 마지막 클라이언트(방장이 아닌 쪽)가 나간다 — 남은 화면에 「나감」. 2명이었으면 혼자 남아 「한 판 더」 가 꺼지고 45초 마감에 스스로 로비로 간다
    leaver="${EDITORS[$((${#EDITORS[@]} - 1))]}"
    log "▶ 로비로 — $(name_of "$leaver") 가 나간다"
    until_ok "$leaver" ResultTap lobby || return
    wait_scene_one "$leaver" DebugLobbyScene || return
    for project in "${EDITORS[@]}"; do
        [ "$project" = "$leaver" ] && continue
        for _ in $(seq 1 10); do
            out=$(call "$project" ResultState)
            [[ "$out" == *"=나감"* ]] && break
            sleep 1
        done
        log "  $(name_of "$project"): $out"
        [[ "$out" == *"=나감"* ]] || fail "$(name_of "$project") 나간 사람이 「나감」 으로 보이지 않는다"
        if [ "$clients" -eq 2 ]; then
            [[ "$out" == *"한 판 더 꺼짐"* ]] || fail "$(name_of "$project") 혼자 남았는데 「한 판 더」 가 켜져 있다"
            if [ -n "${SHOT_DIR:-}" ]; then log "  $(name_of "$project"): $(call "$project" Shot "$SHOT_DIR/result_alone_$(name_of "$project").png")"; fi
        else
            until_ok "$project" ResultTap lobby || return
        fi
    done
    for project in "${EDITORS[@]}"; do
        wait_scene_one "$project" DebugLobbyScene || return   # 혼자 남은 화면은 45초 마감에 자동 이동
    done
    log "  전원 로비로"
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 사람 1 + 봇 3 (18-bot.md 10절): 혼자 하기 → 봇 제거 · 다시 추가 → 시작 → 체력을 낮춰 한 판 끝까지 → 결과 화면(봇이 스스로 준비) → 한 판 더 → 새 판(봇 · 난이도 · 비공개 그대로) → 끝 → 로비로
run_solo() {
    local hp="${1:-30}" line bots
    EDITORS=("$MAIN")
    log "▶ 준비 — 메인 혼자"
    prepare "$LOBBY_SCENE"

    log "▶ 혼자 하기 → 방 (봇 3 · 정원 1 · 비공개)"
    until_ok "$MAIN" Solo || return
    wait_scene RoomScene || return
    until_ok "$MAIN" ExpectBots "1,2,3" 1 False || return
    until_ok "$MAIN" RemoveBot 2 || return
    until_ok "$MAIN" ExpectBots "1,3" 2 False || return
    until_ok "$MAIN" AddBot || return
    until_ok "$MAIN" ExpectBots "1,2,3" 1 False || return   # 비어 있는 가장 작은 번호(2)를 다시 쓴다
    # 난이도 — 기본은 보통, 방장이 봇 칸을 누를 때마다 보통 → 어려움 → 쉬움 (18-bot 8-1). 봇 1 은 어려움, 봇 3 은 쉬움으로 두고 시작한다
    until_ok "$MAIN" ExpectBotLevel 1 Normal || return
    until_ok "$MAIN" TapBotSlot 1 || return
    until_ok "$MAIN" ExpectBotLevel 1 Hard || return
    until_ok "$MAIN" TapBotSlot 3 || return
    until_ok "$MAIN" TapBotSlot 3 || return
    until_ok "$MAIN" ExpectBotLevel 3 Easy || return
    until_ok "$MAIN" StartGame 1 || return
    wait_scene GameScene || return
    sleep 2
    until_ok "$MAIN" SetAllHp "$hp" || return
    # 콘솔은 끝 300줄만 읽는다 — 시작 줄은 판이 길어지기 전에 본다
    if console_lines "$MAIN" | grep -q 'P1000000001 난이도 Hard' && console_lines "$MAIN" | grep -q 'P1000000002 난이도 Normal' && console_lines "$MAIN" | grep -q 'P1000000003 난이도 Easy'; then
        log "  방에서 고른 난이도로 시작 (봇 1 어려움 · 봇 2 보통 · 봇 3 쉬움)"
    else
        fail "봇이 방에서 고른 난이도로 시작하지 않았다"
    fi

    log "▶ 한 판 끝까지 — 봇은 방장 기기에서 스스로, 사람은 차례에 행동"
    # SHOT_DIR 이 있으면 결과 화면 등장 연출(색종이)을 미리 걸어 찍는다
    if [ -n "${SHOT_DIR:-}" ]; then mkdir -p "$SHOT_DIR"; log "  $(call "$MAIN" ArmResultShots "$SHOT_DIR/result_intro_$(name_of "$MAIN")" "2.2,2.8,3.6")"; fi
    play_solo_to_end || return

    log "▶ 판정"
    line=$(console_lines "$MAIN" | grep '\[GameDebugInput\].*게임 종료 — ' | tail -1 | sed -E 's/.*게임 종료 — ([^·]*· 승자 P-?[0-9]+).*/\1/')
    bots=$(console_lines "$MAIN" | grep -c '\[BotDriver\]')
    log "  「${line:-없음}」 · 봇 행동 이유 ${bots}줄 · 사망 $(console_lines "$MAIN" | grep -c '\[GameDebugInput\].* 사망 · 버림')번"
    console_lines "$MAIN" | grep '\[BotDriver\]' | head -8 | sed -E 's/^[^\t]*\t/      /'
    [ -n "$line" ] || fail "게임 종료를 받지 못했다"
    [ "$bots" -ge 3 ] || fail "봇 행동 이유 로그가 적다 (${bots}줄)"
    if console_lines "$MAIN" | grep '\[BotDriver\]' | grep -q '7-1 .*점'; then log "  봇이 7-1 점수로 행동"; else fail "봇의 7-1 행동 줄이 없다"; fi

    log "▶ 결과 화면 — 봇은 스스로 준비한다"
    check_result "" "" 1 || return
    until_ok "$MAIN" ExpectBots "1,2,3" 1 False && log "  봇 3 그대로 · 혼자 하기 방은 비공개 그대로"
    for _ in $(seq 1 10); do
        line=$(call "$MAIN" ResultState)
        [ "$(printf '%s' "$line" | grep -o '=준비' | wc -l)" -ge 3 ] && break
        sleep 1
    done
    log "  $line"
    [ "$(printf '%s' "$line" | grep -o '=준비' | wc -l)" -ge 3 ] || fail "봇 3 이 준비하지 않았다"

    log "▶ 한 판 더 — 카운트다운 → 새 판"
    ucmd "$MAIN" console.clear >/dev/null   # 새 판의 시작 줄만 보려고
    until_ok "$MAIN" ResultTap again || return
    wait_scene GameScene || return
    sleep 2
    until_ok "$MAIN" ExpectBots "1,2,3" 1 False || return
    until_ok "$MAIN" ExpectRoomClosed || return
    if console_lines "$MAIN" | grep -q 'P1000000001 난이도 Hard' && console_lines "$MAIN" | grep -q 'P1000000003 난이도 Easy'; then
        log "  난이도도 그대로 (봇 1 어려움 · 봇 3 쉬움)"
    else
        fail "새 판의 봇 난이도가 방에서 고른 것과 다르다"
    fi
    until_ok "$MAIN" SetAllHp 5 || return   # 둘째 판은 빨리 끝낸다 — 결과 화면의 「로비로」 를 보려는 것
    play_solo_to_end || return
    check_result "" "2" "" || return

    log "▶ 로비로"
    until_ok "$MAIN" ResultTap lobby || return
    wait_scene DebugLobbyScene && log "  방을 나가 로비로"
    check_errors "$MAIN"
}

run_multi() {
    local clients="${1:-2}" rounds="${2:-4}" project out
    enter_game "$clients" || return

    log "▶ 남의 턴에 카드 내기 (거절돼야 한다)"
    for project in "${EDITORS[@]}"; do
        out=$(call "$project" PlayOutOfTurn)
        case "$out" in OK*) log "  $(name_of "$project"): $out"; break ;; esac
    done
    sleep 2

    # SHOT_DIR 이 있으면 처음 나오는 카드의 3 단계(확대) · 반응 시간도 에디터마다 찍는다 (MultiTest.ArmCardShots)
    if [ -n "${SHOT_DIR:-}" ]; then
        mkdir -p "$SHOT_DIR"
        for project in "${EDITORS[@]}"; do
            log "  $(name_of "$project"): $(call "$project" ArmCardShots "$SHOT_DIR/$(name_of "$project")")"
        done
    fi

    log "▶ 턴 ${rounds}번 — 차례인 클라이언트가 행동"
    local done_rounds=0 acted probed=0
    for _ in $(seq 1 $((rounds * 10))); do
        [ $done_rounds -ge "$rounds" ] && break
        acted=0
        for project in "${EDITORS[@]}"; do
            out=$(call "$project" ActIfMyTurn)
            case "$out" in
                OK*)   log "  $(name_of "$project"): $out"; acted=1; done_rounds=$((done_rounds + 1)); break ;;
                FAIL*) fail "$(name_of "$project"): $out"; return ;;
            esac
        done
        # 처음 카드가 나온 뒤 한 번 — 연출(1.46초) 뒤 반응 시간에 남의 턴 클라이언트가 카운터 요청 (04-card.md 7 · 8절)
        # 카드를 낸 뒤의 쉼(2초)은 이 요청 뒤에 둔다 — 요청 하나(script.run)가 1.5초 넘게 걸려, 쉬고 나서 보내면 반응 시간 5초를 넘겨 코드 6 으로 거절된다 (2026-11-26)
        if [ $acted -eq 1 ] && [ $probed -eq 0 ] && printf '%s' "$out" | grep -q '카드'; then
            for project in "${EDITORS[@]}"; do
                out=$(call "$project" CounterProbe)
                case "$out" in
                    *"(카운터 카드)"*) log "  $(name_of "$project"): $out"; probed=2; break ;;
                    OK*)              log "  $(name_of "$project"): $out"; probed=1; break ;;
                esac
            done
        fi
        [ $acted -eq 1 ] && sleep 2
        [ $acted -eq 0 ] && sleep 1
    done
    PROBED=$probed
    # 마지막 카드의 체인 처리 · 후처리(등장 1.46 + 반응 10 + 후처리 1.28초)가 모든 화면에 닿을 때까지
    sleep 15

    log "▶ 화면의 함정 칸 · 손패 장수가 방장 값과 같은가"
    for project in "${EDITORS[@]}"; do
        until_ok "$project" TableUiExpect || return
    done

    log "▶ 대상 선택에서 나를 고름 — 나를 포함하는 카드 (10-ui.md 6절)"
    local main="${EDITORS[0]}"
    until_ok "$main" SelfPickOpen || return
    if [ -n "${SHOT_DIR:-}" ]; then mkdir -p "$SHOT_DIR"; log "  $(call "$main" Shot "$SHOT_DIR/self_pick_$(name_of "$main").png")"; sleep 2; fi
    out=$(call "$main" SelfPickTap)
    case "$out" in OK*) log "  $(name_of "$main"): $out" ;; *) fail "$(name_of "$main"): $out"; return ;; esac
    until_ok "$main" SelfPickDone || return

    log "▶ 내 카드 다시 받기 · 묵은 처리 번호 (09-network.md 5 · 10절)"
    for project in "${EDITORS[@]}"; do
        until_ok "$project" SyncProbe || return
        until_ok "$project" StaleProbe || return
    done
    sleep 3

    # SHOT_DIR 이 있으면 끝 상태의 게임 뷰를 에디터마다 PNG 로 찍는다 (화면 배치 눈 확인용)
    if [ -n "${SHOT_DIR:-}" ]; then
        log "▶ 스크린샷 → $SHOT_DIR"
        mkdir -p "$SHOT_DIR"
        for project in "${EDITORS[@]}"; do
            out=$(call "$project" Shot "$SHOT_DIR/$(name_of "$project").png")
            log "  $(name_of "$project"): $out"
        done
        sleep 3
    fi

    log "▶ 판정"
    compare_screens
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 클라이언트마다 [GameDebugInput] 턴 줄의 "모두의 손패 장수 · 체력" 부분을 순서대로 모아 비교한다.
compare_turns() {
    local project first="" current rejects
    for project in "${EDITORS[@]}"; do
        current=$(console_lines "$project" | grep '\[GameDebugInput\]' | grep ' 턴 P' | sed -E 's/.* · //')
        rejects=$(console_lines "$project" | grep -c '\[GameDebugInput\].*내 요청 거절')
        log "  $(name_of "$project"): 턴 줄 $(printf '%s\n' "$current" | grep -c .)개 · 마지막 「$(printf '%s\n' "$current" | tail -1)」 · 내 요청 거절 ${rejects}번"
        if [ -z "$first" ]; then
            first="$current"
        elif [ "$current" != "$first" ]; then
            fail "$(name_of "$project") 의 화면 값이 메인과 다르다"
            diff <(printf '%s\n' "$first") <(printf '%s\n' "$current") | sed 's/^/      /'
        fi
    done
}

compare_screens() {
    local project total=0 count
    compare_turns
    for project in "${EDITORS[@]}"; do
        count=$(console_lines "$project" | grep -c '\[GameDebugInput\].*내 요청 거절 — 코드 1')
        total=$((total + count))
    done
    [ "$total" -ge 1 ] && log "  남의 턴 요청 거절 확인" || fail "남의 턴 요청이 거절되지 않았다"
    # 카운터 카드를 냈으면 행동 카드가 무효, 아니면 거절 (CounterProbe) — 코드 8(카운터 카드가 아니다), 또는 아무도 낼 카운터가 없어
    # 반응 시간이 0초로 닫힌 뒤라 코드 6(반응 시간 아님, 04-card 8절). 손의 카운터가 그 카드에 낼 수 없는 것이면 코드 9(반응 조건)
    local pattern='내 요청 거절 — 코드 [689]' what='카운터 요청 거절 (코드 6 · 8 · 9)'
    # 실제 카운터(11-card-list 1-3)는 무효만 하지 않는다 — 무효(카드 무효) · 쓴 사람 손패로(2003) · 피해 감소(2001 · 2016 — 카운터 카드가 처리된 줄)
    if [ "$PROBED" -eq 2 ]; then pattern=' 카드 무효 — \| 손패로 반환\| 카드 처리 — 카드 20[0-9][0-9] '; what='카운터 카드가 방장에게 받아들여져 처리됨 (무효 · 반환 · 감소)'; fi
    total=0
    for project in "${EDITORS[@]}"; do
        count=$(console_lines "$project" | grep -c "\[GameDebugInput\].*$pattern")
        total=$((total + count))
    done
    [ "$total" -ge 1 ] && log "  $what 확인" || fail "$what 이 콘솔에 없다"
    total=0
    for project in "${EDITORS[@]}"; do
        count=$(console_lines "$project" | grep -c '\[GameDebugInput\].* 카드 처리 — ')
        total=$((total + count))
    done
    [ "$total" -ge 1 ] && log "  체인 처리 결과 수신 (카드 처리 줄 ${total}개)" || fail "체인 처리 결과(OnCardResolved)를 받지 못했다"
    for project in "${EDITORS[@]}"; do
        if console_lines "$project" | grep -q '\[GameDebugInput\].*내 카드 동기화 — 일치'; then
            log "  $(name_of "$project"): 내 카드 동기화 일치"
        else
            fail "$(name_of "$project"): 내 카드 동기화가 일치하지 않거나 오지 않았다"
            console_lines "$project" | grep '내 카드 동기화' | sed 's/^/      /'
        fi
    done
}

# 찹츄 자동 선언 (07-win-condition.md 4절): 모두 뽑기만 해서 손패 10장 → 자동 선언 → 모든 화면의 표시 → 다음 자기 턴 시작에 찹츄 승리 → 결과 화면(찹츄 승리 알약 · 마크).
run_chapchu() {
    local clients="${1:-2}" project other out ended=0 declared="" actor line first=""
    enter_game "$clients" || return

    # SHOT_DIR 이 있으면 첫 선언 연출(집중선 · 큰 말풍선, 1.5초)을 에디터마다 찍는다 (MultiTest.ArmShoutShots)
    if [ -n "${SHOT_DIR:-}" ]; then
        mkdir -p "$SHOT_DIR"
        for project in "${EDITORS[@]}"; do
            log "  $(name_of "$project"): $(call "$project" ArmShoutShots "$SHOT_DIR/shout_$(name_of "$project")" "0.2,0.6,1.3")"
        done
    fi

    log "▶ 뽑기만 — 손패 10장이 되면 자동 선언"
    for _ in $(seq 1 $((clients * 150))); do
        for project in "${EDITORS[@]}"; do
            out=$(call "$project" ChapchuStep)
            # 뽑았으면 그 화면에 선언이 떴는지 바로 다시 본다 — 다른 사람이 이어서 뽑으면 먼저 선언한 사람이 곧바로 이겨 표시를 볼 틈이 없다
            if [[ "$out" == OK*"뽑기"* ]]; then
                log "  $(name_of "$project"): $out"
                sleep 2
                out=$(call "$project" ChapchuStep)
            fi
            case "$out" in
                OK*"찹츄 선언됨"*)
                    log "  $(name_of "$project"): $out"
                    actor=$(printf '%s' "$out" | sed -E 's/^OK P([0-9]+).*/\1/')
                    # 표시를 모든 화면에서 본다. 턴이 넘어가 먼저 선언한 사람이 바로 이기면 2초 뒤 결과 씬이라 — 그러면 표시 확인을 멈추고 끝으로 본다
                    for other in "${EDITORS[@]}"; do
                        for _ in $(seq 1 $WAIT_TRIES); do
                            out=$(call "$other" ChapchuExpect "$actor")
                            case "$out" in OK*|FAIL*|*"(ResultScene)"*) break ;; esac
                            sleep 2
                        done
                        case "$out" in
                            OK*) log "  $(name_of "$other"): $out" ;;
                            *"(ResultScene)"*) log "  $(name_of "$other"): 표시를 보기 전에 게임이 끝났다"; ended=1; break ;;
                            *) fail "$(name_of "$other"): $out"; return ;;
                        esac
                    done
                    [ $ended -eq 1 ] && { declared="$declared $actor"; break; }
                    if [ -z "$declared" ] && [ -n "${SHOT_DIR:-}" ]; then
                        mkdir -p "$SHOT_DIR"
                        for other in "${EDITORS[@]}"; do
                            log "  $(name_of "$other"): $(call "$other" Shot "$SHOT_DIR/chapchu_$(name_of "$other").png")"
                        done
                        sleep 2
                    fi
                    declared="$declared $actor"
                    break ;;
                OK*)   log "  $(name_of "$project"): $out"; sleep 2; break ;;
                FAIL*) fail "$(name_of "$project"): $out"; return ;;
                *"게임 끝"*|*"(ResultScene)"*) ended=1 ;;
            esac
        done
        [ $ended -eq 1 ] && break
        sleep 1
    done
    [ -n "$declared" ] || { fail "찹츄가 선언되지 않았다"; return; }
    [ $ended -eq 1 ] || { fail "게임이 끝나지 않았다"; return; }

    log "▶ 판정 — 선언한 사람:$declared"
    for project in "${EDITORS[@]}"; do
        line=$(console_lines "$project" | grep '\[GameDebugInput\].*게임 종료 — ' | tail -1 | sed -E 's/.*게임 종료 — ([^·]*· 승자 P-?[0-9]+).*/\1/')
        log "  $(name_of "$project"): 「${line:-없음}」 · 찹츄 선언 줄 $(console_lines "$project" | grep -c '\[GameDebugInput\].* 찹츄 선언')개"
        printf '%s' "$line" | grep -q 'Chapchu' || fail "$(name_of "$project") 찹츄 승리로 끝나지 않았다"
        console_lines "$project" | grep -q '\[GameDebugInput\].* 찹츄 선언' || fail "$(name_of "$project") 에 찹츄 선언 알림이 오지 않았다"
        if [ -z "$first" ]; then first="$line"; elif [ "$line" != "$first" ]; then fail "$(name_of "$project") 의 결과가 메인과 다르다"; fi
    done
    log "▶ 결과 화면 — 찹츄 승리 알약 · 마크"
    check_result "찹츄마크" "_chapchu" 1 || return
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 1012 대소동 (드문 카드): 방장이 턴 주인에게 1012 를 쥐여 바로 쓰게 한 뒤, 모든 화면에 새 판 · 피폭 줄이 같게 왔는지 · 표가 방장 값과 같은지 본다.
run_restart() {
    local clients="${1:-2}" project out lines first=""
    enter_game "$clients" || return
    until_ok "$MAIN" ForceCard 1012 || return
    log "▶ 처리 · 새 판 대기"
    sleep 20
    log "▶ 새 판 — 가운데에 대소동 카드가 남지 않았나 (손패 · 체인 카드가 버림 더미로 날아간 뒤)"
    for project in "${EDITORS[@]}"; do
        for _ in $(seq 1 15); do
            console_lines "$project" | grep -q '\[GameDebugInput\].*대소동 — ' && break
            sleep 1
        done
        sleep 2
        out=$(call "$project" ChainCardsEmpty)
        log "  $(name_of "$project"): $out"
        case "$out" in OK*) ;; *) fail "$(name_of "$project") $out" ;; esac
        if [ -n "${SHOT_DIR:-}" ]; then mkdir -p "$SHOT_DIR"; log "  $(call "$project" Shot "$SHOT_DIR/restart_center_$(name_of "$project").png")"; fi
    done
    log "▶ 새 판에서 두 턴 진행 (피폭 1턴째 · 2턴째)"
    for _ in $(seq 1 40); do
        for project in "${EDITORS[@]}"; do
            out=$(call "$project" ActIfMyTurn)
            case "$out" in OK*) log "  $(name_of "$project"): $out"; sleep 2 ;; FAIL*) fail "$out"; return ;; esac
        done
        console_lines "$MAIN" | grep -q '\[GameDebugInput\].*피폭 — n 1 · k 2' && break
        sleep 2
    done
    log "▶ 판정"
    for project in "${EDITORS[@]}"; do
        lines=$(console_lines "$project" | grep '\[GameDebugInput\]' | grep -E '대소동 — |피폭 — ' | sed -E 's/.*(대소동 — |피폭 — )/\1/')
        log "  $(name_of "$project"): $(printf '%s\n' "$lines" | tr '\n' '/')"
        printf '%s' "$lines" | grep -q '대소동 — P[0-9]* · 새 판 1번째' || fail "$(name_of "$project") 새 판 줄이 없다"
        printf '%s' "$lines" | grep -q '피폭 — n 1 · k 1 · 모두 −1' || fail "$(name_of "$project") 피폭 1턴째 줄이 없다"
        if [ -z "$first" ]; then first="$lines"; elif [ "$lines" != "$first" ]; then fail "$(name_of "$project") 의 대소동 · 피폭 줄이 메인과 다르다"; fi
    done
    compare_turns
    check_table_ui restart
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 반응 시간의 스킵 버튼 (04-card.md 8절 · 10-ui.md 5절): 클론의 턴에 방장이 낼 수 있는 카운터를 쥐고, 클론이 방장을 노리는 카드를 낸다
# → 방장 화면에 초록 빛 + 「스킵」 버튼(남은 초 없음) → 누르면 버튼 · 빛이 꺼지고 → 스킵한 뒤의 카운터는 거절(코드 6).
run_skip() {
    local out project
    enter_game 2 || return
    log "▶ 클론의 턴에 방장에게 카운터를 쥐여 주고 클론이 카드를 낸다"
    for _ in $(seq 1 20); do
        out=$(call "$MAIN" ReactionSkipProbe arm)
        case "$out" in
            OK*) break ;;
            FAIL*) fail "$out"; return ;;
        esac
        # 방장의 턴이다 — 뽑아서 넘긴다
        call "$MAIN" ExpireTurn >/dev/null
        sleep 2
    done
    log "  $out"
    case "$out" in OK*) ;; *) fail "남의 턴이 오지 않았다: $out"; return ;; esac
    for _ in $(seq 1 8); do
        out=$(call "$MAIN" ReactionSkipProbe click)
        case "$out" in WAIT*) sleep 0.3 ;; *) break ;; esac
    done
    log "  $out"
    case "$out" in OK*) ;; *) fail "스킵 버튼: $out" ;; esac
    if [ -n "${SHOT_DIR:-}" ]; then mkdir -p "$SHOT_DIR"; log "  $(call "$MAIN" Shot "$SHOT_DIR/skip_after_$(name_of "$MAIN").png")"; fi
    out=$(call "$MAIN" ReactionSkipProbe after)
    log "  $out"
    case "$out" in OK*) ;; *) fail "스킵 뒤: $out" ;; esac
    sleep 6
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 실제 카운터 (11-card-list 1-3): 클론의 턴에 방장에게 2003 을 쥐여 주고 클론이 방장에게 행동 카드를 낸다 → 방장이 화면에서 카운터
# → 막힌 카드는 클론 손패로 · 두 화면의 손패 장수가 방장 값과 같다.
run_counter() {
    local out project arm="arm"
    [ "${1:-}" = "cost" ] && arm="arm2021" # 2021 으르렁 — 대가로 손패 1장 고르기까지
    [ "${1:-}" = "pick" ] && arm="arm2018" # 2018 물귀신 — 낼 때 화면에서 대상 1명 고르기
    [ "${1:-}" = "reflect" ] && arm="arm2002" # 2002 반사 — 막고 내가 쓴 사람에게 다시 쓴다
    enter_game 2 || return
    if [ "$arm" = "arm2018" ] && [ -n "${SHOT_DIR:-}" ]; then mkdir -p "$SHOT_DIR"; log "  $(call "$MAIN" ArmPickShot "$SHOT_DIR/pick_$(name_of "$MAIN").png")"; fi
    log "▶ 클론의 턴에 방장에게 실제 카운터를 쥐여 주고 클론이 카드를 낸다"
    for _ in $(seq 1 20); do
        out=$(call "$MAIN" ReactionSkipProbe $arm)
        case "$out" in
            OK*) break ;;
            FAIL*) fail "$out"; return ;;
        esac
        call "$MAIN" ExpireTurn >/dev/null
        sleep 2
    done
    log "  $out"
    case "$out" in OK*) ;; *) fail "남의 턴이 오지 않았다: $out"; return ;; esac
    for _ in $(seq 1 8); do
        out=$(call "$MAIN" ReactionSkipProbe play)
        case "$out" in WAIT*) sleep 0.3 ;; *) break ;; esac
    done
    log "  $out"
    case "$out" in OK*) ;; *) fail "카운터 내기: $out"; return ;; esac
    if [ "$arm" = "arm2018" ]; then
        log "▶ 대상 고르기 — 카운터를 내면 좌석 고르기가 열리고, 상대 좌석을 누르면 그 사람으로 나간다"
        for _ in $(seq 1 8); do
            out=$(call "$MAIN" ReactionSkipProbe pick)
            case "$out" in WAIT*) sleep 0.2 ;; *) break ;; esac
        done
        log "  $out"
        case "$out" in OK*) ;; *) fail "카운터 대상 고르기: $out"; return ;; esac
        for _ in $(seq 1 8); do
            out=$(call "$MAIN" ReactionSkipProbe pickclosed)
            case "$out" in WAIT*) sleep 0.2 ;; *) break ;; esac
        done
        log "  $out"
        case "$out" in OK*) ;; *) fail "대상 고른 뒤 체인 카드 되돌리기: $out"; return ;; esac
    fi
    if [ -n "${SHOT_DIR:-}" ]; then mkdir -p "$SHOT_DIR"; sleep 1; for project in "${EDITORS[@]}"; do log "  $(call "$project" Shot "$SHOT_DIR/counter_$(name_of "$project").png")"; done; fi
    if [ "$arm" = "arm2021" ]; then
        log "▶ 대가 — 고르기 판에 내 손패가 카드 칸으로 뜨고, 누르면 그 카드가 버려진다 (다른 화면은 「고르는 중」)"
        for _ in $(seq 1 60); do
            out=$(call "$MAIN" ReactionSkipProbe cost)
            case "$out" in
                WAIT*) sleep 0.5 ;;
                SHOT*) if [ -n "${SHOT_DIR:-}" ]; then for project in "${EDITORS[@]}"; do log "  $(call "$project" Shot "$SHOT_DIR/cost_$(name_of "$project").png")"; done; fi ;;
                *) break ;;
            esac
        done
        log "  $out"
        case "$out" in OK*) ;; *) fail "대가 고르기: $out"; return ;; esac
        for _ in $(seq 1 20); do
            out=$(call "$MAIN" ReactionSkipProbe costdone)
            case "$out" in WAIT*) sleep 0.5 ;; *) break ;; esac
        done
        log "  $out"
        case "$out" in OK*) ;; *) fail "대가 결과: $out" ;; esac
    fi
    for _ in $(seq 1 20); do
        out=$(call "$MAIN" ReactionSkipProbe countered)
        case "$out" in WAIT*) sleep 1 ;; *) break ;; esac
    done
    log "  $out"
    case "$out" in OK*) ;; *) fail "카운터 결과: $out" ;; esac
    sleep 4
    # 카운터 기록 줄 (10-ui 6절) — 2003 「— 손패로 돌아감」 · 2018 「도 같은 피해」 · 2021 「 무효」
    local line="— 손패로 돌아감"
    [ "$arm" = "arm2018" ] && line="도 같은 피해"
    [ "$arm" = "arm2021" ] && line=" 무효"
    [ "$arm" = "arm2002" ] && line="반사! →"
    log "▶ 기록에 카운터가 한 일의 줄이 있는가"
    for project in "${EDITORS[@]}"; do
        out=$(call "$project" HistoryHasPart "$line")
        log "  $(name_of "$project"): $out"
        case "$out" in OK*) ;; *) fail "$(name_of "$project") 기록: $out" ;; esac
    done
    log "▶ 화면의 함정 칸 · 손패 장수가 방장 값과 같은가"
    for project in "${EDITORS[@]}"; do
        out=$(call "$project" TableUiExpect)
        log "  $(name_of "$project"): $out"
        case "$out" in OK*) ;; *) fail "$(name_of "$project") 화면: $out" ;; esac
    done
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 시간 초과 (03-turn.md 4절): 방장이 클론의 턴만 실제 시간 초과로 끝낸다 (메인의 턴은 그냥 넘김) → 자동 뽑기 2번 → 3번째에 뽑지 않고 강제 퇴장
# → 클론은 스스로 방을 나가 로비 + 경고 팝업 (22-popup-ui.md 3절), 메인은 혼자 남아 처치 승리 → 결과 화면.
# 방장 이주 (09-network 9.1) — 메인(방장) + 클론(예비 방장) + 봇 1 (방장이 나가도 판이 남게).
#   migrate      카드가 나온 뒤 방장(메인)이 나감 → 클론이 사본으로 이어받아 봇과 판을 잇는다
#   migrate fail 사본 없이 방장이 나감 → 클론은 방으로 돌아오고 경고 팝업
play_turns() {
    local rounds="$1" project out done_rounds=0
    shift
    for _ in $(seq 1 $((rounds * 20))); do
        [ $done_rounds -ge "$rounds" ] && return 0
        for project in "$@"; do
            out=$(call "$project" ActIfMyTurn)
            case "$out" in
                OK*)   log "  $(name_of "$project"): $out"; done_rounds=$((done_rounds + 1)); LAST_ACT="$out"; sleep 2; break ;;
                FAIL*) fail "$(name_of "$project"): $out"; return 1 ;;
                *"게임 끝"*|*"(ResultScene)"*)
                    # END_OK=1 — 판이 끝나도 되는 구간 (이주 뒤: 새 방장이 판을 끝까지 판정한 것이다)
                    if [ "${END_OK:-0}" = 1 ]; then log "  $(name_of "$project"): 판이 끝남 — $out"; GAME_ENDED=1; return 0; fi
                    fail "$(name_of "$project"): 판이 먼저 끝났다 — $out"; dump_tail "$project"; return 1 ;;
            esac
        done
        sleep 1
    done
    [ $done_rounds -ge "$rounds" ] || { fail "턴이 진행되지 않는다 (${done_rounds}/${rounds})"; return 1; }
}

# 판이 뜻밖에 끝났을 때 — 왜 끝났는지 콘솔의 마지막 줄들을 남긴다
dump_tail() {
    console_lines "$1" | grep -E 'GameDebugInput|PunGameServer|BotDriver' | tail -12 | sed -E 's/^[a-z-]*	//' | cut -c1-260 | sed 's/^/      /'
}

run_migrate() {
    local mode="${1:-}" project out clone lines before after
    EDITORS=("$MAIN" "$ROOT/MuffinProject_clone_0")
    clone="${EDITORS[1]}"
    log "▶ 준비 — 클라이언트 2개 + 봇 1"
    prepare "$LOBBY_SCENE"
    log "▶ 입장 · 봇 추가 · 시작"
    for project in "${EDITORS[@]}"; do
        until_ok "$project" Join || return
        sleep 2
    done
    until_ok "$MAIN" RoomCount 2 || return
    out=$(call "$MAIN" AddBot); log "  메인: $out"
    until_ok "$MAIN" ExpectBots 1 3 False || return
    until_ok "$MAIN" StartGame 2 || return
    wait_scene GameScene || return
    sleep 2

    log "▶ 방장이 있을 때 — 카드가 나올 때까지 턴 진행"
    LAST_ACT=""
    for _ in 1 2 3 4 5 6; do
        play_turns 1 "${EDITORS[@]}" || return
        printf '%s' "$LAST_ACT" | grep -q '카드' && break
    done
    log "  메인: $(call "$MAIN" MigrateProbe state) / $(name_of "$clone"): $(call "$clone" MigrateProbe state)"
    call "$clone" MigrateProbe state | grep -q '방장 False · 사본 True' || { fail "예비 방장(클론)이 사본을 받지 않았다"; return; }
    call "$MAIN" MigrateProbe state | grep -q '사본 False' || fail "방장이 사본을 갖고 있다"

    if [ "$mode" = "fail" ]; then
        log "▶ 사본 없이 방장이 나감 → 방으로 돌아옴"
        until_ok "$MAIN" MigrateProbe stop || return
        sleep 1
        log "  $(name_of "$clone"): $(call "$clone" MigrateProbe drop)"
        log "  메인: $(call "$MAIN" MigrateProbe leave)"
        wait_scene_one "$clone" RoomScene || return
        for _ in $(seq 1 10); do
            out=$(call "$clone" WarningPopupText)
            case "$out" in OK*) break ;; esac
            sleep 1
        done
        log "  $(name_of "$clone"): $out"
        [ "$out" = "OK 방으로 돌아왔어요 / 게임을 이어갈 수 없어 방으로 돌아왔어요." ] || fail "방으로 돌아온 뒤의 경고 팝업이 다르다: $out"
        out=$(call "$clone" ExpectBots 1 3 False); log "  $(name_of "$clone"): $out"
        case "$out" in *"열림 True"*) ;; *) fail "돌아온 방이 열려 있지 않다 — 다시 시작할 수 없다: $out" ;; esac
        console_lines "$clone" | grep -q '방장 이주 실패' || fail "$(name_of "$clone") 이주 실패 로그가 없다"
        # 이주 실패는 LogError 로 남긴다 — 그 줄만 빼고 본다
        lines=$(console_lines "$clone" | awk -F'\t' '$1 == "error"' | grep -v '방장 이주 실패')
        [ -z "$lines" ] && log "  $(name_of "$clone"): 다른 콘솔 에러 0" || { fail "$(name_of "$clone") 콘솔 에러:"; printf '%s\n' "$lines" | sed 's/^/      /'; }
        return
    fi

    log "▶ 처리 도중 방장(메인)이 나감"
    before=$(console_lines "$clone" | grep -c '\[BotDriver\]')
    log "  메인: $(call "$MAIN" MigrateProbe leave)"
    sleep 6
    out=$(call "$clone" MigrateProbe state); log "  $(name_of "$clone"): $out"
    case "$out" in *"방장 True"*"판 보유 True"*) ;; *) fail "예비 방장이 판을 이어받지 않았다: $out"; return ;; esac
    console_lines "$clone" | grep -q '방장 이주 — 사본으로 판을 이어받았다' || fail "$(name_of "$clone") 이주 로그가 없다"
    sleep 12   # 멈췄던 체인(반응 시간을 새로) · 후처리가 끝날 때까지

    log "▶ 클론이 봇과 판을 잇는다 — 클론 턴 3번"
    GAME_ENDED=0
    END_OK=1 play_turns 3 "$clone" || return
    [ "$GAME_ENDED" = 1 ] || sleep 12
    log "▶ 판정"
    lines=$(console_lines "$clone" | grep '\[GameDebugInput\]')
    printf '%s' "$lines" | grep -q '게임 중 나감' || fail "$(name_of "$clone") 앞 방장의 나감 줄이 없다"
    after=$(console_lines "$clone" | grep -c '\[BotDriver\]')
    log "  $(name_of "$clone"): 봇 이유 줄 ${before} → ${after} (새 방장 기기에서 봇이 판단한다)"
    [ "$after" -gt "$before" ] || fail "이주 뒤 봇이 행동하지 않는다"
    log "  마지막 턴 줄: $(printf '%s\n' "$lines" | grep ' 턴 P' | tail -1 | sed -E 's/.*\[GameDebugInput\] //')"
    # 판이 끝났으면 게임 화면이 없다 — 화면 비교 · 기록 서랍은 건너뛴다
    if [ "$GAME_ENDED" = 1 ] || printf '%s' "$lines" | grep -q '게임 종료'; then
        log "  $(name_of "$clone"): 이어받은 판이 끝남 — 화면 확인은 건너뜀"
    else
        until_ok "$clone" TableUiExpect || return
        out=$(call "$clone" HistoryHas "방장이 바뀌었어요"); log "  $(name_of "$clone"): $out"
        case "$out" in OK*) ;; *) fail "기록 서랍에 「방장이 바뀌었어요」 가 없다" ;; esac
    fi
    check_errors "$clone"
}

# 방장이 나간 뒤 남은 사람(클론)이 방 밖으로 나와 경고 팝업을 보는가 — 방 해체 · 결과 화면 방장 이탈
expect_disbanded() {
    local clone="$1" out
    for _ in $(seq 1 20); do
        out=$(call "$clone" WarningPopupText)
        case "$out" in OK*) break ;; esac
        sleep 1
    done
    log "  $(name_of "$clone"): $out · $(call "$clone" InRoom)"
    [ "$out" = "OK 방이 사라졌어요 / 방장이 나가서 방이 사라졌어요." ] || fail "방장이 나간 뒤의 경고 팝업이 다르다: $out"
    call "$clone" InRoom | grep -q '방 안 False' || fail "$(name_of "$clone") 이 아직 방에 있다"
    check_errors "$clone"
}

# migrate room — 방 대기 중 방장이 나감 → 방 해체 (08-room 5절 #5)
run_migrate_room() {
    local project clone
    EDITORS=("$MAIN" "$ROOT/MuffinProject_clone_0"); clone="${EDITORS[1]}"
    log "▶ 준비 — 클라이언트 2개"
    prepare "$LOBBY_SCENE"
    for project in "${EDITORS[@]}"; do until_ok "$project" Join || return; sleep 2; done
    until_ok "$MAIN" RoomCount 2 || return
    wait_scene RoomScene || return
    log "▶ 방 대기 중 방장(메인)이 나감"
    log "  메인: $(call "$MAIN" LeaveRoomNow)"
    expect_disbanded "$clone"
}

# migrate result — 결과 화면에서 방장이 나감 → 전원 로비로 (07 11절)
run_migrate_result() {
    local clone
    enter_game 2 || return
    clone="${EDITORS[1]}"
    until_ok "$MAIN" SetAllHp 5 || return
    play_to_end 2 || return
    wait_scene ResultScene || return
    sleep 3
    log "▶ 결과 화면에서 방장(메인)이 나감"
    log "  메인: $(call "$MAIN" LeaveRoomNow)"
    expect_disbanded "$clone"
}

# migrate kick — 방장이 3턴 연속 시간 초과로 강제 퇴장 → 클론이 이어받아 봇과 판을 잇는다 (03-turn 4절 · 09 9.1)
# 봇은 시간 초과 턴 동안 손패를 모아 찹츄로 이길 수 있다 — 강제 퇴장 전에 판이 끝나면 볼 것이 없으므로 다시 한다 (3 = 다시)
run_migrate_kick() {
    local try
    for try in 1 2 3; do
        migrate_kick_once; [ $? -eq 3 ] || return
        log "  다시 시도 ($try)"
    done
    fail "세 번 모두 강제 퇴장 전에 판이 끝났다"
}

migrate_kick_once() {
    local project out clone
    EDITORS=("$MAIN" "$ROOT/MuffinProject_clone_0"); clone="${EDITORS[1]}"
    log "▶ 준비 — 클라이언트 2개 + 봇 1"
    prepare "$LOBBY_SCENE"
    for project in "${EDITORS[@]}"; do until_ok "$project" Join || return; sleep 2; done
    until_ok "$MAIN" RoomCount 2 || return
    log "  메인: $(call "$MAIN" AddBot)"
    until_ok "$MAIN" ExpectBots 1 3 False || return
    until_ok "$MAIN" StartGame 2 || return
    wait_scene GameScene || return
    sleep 2
    log "▶ 방장(메인)의 턴만 시간 초과로 — 3번째에 강제 퇴장"
    for _ in $(seq 1 40); do
        out=$(call "$MAIN" ExpireTurn mine)
        case "$out" in OK*) log "  ${out%% — *}" ;; *) log "  $out"; break ;; esac
        sleep 3
        console_lines "$clone" | grep -q '\[GameDebugInput\].*강제 퇴장' && break
    done
    if ! console_lines "$clone" | grep -q '\[GameDebugInput\].*P1 게임 중 강제 퇴장'; then
        if console_lines "$clone" | grep -q '\[GameDebugInput\].*게임 종료'; then
            log "  강제 퇴장 전에 판이 끝났다 — $(console_lines "$clone" | grep '게임 종료' | tail -1 | sed -E 's/.*게임 종료/게임 종료/' | cut -c1-80)"
            return 3
        fi
        fail "방장이 강제 퇴장되지 않았다"; dump_tail "$clone"; return
    fi
    sleep 6
    for _ in $(seq 1 10); do
        out=$(call "$MAIN" WarningPopupText)
        case "$out" in OK*) break ;; esac
        sleep 1
    done
    log "  메인: $out"
    [ "$out" = "OK 방에서 나왔어요 / 3턴 동안 행동하지 않아 게임에서 나왔어요." ] || fail "강제 퇴장된 방장의 경고 팝업이 다르다: $out"
    out=$(call "$clone" MigrateProbe state); log "  $(name_of "$clone"): $out"
    console_lines "$clone" | grep -q '방장 이주 — 사본으로 판을 이어받았다' || { fail "$(name_of "$clone") 이주 로그가 없다"; dump_tail "$clone"; return; }
    # 이어받은 뒤 판이 벌써 끝났으면(봇의 찹츄 등 — 새 방장만 찍는 종료 줄) 이어받은 것이다
    if console_lines "$clone" | grep -q '\[PunGameServer\] 게임 종료'; then
        log "  $(name_of "$clone"): 이어받은 판이 끝남 — $(console_lines "$clone" | grep '\[GameDebugInput\].*게임 종료' | tail -1 | sed -E 's/.*게임 종료/게임 종료/' | cut -c1-80)"
        check_errors "$clone"
        return
    fi
    case "$out" in *"방장 True"*"판 보유 True"*) ;; *) fail "클론이 판을 이어받지 않았다: $out"; dump_tail "$clone"; return ;; esac
    log "▶ 클론이 봇과 판을 잇는다 — 클론 턴 2번"
    END_OK=1 play_turns 2 "$clone" || return
    sleep 8
    log "  마지막 턴 줄: $(console_lines "$clone" | grep '\[GameDebugInput\]' | grep ' 턴 P' | tail -1 | sed -E 's/.*\[GameDebugInput\] //')"
    # 그 사이 판이 끝났으면(봇의 찹츄 등) 화면 비교는 건너뛴다 — 이주 뒤 판이 끝까지 간 것이다
    if console_lines "$clone" | grep -q '\[GameDebugInput\].*게임 종료'; then
        log "  $(name_of "$clone"): 판이 끝남 — $(console_lines "$clone" | grep '게임 종료' | tail -1 | sed -E 's/.*게임 종료/게임 종료/')"
    else
        until_ok "$clone" TableUiExpect || return
    fi
    check_errors "$clone"
}

run_timeout() {
    local clone out lines project
    enter_game 2 || return
    clone="${EDITORS[1]}"
    log "▶ 클론의 턴을 시간 초과로 (메인의 턴은 그냥 넘김)"
    for _ in $(seq 1 30); do
        out=$(call "$MAIN" ExpireTurn others)
        case "$out" in FAIL*) fail "$out"; return ;; OK*) log "  $out" ;; *) break ;; esac
        sleep 3
        console_lines "$MAIN" | grep -q '\[GameDebugInput\].*강제 퇴장' && break
    done
    log "▶ 판정"
    for project in "${EDITORS[@]}"; do
        lines=$(console_lines "$project" | grep '\[GameDebugInput\]')
        printf '%s' "$lines" | grep -q '게임 중 강제 퇴장' || fail "$(name_of "$project") 강제 퇴장 줄이 없다"
        printf '%s' "$lines" | grep -q '게임 중 나감' && fail "$(name_of "$project") 강제 퇴장이 보통 나감으로 찍혔다"
    done
    wait_scene_one "$clone" LobbyScene || return
    for _ in $(seq 1 10); do
        out=$(call "$clone" WarningPopupText)
        case "$out" in OK*) break ;; esac
        sleep 1
    done
    log "  $(name_of "$clone"): $out"
    [ "$out" = "OK 방에서 나왔어요 / 3턴 동안 행동하지 않아 게임에서 나왔어요." ] || fail "클론의 로비 경고 팝업이 다르다: $out"
    wait_scene_one "$MAIN" ResultScene || return
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 1021 개판 오분전: 방장이 턴 주인에게 1021 을 쥐여 쓰게 한 뒤 턴을 시간 초과로 빨리 돌린다 → 모든 화면에 자명종 「5」 · 같은 바퀴 · 판정 줄 · 같은 결과 · 판정 뒤 자명종 없음 (SHOT_DIR 이면 제목 · 자명종 · 마지막 바퀴 · 판정 팻말 스크린샷).
run_gaepan() {
    local clients="${1:-2}" project out lines first="" ended=""
    enter_game "$clients" || return
    if [ -n "${SHOT_DIR:-}" ]; then
        mkdir -p "$SHOT_DIR"
        for project in "${EDITORS[@]}"; do
            call "$project" ArmGaepanShots "$SHOT_DIR/gaepan_$(name_of "$project")" >/dev/null
        done
    fi
    until_ok "$MAIN" ForceCard 1021 || return
    log "▶ 처리 대기 (시작 제목 → 자명종)"
    sleep 16
    for project in "${EDITORS[@]}"; do
        out=$(call "$project" GaepanHud)
        log "  $(name_of "$project"): 자명종 $out"
        [ "$out" = "OK HUD 5" ] || fail "$(name_of "$project") 자명종이 「5」 로 보이지 않는다: $out"
    done
    log "▶ 턴을 시간 초과로 넘겨 5바퀴"
    for _ in $(seq 1 80); do
        out=$(call "$MAIN" ExpireTurn)
        case "$out" in FAIL*) fail "$out"; return ;; esac
        sleep 2
        console_lines "$MAIN" | grep -q '\[GameDebugInput\].*개판 판정 — ' && break
    done
    log "▶ 판정 공개 · 사망 처리 대기"
    sleep 20
    for project in "${EDITORS[@]}"; do
        out=$(call "$project" GaepanHud)
        case "$out" in "OK NONE"|"WAIT 게임 화면 아님") ;; *) fail "$(name_of "$project") 판정 뒤에도 자명종이 남았다: $out" ;; esac # 2인 판은 판정 사망으로 끝나 결과 화면으로 넘어간다
    done
    log "▶ 판정"
    for project in "${EDITORS[@]}"; do
        lines=$(console_lines "$project" | grep '\[GameDebugInput\]' | grep -E '개판 — |개판 판정 — ' | sed -E 's/.*(개판 — |개판 판정 — )/\1/')
        ended=$(console_lines "$project" | grep '\[GameDebugInput\].*게임 종료 — ' | tail -1 | sed -E 's/.*게임 종료 — ([^·]*· 승자 P-?[0-9]+).*/\1/')
        log "  $(name_of "$project"): $(printf '%s\n' "$lines" | tr '\n' '/') · 「${ended:-끝 없음}」"
        printf '%s' "$lines" | grep -q '5바퀴 남음' || fail "$(name_of "$project") 개판 시작 줄이 없다"
        printf '%s' "$lines" | grep -q '0바퀴 남음' || fail "$(name_of "$project") 개판 끝 줄이 없다"
        printf '%s' "$lines" | grep -q '개판 판정 — ' || fail "$(name_of "$project") 판정 줄이 없다"
        if [ -z "$first" ]; then first="$lines|$ended"; elif [ "$lines|$ended" != "$first" ]; then fail "$(name_of "$project") 의 개판 줄 · 결과가 메인과 다르다"; fi
    done
    compare_turns
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 1019 목표 조준: 방장이 턴 주인에게 1019 를 쥐여 상대에게 쓰게 한다 → 대상 화면에 고르기 판 · 다른 화면에 「고르는 중」 → 대상이 판의 「피해 받기」 를 누른다(화면 경로)
# → 모든 화면에 같은 고르기 줄 · 표가 방장 값과 같은지.
run_aim() {
    local clients="${1:-2}" project out lines shot first="" opened="" seen=""
    enter_game "$clients" || return
    # 5초 안에 눌러야 해서 고르기는 에디터 안에서 한다 (ArmChoice) — CLI 부름은 한 번에 몇 초라 늦다.
    for project in "${EDITORS[@]}"; do
        shot="-"
        [ -n "${SHOT_DIR:-}" ] && { mkdir -p "$SHOT_DIR"; shot="$SHOT_DIR/aim_choice_$(name_of "$project").png"; }
        call "$project" ArmChoice 1 "$shot" >/dev/null
    done
    until_ok "$MAIN" ForceCard 1019 || return
    log "▶ 고르기 판 (반응 시간 · 체인 처리 뒤 시작)"
    for project in "${EDITORS[@]}"; do
        for _ in $(seq 1 30); do
            out=$(call "$project" ChoiceResult)
            case "$out" in WAIT*) sleep 1 ;; *) break ;; esac
        done
        log "  $(name_of "$project"): $out"
        case "$out" in
            OPEN*) opened="$project" ;;
            SEEN*) seen="$project" ;;
            *) fail "$(name_of "$project"): $out" ;;
        esac
    done
    [ -n "$opened" ] || { fail "고르기 판을 누른 화면이 없다"; return; }
    [ "$clients" -lt 2 ] || [ -n "$seen" ] || fail "다른 화면에 「고르는 중」 이 보이지 않았다"
    sleep 12
    log "▶ 판정"
    for project in "${EDITORS[@]}"; do
        lines=$(console_lines "$project" | grep '\[GameDebugInput\]' | grep -E '고르기 시작 — |고르기 — ' | sed -E 's/.*(고르기 시작 — |고르기 — )/\1/')
        log "  $(name_of "$project"): $(printf '%s\n' "$lines" | tr '\n' '/')"
        printf '%s' "$lines" | grep -q '고르기 — P[0-9]* 피해 받기$' || fail "$(name_of "$project") 「피해 받기」 줄이 없다 (시간 초과면 '(시간 초과)')"
        if [ -z "$first" ]; then first="$lines"; elif [ "$lines" != "$first" ]; then fail "$(name_of "$project") 의 고르기 줄이 메인과 다르다"; fi
    done
    compare_turns
    check_table_ui aim
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 카드 연출 한 장 — 게임 판 · 네트워크 없이 연출 시험장에서 (CardFxTestBench). 3 단계가 시작되면 SHOT_DIR/fx_<ID>_<ms>.png 를 찍고,
# 3 단계 · 4 단계 장식 · 버림까지 끝나길 기다린 뒤 콘솔 오류. 3 단계 길이는 콘솔의 「[CardFxTestBench] 카드 <ID> … 3 단계 n초」.
run_fx() {
    local id="${1:?카드 ID}" times="${2:-300,800,1500}" prefix="-" out seen=""
    EDITORS=("$MAIN")
    log "▶ 준비 — 연출 시험장"
    prepare "$FX_SCENE"
    [ -n "${SHOT_DIR:-}" ] && { mkdir -p "$SHOT_DIR"; prefix="$SHOT_DIR/fx_$id"; }
    until_ok "$MAIN" ArmFxShots "$prefix" "$id" "$times" || return
    until_ok "$MAIN" FxPlay "$id" || return
    log "▶ 연출 대기"
    for _ in $(seq 1 90); do
        out=$(call "$MAIN" FxState "$id")
        case "$out" in SEEN*) seen="$out" ;; "OK IDLE") break ;; esac
        sleep 1
    done
    [ "$out" = "OK IDLE" ] || fail "연출이 끝나지 않는다: $out"
    [ -n "$seen" ] || log "  (3 단계 없음 — 연출 프리팹이 없거나 4 단계만 있는 카드)"
    console_lines "$MAIN" | grep '\[CardFxTestBench\]' | sed 's/^[a-z]*	/  /'
    check_errors "$MAIN"
}

case "${1:-}" in
    rules) shift; run_rules "$@" ;;
    migrate) shift
        case "${1:-}" in
            room)   run_migrate_room ;;
            result) run_migrate_result ;;
            kick)   run_migrate_kick ;;
            *)      run_migrate "$@" ;;
        esac ;;
    ui)    shift; run_ui ;;
    multi) shift; run_multi "$@" ;;
    full)  shift; run_full "$@" ;;
    solo)  shift; run_solo "$@" ;;
    chapchu) shift; run_chapchu "$@" ;;
    restart) shift; run_restart "$@" ;;
    gaepan)  shift; run_gaepan "$@" ;;
    timeout) shift; run_timeout ;;
    skip)    shift; run_skip ;;
    counter) shift; run_counter "$@" ;;
    aim)     shift; run_aim "$@" ;;
    fx)      shift; run_fx "$@" ;;
    *)     sed -n '2,10p' "$0"; exit 2 ;;
esac

if [ $FAILED -eq 0 ]; then
    log "■ 통과"
    exit 0
fi
log "■ 실패"
exit 1
