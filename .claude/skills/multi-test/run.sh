#!/usr/bin/env bash
# 찹츄 멀티 테스트 — 열린 Unity 에디터를 Unity CLI 로 조종해 결과를 비교한다. 사용법은 SKILL.md.
#
#   run.sh rules [인원=4] [턴=8]      규칙 계층만 (네트워크 · 플레이 모드 없음)
#   run.sh ui                          한 클라이언트: 타이틀 → 로비 → 경고 팝업 · 토스트 → 방 → 나가기
#   run.sh multi [클라이언트=2] [턴=4]  메인 + 클론: 디버그 로비 → 방 → 게임, 턴마다 행동, 화면끼리 비교
#   run.sh full [클라이언트=2] [HP=20]   메인 + 클론: 체력을 낮춰 한 판을 끝까지 → 함정 · 사망 · 게임 종료 · 방 복귀
#   run.sh solo [HP=30]                 메인 혼자 + 봇 3: 혼자 하기 → 봇 제거 · 추가 → 한 판 끝까지 → 방 복귀
#
# 종료 코드: 0 통과 · 1 실패 · 2 준비 안 됨 (저장 안 한 씬, 에디터 없음, 컴파일 실패)

set -u

SKILL_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SKILL_DIR/../../.." && pwd)"
MAIN="$ROOT/MuffinProject"
CS="$SKILL_DIR/MultiTest.cs"
LOBBY_SCENE="Assets/Scenes/DebugLobbyScene.unity"
TITLE_SCENE="Assets/Scenes/TitleScene.unity"
WAIT_TRIES=30   # 대기 단계 최대 시도 (2초 간격)

FAILED=0
EDITORS=()
PROBED=0   # multi 의 카운터 요청: 0 안 함 · 1 행동 카드로(코드 8 기대) · 2 카운터 카드로(무효 기대)

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
    for scenario in "Chain:체인 · 카운터" "Death:사망 · 빈 덱 피해 · 나감" "Win:찹츄 · 승리 · 종료" "Trap:함정" "Bot:봇" "Effects:카드 효과"; do
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

# 모든 클라이언트가 그 씬에 올 때까지
wait_scene() {
    local scene="$1" project out
    for project in "${EDITORS[@]}"; do
        for _ in $(seq 1 $WAIT_TRIES); do
            out=$(call "$project" Ui scene)
            [ "$out" = "OK $scene" ] && break
            sleep 2
        done
        [ "$out" = "OK $scene" ] || { fail "$(name_of "$project") $scene 에 오지 않음: $out"; return 1; }
    done
}

run_full() {
    local clients="${1:-2}" hp="${2:-20}" project out ended=0 line first=""
    enter_game "$clients" || return
    until_ok "$MAIN" SetAllHp "$hp" || return

    log "▶ 한 판 끝까지 — 체력 ${hp} 에서 시작, 차례인 클라이언트가 행동"
    for _ in $(seq 1 $((clients * 100))); do  # 4인은 판이 길다 — 인원에 비례
        for project in "${EDITORS[@]}"; do
            out=$(call "$project" ActIfMyTurn)
            case "$out" in
                OK*)   log "  $(name_of "$project"): $out"; sleep 2; break ;;
                FAIL*) fail "$(name_of "$project"): $out"; return ;;
                *"게임 끝"*) ended=1 ;;
            esac
        done
        [ $ended -eq 1 ] && break
        # 함정 시간이면 맞은 사람이 함정을 낸다
        for project in "${EDITORS[@]}"; do
            out=$(call "$project" TrapProbe)
            case "$out" in OK*) log "  $(name_of "$project"): $out" ;; esac
        done
        sleep 1
    done
    [ $ended -eq 1 ] || { fail "게임이 끝나지 않았다"; return; }

    log "▶ 판정"
    for project in "${EDITORS[@]}"; do
        line=$(console_lines "$project" | grep '\[GameDebugInput\].*게임 종료 — ' | tail -1 | sed -E 's/.*게임 종료 — ([^·]*· 승자 P-?[0-9]+).*/\1/')
        log "  $(name_of "$project"): 「${line:-없음}」 · 사망 $(console_lines "$project" | grep -c '\[GameDebugInput\].* 사망 · 버림')번 · 함정 설치 $(console_lines "$project" | grep -c '\[GameDebugInput\].* 함정 설치 — ')번 · 함정 발동 $(console_lines "$project" | grep -c '\[GameDebugInput\].* 함정 발동 — ')번"
        [ -n "$line" ] || fail "$(name_of "$project") 게임 종료를 받지 못했다"
        if [ -z "$first" ]; then first="$line"; elif [ "$line" != "$first" ]; then fail "$(name_of "$project") 의 결과가 메인과 다르다"; fi
    done
    compare_turns
    wait_scene RoomScene && log "  결과 표시 뒤 전원 방으로 돌아옴"
    sleep 2
    for project in "${EDITORS[@]}"; do
        check_errors "$project"
    done
}

