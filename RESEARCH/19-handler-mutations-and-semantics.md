# Handler Mutations, Sequential Apply, And Semantic Events

This document responds to design concerns raised about the patterns in [Action Flow Snippets](18-action-flow-snippets.md), especially `PlayCardHandler`: sequential `scope.Apply` calls, manual `scope.Semantic` next to every zone change, and repeated remove/insert pairs. It is research, not a locked API.

## 1. Is Sequential `Apply` A Problem?

**What the snippet is doing:** Within one `Handle` call, the handler calls `Apply` more than once, and later lines assume earlier mutations succeeded (for example, insert *after* a remove from the same card).

**Is that dangerous?** In a *single-threaded* command resolution, with **no partial visibility** to other systems between `Apply` calls, this is a normal and common pattern. The handler is a **transactional scope** for one `GameCommand`. You are not posting mutations to a shared public mutable store that other code can interleave. You are building an ordered list of state transitions that belong to the same **cause** (the current command) and the same **envelope** at the end.

**Where the risk actually lives:**

- **If `Apply` can fail silently** or return without honoring invariants, then yes, the next line is built on sand. The fix is not “avoid multiple applies”; it is **make `Apply` contractually reliable**: fail fast, return structured outcomes, or roll back the whole scope. Games might use **all-or-nothing** commit, **staged then commit**, or **debug assertions** after each step.
- **If mutations are valid only as a bundle** (for example, remove+insert is one logical move with no stable intermediate state for rules), a **single composite mutation** (see below) can match the *logical* atomicity you want, while still being implemented as one `Apply` from the handler’s point of view.
- **If you expose the same `ResolutionScope` to nested** calls that run in unclear order, *that* is interleaving risk. The snippet’s pattern assumes **one handler owns ordering**; child `Dispatch` should be a **well-defined** sub-tree (stack frame or nested segment), not arbitrary sharing.

**Takeaway:** Multiple `Apply` calls in one handler are *expected* when a command is implemented as a **sequence of small, reusable mutations**. The safety property is: **one command resolution, deterministic order, and no one else mutates the same state mid-sequence**—or if they do, it is an explicit sub-command with defined semantics.

## 2. Sequential Game Commands, Nested Handlers, And The Envelope Chain

A full “play this card” moment in the *simulation* is often **several game-level steps**, not one undifferentiated blob. The **envelope and tooling should make that obvious** so UI, net sync, and debug never flatten “move to play area” and “the card’s effect runs” into the *same* logical action if you need them separated.

**What to separate in meaning (not necessarily in thread/syntax):**


| Step | Typical `GameCommand` (example names)                          | What it is                                                                                                                                                                                                                                          |
| ---- | -------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | `PlayCardCommand` / `MoveInstanceCommand` (game-specific)      | **Placement / rules step**: remove from hand, insert into play area (or whatever this game thinks “played” is). This is the **move** or **play placement** in terms of state.                                                                       |
| 2+   | `HealActorCommand`, `DealDamageCommand`, `DrawCardsCommand`, … | **Follow-up resolution**: effect execution, stack resolution, or nested “do this because the card said so.” These are **another command** (or a short chain of commands), **triggered** by the first step, not “the same” command doing everything. |


**Why that matters for the history model:**

- **Not one monolithic label** like `EverythingHappened` with no interior structure. Observers need to know: **this segment** is “card moved / entered play” and **this segment** is “heal resolved,” with ordering preserved.
- **Chaining, not conflation:** The outer flow can still be a single *player request* and one *top-level* `Dispatch(PlayCardCommand)` if you want, but the **envelope** should record **ordered segments** (or frames), each with its own `Cause` metadata: *which* `GameCommand` ran, *which* handler, *which* `CommandResult`, and the **mutations/semantics** that belong to **that** command only.
- **Nested is explicit:** When `EffectContext.DispatchChild(HealActorCommand)` runs, that child is a **second** (or n-th) **command** in the **same overall resolution tick**, with a **link** in metadata such as *triggered by* / *child of* the `PlayCard` segment. You can follow a **chain** in order: parent segment → child segment(s) → optional further children. Siblings and depth are a **tree**; a simple linear “append segments” is enough for many games.
- **UI:** Segment 1 drives “card moves to play” animation; segment 2 drives “heal number” or VFX, without guessing that one semantic covered both.

