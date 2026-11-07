#!/usr/bin/env bash
# 리뷰 게이트 설치 (팀원마다 한 번씩 실행 — git hook은 저장소에 같이 올라가지 않음)
set -euo pipefail
ROOT="$(git rev-parse --show-toplevel)"
cd "$ROOT"

HOOK="$(git rev-parse --git-path hooks/pre-push)"
mkdir -p "$(dirname "$HOOK")"

# 기존 pre-push가 있으면 (Git LFS 등) 지우지 않고 .local로 옮겨서 이어서 실행되게 함
if [ -f "$HOOK" ] && ! grep -q 'review-gate' "$HOOK"; then
  mv "$HOOK" "$HOOK.local"
  chmod +x "$HOOK.local"
  echo "[review] 기존 pre-push를 pre-push.local로 옮겼습니다 (계속 함께 실행됨)."
fi

cp .githooks/pre-push "$HOOK"
chmod +x "$HOOK" scripts/review/prepush.sh

if ! tr -d '\r' < .gitignore 2>/dev/null | grep -qE '^\.review/?$'; then
  printf '\n# 찬반 리뷰 파이프라인 결과\n.review/\n' >> .gitignore
  echo "[review] .gitignore에 .review/ 추가"
fi

echo "[review] 설치 완료. 이제 push 전에: bash scripts/review/prepush.sh"
