#!/usr/bin/env bash
# measure.sh <session-id>
#
# Reads the session JSONL written by Claude Code under ~/.claude/projects/
# and emits a per-turn CSV of cache token usage:
#   turn_idx,role,cache_create_tok,cache_read_tok,input_tok,output_tok
#
# Also prints a summary row to stderr with totals + hit ratio.
#
# Exit codes:
#   0 success
#   1 usage / session JSONL not found

set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <session-id>" >&2
  exit 1
fi

SESSION_ID="$1"
PROJECT_DIR="${CLAUDE_PROJECTS_DIR:-$HOME/.claude/projects}"

JSONL=$(find "$PROJECT_DIR" -name "${SESSION_ID}.jsonl" 2>/dev/null | head -1)
if [[ -z "$JSONL" ]]; then
  echo "ERROR: session JSONL for $SESSION_ID not found under $PROJECT_DIR" >&2
  exit 1
fi

echo "[measure] reading $JSONL" >&2

# Emit CSV header + rows.
echo "turn_idx,role,cache_create_tok,cache_read_tok,input_tok,output_tok"
jq -r '
  select(.message.usage != null) |
  [
    (.message.id // "-"),
    (.message.role // "-"),
    (.message.usage.cache_creation_input_tokens // 0),
    (.message.usage.cache_read_input_tokens // 0),
    (.message.usage.input_tokens // 0),
    (.message.usage.output_tokens // 0)
  ] | @csv
' "$JSONL"

# Totals + hit-ratio to stderr.
jq -s '
  map(select(.message.usage != null) | .message.usage) |
  {
    turns: length,
    cache_create_total: (map(.cache_creation_input_tokens // 0) | add),
    cache_read_total:   (map(.cache_read_input_tokens // 0)     | add),
    input_total:        (map(.input_tokens // 0)                | add),
    output_total:       (map(.output_tokens // 0)               | add)
  } |
  . + {
    hit_ratio: (
      if (.cache_read_total + .input_total) == 0 then 0
      else (.cache_read_total / (.cache_read_total + .input_total))
      end
    )
  }
' "$JSONL" | jq -c . >&2
