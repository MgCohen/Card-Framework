# Lint Report

📊 12 new entries · 2 skipped (already open: ad-20260501-026, ad-20260501-027) · 18 total open

### Findings by rule
| Rule | New | Description |
|------|-----|-------------|
| A1   | 0   | Forward-pointer integrity |
| A2   | 0   | Stale inbound links |
| A3a  | 0   | Retired status, active location |
| A3b  | 0   | Active status, archive location |
| A4   | 0   | Missing successor |
| S1   | 12  | Broken path references |
| S2   | 0   | Broken builds-on references |

### S1 new findings detail

| id | target | broken reference |
|----|--------|-----------------|
| ad-20260501-028 | `_workflow/PRODUCT-BUILDER.md` | `AI-FIRST-AUTOMATION.md` — 3 prose links (§5.11, §5.12, §5.13) |
| ad-20260501-029 | `general/GAME-ARCHETYPES.md` | `spikes/SabberStone-Architecture-Analysis.md (1)` (URL-decoded) |
| ad-20260501-030 | `general/GAME-ARCHETYPES.md` | `spikes/Fireplace-Architecture-Analysis.md (1)` (URL-decoded) |
| ad-20260501-031 | `general/GAME-ARCHETYPES.md` | `spikes/Brimstone-Architecture-Analysis.md (1)` (URL-decoded) |
| ad-20260501-032 | `support/prior-art/card-game-base-architecture-bb88f456.plan.md` | `Docs/SabberStone-Architecture-Analysis.md` |
| ad-20260501-033 | `support/prior-art/card-game-base-architecture-bb88f456.plan.md` | `Docs/Fireplace-Architecture-Analysis.md` |
| ad-20260501-034 | `support/prior-art/card-game-base-architecture-bb88f456.plan.md` | `Docs/Brimstone-Architecture-Analysis.md` |
| ad-20260501-035 | `support/prior-art/card-game-base-architecture-bb88f456.plan.md` | `Docs/Research/Card Game Architecture Research.md` |
| ad-20260501-036 | `support/prior-art/card-game-base-architecture-bb88f456.plan.md` | `Docs/Architecture/ReactiveNetworkQueue.md` |
| ad-20260501-037 | `support/prior-art/card-game-base-architecture-bb88f456.plan.md` | `Docs/Architecture/BlackBoxSimulation.md` |
| ad-20260501-038 | `support/prior-art/card-game-base-architecture-117b41c9.plan.md` | `Docs/Architecture/ReactiveNetworkQueue.md` |
| ad-20260501-039 | `support/prior-art/card-game-base-architecture-117b41c9.plan.md` | `Docs/SabberStone-Architecture-Analysis.md` |

### Notes

- Archive checks (A1–A4): zero findings — no archive/ subdirectories exist under any feature folder.
- S2 (builds-on): zero new findings — all active builds-on paths resolve. The two pre-existing S2 fixtures (ad-20260501-026/027) target _test-fixture*.md and were already open; skipped.
- S1 skipped paths: PRODUCT-BUILDER.md line 91 bare paths are inside inline backtick spans as illustrative filename examples — excluded per prose/code rule. REPORT-STYLE.md example paths (lines 238, 244) are inside triple-backtick code blocks — excluded.
- GAME-ARCHETYPES.md S1 hits: spikes/ links use URL-encoded filenames (%20(1)) that decode to filenames not present on disk. Actual spike files use different naming (sabberstone-architecture.md etc.).
- support/prior-art/*.plan.md S1 hits: these legacy plan files reference a Docs/ directory tree that does not exist in the current repo layout.

⚠️ Pass-mode: 18 open debt entries — consider a discharge pass before continuing.
