---
type: glossary
status: settled
last-updated: 2026-05-01
tags: [#general, #glossary]
---

# Core Glossary

This glossary establishes working language for the Card Framework. Terms may be refined later, but new research docs should use these meanings unless they explicitly propose a change.

## Foundational Terms


| Term            | Short Definition                                                              | Examples / Notes                                                                                                         |
| --------------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Game            | The authored ruleset and content domain for a playable card-based experience. | Magic-like TCG, Slay-like deckbuilder, Stacklands-like spatial sim, card-based RPG or board game.                        |
| Game Definition | Authored configuration that declares a game's starting assumptions.           | Card types, zone definitions, turn/time model, rule modules, starting state templates, presentation defaults, presets.   |
| Session         | One running simulation instance of a game definition.                         | Match, combat encounter, full roguelike run, board simulation, replayed scenario, test case.                             |
| Actor           | Framework base type — anything that can own instances. | Human player, AI, enemy, automated worker, environment, neutral market, automatic rules system, "the world." Actors can also be entities when they need variables. |
| Player          | Kind/subtype of Actor representing a seated participant. Game-specific. | Use when a game has player seats, ownership, priority, score, deck ownership, or client association. Other Actor kinds — `World`, `Environment`, `Market` — are also valid. |
| Owner           | The Actor that owns an instance. Named concept, not a framework type. | Implementation: an `Actor`-typed field on entities/zones that have ownership. Not framework-mandated on every instance — games that don't need ownership ignore it. |


## Entity Model


| Term                | Short Definition                                                                                 | Examples / Notes                                                                                                                                                          |
| ------------------- | ------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Entity              | Any runtime object the simulation may need to reference.                                         | Card instance, zone instance, player/actor, relic, equipment, resource, villager, enemy, stack entry, token, card pack. Every entity should have a base definition.       |
| Definition          | Authored base data shared by instances.                                                          | Fireball card definition, Iron Sword equipment definition, Hand zone definition, Villager definition, Berry Bush resource definition. Definitions should have stable IDs. |
| Instance            | Runtime entity created from a definition.                                                        | `Instance = definition + instance ID + current relationships + modifiers`. Zone movement preserves the same instance by default.                                          |
| Identifier\<T\>     | Base record providing both value and reference utility for all ID types in the framework.        | Used for `InstanceId`, `DefinitionId`, `ZoneId`, `ActorId`, chain IDs, etc. Replaces ad-hoc per-concept ID types.                                                        |
| Instance ID         | Lookup/reference value associated with an instance. Typed as `Identifier<Instance>`.            | It is a value inside or associated with the instance, not a separate model concept.                                                                                       |
| Definition ID       | Stable authored identifier of a definition. Typed as `Identifier<Definition>`.                  | Used for save/load, replay, debugging, networking, validation, and cross-reference from instances to definitions.                                                         |
| Variable            | Key-value entry on a definition or instance.                                                     | `VariableKey -> VariableValue`. Keys like `cost`, `attack`, `zone`, or `recipe_progress` are game/preset/schema-defined, not hardcoded by the base framework.             |
| Base Value          | Value supplied by the base definition before runtime changes.                                    | Example: a card definition has base `cost = 3`.                                                                                                                           |
| Modifier            | Runtime adjustment applied to an instance or variable.                                           | Cost reduced this turn, attack from equipment, Retain gained in a zone, work speed from a tool.                                                                           |
| Effective Value     | Final calculated value after base values, modifiers, attachments, external effects, and context. | Example: base attack 2 + sword 3 + aura 1 = effective attack 6.                                                                                                           |
| Relationship        | Directed or structured link between entities.                                                    | Owner, controller, in-zone, attached-to, equipped-by, stacked-on, targeting, source-of-effect, parent-child.                                                              |
| Attachment          | Instance connected to another instance through a relationship.                                   | Equipment attached to a character, aura attached to a card, token stacked on a resource. Attachments are usually their own instances.                                     |
| Composition Handler | System that calculates meaning from multiple related instances.                                  | Equipment stats, aura bonuses, stack recipe progress. May be game-specific or preset-specific.                                                                            |


## Zones And Location


| Term            | Short Definition                                                                       | Examples / Notes                                                                                                                                                                         |
| --------------- | -------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Zone            | Thin domain object grouping cards. Carries `(Id, Definition, Owner)`; state lives in slices. Shares the `Identifier<Instance>` namespace with cards. | Deck, hand, discard, battlefield, rules stack, physical stack, shop row, production slot. **Not** card packs (entities with generator effects), reward offers, or drafts (decisions). Zones do not have stats; stat-bearing things are not zones. |
| ZoneDefinition  | Authored config for a zone type — capacity, ordering rules, insertion behavior, default visibility policy. | Hand definition, deck definition, MTG-stack definition. Different zone types vary by definition data, not by class hierarchy. |
| ZoneContentsState | Generic framework-owned slice keyed by zone, holding ordered membership as `IReadOnlyList<Card>`. | Flat list. Slot keys, group keys, spatial coordinates, and other per-entry structure are not pre-defined hooks — a game adds those fields to the contents shape on its own design pass when forced by real requirements. |
| Zone Membership | Relationship or zone-owned state saying an instance is currently in a zone. Canonically lives in `ZoneContentsState`. | Does not define the instance. It can trigger rules and may have ordering/grouping inside the zone. |
| Zone Movement   | State change that moves an entity between zones or changes its position within a zone. | May trigger effects, update visibility, change modifiers, or be blocked. Does not create a new instance by default. |
| Position        | Address, ordering, or game-specific placement inside a zone.                           | Top of deck, third card in hand, battlefield slot, stack depth. World-space placement is only core simulation data if a game-specific spatial system makes it so.                         |
| Stack           | Ambiguous term that must be qualified by context.                                      | Could mean Magic-like rules stack, physical card stack, or scheduler data structure.                                                                                                     |
| Membership Visibility | Whether an actor can know which instance IDs are in a zone and how they are arranged. | Example: a player sees their hand contents; an opponent may only see a count. |
| Detail Visibility | Whether an actor can resolve definition and variable data for a known instance ID. | Example: a face-down card may be visible as an instance but hide its definition and variables. |
| Card Visibility | Per-card runtime flag enum `{ Hidden, VisibleToOwner, VisibleToAll }` on a state slice. Absent entry = use zone policy default. | Override on top of zone definition's policy. `VisibleToOwner` requires Owner; collapses to `Hidden` if no Owner. Read via `card.IsVisibleTo(state, actor)`. |
| Ephemeral Instance ID | Per-actor transport alias for a canonical `InstanceId`. Generated by the server when a card transitions to "newly hidden" relative to that actor; preserves privacy across reveal-then-hide cycles. | Lives only at the network boundary. Simulation-side `InstanceId` stays stable. Triggers, effects, slices reference canonical IDs; ID translation happens at send-time. |
| Zone Capacity | Default organizational limit for a zone. | Capacity can be overridden or ignored by game/action rules when an effect forces a move via `InsertCardPayload { BypassCapacity = true }`. |
| InsertCardPayload | Single payload for zone insertion with optional override fields: `{ Zone, Card, Position, BypassCapacity }`. | `Position` is a sealed record hierarchy: `DefaultPosition`, `AtTop`, `AtBottom`, `AtIndex(int)`. Static factories provide call-site sugar. Composes with `RelocateInstanceMutation`. |
| RelocateInstanceMutation | Composite `StateMutation` encapsulating a remove-then-insert as one logical zone move. Carries the same `InsertOptions` as `InsertCardPayload` and forwards them to the insert side. | Preferred over two raw mutations for common move operations. Intermediate state is not exposed. |
| CardLocationIndex | Derived framework slice keyed by card → zone, kept atomically in sync by mutators that write `ZoneContentsState`. | Backs reverse queries: "what zone is this card in?" is O(1). |


## Rules And Flow


| Term | Short Definition | Examples / Notes |
| ---- | ---------------- | ---------------- |
| Intent | Informal term for what a player or system wants to do. Not a runtime type. | Use `PlayerIntent` for the structured form. |
| PlayerIntent | Structured player-originated intent submitted to the engine. | Drag card, select card, pass turn, select option. Replaces `Request`. |
| IntentResult | Structured outcome returned to the caller after a `PlayerIntent` is submitted. | Accepted (with chain ID) or rejected (with reason code). |
| IntentMapper | Gateway-side component that maps a `PlayerIntent` to an available action or command override. | Provisional name. The full intent → available action → validation relationship is a deferred design topic. |
| AvailableAction | Optional engine-exposed affordance that UI may highlight or present. | This card can be played, this target is available, this ability can be activated. |
| AvailableActionsSnapshot | Read-only artifact published by `GameRules` at queue drain listing currently legal actions per actor. | Optional system — games that do not use it leave it as a no-op. |
| Command | High-level game command or low-level state command, depending on lane. | Game command: deal damage; state command: set variable. Naming still needs refinement. |
| GameCommand | Abstract base for all pipeline-dispatchable types: `GameAction`, `CompositeAction`, and `GameSignal`. | Not directly instantiated. Subtypes carry different execution semantics. |
| StateMutation | Low-level already-approved state mutation. | Insert into zone, set variable, add modifier, create instance. Replaces `StateCommand`. |
| StateChange | A `HistoryEntry` subtype recording a concrete state diff produced by a `StateMutation`. | `VariableChanged<T>`, `ZoneContentRemoved`, `ZoneContentInserted`. Automatically produced by the state layer. |
| GameSignal | A `GameCommand` subtype (and `HistoryEntry`) recording a game-meaningful named fact that cannot be inferred from raw diffs alone. Not executable — trigger surface only. | `CardPlayStartedSignal`, `ActorHealedSignal`. Emitted explicitly by handlers via `scope.Emit(...)`. Replaces `SemanticEvent`. |
| GameAction | Executable `GameCommand` subtype — queued through the engine, runs through the full pipeline, modifiable in before-trigger fold. Atomic and synchronous: one handler, one envelope, returns a `CommandResult`. Never dispatches other `GameAction`s internally. | `PlayCardCommand`, `HealActorCommand`. Replaces the old `GameCommand` executable type. |
| CompositeAction | Reusable group of `StateMutation`s used inside a `GameAction` handler. Runs inline within the parent handler's scope — not queued. Passes through its own trigger fold. Does not orchestrate other `GameAction`s. | `MoveCardComposite` (remove + insert as one logical unit). Distinguished from Effect: a `CompositeAction` groups state changes; an Effect sequences commands. |
| Effect | Async orchestration layer that sequences multiple `GameAction`s and threads their `CommandResult`s. The only layer that can dispatch a series of commands. Defined on or referenced from a card/ability definition. | Play fireball: request target → deal damage → move to discard. See [features/effects/models/01-async-dispatch.md](features/effects/models/01-async-dispatch.md). |
| Decision | Explicit moment where an actor may or must choose from legal options. Modelled as a `GameAction` (`RequestTargetAction`, `RequestChoiceAction`) that opens the decision and returns the choice in its `CommandResult`. | Choose target, choose reward, pass priority, select event branch. |
| DecisionOption | One legal choice inside a decision. | May include ID, display info, target requirements, payload shape, or validation context. |
| Validation | Process of checking whether a command or option is legal in current state. | Enough energy, valid target, actor has priority, destination is legal, recipe exists, card is not exhausted. |
| OnBefore\<T\> | Before-trigger fold that runs before a `GameCommand` handler. `GameAction` payloads can be modified, cancelled, or replaced. | Validate, cancel, replace, redirect, or modify a game action before state changes. Replaces `CommandPreHook`. See [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md). |
| OnAfter\<T\> | After-trigger fold that runs after a `GameCommand` handler. Reactions dispatched into the dispatch channel. | Draw a card when another card is healed. Trigger lifecycle effects. |
| CommandHandler | Resolver for a game command. | Reads state, applies state mutations, returns result. |
| CommandResult | Structured outcome of a game command. | Approved, rejected, actual destination, damage split, drawn instance ID. |
| GameRules | Game-specific rules brain plugged into the engine. Called after each command and when the queue empties; returns follow-up commands based on current `GameState`. | Replaces `IEngineEvaluator`. The engine is the framework loop; `GameRules` is the game-specific plugin. |
| GameState | Read-only view of simulation state passed to `GameRules`. | Zone contents, actor variables, phase info. Exact contract is a named open design task. Replaces `IGameState`. |
| ResolutionScope | Handler-facing context object for one command execution. Provides `Apply`, `Emit`, `Execute`, and `DispatchChild`. | Handlers never touch global state directly; all mutations go through the scope. |
| EnvelopeCause | Metadata attached to a `HistoryEnvelope` describing why it was produced. | Actor, source instance, chain ID, and (first envelope only) `OriginIntent` — the originating `PlayerIntent`. |
| Trigger | Rule that reacts to an event, state change, or command result. Subscribed per-instance via `AbilityDefinition.Register`; runs through the `OnBefore<T>` or `OnAfter<T>` fold on every matching `GameCommand`. See [features/triggers/models/01-trigger-fold.md](features/triggers/models/01-trigger-fold.md). | Whenever a card is drawn, when this enters play, at end of turn, after damage. |
| Scheduler | System that decides when tasks, effects, triggers, decisions, and checks execute. | Queue drain, stack and priority, turn/phase machine, real-time tick. |
| History | Ordered record of meaningful simulation events. | Supports presentation, animation, replay, debugging, tests, and networking later. |
| HistoryEnvelope | Grouped history entries plus cause metadata for one action-flow boundary. | "Player A played Fireball; these mutations happened." Child commands fold into the envelope; evaluator follow-ups each get their own envelope sharing the chain ID. |
| HistoryEntry | One ordered fact emitted by the simulation. | Subtyped as `StateChange` (auto-produced) or `GameSignal` (explicitly emitted). |
| Event | Fact emitted by the simulation. | Entity created, variable changed, entity moved zones, damage dealt, decision opened, task started/ended. May feed history, triggers, or both. |


## Boundaries


| Term         | Short Definition                                                          | Examples / Notes                                                                                                     |
| ------------ | ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Simulation   | Rules and state layer that decides what happens.                          | Owns entities, runtime definitions, commands, validation, effects, triggers, state changes, and history.             |
| Authoring    | Layer that creates and validates content definitions.                     | May use ScriptableObjects, Scaffold Entities, schemas, custom editors, or other tooling.                             |
| Presentation | Layer that renders state and history.                                     | Owns card views, zone views, animation, layout, feedback, UI affordances. Should not be source of gameplay legality. |
| Interaction  | Layer that turns input gestures into commands or decision responses.      | Drag, drop, hover, click, target line, cancel.                                                                       |
| Networking   | Optional transport layer for commands, decisions, history, and snapshots. | Should attach to stable simulation boundaries later.                                                                 |


## Conventions

- **Objects over IDs.** Framework APIs prefer domain objects (`Zone`, `StateEntity`, `Player`, `Actor`) over raw `Identifier<T>` values. IDs are reserved for boundaries — serialization, persistence, network transport, and type-mismatch fallbacks. Within the simulation, code holds and passes the object; the object is also used as the key for slice access (`state.Get<X>(zone)`). See [ADR-0005](decisions/0005-objects-over-ids.md).

## Nuance Notes

- Variables are key-value based. The base framework should not hardcode variable names; games, presets, schemas, or authoring packages define them.
- "Identity" is not a separate core concept. Use `Instance`; use `Identifier<T>` for boundary forms (serialization, transport).
- Zone membership does not define an instance. Zone movement preserves the same instance by default.
- Zones are thin domain objects; their state lives in slices. They do not have stats — stat-bearing things are not zones.
- Card packs are not zones — they are entities with generator effects. Reward offers and drafts are decisions, not zones.
- Zones do not own broad gameplay legality; game/action rules decide whether movement is allowed.
- Spatial placement is not a base zone concern unless a game-specific spatial system makes it simulation data.
- UI creates `PlayerIntent`s and reacts to state/history; it should not mutate simulation state directly.
- History envelopes preserve why state changes happened, not only what changed.
- History is canonical on the server; per-actor views are produced at the transport boundary by ID translation + detail-visibility gating. Movement entries are always sent (fact-of-movement is public); card-detail entries are gated on visibility.
- `StateChange` entries are produced automatically by the state layer; `GameSignal` entries must be emitted explicitly by handlers via `scope.Emit(...)` for meanings the layer cannot infer.
- Every entity should have a base definition, including generated tokens or temporary system entities.
- Attachments and equipment are usually separate instances connected through relationships, not hidden fields on the host.
- `CommandReaction` (post-result pipeline hooks) is removed. Follow-up commands return from `GameRules.Evaluate`.
- `Controller` and `Viewer` are not framework concepts. Controller is a generic system term; visibility queries take an `Actor` parameter directly with no typed `Viewer` wrapper.