# 사람 1 + 봇 3 (18-bot.md 10절): 혼자 하기 → 봇 제거 · 다시 추가 → 시작 → 체력을 낮춰 한 판 끝까지 → 방 복귀 (봇 그대로 · 비공개 그대로)
run_solo() {
    local hp="${1:-30}" out ended=0 line bots
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
    until_ok "$MAIN" StartGame 1 || return
    wait_scene GameScene || return
    sleep 2
    until_ok "$MAIN" SetAllHp "$hp" || return

    log "▶ 한 판 끝까지 — 봇은 방장 기기에서 스스로, 사람은 차례에 행동"
    for _ in $(seq 1 600); do
        out=$(call "$MAIN" ActIfMyTurn)
        case "$out" in
            OK*)   log "  $out" ;;
            FAIL*) fail "$out"; return ;;
            *"게임 끝"*) ended=1; break ;;
        esac
        out=$(call "$MAIN" TrapProbe)
        case "$out" in OK*) log "  $out" ;; esac
        sleep 1
    done
    [ $ended -eq 1 ] || { fail "게임이 끝나지 않았다"; return; }

    log "▶ 판정"
    line=$(console_lines "$MAIN" | grep '\[GameDebugInput\].*게임 종료 — ' | tail -1 | sed -E 's/.*게임 종료 — ([^·]*· 승자 P-?[0-9]+).*/\1/')
    bots=$(console_lines "$MAIN" | grep -c '\[BotDriver\]')
    log "  「${line:-없음}」 · 봇 행동 이유 ${bots}줄 · 사망 $(console_lines "$MAIN" | grep -c '\[GameDebugInput\].* 사망 · 버림')번"
    console_lines "$MAIN" | grep '\[BotDriver\]' | head -8 | sed -E 's/^[^\t]*\t/      /'
    [ -n "$line" ] || fail "게임 종료를 받지 못했다"
    [ "$bots" -ge 3 ] || fail "봇 행동 이유 로그가 적다 (${bots}줄)"
    if console_lines "$MAIN" | grep '\[BotDriver\]' | grep -q '7-1 #[0-9]'; then log "  봇이 7-1 표로 행동"; else fail "봇의 7-1 행동 줄이 없다"; fi
    wait_scene RoomScene && log "  결과 표시 뒤 방으로 돌아옴"
    until_ok "$MAIN" ExpectBots "1,2,3" 1 False && log "  봇 3 그대로 · 혼자 하기 방은 비공개 그대로"
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

    log "▶ 턴 ${rounds}번 — 차례인 클라이언트가 행동"
    local done_rounds=0 acted probed=0
    for _ in $(seq 1 $((rounds * 10))); do
        [ $done_rounds -ge "$rounds" ] && break
        acted=0
        for project in "${EDITORS[@]}"; do
            out=$(call "$project" ActIfMyTurn)
            case "$out" in
                OK*)   log "  $(name_of "$project"): $out"; acted=1; done_rounds=$((done_rounds + 1)); sleep 2; break ;;
                FAIL*) fail "$(name_of "$project"): $out"; return ;;
            esac
        done
        # 처음 카드가 나온 뒤 한 번 — 연출(1.46초) 뒤 반응 시간에 남의 턴 클라이언트가 카운터 요청 (04-card.md 7 · 8절)
        if [ $acted -eq 1 ] && [ $probed -eq 0 ] && printf '%s' "$out" | grep -q '카드'; then
            for project in "${EDITORS[@]}"; do
                out=$(call "$project" CounterProbe)
                case "$out" in
                    *"(카운터 카드)"*) log "  $(name_of "$project"): $out"; probed=2; break ;;
                    OK*)              log "  $(name_of "$project"): $out"; probed=1; break ;;
                esac
            done
        fi
        [ $acted -eq 0 ] && sleep 1
    done
    PROBED=$probed
    # 마지막 카드의 체인 처리 · 후처리(등장 1.46 + 반응 5 + 후처리 1.28초)가 모든 화면에 닿을 때까지
    sleep 10

    log "▶ 내 카드 다시 받기 · 묵은 처리 번호 (09-network.md 5 · 10절)"
    for project in "${EDITORS[@]}"; do
        until_ok "$project" SyncProbe || return
        until_ok "$project" StaleProbe || return
    done
    sleep 3

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
    # 카운터 카드를 냈으면 행동 카드가 무효, 아니면 거절 코드 8 (CounterProbe)
    local pattern='내 요청 거절 — 코드 8' what='카운터 요청 거절 (코드 8)'
    if [ "$PROBED" -eq 2 ]; then pattern=' 카드 무효 — '; what='카운터로 행동 카드 무효'; fi
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

case "${1:-}" in
    rules) shift; run_rules "$@" ;;
    ui)    shift; run_ui ;;
    multi) shift; run_multi "$@" ;;
    full)  shift; run_full "$@" ;;
    solo)  shift; run_solo "$@" ;;
    *)     sed -n '2,9p' "$0"; exit 2 ;;
esac

if [ $FAILED -eq 0 ]; then
    log "■ 통과"
    exit 0
fi
log "■ 실패"
exit 1
