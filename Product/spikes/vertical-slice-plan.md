---
type: spike
status: draft
tags: [#spike, #refoundation, #vertical-slice, #plan]
---

# Vertical Slice Plan — Entity / Command / Stream / Mirror

> **Status: draft, ready to implement.** A runnable proof of the refoundation architecture using only manually-wired primitives. Validates the entity → command → tag-change → block → stream → mirror loop end-to-end before committing to source-gen, attributes, networking, or production-grade ergonomics. Companion to `card-effect-walkthrough.md` (the design) — this is the smallest executable that proves the design hangs together.

## Goal

Prove that the locked refoundation shape can be implemented end-to-end in one Unity scene:

```
[UI button]
   ↓ invokes
[Effect handler]
   ↓ issues
[Command]  ──opens──▶ [Block] ──emits──▶ [Stream]  ──feeds──▶ [Mirror]
   ↓ Apply
[Entity.Set] ──fires──▶ [TagChange event into Block]
   ↓
[Hooks Before/After] (manual signal bus, Replacement-priority shown)
```

The slice succeeds if all six user acceptance criteria pass and the engine code is Unity-agnostic (a pure-C# asmdef runnable from a headless test runner).

## Acceptance criteria

| # | Criterion | Test |
|---|---|---|
| AC-1 | Create entities | Setup button instantiates `Caster` and `Target` entities. Both visible in primary state panel with their initial tag values. |
| AC-2 | Edit a tag manually | Debug button calls `target.Hp = 25`. Primary panel shows new value, stream log shows the `TagChange` entry, mirror panel mirrors the change. |
| AC-3 | Stream pipeline | All TagChanges and Block boundaries land in an observable `EventLog` in the order they happen. |
| AC-4 | Local listener executes | A `MirrorWorld` subscribed to the stream maintains an independent state mirror that matches the primary after every event. |
| AC-5 | Scrub back and forth | A slider scrubs the mirror through the event log. Dragging left rebuilds mirror state at event 0..N. Dragging right re-applies forward. Primary state is untouched by scrubbing. |
| AC-6 | Button → effect → commands → tag changes | "Cast Fireball" button calls a hardcoded effect handler that issues `DealDamage(target, 6)`. Engine pipeline runs Before → Apply → After hooks. Wire log records `BlockStart(POWER, DealDamage) → TagChange(target, hp, 30, 24) → BlockEnd`. Both panels reflect. |
| AC-7 (bonus) | Replacement-band hook | "Divine Shield ON" toggle on target. With shield, Fireball produces `TagChange(target, divineShield, true, false)` and **no** HP change. Wire log shows the shield breaking but the damage write was skipped (equality-skip guard). |

## Out of scope

- Source generators of any kind. No `[Tag]` partial properties — hand-write `Get`/`Set` calls behind manual properties.
- Subscription attributes (`[On(...)]`). Hook registration is imperative: `signalBus.Subscribe<DealDamage>(HookPhase.Before, HookPriority.Replacement, …)`.
- Modifiers as entities. Divine Shield is a single `bool` tag for the slice; full modifier-as-entity work happens in a later spike.
- `Player`, `Zone`, `Match`, stack, turn structure, phases, mulligan. The slice has two entities only.
- Visibility model (`FullEntity` / `ShowEntity` / `HideEntity`). Single-process, both panels see everything.
- Async `Prompt`. No player input mid-resolution.
- Wire serialization (JSON/binary/MessagePack). Events are C# objects in a `List`. Wire format spike is separate.
- Networking. Primary engine and mirror live in the same process.
- Replays-by-command-rerun. Mirror only applies TagChanges + Block markers. Confirms the "replays apply state, not logic" lock.
- Source-of-truth ECS integration with Unity DOTS or any third-party ECS. Pure C#.
- Performance optimization. Correctness only; profile later.

## Sample effect chosen

**Fireball + Divine Shield.** Smallest pair that exercises:

- A primitive command (`DealDamage`)
- A typed Before hook at Replacement priority (the shield)
- The `cmd.Amount = 0` mutation pattern
- The equality-skip guard in `Entity.Set` (Hp write becomes a no-op when damage is replaced to 0)
- Two distinct TagChange events in one Block when the shield breaks (the shield tag flips; the HP doesn't)

If we end up needing a triggered ability before the slice ships, the smallest addition is "Knife Juggler" — but I'd defer it; Fireball + Shield is already enough to prove the architecture.

## Module layout

```
Sample/VerticalSlice/                      ← pure C#, Unity-agnostic, own asmdef
  Engine/
    Tags/
      TagKey.cs                            // typed singleton, slot index
      TagRegistry.cs                       // slot allocator
    Entities/
      Entity.cs                            // slot array, Get<T>/Set<T>
      Card.cs                              // : Entity, typed properties (manual)
    Commands/
      Command.cs                           // abstract base, BlockType, Apply
      SetTag.cs                            // generic SetTag<T>
      DealDamage.cs                        // semantic convenience
    Pipeline/
      World.cs                             // entity registry, Execute, block stack
      Block.cs                             // cause, parent, events
      BlockType.cs                         // typed identifiers (no enum)
      SignalBus.cs                         // imperative subscribe by command type + phase + priority
      HookPhase.cs                         // Before, After
      HookPriority.cs                      // Replacement, PreEffect, OnEffect, Reaction
    Stream/
      WireEvent.cs                         // BlockStart, BlockEnd, TagChange, EntityCreated
      EventLog.cs                          // append-only list, observable
  Mirror/
    MirrorWorld.cs                         // applies WireEvent to own dictionary state
    MirrorEntity.cs                        // {tagKey → object}
    ScrubController.cs                     // GoTo(N), rebuilds mirror state to event N
  Sample/
    Tags.cs                                // Hp, MaxHp, DivineShield, Attack, Kind
    Fireball.cs                            // hardcoded effect: Execute(DealDamage)
    DivineShieldRule.cs                    // static, registered into SignalBus
  Tests/                                   // headless C# tests (no Unity dependency)
    EngineTests.cs                         // SetTag, DealDamage, Block open/close
    MirrorTests.cs                         // mirror equality, scrub correctness
    DivineShieldTests.cs                   // replacement-band hook semantics

Assets/Sample/VerticalSlice/               ← Unity scene + UI
  VerticalSliceScene.unity
  Scripts/
    SliceBootstrap.cs                      // composes Engine + Mirror, wires UI
    PrimaryStatePanel.cs
    MirrorStatePanel.cs
    EventLogPanel.cs
    ScrubBar.cs
  UI/
    SliceUI.uxml                           // or whichever UI flavor matches the repo's convention
    SliceUI.uss
```

`Sample/VerticalSlice/Engine/` and `Sample/VerticalSlice/Mirror/` build into asmdefs with no Unity reference. `Sample/VerticalSlice/Tests/` runs in any .NET test runner.

## Phases

Each phase ends with a runnable demo (console smoke test or scene). Each is a separate commit on `claude/entity-based-architecture-pMNhU`.

### Phase 1 — Engine core (no commands yet)

**Deliverables:**
- `TagKey<T>`, `TagRegistry` (slot allocator returns ascending indices).
- `Entity` with `Get<T>(TagKey<T>)`, `Set<T>(TagKey<T>, T)`. `Set` uses `EqualityComparer<T>.Default` to skip no-op writes; emits a `TagChange` into the active block via a `World` callback.
- `World` with entity allocation (`Create<TEntity>()` → monotonic id from 1; emits `EntityCreatedEvent` into the active block), in-memory `EventLog`, and a "current block" stack. Writes outside any block are auto-wrapped in a single-event `Block(BlockType.System)`.
- `Card : Entity` with two manual properties (`Hp`, `DivineShield`) calling `Get`/`Set`.

**Smoke test (Tests/EngineTests):**
```csharp
var w = new World();
var c = w.Create<Card>();
c.Hp = 30;
c.Hp = 28;
Assert.That(w.EventLog.Count == 2);  // two TagChanges
Assert.That(c.Hp == 28);
```

### Phase 2 — Commands + pipeline + signal bus

**Deliverables:**
- `Command` base with abstract `BlockType` and `Apply(World)`.
- `SetTag<T>(Entity, TagKey<T>, T)` command (the universal write).
- `DealDamage(Entity Source, Entity Target, int Amount)` command — its `Apply` writes to `Target.Hp`. Mutable `Amount` so Replacement hooks can change it.
- `SignalBus` with imperative `Subscribe<TCmd>(HookPhase phase, HookPriority pri, Action<TCmd> handler)`. Stores handlers in priority order.
- `World.Execute(cmd)` opens a block, fires Before hooks in priority order, calls `cmd.Apply(this)`, fires After hooks, closes the block.

**Smoke test:**
```csharp
var w = new World();
var c = w.Create<Card>();
c.Hp = 30;

bool beforeFired = false, afterFired = false;
w.Hooks.Subscribe<DealDamage>(HookPhase.Before, HookPriority.OnEffect, _ => beforeFired = true);
w.Hooks.Subscribe<DealDamage>(HookPhase.After,  HookPriority.Reaction,  _ => afterFired = true);

w.Execute(new DealDamage(source: null, target: c, amount: 6));

Assert.That(beforeFired && afterFired);
Assert.That(c.Hp == 24);
Assert.That(w.EventLog.Pattern() is [BlockStart, TagChange, BlockEnd]);
```

### Phase 3 — Mirror + scrub (cursor-based reverse-apply)

**Deliverables:**
- `MirrorEntity` (id + dict of tag values).
- `MirrorWorld` holds `_entities : Dictionary<int, MirrorEntity>` and `_cursor : int` (mirror state reflects "after applying events `0.._cursor-1`").
- `Apply(WireEvent)` and `Unapply(WireEvent)`:
  - `BlockStart` / `BlockEnd` — no-op for state correctness (optional nesting counter for diagnostic).
  - `TagChange(id, key, prev, next)` — `Apply` writes `next`, `Unapply` writes `prev`.
  - `EntityCreated(id)` — `Apply` instantiates `MirrorEntity`, `Unapply` removes from dict.
- `ScrubController.GoTo(target, log)`:
  ```csharp
  while (_cursor < target) Apply(log[_cursor++]);
  while (_cursor > target) Unapply(log[--_cursor]);
  ```

**Smoke test:**
```csharp
var w = new World();
var c = w.Create<Card>();
c.Hp = 30;
w.Execute(new DealDamage(source: null, target: c, amount: 6));   // 30 → 24
w.Execute(new DealDamage(source: null, target: c, amount: 4));   // 24 → 20

var mirror = new MirrorWorld();
var scrub = new ScrubController(mirror, w.EventLog);

scrub.GoTo(w.EventLog.Count);   // forward to head
Assert.That(mirror.Get(c.Id, Tags.Hp) == 20);

scrub.GoTo(2);                  // reverse-apply backward
Assert.That(mirror.Get(c.Id, Tags.Hp) == 24);

scrub.GoTo(0);                  // reverse all the way
Assert.That(mirror.Contains(c.Id) == false);   // entity removed
```

### Phase 4 — Sample effect (Fireball) + Divine Shield rule

**Deliverables:**
- `Fireball` — static method `Cast(World w, Entity caster, Entity target)` that issues `Execute(new DealDamage(caster, target, 6))`. Not an attribute-driven handler — just a method we'll wire to the UI button.
- `DivineShieldRule` — static `Register(SignalBus bus)` that subscribes a single Before+Replacement hook for `DealDamage`. Hook reads `target.DivineShield`; if true, sets `cmd.Amount = 0` and `target.DivineShield = false`.
- `Entity.Set` includes the equality-skip guard so the Hp write becomes a no-op when `cmd.Amount` was zeroed.

**Smoke test:**
```csharp
var w = new World();
DivineShieldRule.Register(w.Hooks);

var caster = w.Create<Card>();
var target = w.Create<Card>();
target.Hp = 30;
target.DivineShield = true;

Fireball.Cast(w, caster, target);

Assert.That(target.DivineShield == false);
Assert.That(target.Hp == 30);   // shield ate the damage
var tagChanges = w.EventLog.OfType<TagChangeEvent>().ToList();
Assert.That(tagChanges.Count == 1);
Assert.That(tagChanges[0].Key == "divineShield");
```

### Phase 5 — Unity scene

**Deliverables:**
- `SliceBootstrap`: instantiates `World`, `MirrorWorld`, `EventLog → MirrorWorld` subscription, registers `DivineShieldRule`, creates initial entities.
- Three panels (UI Toolkit or IMGUI — match the repo's convention):
  - **Primary State** — lists entities and their visible tags from the engine's `World`.
  - **Mirror State** — lists entities and their tags from `MirrorWorld`.
  - **Event Log** — scrollable list of wire events with indentation for block nesting.
- Controls:
  - "Reset / Setup" — recreates the two entities.
  - "Set Target HP to 25" — manual tag write (validates AC-2).
  - "Cast Fireball" — invokes `Fireball.Cast(w, caster, target)`.
  - "Divine Shield on Target" — toggle.
  - **Scrub slider** — range 0..EventLog.Count, drives `ScrubController.GoTo`.

**Manual test = the acceptance criteria.** Run through AC-1 to AC-7 in the scene.

## Implementation order within phases

For each phase: types → commands → tests → confirm green → commit → move on. No skipping ahead.

## Locked decisions (Q&A walkthrough)

The six open questions from the original plan were settled in a Q&A pass on the chat. Locks below are binding for the slice; revisit when their scope expands beyond the slice.

| # | Decision | Notes |
|---|---|---|
| Q1 | **Entity ID = monotonic int from 1.** | `0` reserved for "no entity / null ref". IDs are not reused. Persistence and cross-process stability are deferred. |
| Q2 | **`Entity.Set` equality skip uses `EqualityComparer<T>.Default`.** | Value equality for value types, reference equality for reference types. Documented gotcha: reference-typed tag values (e.g. immutable lists) must be reassigned to a new instance to register as a change. |
| Q3 | **Tag writes outside `Execute` auto-wrap.** | Each `Set` outside an active block opens a single-event `Block(BlockType.System)`, emits the `TagChange`, closes. Wire invariant "every `TagChange` is inside a block" holds without exception. Optional batching sugar (`using world.OpenSystemBlock(...)`) deferred. |
| Q4 | **`BlockStart` payload is a reflection-based dict.** | Walks the command's public properties: `{ type, props: { Source, Target, Amount, … } }`. Entity references stay as `Entity` instances for the slice (in-process mirror displays via `ToString`); we'll catch the "convert references to IDs" gotcha when wire-format spike runs. Payload is display-only — the mirror reconstructs state from `TagChange` events, not from `BlockStart` payloads. |
| Q5 | **Mirror uses reverse-apply with a cursor.** | `ScrubController` moves the cursor; mirror walks forward via `Apply` (using `TagChange.Next`) or backward via `Unapply` (using `TagChange.Prev`). Requires adding **(Q5-i)** `EntityCreatedEvent(id)` to the wire alphabet — emitted by `World.Create<T>()` inside the active block (synthetic system block if none). Forward `Apply` instantiates a `MirrorEntity`; reverse `Unapply` removes it. Keeps the mirror exact at every cursor position. |
| Q6 | **Unity UI uses IMGUI for the slice.** | Throwaway debug HUD; ugly is fine. Trivial to author, zero asset setup. Replace later if the slice grows into something demoable beyond the team. |

## What "done" looks like

- Phases 1–4 are tests-only and live in a CI-runnable C# test project.
- Phase 5 is a Unity scene that exercises the same code path.
- A reviewer can clone the branch, run the test project headless, get green; then open the scene, click through, see the architecture work; then scrub the slider and see the mirror walk back in time without affecting the primary.

If all of that holds, the architecture as designed in `card-effect-walkthrough.md` is implementable. We can then go after the next five locks (wire-alphabet ADR, aura spike, simultaneous-events ADR, visibility spike, modifier-semantics ADR) with much higher confidence.

## Estimated effort

Phases 1–4 are ~1 day of focused work each (small surface, lots of small types). Phase 5 is another day depending on UI flavor. Total: ~5 days of unhurried work to a fully demoable slice. Faster if we skip Phase 5 and stop at headless tests (the architecture is fully validated by Phase 4; Phase 5 is just the demo).
