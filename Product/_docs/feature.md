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

A trimmed instance of the skeleton above. Frozen — does not track [`features/triggers/triggers.md`](features/triggers/triggers.md) if that drifts. Chosen as the worked example because it has multiple models *and* real cross-model story to fit into `## Synthesis`; single-model features (e.g. `cards`) stub Synthesis / Supporting elements with `*n/a — ...*` instead.

    ---
    type: feature
    status: settled
    references: [features/actions/models/01-command-pipeline.md, features/engine/models/03-game-rules-runtime.md, features/effects/models/02-effect-scope.md, decisions/0006-two-pipeline-architecture.md]
    tags: ["#triggers"]
    ---

    # triggers — feature

    ## Concept

    The framework's reactive fan-out. When a `GameCommand` flows through the action pipeline, **`OnBefore<T>`** triggers run first (they may modify, cancel, or replace the command), then the handler executes (`GameAction` only), then **`OnAfter<T>`** triggers run (they may dispatch reactions). Triggers are typed by the command shape they observe.

    A **`Trigger<T>`** is the reaction class (a `Validate(cmd, scope)` condition + an `Execute(cmd, scope)` body); game authors register them through **`ITriggerSystem`** (the subscription surface called from `AbilityDefinition.Register`). The action pipeline drives them through **`ITriggerPipeline`** (the execution surface — `RunBefore` / `RunAfter`). One `TriggerSystem` class implements both interfaces; subscription and execution are split surfaces so abilities never see fold internals. The fold runs registered triggers in sequence — the framework, not the trigger author, handles aggregation, ordering, and replacement uniformly.

    ## Architecture

                  GameCommand enters pipeline
                             │
                             ▼
          ┌────────────────────────────────────────┐
          │ Registry query → GameRulesRuntime.     │
          │ FilterTriggers → available triggers    │
          └────────────────────────────────────────┘
                             │
                             ▼
                OnBefore<T> fold        ← may modify / cancel / replace
                             │
                             ▼
                  handler.Execute       ← GameAction only
                             │
                             ▼
                OnAfter<T> fold         ← may dispatch reactions
                             │
                             ▼
                        HistoryEnvelope

    ## Composition

    | Model | Role |
    |---|---|
    | [`models/01-trigger.md`](features/triggers/models/01-trigger.md) | `Trigger<T>` (the reaction class); `ITriggerSystem` subscription surface; `AbilityDefinition.Register/Unregister` per-instance lifecycle; closure-capture for per-instance data |
    | [`models/02-trigger-pipeline.md`](features/triggers/models/02-trigger-pipeline.md) | `ITriggerPipeline` (the fold orchestrator); `TriggerFoldResult`, `TriggerMatch`, `TriggerPhase` supports; three before-fold outcomes; replacement-of-replacements; priority + ordering; `FifoPipeline` default |

    ## Synthesis

    ### Reactions and the dispatch channel

    Reaction commands dispatched from inside a trigger go through the same channel as everything else — `engine.Dispatch`. There is no separate reaction queue.

        // "Whenever another card is healed, draw a card"
        triggers.OnAfter<HealActorAction>(
            new Trigger<HealActorAction>(
                validate: (cmd, scope) => cmd.Target != self,
                execute: async (cmd, scope) =>
                {
                    await scope.Dispatch(new DrawCardAction { Actor = myActor });
                }),
            owner: self);

    The channel validates the dispatched action through `pipeline.Validate` and enqueues it on the engine's strict-FIFO queue. Per [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md) the runtime does **not** gate intake or route commands. Game-style differentiation lives at the *effect* level, not at command-queue routing — what differs across games is *when* triggered effects start, not how the queue runs:

    | Style | How triggered effects are scheduled |
    |---|---|
    | Slay the Spire | Reactions enter framework FIFO; with the queue empty at trigger-fire time, they run as the next command — "on the spot" semantics. |
    | Hearthstone | Same FIFO. Reactions queue normally and run after the current command's pipeline completes. |
    | MTG | The stack is a game-state zone, not the engine queue. Triggered abilities push themselves into the stack via state mutation; `runtime.Evaluate` watches the stack and dispatches `ResolveTopOfStack`-style follow-ups. |

    ### History when replaced

    When command A is replaced by B in the before-fold: A's envelope is closed and published with `CommandResult.Replaced` — appears in history as a command that was dispatched but did not execute. B is dispatched fresh via `engine.Dispatch`, gets its own full fold from scratch, and produces a sibling envelope. The causal link between them is preserved via `OriginCommandId` threading if A and B share the originating intent. See [`actions/04-history-envelope.md`](features/actions/models/04-history-envelope.md) for envelope shape.

    ### Emitting `GameSignal`s and `CompositeAction`s from a handler

    Handlers emit `GameSignal`s and execute `CompositeAction`s through the scope. Both pass through their own trigger folds before/after — the same `ITriggerPipeline` runs them, with no handler step between the folds for these types:

        scope.Emit(new ArmorBrokenSignal(target));          // GameSignal — before/after fire; no handler
        scope.Execute(new MoveCardComposite(source, zone)); // CompositeAction — before/after fire; executes inline

    A handler should never dispatch another `GameAction` directly — that's an `Effect`'s responsibility. `CompositeAction` and `GameSignal` use a lighter path: trigger folds only, no handler, no envelope of their own; their entries fold into the parent command's envelope.

    ### Pipeline integration

    The full `pipeline.Execute(action)` shape (envelope open → filter → before-fold → handler → after-fold → envelope close) is owned by [`actions/01-command-pipeline.md` §Pipeline mechanics](features/actions/models/01-command-pipeline.md). Trigger-side notes: the action pipeline holds `ITriggerPipeline`; card abilities hold `ITriggerSystem`; one class implements both. Before/after folds run only the filtered `available` list — the upstream filter is `GameRulesRuntime.FilterTriggers` ([`engine/03-game-rules-runtime.md`](features/engine/models/03-game-rules-runtime.md)). Replacement-of-replacement is fold-natural: each step's condition checks the *current* command, so substituting `DealDamageAction → HealAction` automatically routes through `HealAction`-watching triggers and skips `DealDamageAction`-watching ones.

    ## Supporting elements

    *n/a — all trigger types have model homes (`Trigger<T>`, `ITriggerSystem`, `AbilityDefinition` in `01-trigger.md`; `ITriggerPipeline`, `TriggerFoldResult`, `TriggerMatch`, `TriggerPhase` in `02-trigger-pipeline.md`).*

    ## Invariants

    - **Phase order is strict** → Before → Execute → After. No phases overlap, no re-entry within a single command.
    - **Replacement creates a fresh fold** → when a before-fold trigger calls `scope.Replace(next)`, the original command does not execute; `next` dispatches as a fresh command through the channel and runs through its own full trigger fold from scratch.
    - **After-phase `scope.Cancel` / `scope.Replace` are no-ops** → the command already executed.
    - **Handlers never dispatch `GameAction` directly** → sequencing multiple `GameAction`s is an `Effect`'s responsibility. Triggers may dispatch reactions; effects may sequence; handlers may not.
    - **Registry is filtered upstream by `GameRulesRuntime.FilterTriggers`** → the pipeline never queries the trigger registry itself.
    - **`ITriggerSystem` and `ITriggerPipeline` are split surfaces** → subscription (game code) vs. execution (pipeline internals). One `TriggerSystem` class implements both.
    - **Command-queue routing is framework-owned** → [ADR-0006](Product/decisions/0006-two-pipeline-architecture.md). The runtime does not customize the queue; effect-level orchestration is a separate forthcoming surface.

    ## Boundaries

    **Owns:** `Trigger<T>` base class; `ITriggerSystem` and `ITriggerPipeline` interfaces; `TriggerMatch`, `TriggerFoldResult`, `TriggerPhase` types; the trigger fold mechanic (sequential fold, cancel/replace via scope, fresh-fold-on-replace); priority + ordering policy; `AbilityDefinition.Register` / `Unregister` contract.

    **Does not own:**
    - The `GameCommand` / `GameAction` / `CompositeAction` / `GameSignal` type hierarchy → `actions/`.
    - Command pipeline (envelope, handler dispatch, history) → `actions/`.
    - Engine evaluation loop, framework FIFO routing, `GameRulesRuntime` → `engine/`.
    - Effect orchestration, async dispatch, `IEffectScope` → `effects/`.
    - Per-game richer trigger ordering (controller-choice, named tiers) → owners of custom `ITriggerPipeline` implementations.

    ## Dependencies

    *Mirrors the front-matter `references:` array in human-readable form.*

    - **Upstream:** `actions/` (pipeline that hosts the trigger fold; `HistoryEnvelope`; `CommandResult`; `GameCommand` hierarchy), `engine/` (evaluation loop, framework-FIFO routing per ADR-0006, `GameRulesRuntime.FilterTriggers` + `Evaluate`), `effects/` (`IEffectScope` — the shared execution surface).
    - **Downstream:** `effects/` (subscribes via the same `IEffectScope` surface triggers use; replacement-prevention layer in `effects/02` builds on the fold), game-side rules code (registers abilities via `AbilityDefinition`).

    ## Open

    - `oq-trigger-subscription-entry` — when `Register`/`Unregister` is called; relationship to zone transitions and game lifecycle.
    - `oq-trigger-ordering` — what determines sequence within the fold beyond the `Priority` int.
