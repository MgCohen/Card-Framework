#!/usr/bin/env bash
# fork-from.sh <pack-name> <prompt>
#
# Verifies every file in the pack still matches its recorded SHA-256, then
# forks the baked session and runs <prompt> against it.
# Emits the usage block of the forked turn on stdout (JSON).
#
# Exit codes:
#   0 success
#   1 usage / pack not found
#   2 STALE — at least one file's hash changed since bake
#   3 claude CLI failure

set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <pack-name> <prompt>" >&2
  exit 1
fi

PACK_NAME="$1"
PROMPT="$2"

PACK_DIR="${CONTEXT_PACKS_DIR:-$HOME/.context-packs}"
CLAUDE_BIN="${CLAUDE_BIN:-claude}"
PACK_FILE="$PACK_DIR/${PACK_NAME}.json"

if [[ ! -f "$PACK_FILE" ]]; then
  echo "ERROR: pack '$PACK_NAME' not found at $PACK_FILE" >&2
  exit 1
fi

# Hash-gate: re-hash every recorded file and compare.
STALE=()
while IFS=$'\t' read -r path expected_hash; do
  if [[ ! -f "$path" ]]; then
    STALE+=("$path  (missing)")
    continue
  fi
  actual_hash=$(sha256sum "$path" | awk '{print $1}')
  if [[ "$actual_hash" != "$expected_hash" ]]; then
    STALE+=("$path  (sha256 changed)")
  fi
done < <(jq -r '.files[] | "\(.path)\t\(.sha256)"' "$PACK_FILE")

if [[ ${#STALE[@]} -gt 0 ]]; then
  echo "STALE: pack '$PACK_NAME' has invalidated files:" >&2
  printf '  %s\n' "${STALE[@]}" >&2
  echo "Re-bake with: bake.sh $PACK_NAME <files...>" >&2
  exit 2
fi

SESSION_ID=$(jq -r '.session_id' "$PACK_FILE")
BAKED_AT=$(jq -r '.baked_at' "$PACK_FILE")

echo "[fork] pack '$PACK_NAME' is fresh (baked $BAKED_AT)" >&2
echo "[fork] forking session $SESSION_ID..." >&2

RESULT=$("$CLAUDE_BIN" --resume "$SESSION_ID" --fork-session \
  -p "$PROMPT" --output-format json 2>&1) || {
  echo "ERROR: claude fork failed" >&2
  echo "$RESULT" >&2
  exit 3
}

NEW_SESSION=$(echo "$RESULT" | jq -r '.session_id // empty')
echo "[fork] parent: $SESSION_ID" >&2
echo "[fork] forked: $NEW_SESSION" >&2

# Stdout: the JSON object we care about for measurement.
echo "$RESULT" | jq '{
  parent_session: "'"$SESSION_ID"'",
  forked_session: .session_id,
  usage: .usage,
  result: .result
}'
