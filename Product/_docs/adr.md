---
type: doc-rules
target: adr
status: accepted
references: [_workflow/PRODUCT-BUILDER.md, _docs/model.md]
tags: [#workflow, #rules, #adr]
---

# ADR — rulebook + skeleton

Project-wide / cross-feature decision history. Reserved for decisions that genuinely span feature boundaries.

**Feature-internal decisions live in the affected model's provenance section** (`## Options considered` / `## Rationale` / `## Consequences`), not as ADRs. The ADR is duplicating provenance that grep already finds; collapse into the model.

## Non-negotiable gates

1. **Cross-feature reach.** Decision touches **≥2 features** — multiple features cite it or are constrained by it. Single-feature decision → fold into the affected model's provenance, not an ADR.
2. **Captures alternatives + chosen option + rationale.** A decision without weighed alternatives is a note, not an ADR.
3. **Decision is durable.** Append-only history, not iterated content. If still being iterated → that's draft territory.

## Decision flow

1. **Cross-feature reach?** Yes → ADR. No → fold into the model that owns the affected surface.
2. **If unsure, default to fold-into-model.** Easier to elevate to ADR later than to demote.

## Conventions (once classified as an ADR)

- Lives at `Product/decisions/NNNN-<slug>.md` (project-wide counter, sequential numbering).
- Frontmatter: `type: adr`, `status:` (`proposed` / `accepted` / `superseded`).
- Append-only — status flips, history isn't rewritten. Supersede with a new ADR rather than rewriting an accepted one.
- Cross-references use `Product/`-rooted absolute paths.

## Heuristics

- **Hard scope rule: ≥2 features in scope.** A decision that touches only one feature — even one cited from many models inside that feature — lives in the affected model's provenance, not as an ADR. ADRs apply only when the decision genuinely spans feature boundaries: cross-feature principles, framework-wide naming, project-shaping moves, or decisions cited from models in two or more sibling features.
- If unsure, default to fold-into-model.

## Migration recipe — retiring `features/<feature>/decisions/`

For projects that accumulated feature-internal ADRs before the cross-feature rule, migrate per-ADR:

1. **Decide outcome.** Apply the ≥2-features hard-scope rule. Yes → **elevate** to `Product/decisions/NNNN-<slug>.md`. No → **fold** into the affected model's provenance below the divider.
2. **Preserve dates.** Both original and migration date stay visible. Folded provenance gains a one-line *"Decided YYYY-MM-DD (originally `<old-path>`); folded YYYY-MM-DD."* note.
3. **Apply the move.** Elevate: copy to new ADR file, renumber. Fold: paste into host model's provenance, sub-section as `### Q-N — <slug>` if multi-decision.
4. **Inbound-ref sweep.** Find every doc that references the retired ADR; update each to point at the new home.
5. **Delete the old file.** Remove `features/<feature>/decisions/<file>.md`. When the feature folder is empty, delete the `decisions/` directory.

**Supersede-then-fold.** When an old ADR's decision has been overturned, fold the *superseded* body — preserving the supersession record — rather than the live one. Apply supersession header first, then fold with header intact.

## Audit obligation

When a gate or rule changes, every existing ADR is re-checked against the new wording.

## Skeleton

    ---
    type: adr
    status: proposed
    references: [<related models or features>]
    tags: []
    ---

    # ADR-NNNN — <title>

    Context-doc: `Product/features/<feature>/models/NN-<slug>.md` (or whichever model surfaced the decision)

    ## Context

    Two paragraphs max.

    ## Options considered

    - A: ... — rejected because ...
    - B: ... — rejected because ...
    - C: ... — chosen.

    ## Decision

    One paragraph.

    ## Consequences

    - Enables: ...
    - Constrains: ...
    - Reverts/replaces: ADR-NNNN
