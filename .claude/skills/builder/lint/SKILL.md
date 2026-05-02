---
name: builder:lint
description: >
  Reference-staleness and archive-integrity linter for the Product Builder workflow.
  Scans Product/ docs for broken path references, convention violations, stale archive
  links, and status/location mismatches; reports findings to stdout (no ledger writes).
  Use this skill whenever the user runs /builder:lint, asks to lint the product docs,
  check for stale references, run an integrity check on the archive, audit broken
  paths in the feature docs, or wants to know if there are any reference problems
  before closing a session or after archiving a document.
allowed-tools: Read, Bash, Glob, Grep, Edit
---

# builder:lint

You are the Product Builder reference-staleness and archive-integrity linter. Your job is to scan `Product/` docs for integrity problems and **report findings to stdout**. You do not fix problems and you do not write to any ledger — findings are surfaced for fix-in-place.

**Why this matters:** the Product Builder workflow maintains a web of cross-references between feature docs, decisions, ledgers, and archived predecessors. The reference convention (`_workflow/REFERENCE-CONVENTIONS.md`) requires every cross-doc reference to be a `Product/`-rooted absolute path — this keeps grep, sed, and renames mechanical. When a doc moves, gets archived, or someone slips a relative path or encoded filename into a link, the references break silently. This linter surfaces those breaks before they cause confusion or data loss.

Workflow context: `Product/_workflow/PRODUCT-BUILDER.md` §5.10 (path/references rules) and §5.14 (archive integrity rules). `Product/_workflow/REFERENCE-CONVENTIONS.md` (canonical reference format).

## Arguments

User input: `$ARGUMENTS` (default: `--all`)

- `--all` — run all checks
- `--archive-only` — only archive integrity checks (rules A1–A4)
- `--paths-only` — only path-and-convention checks (rules S1–S4)
- `--structure-only` — only Model structure check (rule M1)
- `--tokens-only` — only retired-tokens check (rule T1)

## Rule index

- **S1** — every path reference in `Product/` resolves.
- **S2** — every `references:` entry resolves to an existing, non-archived file.
- **S3** — no relative paths (`../`, `./`, `/`-prefixed) in links or `references:`.
- **S4** — filename hygiene (ASCII, no URL encoding).
- **A1–A4** — archive integrity (forward-pointer, no stale inbound, status/location agreement, successor presence). See §5.14.
- **M1** — Model structure: every Model has the canonical sections (Problem, Concepts, Contracts, Invariants, Examples, Open follow-ups, then `---` divider, then Options considered, Rationale, Consequences, optional ADR), in canonical order. Empty sections may carry the stub `*n/a — purely descriptive Model.*` but cannot be reordered or omitted.
- **T1** — retired tokens: any token in `Product/_workflow/retired-tokens.json` appearing outside `_archive/` and `sessions/` surfaces with its replacement.

## Hardening (§5.10)

- Exit non-zero on internal errors (file not found, parse failure).
- Emit a structured summary line at end of report: `lint: P passed, F failed, S skipped`.
- Distinguish *findings* (continue-on-error, accumulate and report) from *infrastructure failures* (exit-on-error, abort with diagnostic).
- Smoke-test fixtures live under `.claude/skills/builder/lint/fixtures/` — one Model with a broken `references:` entry, one with a structure violation. CI / manual run against fixtures verifies the linter still flags them.

## Performance rule

Batch all filesystem operations. Never read files one-by-one in a loop — use `grep -rn` with regex alternation to scan multiple patterns in a single pass. Turn count matters; the linter may touch many files.

---

## Step 1 — Collect the working surface

One bash call:

```bash
find Product/ -name '*.md' \
  -not -path '*/_workflow/_validation/*' \
  | sort
```

Partition into:
- **archived** — paths containing `/archive/`
- **active** — everything else under `Product/features/`
- **other** — ledgers, sessions, decisions, general, spikes, support, `_workflow`

The `_workflow/_validation/` exclusion is intentional: that fixture deliberately contains retired tokens and stale references as test cases and must never be flagged.

---

## Step 2 — Archive integrity checks (run when `--all` or `--archive-only`)

These four rules exist because archived docs create a directed graph of supersession pointers. The graph breaks silently when files move or successors are never created.

### A1 — Forward-pointer integrity

Every archived doc with `status: Superseded` must have a `superseded_by:` pointing at an active (non-archived) file.

```bash
grep -rn '^superseded_by:' Product/features/*/archive/
```

For each hit: verify the resolved path exists and is not itself under `archive/`.

**Finding:** `archive integrity A1 — superseded_by path missing or itself archived`

### A2 — No stale inbound links to archive

Active docs linking into `archive/` create reader confusion — the archived version is the old one, not the canonical one. The only legitimate link into `archive/` from a non-archived doc is the successor naming its predecessor.

```bash
grep -rn 'archive/' Product/ \
  --include='*.md' \
  --exclude-dir='_workflow/_validation'
```

For each hit: the linking doc is allowed only if it is the `superseded_by` successor of the archived doc being linked. Any other link is stale.

