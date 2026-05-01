# Lint Report — All Rules

📊 11 new findings · 1 skipped (already open as ad-20260501-026) · 5 open in ledger pre-run

Checked: 102 docs in `Product/` (excluding `_workflow/_validation/`)
Date: 2026-05-01
Mode: `--all` (archive integrity + path resolution + builds-on)

---

## Summary by check type

| Check | Rule | New findings | Already logged | Status |
|-------|------|-------------|----------------|--------|
| Archive integrity — forward-pointer | A1 | 0 | 0 | Clean |
| Archive integrity — stale inbound links | A2 | 0 | 0 | Clean |
| Archive integrity — status/location | A3a | 0 | 0 | Clean |
| Archive integrity — status in archive/ | A3b | 0 | 0 | Clean |
| Archive integrity — successor presence | A4 | 0 | 0 | Clean |
| Broken path references | S1 | 8 | 1 | Issues found |
| builds-on references | S2 | 3 | 0 | Issues found |
| **Total** | | **11** | **1** | |

---

## A — Archive Integrity

All 7 `archive/` directories (one per feature) are empty. No archived docs exist. All A1–A4 checks pass trivially.

**Clean — no archive integrity findings**

---

## S1 — Broken Path References

### S1-1 through S1-3: Stale spike links in GAME-ARCHETYPES.md (genuine broken links)

| Rule | Doc | Line | Reference | Actual file |
|------|-----|------|-----------|-------------|
| S1 | `general/GAME-ARCHETYPES.md` | 56 | `spikes/SabberStone-Architecture-Analysis.md%20(1)` | `spikes/sabberstone-architecture.md` |
| S1 | `general/GAME-ARCHETYPES.md` | 57 | `spikes/Fireplace-Architecture-Analysis.md%20(1)` | `spikes/fireplace-architecture.md` |
| S1 | `general/GAME-ARCHETYPES.md` | 58 | `spikes/Brimstone-Architecture-Analysis.md%20(1)` | `spikes/brimstone-architecture.md` |

The three spike links use stale filenames with `%20` URL-encoding (old files with spaces and a `(1)` copy suffix). The spikes were renamed to lowercase kebab-case during migration. The link text is correct but the targets do not exist.

Fix: Update lines 56-58 to use the actual filenames: `spikes/sabberstone-architecture.md`, `spikes/fireplace-architecture.md`, `spikes/brimstone-architecture.md`.

---

### S1-4: Wrong-depth relative link in 04-gamestate-slices.md (genuine broken link)

| Rule | Doc | Lines | Reference | Resolves to | Correct |
|------|-----|-------|-----------|-------------|---------|
| S1 | `features/engine/models/04-gamestate-slices.md` | 83, 150, 162 | `../ledgers/OPEN-QUESTIONS.md` | `features/engine/ledgers/OPEN-QUESTIONS.md` (missing) | `../../../ledgers/OPEN-QUESTIONS.md` |

The file lives at `features/engine/models/` (3 levels deep under `Product/`). One `..` step reaches `features/engine/`, not `Product/`. The ledger is at `Product/ledgers/OPEN-QUESTIONS.md`, requiring 3 `../` steps up. All three occurrences share the same bug.

Fix: Replace `../ledgers/OPEN-QUESTIONS.md` with `../../../ledgers/OPEN-QUESTIONS.md` at lines 83, 150, and 162.

---

### S1-5 through S1-8: Phantom paths in prose/code examples (low severity)

These paths appear as backtick-quoted text or inside code block examples, not as clickable `[text](path)` hyperlinks. Readers cannot follow them to a 404. Logged for completeness; no immediate action required.

| Rule | Doc | Line | Path | Context |
|------|-----|------|------|---------|
| S1 | `_workflow/PRODUCT-BUILDER.md` | 91 | `features/actions/decisions/0002-async-layer-hierarchy.md` | Prose naming example (intentionally fabricated) |
| S1 | `_workflow/PRODUCT-BUILDER.md` | 91 | `features/actions/models/03-action-definition.md` | Prose naming example (intentionally fabricated) |
| S1 | `_workflow/REPORT-STYLE.md` | 238 | `features/engine/models/04-evaluation-loop.md` | Sample end-session output block |
| S1 | `_workflow/REPORT-STYLE.md` | 272 | `features/engine/archive/01-old.md` | Sample lint report table |

