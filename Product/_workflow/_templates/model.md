---
type: model
status: draft
references: [<upstream features or models, or the literal `root`>]
tags: []
---

# <subject> — model

<!-- Strict section order. 5 required + 1 optional above the divider; 3 below.
     Required sections may be empty with a stub line (e.g. `*n/a — see Contracts.*`)
     but cannot be reordered or omitted. `## Decisions` is optional — omit when no
     project-wide ADR applies. The linter validates structure. -->

## What it is
High-level orientation + a canonical artifact (code snippet, algorithm pseudocode, diagram — whichever fits the Model's shape). A reader gets the shape in 30 seconds.

## Contracts
Full API surface: interfaces, methods, signatures, return shapes, forbidden behaviors. The developer's checklist for implementation.

## Invariants
Model-local rules (cross-Model rules live in the Feature root's `## Invariants`).

## Decisions
*(Optional — omit if no project-wide ADR applies. Feature-internal decisions live below the divider.)*

Linked: `Product/decisions/NNNN-<title>.md`

## Examples
Walked through against the floor sample, or a stub `*n/a — see Contracts.*` if no example fits the shape.

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
