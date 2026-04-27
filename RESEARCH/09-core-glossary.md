# Core Glossary

This glossary establishes working language for the Card Framework. Terms may be refined later, but new research docs should use these meanings unless they explicitly propose a change.

## Foundational Terms


| Term            | Short Definition                                                              | Examples / Notes                                                                                                         |
| --------------- | ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| Game            | The authored ruleset and content domain for a playable card-based experience. | Magic-like TCG, Slay-like deckbuilder, Stacklands-like spatial sim, card-based RPG or board game.                        |
| Game Definition | Authored configuration that declares a game's starting assumptions.           | Card types, zone definitions, turn/time model, rule modules, starting state templates, presentation defaults, presets.   |
| Session         | One running simulation instance of a game definition.                         | Match, combat encounter, full roguelike run, board simulation, replayed scenario, test case.                             |
| Actor           | Entity or system capable of owning, controlling, viewing, or deciding things.  | Human, AI, enemy, automated worker, environment, neutral market, automatic rules system. Actors can also be entities when they need variables. |
| Player          | Game-specific actor representing a player seat or participant.                | Use when a game has player seats, ownership, priority, score, deck ownership, or client association.                     |


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
| Zone            | Simulation model that contains or groups instances and organizes those contents.         | Deck, hand, discard, battlefield, rules stack, physical stack, shop row, card pack, production slot. Zones can have definitions and instances.                                            |
| Zone Membership | Relationship or zone-owned state saying an instance is currently in a zone.             | Does not define the instance. It can trigger rules and may have ordering/grouping inside the zone.                                                                                       |
| Zone Movement   | State change that moves an entity between zones or changes its position within a zone. | May trigger effects, update visibility, change modifiers, or be blocked. Does not create a new instance by default.                                                                      |
| Position        | Address, ordering, or game-specific placement inside a zone.                           | Top of deck, third card in hand, battlefield slot, stack depth. World-space placement is only core simulation data if a game-specific spatial system makes it so.                         |
| Stack           | Ambiguous term that must be qualified by context.                                      | Could mean Magic-like rules stack, physical card stack, or scheduler data structure.                                                                                                     |
| Membership Visibility | Whether an actor can know which instance IDs are in a zone and how they are arranged. | Example: a player sees their hand contents; an opponent may only see a count. |
| Detail Visibility | Whether an actor can resolve definition and variable data for a known instance ID. | Example: a face-down card may be visible as an instance but hide its definition and variables. |
| Zone Capacity | Default organizational limit for a zone. | Capacity can be overridden or ignored by game/action rules when an effect forces a move. |
| RelocateInstanceMutation | Composite `StateMutation` encapsulating a remove-then-insert as one logical zone move. | Preferred over two raw mutations for common move operations. Intermediate state is not exposed. |


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
| GameCommand | High-level command that needs rules logic and returns a result. | Play card, draw card, deal damage, move instance after approval. |
| StateMutation | Low-level already-approved state mutation. | Insert into zone, set variable, add modifier, create instance. Replaces `StateCommand`. |
| StateChange | A `HistoryEntry` subtype recording a concrete state diff produced by a `StateMutation`. | `VariableChanged<T>`, `ZoneContentRemoved`, `ZoneContentInserted`. Automatically produced by the state layer. |
| SemanticEvent | A `HistoryEntry` subtype recording a game-meaningful named fact that cannot be inferred from raw diffs alone. | `CardPlayStartedEvent`, `ActorHealedEvent`. Emitted explicitly by handlers via `scope.Semantic(...)`. |
| Decision | Explicit moment where an actor may or must choose from legal options. | Choose card, choose target, choose reward, pass priority, select event branch. |
| DecisionOption | One legal choice inside a decision. | May include ID, display info, target requirements, payload shape, or validation context. |
| Validation | Process of checking whether a command or option is legal in current state. | Enough energy, valid target, actor has priority, destination is legal, recipe exists, card is not exhausted. |
| Task | Deterministic work item produced by commands, effects, triggers, or rules. | Deal damage, draw card, move zone, create token, apply modifier, start timer. |
| Effect | Rules behavior caused by a card, ability, relic, zone, system, or task. | May create tasks, modify events, apply modifiers, request decisions, or emit history. |
| CommandPreHook | Pipeline hook before a command handler. Provisional — subject to redesign when the trigger/reaction system is explored. | Validate, cancel, replace, redirect, or modify a game command before state changes. |
| CommandHandler | Resolver for a game command. | Reads state, applies state mutations, returns result. |
| CommandResult | Structured outcome of a game command. | Approved, rejected, actual destination, damage split, drawn instance ID. |
| GameRules | Game-specific rules brain plugged into the engine. Called after each command and when the queue empties; returns follow-up commands based on current `GameState`. | Replaces `IEngineEvaluator`. The engine is the framework loop; `GameRules` is the game-specific plugin. |
| GameState | Read-only view of simulation state passed to `GameRules`. | Zone contents, actor variables, phase info. Exact contract is a named open design task. Replaces `IGameState`. |
| ResolutionScope | Handler-facing context object for one command execution. Provides `Apply`, `Semantic`, and `DispatchChild`. | Handlers never touch global state directly; all mutations go through the scope. |
| EnvelopeCause | Metadata attached to a `HistoryEnvelope` describing why it was produced. | Actor, source instance, chain ID, and (first envelope only) the originating `PlayerIntent`. |
| Trigger | Rule that reacts to an event, state change, or command result. Runtime shape is undesigned — part of the deferred trigger/reaction exploration. | Whenever a card is drawn, when this enters play, at end of turn, after damage. |
| Scheduler | System that decides when tasks, effects, triggers, decisions, and checks execute. | Queue drain, stack and priority, turn/phase machine, real-time tick. |
| History | Ordered record of meaningful simulation events. | Supports presentation, animation, replay, debugging, tests, and networking later. |
| HistoryEnvelope | Grouped history entries plus cause metadata for one action-flow boundary. | "Player A played Fireball; these mutations happened." Child commands fold into the envelope; evaluator follow-ups each get their own envelope sharing the chain ID. |
| HistoryEntry | One ordered fact emitted by the simulation. | Subtyped as `StateChange` (auto-produced) or `SemanticEvent` (explicitly emitted). |
| Event | Fact emitted by the simulation. | Entity created, variable changed, entity moved zones, damage dealt, decision opened, task started/ended. May feed history, triggers, or both. |