---

### S1 — already logged

| ID | Doc | Detail |
|----|-----|--------|
| ad-20260501-026 | `features/engine/models/_test-fixture.md` | S2 test fixture in ALIGNMENT-DEBT; file intentionally absent |

---

## S2 — builds-on References

All 13 `builds-on:` entries in `Product/features/` resolve correctly. Issues are in `_workflow/` docs using same-directory shorthand rather than `Product/`-root-relative paths.

| Rule | Doc | Entry | Resolution from `Product/` root | Status |
|------|-----|-------|----------------------------------|--------|
| S2 | `_workflow/REPORT-STYLE.md` | `PRODUCT-BUILDER.md` | `Product/PRODUCT-BUILDER.md` — MISSING (exists at `_workflow/PRODUCT-BUILDER.md`) | Broken |
| S2 | `_workflow/REPORT-STYLE.md` | `EMOJI-LEGEND.md` | `Product/EMOJI-LEGEND.md` — MISSING (exists at `_workflow/EMOJI-LEGEND.md`) | Broken |
| S2 | `_workflow/EMOJI-LEGEND.md` | `PRODUCT-BUILDER.md` | `Product/PRODUCT-BUILDER.md` — MISSING (exists at `_workflow/PRODUCT-BUILDER.md`) | Broken |

`PRODUCT-BUILDER.md` itself uses `Product/`-root convention (`builds-on: [SESSION-CHECKPOINT.md, ledgers/OPEN-QUESTIONS.md]` — both verified at root). `REPORT-STYLE.md` and `EMOJI-LEGEND.md` omit the `_workflow/` prefix, producing dangling references. Fix: prefix entries with `_workflow/`.

---

## Prior-art docs (informational, out of scope)

Two `.plan.md` files in `support/prior-art/` reference pre-migration paths (`Docs/` and `Assets/Scripts/`) that no longer exist. These are historical plan documents predating the Product Builder workflow; stale links are expected. Not proposed as new debt entries.

| File | Stale reference type |
|------|---------------------|
| `support/prior-art/card-game-base-architecture-bb88f456.plan.md` | `Docs/` (5 paths) + `Assets/Scripts/` (7 paths) |
| `support/prior-art/card-game-base-architecture-117b41c9.plan.md` | `Docs/` (2 paths) + `Assets/Scripts/` (5 paths) + `Generators/` (1 path) |

---

## Proposed new ALIGNMENT-DEBT entries

```
- id: ad-20260501-027 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — line 56 broken link: spikes/SabberStone-Architecture-Analysis.md%20(1) not found; actual file is spikes/sabberstone-architecture.md
- id: ad-20260501-028 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — line 57 broken link: spikes/Fireplace-Architecture-Analysis.md%20(1) not found; actual file is spikes/fireplace-architecture.md
- id: ad-20260501-029 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — line 58 broken link: spikes/Brimstone-Architecture-Analysis.md%20(1) not found; actual file is spikes/brimstone-architecture.md
- id: ad-20260501-030 | status: open | source: linter | target: features/engine/models/04-gamestate-slices.md | reason: S1 — lines 83/150/162: ../ledgers/OPEN-QUESTIONS.md resolves to features/engine/ledgers/ (missing); correct relative path is ../../../ledgers/OPEN-QUESTIONS.md
- id: ad-20260501-031 | status: open | source: linter | target: _workflow/REPORT-STYLE.md | reason: S2 — builds-on [PRODUCT-BUILDER.md, EMOJI-LEGEND.md] not found at Product/ root; should be [_workflow/PRODUCT-BUILDER.md, _workflow/EMOJI-LEGEND.md]
- id: ad-20260501-032 | status: open | source: linter | target: _workflow/EMOJI-LEGEND.md | reason: S2 — builds-on [PRODUCT-BUILDER.md] not found at Product/ root; should be _workflow/PRODUCT-BUILDER.md
```

Debt opened: 6 new entries proposed (027-032)
Existing open entries pre-run: 5 (ad-20260501-022 through 026)
Total open after this run: 11
