# Transcript — /builder:lint --archive-only

**Date:** 2026-05-01  
**Arguments:** `--archive-only`  
**Checks run:** A1, A2, A3a, A3b, A4 only (path/builds-on checks S1–S2 skipped)

---

## Step 1 — Collect the working surface

```bash
find Product/ -name '*.md' \
  -not -path '*/_workflow/_validation/*' \
  | sort
```

**Result:** 85 markdown files found. Partitioned as:
- **archived:** 0 files (no `archive/` directories exist under `Product/features/`)
- **active:** 32 files under `Product/features/`
- **other:** 53 files (ledgers, sessions, decisions, general, spikes, support, _workflow)

---

## Step 2 — Archive integrity checks

### A1 — Forward-pointer integrity

```bash
grep -rn '^superseded_by:' Product/features/
```

**Result:** No output. No `superseded_by:` front-matter found anywhere. **0 findings.**

### A2 — No stale inbound links to archive

```bash
grep -rn 'archive/' Product/ \
  --include='*.md' \
  --exclude-dir='_workflow/_validation'
```

**Result:** Hits found in:
- `Product/ledgers/BACKLOG.md:46` — prose commentary on the migration task (strikethrough/done entry), not a link to an archived file
- `Product/ledgers/BACKLOG.md:48` — prose commentary on the `/builder:archive` skill, not a link
- `Product/_workflow/REPORT-STYLE.md:272` — template example row showing a hypothetical finding, inside a code block
- `Product/_workflow/REPORT-STYLE.md:280` — prose explaining archive convention
- `Product/_workflow/REPORT-STYLE.md:288` — template showing `git mv` command example
- `Product/_workflow/REPORT-STYLE.md:301` — template metadata line
- `Product/decisions/0004-archive-convention.md` — multiple hits, all policy prose about the archive convention itself
- `Product/_workflow/PRODUCT-BUILDER.md` — multiple hits, all policy/structural documentation
- `Product/_workflow/_templates/archive.md` — the archive doc template itself

None of these are active docs linking into actual archived files. All are documentation about the archive convention or template examples. **0 findings.**

### A3a — Active files with retired status

```bash
grep -rn 'status: Superseded\|status: Cancelled' Product/ \
  --include='*.md' \
  | grep -v '/archive/'
```

**Result:** No output (exit code 1 — no matches). **0 findings.**

Note: The string "superseded" appears in prose in `actions/models/01-command-pipeline.md` and `effects/models/01-async-dispatch.md` as inline commentary on retired concepts, not as a `status:` field value. These are not flagged.

### A3b — Archive files lacking retired status

```bash
find Product/features -path '*/archive/*.md' \
  | xargs grep -L 'status: Superseded\|status: Cancelled' 2>/dev/null
```

**Result:** No archive files exist under `Product/features/`. `find` returns no paths, `xargs` exits cleanly. **0 findings.**

### A4 — Successor presence

No `superseded_by:` entries were found in A1, so there are no successor paths to verify. **0 findings.**

---

## Step 3 — Deduplicate against existing ledger

Read `Product/ledgers/ALIGNMENT-DEBT.md`. Existing open entries (5 total):
- ad-20260501-022 through ad-20260501-026

No new findings to deduplicate against.

---

## Step 4 — Write new ledger entries

No new findings. No edits made to `ALIGNMENT-DEBT.md`.

---

## Step 5 — Final tally

| Rule | Findings |
|------|----------|
| A1   | 0        |
| A2   | 0        |
| A3a  | 0        |
| A3b  | 0        |
| A4   | 0        |

Total new entries: **0**  
Total open entries in ledger: **5**  
(5 open entries is at the warning threshold of ≥5, but no new entries were added by this run.)