**Flat vs shaped envelope:**

- A **flat** list of *all* `HistoryEntry` rows in one bag without **segment** boundaries makes it hard to see “one action then another.”
- A **shaped** envelope: **ordered `HistorySegment`[]** (or equivalent), where each segment has `{ Command, Result, RulePhase?, Entries: HistoryEntry[] }` and optional `ParentSegmentId` / `TriggerKind`. Same underlying facts can be rendered as a **followable chain** for animation and for multiplayer logs.

**Relationship to the next section:** **Composite mutations** (below) are about *packaging low-level* remove+insert into one `Apply`. **Nested commands** are about *a different game command* (effect) with its **own** handler and **own** segment. Both can appear in one play: first segment uses a composite move + semantics for “entered play,” later segments are child `GameCommand` dispatches for effects.

## 3. Why Semantic Events Are Not Automatically Every Mutation

There are two different streams in the history model:


| Stream                                   | Role                                                              | How it often appears                                                                                             |
| ---------------------------------------- | ----------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| **State change** (`StateChange` / diffs) | *What* changed in canonical state                                 | Can often be **derived in one place** when a `StateMutation` is applied.                                         |
| **Semantic event**                       | *What it means* in game language (`CardPlayed`, `DamageDealt`, …) | Often **not** recoverable from raw diffs alone, because the **same** low-level change can mean different things. |


**Example of ambiguity:** A `RemoveFromZone` + `InsertIntoZone` could be “play from hand to battlefield,” “discard from hand,” “sacrifice from battlefield,” or “opponent’s spell resolve.” The **physics** of zone entries can match while the **meaning** for animation and rules differs.

So:

- **Automatic everything from `InsertIntoZoneMutation` is risky** if the framework guesses the wrong *meaning*.
- **Automatic `StateChange` from every mutation** is a good default: one implementation path, one place to record `VariableChanged`, `ZoneContentRemoved`, and so on.
- **Semantic events** can still be *partially* **policy-driven** (for example, “when this zone’s insertion is triggered by a `PlayCard` command, emit `CardMoved` with cause play”)—but that drifts into **command-aware** or **context-aware** code, not pure mutation.

**Takeaway:** Repeating `Semantic` at every `Apply` is not a law of nature; it is a **teaching** snippet. The long-term goal is: **low-level diffs** largely automatic from mutations; **high-level meaning** from **where you still need human intent** (handler, command kind, or a small set of **named operations**).

## 4. Composite Zone Moves: What It Is, And How The Scope Triggers It

**Composite (mutation layer)** = one *logical* move with one `Apply` from the handler, implemented *inside* the state layer as **remove + insert** (using each zone’s default placement) plus whatever `StateChange` rows you choose to emit. It does **not** replace a **nested `GameCommand`**; it only replaces *verbose micro-mutations* in the **same** command handler (usually `PlayCard` or `MoveInstance`).

**Two equivalent shapes (research-level, not a final API):**

**A. Single `StateMutation` type that encapsulates the pair**

The handler (or a helper) calls `Apply` **once** with a record that *means* “relocate this instance from zone A to zone B.”

```csharp
// One record; backend owns remove → insert and default zone ordering.
public sealed record RelocateInstanceMutation(
    ZoneId From,
    ZoneId To,
    InstanceId Instance,
    RelocateOptions? Options = null) : StateMutation;
```

**B. `ResolutionScope` sugar that builds the same thing**

The handler does not name `RelocateInstanceMutation` every time; the scope **creates and applies** the composite so rules code reads as “move this card”:

```csharp
// Inside ResolutionScope
public void RelocateInstance(ZoneId from, ZoneId to, InstanceId instance, RelocateOptions? options = null)
{
    // Default: one Apply of RelocateInstanceMutation; `options` can carry non-default
    // placement for the exceptional cases.
    Apply(new RelocateInstanceMutation(from, to, instance, options));
}
```

**What happens on `Apply` (conceptually, not a prescription):**

