---
type: ledger
status: settled
last-updated: 2026-05-01
tags: [#general, #questions]
---



# Open Questions

These are unresolved design questions worth answering before implementation or during small focused spikes.

## Core Model

Status: initial answers captured in [Core Model Decisions](features/entities/models/01-definition-instance-model.md), then incorporated into [Core Glossary](ledgers/GLOSSARY.md) and [Entity, Definition, And Instance Model](features/entities/models/01-definition-instance-model.md).

- ~~Should the pure simulation core depend directly on Scaffold packages, or should Scaffold be used only in Unity-facing adapters?~~ → No enforced boundary yet; allow Scaffold wherever it fits. (doc 08)
- ~~Should card/entity values be stored primarily as flexible variables/tags, typed state records, or a hybrid?~~ → Start with key-value variables. (doc 08)
- ~~What is the canonical source of truth: entity instances, Store slices, or a composed model?~~ → **Store is canonical; Entities is the API.** Per-entity variables are `EntityVariableState` slices; `StateEntity<TDef>` is a handle implementing `IReadOnlyEntity` / `IMutableEntity`. Implemented by `com.scaffold.entities.states` bridge. (doc 08, doc 10 §"Store And Entities Relationship")
- ~~How should instances behave when a card changes zones?~~ → Same instance preserved by default; game-specific rules may explicitly replace it. (doc 08)
- ~~Do all entities have definitions, or can runtime-only entities exist without definitions?~~ → All entities must have a base definition, including generated tokens. (doc 08)
- ~~How do attachments, equipment, stacked cards, tokens, and transformed cards relate to a base card instance?~~ → Instance = definition + modifiers. Attachments are separate instances connected through relationships. (doc 08)

Still open:
- id: oq-entity-destruction | **Entity destruction / Cleanup lifecycle** — what "destroyed" means for the Store, when the framework Cleanup hook fires relative to the destruction command envelope, and what happens to in-flight dispatches referencing the destroyed entity. Load-bearing for ongoing effects that self-destruct. ([effects/03-ongoing-effects §5](features/effects/models/03-ongoing-effects.md))
- ~~id: oq-entity-modifier-api | **Entity modifier mutation API (Scaffold bridge)** — concrete payload shapes for `AddModifierPayload`, `SetBaseValuePayload`, `AddEntityVariablePayload` through the `com.scaffold.entities.states` bridge. Needed before any game reads or writes layered modifier values. ([entities/01-definition-instance-model §Values And Modifiers](features/entities/models/01-definition-instance-model.md))~~ → resolved 2026-05-01 (session-20260501-1710-entity-modifier-api: shapes are settled by the bridge package; Card Framework conventions Q1–Q5 documented in `entities/01` §"Mutation API conventions" — surface = `IMutableEntity` default + raw payloads fallback, `AddEntityVariablePayload` reserved for instance-only variables, `SetBaseValuePayload` for out-of-band base changes, `ModifierSource.Source` = attachment `InstanceId`, `IModifierIdGenerator` for deterministic ids on `IHandlerContext`).

## Zones

Status: high-level direction captured in [Zone Model](features/zones/models/01-zone-model.md). Internal sub-questions settled in session-20260501-1602: zone shape ([ADR-0001](features/zones/decisions/0001-zone-shape.md)), visibility model ([ADR-0002](features/zones/decisions/0002-visibility-model.md)), insertion API ([ADR-0003](features/zones/decisions/0003-insertion-api.md)). Spatial rule attachment deferred to backlog (`bl-spatial-rule-attachment`).

- ~~What zone traits are mandatory in core?~~ → Zones are responsibility-based models (contents, arrangement, capacity, insertion/removal behavior, visibility, change bookkeeping), not a fixed trait checklist. (docs 11, 12)
- ~~How should spatial board positions relate to zone membership?~~ → Spatial placement is outside the core zone model; it is view-defined or game-specific. (docs 11, 12)
- ~~Are stacks zones, relationships, or both?~~ → Stacks are zones with custom organization. (docs 11, 12)
- ~~How should hidden, partially visible, and viewer-specific state be modeled?~~ → Two visibility channels: membership visibility and detail visibility. Actor/player/ownership semantics are not yet designed; per-actor visibility query API is deferred. (docs 11, 12)
- ~~Should zone movement be a generic event, a task, or a specialized rules primitive?~~ → Zone movement is a normal simulation-driven game action flowing through the standard pipeline. (docs 11, 12)
- ~~How do we model temporary views like reward offers, shops, card packs, draft picks, and generated choices?~~ → Deferred to the action/decision model; may or may not be zones depending on game. (docs 11, 12)

Still open:
- ~~id: oq-zone-internal-opens | **Zone model internal open questions** — nine questions within `zones/01-zone-model.md` "Open Questions Left For Later" have no follow-up model: zone-as-entity vs lightweight record, membership storage model, actor/owner/controller semantics, visibility query API, forced insertion API, zone organisation query API, history of hidden movement, generated reward zones, and spatial rule attachment.~~ → Resolved session-20260501-1602: 8 of 9 settled; spatial rule attachment deferred (see `bl-spatial-rule-attachment` in BACKLOG). See [ADR-0001 zone shape](features/zones/decisions/0001-zone-shape.md), [ADR-0002 visibility model](features/zones/decisions/0002-visibility-model.md), [ADR-0003 insertion API](features/zones/decisions/0003-insertion-api.md), and updated [Zone Model](features/zones/models/01-zone-model.md). Cross-cutting: [ADR-0005 Objects over IDs](decisions/0005-objects-over-ids.md). Actor/Player/Owner terminology refined in glossary; Controller and Viewer dropped as non-framework concepts.
- id: oq-attachment-storage | **Attachment storage location** — whether attachments (equipment, auras, buffs) live in a sub-zone on the host, a global attachment registry, or a dedicated zone. Load-bearing for the ongoing-effects attachment model. ([effects/03-ongoing-effects §6](features/effects/models/03-ongoing-effects.md))

## Rules And Scheduling

Status: action-flow foundation captured in [Action Flow Decisions](features/actions/models/01-command-pipeline.md) and [Action, Request, Command, And History Model](features/actions/models/01-command-pipeline.md). Naming discussion is framed in [Action Terminology Discussion](ledgers/GLOSSARY.md), with a proposed vocabulary in [Action Definition Proposal](features/actions/models/01-command-pipeline.md). Engine loop and evaluator pattern captured in [Engine — Orchestrator And Evaluation Loop](features/engine/models/01-evaluation-loop.md). Trigger and reaction system captured in [Trigger And Reaction System](features/triggers/models/01-trigger-fold.md). `GameRules` contract in [GameRules Shape](features/engine/models/02-gamerules-contract.md). Snapshot shape in [Action Availability](features/actions/models/02-availability-snapshot.md). Snapshot production in [Snapshot Production Pipeline](features/engine/models/03-snapshot-pipeline.md).

Resolved:
- ~~**Is the first scheduler a simple deterministic queue, or should a stack/priority model exist from the start?**~~ → FIFO queue in `GameEngine`. Stack/priority is a named variation via the same `GameRules` interface — no separate abstraction needed from the start. (doc 20)
- ~~**Can queue, stack, turn-phase, and real-time tick scheduling share one abstraction?**~~ → Yes. All entry points (player input, timers, triggers) call `GameEngine.Dispatch`. Phase transitions and priority grants are commands dispatched from `OnQueueDrained`. One loop, one evaluator. (doc 20)
- ~~**When exactly are state-based checks run?**~~ → After every command execution, via `GameRules.Evaluate`. (doc 20)
- ~~**Available actions ownership**~~ → Snapshot lives in `GameState.AvailableActions: AvailableActionsSnapshot`. Rule writes through the injected `IAvailableActions` service (default in `OnQueueDrained`); effects can override mid-execution. Engine never auto-publishes. (docs 25, 27, 29)
- ~~**Snapshot element type**~~ → Typed `ActionTemplate` subtypes per command type, with shared abstract base (`CommandType`, `Source`). Templates carry per-action fields (slots, modes, parameters as needed). (doc 29 §3)
- ~~**Eager vs lazy binding validation**~~ → On-demand by the rule, not automatic. Per-template eligibility filters during snapshot build; per-binding `Validate` runs at dispatch (authoritative) and on-demand if the rule opts in. (doc 29 §2)
- ~~**`GameRules` state pattern (stateless vs stateful)**~~ → Services injected at construction (Scaffold `IStateScope`, `IAvailableActions`, `IIntakeQueue`, `IHandlerRegistry`); methods take only ephemeral data. State read through `State.Get<TState>()`. The "zero game-affecting state" contract is preserved — services are configuration, not gameplay memory. (doc 29 §5; [04-effect-flow §4.1](features/effects/models/04-effect-flow.md))
- ~~**`IntakeContext.Pending` type**~~ → `IntakeContext` collapsed; `Pending` is the injected `IIntakeQueue` service with the same operations as the original `LinkedList<GameCommand>` (`AddFirst`/`AddLast`/`Remove`/iterate). (doc 29 §5)
- ~~**Command chain correlation**~~ → `OriginCommandId` is stamped on the first command by the gateway and threaded by the engine onto all evaluator follow-ups. The first envelope also carries `OriginIntent` for UI correlation. Follow-up envelopes carry `OriginCommandId` only. (docs 18, 20)

Still open:
- ~~**Replacement/prevention effects**~~ → Resolved. Three before-fold outcomes: modification, cancellation, substitution. Replacement-of-replacements handled naturally by the fold (condition evaluates against the current command after prior transforms). `ITriggerPipeline` owns collection/ordering/fold execution; `GameRules.FilterTriggers` owns availability policy. Extension point for MTG-style controller-choice: custom `ITriggerPipeline` that dispatches a `Decision`. Prevention is modification with a zero floor; stateful prevention deferred to ongoing effects. (doc 23)
- ~~**Simultaneous events**~~ → Resolved. True simultaneity (all effects measured against a state snapshot, deltas merged and applied atomically) is explicitly off the table — it requires a scratch-pad model and introduces irreconcilable conflicts (two effects wanting to move the same card to different zones, etc.). "Simultaneous" in this framework always means sequential in a defined order. Multiple triggers matching the same command are multiple `TriggerMatch` entries in the fold list, run one by one. Ordering is game-specific via `GameRules.FilterTriggers` return order or a custom `ITriggerPipeline`.
- ~~**Continuous effects and derived values**~~ → Resolved. Ongoing effects are attachment entities: created, associated with a host, subscribe to triggers (including turn signals), manage their own state via variables, and self-destruct when done. Cleanup on destruction automatically unsubscribes. No new pipeline concept. Future: attachments as modifier carriers for stat reads (deferred to stats redesign). (doc 24)
- ~~**Async effects for player input**~~ → Resolved. Layer hierarchy settled: Effects are async orchestrators; `GameAction`s are synchronous and atomic. Decisions (`RequestTargetAction`, etc.) are normal `GameAction`s whose handlers wait for input and return the choice in `CommandResult`. `scope.Dispatch` executes immediately and inline — the effect awaits each result before continuing. Everything returns `Task`; most resolve synchronously. The engine pauses for the duration of an effect; whether that is acceptable is game-specific. (doc 22)
- id: oq-move-validation-execution-drift | **Move validation-vs-execution state drift** — when a `MoveCardMutation` (or any mutation with state preconditions) is validated and queued, then another mutation moves the card before the original executes, the original's source precondition fails at execute-time. Current behavior (per Q14 lock, session-20260501-1602): throw at execution. Other strategies to evaluate when this becomes painful: re-resolve source from current state, silent no-op, or deeper interlocking via the trigger-fold / replacement-effects pipeline. Affects all mutations with state preconditions, not just MoveCard. ([cards/01-card-model.md](features/cards/models/01-card-model.md), [zones/01-zone-model.md](features/zones/models/01-zone-model.md))
- id: oq-stash-release | **Stash release mechanics** — MTG-style scheduling uses `Stash` to hold commands until priority resolves, but the release mechanism (`Release(key)`, ordering rules on release, LIFO ordering) is not designed. (doc 21 §8)
- id: oq-queue-ordering | **Queue ordering shape** — a simple FIFO queue is shown; whether priority queues, interrupt injection, or stack semantics for MTG-like resolution order are built into the engine queue or handled entirely via `GameRules` is unresolved. (doc 20 §11)
- id: oq-gamerules-composition | **`GameRules` composition** — a single `GameRules` implementation is shown; in practice it may be composed of smaller rule modules. `IAvailableActions` service surface (`Set`/`Append`/`Clear`) supports module composition mechanically. The composition contract (provider/policy ordering, enable/disable) is deliberately undesigned for now. (doc 29 §9)
- ~~**`GameState` slice catalog**~~ → Resolved S19. Two-tier model: framework-owned slices (`AvailableActionsState`, `EngineState`, `HistoryState`) auto-seeded at `Build()`; game-authored slices registered via `WithInitialSlices` + `WithMutators`. Uniform read surface: `IStateScope.Get<TState>()` for all tiers. ([features/engine/models/04-gamestate-slices.md](features/engine/models/04-gamestate-slices.md))
- id: oq-intake-queue | **`IIntakeQueue` shape** — same operations as the original `LinkedList`-typed `Pending`. `CurrentlyExecuting` lives on the queue or on a sibling slice. Pin at engine implementation. (doc 29 §9)
- ~~**Effect-side service symmetry**~~ → Resolved. `IEffectScope` carries Scaffold `IStateScope` (read-only) + `Dispatch` + `Cancel`. `IHandlerContext` carries concrete Scaffold `Store` (read + execute mutators), `IAvailableActions`, `IDecisionResolver`. Decision id ≡ `CommandId` of the opening action; routing stays uniform (intent → mapper → command → dispatch); `DecisionResponseCommand` + handler invokes `Resolver.TryResolve`. Mutation only through Scaffold `Mutator<TState, TPayload>` invoked by handlers via `Store.Execute(payload)`. (`features/effects/models/04-effect-flow.md`)
- id: oq-fsm-game-state | **FSM-shaped game state** — `FreeInput`/`DecisionPrompt` were deleted (S15 Q6); snapshot carries templates only. Type-only routing in `ICommandAwaiter` is the current mechanism for recognizing in-progress decision commands. The FSM-shaped state model (typed `PickATargetState`, `MainPhaseState`, etc., each owning their own snapshot-generation surface) is the eventual successor. Revisit when the first async-decision-heavy game arrives. (doc 29 §9; [04-effect-flow §9](features/effects/models/04-effect-flow.md))
- id: oq-trigger-ordering | **Trigger ordering within fold** — what determines sequence when multiple triggers are registered for the same type: FIFO by registration, priority weight, game-configurable, or something else. (doc 21 §8)
- ~~id: oq-handler-registry | **`IHandlerRegistry` interface** — typed lookup API (how to resolve the correct `Validate` overload for a given command type), registration surface, and how `GameEngineBuilder` populates it. Placeholder one-liner in engine/02 §6 explicitly deferred. ([engine/02 §6](features/engine/models/02-gamerules-contract.md))~~ → Resolved session-20260501-1132 — `IHandlerRegistry` is pipeline-internal (resolver-only); not injected into `GameRules`. Interface shape defined in engine/02 §6.
- id: oq-engine-builder | **`GameEngineBuilder` construction protocol** — the builder type used in `pile-counter §9.8` (`WithHandlers`, `WithMutators`, `WithRule`, `WithIntentMapper`, `Build`) and the `services` bag object are undefined anywhere. Load-bearing for any game author. ([pile-counter §9.8](support/samples/pile-counter.md))
- ~~id: oq-engine-startup | **Engine startup / first `OnQueueDrained` trigger** — on a freshly-built idle engine the queue is empty; the drain loop condition (`!running && pending.Count > 0`) would never fire. How the first drain (and the initial available-actions write) is triggered is undocumented. ([engine/01 §3](features/engine/models/01-evaluation-loop.md))~~ → Resolved session-20260501-1132 — engine startup and first drain trigger settled in engine wiring design pass.
- ~~id: oq-command-pipeline | **`ICommandPipeline` interface** — `GameEngine` holds a `readonly ICommandPipeline pipeline` and calls `pipeline.Execute(action)`, but the interface is never defined. Its relationship to the trigger fold (effects/02) and handler dispatch is described in prose only. ([engine/01 §2 §9](features/engine/models/01-evaluation-loop.md))~~ → Resolved session-20260501-1132 — `ICommandPipeline` interface settled; owns validate/execute pipeline; defined in engine/01.
- ~~id: oq-trigger-type-unification | **`ITriggerSystem` vs `ITriggerPipeline` — unification decision** — two competing shapes: `triggers/01` uses `ITriggerSystem` with `OnBefore<T>/OnAfter<T>/RemoveAll(owner)` (subscription registration surface); `effects/02` uses `ITriggerPipeline` with `RunBefore/RunAfter` + `TriggerMatch` (execution surface). Are these the same type, two sides of one type, or two separate objects? ([triggers/01 §2 §5](features/triggers/models/01-trigger-fold.md), [effects/02 §3 §5](features/effects/models/02-replacement-prevention.md))~~ → Resolved session-20260501-1132 — split: `ITriggerSystem` (subscription) and `ITriggerPipeline` (execution); one `TriggerSystem` class implements both. Shape conflict tracked in ad-20260501-025.
- id: oq-intent-template-lookup | **Intent → template lookup** — `IIntentMapper.Map(ActionTemplate, PlayerChoice)` requires a pre-resolved template, but how the gateway/store picks the matching `ActionTemplate` from the snapshot for a raw `PlayerIntent` is undocumented. The "(or builds one from the intent)" clause in presentation/01 §10 step 6 is unexplained. ([presentation/01 §8 §10](features/presentation/models/01-presentation-loop.md))
- ~~id: oq-gamerules-service-wiring | **`GameRules` service wiring in builder** — what the `services` bag type is, which framework layer constructs the four injected services (`IStateScope`, `IAvailableActions`, `IIntakeQueue`, `IHandlerRegistry`), and in what order they are materialized. ([engine/02 §2](features/engine/models/02-gamerules-contract.md), [pile-counter §9.8](support/samples/pile-counter.md))~~ → Resolved session-20260501-1132 — 3 services injected (`IStateScope`, `IAvailableActions`, `IIntakeQueue`); `IHandlerRegistry` is pipeline-internal; wiring documented in engine/02 §2.
- id: oq-trigger-subscription-entry | **Trigger subscription entry point for regular cards** — `effects/03` §6 shows a handler calling `AbilityDefinition.Register` for attachments. For regular cards entering play (zone entry), no system or lifecycle hook is shown that calls `Register`. Tied to game lifecycle and zone-entry events. ([triggers/01 §5](features/triggers/models/01-trigger-fold.md))
- id: oq-history-envelope | **`HistoryEnvelope` typed definition** — currently pseudocode with named fields (`envelopeId`, `originCommandId`, `actor`, `entries[]`); no typed C# record exists. `EnvelopeCause` is glossary-named but undefined. Load-bearing for replay, multiplayer transport, and the presentation history loop. ([actions/01 §History Envelope](features/actions/models/01-command-pipeline.md))

## Authoring

- Should card effects be authored as data, code, visual graphs, or a mix?
- What is the smallest useful effect language?
- How do we make simple cards easy while keeping complex cards possible?
- How do definitions reference visuals, sounds, animations, and gameplay logic without coupling everything together?
- What validation should run in editor?
- How are definition IDs versioned for save/load and multiplayer?

## Presentation

- What is the common card presentation contract across UI, 2D, and 3D?
- Should animation consume raw history, presentation events, or a transformed animation timeline?
- How much visual state is allowed to exist outside canonical simulation state?
- How do we handle animation lag while the simulation has already advanced?
- How does input ask for legal targets or drop destinations?
- What should a failed action look like when the gesture was visually allowed but rules reject it?

## Multiplayer

- What is the minimum command envelope needed for future network transport?
- What is the minimum history envelope needed for replay and transport?
- When should visibility filtering happen: in core, adapter, or both?
- Should clients maintain a full filtered Store or only presentation state?
- How are reconnect snapshots generated and applied?
- How are definition mismatches detected?
- Do we need rollback/prediction for any target game type, or can authoritative reactive play be the default?

## Scaffold Integration

Resolved:
- ~~Is `com.scaffold.entities` suitable for runtime simulation, or better for authoring only?~~ → Used at runtime via the state-backed bridge (`com.scaffold.entities.states`). Plain `EntityInstance` remains valid for non-state-backed cases. (doc 08, doc 10)
- ~~Should `com.scaffold.states` become the canonical match state store?~~ → Yes, including per-entity variables via the bridge. (doc 08, doc 10)
- ~~How do entity variable modifiers and States mutators interact without duplicating state?~~ → Entity mutations *are* Store payloads. `StateEntity` is a thin handle; modifier stacks live in `EntityVariableState`. No duplication. (doc 10)

Still open:
- Should MVVM models be generated from simulation state/history, or should they subscribe directly to Store slices?
- When do we introduce AutoPacker: after local simulation, after multiplayer design, or only when payloads are stable?

## Presets

- What belongs in the common core vs a TCG preset?
- What belongs in a deckbuilder preset vs a generic zone/effect system?
- What belongs in a spatial simulation preset vs generic drag/stack interaction?
- Can presets be composed, or are they mutually exclusive modes?
- How do games override preset behavior without forking the framework?

## Testing And Tooling

- What is the first testable scenario?
- What must be deterministic in tests?
- Should tests assert final state, full history, or both?
- How do we snapshot a scenario in a human-readable way?
- What debug tools are required before complex effect work begins?
- How do we trace "why was this action legal/illegal?"

## Explicit Non-Goals

- **True simultaneous resolution** — measuring all competing effects against a state snapshot and applying merged deltas atomically is off the table. It requires a scratch-pad model and introduces irreconcilable conflicts. "Simultaneous" always means sequential in a game-defined order.

## Research Biases To Watch

- Do not overfit to Hearthstone just because the old docs explored SabberStone deeply.
- Do not overfit to Magic's full rules complexity before simpler schedulers prove themselves.
- Do not treat Stacklands as "just presentation"; spatial placement and stacking are rules.
- Do not make multiplayer early implementation work, but do keep command/history/instance-reference boundaries multiplayer-friendly.
- Do not let data-driven authoring prevent custom code for genuinely complex cards.

## Likely First Decisions

The first practical decisions should probably be:

1. ~~Pure core dependency boundary.~~ → No enforced boundary yet. (doc 08)
2. ~~Canonical state model.~~ → Resolved: Store-canonical, Entities-as-API via the `com.scaffold.entities.states` bridge. (doc 08, doc 10)
3. ~~Entity/definition/instance and instance ID rules.~~ → Resolved. (docs 08, 09, 10)
4. ~~Zone taxonomy.~~ → Resolved. (docs 11, 12)
5. ~~Command/task/event/history terminology.~~ → Resolved. (docs 09, 14, 15, 16, 17, 18)
6. ~~First scheduler model.~~ → Resolved: FIFO queue + evaluator pattern. (doc 20)
7. **First game preset target for a vertical slice.** → Still open.