## Boundaries


| Term         | Short Definition                                                          | Examples / Notes                                                                                                     |
| ------------ | ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Simulation   | Rules and state layer that decides what happens.                          | Owns entities, runtime definitions, commands, validation, effects, triggers, state changes, and history.             |
| Authoring    | Layer that creates and validates content definitions.                     | May use ScriptableObjects, Scaffold Entities, schemas, custom editors, or other tooling.                             |
| Presentation | Layer that renders state and history.                                     | Owns card views, zone views, animation, layout, feedback, UI affordances. Should not be source of gameplay legality. |
| Interaction  | Layer that turns input gestures into commands or decision responses.      | Drag, drop, hover, click, target line, cancel.                                                                       |
| Networking   | Optional transport layer for commands, decisions, history, and snapshots. | Should attach to stable simulation boundaries later.                                                                 |


## Nuance Notes

- Variables are key-value based. The base framework should not hardcode variable names; games, presets, schemas, or authoring packages define them.
- "Identity" is not a separate core concept. Use `Instance`; use `Identifier<T>` for all ID/reference values across the framework.
- Zone membership does not define an instance. Zone movement preserves the same instance by default.
- Zones are richer organization models, not just fixed trait bags.
- Zones do not own broad gameplay legality; game/action rules decide whether movement is allowed.
- Spatial placement is not a base zone concern unless a game-specific spatial system makes it simulation data.
- UI creates `PlayerIntent`s and reacts to state/history; it should not mutate simulation state directly.
- History envelopes preserve why state changes happened, not only what changed.
- `StateChange` entries are produced automatically by the state layer; `SemanticEvent` entries must be emitted explicitly by handlers for meanings the layer cannot infer.
- Every entity should have a base definition, including generated tokens or temporary system entities.
- Attachments and equipment are usually separate instances connected through relationships, not hidden fields on the host.
- `CommandReaction` (post-result pipeline hooks) is removed. Follow-up commands return from `GameRules.Evaluate`.