1. The **state backend** validates preconditions (instance in `From`, and so on) or returns failure; the handler can react before any other step in the same command.
2. The backend **removes** the instance from `From`, then **inserts** into `To` with **default** zone policy (no separate “plan” in the handler for the common case; see [Action Flow Snippets](18-action-flow-snippets.md)).
3. The backend appends to the **current segment’s** history: either **two** `StateChange` lines (out of A, into B) or **one** composite “relocated” line if the game wants coarser history for replay/animation.
4. Optional: one **default** `Semantic` for the *net* move, if the project chooses policy-driven `CardMoved` for relocations. Game-specific `Semantic` for “played from hand” still lives in the `PlayCard` handler or in a command-aware policy.

**When to use composite `Apply` vs two raw mutations**

- **Composite:** one English sentence in the rules—“**move** this from here to there”—and you want the handler to stay short; intermediate state is not interesting.
- **Two explicit `Remove` + `Insert`:** debugging, a game that *must* hook between remove and insert, or replay tooling that only understands primitive mutations without a `Relocate` type yet.

**Composite vs nested command (again, clearly):**

- `scope.RelocateInstance(Hand, PlayArea, card)` = **one** `GameCommand`’s *mutation step* (same segment as `PlayCardCommand`’s placement).
- `scope.DispatchChild(HealActorCommand)` = **another** `GameCommand`, **new** handler, **new** segment in the envelope; that is the “**second** action” (resolve effect), not a fancier `Apply`.

**C. Play lifecycle: hand → play → discard**

A second composite call for “relocate to discard” keeps the same pattern without four primitives in the open:

```csharp
var discard = scope.Game.DiscardZoneFor(command.Actor);
scope.RelocateInstance(command.PlayZone, discard, command.Source);
// or: scope.Apply(new RelocateInstanceMutation(command.PlayZone, discard, command.Source));
```

**D. “Nested” at the *mutation* level (optional building block)**

If you want a **single** `Apply` to mean “run this ordered list of *primitive* mutations as one unit” (all-or-nothing, one bundle of `StateChange`):

```csharp
scope.Apply(CompositeMutation.Seq(
    new RemoveFromZoneMutation(from, id),
    new InsertIntoZoneMutation(to, id)));
```

This is *still* the **same** `GameCommand` and **same** segment; it is *not* a child `GameCommand`. Prefer `**RelocateInstanceMutation`** for moves when the pair is always remove+insert with zone defaults, so the state layer can optimize and emit a consistent `StateChange` story.

**Takeaway (composite):** The scope **triggers** the composite by `Apply(single move mutation)` or `RelocateInstance(...)`. The **envelope** still gains **separate** segments when you **DispatchChild** for effects, not when you use a compositional *mutation* to tidy up a zone move.

## 5. Composite In Practice: Card Movement In `PlayCardHandler` And `ExecuteCardHandler`

The two prime candidates from the snippet doc share an identical low-level pattern: remove a card from one zone, insert it into another. `PlayCardHandler` also emits a `CardMovedEvent` semantic; `ExecuteCardHandler` silently omits it. Neither is wrong for a teaching snippet, but together they show the cost of not having a named operation: duplicated mutations and inconsistent semantics.

### Before — verbose, inconsistent

**`PlayCardHandler`** (placement step):
```csharp
// three lines for one logical move, semantic emitted manually
scope.Apply(new RemoveFromZoneMutation(command.FromZone, command.Source));
scope.Apply(new InsertIntoZoneMutation(command.PlayZone, command.Source));
scope.Semantic(new CardMovedEvent(
    card: command.Source,
    fromZone: command.FromZone,
    toZone: command.PlayZone));
```

**`ExecuteCardHandler`** (move to discard after resolution):
```csharp
// same two mutations, but CardMovedEvent is missing — silent inconsistency
scope.Apply(new RemoveFromZoneMutation(command.FromPlayZone, command.Source));
scope.Apply(new InsertIntoZoneMutation(command.DiscardZone, command.Source));
```

In both cases the English sentence is the same — "move this card from zone A to zone B" — but the code says it differently every time and forgets the semantic in one of the two places.

A scope extension method (`scope.MoveCard(from, to, card)`) would collapse the boilerplate but is a dead end at scale: it has no seam, cannot be intercepted, cannot be overridden per game, and does not appear in history as a named operation. It is just an alias — it cannot be *reacted to*.

### The composite operation

