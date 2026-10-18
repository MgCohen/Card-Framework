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
- Body sections (per the skeleton): Concept, Definition, Supporting elements, Use cases, Invariants, Open follow-ups. Strict order; cannot be reordered. Concept, Definition, and Invariants are always required. Supporting elements, Use cases, and Open follow-ups may be empty with stub `*n/a — <reason>.*` but cannot be omitted.
- Cross-references use `Product/`-rooted absolute paths (see `_workflow/PRODUCT-BUILDER.md` §Cross-references).

## Section shape

Each section has a fixed format. **Concept** is the prose section; Supporting-element descriptions may stretch to a short paragraph when one line can't carry the role. Everywhere else uses labels, snippets, and one-liners.

| Section | Format |
|---|---|
| **Concept** | Prose. 1 sentence to a few short paragraphs. Orient the reader on what the type is and the role it plays. |
| **Definition** | Code only — the central type's signature(s). No prose around the block. Comments only for behavior the signature can't express. |
| **Supporting elements** | H3 per element. Heading = type name; role description (one line preferred, short paragraph when needed); code block. Tightly-coupled enums may sit under their consumer. |
| **Use cases** | H3 per scenario. Heading = scenario name; one-line description; tight snippet. Only non-derivable compositions — skip single-call demos that are obvious from `Definition`. |
| **Invariants** | Bold-label one-liners (`**Label** → rule.`). Pack choice + operational rule together when they share a concept (e.g. `**Identity** → two refs. Stable for X; ephemeral rotates for Y.`). |
| **Open follow-ups** | Bullets, one line each. Reference the open-item id (`oq-` / `bl-` / etc.) plus brief description. |

**Where design decisions go.** Choices that fit a one-liner: `## Invariants`. Choices that need rationale: [ADRs](Product/_docs/adr.md), linked from the relevant Invariant. The model doc never re-elaborates an ADR.

**Prose budget.** Concept is the prose section. Supporting-element descriptions can stretch to a short paragraph when one line can't carry the role. Everywhere else uses one-liners — if Use cases, Invariants, or Open follow-ups need more, the wrong content is in them: extract to Concept, fold into an Invariant, or promote to an ADR.

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

Instantiate this for a new model. Fill in placeholders; preserve section order. See `## Section shape` above for per-section formatting rules. The Worked example below shows a fully-fleshed-out model.

    ---
    type: model
    status: draft
    references: [<upstream features or models, or the literal `root`>]
    tags: ["#<feature>", "#model"]
    ---

    # <Element Name> — model

    ## Concept

    Prose orientation. What is this type in the system? What role does it play? Why does it exist? 1 sentence to a few short paragraphs.

    ## Definition

        <Central type signature(s). Code only — no prose around the block.>

    ## Supporting elements

    ### <SupportingTypeName>

    One-line role description.

        <Code block.>

    ### <NextSupportingTypeName>

    Short paragraph — for supporting types whose role doesn't fit one line.

        <Code block.>

    ## Use cases

    ### <Scenario name>

    One-line description.

        <Tight snippet.>

    ## Invariants

    - **<Label>** → <rule + operational consequence packed when they share a concept>.
    - **<Label>** → <rule>.
    - **<Label>** → [ADR-NNNN](Product/decisions/NNNN-<slug>.md). <One-line summary>.

    ## Open follow-ups

    - `<id>` — <brief description>.

    *If a section has nothing to write, replace its body with a stub:* `*n/a — <reason>.*`

## Worked example

