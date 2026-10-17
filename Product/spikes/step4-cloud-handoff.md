---
type: handoff
status: in-progress
created: 2026-05-20
references:
  - Product/spikes/archive/wire-refoundation-roadmap.md
  - Product/spikes/definition-service-pattern.md
  - Product/spikes/step4-service-phases.md
  - Product/spikes/step3-entity-phases.md
tags: [#handoff, #cloud, #step4, #services]
---

# Step 4 — Cloud session handoff

> **Purpose.** Mid-design checkpoint for continuing planning in a cloud
> Claude session. Captures what we've decided so far, what's still open,
> and what's pending verdict before implementation begins. Delete this
> file once step 4 ships.

> **2026-05-20 update — Q1 verdict landed.** The handler-based pattern
> as originally drafted was rejected; redesign walked through `Network`
> as a dumb pipe, `EventStream` as the dispatch surface, services emit
> via `IEventEmitter`, and per-type Apply handler classes register
> directly on `EventStream`. Locked as L8/L9/L10 in
> [`definition-service-pattern.md`](definition-service-pattern.md).
> Remaining Qs (Q2/Q3/Q4/Q6/Q7) lock during their phase, not blockers.
> Everything below this banner reflects the pre-verdict state and is
> kept for context.

## Where we are

Step 3 (entity layer) shipped completely. We started step 4 (service
pattern) on 2026-05-20 and worked through a design walk that reframed
the original "port slice services" framing into a **scaffold-first +
handler-based event command pattern** design.

Two artifacts live on this branch (`claude/step4-service-pattern-docs`):

- [`Product/spikes/definition-service-pattern.md`](definition-service-pattern.md)
  — design doc with locked decisions (L1–L8) and remaining open Qs.
- [`Product/spikes/step4-service-phases.md`](step4-service-phases.md)
  — phase plan 4a–4d.

**Status:** `status: in-design` on both. Final user verdict pending on
the handler-based pattern (L8) before any code lands.

## The journey, briefly

The conversation went through three meaningful reframes before settling
on the current direction:

**Reframe 1 — Step 4 is generalization, not port.** Original framing
was "translate slice's Command + Effect + Trigger into framework asmdefs."
We narrowed step 4 to **pattern + one worked example (EntityService)**;
slice's specific services move to step 5+.

**Reframe 2 — Scaffold first, not Q-walk first.** Rather than locking
abstract pattern Qs in a doc, **build the substrate first** and let
pattern conventions emerge from the code. That brought in:

- VContainer + Scaffold.Appflow as the DI substrate
- `Framework.Engine` (pure C#) + `Framework.Bootstrap` (Unity-coupled)
  split asmdefs
- A sandbox scene as the visible milestone (Play button → IMGUI shows
  "Engine bootstrapped")

**Reframe 3 — Handler-based event command pattern (the architectural lock).**
User pushed back on multiple iterations of "EntityService holds wire ref"
designs (asymmetric `EntityService`+`EntityMirror`, orchestrator + sub-services).
Each had issues: two state owners, awkward preview/rollback ownership,
switch-case duplication across emit/receive paths, EntityService event
surface bloat.

Resolution: **services are side-agnostic; each wire event type has
handler classes; per-side behavior comes from which handlers a scope's
layers register**. Two flavors of handler: `Emit*` (server, subscribe to
domain events, write to EventStream) and `Apply*` (client, implement
`IWireEventHandler<TEvent>`, mutate via service methods). A
`WireEventDispatcher` routes events by runtime type.

See [`definition-service-pattern.md`](definition-service-pattern.md)
§L8 for the locked design and concrete sketches.

## Locked decisions (L1–L8 in the definition doc)

| | What |
|---|---|
| **L1** | Convention only — no `IService` interface or base class |
| **L2** | Services are **side-agnostic**; emission lives in handlers (revised from earlier "service holds wire ref") |
| **L3** | DI substrate = VContainer + Scaffold.Appflow |
| **L4** | Split asmdefs: `Framework.Engine` (pure C#) + `Framework.Bootstrap` (Unity-coupled) |
| **L5** | Step 4 ships scaffold + EntityService example (4a–4d) |
| **L6** | Single `EntityService` class both sides; per-side via layer registration only |
| **L7** | No `World` substrate — its pieces split across services that need them |
| **L8** | Handler-based event command pattern with `WireEventDispatcher` + `IWireEventHandler<T>` |

## Open questions (pending walk before 4a kicks off)

### Q1 — Final direction confirmation

**Status: pending user verdict.** The handler-based pattern in L8.
Specifically:

1. `WireEventDispatcher` + `IWireEventHandler<TEvent>` infrastructure in
   `Framework.Wire`.
2. Emit / Apply asymmetry (separate concepts; only Apply has the marker
   interface).
3. Single `EntityService` class both sides; per-side via layer
   registration only.

This is **the** decision to make before any code lands. Everything else
follows.

### Q2 — Sandbox scene: one root or two scopes?

For 4b's "server mutates → client mirrors" demo:

- **A** — One scene, one `AppFlowRoot`; keyed registration distinguishes
  server vs client. Simplest. **My lean.**
- **B** — Two `AppFlowRoot`s; shared `Wire` via parent. Cleaner separation.

Lock during 4b's sandbox-scene work, not before.

### Q3 — `GameEngine`'s actual surface

- **A** — Empty marker class. **My lean.** Services resolved from
  VContainer.
- **B** — Service-bag (`engine.Get<T>()`).
- **C** — Composition-shaped properties.

Lock during 4a's `Framework.Engine` work.

### Q4 — `GameEngineBuilder` needed in step 4?

- **Lean: no.** Tests construct services directly; production uses
  `AppFlowRoot`. Defer until a non-Unity consumer appears.

Lock during 4a's `Framework.Engine` work.

### Q5 — EntityService specifics (mostly locked)

| Sub-Q | Status |
|---|---|
| Naming `EntityService` (rename from `EntityStore`) | Locked |
| Auto-emission seam (per L8) | Locked |
| Add `TagChangeEvent<T>` to Framework.Tags | Locked |
| Indexed queries (`With` / `Where`) | Defer |
| `Destroy` + `EntityDestroyedEvent` | Defer |

### Q6 — Per-T closed-type registration ergonomics

`Layer.Install` enumerates closed tag-value types
(`EmitTagSet<int>`, `EmitTagSet<bool>`, …). For step 4: ~6 lines per
side. As tag types grow this becomes friction.

- **Lean: accept manual enumeration for step 4.** Future: source-gen
  picks up registered tag types and emits closed registrations. Tracked
  as `oq-handler-codegen`.

## What we need to discuss in cloud

### Primary

1. **Final verdict on L8.** Does the handler-based command pattern feel
   right architecturally? Specifically:
   - Is the `Emit*` / `Apply*` asymmetry honest about what's happening,
     or does it create surface bloat (~6 small classes per service)?
   - Is `WireEventDispatcher` the right placement? Lives in
     `Framework.Wire` since it deals with `WireEvent` + `EventStream`.
   - Should Apply handlers subscribe through the dispatcher
     (`stream.OnAppended` → router → handler) or directly via
     `Network.Register<T>().OnReceived`? Trade-offs:
     - Dispatcher route: handlers see the event already-logged on
       `EventStream` (consistent with cross-cutting handlers like
       logging, replay).
     - Network direct: typed channels per event type; no central
       dispatcher needed; but ties handlers to network rather than
       event log.
   - Current draft uses dispatcher route. Confirm.

2. **Server-side cross-cutting handlers.** L8 implies the dispatcher
   could fire server-side handlers too (e.g. `ServerLoggingHandler` that
   subscribes to `EventStream.OnAppended` for every emission). Is this
   pattern useful enough to call out as a feature, or just an emergent
   property?

3. **The "preview/rollback" future shape.** User flagged this as the
   key concern that killed the asymmetric `EntityService`+`EntityMirror`
   design. The current handler pattern is friendly to it (Emit handlers
   can be replaced/wrapped with buffering versions), but the design
   isn't fleshed out. Should we sketch it now (low cost) or defer
   entirely (don't anticipate)?

### Secondary

4. **Sandbox scene shape (Q2).** One root + keyed registration vs. two
   roots. Lower-stakes; can be locked when 4b starts.

5. **Per-T enumeration friction (Q6).** Is ~12 lines of registration
   across two layers tolerable for step 4? Source-gen plan for later?

6. **Handler registration mechanism in `Framework.Wire`.** The current
   sketch uses a `WireHandlerInstaller<TEvent> : IAsyncInitializable`
   bridge to register each closed-type handler with the dispatcher. Is
   this the right plumbing pattern, or is `IPostBuildCallback` (a
   VContainer hook) cleaner?

### Tertiary (won't block step 4)

7. **Naming.** `EmitEntityCreated` vs `EmitEntityCreatedHandler` vs
   `EntityCreatedEmitter`. Minor; bike-shed during 4b.

8. **Folder layout.** Handlers under `Framework.Entities/Runtime/Wire/`
   vs scattered. Lean is colocate under `Wire/`.

## What was done

Three doc commits on this branch:

1. **Initial draft** (`0bd7380` or near it) — first pass at
   `definition-service-pattern.md` + `step4-service-phases.md` with the
   pattern-first methodology and 10 open Qs.
2. **Scaffold-first rewrite** — flipped methodology; locked VContainer +
   Appflow + split asmdefs.
3. **Handler-pattern rewrite** — locked L8 after Q1 walk; trimmed Qs to
   6 remaining.

(This handoff doc is the next commit.)

No code shipped this session. Step 4 design lives entirely in docs.

## What's next

**To continue planning in cloud:**

1. Read [`definition-service-pattern.md`](definition-service-pattern.md)
   — full design, locks, remaining Qs.
2. Read [`step4-service-phases.md`](step4-service-phases.md) — phase
   plan and what each phase ships.
3. Decide on the open questions above. The big one is **Q1 (L8
   verdict)**; once that locks, the rest are smaller calls that can fire
   during their respective phases.

**To start implementing (after cloud planning lands verdicts):**

1. Sync the verdicts back into `definition-service-pattern.md` (turn
   open Qs into more locked Ls, or amend existing Ls).
2. Cut a fresh branch from current 3e parent
   (`claude/entity-based-architecture-pMNhU`) — `claude/step4-scaffold`
   or similar.
3. Start phase 4a:
   - Manifest changes (add VContainer + Scaffold.Appflow).
   - `Framework.Engine` asmdef + empty `GameEngine`.
   - `Framework.Bootstrap` asmdef + `FrameworkAppFlowRoot` + `EngineLayer`
     + `BootstrapStatusUI`.
   - `Framework.Wire` additions: `IWireEventHandler<T>` +
     `WireEventDispatcher` + the handler-installer plumbing.
   - Sandbox scene at `Assets/Framework/Bootstrap/Sandbox/`.
   - Verify Play mode: IMGUI shows "Engine bootstrapped".
   - Unity batch tests green.

## Current branch state

```
* claude/step4-service-pattern-docs (HEAD)
  - 0bd7380  docs(spikes): step 4 service-pattern scoping — scaffold-first methodology
  - ca02dc1  docs(spikes): flip graduated spike docs
  - f61a712  chore(entities,tags): post-3e audit cleanups
  ↑ branches from claude/entity-phase-3e at e672a95
```

The handoff doc + L8 doc updates are the next commit (this session).
Push the branch after that. The PR target is `claude/entity-phase-3e`
per the project's branch-and-PR rule (cut from there, PR back there).