A `CompositeOperation` is not a `GameCommand` and not a raw mutation. It is a named, self-contained unit of scope-level work: it owns its internal primitives, emits its own semantics, and is routed through the scope so it can be intercepted at both levels — before its primitives run and via the events it emits.

```csharp
// Not a GameCommand — no command type, not enqueued by the engine.
// A named scope-level operation that owns its own primitives and semantics.
public interface ICompositeOperation
{
    void Execute(ResolutionScope scope);
}
```

```csharp
public sealed record MoveCardOperation(
    ZoneId From,
    ZoneId To,
    InstanceId Card) : ICompositeOperation
{
    public void Execute(ResolutionScope scope)
    {
        // Primitives survive as the implementation atoms — full control retained.
        scope.Apply(new RemoveFromZoneMutation(From, Card));
        scope.Apply(new InsertIntoZoneMutation(To, Card));

        // Semantic emitted unconditionally from one place.
        scope.Semantic(new CardMovedEvent(Card, From, To));
    }
}
```

The scope routes composite operations rather than calling them directly, which provides the interception seam:

```csharp
// ResolutionScope
public void Execute(ICompositeOperation operation)
{
    // Pre-hooks run here — before any primitive inside the operation fires.
    RunCompositePreHooks(operation);

    operation.Execute(this);

    // Post-hooks or scope-level recording run here if needed.
}
```

### Two levels of reactability

**Event level** — anything that listens for `CardMovedEvent` reacts to the *result* of the operation. This is the right place for trigger effects ("whenever a card moves to the graveyard, draw a card"). Because the event now comes from one place unconditionally, the trigger is reliable regardless of which handler initiated the move.

```csharp
// A trigger registered on the scope or engine — reacts to what the operation emitted.
// Fires for both PlayCard and ExecuteCard moves without either handler doing anything extra.
case CardMovedEvent e when e.ToZone == WellKnownZones.Graveyard:
    scope.DispatchChild(new DrawCardsCommand(actor: e.MovingActor, amount: 1));
    break;
```

**Operation level** — a pre-hook on the composite itself runs *before* `RemoveFromZone` or `InsertIntoZone` fire. This is the right place for prevention effects ("if a card would move to the graveyard, exile it instead") — the operation can be cancelled or replaced before any state changes.

```csharp
// A composite pre-hook — intercepts the operation before its primitives run.
public sealed class GraveyardReplacementHook : ICompositePreHook<MoveCardOperation>
{
    public CompositePreHookResult<MoveCardOperation> Evaluate(MoveCardOperation op, ResolutionScope scope)
    {
        if (op.To == WellKnownZones.Graveyard && scope.HasReplacementEffect(op.Card))
            return CompositePreHookResult<MoveCardOperation>.Replace(
                op with { To = WellKnownZones.Exile });

        return CompositePreHookResult<MoveCardOperation>.Continue(op);
    }
}
```

### After — one `Execute` call per move, both handlers consistent

**`PlayCardHandler`**:
```csharp
public PlayCardResult Handle(PlayCardCommand command, ResolutionScope scope)
{
    scope.Semantic(new CardPlayStartedEvent(
        actor: command.Actor,
        card: command.Source,
        fromZone: command.FromZone,
        playZone: command.PlayZone));

    scope.Execute(new MoveCardOperation(command.FromZone, command.PlayZone, command.Source));

    scope.Semantic(new PlayCardResolvedEvent(command.Actor, command.Source, command.PlayZone));

    return new PlayCardResult { Status = CommandStatus.Success, PlayedCard = command.Source };
}
```

**`ExecuteCardHandler`**:
```csharp
public ExecuteCardResult Handle(ExecuteCardCommand command, ResolutionScope scope)
{
    var card = scope.GetInstance(command.Source);
    var definition = scope.GetDefinition(card.DefinitionId);

    definition.Effect.Trigger(new EffectContext(
        actor: command.Actor,
        sourceInstance: command.Source,
        scope: scope));

    scope.Execute(new MoveCardOperation(command.FromPlayZone, command.DiscardZone, command.Source));

    return new ExecuteCardResult { Status = CommandStatus.Success, ExecutedInstance = command.Source };
}
```

### What this illustrates

