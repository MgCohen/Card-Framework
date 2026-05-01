# Lint Report

✅ Clean: no new findings.

📊 0 new entries · 0 skipped (already open) · 4 total open

### Findings by rule
| Rule | New | Description |
|------|-----|-------------|
| A1   | 0   | Forward-pointer integrity |
| A2   | 0   | Stale inbound links |
| A3a  | 0   | Retired status, active location |
| A3b  | 0   | Active status, archive location |
| A4   | 0   | Missing successor |
| S1   | 0   | Broken path references |
| S2   | 0   | Broken builds-on references |

---

## Detail

### A1 — Forward-pointer integrity
No archive/ directories exist under Product/features/. No superseded_by: pointers to check. Zero findings.

### A2 — Stale inbound links to archive
All archive/ text hits were in workflow docs, templates, and the archive-convention ADR — prose describing the convention, not actual links into archived files. No archive files exist on disk. Zero findings.

### A3a — Active files with retired status
All hits for 'status: Superseded|status: Cancelled' were in BACKLOG.md prose and _workflow templates. No active feature docs carry a retired status. Zero findings.

### A3b — Archive docs lacking retired status
No archive/ directories exist. Zero findings.

### A4 — Successor presence
No superseded_by: pointers exist anywhere in the project. Zero findings.

### S1 — Broken path references
All bare path references found resolve to existing files:
- features/entities/entities.md OK
- features/zones/zones.md OK
- features/actions/actions.md OK
- features/triggers/triggers.md OK
- features/effects/effects.md OK
- features/engine/engine.md OK
- features/presentation/presentation.md OK
- Product/support/samples/pile-counter.md OK

Zero findings.

### S2 — Broken builds-on references
All builds-on: entries in active feature docs resolve to existing, non-archived files:
- features/effects/models/01-async-dispatch.md OK
- features/effects/models/04-effect-flow.md OK
- features/engine/models/01-evaluation-loop.md OK
- features/engine/models/02-gamerules-contract.md OK
- features/engine/models/03-snapshot-pipeline.md OK
- features/actions/models/01-command-pipeline.md OK
- features/actions/models/02-availability-snapshot.md OK

Zero findings.

---

## Existing open debt (not modified)

4 entries remain open in Product/ledgers/ALIGNMENT-DEBT.md:
- ad-20260501-022: engine/02 -> engine/03: IHandlerRegistry removal from constructor
- ad-20260501-023: engine/02 -> decisions/0001: IHandlerRegistry service count
- ad-20260501-024: engine/02 -> OPEN-QUESTIONS.md: 5 resolved OQs need strikethrough
- ad-20260501-025: triggers/01 -> effects/02: ITriggerPipeline shape conflict
