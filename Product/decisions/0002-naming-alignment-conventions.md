---
type: adr
status: accepted
tags: [#adr, #general, #naming, #conventions]
---

# ADR-0002 — Naming alignment conventions

Date: 2026-04-29
Context-doc: ledgers/GLOSSARY.md
Proposal: features/actions/decisions/0008-intent-vocabulary.md

## Context
Several research-era renames cascaded through many docs (Request→Intent, ActionId retired, CommandPreHook/CommandReaction → OnBefore/OnAfter, etc.). Without a written convention, future renames risk re-fragmenting the vocabulary.

## Options considered
- A: No formal convention; rename ad hoc per session — rejected because past renames showed the cascade is expensive without rules.
- B: Lock vocabulary now, no further renames — rejected because the project is research-phase; some renames are still right.
- C: Codify rules: prefer domain-meaningful nouns over generic ("Intent" not "Request"), prefer typed discriminators over string IDs, retire identifiers when the type already discriminates, register every cross-doc rename in `ALIGNMENT-DEBT.md`. Chosen.

## Decision
Naming changes that span more than one feature follow the rules above and *must* be opened as alignment-debt entries until propagated. Single-doc renames don't require a ledger entry but should still prefer domain-meaningful nouns and typed discriminators.

(ADR counter starts at `0002` because `0001-adopt-workflow.md` is reserved for the workflow-adoption ADR landed in Phase 8 of the migration.)

## Consequences
- Enables systematic propagation of cross-cutting renames.
- Constrains: any cross-feature rename pays the cost of opening and discharging an alignment-debt entry.
