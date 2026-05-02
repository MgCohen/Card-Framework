---
type: adr
status: accepted
tags: [#adr, #general, #workflow, #adoption]
---

# ADR-0001 — Adopt the Product Builder workflow

Date: 2026-04-29
Context-doc: _workflow/PRODUCT-BUILDER.md
Proposal: _workflow/PRODUCT-BUILDER.md

## Context
After 11 sessions of flat `RESEARCH/` work the project's design content had outgrown a single chronological list. Decisions were folded into mega-docs, cross-references were brittle (`doc 28 §6`), exploration mixed with feature design, and there was no canonical place for ADRs, ledgers, or session notes. Session 11 produced a workflow proposal; session 12 ran the migration. This ADR records the adoption decision.

## Options considered
- A: Keep flat `RESEARCH/` + add ADRs alongside — rejected because it doesn't address mega-docs, spike/feature mixing, or alignment-debt tracking.
- B: Adopt a heavy framework (e.g., a dedicated docs SaaS) — rejected because the project is small and tooling overhead would dominate value.
- C: Adopt the seven-doc-type, feature-first, `general/`+`features/`+`spikes/`+`support/`+`sessions/` structure with three ledgers (`ALIGNMENT-DEBT`, `BACKLOG`, `FEATURE-MAP`). Chosen.

## Decision
The workflow described in `_workflow/PRODUCT-BUILDER.md` is the canonical Product Builder workflow for this project. Forward-going design work follows it. The migration from the legacy flat `RESEARCH/` layout into `Product/` was carried out in session 12 (see `sessions/session-12.md`) per `ProductBuilder/MIGRATION-GUIDE.md`.

## Consequences
- Enables typed cross-references (`features/<feature>/decisions/NNNN-<title>.md`), per-feature counters, and stable navigation.
- Enables alignment-debt tracking as a single ledger, removing the dual-write drift that comes with inline `## Implications` sections.
- Enables a clear two-gate lifecycle: `draft → settled` (research) and `settled → implemented` (build).
- Constrains: every cross-feature rename must open an alignment-debt entry; every `settled` Model edit must declare debt or "none."
- Re-open trigger: a workflow change of similar scope would require a new project-wide ADR (`decisions/`) and a planned migration guide.
