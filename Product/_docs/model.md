---
type: doc-rules
target: model
status: accepted
references: [_workflow/PRODUCT-BUILDER.md]
tags: [#workflow, #rules, #model]
---

# Model — rulebook + skeleton

The unit of canonical design. **One model = one C# type explained in depth.**

If you are creating, auditing, or reviewing a model, the rules below apply at every step. The skeleton at the bottom is what you instantiate.

## Non-negotiable gates

A doc fails to qualify as a model if any of these gates fails. Each gate is a yes/no test.

1. **Point-at-the-type.** Name the C# class / interface / record / struct / payload / service. If you cannot, **NOT A MODEL** — fold the content into the type that owns it, or into feature root for cross-element synthesis.
2. **Cardinality (the "and" test).** The element description must name **exactly one** type. If it uses "X and Y" as peers (`GameRulesDefinition and GameRulesRuntime`, `Entity, Definition, And Instance`, title with `&` / `+` / `,`), **NOT ONE MODEL** — split into one model per type.
3. **Title-filename equality.** The H1 title's element name (drop `I` prefix on interfaces, hyphenate camelcase) must equal the filename slug. `# Game Engine — model` requires `01-game-engine.md`. Mismatch → mistitled or misclassified — re-run gates 1 and 2.
4. **Load-bearing.** The type has its own behavior (methods, logic, runtime semantics) **OR** substantial design space (authoring contract, evolution rules, integration story, multi-consumer needs, validation/serialization concerns). Pure data with no design space (bare enum, fieldless signal, simple shape-only payload) → **NOT A MODEL** — place as support inside the model that emits or consumes it.
5. **Canonical home.** The model is the type's only canonical home. If the doc defines rules / behaviors / extensions of a type defined elsewhere, or describes a recipe composing existing surfaces, → **NOT A MODEL** — content belongs at the type's home, in feature root, or as examples.

## Decision flow

Apply in order. The first failure determines the verdict.

1. **Identify the candidate central element.** Exactly one. If the description uses "X and Y" as peers, stop — that is two models, not one.
2. **Point-at-the-type test.** Can you name the C# type? Yes → continue. No → **NOT A MODEL.** Fold the content.
3. **Title-filename equality test.** Does title's element name == filename slug (with conventions)? Yes → continue. No → mistitled or misclassified — go back to step 1.
4. **Load-bearing check.** Behavior present OR design space present → continue. Pure data with no design space → **NOT A MODEL.** Place as support.
5. **Body shape.** Body focused on the one element + its supports → continue. Body is synthesis-across-peers + ADR design pass → split into feature root + ADR.
6. **Independence.** Always reached via parent + small + no design depth → **SUPPORT.** Substantial design depth OR independent consumers OR own supports → **MODEL.**

## Anti-patterns (recognise and reject)

| Smell | What it looks like | Why it fails | Where the content goes |
|---|---|---|---|
| **Process / pipeline / loop / surface / contract title** | "Evaluation Loop", "Snapshot Pipeline", "Runtime Surface", "GameRules Contract" | Title names a process or topic, not a type. Fails point-at-the-type. | Rename to the type, or dissolve if it's a piece of another type's behavior. |
| **Multi-type bundle** | "GameRulesDefinition and GameRulesRuntime", "Entity, Definition, And Instance", title with `&` / `+` / `,` | Two or more peers presented as one. Fails the "and" test. | Split — one model per type. |
| **Pattern / convention as model** | "Framework Slice Tier", "Cleanup conventions", "Modifier source convention" | Body describes rules/conventions over types defined elsewhere. Fails canonical-home rule. | Feature root (cross-cutting conventions) or the owning type's model. |
| **Bare enum or single value type** | A model whose central element is just an `enum` or fieldless signal | Fails load-bearing — no design weight, no behavior. | Support inside the model that emits or consumes it. |
| **Title-filename mismatch** | Title says `Game Engine`, filename says `evaluation-loop` | Naming is inconsistent — one of the two is wrong. | Fix the wrong side. If the title is the type and filename is the topic, rename the file. |
| **Recipe composing existing surfaces** | A model that doesn't define a new type but tells you how to use existing ones in combination | Fails canonical-home rule — content belongs at the level that owns the composition. | Feature root, or examples inside the relevant type's model. |
| **Piece-of-a-thing** | "before-fold" (step inside `ITriggerPipeline.RunBefore`), "decision-routing" (behavior of `ICommandAwaiter`), "attachment-entity" (usage pattern of `EntityDefinition`) | Candidate is a verb / gerund / adjective-noun describing what happens during a larger element's execution. Pieces are not things. | Fold into the thing that owns the piece. |

## Conventions (once classified as a model)

- Lives at `Product/features/<feature>/models/<NN>-<slug>.md`. Numbered prefix within the feature.
- Frontmatter: `type: model`, `status:`, `references:`, `tags:`.
- Title: `# <Element Name> — model`. Element name is the C# type name.
- Filename slug derives from the type — drop the `I` prefix on interfaces (`ICommandHandler` → `command-handler.md`), keep records/classes verbatim (`HistoryEnvelope` → `history-envelope.md`).
- Topic-shaped or process-shaped titles ("decision-routing", "before-fold", "async-dispatch", "lifecycle", "pipeline-mechanics") are misclassifications. If the title is not a type name, the file is either misclassified (not a model) or correctly a model but mistitled. Re-run gate 1.
- Body sections (per the skeleton): What it is, Contracts, Invariants, Decisions, Examples, Open follow-ups, Options considered, Rationale, Consequences. Strict order. Sections may be empty with stub `*n/a — <reason>.*` but cannot be reordered or omitted.
- Cross-references use `Product/`-rooted absolute paths (see `_workflow/PRODUCT-BUILDER.md` §Cross-references).

## Heuristics

- **Default = support.** Promotion to model needs a reason: substantial depth, independent consumption, supports forming around the element.
- **Piece-of-a-thing trap.** Verb / gerund / adjective-noun candidate names → it's a piece. Fold into the thing.
- **Bundle-of-things trap.** "and" / "+" / "/" / comma between concepts → it's a bundle. Split.
- **Title-content mismatch — fix the title first.** If body has a clear central element but the title is process/concept-shaped, rename the title. Reclassification is the harder fallback.
- **Test for design space:** would the body be substantively more than `definition + field list`? If no → support, not model.
- **Test for body shape:** does the body discuss one element in depth, or synthesize across many? One-in-depth → model. Synthesis-across-many → feature root. Decision-with-options → ADR.

## Audit obligation

Rules apply at creation **and** retroactively. When a gate or rule changes, every existing model is re-checked against the new wording. Promoting a rule from draft → accepted in this file triggers a mandatory audit pass over existing models of that type.

## Skeleton

Instantiate this for a new model. Fill in placeholders; preserve section order. Strict-order sections with empty content carry a stub line (`*n/a — <reason>.*`).

    ---
    type: model
    status: draft
    references: [<upstream features or models, or the literal `root`>]
    tags: []
    ---

    # <Element Name> — model

    ## What it is

    High-level orientation in 30 seconds. SHOULD include a canonical artifact:

    | Model shape | Artifact form |
    |---|---|
    | Class / interface / contract | Code snippet (signatures + types) |
    | Algorithm | Pseudocode |
    | State machine / lifecycle | State diagram |
    | Data structure / schema | Shape sketch |
    | Pipeline / pass / phase | Arrow chain |

    Stub `*n/a — see Contracts.*` permitted if no form fits.

    ## Contracts

    What this model defines: types, interfaces, method signatures, semantics, forbidden behaviors. Implementation is welcome when the framework owns it; surface-only signatures are appropriate when implementation is game-supplied.

    ## Invariants

    Model-local rules. Cross-model rules live in the feature root's `## Invariants`, referenced by name.

    ## Decisions

    **Reserved for elevated ADRs.** Project-wide ADRs (per `_docs/adr.md` two-feature rule) that shape this model. Feature-internal decisions live below the divider, not here. Stub `*n/a — no elevated ADR.*` if none.

    Linked: `Product/decisions/NNNN-<title>.md`

    ## Examples

    Consumers of the contract — user extensions and call-site usage. Stub `*n/a — see Contracts.*` if no consumer interaction fits.

    ## Open follow-ups

    Unresolved threads. Promote durable items to `Product/ledgers/OPEN-ITEMS.md`.

    ---

    *Below: provenance — what was considered, why we chose this, what it constrains. Feature-internal decisions live here; project-wide cross-cutting decisions get an ADR.*

    ## Options considered

    - **A** — rejected because ...
    - **B** — rejected because ...
    - **C** — chosen.

    *Multi-decision models: accumulate `### Q-N — <slug>` sub-sections in narrative order, each with its own options / lean / rationale.*

    ## Rationale

    Why the chosen approach. Skip if obvious.

    ## Consequences

    What this enables; what it constrains. Skip if obvious.
