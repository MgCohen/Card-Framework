---
type: session
status: completed
last-updated: 2026-05-01
date: 2026-04-30
tags: [#session]
---

# Session 15 — 2026-04-30

## Part 1 — Content (cap ~50 lines)

### 🎯 Active question coming in

**§6 boundary redesign in `features/effects/models/04-effect-flow.md`.** `IIntentMapper`-branches-on-snapshot-scope flagged wrong-batch in S13. Settle alternative shape and patch §6 + downstream mirrors.

### Work done

- Design pass only — **no doc deltas this session** (zero Models/ADRs touched). Patches deferred to a fresh session per "design unit ≠ patch unit."
- Walked 8 design questions (Q1–Q8) one-by-one, options + lean + verdict.
- Architectural reframe over the original §6 sketch: input layer is decision-unaware; mapping is state-driven for normal reasons (game phase, narrowing); decisions are just one source of `AvailableActions` narrowing.
- Locked the `ICommandAwaiter` final shape after a misstep where I over-rotated on a critique and proposed dropping the id check entirely. User course-corrected; we landed on type-only routing with no id and no marker interface.
- `debt: none`

### Decisions settled

- **Q1 = A** — `RequestTargetAction` stays as a real `GameAction`. Effects always `scope.Dispatch`; the request envelope captures both question and answer.
- **Q2 = type-only** — no `AwaitingFor` id, no `IAwaitResponse` marker. Routing is `cmd is T` pattern matching alone. Single-open per type-hierarchy is a runtime assert.
- **Q3 = C** — polymorphism via assignability; `Wait<TBase>` matches concrete subtypes through pattern matching. Game owns the hierarchy.
- **Q4 = A** — matched commands short-circuit; no envelope, no handler, no triggers for the response. One logical event = one envelope (the request's).
- **Q5 = B** — non-matches fall through to the normal pipeline; existing five-validation-layers gate them via `AvailableActions`. Awaiter only siphons matches.
- **Q6 = A** — drop `Scope` discriminator entirely. Snapshot is `AvailableActionsSnapshot { Templates }`. Anyone needing "is an await open?" asks the awaiter.
- **Q7 = A** — engine-level pre-pipeline awaiter check. Awaiter sibling to pipeline, not nested.
- **Q8 = A + B** — cancel-as-command for the player path (game declares cancel as part of the awaitable hierarchy); `CancellationToken` for engine-internal unwinds (game ends, scope.Cancel).
- **Architectural deletes:** `DecisionResponseCommand` · `DecisionResponseHandler` · `DecisionPrompt(CommandId)` snapshot scope · `IDecisionResolver` (renamed/reshaped to `ICommandAwaiter`) · mapper-side scope branching. ADR-0006 + ADR-0007 unchanged (this restores them).
- **Final `ICommandAwaiter` surface:** `Task<TCommand> Wait<TCommand>(CancellationToken ct = default) where TCommand : GameCommand` + `bool TryResolve(GameCommand cmd)`. Impl: closure-captured `cmd is T` pattern matching.

### 🎯 Active question going out

Patch §6 of `04-effect-flow.md` and cascade. Open one alignment-debt entry per cascade target and discharge in the same session per §5.5: `04-effect-flow §4.5 / §5 / §6 / §10 / §11`, `03-snapshot-pipeline §4 / §7 / §9`, `01-command-pipeline §3`, `02-availability-snapshot`, `01-presentation-loop`, `pile-counter` mapper constructor, `OPEN-QUESTIONS` snapshot-scope-as-FSM reframe.

## Part 2 — Links & open threads (cap ~50 lines)

### Followups created

- **`/builder:design-pass` skill** — captured in `BACKLOG.md`. Pattern: Q-by-Q with options + lean + verdict, advance once locked. Session 15 is the canonical example.
- **Multi-actor / multi-open awaits.** Today single-open is a runtime assert in the awaiter. With type-only routing, structurally tractable for distinct types. Pin when an async-decision-heavy game arrives.

### Ephemeral questions (scratch, not promoted)

- X (shared id) vs Y (`IAwaitResponse` marker) — both rejected. X breaks command-id uniqueness; Y forces a class-level commitment to "always a response" that doesn't hold semantically. Settled by dropping the id entirely.
- Reuse `OriginCommandId` for await routing — rejected. Wrong semantics (intent-chain-ancestor vs response-to); would require decision-aware gateway.
- W (id on awaitable, dropped from command) — superseded. We went one step further: drop the id everywhere; type alone is the routing key.
- `IsAssignableFrom` vs pattern matching — pattern matching wins (closure-captured `cmd is T` per registration; JIT can specialise; idiomatic).

### References

- `features/effects/models/04-effect-flow.md` §4.5–§6, §10–§11 — primary patch target
- `features/engine/models/03-snapshot-pipeline.md` §4, §7, §9 — Scope drop, handler example update
- `features/actions/models/01-command-pipeline.md` §3 — gateway stays decision-unaware
- `features/actions/models/02-availability-snapshot.md` — snapshot shape (Scope removal)
- `features/actions/decisions/0006-player-intent-mapper.md` — restored, not mutated
- `features/actions/decisions/0007-five-validation-layers.md` — restored, not mutated
- `features/presentation/models/01-presentation-loop.md` — boundary prose
- `support/samples/pile-counter.md` — mapper constructor cleanup
- Related sessions: 13 (effect-flow Model + scope-branch flagged), 14 (alignment-debt drained), 15 (this session)