**Finding:** `archive integrity A2 — stale inbound link to archive/ from non-successor`

### A3 — Status/location agreement

Files should live where their status says they live.

**A3a.** Active files (not in `archive/`) with a retired status:

```bash
grep -rn 'status: Superseded\|status: Cancelled' Product/ \
  --include='*.md' \
  | grep -v '/archive/'
```

**Finding:** `archive integrity A3a — doc has retired status but lives outside archive/`

**A3b.** Files inside `archive/` that lack a retired status:

```bash
find Product/features -path '*/archive/*.md' \
  | xargs grep -L 'status: Superseded\|status: Cancelled' 2>/dev/null
```

**Finding:** `archive integrity A3b — doc in archive/ lacks retired status`

### A4 — Successor presence

`superseded_by` is only meaningful if the successor actually exists and is active.

For every `superseded_by: <path>` found in step A1: verify (1) the path exists on disk and (2) it is not itself under `archive/`. (This overlaps A1 intentionally — belt and suspenders for the graph edges that matter most.)

**Finding:** `archive integrity A4 — successor named in superseded_by does not exist or is itself archived`

---

## Step 3 — Path-and-convention checks (run when `--all` or `--paths-only`)

Rules S1–S4 enforce the reference convention: every cross-doc reference is a `Product/`-rooted absolute path; filenames are ASCII-clean.

### S1 — Broken path references

Every absolute path mentioned in a markdown link target or in bare prose must resolve to an existing file under `Product/`.

**Pass 1 — markdown link targets:**

```bash
grep -rnoP '\]\(([^)]*\.md[^)#]*)' Product/ --include='*.md' \
  --exclude-dir='_workflow/_validation'
```

For each extracted path (strip any `#anchor` suffix): check that `Product/<path>` exists on disk.

**Pass 2 — bare path mentions in prose:**

```bash
grep -rnoE '\b(features|ledgers|sessions|spikes|support|general|decisions|_workflow)/[a-zA-Z0-9/_.-]+\.md' \
  Product/ --include='*.md' --exclude-dir='_workflow/_validation' \
  | grep -v '/archive/'
```

For each unique path: verify `Product/<path>` exists on disk.

**Backtick exclusion:** paths inside inline backticks (`` `path/to.md` ``) or fenced code blocks are illustrative — not flagged. Mechanical rule, no judgment: if the match is wrapped in backticks or sits inside ` ``` ` fences, skip it.

**Finding:** `S1 — broken path reference`

### S2 — references entries

`references:` declares dependency chains. Every entry must resolve to an existing, non-archived file (or be the literal `root`).

```bash
grep -rn '^references:' Product/ --include='*.md' | grep -v '/archive/'
```

Parse each `references:` list. Each entry is interpreted as `Product/<entry>` (no relative resolution, no per-location prefix logic). Entries pointing into `archive/` are always findings.

**Finding:** `S2 — references entries missing or archived path`

### S3 — Relative path violation

The convention forbids relative paths in markdown links and `references:`. Catch them with a single regex pass:

```bash
grep -rnE '\]\((\.\./|\./|/)[^)]*\.md|^references:.*\.\./' \
  Product/ --include='*.md' \
  --exclude-dir='_workflow/_validation'
```

Backtick-wrapped relative paths in prose (illustrative shape examples) are not flagged — same rule as S1.

**Finding:** `S3 — relative path reference (must be Product/-rooted absolute)`

### S4 — Filename hygiene

Filenames under `Product/` must be ASCII without spaces, parens, percents, or brackets — this guarantees URL encoding is never needed.

```bash
find Product/ -name '*.md' -not -path '*/_workflow/_validation/*' \
  | grep -E '[ ()%\[\]]'
```

**Finding:** `S4 — filename contains forbidden character (space/paren/percent/bracket)`

---

## Step 4 — Render the report

(ALIGNMENT-DEBT writing removed in PR 2 of the doc-workflow simplification — the linter no longer appends to any ledger. Findings are reported only; fix in place.)

**Render order:**

1. **Title** (H1) — `# Lint — <scope>` (scope = "all rules" / "archive integrity" / "path checks" per flag passed).
2. **Summary stat strip** — `📊 <N> findings · <K> rules checked`.
3. **Findings** (H3 + table, omitted if zero findings):
   ```
   ### ⚠️ Findings
   | Rule | Doc | Detail |
   |---|---|---|
   | A1 | features/engine/archive/01-old.md | superseded_by path missing |
   | S3 | ledgers/FEATURE-MAP.md:25 | relative link `../features/...` |
   ```

**Empty-state:** when the linter finds nothing, the findings table collapses to a single `✅ Clean — no findings` strip. The H1 and summary stat strip still render.

---

## Safety rails

- **Read-only.** Never modify any file. The linter reports findings to stdout only.
- **Idempotent.** Re-runs against unchanged inputs produce the same report.
- **Never modify archived docs.** The linter reports; archiving and fixing are for `/builder:archive` and the user.
- **No auto-resolution.** The linter surfaces findings; humans (or the user-driven agent) fix them in place.
