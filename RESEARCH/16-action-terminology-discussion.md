# Action Terminology Discussion

This document frames the naming problem around action flow. It exists because DeckMech showed that overloaded words like "action", "command", "effect", "intent", and "trigger" can blur together quickly.

This is a discussion document for the next pass, not a decision record.

## Problem

The framework will need several command-pattern-like concepts:

- Player input attempts.
- UI interaction events.
- Possible actions/options.
- Authored card actions.
- Runtime game commands.
- Low-level state mutations.
- Card effects.
- Triggers and reactions.
- History events.
- Scheduler tasks.

If these all get called "actions" or "commands", the architecture becomes hard to discuss and eventually hard to implement.

DeckMech had this problem with terms like:

- `GameAction`.
- `ActionPlan`.
- `ActionIntent`.
- `MoveActionCommand`.
- `MoveUnitCommand`.
- `ICommand`.
- `CommandDispatcher`.
- `CommandHandler`.
- `EffectContext`.

The layers were often good, but the naming made it easy to send work through the wrong door.

## Current Unsettled Terms


| Candidate Term | Possible Meaning                                                                   | Risk                                                         |
| -------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| Action         | Could mean player action, authored card action, possible action, or engine action. | Too overloaded.                                              |
| Intent         | Informal player/system attempt.                                                    | May be too vague for concrete APIs.                          |
| PlayerIntent   | Structured thing submitted to the engine.                                          | **Settled name** for UI/network boundary. Covers all actor-originated inputs. |
| Command        | Could mean high-level game command or low-level state mutation.                    | Needs qualifiers.                                            |
| Game Command   | High-level command requiring rules logic and result.                               | Clear but possibly too implementation-flavored.              |
| State Command  | Low-level already-approved mutation.                                               | Clear if Store/mutator model remains.                        |
| Effect         | Behavior attached to cards, abilities, rules, or triggers.                         | Should not be overloaded with state mutation.                |
| Trigger        | Rule that reacts to something.                                                     | Could mean pre-hook, post-reaction, or event listener.       |
| Reaction       | Post-result hook.                                                                  | Good but may overlap with trigger.                           |
| Event          | Fact that happened.                                                                | Can mean domain event, UI event, history event, or C# event. |
| History Entry  | Ordered simulation fact for replay/UI/debug.                                       | Clearer than generic event.                                  |
| Envelope       | Batch of related changes and metadata.                                             | Good for history/network/debug grouping.                     |


## Layers We Need To Name

### 1. Player Or System Attempt

This is what happens when something tries to do something:

- Player drags a card.
- AI chooses an option.
- Network client submits a selected option.
- Scripted enemy acts.
- Replay replays an input.

Candidate names:

- `Intent`.
- `Request`.
- `ActionRequest`.
- `InteractionRequest`.
- `PlayerIntent`.

**Settled:** Use `PlayerIntent` as the structured type name. Use `intent` informally in docs.

### 2. Available Or Possible Choice

This is something the engine exposes as legal/suggested/available:

- Play this card.
- Move this card here.
- Choose target X.
- Pass priority.

Candidate names:

- `Option`.
- `PossibleAction`.
- `AvailableAction`.
- `ActionOption`.
- `DecisionOption`.

Likely direction:

Use `Option` inside a `Decision`, and maybe `AvailableAction` for broader non-decision UI affordances.

### 3. Authored Card/System Behavior

This is content-defined behavior:

- Card has a primary behavior.
- Ability can be activated.
- Relic has passive behavior.
- Zone has an organization behavior.

Candidate names:

- `Effect`.
- `Ability`.
- `Behavior`.
- `GameAction`.
- `ActionDefinition`.

Likely direction:

Avoid `GameAction` for the general framework unless we are talking about scheduled/timeline actions specifically. For cards, start with `Effect` as a simple entry point and keep deeper effect design deferred.

### 4. High-Level Engine Command

