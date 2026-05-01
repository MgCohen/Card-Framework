# Evaluation Transcript — Archive Integrity Lint (without skill)

**Date:** 2026-05-01
**Scope:** Archive integrity only (no reference-staleness check)
**Method:** Manual shell commands (no builder:lint skill)

---

## Steps taken

### 1. Enumerated all files under `Product/`

```
find /home/user/Card-Framework/Product -type f -name "*.md" | sort
```
Found 105 `.md` files across sessions, features, decisions, general, spikes, ledgers, workflow, and support directories.

### 2. Located all archive directories

```
find /home/user/Card-Framework/Product -type d | sort
```
Found 7 archive directories, all under `Product/features/<feature>/archive/`.

### 3. Listed files in archive directories

```
find /home/user/Card-Framework/Product -path "*/archive/*" | sort
```
Each archive directory contains only a `.gitkeep`. No archived content docs exist.

### 4. Searched for `superseded_by:` fields

```
grep -rn "superseded_by:" /home/user/Card-Framework/Product --include="*.md" | sort
```
The field appears only in:
- `_workflow/PRODUCT-BUILDER.md` — spec prose
- `_workflow/REPORT-STYLE.md` — report template
- `_workflow/_templates/archive.md` — template definition

No content doc carries a `superseded_by:` field. No broken pointers to check.

### 5. Searched for retired/superseded/cancelled status in front matter

```
grep -rn "^status:" /home/user/Card-Framework/Product --include="*.md" | grep -i "retired|superseded|cancelled|abandoned"
```
Only matches from `_workflow/PRODUCT-BUILDER.md` (line 741: template example) and `_workflow/_templates/archive.md` (line 3: template placeholder). No content doc has a retired-family status.

### 6. Searched for active docs linking into archive/ paths

```
grep -rn "archive/" /home/user/Card-Framework/Product --include="*.md" | sort
```
All matches are in:
- Workflow spec docs (`_workflow/`)
- Decision ADR-0004 (`decisions/0004-archive-convention.md`)
- Ledger (`ledgers/BACKLOG.md`)
- Sessions referencing the migration task

None are stale inbound links from active feature/model/ADR docs to real archived content.

### 7. Cross-checked all status values in `features/`

```
grep -rn "^status:" /home/user/Card-Framework/Product/features --include="*.md" | sort
```
All 32 feature docs carry active statuses: `settled`, `accepted`, or `draft`. None carry `Superseded`, `Cancelled`, `Retired`, or `Abandoned`.

### 8. Verified ALIGNMENT-DEBT.md for prior archive findings

```
grep -n "archive|superseded|retired" /home/user/Card-Framework/Product/ledgers/ALIGNMENT-DEBT.md
```
The file references "retired" only in the context of retired *type identifiers* (e.g. `IDecisionResolver retired in favour of ICommandAwaiter`), not retired docs. No open archive-integrity debt entries exist.

---

## Conclusion

All four archive-integrity checks return zero findings. The `archive/` convention (ADR-0004) is structurally in place but currently empty of content — the migration scan run on 2026-05-01 confirmed no docs needed to be moved.
