---
type: doc-rules
target: sample
status: accepted
references: [_workflow/PRODUCT-BUILDER.md, _docs/feature.md, _docs/model.md]
tags: [#workflow, #rules, #sample]
---

# Sample — rulebook + skeleton

A concrete reference instance projected onto the abstractions being designed. Exists to stress-test the design against real cases — a **floor** (simplest case the design must support) or a **ceiling** (complex case that bounds expressiveness).

Examples of subjects: a card game's rules and how its elements map onto framework concepts; a client codebase using a library; a protocol implementation; an integration for an API — anything concrete enough to project the design onto.

## Two layers

A sample doc has two layers:

- **Subject** — the concrete thing being described. Usually stable once written.
- **Projection** — how the subject's elements map onto the design abstractions. Moves whenever the design moves. This is the load-bearing layer.

## Non-negotiable gates

1. **Real or realistic concrete subject.** Not a contrived placeholder — something with enough substance to stress the design.
2. **Stated purpose: floor or ceiling.** Why this sample exists — what it's pinning down at the bottom or the top of the design space.
3. **Projection uses only `settled` abstractions** (for `locked` status). Locking onto draft/proposed abstractions creates sand under the sample.

## Decision flow

1. **Concrete subject?** No → not a sample.
2. **Floor or ceiling?** Yes → sample. Unclear → reconsider what the sample is exercising.
3. **Lock when ready** — see `draft → locked` transition below.

## Conventions (once classified as a sample)

- Lives under `Product/support/samples/` with a descriptive filename.
- Frontmatter: `type: sample`, `status:` (`draft` / `locked`), `tags:`.
- Body sections: Subject, Why this sample, Projection, What this does NOT exercise, Open mappings.
- Cross-references use `Product/`-rooted absolute paths.

## `draft` → `locked` transition

A sample becomes `locked` when **all** are true:

1. **Subject finalized.** The concrete description is fully specified; we aren't still debating what the example is.
2. **Projection uses only `settled` abstractions.** Every design concept the projection depends on is itself `settled`, not `draft` or `proposed`.
3. **Linter passes** (no broken references, no structure violations).
4. **Stable ≥ 1 session.** At least one session has passed without **contract edits** to either the sample or its dependencies. Soft cooldown. Hygiene edits (typos, formatting, link fixes, glossary-rename mirrors) don't reset the cooldown; only changes to Subject text, Projection mapping, or framing do. Mark commits explicitly: `contract edit — cooldown reset` or `hygiene edit — no cooldown reset`.

After locking, design changes that affect the sample surface as linter findings against the locked sample. Resolving them is a deliberate decision: re-lock with the new shape, unlock back to `draft`, or accept the divergence (rare).

## Audit obligation

When a gate or rule changes, every existing sample is re-checked against the new wording.

## Skeleton

    ---
    type: sample
    status: draft
    references: [<features or models the projection depends on>]
    tags: []
    ---

    # <sample name> — sample

    ## Subject

    The concrete thing this sample describes, in plain language. No design-doc terminology.

    ## Why this sample

    Floor / ceiling / stresses-which-feature.

    ## Projection

    How the subject's elements map onto the design abstractions.

    ## What this does NOT exercise

    Honest list — keeps us from over-claiming coverage.

    ## Open mappings

    Ambiguous bits, unresolved projections.
