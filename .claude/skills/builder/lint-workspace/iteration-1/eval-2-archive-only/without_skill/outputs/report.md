# Archive Integrity Lint Report

**Scope:** `Product/` docs — archive integrity checks only
**Date:** 2026-05-01
**Checks performed:**
1. Archived docs whose `superseded_by:` points at missing or archived files
2. Active docs that link into `archive/` directories
3. Files with retired status living outside `archive/`, or files in `archive/` without retired status
4. `superseded_by:` pointers whose named successor doesn't exist

---

## Summary

**Zero findings.** All four archive-integrity checks pass clean.

---

## Check 1 — Archived docs with broken `superseded_by:` pointers

All seven feature `archive/` directories contain only a `.gitkeep` file. No archived docs exist, so there are no `superseded_by:` fields to validate.

**Findings: none**

---

## Check 2 — Active docs linking into `archive/` directories

A scan of all non-workflow, non-session, non-ADR-0004, non-ledger docs for markdown links of the form `(*/archive/*)` returned no matches. The only references to `archive/` paths in the codebase appear in:

- `Product/_workflow/PRODUCT-BUILDER.md` — normative spec prose (not a link to a real archived file)
- `Product/_workflow/REPORT-STYLE.md` — report template examples
- `Product/_workflow/_templates/archive.md` — template definition
- `Product/decisions/0004-archive-convention.md` — ADR prose describing the convention
- `Product/ledgers/BACKLOG.md` — backlog entry prose referencing the migration task (now struck through as done)

None of these are stale inbound links to real archived content.

**Findings: none**

---

## Check 3 — Status / location mismatches

### Files with `status: Superseded`, `status: Cancelled`, `status: Retired`, or `status: Abandoned` living outside `archive/`

Scan of `^status:` front-matter across all 105 `.md` files under `Product/`:

| Location | Statuses observed |
|---|---|
| `features/*/models/*.md` | `settled`, `draft` |
| `features/*/decisions/*.md` | `accepted` |
| `features/*.md` (overview docs) | `settled` |
| `decisions/*.md` | `accepted` |
| `general/*.md` | `settled` |
| `spikes/*.md` | `concluded`, `graduated` |

No file outside `archive/` carries `Superseded`, `Cancelled`, `Retired`, or `Abandoned` status.

### Files inside `archive/` without a retired status

The seven `archive/` directories contain only `.gitkeep` files. No content files are present.

**Findings: none**

---

## Check 4 — `superseded_by:` pointers whose named successor doesn't exist

No `superseded_by:` fields appear in any content doc (only in the `_workflow/_templates/archive.md` template and normative spec prose). There are no broken successor pointers.

**Findings: none**

---

## Notes

- The BACKLOG.md migration task (migrate superseded/cancelled docs into `archive/`) was marked done on 2026-05-01 with the note that the migration scan ran clean — confirmed here independently.
- The word "superseded" appears in inline prose within `features/actions/models/01-command-pipeline.md` and `features/effects/models/01-async-dispatch.md`, but these are commentary on retired *concepts*, not `status:` front-matter fields on retired docs. No action required.
- All seven feature `archive/` directories are properly scaffolded with `.gitkeep` and are ready to receive archived docs when needed.