This is the thing that enters the game pipeline and needs rules:

- Play card.
- Move instance.
- Deal damage.
- Draw card.
- Attach equipment.
- Resolve stack item.

Candidate names:

- `GameCommand`.
- `RulesCommand`.
- `ActionCommand`.
- `EngineCommand`.

Likely direction:

`GameCommand` is clear enough for now, but should be revisited before implementation.

### 5. Low-Level State Mutation

This is the already-approved state change:

- Insert instance into zone.
- Set variable.
- Add modifier.
- Create instance.
- Remove instance from zone.

Candidate names:

- `StateCommand`.
- `MutationCommand`.
- `StateMutation`.
- `StateFact`.

Likely direction:

Use `StateMutation` for framework-level docs. DeckMech's `StateCommand` remains a useful implementation reference, but the framework vocabulary should avoid making every layer a "command."

### 6. Pipeline Hooks

Pre-handler behavior:

- Validate.
- Cancel.
- Replace.
- Redirect.
- Modify.

Post-handler behavior:

- Observe result.
- Dispatch follow-up.
- Trigger ability.
- Start animation.

Candidate names:

- Pre: `PreHook`, `Filter`, `Interceptor`, `Replacement`, `Validator`.
- Post: `Reaction`, `Observer`, `AfterHook`, `Trigger`.

Likely direction:

Use `Pre-Hook` and `Post-Reaction` in docs for now. Later, specific systems like "replacement effect" or "triggered ability" can map onto those hooks.

### 7. History And Notification

This is what observers consume:

- State changed.
- Command resolved.
- Card moved.
- Damage happened.
- Decision opened.

Candidate names:

- `HistoryEntry`.
- `SimulationEvent`.
- `DomainEvent`.
- `StateChange`.
- `HistoryEnvelope`.

Likely direction:

Use `HistoryEntry` for ordered facts and `HistoryEnvelope` for grouped cause-aware batches.

## Strong Current Preferences

- Avoid using `Action` alone as a core type name.
- Use `PlayerIntent` for structured external attempts.
- Keep `Option` connected to `Decision`.
- Use `GameCommand` as a provisional term.
- Prefer `StateMutation` over `StateCommand` in framework docs.
- Use `HistoryEnvelope` for batched state-change output.
- Treat `Effect` as an authoring/content term, not as a state mutation term.
- Keep `Trigger` broad for now, but separate pre-hooks and post-reactions in the pipeline docs.

## Questions For Next Discussion

1. ~~Should the external submitted object be `Request`, `Intent`, or `ActionRequest`?~~ **Settled:** `PlayerIntent`. See [Action Definition Proposal](17-action-definition-proposal.md).
2. Should `GameCommand` be renamed before it becomes sticky?
3. Should `StateCommand` be called `StateMutation` instead?
4. Should `Effect` mean only authored card behavior, or any executable behavior?
5. Should `Trigger` be a content concept while `PreHook`/`PostReaction` are pipeline concepts?
6. Should generated choices be `Decision -> Option`, or should `PossibleAction` be a separate top-level concept?
7. How much of this terminology should be user-facing in editor tooling?

## Temporary Working Vocabulary

Until this is settled, research docs can use:


| Working Term     | Meaning                                    |
| ---------------- | ------------------------------------------ |
| Intent           | Informal attempt.                          |
| PlayerIntent     | Structured attempt submitted to engine.    |
| Decision         | Engine-created choice window.              |
| Option           | Choice inside a decision.                  |
| Game Command     | High-level command resolved by game rules. |
| StateMutation    | Low-level approved state mutation.         |
| Effect           | Authored behavior entry point.             |
| Pre-Hook         | Pipeline behavior before command handler.  |
| Handler          | Main command resolver.                     |
| Post-Reaction    | Pipeline behavior after command result.    |
| Result           | Structured command outcome.                |
| History Entry    | Ordered fact.                              |
| History Envelope | Cause-aware batch of facts.                |
