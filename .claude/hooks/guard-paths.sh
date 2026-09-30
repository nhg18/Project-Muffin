#!/usr/bin/env bash
# PreToolUse(Edit|Write|MultiEdit) — 건드리면 안 되는 경로를 막는다. (CLAUDE.md 13 · 14절)
# exit 2 → 도구 실행 차단, stderr 가 Claude 에게 전달된다.

input=$(cat)
path=$(printf '%s' "$input" | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 \
  | sed 's/.*:[[:space:]]*"\(.*\)"/\1/; s#\\\\#/#g')
[ -z "$path" ] && exit 0

block() { echo "차단: $1 ($path)" >&2; exit 2; }

case "$path" in
  *_clone_*/*)            block "ParrelSync 복제본은 고치지 않는다. MuffinProject/ 에서 작업한다" ;;
  */Library/*|*/Temp/*)   block "Unity 생성 폴더(Library/Temp)는 고치지 않는다" ;;
  *.meta)                 block ".meta 는 Unity 가 만든다. 손으로 만들거나 고치지 않는다 (GUID 가 바뀌면 참조가 끊긴다)" ;;
esac

case "$path" in
  *.unity|*.prefab)
    dir=$(dirname "$path")
    branch=$(git -C "$dir" rev-parse --abbrev-ref HEAD 2>/dev/null)
    case "$branch" in
      logic|logic/*) block "씬 · 프리팹은 ui 트랙에서만 바꾼다 (현재 브랜치: $branch)" ;;
    esac
    ;;
esac

exit 0
