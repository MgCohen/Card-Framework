# Eval Transcript — eval-2-archive-only (old skill)

**Skill:** builder:lint (old/snapshot)
**Arguments:** --archive-only
**Date:** 2026-05-01
**Project:** /home/user/Card-Framework

---

## Step 1 — Collect working surface

Command:
```bash
find Product/ -name '*.md' -not -path '*/_workflow/_validation/*' | sort
```

Result: 89 markdown files found. No files under any `archive/` path. Partitioned as:
- **archived:** 0 files
- **active (features/):** 32 files across actions, effects, engine, entities, presentation, triggers, zones
- **other:** 57 files (ledgers, sessions, decisions, general, spikes, support, _workflow)

---

## Step 2 — Archive integrity checks (--archive-only)

### A1 — Forward-pointer integrity

Command:
```bash
grep -rn '^superseded_by:' Product/features/*/archive/
```

Result: No output. No `superseded_by:` front-matter found in any archive directory.
**Findings: 0**

### A2 — No stale inbound links to archive

Command:
```bash
grep -rn 'archive/' Product/ --include='*.md' --exclude-dir='_workflow/_validation'
```

Result: Hits found in:
- `Product/ledgers/BACKLOG.md` — prose discussing migration task (references archive/ convention in documentation, not a link to a specific archived file)
- `Product/_workflow/PRODUCT-BUILDER.md` — multiple lines in §5.14 documenting the archive/ convention
- `Product/_workflow/REPORT-STYLE.md` — example entries in the report style guide showing archive/ in sample findings
- `Product/decisions/0004-archive-convention.md` — ADR discussing the archive/ convention

Assessment: All hits are in workflow/template/convention documentation that describes the archive/ concept. None are inbound links to actual archived documents. No archived documents exist to link to.
**Findings: 0**

### A3a — Active files with retired status

Command:
```bash
grep -rn 'status: Superseded\|status: Cancelled' Product/ --include='*.md' | grep -v '/archive/'
```

Result: Hits in:
- `Product/ledgers/BACKLOG.md` — prose referencing the migration task (strikethrough, done entry, not a doc status)
- `Product/_workflow/PRODUCT-BUILDER.md` — template/documentation showing `status: Superseded | Cancelled` as the valid archive statuses
- `Product/_workflow/_templates/archive.md` — the archive template front-matter

Assessment: No active feature/model/decision doc has `status: Superseded` or `status: Cancelled` as its own front-matter status. All hits are prose mentioning these status values, not docs carrying them.
**Findings: 0**

### A3b — Archive files lacking retired status

Command:
```bash
find Product/features -path '*/archive/*.md' | xargs grep -L 'status: Superseded\|status: Cancelled' 2>/dev/null
```

Result: Exit code 123 — `find` returned no files matching `*/archive/*.md`. No archive/ directories exist under Product/features/.
**Findings: 0**

### A4 — Successor presence

No `superseded_by:` entries found in A1 — nothing to validate.
**Findings: 0**

---

## Step 3 — Skipped (--archive-only mode; S1/S2 not in scope)

---

## Step 4 — Deduplication check

Read `Product/ledgers/ALIGNMENT-DEBT.md`. Highest existing id for 2026-05-01 is `ad-20260501-026`. 5 open entries total.

No new findings to deduplicate.

---

## Step 5 — No new ledger entries

Zero findings — ALIGNMENT-DEBT.md not modified.

---

## Step 6 — Report rendered

See report.md.
