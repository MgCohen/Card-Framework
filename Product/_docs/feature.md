---
type: doc-rules
target: feature
status: accepted
references: [_workflow/PRODUCT-BUILDER.md, _docs/model.md]
tags: [#workflow, #rules, #feature]
---

# Feature — rulebook + skeleton

The capability-level entry point. Carries composition (which models make it up), cross-model architecture, shared vocabulary, and cross-model invariants — the synthesis layer above the buildable models.

## Non-negotiable gates

A doc fails to qualify as a feature if any of these gates fails.

1. **Coherent capability with a name.** A thing the framework offers, not a grouping of unrelated stuff. If the scope is an internal concern of one existing feature → fold into that feature.
2. **Multiple models OR external integration.** A feature needs more than one buildable model under it, or it integrates an external system (Scaffold, Unity, etc.). Single-focused-thing → that's one model under an existing feature, not a feature.
3. **Real consumers across other features.** Other features, game authors, runtime, or presentation reference it. Nothing outside the feature references it → reconsider; might be internal to one feature.

## Decision flow

1. **Coherent capability with a name?** No → fold into existing feature.
2. **Multiple models OR integrates an external system?** No → likely a single model under an existing feature.
3. **Real consumers across other features?** No → reconsider; might be internal to one feature.

## Conventions (once classified as a feature)

- Lives at `Product/features/<feature>/<feature>.md` (root file matches folder name).
- Frontmatter: `type: feature`, `status:`, `references:`, `tags:`.
- Body sections: What it is, Vocabulary, Architecture, Composition, Invariants, Boundaries, Dependencies, Open.
- Vocabulary section lists feature-internal terms only — cross-feature terms link to canonical home or `Product/ledgers/GLOSSARY.md`.
- Models live under `Product/features/<feature>/models/` with numbered prefix.
- Cross-references use `Product/`-rooted absolute paths.

## Heuristics

- **Feature-root-only is allowed** for infrastructure / package-integration features. No models needed if the feature root carries the whole story.
- **A feature with one model** keeps both files separate. Feature root carries synthesis; model carries depth. If the synthesis would be redundant, reconsider the feature.
- **Consider splitting** when models cluster around two distinct capabilities consumers reference separately.
- **Cross-cutting conventions** (rules / patterns / recipes that span multiple types in the feature) belong in the feature root, not in any one model.

## Audit obligation

When a gate or rule changes, every existing feature is re-checked against the new wording.

## Skeleton

    ---
    type: feature
    status: draft
    references: [<upstream features or models>]
    tags: []
    ---

    # <feature> — feature

    ## What it is

    High-level prose framing the feature in 30 seconds. SHOULD include a canonical artifact — a small visual or structural rendering that lets a reader grasp the shape without opening models.

    | Feature shape | Artifact form |
    |---|---|
    | Sequential pipeline / phase order | Flow diagram or arrow chain |
    | Component composition / orchestration | ASCII layout or component sketch |
    | State machine / lifecycle | State diagram |

    Stub `*n/a — descriptive only.*` permitted if no form fits.

    ## Vocabulary

    **Feature-owned terms only.** Terms defined in another feature, in `Product/ledgers/GLOSSARY.md`, or in upstream prior art do not belong here — link to their canonical home with a one-line pointer.

    | Term | Meaning |
    |---|---|
    | `<term>` | <one-line definition> |

    ## Architecture

    Cross-model flow: how the models in this feature fit together — phase order, interaction sequence, what calls what. Size to model count:

    | Model count | Architecture depth |
    |---|---|
    | **1** | Thin — single sentence pointing at the model. Stub `*n/a — see model 01.*` permitted. |
    | **2–3** | Medium — paragraph or short diagram. |
    | **4+** | Deep — sub-headers per phase or sub-system; sequence diagram or component map. |

    ## Composition

    | Model | Role |
    |---|---|
    | `models/01-<slug>.md` | <one-line role> |
    | `models/02-<slug>.md` | <one-line role> |

    ## Invariants

    Cross-model rules — properties that must hold across the feature's models. Named, so individual models can reference them by name rather than restating. Model-local invariants live in the models themselves.

    - **<Invariant name>.** <one-line rule>

    ## Boundaries

    **Owns:** <list>

    **Does not own:**
    - <thing> → <feature that does>

    ## Dependencies

    *Mirrors the front-matter `references:` array in human-readable form.*

    - **Upstream:** features/X — consumed
    - **Downstream:** features/Y — consumes this

    ## Open

    Pointers to `Product/ledgers/OPEN-ITEMS.md` items relevant to this feature.
