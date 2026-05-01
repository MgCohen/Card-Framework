# Lint Report

📊 6 new entries · 2 skipped (already open, stale targets) · 8 total open

### Findings by rule
| Rule | New | Description |
|------|-----|-------------|
| A1   | 0   | Forward-pointer integrity |
| A2   | 0   | Stale inbound links |
| A3a  | 0   | Retired status, active location |
| A3b  | 0   | Active status, archive location |
| A4   | 0   | Missing successor |
| S1   | 6   | Broken path references |
| S2   | 0   | Broken builds-on references |

⚠️ Pass-mode: 14 total open debt entries — consider a discharge pass before continuing.

---

### S1 — Broken path references (new findings)

1. **`Product/general/GAME-ARCHETYPES.md`** — markdown link to `spikes/SabberStone-Architecture-Analysis.md (1)` resolves to a missing file. Actual spike is `spikes/sabberstone-architecture.md`.

2. **`Product/general/GAME-ARCHETYPES.md`** — markdown link to `spikes/Fireplace-Architecture-Analysis.md (1)` resolves to a missing file. Actual spike is `spikes/fireplace-architecture.md`.

3. **`Product/general/GAME-ARCHETYPES.md`** — markdown link to `spikes/Brimstone-Architecture-Analysis.md (1)` resolves to a missing file. Actual spike is `spikes/brimstone-architecture.md`.

4. **`Product/_workflow/PRODUCT-BUILDER.md`** — markdown links to `AI-FIRST-AUTOMATION.md` (×3, lines 625, 647, 711) target a missing file; no such document exists under `Product/_workflow/`.

5. **`Product/support/prior-art/card-game-base-architecture-bb88f456.plan.md`** — markdown links to `Docs/SabberStone-Architecture-Analysis.md`, `Docs/Fireplace-Architecture-Analysis.md`, `Docs/Brimstone-Architecture-Analysis.md`, `Docs/Research/Card Game Architecture Research.md`, `Docs/Architecture/ReactiveNetworkQueue.md`, `Docs/Architecture/BlackBoxSimulation.md` — all missing (prior-art Docs/ paths do not exist in this repo).

6. **`Product/support/prior-art/card-game-base-architecture-117b41c9.plan.md`** — markdown links to `Docs/Architecture/ReactiveNetworkQueue.md` and `Docs/SabberStone-Architecture-Analysis.md` — both missing.

---

### Skipped (already open in ledger)

- `ad-20260501-027` — S2 target `features/engine/models/_test-fixture2.md` (file no longer on disk; entry is stale but not re-opened)
- `ad-20260501-026` — S2 target `features/engine/models/_test-fixture.md` (file never on disk; entry is stale)

---

### ALIGNMENT-DEBT.md entries that would be appended (read-only baseline — not written)

```
- id: ad-20260501-028 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — broken path reference (spikes/SabberStone-Architecture-Analysis.md (1) missing)
- id: ad-20260501-029 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — broken path reference (spikes/Fireplace-Architecture-Analysis.md (1) missing)
- id: ad-20260501-030 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — broken path reference (spikes/Brimstone-Architecture-Analysis.md (1) missing)
- id: ad-20260501-031 | status: open | source: linter | target: _workflow/PRODUCT-BUILDER.md | reason: S1 — broken path reference (AI-FIRST-AUTOMATION.md missing, ×3 links)
- id: ad-20260501-032 | status: open | source: linter | target: support/prior-art/card-game-base-architecture-bb88f456.plan.md | reason: S1 — broken path references (Docs/... paths missing, ×6 links)
- id: ad-20260501-033 | status: open | source: linter | target: support/prior-art/card-game-base-architecture-117b41c9.plan.md | reason: S1 — broken path references (Docs/Architecture/ReactiveNetworkQueue.md, Docs/SabberStone-Architecture-Analysis.md missing)
```
