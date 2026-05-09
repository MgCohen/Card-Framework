---
type: doc-rules
target: research
status: accepted
references: [_workflow/PRODUCT-BUILDER.md, _docs/spike.md]
tags: [#workflow, #rules, #research]
---

# Research — rulebook

Captured external knowledge: studied games, codebase analyses, prior-art surveys, pattern catalogs. Research is the source; models cite it when a design decision drew on it.

**Research vs. spike.** Research has no planned outcome — it's revisited and refined in place. A spike has a resolution path (folds into a model or is abandoned). If a spike loses its resolution path, reclassify to research.

## Non-negotiable gates

1. **Captures external knowledge for later consultation.** Internal design content → that's a model, spike, or draft, not research.
2. **No planned outcome / resolution path.** A "when this lands, X gets settled" pointer → spike, reclassify.
3. **Can be revisited and refined in place.** Not archived as superseded; rewritten as understanding evolves.

## Decision flow

1. **Has a planned outcome?** No → research. Yes → spike.
2. **Drifted spike** (started as spike, no longer has resolution path) → reclassify to research.

## Conventions (once classified as research)

- Lives under `Product/research/` with a descriptive filename.
- Frontmatter: `type: research`. **No `status:` field** — research isn't a workflow item that ends.
- Tagged with `#research` plus a topic tag (`#prior-art`, `#pattern`, `#codebase`).
- Cross-references use `Product/`-rooted absolute paths.

## Heuristics

- Models cite research when a design decision drew on it. Research is the source; the model is canonical for usage.
- A second pass on the same topic appends or rewrites in place — don't archive prior-pass research as superseded.

## Audit obligation

When a gate or rule changes, every existing research doc is re-checked against the new wording.

## Skeleton

Research docs are shape-flexible — the content varies by what's being captured (architecture analysis, pattern catalog, prior-art survey). Minimum spine:

    ---
    type: research
    tags: [#research, <topic>]
    ---

    # <subject>

    ## Why this exists

    What knowledge this captures and why we'd consult it later.

    ## <body sections vary by content>

    Free-form. Architecture sketches, pattern tables, prior-art notes — whatever shape fits the subject.

    ## Citations

    Source material — links, papers, codebases studied.
