---
type: doc-rules
target: draft
status: accepted
references: [_workflow/PRODUCT-BUILDER.md]
tags: [#workflow, #rules, #draft]
---

# Draft — rulebook

In-flight content that would churn its canonical home if iterated there. A draft amortizes the iteration cost — once the shape stabilizes, the draft promotes to its target home in one commit.

## Non-negotiable gates

1. **Target shape is unstable.** Multiple iterations expected before stabilizing. Stable target → direct edit at the target home.
2. **Iterating directly at the canonical home would churn it.** The draft exists because the iteration is too noisy for the target home.
3. **Has a named target home.** The draft states explicitly where the content will land once stable.

## Decision flow

1. **Target shape unstable + multiple iterations expected?** Yes → draft. No → direct edit at target.
2. **Stable enough to land?** Promote in one commit; delete the draft file.

## Conventions (once classified as a draft)

- Lives under `Product/_drafts/` with a descriptive filename.
- Frontmatter: `type: draft`, `status: in-flight`.
- States its target home explicitly in the body (near top).
- Promotes in one commit when stable; original draft file is deleted.
- Cross-references use `Product/`-rooted absolute paths.

## Heuristics

- **Drafts amortize iteration** — iterate freely, promote in one commit when stable.
- **Drafts can carry meta-content** (open questions about the draft itself) that wouldn't belong at the target home.
- **Stale draft surfacing** — drafts that haven't moved in N sessions surface for "promote, abandon, or rewrite." (N TBD.)

## Audit obligation

When a gate or rule changes, every existing draft is re-checked against the new wording.
