---
type: adr
status: accepted
tags: [#adr, #general, #architecture, #engine, #effects, #triggers]
---

# ADR-0006 — Two-pipeline architecture: framework command pipeline + game effect pipeline

Date: 2026-05-07
Context-doc: features/engine/models/03-game-rules-runtime.md, features/engine/models/01-game-engine.md
Proposal: session triggers+dispatch design pass (2026-05-07)

## Context

A design pass on triggers + dispatch flow surfaced that the framework had been conflating two distinct schedulers:

1. **Command-level scheduling** — what's the next atomic action to execute? Strict FIFO; one at a time; framework plumbing.
2. **Effect-level scheduling** — when does a multi-command effect (or triggered ability) get to *start*? This varies by game style (Stacklands runs immediately; Hearthstone nests freely; MTG batches on a stack zone and resolves LIFO).

Earlier candidates tried to give the runtime queue-routing control over commands (`Enqueue` / `GetNext` / `EnqueueReaction` / `EnqueueReplacement` virtual methods on `GameRulesRuntime<T>`). Walking through Stacklands, Hearthstone, and MTG-like game styles concretely showed this was the wrong abstraction level: what games actually customize is when an effect *starts*, not where individual commands land in a queue.

A second observation reinforced the split: in MTG, the stack does not hold raw `GameAction` instances — it holds resolving cards and triggered abilities (entity-like things). When one of those resolves, its effect runs and dispatches commands; those commands flow through the framework's command queue normally. The "stack" is game state, not the engine queue.

## Options considered

- **A — Single queue, runtime owns routing** (B′ from the design pass). Runtime exposes `Enqueue` / `GetNext` / `EnqueueReaction` / `EnqueueReplacement` virtual methods so games can customize where each command lands in the queue. *Rejected:* per-command routing isn't what games actually customize; the real differentiation is effect-level orchestration (when an effect starts, whether it can nest, batch, or stack). Putting that knob on the command queue solves the wrong problem.
- **B — Single queue, framework-owned, all game customization expressed via game state**. Effect-level scheduling lives implicitly in game-state zones (stack, trigger queue) watched by `Evaluate`. Cleaner than A but leaves the effect-pipeline contract entirely implicit and undocumented.
- **C — Two pipelines, named explicitly.** Framework owns the command pipeline (FIFO, one-at-a-time, fold-based per-command primitives). Game customizes the effect pipeline (decides when each effect starts). Chosen.

## Decision

**Three-level model.** The framework distinguishes three concerns:

1. **Commands** — atomic state-change units. One at a time, framework FIFO. `GameAction` is the queueable subtype.
2. **Effects** — multi-command logic units. Game-scheduled.
3. **Triggers** — effects with an intercepted-command payload. Reactive (kicked off by the trigger fold). Internally identical to effects; the only differences are *what kicks them off* and *what they receive*.

Triggers ⊆ Effects.

**Command pipeline (framework).** Strict FIFO; one command at a time. `pipeline.Execute(action)` runs before-fold → handler → after-fold → envelope. Per-command primitives operate on the in-flight command via `IEffectScope`:

- `scope.Modify` — adjust the in-flight payload
- `scope.Cancel(reason)` — original is skipped
- `scope.Replace(other)` — original is swapped for `other`
- `scope.Dispatch(side)` — `side` enters the command queue; runs after the current command

The pending list is engine-private. No game customization at this level. `runtime` does not own command-queue routing.

**Effect pipeline (game).** Game-customizable orchestration that decides *when* each effect starts, whether effects nest mid-execution, batch, or sit in a game-state zone (e.g., MTG stack) until resolved. The mechanism — queue methods, "should-start-now" hook, state-driven via `Evaluate`, or a mix — is the open design surface. Tracked as the next design pass.

**Runtime contract responsibilities.** `GameRulesRuntime<T>` decides:

- Trigger eligibility (`FilterTriggers`).
- State-based follow-up actions (`Evaluate`).
- Effect-pipeline orchestration (shape TBD).

It does not own command-queue routing.

## Consequences

- Stacklands "before drawing, take 1 damage" works under framework FIFO without special routing: the side dispatch from a before-fold trigger lands at queue end, runs as the next command.
- MTG-style stack semantics is expressible via game-state (a zone holding waiting effects) plus `Evaluate` watching that state and dispatching `ResolveTopOfStack` follow-ups. No runtime queue customization needed.
- Earlier candidate runtime queue-ops (`Enqueue` / `GetNext` / `EnqueueReaction` / `EnqueueReplacement`) are rejected. Runtime contract sheds these.
- ADR-0001's reference to `IIntakeQueue` becomes stale; the queue is engine-private FIFO with no runtime service.
- ADR-0002's "zero game-affecting state" wording refreshes to "zero **gameplay** state" — pending list is engine plumbing, not gameplay memory.
- The substantive open design surface is the effect pipeline shape — what does "decide when an effect starts" look like as a contract. Carried as `oq-effect-pipeline`.
- `oq-intake-queue` and `oq-stash-release` resolve by deletion (answered by the reframe — runtime does not own command queue; stashing is effect-level, not command-level).
