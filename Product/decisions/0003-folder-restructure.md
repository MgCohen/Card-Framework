---
type: adr
status: accepted
references: [_workflow/PRODUCT-BUILDER.md]
tags: [#adr, #workflow, #restructure]
---

# ADR-0003 — Restructure Product/ folder layout

Date: 2026-04-29
Context-doc: Product/_workflow/PRODUCT-BUILDER.md
Proposal: Product/_workflow/PRODUCT-BUILDER.md §1

## Context

ADR-0001 adopted the workflow with a single `general/` folder holding everything cross-cutting: the workflow spec (`PRODUCT-BUILDER.md`), the doc-type templates, the three ledgers + glossary + feature-map, project-wide ADRs, and project-orientation files (`VISION`, `GAME-ARCHETYPES`, `CAPABILITY-MAP`, `ARCHITECTURE`, `ROADMAP`, `RECOMMENDATIONS`).

Three problems with that layout surfaced after operating it for one session post-migration:

1. **Workflow files were mixed with project content.** `PRODUCT-BUILDER.md` (project-agnostic spec) sat next to `GAME-ARCHETYPES.md` (Card Framework's analysis of card games). Conceptually different artifacts; the same folder said nothing about which was which.
2. **`PRODUCT/` was shouty all-caps**, breaking the visual convention used elsewhere in the repo (`Assets/`, `Library/`, `Packages/`).
3. **`features/<f>/notes/` only ever contained Models** — the `notes/` name was vestigial from an earlier draft when the folder was meant to be more general. The doc type is *Model*; the folder pretended to be broader than it is.

A fourth observation drove the split shape: the workflow definition is *rarely opened* in normal operation (the operating manual at `Product/README.md` is sufficient for daily work), while the ledgers are *opened daily*. Bundling them in one folder ignored that volatility difference.

## Options considered

- **A: Keep the existing layout, tighten prose.** Rejected — the file mix would persist. Cleaning up wording in `PRODUCT-BUILDER.md` doesn't change which folder a `git status` shows you.
- **B: Pull only `_workflow/` out (spec + templates), leave ledgers in `general/`.** Rejected — half-measure. The complaint that workflow-shaped files mix with project orientation in `general/` would still apply to `OPEN-ITEMS.md`, `ALIGNMENT-DEBT.md`, etc.
- **C: Three-way split — `_workflow/` + `ledgers/` + `general/` (orientation only) + top-level `decisions/`.** Chosen. Cleanly separates: workflow definition (rarely opened), workflow-shaped working files (daily), pure project orientation (occasional), project-wide ADRs (load-bearing context, lifted to top level for visibility).

## Decision

Adopt the three-way split. Concretely:

- `PRODUCT/` → `Product/` (case only).
- `Product/_workflow/` holds `PRODUCT-BUILDER.md` and `_templates/`. The `_` prefix sorts the folder above letter-prefixed siblings naturally; the name signals "workflow infrastructure, rarely opened."
- `Product/ledgers/` holds `OPEN-ITEMS.md`, `GLOSSARY.md`, `FEATURE-MAP.md`. Glossary and feature-map are honorary ledgers (workflow-shaped files); the folder name is the closest single-word fit. (Original split also included `OPEN-QUESTIONS.md`, `ALIGNMENT-DEBT.md`, and `BACKLOG.md`; all three were merged into `OPEN-ITEMS.md` in the doc-workflow simplification — see retired-tokens registry.)
- `Product/decisions/` (top-level, lifted from `general/decisions/`) holds project-wide ADRs.
- `Product/general/` keeps only project orientation: `VISION`, `GAME-ARCHETYPES`, `ARCHITECTURE`, `ROADMAP`, `RECOMMENDATIONS`.
- `Product/features/<f>/notes/` → `Product/features/<f>/models/` for every feature. Folder name now matches the only doc type that lives there.

The migration was carried out in this same commit: `git mv` of folders and files, sed pass for path references across all `.md` files in scope (AGENTS.md, Product/, ProductBuilder/, .claude/commands/builder/), surgical prose updates to `_workflow/PRODUCT-BUILDER.md` §1 + cross-cutting prose, and updates to the operating manual at `Product/README.md`.

## Consequences

- **Enables:** clean conceptual separation between workflow definition, workflow-defined working files, and project content. Daily ops touch `ledgers/`, `decisions/`, `features/`, `sessions/`. Workflow-rules debate touches `_workflow/`. Two distinct folders, two distinct cadences.
- **Enables:** `features/<f>/models/` reads self-documenting in cross-references — `features/actions/models/01-command-pipeline.md` literally says "the command-pipeline model in the actions feature."
- **Constrains:** every cross-reference in every existing doc had to be rewritten in this commit. The Tier 1 builder commands at `.claude/commands/builder/*.md` were updated as part of the migration.
- **Constrains:** future workflow renames of canonical folders (`_workflow/`, `ledgers/`, `decisions/`, `general/`, `features/`) carry the same migration cost.
- **Reverts/replaces:** partially supersedes ADR-0001 (`decisions/0001-adopt-workflow.md`) — specifically the folder-layout choice in option C of that ADR. The rest of ADR-0001 (workflow adoption, doc types, status gates, three ledgers, cascade discipline) stands unchanged. ADR-0001 retains `status: accepted`; only the folder-layout subset is refined here.
- **Drift caveat:** session notes for sessions 01–12 contain prose references to the old folder names (`general/` as a holder of ledgers, etc.). Those are historical record of past state and are not edited; the workflow spec and operating manual reflect the new layout.

## Notes

This change has no impact on `settled` or `implemented` Models — moves are pure path renames, not contract edits. By §5.7, debt entries are not opened: the ledger is a cascade-of-meaning tracker, and a folder rename doesn't change what any Model says. The `builds-on:` paths inside Models are updated by the same sed pass.
