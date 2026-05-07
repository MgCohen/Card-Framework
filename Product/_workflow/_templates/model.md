---
type: model
status: draft
references: [<upstream features or models, or the literal `root`>]
tags: []
---

# <subject> — model

<!-- Strict section order. 6 sections above the divider; 3 below.
     Sections may be empty with a stub line (e.g. `*n/a — see Contracts.*` or
     `*n/a — no elevated ADR.*` for `## Decisions`) but cannot be reordered
     or omitted. The linter validates structure. -->

## What it is
High-level orientation in 30 seconds. SHOULD include a canonical artifact that fits the Model's shape:

| Model shape | Artifact form |
|---|---|
| Class / interface / contract | Code snippet (signatures + types) |
| Algorithm | Pseudocode |
| State machine / lifecycle | State diagram |
| Data structure / schema | Shape sketch |
| Pipeline / pass / phase | Arrow chain |

If no form fits cleanly, the stub `*n/a — see Contracts.*` is permitted.

## Contracts
What this Model defines: types, interfaces, signatures, semantics, forbidden behaviors. Implementation is welcome when the framework owns it; surface-only signatures are fine when implementation is game-supplied. Consumer usage and user-supplied extensions live in `## Examples`.

## Invariants
Model-local rules (cross-Model rules live in the Feature root's `## Invariants`, referenced by name).

## Decisions
**Reserved for elevated ADRs.** Project-wide ADRs (per §4.5's two-feature rule) that shape this Model. Feature-internal decisions live below the divider, not here. If no ADR applies, stub `*n/a — no elevated ADR.*`.

Linked: `Product/decisions/NNNN-<title>.md`

## Examples
Consumers of the contract — **user extensions** (game-supplied implementations of a framework interface, e.g., `DealDamageHandler : ICommandHandler<...>`) and/or **call-site usage** (code invoking the contract, traced flows, wiring sketches). If no consumer interaction fits the Model's shape, stub `*n/a — see Contracts.*`.

## Open follow-ups
Unresolved threads. (Promote durable items to `ledgers/OPEN-ITEMS.md`; ephemerals to `SESSION-CHECKPOINT.md ## Scratch`.)

---

*Below the divider: provenance — what was considered, why we chose this, what it constrains. Feature-internal decisions live here, not in a separate `features/<feature>/decisions/` ADR file.*

## Options considered
- **A** — rejected because ...
- **B** — rejected because ...
- **C** — chosen.

*Multi-decision Models: accumulate `### Q-N — <slug>` sub-sections in narrative order, each with its own options / lean / rationale.*

## Rationale
Why the chosen approach. Skip if obvious.

## Consequences
What this enables; what it constrains. Skip if obvious.
