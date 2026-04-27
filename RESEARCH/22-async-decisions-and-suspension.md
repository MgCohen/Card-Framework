# Async Decisions And Suspension

This document explores how the engine handles effects that require player input mid-execution: targeting, choices, mulligans, and similar decision points. The approach is to trace desired UX outcomes first, then work backwards to infer data requirements, envelope shape, and finally the suspension mechanism.

Builds on:

- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop, `GameRules`, `OnQueueDrained`
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — trigger fold, dispatch channel
- [18-action-flow-snippets.md](18-action-flow-snippets.md) — pipeline, envelope, handler flow

---

## 1. The Problem In One Sentence

Some effects cannot complete without asking the player something. The current pipeline is synchronous — a handler runs start to finish and returns. We need to decide how a handler that needs input mid-run should behave.

---

## 2. Concrete Scenarios

Four representative cases, ordered from simplest to most complex.

### Scenario A — Targeted Spell (Slay The Spire style)

> Player plays a fireball. Fireball needs a target enemy before it can deal damage.

**What the player sees:**
1. Card lifts from hand.
2. Game enters targeting mode — valid targets highlight.
3. Player taps/clicks a target.
4. Fireball animates toward the target, damage resolves.
5. Card moves to discard.

**What has changed in state when targeting starts:**
- The card may or may not have moved to the play zone yet.
- No damage has been applied.
- No history has been published.

**What other actors see while targeting is open:**
- In a single-player game: nothing — this is a local pause.
- In a multiplayer game: the opponent should probably see that a spell is being cast and is awaiting targeting, but not necessarily know the intent yet.

### Scenario B — Discover / Choose One (Hearthstone style)

> Player plays a card that says "Choose one of three generated options."

**What the player sees:**
1. Card is played.
2. A choice overlay appears with three options.
3. Player picks one.
4. The chosen effect resolves.

**What has changed in state when the choice overlay opens:**
- The card has been played (moved from hand, cost paid).
- The three generated options have been created (they may be entities or data).
- No effect has resolved yet.

**What other actors see:**
- Opponent knows a card was played (card left hand, mana was spent).
- Opponent does not know which option will be chosen.
- Opponent sees no effect until the choice resolves.

### Scenario C — Mulligan

> At game start, each player may swap any of their starting cards before the game begins.

**What the player sees:**
1. Starting hand is shown.
2. Player selects cards to replace.
3. Confirms.
4. Replaced cards go to bottom of deck, new cards drawn.
5. Game begins.

**What has changed in state during mulligan:**
- The session has started but no turn has begun.
- Hand state is technically live.
- Player is in a pre-game decision window.

**What other actors see:**
- In a two-player game, each player mulligans concurrently but in isolation.
- Neither player sees the other's hand or choices.
- Both players see when the other has confirmed.

### Scenario D — Mid-Chain Target Request (MTG style)

> Player casts a spell. Spell triggers an ability on another card. That ability requires the player to choose a target.

**What the player sees:**
1. Original spell resolves partially.
2. A triggered ability fires.
3. Targeting prompt opens for the triggered ability's target.
4. Player picks a target.
5. Triggered ability resolves.
6. Original spell finishes.

**What has changed in state when the triggered targeting opens:**
- The original spell may have already applied some state changes.
- History for those changes exists but the envelope is not closed.
- A second decision point has opened inside an in-progress action chain.

**What other actors see:**
- Opponent sees the partial resolution of the first spell.
- Opponent knows a triggered ability is waiting for a target.
- Opponent waits.

---

## 3. What The Client Needs In Each Case

Working through scenarios A–D, the client needs:

| Moment | Client needs |
|---|---|
| Decision opens | Know that a decision is active |
| Decision opens | Know what kind of decision it is (targeting, choose-one, mulligan, etc.) |
| Decision opens | Know the available options or valid targets |
| Decision opens | Know which actor must decide |
| While decision is open | See any state that was already committed before the pause |
| While decision is open | See other actors waiting (in multiplayer) |
| Decision resolves | See the choice that was made (or just the result, if hidden) |
| After decision | See the effect complete from where it left off |

The critical observation: **the client needs to see committed state before the pause, but should not see uncommitted state that was speculatively started.**

---

## 4. What This Means For Envelopes

The current model is: one `HistoryEnvelope` per command, closed and published when the command finishes.

If a command suspends mid-execution, three options exist:

### Option 1 — Hold the envelope until the decision resolves

The envelope is not published until the entire command finishes — including after the decision is answered. The client sees nothing until the full effect completes.

**Consequence:** For effects that cause visible state changes *before* the decision (e.g. paying a cost, moving a card to the stack), the client is blind during the decision window. This is only acceptable if no state change happens before the decision point.

**Works for:** Scenario A if the card has not yet been moved or cost paid. Scenario B if the card play is atomic with the choice.

**Fails for:** Scenario D — the first spell has already applied changes; those need to be visible while the triggered ability waits for a target.

### Option 2 — Publish a partial envelope, then a continuation envelope

When execution suspends, close and publish the envelope up to the suspension point. When execution resumes and completes, publish a second envelope referencing the same chain ID.

**Consequence:** Two envelopes for one logical command. The client can animate the first half, then animate the second half after the decision. History is split but causally linked via `OriginCommandId`.

**Works for:** All scenarios. The chain ID ties the two envelopes together so history and replay remain coherent.

**Complication:** The pipeline needs a concept of a suspendable command — something that can emit a partial envelope and then continue.

### Option 3 — Decisions happen before state changes (decision-upfront model)

Restructure effects so that any required decisions are gathered *before* any state mutations happen. The full decision context is collected up front, then the handler runs to completion without suspending.

