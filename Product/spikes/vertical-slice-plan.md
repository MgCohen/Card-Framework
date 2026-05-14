---
type: spike
status: concluded
tags: [#spike, #refoundation, #vertical-slice, #plan]
---

# Vertical Slice Plan — Entity / Command / Stream / Mirror

> **Status: concluded (2026-05-14).** Originally a 5-phase plan to prove the refoundation loop. The slice landed, then grew well past the original AC-1..AC-7 acceptance criteria over a sustained design-pass session: ECS rewrite, per-role services + scopes, zones + players, deathrattle direct triggers, block metadata, Effect<TPayload> + Validate, first-class Request with typed option wire events. **The original plan + locks below are preserved as provenance.** Current shipped state + post-plan architectural locks are captured at the bottom of this file under [§What actually shipped](#what-actually-shipped) and [§Architectural locks discovered post-plan](#architectural-locks-discovered-post-plan). New work that grows out of the slice should land as its own spike → model → ADR per workflow.

A runnable proof of the refoundation architecture using only manually-wired primitives. Validates the entity → command → tag-change → block → stream → mirror loop end-to-end before committing to source-gen, attributes, networking, or production-grade ergonomics. Companion to `card-effect-walkthrough.md` (the design) — this is the smallest executable that proves the design hangs together.

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

---

## What actually shipped

The 5-phase plan above landed in [`ffb9dee`](https://github.com/MgCohen/Card-Framework/commit/ffb9dee9) as a single batch (validation done locally, no headless test environment on the implementing side). All seven acceptance criteria pass. After that, a multi-session design pass on `claude/entity-based-architecture-pMNhU` reshaped the slice — keeping the architecture proof intact but pushing it well beyond the original scope.

Branch: `claude/entity-based-architecture-pMNhU`, 10 commits since `ffb9dee`. As of 2026-05-14: **71/71 EditMode tests passing** under Unity 6000.4.0f1 batch mode. Six assembly DLLs compile clean (Engine, Mirror, Sample, Unity, Unity.Editor, Tests).

### Commit walk

| Commit | What landed |
|---|---|
| `ffb9dee` | Original 5-phase plan: TagKey/Entity/World/SignalBus/Command/Block + Sample (Card/Fireball/DivineShield) + Mirror + Unity scene + tests. |
| `5cb524b` | Test-discovery asmdef fix (`TestAssemblies` ref + `Editor` platform); Unity-generated .meta files. |
| `4a0a6f4` | Sample.unity scene asset + `SliceSceneBuilder` editor utility. |
| `6b2dd2b` | Event-log render: hide blocks whose body hasn't been re-applied yet on scrub-back. |
| `019e49d` | **Rewrite on ECS + per-role services + scope gatekeeping.** Single `Entity` (no subclasses), `IEntity` read-only interface, `World` as pure substrate, `EffectService` / `CommandService`, `IEffectScope` / `ICommandScope` / `IHookScope`, wire alphabet trimmed to 6 events (BlockStart/BlockEnd/EntityCreated/EntityDestroyed/TagSet/TagChange), TagSet/TagChange split fixes mirror init-value rewind bug, BlockType enum dropped (kind comes from dispatch site, not declared on type). |
| `c3f6247` | Zones — `IsZone` marker, `Zone` tag pointing at zone id, `ZoneName`, `MoveCard` command, `ZoneQueries`, Hand/Battlefield/Graveyard. |
| `a710d72` | Players + ownership — `IsPlayer` marker, `Owner` tag, `IsGraveyard` semantic marker. Initial deathrattle as a hardcoded effect + `MoveCard`-bridge subscription. |
| `a9c27b1` | Block metadata — `Id` (monotonic), `ParentId`, `BlockKind` (Effect/Command), `Source` (convention-extracted from a public Source property), `Outcome` (Completed/Errored). `BlockEndEvent` carries the matching `Id`. Caster→Source rename across effects for the convention to land. |
| `cd96fb3` | **Effect<TPayload> + Validate + direct triggers via CardEffectStore.** Unifies game rules (DivineShield/Armor/Backlash) and per-card abilities (Deathrattle) under one `Effect<TCommand>` shape with payload-on-construction. `Validate` separate from `Run`, gates wire-emission. New `CardEffectStore` for direct triggers attached to a specific card; `IEffectScope.FireDirect<TTrigger>` for engine bridges. Two distinct deathrattles on two different cards prove per-card variation works without per-card C# subclasses. `HasDeathrattle` tag retired — store entry is the source of truth. |
| `12f2383` | **Choose-One via first-class Request + typed option wire events.** `Request` peer to Effect/Command (not subordinate). `BlockKind.Request` for the options block. Three new leaf wire events: `PickOptionEvent` (generic — source/target/value), `CancelOptionEvent`, `ResponseEvent`. No labels on the wire — wire is pure data; client owns presentation. `RequestsService` on `GameEngine`. `IEffectScope.OpenRequest`. `Tactician` sample card demonstrates Choose-One (damage / heal / cancel) end-to-end. |

### Files inventory (Assets/Sample/VerticalSlice/)

**Engine** (6 DLLs total):

- `Engine/Entities/` — `Entity.cs`, `IEntity.cs`
- `Engine/Commands/` — `Command.cs` (abstract base), `ICommandScope.cs`
- `Engine/Effects/` — `Effect.cs` (+ `Effect<TPayload>`), `IEffectScope.cs`, `Request.cs`
- `Engine/Hooks/` — `HookHandler.cs`, `IHookScope.cs`
- `Engine/Pipeline/` — `GameEngine.cs` (composition root), `World.cs` (substrate), `CommandService.cs`, `EffectService.cs`, `RequestsService.cs`, `CardEffectStore.cs`, `HookBus.cs`, `HookPhase.cs`, `HookPriority.cs`, `PayloadCapture.cs`
- `Engine/Stream/` — `WireEvent.cs` (BlockStart/End, EntityCreated/Destroyed, TagSet/TagChange), `BlockKind.cs`, `BlockOutcome.cs`, `OptionEvents.cs` (PickOption/CancelOption/Response), `EventLog.cs`
- `Engine/Tags/` — `TagKey.cs`, `TagRegistry.cs`

**Sample:**

- Tags + extensions: `Tags.cs`, `EntityExtensions.cs`
- Effects: `Fireball.cs`, `HealingPotion.cs`, `ShieldBreak.cs`, `Backlash.cs`, `DamageAllEnemies.cs`, `HealAllAllies.cs`, `Tactician.cs`
- Commands: `DealDamage.cs`, `Heal.cs`, `BreakDivineShield.cs`, `MoveCard.cs`
- Rules (Effect<DealDamage> with static Register): `DivineShieldRule.cs`, `ArmorRule.cs`, `BacklashRule.cs`
- Direct-trigger plumbing: `Deathrattle.cs` (record-shaped class), `DeathrattleBridge.cs` (static bus subscription)
- Choose-One: `TacticianRequest.cs`
- Queries: `ZoneQueries.cs`

**Tests** (71 cases, 9 fixtures):

EngineTests, EffectTests, BlockMetadataTests, MirrorTests, MirrorInitValueTests, ScopeTests, DivineShieldTests, ArmorTests, HealTests, BacklashTests, DeathrattleTests, ZoneTests, PlayerTests, TacticianTests.

**Unity:**

- `SliceBootstrap.cs` (IMGUI HUD with three panels + scrub slider + open-requests picker)
- `Sample.unity` scene (pre-wired)
- `SliceSceneBuilder.cs` (editor menu for rebuilding the scene)

### Acceptance criteria

All seven (AC-1..AC-7) from the original plan still pass — extended by:

- AC-8 (informal): zones — move a card between zones; the wire shows `MoveCard` → `TagChange(zone)`; mirror rewinds correctly.
- AC-9 (informal): deathrattle — two cards with two different deathrattles; moving either to graveyard fires the right effect; wire shows nested causal chain.
- AC-10 (informal): Choose-One — Cast Tactician opens a Request block with three typed option events; picking dispatches the right command top-level; cancelling no-ops.

---

## Architectural locks discovered post-plan

These were settled by the design-pass walk after the original Q1–Q6 locks. Listed in the order they were locked. Each is binding for the current slice; revisit when scope expands beyond it.

| # | Decision | Notes |
|---|---|---|
| P1 | **ECS shape — single `Entity` type, no subclasses.** | Card/Zone/Player/Modifier are all just `Entity` with marker tags (`IsCard`, `IsZone`, etc.). Replaces the C# subclass hierarchy from the original plan. Wire alphabet's `EntityCreated` carries only id, no type name. |
| P2 | **IEntity / Entity split + scope-typed access.** | `IEntity` is read-only; `Entity` is mutable. Effects + hooks see `IEntity` (compile-enforced no-mutate); commands `Resolve(IEntity) → Entity` via `ICommandScope` for writes. State doesn't pollute Entity — extension methods own typed accessors. |
| P3 | **Per-role services (EffectService, CommandService), per-role scopes.** | `GameEngine` composes them. Each scope is the smallest API its role needs — `IEffectScope` has Execute + Run + OpenRequest + FireDirect; `ICommandScope` has Resolve + read; `IHookScope` has Run + FireDirect + read. World is pure substrate (no verbs). |
| P4 | **Wire alphabet — 6 concrete event classes, no shared `Mutation` supertype.** | `BlockStart` / `BlockEnd` / `EntityCreated` / `EntityDestroyed` / `TagSet` / `TagChange`. Mirror pattern-matches on the concrete type. Block-kind is metadata on `BlockStart`, not a wire-level enum on the operation type. |
| P5 | **`TagSet` vs `TagChange` split — fixes mirror init-value rewind bug.** | First write of a tag key emits `TagSet(id, key, value)`; subsequent writes emit `TagChange(id, key, prev, next)`. Unapply of `TagSet` REMOVES the key from the mirror entity (the bug-fix). Three dedicated tests in `MirrorInitValueTests`. |
| P6 | **Block metadata: Id + ParentId + Kind + Source + Outcome.** | `BlockStartEvent` carries `Id` (monotonic), `ParentId?` (causal chain explicit), `BlockKind` (Effect/Command/Request — set by dispatch site, not by runnable type), `Source : IEntity?` (convention-extracted from a public Source property), payload (reflection-walked). `BlockEndEvent` carries `Id` (matches start) + `Outcome` (Completed/Errored/Cancelled). |
| P7 | **Effects + commands have NO BlockType declaration.** | Kind comes from which service dispatched the call (`EffectService.Run` → Effect; `CommandService.Execute` → Command; `RequestsService.Open` → Request). Authors don't write `public override BlockType BlockType => …`. |
| P8 | **Effect<TPayload> with payload-on-construction.** | Effects that react to something specific (a command for hook effects; a typed trigger record for direct effects) carry their payload on `Trigger` property, bound at construction. `Validate(IEffectScope) → bool` is separate from `Run(IEffectScope)` — engine short-circuits with no wire event if Validate returns false. |
| P9 | **`EffectService.Register<TCommand>(phase, priority, factory)` for hook effects.** | One call binds a factory `cmd → Effect<TCommand>` to a command type. EffectService subscribes to the bus on the caller's behalf, instantiates the effect on fire, runs through Validate → Block → Run. Game rules (Shield/Armor/Backlash) use this. Phase/priority live at the registration call site, **not** declared on the effect class (acknowledged as "works for now, revisit later"). |
| P10 | **Direct triggers via `CardEffectStore` + `IEffectScope.FireDirect`.** | Per-entity, per-trigger-type effect storage. Cards attach effects via `engine.CardEffects.Set<TTrigger>(card, factory)` at setup. Engine bridges (subscribed via `Commands.Subscribe`) translate broadcast events into targeted dispatch: `scope.FireDirect(card, new TriggerType(...))`. The store stays out of effects' API surface — effects never reach into it. Per-card different effects (e.g., damage-all-enemies vs heal-all-allies as different cards' deathrattles) work without per-card C# subclasses. |
| P11 | **Trigger payload types are sealed classes, not records.** | Unity's .NET surface doesn't ship `System.Runtime.CompilerServices.IsExternalInit`, so records with init-setters break. `Deathrattle(IEntity Source)` etc. are plain sealed classes with a constructor + read-only properties. Will revisit if Unity ships the type, or we add a shim. |
| P12 | **State-tags stay tags; triggered effects don't.** | Taunt-style permanent properties (DivineShield, Backlash, Armor) the rule engine consults during decision/resolution — these are bool/int tags. Triggered effects (Deathrattle, Battlecry, OnPlay) are direct-trigger entries in CardEffectStore, not tags. The previous tag-only approach (`HasDeathrattle : bool` with a separate hardcoded handler) caused sync bugs and couldn't represent per-card behavior. |
| P13 | **Request is a peer to Effect/Command, not subordinate.** | Player decisions (Choose-One, target picking, mulligan, pre-play targeting) are first-class. `Request` base + `RequestsService` open a `BlockKind.Request` block on the wire. Effects open requests via `IEffectScope.OpenRequest`. Game phases / system prompts would open them too (future). Adapted from ADR-0011's request system but reshaped to the slice's vocabulary (no Slice/GameCommand/Reference/Validators). |
| P14 | **Typed option wire events as building blocks; no labels on the wire.** | `PickOptionEvent(idx, source?, target?, value?)`, `CancelOptionEvent(idx)`, `ResponseEvent(reqId, picked)`. Option PAYLOAD is generic refs+value+ordinal — no per-effect option subclasses (no DamageOption/HealOption). The wire carries pure data. Server-side resolution lives in `Request.Resolve(picked, scope)` — switch on the picked index to dispatch the right command. Labels are presentation, owned by the client. |
| P15 | **Sync model (no async/Task).** | Effect.Run / Command.Apply are synchronous. Request open is fire-and-fork; the chosen option's resolution runs top-level later. Async (`Task Run`, `await scope.Dispatch(request)`) is the future-correct shape for pre-play targeting / mulligan / mid-effect decisions — explicitly deferred. The current shape carries over: the Request type and wire alphabet don't change when async lands. |
| P16 | **No auto-wrap on tag writes; bare TagSet/TagChange at top level OK.** | Setup phase is a flat sequence of EntityCreated + TagSet events at the top of the log. The previous "every write inside a Block(System)" invariant is gone (it didn't earn its keep, and `OpenSystemBlock`/`OpenTriggerBlock` were retired alongside). The auto-wrap mechanism was removed from `Entity.Set`. |
| P17 | **`EffectService.RunResolution` — internal scope-without-block path.** | The RequestsService.Respond path needs an `IEffectScope` to call `request.Resolve(picked, scope)` but should NOT open a wrapper Block (the resolution's child commands appear top-level on the wire). EffectService exposes `internal void RunResolution(Action<IEffectScope> body)` for this. Same pattern usable for future engine-side dispatches that need a scope without a block. |

## Deferred items, flagged for next steps

These were explicitly noted as "works for now; revisit later" during the design pass. Not slice-blocking; carrying them as context.

- **Phase/priority on the registration site vs. on the effect class.** Today: passed at `Register(phase, priority, factory)` time. User flagged "I don't think effects should be defining the priority/phase like that, but that works for now."
- **Factory-closure shape (`Func<TPayload, Effect>`)** at registration sites. Works but not loved.
- **General-lookup feel of `CardEffectStore`.** Conceptually correct (sidecar of trigger-keyed factories per entity); will revisit when the data-driven definition layer lands.
- **Validate-without-running observability.** Today: if Validate returns false, no wire event is emitted at all. Future cost-paying / option-showing systems may want to observe "this effect was eligible, here are why-not reasons." Wire alphabet would gain a `ValidationFailed` event then.
- **Hooks opening requests.** `IHookScope.OpenRequest` not yet added — a reactive hook can't ask the player a question today. Easy follow-up.
- **Async effects.** Pre-play target picking + mulligan + mid-effect choices need `Task Run`. Deferred. Request type + wire alphabet carry over.
- **Effect-id registry for per-card abilities.** Per-card Deathrattle works via `CardEffectStore` + factory closures. Eventually we want card definitions (`CardDefinition` static data → instantiates Effect factories at card creation). The current shape is the runtime substrate the definition layer will sit on top of.
- **OptionEvent kinds beyond Pick/Cancel.** `TargetOptionEvent`, `NumberOptionEvent`, `ConfirmOptionEvent` are additive when needed.
- **Modifier/aura as entities.** "+1/+1 to all friendlies while this is on the battlefield" doesn't fit triggers or state-tags cleanly. Modifier-as-entity is the production path; the slice doesn't demo it. The flat ECS + CardEffectStore foundation supports it when we want to add it.
- **Label tables for option display on the client.** Bootstrap renders generic debug labels for Pick/Cancel options today. A real game would have a content-side label lookup keyed by (request name, option index).
- **PR open.** Branch `claude/entity-based-architecture-pMNhU` is 10 commits ahead of `origin/main`. PR creation blocked on stale `gh` auth — manual PR or re-auth, see prior chat for the prefilled body.

## Status note

This spike is `concluded`. The work it covers shipped on the branch; the architectural shape is stable enough to inform follow-up work (draggable cards UI, pile counter sample, eventual async + request enhancement, definition layer, modifier-as-entity). New work should land as its own spike → model → ADR per workflow, not as further appends here.
