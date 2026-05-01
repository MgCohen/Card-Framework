---
name: builder:lint
description: >
  Reference-staleness and archive-integrity linter for the Product Builder workflow.
  Scans Product/ docs for broken path references, stale archive links, and
  status/location mismatches, then appends findings to ledgers/ALIGNMENT-DEBT.md.
  Use this skill whenever the user runs /builder:lint, asks to lint the product docs,
  check for stale references, run an integrity check on the archive, audit broken
  paths in the feature docs, or wants to know if there are any reference problems
  before closing a session or after archiving a document.
allowed-tools: Read, Bash, Glob, Grep, Edit
---

# builder:lint

You are the Product Builder reference-staleness and archive-integrity linter. Your job is to scan `Product/` docs for integrity problems and record them in `ledgers/ALIGNMENT-DEBT.md`. You do not fix problems — you report them so the discharge flow can handle them.

**Why this matters:** the Product Builder workflow maintains a web of cross-references between feature docs, decisions, ledgers, and archived predecessors. When a doc moves, gets archived, or has its path changed, the references pointing to it can silently break. This linter surfaces those breaks before they cause confusion or data loss.

## Arguments

User input: `$ARGUMENTS` (default: `--all`)

- `--all` — run all checks (archive integrity + path resolution + builds-on)
- `--archive-only` — only archive integrity checks (rules A1–A4)
- `--paths-only` — only path and builds-on checks (rules S1–S2)

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

## Step 3 — Path-resolution check (run when `--all` or `--paths-only`)

### S1 — Broken path references

Bare path references of the form `features/<feature>/...` or `Product/...` in docs must resolve to real files. Broken references mean readers follow links to 404s.

```bash
grep -rnoE 'features/[a-z]+/[a-z/._-]+\.md|Product/[a-z/._-]+\.md' \
  Product/ --include='*.md' | grep -v '/archive/'
```

For each unique path: verify it exists on disk. Paths under `archive/` are valid only when the linking doc is the designated successor (already covered by A2).

**Finding:** `S1 — broken path reference`

### S2 — builds-on references

`builds-on:` declares dependency chains between docs. A doc that builds on an archived or missing predecessor is built on a moving target.

```bash
grep -rn '^builds-on:' Product/features/ --include='*.md' | grep -v '/archive/'
```

Parse each `builds-on:` list. For each entry that is not the literal `root`: verify the path resolves to an existing, non-archived file.

**Finding:** `S2 — builds-on references missing or archived path`

---

## Step 4 — Deduplicate against existing ledger

Read `Product/ledgers/ALIGNMENT-DEBT.md`. For each finding from steps 2–3, check whether an open entry already exists with the same rule code and target path. Skip duplicates — the linter is idempotent by design.

---

## Step 5 — Write new ledger entries

Append all new findings to `Product/ledgers/ALIGNMENT-DEBT.md` under `## Entries` in a single Edit call. Use the v2 entry shape:

```
- id: ad-YYYYMMDD-NNN | status: open | source: linter | target: <affected path> | reason: <rule code> — <one-line description>
```

Use today's date. Batch all new entries into **one Edit call** — never one edit per finding.

The `NNN` sequence continues from the highest existing `ad-YYYYMMDD-NNN` id in the file for today's date. If today has no entries yet, start at `001`.

---

## Step 6 — Render the report

Emit a compact lint report:

```
# Lint Report

📊 <N> new entries · <M> skipped (already open) · <K> total open

### Findings by rule
| Rule | New | Description |
|------|-----|-------------|
| A1   | N   | Forward-pointer integrity |
| A2   | N   | Stale inbound links |
| A3a  | N   | Retired status, active location |
| A3b  | N   | Active status, archive location |
| A4   | N   | Missing successor |
| S1   | N   | Broken path references |
| S2   | N   | Broken builds-on references |
```

If total open entries reach ≥ 5 after this run, add a pass-mode strip:

```
⚠️ Pass-mode: <total> open debt entries — consider a discharge pass before continuing.
```

If all rules returned zero findings, emit:

```
✅ Clean: no new findings.
```

---

## Safety rails

- **Read-only except for `ledgers/ALIGNMENT-DEBT.md`.** Never modify any other file.
- **Idempotent.** Re-runs must not open duplicate entries.
- **Never modify archived docs.** The linter reports; archiving and fixing are for `/builder:archive` and `/builder:debt-pass`.
- **No auto-resolution.** The linter opens debt entries; the discharge flow closes them.
