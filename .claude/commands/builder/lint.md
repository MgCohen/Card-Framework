---
description: Reference-staleness and archive-integrity linter — reports findings as ALIGNMENT-DEBT.md entries.
argument-hint: [--archive-only | --paths-only | --all]
allowed-tools: Read, Bash, Glob, Grep
---

You are running the Product Builder reference-staleness and archive-integrity linter. Workflow rules: `Product/_workflow/PRODUCT-BUILDER.md` §5.10 and §5.14. Findings are reported, not blocked — each finding becomes a new entry in `ledgers/ALIGNMENT-DEBT.md`.

User input: `$ARGUMENTS` (default: `--all`)

## Performance

Batch bash calls. Do not read files one-by-one in a loop — use `grep -rn` with regex alternation to scan multiple patterns in one pass. The linter may touch many files; low turn count matters.

## Steps

### 1. Parse arguments

- `--all` (default) — run all checks below.
- `--archive-only` — run only the §5.14 archive integrity checks (rules A1–A4).
- `--paths-only` — run only the path-resolution and builds-on checks (rules S1–S2).

### 2. Collect the working surface

In one bash call:

```bash
# All markdown docs under Product/ (excluding _validation fixtures)
find Product/ -name '*.md' \
  --exclude-dir='_workflow/_validation' \
  -not -path '*/_workflow/_validation/*' \
  | sort
```

Partition the list into:
- **archived** — paths that contain `/archive/`
- **active** — everything else under `Product/features/`
- **other** — ledgers, sessions, decisions, general, spikes, support, _workflow

### 3. Archive integrity checks (§5.14 rules A1–A4)

Run **only when** flag is `--all` or `--archive-only`.

#### A1 — Forward-pointer integrity

For every archived doc with `status: Superseded`:
- Parse `superseded_by:` from front matter.
- Verify the resolved path exists and is not itself under `archive/`.
- **Finding:** `archive integrity A1 — superseded_by path missing or itself archived`

Single bash: `grep -rn '^superseded_by:' Product/features/*/archive/`

#### A2 — No stale inbound links

Scan all **active + other** docs for any markdown link or bare path reference containing `/archive/`.

```bash
grep -rn 'archive/' Product/ \
  --include='*.md' \
  --exclude-dir='_workflow/_validation'
```

For each hit, check whether the linking doc is itself the successor named in the archived doc's `superseded_by:` field. If not — it's a stale reference.

**Finding:** `archive integrity A2 — stale inbound link to archive/ from non-successor`

#### A3 — Status/location agreement

Two sub-checks:

- **A3a.** Active docs (not in `archive/`) with `status: Superseded` or `status: Cancelled` — these should have been moved.
  ```bash
  grep -rn 'status: Superseded\|status: Cancelled' Product/ \
    --include='*.md' \
    | grep -v '/archive/'
  ```
  **Finding:** `archive integrity A3a — doc has retired status but lives outside archive/`

- **A3b.** Archived docs (in `archive/`) without `status: Superseded` or `status: Cancelled`.
  ```bash
  find Product/features -path '*/archive/*.md' \
    | xargs grep -L 'status: Superseded\|status: Cancelled' 2>/dev/null
  ```
  **Finding:** `archive integrity A3b — doc in archive/ lacks retired status`

#### A4 — Successor presence

For every `superseded_by: <path>` in an archived doc, verify:
1. The successor path exists.
2. The successor itself is not in `archive/`.

**Finding:** `archive integrity A4 — successor named in superseded_by does not exist or is itself archived`

### 4. Path-resolution check (§5.10 rule S1)

Run **only when** flag is `--all` or `--paths-only`.

Extract all bare path references of the form `features/<feature>/...` or `Product/...` from active + other docs:

```bash
grep -rnoE 'features/[a-z]+/[a-z/._-]+\.md|Product/[a-z/._-]+\.md' \
  Product/ --include='*.md' | grep -v '/archive/'
```

For each unique path found, verify it exists on disk. Paths under `archive/` are valid targets only from their designated successors (already covered by A2).

**Finding:** `S1 — broken path reference`

### 5. builds-on check (§5.10 rule S2)

Run **only when** flag is `--all` or `--paths-only`.

```bash
grep -rn '^builds-on:' Product/features/ --include='*.md' | grep -v '/archive/'
```

Parse each `builds-on:` list. For each entry that is not the literal `root`, verify the path resolves to an existing file. Paths into `archive/` are always a finding (active docs should not `builds-on:` an archived doc).

**Finding:** `S2 — builds-on references missing or archived path`

### 6. Deduplicate against existing ledger entries

Read `Product/ledgers/ALIGNMENT-DEBT.md`. For each finding from steps 3–5, check whether an open entry already exists with the same `source=linter` / `source=linter/archive` and `target=<affected path>`. Skip if a duplicate open entry exists (idempotent).

### 7. Write new ledger entries

For each new finding, append to `Product/ledgers/ALIGNMENT-DEBT.md` under `## Entries` (or whatever the most recent date-stamped subsection is — group same-day findings together). Use the v2 entry format:

```markdown
- id: ad-YYYYMMDD-NNN | status: open | source: linter | target: <affected path> | reason: <rule code> — <one-line description>
```

- `source:` is `linter` for §5.10 rules (S1, S2) and `linter/archive` for §5.14 rules (A1–A4). The `linter*` prefix is what excludes these from the §5.8 threshold count — do not invent other source labels.
- `id:` uses today's date (`YYYYMMDD`) and an incrementing 3-digit suffix that doesn't collide with existing entries on that date. Read the ledger first to find the next free NNN.

Batch all new entries into a **single Edit call**.

### 8. Render the report

**Render order:**

1. **Title** (H1) — `# Lint — <scope>` (scope = "all rules" / "archive integrity" / "path checks" per flag passed)
2. **Summary stat strip** — `📊 <N> new findings · <M> already in ledger · <K> rules checked`
3. **Findings** (H3 + table, omitted if zero findings):
   ```
   ### ⚠️ Findings
   | Rule | Doc | Detail |
   |---|---|---|
   | A1 | features/engine/archive/01-old.md | superseded_by path missing |
   ```
4. **Debt opened** (strip, omitted if zero) — `📋 Debt opened: <N> new entries in ALIGNMENT-DEBT.md (source: linter*)`

**Empty-state behavior:** when the linter finds nothing, the entire §3–§4 block collapses to a single `✅ Clean — no findings` strip. The H1 and summary stat strip still render.

**Linter findings do not trip the §5.8 threshold** (only cascade entries do — see PRODUCT-BUILDER.md §5.8 / §5.10). No pass-mode strip from this skill.

## Safety rails

- **Read-only except for `ledgers/ALIGNMENT-DEBT.md`.** Never modify any other file.
- **Idempotent.** Re-runs must not open duplicate entries.
- **Never modify archived docs** — the linter reports only; archiving and fixing are manual or via `/builder:archive`.
- **No auto-resolution.** The linter opens entries; the discharge flow closes them.

## Notes

- The `--exclude-dir='_workflow/_validation'` exclusion applies everywhere: the validation fixture deliberately contains retired tokens and stale references as test cases and must not be flagged.
- If `Product/features/` has no `archive/` subdirectories yet, A1/A2/A3b/A4 will produce zero findings — that is expected for a project that has not yet archived anything.
- Run after any `/builder:archive` invocation and before closing a session that includes a supersession or cancellation.
