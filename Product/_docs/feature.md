---
type: doc-rules
target: feature
status: accepted
references: [_workflow/PRODUCT-BUILDER.md, _docs/model.md]
tags: [#workflow, #rules, #feature]
---

# Feature — rulebook + skeleton

The synthesis layer above the models. **One feature = one capability the framework offers, expressed as a flow across multiple types.**

If you are creating, auditing, or reviewing a feature, the rules below apply at every step. The skeleton at the bottom is what you instantiate.

## Non-negotiable gates

A doc fails to qualify as a feature if any of these gates fails. Each gate is a yes/no test.

1. **Capability with a name and a result.** The framework offers this thing under a name; it produces something nameable (a flow, a state, an artifact, a contract). Process / topic / pattern as the candidate → **NOT A FEATURE** — fold into the capability that owns the process.
2. **Synthesis-layer body.** The body discusses **across** multiple types — how they connect, the flow between them, the patterns used. If the body is one type explained in depth → **NOT A FEATURE** — that's a model under a parent feature.
3. **Named consumer.** At least one nameable consumer (another feature root, runtime, game author, presentation). If no consumer can be named → **NOT A FEATURE** — fold into a parent feature.
4. **Synthesis pulls its own weight.** Even one-model features must have synthesis to write — boundaries, integration with neighbors, cross-feature invariants. If the feature root would only paraphrase the model → **NOT A FEATURE** — fold the model under a parent feature.

## Decision flow

Apply in order. The first failure determines the verdict.

1. **Capability with a name and a result?** Yes → continue. No → **NOT A FEATURE** — fold into the capability that owns it.
2. **Synthesis-layer body?** Yes → continue. One-type-in-depth → **NOT A FEATURE** — that's a model, fold up.
3. **Named consumer?** Yes → continue. None → **NOT A FEATURE** — fold.
4. **Synthesis pulls its own weight?** Yes → **FEATURE.** No → fold the model under a parent feature.

## Anti-patterns (recognise and reject)

| Smell | What it looks like | Why it fails | Where the content goes |
|---|---|---|---|
| **Bag of related types** | Feature root inlines `### CardDefinition`, `### CardRef`, ... with full signatures | No synthesis, just an index. Fails synthesis-layer test. | Each type → its own model. Feature root carries cross-model story only. |
| **Re-definition of model content** | Feature root re-states a class signature already in `models/01-X.md` | Repetition; drift target. Fails canonical-home rule (model owns the type). | Replace with a reference to the model + a one-line role. |
| **Process-as-feature** | "Validation", "Lifecycle", "Pipeline" as the feature name | Names a process, not a capability with a result. Fails capability gate. | Fold into the capability that owns the process (Validation → actions, Lifecycle → engine). |
| **Cross-cutting H2 floating between sections** | `## Match setup`, `## Reading state`, `## Disposal split` as ad-hoc H2s between Composition and Invariants | No defined home; bypasses Synthesis. Fails section-order rule. | Move into `## Synthesis` as H3. |
| **Single-model feature with no synthesis** | Feature root paraphrases the one model's Concept; no boundaries, no cross-feature story | Fails synthesis-pulls-its-own-weight. | Fold the model under a parent feature, or surface the real synthesis if it exists. |
| **Supporting element that grew up** | A `### EffectInstance` in feature root with full method list, multi-paragraph design discussion | The thing has its own design depth — it's a model. | Graduate to `models/NN-effect-instance.md`; replace with a reference + one-line role. |

## Conventions (once classified as a feature)

- Lives at `Product/features/<feature>/<feature>.md`. Filename matches folder name.
- Frontmatter: `type: feature`, `status:`, `references:`, `tags:`.
- Title: `# <feature> — feature`. Feature name matches folder name (lowercase, no spaces).
- Models live under `Product/features/<feature>/models/` with numbered prefix (`01-`, `02-`, ...).
- Body sections (per the skeleton): Concept, Architecture, Composition, Synthesis, Supporting elements, Invariants, Boundaries, Dependencies, Open. Strict order; cannot be reordered. Concept, Architecture, Composition, and Boundaries are always required. Synthesis, Supporting elements, Invariants, Dependencies, and Open may be empty with stub `*n/a — <reason>.*` but cannot be omitted.
- Cross-references use `Product/`-rooted absolute paths (see `_workflow/PRODUCT-BUILDER.md` §Cross-references).

## Section shape

Each section has a fixed format. **Concept** is the prose section; **Synthesis** sub-headings may stretch to one or two short paragraphs when a cross-model story needs it. Everywhere else uses tables, labels, snippets, and one-liners.

| Section | Format |
|---|---|
| **Concept** | Prose. 1 sentence to a few short paragraphs. Frame the capability's purpose, what it produces, why it exists. Define feature-internal terms inline on first use (bold the term). Cross-feature terms link to their canonical home. |
| **Architecture** | Visual / structural rendering. Flow diagram, graphflow, ASCII layout, sequence, state machine — sized to feature shape. Stub `*n/a — descriptive only.*` permitted only when no form fits. |
| **Composition** | Table only. `Model` / `Role`. One row per model under the feature; each row links to the model file. |
| **Synthesis** | H3 per cross-model topic. Heading = the topic; one or two short paragraphs + tight snippets that make a point. **Never re-defines a model's signature** — links to the model. Stub `*n/a — single-model feature, no cross-model story.*` permitted. |
| **Supporting elements** | H3 per homeless type. Heading = type name; one-line role; tight snippet; one-line **graduation trigger** (`Graduate to model when: <condition>.`). Stub `*n/a — all types have model homes.*` permitted. |
| **Invariants** | Bold-label one-liners (`**Label** → cross-model rule.`). Cross-model only — model-local invariants live in their models. |
| **Boundaries** | `**Owns:** <list>` then `**Does not own:**` with redirect-bullets pointing at the feature that does. |
| **Dependencies** | `**Upstream:** <feature> — <consumed>`. `**Downstream:** <feature> — <consumes this>`. Mirrors front-matter `references:`. |
| **Open** | Bullets, one line each. Reference open-item id (`oq-` / `bl-` / etc.) plus brief description. |

**Where design decisions go.** Cross-model choices that fit a one-liner: `## Invariants`. Choices that need rationale: [ADRs](Product/_docs/adr.md), linked from the relevant Invariant. The feature root never re-elaborates an ADR.

**Prose budget.** Concept is the prose section. Synthesis sub-sections can stretch when a cross-model story genuinely needs more than a paragraph. Everywhere else uses tables, labels, snippets, and one-liners — if Invariants, Composition, or Boundaries need more, the wrong content is in them: extract to Concept, fold into Synthesis, or promote to an ADR.

## Heuristics

- **Default = synthesis.** Body discusses across multiple types → feature root. Body discusses one type in depth → model. Body weighs options against each other → ADR.
- **Repetition test.** Re-stating a model's signature, definition, or invariants? Replace with a reference + one-line role.
- **Section-extras test.** Want to add an H2 not in the skeleton? It's a `## Synthesis` H3, not an H2.
- **Graduation trigger required.** Every `## Supporting elements` entry must carry a one-line graduation trigger. Pre-empts indefinite squatting.
- **Vocabulary inline, not in a table.** Feature-internal terms get a bold introduction in Concept on first use. Cross-feature terms link to their canonical home (owning feature root or `Product/ledgers/GLOSSARY.md`).
- **One model is fine.** A single-model feature is allowed if there's real synthesis to write (boundaries with neighbors, cross-feature invariants). If there isn't, fold the model up.

## Audit obligation

Rules apply at creation **and** retroactively. When a gate or rule changes, every existing feature is re-checked against the new wording. Promoting a rule from draft → accepted in this file triggers a mandatory audit pass over the existing features.

## Skeleton

Instantiate this for a new feature. Fill in placeholders; preserve section order. See `## Section shape` above for per-section formatting rules. The Worked example below shows a fully-fleshed-out feature.

    ---
    type: feature
    status: draft
    references: [<upstream features or models>]
    tags: ["#<feature>"]
    ---

    # <feature> — feature

    ## Concept

    Prose orientation. What capability is this? What does it produce? Why does it exist? Define feature-internal terms inline on first use (bold the term). 1 sentence to a few short paragraphs.

    ## Architecture

        <Flow diagram / ASCII layout / sequence / state machine. Sized to feature shape.>

    ## Composition

    | Model | Role |
    |---|---|
    | [`models/01-<slug>.md`](features/<feature>/models/01-<slug>.md) | <one-line role> |
    | [`models/02-<slug>.md`](features/<feature>/models/02-<slug>.md) | <one-line role> |

    ## Synthesis

    ### <Cross-model topic>

    One or two short paragraphs synthesising how the models interact for this topic. Tight snippet to make a point — does not re-define a model's signature. Link out to the model for the type.

        <Optional snippet.>

    ## Supporting elements

    ### <SupportingTypeName>

    One-line role.

        <Tight snippet.>

    *Graduate to model when: <independent consumers / own supports / design depth condition>.*

    ## Invariants

    - **<Cross-model invariant name>** → <one-line rule + operational consequence>.
    - **<Cross-model invariant name>** → [ADR-NNNN](Product/decisions/NNNN-<slug>.md). <One-line summary>.

    ## Boundaries

    **Owns:** <feature-owned types and seams>.

    **Does not own:**
    - <thing> → <feature that does>.

    ## Dependencies

    *Mirrors the front-matter `references:` array in human-readable form.*

    - **Upstream:** `features/<X>` — <what this feature consumes>.
    - **Downstream:** `features/<Y>` — <what consumes this>.

    ## Open

    - `<id>` — <brief description>.

    *If a section has nothing to write, replace its body with a stub:* `*n/a — <reason>.*`

## Worked example

A trimmed instance of the skeleton above. Frozen — does not track [`features/cards/cards.md`](features/cards/cards.md) if that drifts.

    ---
    type: feature
    status: settled
    references: [features/entities/models/02-instance.md, features/zones/models/01-zone-model.md]
    tags: ["#cards"]
    ---

    # cards — feature

    ## Concept

    The framework's card primitive. A **`Card`** is a thin handle extending the entity bridge (`InstanceWithSlices<CardDefinition>`) with a typed `Ref<Card> SelfRef`, fronted by a privacy-rotating **`CardRef`** for client-visible state. All mutable card data — variables, modifiers, current public ref, owner, visibility — lives in Store slices. Per-card-type variants (Fireball, Goblin, ...) are different `CardDefinition` instances, not different C# classes; the framework provides one `Card` class.

    Two state slices are owned here: **`CardMapState`** (the privacy-rotation indirection that maps `CardRef ↔ Ref<Card>`) and **`CardVisibilityState`** (the per-card override on top of the zone default). **`OwnershipState`** is defined here but generic across catalog handles, shared with `zones/` and `players/`.

    ## Architecture

        Card handle (class)
               │
        InstanceWithSlices<CardDefinition>   ← entity bridge (entities/)
               │
         ┌─────────────┼──────────────┬──────────────────┐
         │             │              │                  │
        CardMapState  store.Catalog  OwnershipState   CardVisibilityState
        (CardRef ↔     (Ref<Card> →   (Reference →    (Ref<Card> →
         Ref<Card>)    live Card)     IActor)          override)
               │
        Mutations (CompositeAction)
        Create / Move / SetCardVisibility / Destroy

    ## Composition

    | Model | Role |
    |---|---|
    | [`models/01-card-model.md`](features/cards/models/01-card-model.md) | Card handle, four state slices, four mutations, resolution surfaces, read API |

    ## Synthesis

    *n/a — single-model feature; the cross-model story lives at the entities / zones boundary, called out in Boundaries and Dependencies.*

    ## Supporting elements

    *n/a — all card-related types live in `models/01-card-model.md`.*

    ## Invariants

    - **Two-ID model** → `Ref<Card>` is stable for the card's lifetime; `CardRef` rotates on every move and visibility transition. The two are not interchangeable.
    - **Server-internal vs client-visible split** → server-internal references (triggers, ongoing effects, attachments, history identifying the card itself) use `Card` or `Ref<Card>`. Client-visible references and projections (zones, indices, history naming a current location) use `CardRef`.
    - **All mutable card state in slices** → variables, modifiers, current public ref, owner, visibility — all in Store slices. The handle holds no mutable data beyond `SelfRef`, protected `Definition`, and `Storage`.
    - **`CardRef` rotation is the privacy mechanism** → observers see refs change but never receive data they aren't allowed to see; the same rotation runs locally and across networks.
    - **One live `Card` per `Ref<Card>`** → held in `store.Catalog`; equality is `SelfRef`-based.

    ## Boundaries

    **Owns:** the `Card` class and `CardRef` type; `CardMapState`, `CardVisibilityState`; the four card-lifecycle `CompositeAction` mutations; the `card.GetRef`, `card.GetOwner`, `card.IsVisibleTo` accessors.

    **Does not own:**
    - Entity bridge and variable/modifier storage → `entities/`.
    - Zone containment, insertion, capacity → `zones/`.
    - Visibility *policy* (the per-zone default) → `zones/` (`ZoneDefinition`).
    - `OwnershipState` slice mechanism — defined here, generic across catalog-registered handles → shared with `zones/`, `players/`.
    - Action pipeline, command handlers → `actions/`.
    - Trigger fold semantics around card mutations → `triggers/`.
    - Effect orchestration of card lifecycle → `effects/`.
    - `store.Catalog` registry mechanism → `engine/`.

    ## Dependencies

    *Mirrors the front-matter `references:` array in human-readable form.*

    - **Upstream:** `entities/` (the entity bridge `InstanceWithSlices<TDef>`, storage adapter `IEntityVariableStorage`, runtime spawn surface), `zones/` (containment + visibility policy + insertion API; cards are placed into zones).
    - **Downstream:** `actions/` (consumes mutations as `CompositeAction`), `effects/` (subscribes to card lifecycle), `engine/` (state slice infrastructure; `store.Catalog`), `presentation/` (consumes card-keyed state via `CardRef`).

    ## Open

    See `ledgers/OPEN-ITEMS.md`:

    - `oq-move-validation-execution-drift` — concurrency between move-validate and move-execute when state drifts between the two.
    - `oq-entity-destruction` — `Cleanup` hook for destroyed cards (per-card slice cleanup on `DestroyCardMutation`).
    - `bl-card-resolution-helper-shape` — final form for the `CardRef → Card` two-hop resolution helper.