**How it could work:**
- During the `OnBefore<T>` fold, the command is inspected and any required targeting or choices are declared.
- The engine opens a decision, waits for a response, and injects the resolved payload into the command before the handler runs.
- The handler receives a fully-resolved command with targets already chosen, and runs synchronously.

**Consequence:** No mid-handler suspension needed. The handler is always synchronous. The decision phase is separated from the execution phase.

**Works well for:** Simple cases where targets and choices are known before the effect runs — most spells, most abilities.

**Fails for:** Cases where the needed decision depends on mid-execution state (Scenario D — the triggered ability's valid targets may depend on what the first spell just did). Also fails when an effect conditionally requires a decision only if certain state conditions are met at execution time.

---

## 5. Key Design Tension

The tension is between two ideals:

**Ideal A — Authoring simplicity.** An effect reads like a linear script:
```
cost.Pay();
var target = await scope.RequestTarget(filter);
target.ApplyDamage(damage);
source.MoveToDiscard();
```
The author sees a clear flow. The suspension is invisible.

**Ideal B — Pipeline simplicity.** The pipeline has no suspension concept. Handlers are synchronous functions. Decisions are always resolved before execution begins.

These two ideals are in direct tension. Pursuing A requires async infrastructure. Pursuing B requires that effects can declare their decisions upfront, which is not always possible.

---

## 6. Candidate Models

### Model 1 — Async Coroutine (Ideal A)

Handlers are async. `scope.RequestDecision(...)` suspends the handler. The engine parks the command, opens a `Decision`, and resumes when the player responds.

**Requires:**
- Async pipeline (`async/await` or coroutine mechanism).
- Engine state to track in-progress commands.
- A partial-envelope publication mechanism (Option 2 above) for cases where state was already changed before the suspend.
- Care around Unity's main-thread constraint and what "resume" means.

**Authoring experience:** Linear, readable. The handler is one function.

**Envelope shape:** Either held until complete (if no pre-decision state changes) or split into two linked envelopes.

### Model 2 — Decision-Upfront (Ideal B)

Decisions are declared as part of the command, resolved by the engine before the handler runs, and injected as resolved payload.

```csharp
public sealed record FireballCommand : GameAction
{
    public InstanceId Source { get; init; }
    public TargetRequirement TargetRequirement { get; init; }
    public InstanceId? ResolvedTarget { get; init; } // null until decision resolves
}
```

The engine detects an unresolved decision, opens the `Decision`, waits, then re-dispatches the command with `ResolvedTarget` filled in.

**Requires:**
- Commands that carry both a decision declaration and a resolved payload field.
- Engine logic to detect and open decisions before execution.
- Re-dispatch mechanism.

**Authoring experience:** More verbose. Two-phase commands are not as readable as linear scripts.

**Fails for:** Cases where the decision cannot be known until mid-execution.

### Model 3 — Split Commands (no suspension)

Handlers never suspend. When input is needed, the handler dispatches a `RequestDecisionCommand`, records a continuation command ID, and returns. The player's response dispatches a `ResolvesDecisionCommand` that carries the payload and the continuation ID. A continuation handler picks up from where the first left off.

**Requires:**
- Convention for pairing request and resolution commands.
- Continuation command concept.
- State that remembers "we are partway through an effect."

**Authoring experience:** Most verbose. Effect logic is split across two handlers.

**Works for:** All scenarios, but at the cost of authoring complexity.

---

## 7. Tentative Direction

No final decision yet, but the desired-outcome analysis suggests:

1. **Decisions that happen before any state change** (most targeting, most choose-one) can use a **decision-upfront** approach (Model 2) cleanly. The command declares its requirements; the engine resolves them before dispatching to the handler.

2. **Decisions triggered by mid-chain state** (Scenario D style) likely need **async coroutine** (Model 1) or split commands (Model 3). The decision-upfront model cannot handle cases where valid options depend on state that only exists after partial execution.

3. **Partial envelopes** (Option 2) are probably necessary regardless of which suspension model is chosen — some effects will have pre-decision state changes that the client must see while waiting.

4. **The envelope split must be explicit and causally linked.** Two envelopes for one logical effect are acceptable as long as they share `OriginCommandId` and the history record can reconstruct the full flow.

This suggests a **hybrid**:
- Prefer decision-upfront for the common case.
- Support async suspension for the rare case where mid-execution state determines what the decision needs to be.
- Treat partial envelope publication as a first-class concept rather than an edge case.

---

## 8. Open Questions From This Analysis

- **When is a partial envelope published?** At the point `scope.RequestDecision` suspends? Explicitly by the handler? Automatically by the engine?
- **Does the client need to know a decision is open before the partial envelope arrives, or is the partial envelope what signals it?**
- **In multiplayer: does a `DecisionOpenedSignal` need to go on the wire immediately when a decision opens, even before the partial envelope?**
- **Mulligan (Scenario C) is a pre-game decision, not a mid-command decision — does it go through the same pipeline, or is it a separate session lifecycle concept?**
- **Can decisions be nested?** (Scenario D suggests yes — a triggered effect inside a command's execution opens a second decision. What is the stack shape for nested open decisions?)
- **What is the re-entry contract for the engine when a decision resolves?** Does it call a new `Dispatch`, resume a parked coroutine, or something else?
- **What does `scope.RequestDecision` return if the player cancels?** Is cancellation of a mid-effect decision always possible? What rolls back?

---

## 9. Related

- [20-engine-evaluation-loop.md](20-engine-evaluation-loop.md) — engine loop, Stash scheduling decision
- [21-trigger-reaction-system.md](21-trigger-reaction-system.md) — trigger fold, OnBefore/OnAfter
- [18-action-flow-snippets.md](18-action-flow-snippets.md) — pipeline and envelope shape
- [06-open-questions.md](06-open-questions.md) — async suspension open item
