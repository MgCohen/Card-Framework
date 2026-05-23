#!/usr/bin/env bash
# test-hash-gate.sh
#
# Offline validation of bake.sh + fork-from.sh hash invalidation logic.
# Stubs the `claude` CLI so no API calls happen — this exercises only
# the file-hashing and gate behavior.
#
# Pass criteria (all four must pass):
#   T1: bake records every file's sha256 + pack metadata
#   T2: fork-from succeeds when no files have changed
#   T3: fork-from exits 2 (STALE) when any file content changes
#   T4: fork-from exits 2 (STALE) when any file is deleted
#
# Run from the tools/context-packs/ directory.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BAKE="$SCRIPT_DIR/bake.sh"
FORK="$SCRIPT_DIR/fork-from.sh"

TMPROOT=$(mktemp -d)
trap 'rm -rf "$TMPROOT"' EXIT

export CONTEXT_PACKS_DIR="$TMPROOT/packs"

# --- Stub claude binary ---------------------------------------------------
STUB_BIN="$TMPROOT/stub"
mkdir -p "$STUB_BIN"
cat > "$STUB_BIN/claude" <<'STUB'
#!/usr/bin/env bash
# Stub: emits a deterministic session_id; ignores all args.
# Records how it was called for inspection.
echo "$@" >> "${STUB_LOG:-/dev/null}"
case " $* " in
  *" --fork-session "*) SID="forked-session-xyz" ;;
  *)                    SID="baked-session-abc" ;;
esac
cat <<JSON
{"session_id":"$SID","result":"ok","usage":{"cache_creation_input_tokens":1234,"cache_read_input_tokens":0,"input_tokens":42,"output_tokens":7}}
JSON
STUB
chmod +x "$STUB_BIN/claude"

export STUB_LOG="$TMPROOT/stub.log"
export CLAUDE_BIN="$STUB_BIN/claude"

# --- Fixtures -------------------------------------------------------------
FIXTURES="$TMPROOT/repo"
mkdir -p "$FIXTURES"
echo "feature spec v1" > "$FIXTURES/feature.md"
echo "module code v1"  > "$FIXTURES/code.py"

PASS=0
FAIL=0
note() { printf '  %s\n' "$*"; }
pass() { PASS=$((PASS+1)); printf '\033[32mPASS\033[0m %s\n' "$1"; }
fail() { FAIL=$((FAIL+1)); printf '\033[31mFAIL\033[0m %s\n' "$1"; [[ -n "${2:-}" ]] && note "$2"; }

# --- T1: bake records hashes and metadata --------------------------------
echo
echo "T1: bake records every file's sha256 + pack metadata"
"$BAKE" testpack "$FIXTURES/feature.md" "$FIXTURES/code.py" >/dev/null

PACK_FILE="$CONTEXT_PACKS_DIR/testpack.json"
if [[ ! -f "$PACK_FILE" ]]; then
  fail "T1" "pack file not created"
else
  recorded=$(jq -r '.files | length' "$PACK_FILE")
  session=$(jq -r '.session_id' "$PACK_FILE")
  feature_hash=$(jq -r '.files[] | select(.path | endswith("feature.md")) | .sha256' "$PACK_FILE")
  expected=$(sha256sum "$FIXTURES/feature.md" | awk '{print $1}')
  if [[ "$recorded" == "2" && "$session" == "baked-session-abc" && "$feature_hash" == "$expected" ]]; then
    pass "T1"
  else
    fail "T1" "recorded=$recorded session=$session feature_hash=$feature_hash expected=$expected"
  fi
fi

# --- T2: fork-from succeeds when nothing changed -------------------------
echo
echo "T2: fork-from succeeds when no files have changed"
if "$FORK" testpack "do thing" >/dev/null 2>&1; then
  pass "T2"
else
  fail "T2" "fork-from exited non-zero on clean state"
fi

# --- T3: modified file -> STALE ------------------------------------------
echo
echo "T3: fork-from exits 2 (STALE) when any file content changes"
echo "feature spec v2" > "$FIXTURES/feature.md"
set +e
"$FORK" testpack "do thing" >/dev/null 2>&1
rc=$?
set -e
if [[ "$rc" == "2" ]]; then
  pass "T3"
else
  fail "T3" "expected exit 2, got $rc"
fi
# Restore for next test
echo "feature spec v1" > "$FIXTURES/feature.md"

# --- T4: deleted file -> STALE -------------------------------------------
echo
echo "T4: fork-from exits 2 (STALE) when any file is deleted"
rm "$FIXTURES/code.py"
set +e
"$FORK" testpack "do thing" >/dev/null 2>&1
rc=$?
set -e
if [[ "$rc" == "2" ]]; then
  pass "T4"
else
  fail "T4" "expected exit 2, got $rc"
fi

# --- summary -------------------------------------------------------------
echo
echo "==== summary: $PASS passed, $FAIL failed ===="
exit $FAIL
