---
type: spike
status: active
created: 2026-06-03
references:
  - Product/spikes/archive/wire-refoundation-roadmap.md
  - Product/spikes/definition-service-pattern.md
  - Product/spikes/request-service.md
  - Product/spikes/state-service.md
  - Product/spikes/wire-block-events.md
sandbox: Assets/Framework/Commands/
tags: [#spike, #commands, #wire, #service, #blocks]
---

# Command Service — server-side intake & orchestration

> **Status: active — design locked, build phased.** This spike captures the
> design walk (2026-06-03) for step 5c of the wire-refoundation. Unlike 5a/5b
> (designed and built in one pass), commands ship **phase by phase** (see
> §Phase plan). The locks L1–L5 below are settled; the build lands
> incrementally, Phase 0 first.
>
> **Scope is deliberately minimal.** This is the *intake + orchestration* layer
> only — validate → execute → emit mutations. Reactions (triggers, SBAs, the
> effect stack) and the engine evaluate-loop are a **separate, later layer**.
> Everything that needs the queue, async, or block-nesting lives there, not here.

## TL;DR

A command is **player/game intent** that the server validates and executes to
author mutations. It is the first framework layer that is *not* a reactive
mirror twin: a command can be **rejected**, it **produces** mutations (by
calling the existing `Tags`/`Entities`/`States` services), and its logic runs
**once, authoritatively, on the server**. The command itself **never crosses
the wire** — only the mutations it emits do, bracketed by a `Block` for
causality. Commands are **pure data**; logic lives in DI-registered
`ICommandHandler<T>`; `CommandService.Execute` is **inline + synchronous** and
returns a thin `CommandResult`.

## Where this slots

Step 5c of [`wire-refoundation-roadmap.md`](archive/wire-refoundation-roadmap.md) — the
front half of "engine reassembly." It builds directly on the shipped reactive
layers and is the first consumer-facing *authoring* surface above them.

**Pre-step-5c landed:**
- The service pattern — Store + Service + per-type apply handler
  ([`definition-service-pattern.md`](definition-service-pattern.md)).
- Tags / Entities / States / Requests services — the mutation alphabet a
  command orchestrates ([`request-service.md`](request-service.md),
  [`state-service.md`](state-service.md)).
- Block bracket wire events (`BlockStartEvent` / `BlockEndEvent` / `BlockKind`
  / `BlockOutcome`) exist in `Framework.Wire` but **nothing emits them yet** —
  commands are the first runtime bracket emitter
  ([`wire-block-events.md`](wire-block-events.md)).

**After this:**
- The **engine evaluate-loop + SBAs** — the `Evaluate`/pump cycle that runs a
  command, then evaluations (state-based actions, state transitions), looping
  to a fixpoint. This is where the **queue and async** (deferred from L3) come
  back, because there's finally something to interleave between commands.
- **Effects / triggers / hooks + the stack** — the reaction machine. This is
  where block **nesting** (L5's parent-stack) and **payload/source capture**
  (L2) light up.

## The central tension (why commands break the mold)

Every shipped layer obeys one law: *a service emits a wire event → a per-type
handler applies it → the **same handler runs on both sides**, so server and
client converge from the identical stream.* The handler is the sole writer.

A command cannot obey that law, and that's the point of it:
- it can be **rejected** (a `TagSet` just happens; "play this card" might be
  illegal);
- it **produces** mutations (it orchestrates calls into the services);
- its orchestration must run **once on the server** — the client must *not*
  recompute "deal 3, check lethal, move to graveyard," it must replay the
  resulting mutations.

So the command is the server-side **author** of mutations; the wire still
carries only the mutation alphabet. The two prior efforts (the VerticalSlice
prototype and the `Product/actions` design research) both landed here — neither
made the command a both-sides-applied wire event. They differed on *logic
location* (prototype: `Command.Apply`; design research: separate handler) and
*wire framing* (prototype: `Block` bracket; design research: `HistoryEnvelope`).
L1–L5 pick the substrate-native combination.

## Locked decisions

### L1 — Commands are server-only; only mutations cross the wire

`Command` is a plain object, **not** a `WireEvent`. `CommandService.Execute`
runs server-side: validate → handler → the handler calls the existing services
→ *their* mutation events hit the wire and mirror through the channels already
wired. The command never serializes; the client reconstructs purely from
mutations and never sees or re-runs the command.

Rejected: command as a `WireEvent` applied on both sides — its orchestration
produces *new* mutations the client can't recompute, so it would need a
divergent client handler, breaking the same-handler-both-sides law.

### L2 — A command brackets its mutations with `Block` events (causal grouping)

`Execute` emits `BlockStart{Kind=Command}` → the handler's mutations → `BlockEnd`.
The `Id`/`ParentId` on the bracket are what let a client say "these three
mutations were one command." This reuses the existing `Framework.Wire` block
alphabet; commands are its first runtime emitter.

**Bracket ships as-is.** The shipped `BlockStartEvent` carries `Id`, `ParentId`,
`Kind`, `SourceId` — causal structure only. The prototype *also* reflected the
command's type name + payload onto the bracket; that enrichment is **deferred**
— it changes nothing about what the client reconstructs (state always comes from
the mutations), it only enriches human/tooling readability. It lights up with
scrub/replay tooling, and `IPackingHandler` is the seam that would convert a
domain ref (e.g. `Entity`) → its `Id` int for the payload then. `SourceId` is
`0` for now (source extraction is in the same deferred bucket).

### L3 — Execution is inline + synchronous; returns `CommandResult`

```csharp
public CommandResult Execute(Command cmd);   // server-only, fully resolved on return

public readonly struct CommandResult          // data-only
{
    public bool   Accepted;
    public string RejectReason;               // null when accepted
}
```

No queue, no `Task`. **Both prior efforts used a queue + async — but only
because of reactions:** the prototype's `Queue<Command>` + `RunPump` existed so
SBAs/the stack could resolve *between* commands; the design research's
`Task<CommandResult> Dispatch` was async so a command could `await` a player
decision mid-flight. Strip reactions and Requests-integration (both later
layers) and every reason for the queue and the `Task` evaporates — a minimal
command validates, mutates, brackets, and is done.

`CommandResult` (not `void`) is kept deliberately: Phase 1 adds rejection, and
the eventual client-submission path needs accepted-vs-rejected-and-why. It is
also the clean seam the future evaluate-loop threads its outcome through.

### L4 — Command = pure data + DI-registered handler; registry dispatches by type

Logic does **not** live on the command (no `Apply`). The command is data; a
separately-registered `ICommandHandler<T>` holds the logic and **ctor-injects**
the services it orchestrates — exactly the shipped-codebase idiom
(`StatePushHandler`, `TestPickResponseHandler`). `CommandService.Execute`
resolves the handler by `cmd.GetType()` via a `CommandHandlerRegistry`,
mirroring how `HandlerRegistry` dispatches wire events.

```csharp
public abstract class Command { }              // pure data; carries domain refs, not infra/behavior

public interface ICommandHandler<T> where T : Command { void Handle(T cmd); }

public sealed class DealDamageHandler : ICommandHandler<DealDamage>
{
    readonly TagService _tags;                 // infrastructure — injected ONCE at startup
    readonly IReadOnlyTagStore _read;
    public DealDamageHandler(TagService tags, IReadOnlyTagStore read) { _tags = tags; _read = read; }
    public void Handle(DealDamage cmd) =>
        _tags.Set(cmd.Target.Id, Health, _read.Get(cmd.Target.Id, Health) - cmd.Amount);
}
```

**Rationale (the load-bearing reason):** handlers are DI-registered *once*, so
injection cost is paid one time, not per command instance; commands stay pure
data, so no service refs are threaded through every `new DealDamage(...)`; and
the entire service/DI boundary lives in handlers — if injection or the services
change, it touches handlers only, never the command data shapes.

**Commands carry references freely** — `Entity Target`, values, whatever the
caller already holds. The constraint is **infrastructure** (services / stores /
registries) and **behavior** (delegates / logic), which belong in handlers, not
the "data-only-for-the-wire" rule that drove the design research (that
constraint died with L1 — the command never serializes). Holding a *live*
`Entity` ref is safe specifically because `Execute` is inline (L3): there's no
gap between constructing the command and handling it, so the ref can't go stale.
*If* commands are ever queued (reaction era), live-ref-vs-id gets revisited.

Rejected: logic on the command (`Apply` + an `ICommandScope`), the prototype's
shape. In *this* substrate the services already are the authoring surface, so a
command scope would duplicate them; and putting infra on the command reintroduces
the per-instance-injection problem the handler split exists to avoid.

### L5 — Commands carry no id; `BlockScope` (in `Wire`) is the sole block-id allocator

A command needs **no identity**: it is transient and inline — it executes,
brackets, and is done, and nothing later in time refers back to it. (Contrast
`Request`, which *has* an `Id` precisely because it is long-lived, stored, and
correlated to a separate `Response` across time.)

Therefore block-id allocation belongs **entirely** to the block layer. A small
server-only `BlockScope` in `Framework.Wire` owns the monotonic counter and
emits the brackets; Commands — and later Effects/Triggers — all call the same
`BlockScope.Open`. Because allocation is centralized **by construction**, id
conflict across layers is structurally impossible (this is the decisive reason
for a shared `BlockScope` over an inline counter: inline is what *creates* a
second allocator when effects land). `Kind` cannot carry identity — many blocks
share `Kind=Command` — so it is a tooling discriminator, not a substitute for
the unique `Id`.

```csharp
namespace Framework.Wire
{
    public sealed class BlockScope                 // server-only; the bracket emitter
    {
        readonly IEventEmitter _events;
        int _nextId = 1;
        public BlockScope(IEventEmitter events) => _events = events;

        public int Open(BlockKind kind, int sourceId = 0)
        {
            var id = _nextId++;
            _events.Emit(new BlockStartEvent { Id = id, ParentId = 0, Kind = (byte)kind, SourceId = sourceId });
            return id;                             // ParentId = 0: no nesting until reactions
        }

        public void Close(int id, BlockOutcome outcome = BlockOutcome.Completed)
            => _events.Emit(new BlockEndEvent { Id = id, Outcome = (byte)outcome });
    }
}
```

The id is a **local token** threaded `Open → Close` inside `Execute`; it never
enters the command/handler domain and the `Command` has no `Id` field. The
open-block **parent-stack** (for `ParentId` nesting) is **not built** — it
lights up only when effects run inside commands (reaction era). Revisit only if
a command ever needs identity.

## Concrete shape (Phase 0 target)

```csharp
// Framework.Commands
public abstract class Command { }                      // pure data, server-only

public interface ICommandHandler<T> where T : Command { void Handle(T cmd); }

public sealed class CommandHandlerRegistry { /* type → ICommandHandler, DI-collected like HandlerRegistry */ }

public sealed class CommandService                     // server-only
{
    readonly CommandHandlerRegistry _handlers;
    readonly BlockScope _blocks;
    public CommandService(CommandHandlerRegistry handlers, BlockScope blocks) { _handlers = handlers; _blocks = blocks; }

    public CommandResult Execute(Command cmd)
    {
        var id = _blocks.Open(BlockKind.Command);
        try     { _handlers.HandlerFor(cmd.GetType()).Handle(cmd); }
        catch   { _blocks.Close(id, BlockOutcome.Errored); throw; }
        _blocks.Close(id, BlockOutcome.Completed);
        return new CommandResult { Accepted = true };  // validation enters Phase 1
    }
}
```

`GameEngine.Commands` (server-only) is the discoverable handle; `CommandLayer`
registers `CommandService` + `CommandHandlerRegistry` server-only and
`BlockScope` where it belongs in `Wire`, matching the role-gating of
`RequestLayer` / `StateLayer`. **No store, no scope, no validators (Phase 0).**

## Phase plan

Each phase lands green (headless batch run) before the next.

| Phase | Ships | Proof | Resolves |
|---|---|---|---|
| **0 — Walking skeleton** ✅ **built (356/356 green)** | `Framework.Commands` asmdef; `Command`; `CommandService.Execute`; `ICommandHandler` + `CommandHandler<T>` + `CommandHandlerRegistry`; `BlockScope` in `Wire`; `GameEngine.Commands`; `CommandLayer`. One fixture command → one mutation. | A fixture command runs end-to-end: `Execute` → `BlockStart` → handler → one mutation → `BlockEnd`. Layer resolves, role-gated. | L1–L5 |
| **1 — Validation** ✅ **built (358/358 green)** | `ICommandValidator` + `CommandValidator<T>` + `CommandValidatorRegistry`; opt-in per command type (no validator → accept). **No system gate** — that belongs in the engine, not here. | Illegal command (negative health) → **zero** mutations, **zero** wire traffic, state untouched; legal unchanged. Woven into both integration spines. | F4 (validation layers) |
| **2 — Stateful validation** ✅ **built (359/359 green)** | `DealDamage` fixture: a read-modify-write command whose validator **ctor-injects `TagService`** and rejects on world state (`NotDamageable` when the target has no Health tag), not just payload. Locks the validator-with-a-dependency shape Phase 1's payload-only validator never exercised. | Stateful reject → zero wire traffic; accept → handler reads current health and writes `health-amount` (30 → 18), read back through the apply-loop. Woven into both spines + a unit reject test; the injected validator resolves through DI in `BootstrapWiring`. | extends F4 (validation reads state) |
| **3 — Real commands** | Fill the bodies: multi-service orchestration in one `Execute` (Entities + Tags under one block); real reject logic. | Multi-mutation atomicity + ordering on the wire from one `Execute`. | confirms L4 handler-scope under real load |

Seam-stubs for the *next* layer (reactions, client-submission, block-nesting)
are **not** a phase — they're logged as deferred items below as they surface.

### Phase 0 build notes (2026-06-03)

Two refinements surfaced while building against the locks — both faithful to the
design, both chosen to match shipped-codebase idiom over the spike's first sketch:

- **Handler shape mirrors `WireEventHandler<T>`, not a bare interface.** L4 sketched
  `ICommandHandler<T>` as the authoring surface, but VContainer can't collect an open
  generic into one list, and the substrate already solves this exact problem one layer
  down: a non-generic `IWireEventHandler` (carrying `Type EventType`) + an abstract
  `WireEventHandler<T>` base that bridges. Commands now copy it verbatim — non-generic
  `ICommandHandler` (`Type CommandType`, `void Handle(Command)`) + abstract
  `CommandHandler<T>` base authors derive from. `CommandHandlerRegistry` collects the
  non-generic interface and dispatches by `CommandType` (one handler per command — duplicate
  registration throws, unlike the wire registry's fan-out list).
- **`CommandResult` uses `Accept()` / `Reject(reason)` factories**, not an object
  initializer. A `readonly struct` with `init`-only members needs `IsExternalInit`, which
  Framework runtime asmdefs don't pull in; static factories keep it a true readonly struct
  with no polyfill dependency.

The `GameEngine` aggregate-ctor change rippled to its two hand-wired / non-`CommandLayer`
construction sites (`EngineStack`, `BootstrapWiringIntegrationTests`); both now supply /
install the command surface. `CommandHandlerRegistry` resolves an **empty** handler list
cleanly in Phase 0 (no runtime commands ship yet) — VContainer's single-element-collection
fallback returns an empty collection rather than throwing.

### Phase 1 build notes (2026-06-04)

Validation is the **opt-in single validator** shape (design-pass walk, 2026-06-04),
deliberately narrower than the settled `Product/actions` reference (ADR-0010 +
`features/actions/models/06-command-validator.md`). The reference is a *fan-out
list* — every validator registered for a command's runtime type runs in
registration order, short-circuit on first reject — with framework "gates"
(`SessionGate`, `MatchStateActionGate`) registered against the **base** command
type so they fire for everything. We took its **opt-in** core and nothing else:

- **One validator per command, not a list.** `CommandValidatorRegistry` is a
  verbatim copy of `CommandHandlerRegistry` (dict keyed by `CommandType`,
  duplicate throws) — its **sole** divergence is `TryFor` (absent → accept)
  instead of throw-on-miss, because validation is opt-in where handling is
  mandatory. The fan-out list + base-type matching are deferred until a second
  validator on one command actually earns them (the reference itself flags
  base-type resolution as unconfirmed).
- **No `ICommandGate`.** The cross-cutting system gate (is-match-playing,
  is-it-your-turn) is **not** in the commands layer — it reads engine-owned
  state and belongs in the engine evaluate-loop. Dropped here entirely rather
  than stubbed.
- **`Validate` runs before `BlockScope.Open`.** A reject returns
  `CommandResult.Reject(reason)` having emitted nothing — the zero-traffic
  invariant, asserted end-to-end (event-count `== 0`) in both the hand-wired
  `EngineStack` flow and the DI `BootstrapWiring` flow, not a standalone test.
- **Registration mirrors handlers 1:1.** `RegisterCommandValidator<T>` →
  `Register().AsImplementedInterfaces()`, manual DI like
  `RegisterCommandHandler<T>`. No reflection — the repo's reflection/source-gen
  placeholder (`WireRegistrar`) is channel-discovery's, a different mechanism.

### Phase 2 build notes (2026-06-04)

Phase 2 was specced as *contract-stubs* — empty-body `MoveCard`/damage shapes,
proof "compiles." Two facts on the ground reframed it (design-pass walk):

- **No game/content asmdef exists.** Every concrete command (`SetHealth`,
  `IntroduceCard`) lives in the **Tests** layers; the framework substrate ships
  zero commands, correctly — commands are game content. So Phase 2 isn't "ship
  game commands," it's "add a richer **fixture** that stresses a substrate shape
  the current fixtures don't."
- **Empty-body stubs would be dead, untested code** — the exact thing the
  "a command has a purpose, it has to be tested and integrated" steer rejects.

So Phase 2 was reframed to the one shape the fixtures hadn't locked: a
**validator that reads world state**. `DealDamageValidator` ctor-injects
`TagService` (the validator analog of what handlers already do) and rejects
`NotDamageable` when the target carries no Health tag — a decision on live state,
not the command payload. `DealDamageHandler` is read-modify-write (`Get` then
`Set value-amount`), vs `SetHealth`'s blind set.

- **The accept + read-modify-write proof is integration-only.** `TagService.Set`
  *emits*; the `TagStore` is written by the apply-handler subscribed to the
  stream. The unit harness (`CommandServiceTests.Build`) wires **no apply-loop**
  (its tests assert on emitted events, never store reads), so a stateful
  validator's accept path can't pass there without seeding the store — an idiom
  the unit suite doesn't use. Kept the unit suite to the **reject** path
  (no tag → reject → empty log, which needs no applied state) and proved
  accept + 30→18 in both integration spines, where the apply-loop is real.
- **Stateful validators resolve through DI unchanged.** `BootstrapWiring`
  registers `DealDamageValidator` with the same `RegisterCommandValidator<T>`;
  VContainer supplies its `TagService` exactly as it does for handlers — no new
  registration mechanism.
- **Multi-service orchestration stays Phase 3.** Damage still touches one service
  (Tags). One `Execute` spanning Entities + Tags under a single block — and the
  wire-ordering/atomicity assertions — is the next phase's distinct proof.

## Test fixture (Phase 0 verification surface)

A trivial concrete command + handler lives in `Framework.Commands.Tests` and
proves the orchestration path (not mirroring — the mutations already mirror via
the Tags/Entities tests). The fixture command calls one existing service; the
test asserts the wire shows `BlockStart{Kind=Command}` → the mutation →
`BlockEnd{Completed}` in stream order, and that `Execute` returns
`Accepted = true`. A handler-throws case asserts `BlockEnd{Errored}` and the
exception propagating.

## Deferred items

| Item | Lights up when |
|---|---|
| **Validation model** — the full four-layer picture (gateway / system-gate / command-specific / before-fold-cancel); Phase 1 shipped **only** command-specific (opt-in). System-gate moved to the engine; gateway is UI-time; before-fold-cancel is reaction-era | command-specific ✅ Phase 1; system-gate → engine evaluate-loop; gateway → presentation; before-fold-cancel → reaction era |
| **Validator fan-out + base-type gates** — multiple validators per command (registration-order, short-circuit) and cross-cutting validators on base command types, per the `Product/actions` reference (ADR-0010) | A second validator on one command, or a genuine cross-cutting check, earns the list |
| **Engine evaluate-loop + queue + async** — `Execute` gets wrapped by the pump; the queue (schedule reactions) and `Task` (await Requests mid-command) return here | The reaction/SBA layer (next step after commands) |
| **Block nesting / parent-stack** — `BlockScope` gains the open-block stack so inner blocks get a real `ParentId` | Effects run inside commands (reaction era) |
| **Block payload + source capture** — reflect command type/args + `SourceId` onto `BlockStart` for scrub/replay readability; `IPackingHandler` converts domain refs → ids | Scrub/replay tooling matures |
| **Client → server submission** — commands are the client→server intent path; today the server drives (like Requests' deferred symmetric path) | First multi-process integration test |
| **Live-ref vs id on commands** — holding `Entity` refs is safe only while `Execute` is inline; queued commands may need ids | Commands get queued (reaction era) |
| **Generic handler base / batch registration** — collapse per-command handler boilerplate | 3+ command types ship in any single game |
| **ADR vs spike-lock pass** — confirm whether any lock (likely the L1/L2 command↔wire foundation) earns a project-wide ADR vs staying a spike lock | End of the design walk, before the foundation is cited by the effects layer |

## When this lands

Phase 0 is scoped to: `Framework.Commands` asmdef (Runtime + Tests);
`Command` / `ICommandHandler<T>` / `CommandHandlerRegistry` / `CommandService` /
`CommandResult`; `BlockScope` in `Framework.Wire`; `GameEngine.Commands` +
`CommandLayer` role-gated registration; one fixture command + handler +
`CommandServiceTests`. Verified via headless EditMode batch run.
