#!/usr/bin/env bash
# PostToolUse(Edit|Write|MultiEdit) — 프로젝트 .cs 파일 검사. (CLAUDE.md 10 · 11 · 12 · 13절)
#  1. BOM 이 없으면 붙인다.
#  2. 금지 패턴이 있으면 exit 2 로 Claude 에게 되돌려 고치게 한다.

input=$(cat)
path=$(printf '%s' "$input" | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 \
  | sed 's/.*:[[:space:]]*"\(.*\)"/\1/; s#\\\\#/#g')

case "$path" in
  *MuffinProject/Assets/Scripts/*.cs) ;;
  *) exit 0 ;;
esac
[ -f "$path" ] || exit 0

# 1. UTF-8 BOM
if [ "$(head -c3 "$path" | od -An -tx1 | tr -d ' \n')" != "efbbbf" ]; then
  tmp="$path.bomtmp"
  { printf '\xEF\xBB\xBF'; cat "$path"; } > "$tmp" && mv "$tmp" "$path"
fi

# 2. 금지 패턴 — 주석(// 이후)은 빼고 검사
code=$(sed $'1s/^\xEF\xBB\xBF//; s#//.*$##' "$path")   # 첫 줄 BOM 을 떼야 ^using 이 걸린다
problems=""
check() { # $1 = 정규식, $2 = 설명
  hits=$(printf '%s\n' "$code" | grep -nE "$1")
  [ -n "$hits" ] && problems="$problems\n- $2\n$(printf '%s\n' "$hits" | sed 's/^/    /')"
}

check '\.RPC\(\s*"'                              'RPC 이름은 nameof(RPC_Xxx) 로 넘긴다 (12절)'
check '\basync\s+void\b'                         'async void 금지 — 코루틴 또는 async Task (12절)'
check '\[SerializeField\][^;]*\bstatic\b'        '[SerializeField] static 은 직렬화되지 않는다 (12절)'
check '^\s*namespace\s+[A-Za-z0-9_.]+\s*;'       'file-scoped namespace 는 C# 10 — C# 9 문법만 (10절)'
check '\brecord\s+struct\b|^\s*global\s+using\b' 'C# 10+ 문법 금지 (10절)'

case "$path" in
  */Game/Server/*)
    check '^\s*using\s+(UnityEngine|Photon)'     'GameServer 는 순수 C# — UnityEngine · Photon 참조 금지 (11절)'
    check '\bGameEvents\b'                       'GameServer 는 GameEvents 를 부르지 않는다 — IServerOutbox 로만 (11절)'
    ;;
esac

if [ -n "$problems" ]; then
  printf "지침 위반 (%s):%b\n" "$path" "$problems" >&2
  exit 2
fi
exit 0