A trimmed instance of the skeleton above. Frozen — does not track [`features/cards/archive/01-card-model.md`](features/cards/archive/01-card-model.md) if that drifts.

    ---
    type: model
    status: settled
    references: [features/entities/models/01-definition-instance-model.md, features/zones/archive/01-zone-model.md]
    tags: ["#cards", "#model"]
    ---

    # Card — model

    ## Concept

    `Card` is the per-instance runtime handle for one card. Game code holds a `Card` to address a specific card across moves, visibility transitions, and snapshot/restore — the handle is stable identity, while the underlying client-facing `CardRef` rotates for privacy.

    The class is intentionally thin: only `SelfRef` plus protected refs to definition and storage. All mutable state (variables, modifiers, owner, visibility) lives in Store slices keyed by `SelfRef`. The framework provides one `Card` class — per-card variants live as `CardDefinition` instances, not as `Card` subclasses, so the runtime stays data-driven.

    ## Definition

        public class Card : InstanceWithSlices<CardDefinition>
        {
            public Ref<Card> SelfRef { get; init; }

            public CardRef GetRef(IStateScope state)   => state.Get<CardMapState>().RefOf(SelfRef);
            public IActor? GetOwner(IStateScope state) => state.Get<OwnershipState>().OwnerOf(SelfRef);

            public override bool Equals(object? obj) => obj is Card other && SelfRef.Equals(other.SelfRef);
            public override int  GetHashCode()       => SelfRef.GetHashCode();
        }

    ## Supporting elements

    ### CardRef

    Client-visible ref. Rotates on every move and visibility transition.

        public readonly record struct CardRef(Guid Value)
        {
            public static CardRef NewRef() => new(Guid.NewGuid());
        }

    ### CardMapState

    Privacy-rotation indirection.

        public sealed record CardMapState : IState
        {
            public ImmutableDictionary<CardRef, Ref<Card>> RefToInstance { get; init; }
            public ImmutableDictionary<Ref<Card>, CardRef> InstanceToRef { get; init; }

            public Ref<Card> InstanceOf(CardRef @ref) => RefToInstance[@ref];
            public CardRef   RefOf(Ref<Card> selfRef) => InstanceToRef[selfRef];
        }

    ### OwnershipState

    Owner of any catalog handle (shared by Cards + Zones).

        public sealed record OwnershipState : IState
        {
            public ImmutableDictionary<Reference, Actor> Owners { get; init; }
            public Actor? OwnerOf(Reference selfRef);
        }

    ### CardVisibilityState

    Per-card override on top of zone default.

        public enum CardVisibility { Hidden, VisibleToOwner, VisibleToAll }

        public sealed record CardVisibilityState : IState
        {
            public ImmutableDictionary<Ref<Card>, CardVisibility> Overrides { get; init; }
            public CardVisibility? OverrideFor(Ref<Card> r);
        }

    ### Mutations

    All `: CompositeAction` (pass through trigger fold). Rotators carry `NewRef` in payload for replay determinism.

        public sealed record CreateCardMutation(CardDefinition Definition, Ref<Card> NewSelfRef, CardRef NewRef, Zone? IntoZone = null, InsertPosition? Position = null) : CompositeAction;
        public sealed record MoveCardMutation(Card Card, CardRef NewRef, Zone Source, Zone Destination, InsertPosition Position, bool BypassCapacity = false) : CompositeAction;
        public sealed record SetCardVisibilityMutation(Card Card, CardRef NewRef, CardVisibility Visibility) : CompositeAction;
        public sealed record DestroyCardMutation(Card Card) : CompositeAction;

    ## Use cases

    ### Create + place

    Spawn a card into a zone, then move it.

        var card = store.CreateCard(fireballDefinition, p1Hand);
        store.Execute(MoveCardMutation.AtTop(card, p1Hand, battlefield));

    ### Resolve a CardRef

    Two-hop lookup from a client-facing ref to the live `Card` handle.

        var card = store.Catalog.Resolve(state.Get<CardMapState>().InstanceOf(someRef));

    ## Invariants

    - **Canonical home** → slice (snapshots; `store.Catalog` rebuilds on `LoadSnapshot`) + catalog (in-process). Slice holds typed refs only; never live `Card` handles.
    - **Identity** → two refs. `Ref<Card>` stable for history & subscriptions; `CardRef` rotates on hide/move for privacy. Rotation writes only `CardMapState`. Client-visible state uses `CardRef`; server-internal uses `Card` or `Ref<Card>`.
    - **Equality** → by `SelfRef`.
    - **Type relation** → `Card` extends the bridge handle, reusing variable / modifier / definition machinery.
    - **Generic parameter** → non-generic. Per-card variants live in `CardDefinition` subclasses; one runtime class keeps the framework data-driven.
    - **Visibility** → [ADR-0008](Product/decisions/0008-zone-visibility-model.md). Zone default + per-card override + server-side `CardRef` rotation.

    ## Open follow-ups

    - `oq-move-validation-execution-drift` — concurrency between move-validate and move-execute.
    - `oq-entity-destruction` — `Cleanup` hook for destroyed cards.
    - `bl-card-resolution-helper-shape` — final form of the `CardRef → Card` two-hop helper.
