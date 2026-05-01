# Lint Report

📊 0 new entries · 0 skipped (already open) · 5 total open

### Findings by rule
| Rule | New | Description |
|------|-----|-------------|
| A1   | 0   | Forward-pointer integrity |
| A2   | 0   | Stale inbound links |
| A3a  | 0   | Retired status, active location |
| A3b  | 0   | Active status, archive location |
| A4   | 0   | Missing successor |

✅ Clean: no new findings.

---

**Scope:** `--archive-only` (rules A1–A4 only)

**Summary:** No `archive/` directories exist under `Product/features/`. All 13 Models and 19 ADRs carry active statuses (`draft`, `settled`, `implemented`, `proposed`, `accepted`). No `superseded_by:` front-matter found anywhere. No stale inbound links to archive paths from non-workflow/template files. Archive integrity is fully clean.
