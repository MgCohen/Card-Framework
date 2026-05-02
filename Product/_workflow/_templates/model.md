---
type: model
status: draft
references: [<upstream features or models, or the literal `root`>]
tags: []
---

# <subject> — model

<!-- Strict section order. Sections may be empty with the stub `*n/a — purely descriptive Model.*`
     but cannot be reordered or omitted. The linter validates structure. -->

## Problem
What we're modeling and why this exists.

## Concepts
Types and entities in this system, glossary-style.

## Contracts
Interfaces, methods, return shapes, forbidden behaviors.

## Invariants
What must always be true.

## Examples
At minimum, walked through against the floor sample.

## Open follow-ups
Unresolved threads. (Promote durable items to `ledgers/OPEN-ITEMS.md`; ephemerals to `SESSION-CHECKPOINT.md ## Scratch`.)

---

*Below the divider: provenance — what was considered, why we chose this, what it constrains. Feature-internal decisions live here, not in a separate `features/<feature>/decisions/` ADR file.*

## Options considered
- **A** — rejected because ...
- **B** — rejected because ...
- **C** — chosen.

*If a sub-decision deserves more depth, write it as a sub-section below `## Options considered` (e.g., `### Q1 — visibility mechanism` with its own options/lean/rationale). Multi-decision Models accumulate sub-sections in narrative order.*

## Rationale
Why the chosen approach. Skip if obvious.

## Consequences
What this enables; what it constrains. Skip if obvious.

## ADR
*(Optional — present only if a project-wide ADR was opened for cross-cutting decisions involving this Model. Feature-internal decisions stay above; cross-cutting goes to `Product/decisions/`.)*

Linked: `Product/decisions/NNNN-<title>.md`
