# context-packs — prototype

Prove (or disprove) that a baked Claude Code session can be **forked** later
and reuse its prompt cache — the economic premise of a "context pack"
workflow. If the cache doesn't hit on the forked turn, the whole pitch
collapses and we stop here.

## What's in this directory

| file | role |
|---|---|
| `bake.sh` | Spawns a non-interactive Claude Code session, forces it to `Read` a set of files, records `{session_id, sha256[], cwd, baked_at}` to a pack JSON. |
| `fork-from.sh` | Re-hashes the pack's files; if anything changed, exits `2 STALE`. Otherwise `claude --resume <sid> --fork-session -p <prompt>` and prints the forked turn's `usage`. |
| `measure.sh` | Reads `~/.claude/projects/**/<session-id>.jsonl` and emits per-turn CSV of `cache_creation_input_tokens`, `cache_read_input_tokens`, `input_tokens`, `output_tokens`, plus a totals JSON to stderr (including hit ratio). |
| `test-hash-gate.sh` | Offline test (stubs `claude`); proves the invalidation logic is correct without spending tokens. **Already green in CI/cloud.** |

## Status

| validation | where | status |
|---|---|---|
| Hash-gate logic (bake records, fork validates, stale blocks) | cloud session, no API | **passing** (`./test-hash-gate.sh`) |
| Cache actually hits on `--fork-session` | local, real CLI | **not yet run** — runbook below |
| Cache survives 10 / 65 min | local, real CLI | not yet run |
| Multi-turn forks compound the savings | local, real CLI | not yet run |

## Why local + manual, not CI

The cloud session that built these scripts can't validate the cache
hypothesis itself: it would need to spawn a sibling `claude` process,
wait through the 5-min and 1-hour TTL boundaries, and parse session
JSONLs as they're being written by its own runtime. Running the matrix
on a developer laptop against the real CLI is cheaper and more
trustworthy than re-engineering this into CI.

---

## The runbook

### Setup (once)

```bash
# Confirm the CLI flags this prototype assumes exist on your install:
claude --help | grep -E -- '--resume|--fork-session|--output-format|-p '

# Pick a real target — a small-but-not-tiny doc set that you'd realistically
# bake. ~5–20 files, a few thousand tokens total.
export TARGET_FILES=(DOCS/architecture.md DOCS/feature-x.md Assets/Scripts/FeatureX/*.cs)

# Pack storage (default ~/.context-packs; override for sandbox runs):
export CONTEXT_PACKS_DIR=$PWD/.context-packs-runs
mkdir -p "$CONTEXT_PACKS_DIR"
```

### Run A — cold baseline (no pack)

A fresh session that reads the files inline and answers the test prompt.
This is the cost we're trying to beat.

```bash
TEST_PROMPT="In one sentence, summarize the responsibility of the main module."

# Inline form: ask in one shot with files referenced in the prompt.
claude -p "Read these files in full, then: $TEST_PROMPT
Files:
$(printf -- '- %s\n' "${TARGET_FILES[@]}")
" --output-format json > runs/A.json

./measure.sh "$(jq -r .session_id runs/A.json)" > runs/A.csv
```

### Bake the pack

```bash
./bake.sh featx "${TARGET_FILES[@]}"
```

This writes `$CONTEXT_PACKS_DIR/featx.json` containing the baked
session_id and per-file SHA-256s.

### Run B — fork immediately (T+0)

```bash
./fork-from.sh featx "$TEST_PROMPT" > runs/B.json
./measure.sh "$(jq -r .forked_session runs/B.json)" > runs/B.csv
```

### Run C — fork after 10 minutes

```bash
sleep 600
./fork-from.sh featx "$TEST_PROMPT" > runs/C.json
./measure.sh "$(jq -r .forked_session runs/C.json)" > runs/C.csv
```

### Run D — fork after 65 minutes (past 1h TTL)

```bash
sleep 3300   # i.e. 65 min total since bake
./fork-from.sh featx "$TEST_PROMPT" > runs/D.json
./measure.sh "$(jq -r .forked_session runs/D.json)" > runs/D.csv
```

### Run E — invalidation works on a real change

```bash
echo "# breaking change" >> "${TARGET_FILES[0]}"
./fork-from.sh featx "$TEST_PROMPT"
# Expect: exits 2 with STALE message, no API call made.
echo "Exit code: $?"   # must be 2
# Restore file before next runs:
git checkout -- "${TARGET_FILES[0]}"
```

### Run F — multi-turn fork (compounds?)

Fork five times in a row off the same pack, capture each one's
hit ratio. We're checking whether the savings persist or fade.

```bash
for i in 1 2 3 4 5; do
  ./fork-from.sh featx "Follow-up #$i: name one risk." > runs/F$i.json
  ./measure.sh "$(jq -r .forked_session runs/F$i.json)" > runs/F$i.csv
done
```

---

## Pass / fail thresholds (decided upfront)

| run | metric | threshold | meaning if it fails |
|---|---|---|---|
| B | `cache_read / (cache_read + input)` ≥ **0.80** | the core hypothesis | fork is not actually reusing the cache; investigate [#27048](https://github.com/anthropics/claude-code/issues/27048) before doing anything else |
| B | cost(B) / cost(A) ≤ **0.25** | the economic claim | even if cache hits, the savings aren't material enough to justify UX |
| C | hit ratio ≥ **0.60** | the time-horizon claim | tool is "save time only", not "save money" — reframe the pitch |
| D | hit ratio < 0.60 (expected) | TTL sanity check | if D *does* still hit, our model of TTL is wrong; flag it |
| E | exits with code 2, no API call | invalidation correctness | hash gate is broken; whole workflow unsafe |
| F1..F5 | hit ratio ≥ 0.60 on each | multi-turn compounding | fork only helps on the first turn; smaller value prop |

Compute cost from the standard pricing for the model you used; the
hit-ratio is the bottom line either way.

## What "done" looks like

After running A–F locally, paste the seven CSVs (and B–F's JSON
responses) into a new GitHub issue or PR comment. The decision tree:

- B passes both thresholds → keep building (UX layer next).
- B passes hit ratio but fails cost ratio → the cached prefix is too small
  relative to the new prompt; revisit what we bake.
- B fails hit ratio → stop. The premise is wrong on this CLI version.
- E fails → fix the hash gate before doing anything else.

## Known caveats

1. **`--fork-session` cache behavior is the explicit thing under test.**
   If your version of Claude Code doesn't share prompt cache between
   parent and forked session, B will fail. That's a real finding.
2. **Session JSONL location.** `measure.sh` looks under
   `~/.claude/projects/`. If your install writes elsewhere, set
   `CLAUDE_PROJECTS_DIR`.
3. **Hash atomicity.** The pack is invalidated by any file change. This
   is intentional. Partial invalidation is a UX choice for later, not a
   prototype concern.
4. **TTL is wall-clock from bake, not from last use.** Runs C/D measure
   from when the bake happened. If you bake and immediately fork (B),
   that may reset the cache TTL inside Claude Code's accounting — note
   whether C still hits despite the warmup.
