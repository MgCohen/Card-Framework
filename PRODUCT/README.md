---
type: index
last-updated: 2026-04-29
tags: [#index]
---

# PRODUCT/

Canonical knowledge layer for the Card Framework. All durable design content lives here. Workflow rules live in `general/PRODUCT-BUILDER.md` (post-Phase-8) — until then refer to `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md`.

## Layout

- **`general/`** — project-wide orientation, ledgers, and the workflow doc.
  - `VISION.md` — north star + scope.
  - `GAME-ARCHETYPES.md` — representative card-game families.
  - `CAPABILITY-MAP.md` — broad inventory of framework systems.
  - `ARCHITECTURE.md` — candidate boundaries and layers.
  - `ROADMAP.md` — learning + construction phases.
  - `RECOMMENDATIONS.md` — opinionated synthesis from research.
  - `GLOSSARY.md` — working language for the framework.
  - `OPEN-QUESTIONS.md` — durable unresolved questions (three-lifetime form).
  - `ALIGNMENT-DEBT.md` — cascade ledger (single source of truth).
  - `BACKLOG.md` — deferred concrete work.
  - `FEATURE-MAP.md` — bird's-eye index, dependency graph, model breakdown.
  - `_templates/` — 7 doc-type templates (spike, feature, model, sample, adr, session, checkpoint).
  - `decisions/` — project-wide ADRs.

- **`features/`** — feature folders with `<feature>.md` root, `notes/` (Models), `decisions/` (ADRs).
  Current features: `entities/`, `zones/`, `actions/`, `triggers/`, `effects/`, `engine/`, `presentation/`.

- **`spikes/`** — exploratory investigations. Status: `active`, `concluded`, or `graduated`.

- **`support/`**
  - `samples/` — game implementations exercising the framework (e.g. `pile-counter.md`).
  - `prior-art/` — historical/reference docs not actively informing decisions.

- **`sessions/`** — per-session notes (`session-NN.md`). Backfilled retroactively.

- **`SESSION-CHECKPOINT.md`** — current cross-session state of the project.

## Reading order on resume

1. `SESSION-CHECKPOINT.md` — what's settled and what's next.
2. `general/PRODUCT-BUILDER.md` (or `ProductBuilder/PRODUCT-BUILDER-PROPOSAL.md` pre-Phase-8) — workflow rules.
3. `general/FEATURE-MAP.md` — current shape of the design.
4. Feature root for the area in play.

## Source-of-truth pointers

- Cascade ledger: `general/ALIGNMENT-DEBT.md`.
- Deferred work: `general/BACKLOG.md`.
- Workflow ADR: `general/decisions/0001-adopt-workflow.md` (added in Phase 8).