- **Primitives survive.** `RemoveFromZoneMutation` and `InsertIntoZoneMutation` remain the atoms inside `MoveCardOperation.Execute`. The composite is an abstraction over the *pattern*, not over the *mechanism* — you still control every step.
- **Named, not anonymous.** `MoveCardOperation` has an identity. It appears as a named entry in history, shows up in debug tooling, and can carry metadata (initiating actor, reason) without the call site having to supply it on every `Apply`.
- **Two hook levels for two rule types.** Replacement effects (prevent, redirect) hook at the operation level before primitives run. Trigger effects (react to what happened) hook at the event level after. Both are the right model for their respective rule types.
- **Reusable from anywhere.** Any handler, effect, or child dispatch can `scope.Execute(new MoveCardOperation(...))` and get consistent primitives, consistent semantics, and consistent hook coverage — without knowing or caring how the move is implemented.
- **Changeable internals, stable call sites.** If a game adds "cards leaving hand trigger loyalty loss" or "zone insertion order matters," that logic goes inside `MoveCardOperation.Execute` or its hooks. Every call site gets the change for free.

## 6. Reducing Fumbling: Scope Helpers, Defaults, And Child Segments (Recap)

To avoid the “remove then insert twice, type four events” feel without losing clarity:

- **Composite relocations** (above) and optional **policy defaults** for boring `CardMoved` semantics.
- **Play lifecycle** may use two `Relocate` calls (to play, then to discard) or a **named** `PlayCard` helper that does both in one place—still one `PlayCard` **command** for the *placement and resolution* story, with **child commands** for effect bodies.
- **Nested command segments** when `EffectContext.DispatchChild` runs: **separate** handler, **separate** segment, **ordered** after the parent’s mutations (see [Action Definition Proposal](17-action-definition-proposal.md) on nested envelopes).

**Takeaway:** The [snippet doc](18-action-flow-snippets.md) is **verbose on purpose** to show each layer. Productivity is **composites** + **child dispatches** with **shaped** envelopes—not one flat list pretending one `GameCommand` did the move and the heal as the same *kind* of action.

## 7. How This Stays Aligned With Earlier Decisions

- **Determinism and multiplayer:** The boundary remains **game commands** (and, if needed, **aggregated** history for clients). Composites and helpers do not change the boundary; they **package** the same state transitions with less boilerplate.
- **Zones and legality:** Zone **organization** still applies *after* the command is allowed; compositing moves does not move “who validates legality” into the zone; it is still **game/action rules** before `Apply` (see [Zone Model](12-zone-model.md)).
- **Batched history for UI:** Whether you record **one** `Relocate` fact or **two** `ZoneContent` facts is an **envelope design** choice; animation can subscribe to **semantics** and/or **deltas** (see [Action Flow Decisions](14-action-flow-decisions.md)).

## 8. Open Choices (To Decide In Implementation, Not In Research)

- **Granularity of `StateChange` in history:** finest grain vs composite rows for replay and net sync.
- **Whether `Semantic` is always explicit for “meaningful” events** vs **inferred** from a small catalog of `NamedGameOperation` types.
- **Child commands:** flat list in one envelope vs **tree** of segments; affects how the UI times “play animation” vs “heal number pop.”

These are tuning knobs, not a refutation of the pipeline shape.

## 9. Short Answers To The Original Worries

1. **“We remove then insert hoping the first worked.”** — In a single `Handle` on a `ResolutionScope`, ordered `Apply` is normal. Make failures **loud** or **transactional**; use **composites** if intermediate states are invalid for your game.
2. **“We have to `Semantic` everywhere we `Apply` for zones.”** — No long-term. Prefer **auto `StateChange*`*, and **composites or defaults** for common `CardMoved` semantics; reserve manual `Semantic` for **meanings the engine cannot infer** from raw data alone.
3. **“We do remove/insert twice for hand→play then discard.”** — That is the **play lifecycle**; wrap it in **one or two** named operations (composite or `PlayCard` sub-phases) so handlers read like rules, not like four copy-pasted mutations.

## Related

- [18-action-flow-snippets.md](18-action-flow-snippets.md) — motivating example
- [17-action-definition-proposal.md](17-action-definition-proposal.md) — payload/result/service split and flow graph
- [15-action-request-command-history-model.md](15-action-request-command-history-model.md) — history envelope and layers

