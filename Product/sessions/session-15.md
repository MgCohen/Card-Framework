---
type: session
status: in-progress
last-updated: 2026-05-01
date: 2026-04-30
tags: [#session]
---

# Session 15 — 2026-04-30

## Part 1 — Content (cap ~50 lines)

### 🎯 Active question coming in

**§6 boundary redesign in `features/effects/models/04-effect-flow.md`.** The current §6 sketch has `IIntentMapper` branching on snapshot scope to pick between `MapFreeInput` and `DecisionResponseCommand`. User flagged this wrong-batch in session 13: the routing concern likely belongs to a different layer than the mapper, but no counter-shape was pinned. Settle the alternative shape and patch §6 (with cascade-aware edits to anywhere downstream that mirrors the current sketch).

### Work done

- **First proposal: gateway-routes.** Move scope-branch from mapper to gateway; keep `DecisionResponseCommand`. Mapper restored to ADR-0006 vanilla.
- **User reframed one level up.** The branch shouldn't exist anywhere — input layer should be decision-unaware. Mapping is state-driven for normal reasons; decisions are just one source of `AvailableActions` narrowing.
- **Locked architectural shape.** No `FreeInput`/`DecisionPrompt` distinction at the input layer. `DecisionResponseCommand` and `DecisionResponseHandler` deleted from the model. `DecisionPrompt(CommandId)` snapshot scope dropped — snapshot contents speak for themselves. Decision answers are real game commands (`SelectTargetCommand`, `PickOptionCommand`, etc.).
- **Walked Q-by-Q design pass.** 8 questions sketched; 3 verdicted; awaiter shape sketched and locked.
- **No doc edits yet** — design pass mid-flight; patches deferred until Q3–Q8 settle.

### Decisions settled

- **Q1 = A** — `RequestTargetAction` stays as a real `GameAction`. Effects always go through `scope.Dispatch`; the request envelope captures both question and answer.
- **Q2 = type + `AwaitingFor` id** — primary routing by command type via `Wait<TCommand>`; `AwaitingFor` id stamped through snapshot template → mapper → command for correctness check and future multi-await disambiguation.
- **Q7 = engine-level pre-pipeline** — engine asks awaiter "consume this?" before routing to the pipeline. Awaiter sits next to the pipeline, not inside.
- **`IDecisionResolver` → `ICommandAwaiter`.** Surface: `Task<TCommand> Wait<TCommand>(CommandId awaitingFor)` (handler-side) + `bool TryResolve(GameCommand cmd)` (engine-side). Resolution flow drafted (handler narrows actions + registers await; pipeline returns pending Task; engine yields; on response, awaiter matches → tcs completes → handler resumes; response command never opens its own envelope).

### 🎯 Active question going out

Continue Q-walk: pending verdict on **X (shared id) vs Y (`IAwaitResponse` marker interface, lean Y)** for how response commands carry the awaitingFor key → Q3 polymorphism on `Wait<T>` (lean assignability) → Q4 replace vs alongside (lean replace) → Q5 non-match handling (lean normal-pipeline) → Q6 confirm dropping snapshot Scope (lean drop) → Q8 cancellation of open await (lean cancel-as-command). Then patch §6 + cascade.

## Part 2 — Links & open threads (cap ~50 lines)

### Followups created

- **`/builder:design-pass` skill** — captured in `BACKLOG.md`. Pattern: present design Qs one at a time with options + lean, wait for verdict, advance. This session is the canonical example.
- **Multi-actor / multi-open awaits.** Today single-open is behavioural (engine pauses during effects). With id-based routing it's structurally tractable. Worth pinning when an async-decision-heavy game arrives.

### Ephemeral questions (scratch, not promoted)

- Whether the awaiter should also support timeouts. Out of scope; handler-level concern if ever needed.
- Whether `RequestTargetAction` should collapse to an effect-internal service call (no envelope). Considered and rejected (Q1 = A).
- Whether `DecisionPrompt(CommandId)` should survive as an informational flag for UI/debug. Considered; lean drop entirely — UI infers from snapshot contents; rule asks awaiter directly if needed.

### References

- `features/effects/models/04-effect-flow.md` §6 — target of redesign
- `features/effects/models/04-effect-flow.md` §4.5 — today's `IDecisionResolver`, to be replaced by `ICommandAwaiter`
- `features/actions/decisions/0006-player-intent-mapper.md` — mapper contract; redesign restores its chosen shape (no scope branch)
- `features/actions/models/01-command-pipeline.md` §3 — gateway location; pre-pipeline awaiter check lands here
- `features/engine/models/03-snapshot-pipeline.md` §4, §7 — snapshot scope discriminator (to drop), effect-side handler example (to update)
- `features/presentation/models/01-presentation-loop.md` — IntentMapper boundary prose; cascade target
- `support/samples/pile-counter.md` — `PileCounterIntentMapper(IStateScope)` constructor; loses state injection under new mapper contract
- Related sessions: 13 (effect-flow Model + scope-branch flagged), 14 (alignment-debt drained, scaffold cascade)
