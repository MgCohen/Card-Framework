# Lint Report

**Run:** `--archive-only` (rules A1–A4 only) · 2026-05-01

0 new entries · 0 skipped · 6 total open

### Findings by rule

| Rule | New | Description |
|------|-----|-------------|
| A1   | 0   | Forward-pointer integrity |
| A2   | 0   | Stale inbound links to archive/ |
| A3a  | 0   | Retired status, active location |
| A3b  | 0   | Active status, archive location |
| A4   | 0   | Missing or archived successor |

✅ Clean: no new findings.

**Notes:** No `archive/` subdirectories exist under `Product/features/` — the migration scan (BACKLOG task PR-2) confirmed all 13 Models and 19 ADRs carry active statuses. Archive integrity rules A1–A4 have nothing to check.

⚠️ Pass-mode: 6 open debt entries (from prior cascade passes) — consider a discharge pass before continuing.
