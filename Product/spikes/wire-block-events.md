---
type: spike
status: active
created: 2026-05-20
references:
  - Product/spikes/wire-refoundation-roadmap.md
  - Product/spikes/definition-service-pattern.md
  - Product/spikes/wire-typed-payloads.md
  - Product/spikes/framework-architecture.md
sandbox: Assets/Framework/Wire/Runtime/Events/
tags: [#spike, #wire, #blocks, #events, #causality, #presentation]
---

# Wire Block Events — spike

> **Status: active.** Design walk from 2026-05-20; envelope shape landed
> 2026-05-30 as a pre-step-5 cut. Block + Request event families now
> ship in `Framework.Wire` with the L1–L7 shape; round-trip tests cover
> all six event types. Service emission (who-emits) remains deferred to
> step 5 — services will adopt these events when they port.

## Where this slots

Sits between step 4 (service pattern) and step 5 (existing services reviewed)
in [`wire-refoundation-roadmap.md`](wire-refoundation-roadmap.md). Step 5
ports CommandService / EffectService / TriggerService / RequestService /
StateService onto the handler-based service pattern from
[`definition-service-pattern.md`](definition-service-pattern.md); those
services emit block events as part of their dispatch. The wire shape
must be settled before the services can port.

The slice's reference shape lives in
`Assets/Spike/ECS/VerticalSlice/Engine/Stream/{WireEvent.cs, BlockKind.cs,
BlockOutcome.cs, OptionEvents.cs}` — this spike updates it for the
AutoPacker world and splits Request out of `BlockKind` into its own
event family.

## Core insight

**Blocks are causal envelopes for presentation, not payload carriers.**
State mutation lives in the leaves (`TagSet` / `TagChange` / `EntityCreated`).
Leaves apply and unapply cleanly regardless of whether they're inside a
block. Blocks exist so downstream consumers — primarily presentation,
secondarily tooling and replay — can group leaves into causality chains
("this damage was caused by this effect was caused by this command"),
route animations by source, and sync timing.

**What blocks don't do:**
- Carry mutation parameters (leaves carry them).
- Drive state reconstruction (mirror walks leaves; envelopes are
  metadata, not mutations).
- Drive rollback (Unapply is leaf-level).

**What blocks do:**
- Identify a causal scope (`Id`).
- Identify the cause's origin (`Kind`, `SourceId`).
- Thread causality across scopes (`ParentId`).
- Mark the scope's end (`Outcome`).

This is the lens that collapses most of the slice's `BlockStartEvent`
fields: `Name` (string), `Payload` (reflection dict), per-Kind typed
subclasses — all gone. The leaves are the truth; the envelope is just
the story.

## Block vs Request: parallel bracket families

The slice routed both "cause + mutations" and "choice + options" through
one `BlockKind` enum (`Effect` / `Command` / `Request`). The semantic
shapes are different enough that conflating them was a corner-cut:

- **Block** = "a cause's mutations are bracketed here." Inside: mutation
  leaves. Same uniform metadata across Kinds. End carries `Outcome` (did
  the active phase complete / error / cancel).
- **Request** = "a choice is declared, awaiting answer." Inside:
  option-declaration leaves. Different metadata (`ResponderId`,
  `OptionCount`, `IsCancellable`). End carries no outcome — the envelope
  wraps a declaration, not an active phase; the answer arrives as a
  separate `ResponseEvent` leaf downstream.

They share *structural* shape (bracket around leaves) but have different
*semantic* shape. Splitting them keeps both clean and leaves the
alphabet open for further envelope families if a future concept doesn't
fit either pattern.

## Locked decisions

### L1 — Blocks are causal envelopes for presentation, not payload carriers

State truth lives in leaves; envelopes bracket them for downstream
consumers. No reflection-payload, no `Name` string, no per-Kind typed
subclasses. Presentation derives "what's about to happen" from
`SourceId` + `Kind` (look up Source's `DefinitionId` in the catalog).

### L2 — Block and Request are parallel bracket families

Two separate event families on the wire alphabet, each with their own
opener/closer pair. Forcing them into one envelope means
`BlockStartEvent` accumulates per-Kind optional fields (`ResponderId`
only meaningful when `Kind = Request`, etc.). Splitting them avoids
that.

### L3 — Shared `int` Id space; per-session monotonic; 0 reserved

One monotonic counter shared between Block and Request families
(Requests can nest inside Block envelopes and vice versa; ParentId
crosses families). Width: `int` (matches `EntityId`; 2B max is far past
any realistic match). Per-session reset; **a session is one match**, so
the counter starts at 1 each match. `0` is the "unset / no parent"
sentinel — never allocated as a real Id.

### L4 — Explicit `ParentId` on opener events

Required for the reaction stack-sibling pattern (slice's P22): reactions
fire as top-level wire blocks but causally point back at a
non-currently-open block. Implicit-by-stack threading would conflate
"nested" with "reactive." `ParentId = 0` means top-level (no parent).

### L5 — `BlockKind` is a closed enum, three values for v1

```csharp
public enum BlockKind : byte
{
    None    = 0,  // unset sentinel; catch-bug-early
    Command = 1,  // CommandService.Execute opens
    Effect  = 2,  // EffectService.Run / Stack.Resolve opens
    Trigger = 3,  // TriggerService.Fire opens (reserved for step 5+)
}
```

Kinds correspond to dispatch sites in the engine; games author content
that flows *through* sites, they don't add new sites. Closed enum is
correct.

**Folded elsewhere:**
- **System** actions → `Command` with a synthetic `SourceId` (world
  entity or 0).
- **SBA** → emits Commands, which become Command blocks. No distinct
  Kind.
- **StateTransition** → `StatePushEvent` is a single non-bracketed event
  per [`state-service.md`](state-service.md). Not a block.

### L6 — `BlockEnd.Outcome` enum; Request close is plain

```csharp
public enum BlockOutcome : byte
{
    None      = 0,
    Completed = 1,
    Errored   = 2,
    Cancelled = 3,
}
```

`BlockEnd` carries `Outcome` because blocks bracket active happenings.
`RequestClosed` carries only `Id` because the envelope wraps a
declaration. The actual outcome of a request arrives in the separate
`ResponseEvent` (and a player cancellation is encoded as picking the
`CancelOption` leaf, not as a close-outcome).

### L7 — Mutation leaves shared; option leaves Request-only

Mutation leaves (`EntityCreatedEvent`, `EntityDestroyedEvent`,
`TagSetEvent<T>`, `TagChangeEvent<T>`, `TagRemovedEvent`) can appear
inside Block envelopes or at top level. Option leaves (`PickOptionEvent`,
`CancelOptionEvent`) appear only inside Request envelopes. `ResponseEvent`
is a sibling top-level event, not bracketed.

## Concrete shape

```csharp
// === Block family ===

public partial class BlockStartEvent : WireEvent<BlockStartEvent.Packed>
{
    [Packed] public int  Id;
    [Packed] public int  ParentId;   // 0 = top-level
    [Packed] public byte Kind;       // BlockKind
    [Packed] public int  SourceId;   // entity that opened the block; 0 if none
}

public partial class BlockEndEvent : WireEvent<BlockEndEvent.Packed>
{
    [Packed] public int  Id;
    [Packed] public byte Outcome;    // BlockOutcome
}

// === Request family ===

public partial class RequestOpenedEvent : WireEvent<RequestOpenedEvent.Packed>
{
    [Packed] public int  Id;
    [Packed] public int  ParentId;
    [Packed] public int  SourceId;
    [Packed] public int  ResponderId;
    [Packed] public byte OptionCount;
    [Packed] public bool IsCancellable;
}

public partial class RequestClosedEvent : WireEvent<RequestClosedEvent.Packed>
{
    [Packed] public int Id;          // no Outcome — see L6
}

// Response: sibling top-level event, emitted after the envelope closes
public partial class ResponseEvent : WireEvent<ResponseEvent.Packed>
{
    [Packed] public int  RequestId;
    [Packed] public byte Picked;     // index into the declared options
}

// === Option leaves (shape deferred — appear only inside Request envelopes) ===
//   PickOptionEvent { Index, SourceId?, TargetId?, Value? }
//   CancelOptionEvent { Index }
// Field shapes TBD when Request ships in step 5+; nullable encoding under
// AutoPacker is the open detail.
```

## Wire grammar example

A Tactician-style Choose-One spell from the slice, in the new shape:

```
BlockStart  { Id=1, Parent=0, Kind=Command, Source=#7 (Tactician card) }
  EntityCreated(Tactician spell in limbo)
  TagChange(#7, Zone, Hand→Limbo)
  BlockStart { Id=2, Parent=1, Kind=Effect, Source=#7 }
    RequestOpened { Id=3, Parent=2, Source=#7, Responder=#playerA, OptionCount=3, Cancellable=true }
      PickOption(idx=0, Value=6)          // Deal 6 damage
      PickOption(idx=1, Value=8)          // Heal 8
      CancelOption(idx=2)                  // Cancel
    RequestClosed { Id=3 }
  BlockEnd { Id=2, Outcome=Completed }
BlockEnd { Id=1, Outcome=Completed }
... time passes, player thinks ...
Response { RequestId=3, Picked=0 }
... resolution of Picked=0 runs as a top-level Command block with ParentId=2 ...
BlockStart { Id=4, Parent=2, Kind=Effect, Source=#7 }
  TagChange(#enemy, Health, 10→4)
BlockEnd { Id=4, Outcome=Completed }
```

Note ParentId=2 on Block#4 — the resolution effect threads back to the
original Effect block even though Block#2 has long since closed. That's
the slice's P22 stack-sibling causality, expressed naturally.

## Mirror behavior

Mirror walks the wire and applies/unapplies mutation leaves into entity
state. Envelopes are skipped for state reconstruction — they're
metadata.

Where envelopes matter for mirror:
- **Scrub boundaries.** Scrubbing to "before this cast" finds the
  `BlockStart` and rewinds all leaves between it and the cursor.
  Cheaper than scrubbing leaf-by-leaf.
- **Block-level undo (future).** Undoing "the whole effect" walks from
  `BlockEnd` back through the leaves to its matching `BlockStart`. Same
  primitive.

Request envelopes have no mirror state. They're presentation-only.

## Presentation behavior

The downstream consumer that justifies blocks existing. Presentation:

- Routes animations on `BlockStartEvent` by `Kind` + `SourceId` → look
  up Source's `DefinitionId` in the catalog → resolve to the authored
  animation set.
- Groups leaves between Start and End as a visual unit; staggers or
  syncs them as a sequence.
- Reads `ParentId` to chain causally ("this damage animation plays
  after the spell cast resolves").
- Handles Request envelopes specifically: open dialog on
  `RequestOpenedEvent`, render options from the inside leaves, await
  player input, emit `Response` back through the command pipeline.

## Open questions

### Q5 — Who emits the block events: service-direct or Emit handler?

The wire shape locked above doesn't change either way; the question is
which side of the step-4 handler boundary owns the call to
`stream.Add(new BlockStartEvent { ... })`.

- **Service-direct.** `CommandService.Execute` opens the block as part
  of dispatch. The service knows about wire events.
- **Emit handler (per L8 of definition-service-pattern.md).**
  `CommandService` raises an internal `OnBlockOpened` domain event; an
  `EmitBlockStart` handler subscribes and writes to the stream. Matches
  the pattern used for entity events.

**Defer to step 5+.** Belongs in whichever session ports
CommandService. The wire shape here works for both choices.

### Animation pre-roll timing

Hearthstone-style pre-roll plays a cast visual *before* the leaves
land (projectile flying, then damage number pops). Two options:

- **Pre-roll off `Source + Kind`.** Client looks up Fireball-card's
  effect animation when `BlockStart(Kind=Effect, Source=#fireball)`
  arrives; pre-rolls; leaves arrive after with the numbers.
- **Chase leaves.** Animations track wire events as they arrive; no
  pre-roll.

Wire shape doesn't change either way. Lock when views land (step 7).

### Match-end / disconnect with an open Request

If the match ends mid-Request, the Request never gets a Response.
Encode as:
- Synthetic Response (engine auto-picks CancelOption on cleanup), or
- Separate `RequestAbandonedEvent` leaf, or
- Match-end is its own envelope that implicitly closes everything.

Defer to step 5+ when Request ships.

## Deferred items

| Item | Lights up when |
|---|---|
| Who emits block events (service vs Emit handler) | Step 5+ services port |
| Option leaves field shapes (`PickOption` / `CancelOption` nullability under AutoPacker) | Step 5+ when Request ships |
| Match-end with open Request | Step 5+ |
| Animation pre-roll model | Views land (step 7) |
| Block-level undo as a primitive | A UI scrub feature needs it |
| Reaction-as-distinct-Kind vs `Effect + ParentId` | TriggerService design |
| Wire-level `SystemBlock` Kind | A clearly-system-attributed cause appears that doesn't fit Command |

## Landed (2026-05-30)

The wire-shape graduation criteria are met:

1. ✅ `BlockStartEvent` reshaped — `Id / ParentId / Kind / SourceId` (L5).
2. ✅ `BlockEndEvent` reshaped — `Id / Outcome` (L6).
3. ✅ `BlockKind` enum (`None / Command / Effect / Trigger`) and
   `BlockOutcome` enum (`None / Completed / Errored / Cancelled`) added
   under `Assets/Framework/Wire/Runtime/Events/`.
4. ✅ `RequestOpenedEvent`, `RequestClosedEvent`, `ResponseEvent` added.
5. ✅ Round-trip tests in `Framework.Wire.Tests/RoundTripTests.cs` cover
   all six event types under AutoPacker (block brackets, nested ParentId,
   all three Outcomes, full Request open/close/response, non-cancellable
   Request).

**Still deferred** (lights up with step 5 services): who-emits
(service-direct vs Emit handler — see Q5), option-leaf shapes
(`PickOptionEvent` / `CancelOptionEvent` nullability under AutoPacker),
match-end-with-open-Request encoding.
