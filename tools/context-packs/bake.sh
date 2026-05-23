#!/usr/bin/env bash
# bake.sh <pack-name> <file-or-glob> [<file-or-glob>...]
#
# Spawns a non-interactive Claude Code session, forces it to Read each given
# file so they are loaded into context, then records:
#   - the resulting session_id
#   - SHA-256 of every baked file
#   - timestamp + cwd
# into ${CONTEXT_PACKS_DIR:-~/.context-packs}/<pack-name>.json
#
# Exit codes:
#   0 success
#   1 usage / missing file
#   3 claude CLI failure or no session_id returned

set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <pack-name> <file-or-glob> [<file-or-glob>...]" >&2
  exit 1
fi

PACK_NAME="$1"
shift

PACK_DIR="${CONTEXT_PACKS_DIR:-$HOME/.context-packs}"
CLAUDE_BIN="${CLAUDE_BIN:-claude}"
mkdir -p "$PACK_DIR"

# Expand globs and dedupe while preserving order.
declare -A SEEN=()
EXPANDED=()
for pattern in "$@"; do
  # shellcheck disable=SC2206
  matches=( $pattern )
  if [[ ${#matches[@]} -eq 1 && ! -e "${matches[0]}" ]]; then
    echo "ERROR: no match for '$pattern'" >&2
    exit 1
  fi
  for m in "${matches[@]}"; do
    abs=$(realpath "$m")
    if [[ -z "${SEEN[$abs]:-}" ]]; then
      SEEN[$abs]=1
      EXPANDED+=("$abs")
    fi
  done
done

# Build the hash manifest and the prompt body together.
HASHES_JSON="[]"
PROMPT_FILES=""
for abs in "${EXPANDED[@]}"; do
  if [[ ! -f "$abs" ]]; then
    echo "ERROR: $abs is not a regular file" >&2
    exit 1
  fi
  hash=$(sha256sum "$abs" | awk '{print $1}')
  size=$(wc -c <"$abs" | tr -d ' ')
  HASHES_JSON=$(echo "$HASHES_JSON" | jq \
    --arg p "$abs" --arg h "$hash" --argjson s "$size" \
    '. + [{path: $p, sha256: $h, size: $s}]')
  PROMPT_FILES+="- $abs"$'\n'
done

PROMPT="You are baking a reusable context pack. Read each file below in full using the Read tool so its contents are loaded into context. After reading every file, respond with exactly the word: BAKED

Files:
${PROMPT_FILES}"

echo "[bake] invoking claude with ${#EXPANDED[@]} file(s)..." >&2
RESULT=$("$CLAUDE_BIN" -p "$PROMPT" --output-format json 2>&1) || {
  echo "ERROR: claude invocation failed" >&2
  echo "$RESULT" >&2
  exit 3
}

SESSION_ID=$(echo "$RESULT" | jq -r '.session_id // empty')
if [[ -z "$SESSION_ID" ]]; then
  echo "ERROR: no session_id in claude response" >&2
  echo "$RESULT" >&2
  exit 3
fi

CACHE_CREATE=$(echo "$RESULT" | jq -r '.usage.cache_creation_input_tokens // 0')
CACHE_READ=$(echo "$RESULT" | jq -r '.usage.cache_read_input_tokens // 0')
INPUT_TOK=$(echo "$RESULT" | jq -r '.usage.input_tokens // 0')
OUTPUT_TOK=$(echo "$RESULT" | jq -r '.usage.output_tokens // 0')

TIMESTAMP=$(date -u +%Y-%m-%dT%H:%M:%SZ)
PACK_FILE="$PACK_DIR/${PACK_NAME}.json"
jq -n \
  --arg name "$PACK_NAME" \
  --arg session "$SESSION_ID" \
  --arg ts "$TIMESTAMP" \
  --arg cwd "$(pwd)" \
  --argjson files "$HASHES_JSON" \
  --argjson bake_usage "{\"cache_creation_input_tokens\": $CACHE_CREATE, \"cache_read_input_tokens\": $CACHE_READ, \"input_tokens\": $INPUT_TOK, \"output_tokens\": $OUTPUT_TOK}" \
  '{name: $name, session_id: $session, baked_at: $ts, cwd: $cwd, files: $files, bake_usage: $bake_usage}' \
  > "$PACK_FILE"

echo "[bake] pack '$PACK_NAME' -> $PACK_FILE"
echo "[bake] session_id: $SESSION_ID"
echo "[bake] files baked: ${#EXPANDED[@]}"
echo "[bake] bake usage: create=$CACHE_CREATE read=$CACHE_READ input=$INPUT_TOK output=$OUTPUT_TOK"
