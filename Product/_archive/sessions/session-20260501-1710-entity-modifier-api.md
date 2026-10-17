---
type: session
schema-version: 2
session-id: session-20260501-1710-entity-modifier-api
parent: session-20260501-1545-trigger-pipeline-shape
status: completed
started-at: 2026-05-01T17:10:18Z
work-summary: "Resolved oq-entity-modifier-api with Q1–Q5 conventions on the Scaffold bridge; created entities/02 runtime-surface Model; added slice subscriptions + atomic batches to engine/04; clarified ActorId vs InstanceId in GLOSSARY."
debt-opened: 2
tags: [#session]
---

# session-20260501-1710-entity-modifier-api

## Part 1 — Active question coming in

**Entity modifier mutation API (Scaffold bridge)** (`oq-entity-modifier-api`) — concrete payload shapes for `AddModifierPayload`, `SetBaseValuePayload`, `AddEntityVariablePayload` through the `com.scaffold.entities.states` bridge. Needed before any game reads or writes layered modifier values.
*(Pivoted from `oq-zone-internal-opens`; carried forward from earlier sessions.)*

## Part 2 — Work done

Combined design pass + patch session, then expanded to a runtime-surface write-up after a doc-clarity review found gaps that would have blocked any game author trying to use entities/variables (no documented read API, no entity-handle access path, no lifecycle, no authoring-type bridge, no worked example, plus stale prose in older Models). Three rounds of "let's do it now" deviations from "design unit ≠ patch unit" — all at user request.

Pulled ground-truth shapes from `com.scaffold.entities.states` (six payloads, `EntityVariableState`, `StateEntity`, `EntityStateFactory`, `StoreVariableStorage`) and `com.scaffold.entities` (`Variable`, `VariableModifier`, `ModifierSource`, `ModifierId`, `InstanceId`, `IReadOnlyEntity`, `IMutableEntity`, `EntityModifierEntry`, `IEntityDefinition`). Original Q list (variable typing, modifier identity, operation shape, ordering, removal surface) was fully foreclosed by the package — those are settled by Scaffold, not the Card Framework. Reset to integration discipline.

A second wrinkle surfaced when reading the bridge: `StateEntity.AddModifier(EntityModifierEntry)` calls `ModifierId.New()` (`Guid.NewGuid()`) internally, contradicting the Q5 deterministic-id lock. User chose the cleanest fix — a Scaffold update extending `EntityModifierEntry` with optional `Id` and `Source` fields, plus a Card Framework `IModifierFactory.Create(...)` service that returns a fully-populated entry with a deterministic id baked in. The factory replaces the earlier `IModifierIdGenerator.NextId()` shape; handler call sites become two-line build-then-install rather than chained one-liners.

Patches applied:
- `features/entities/models/01-definition-instance-model.md` §"Store And Entities Relationship" — added new subsection §"Mutation API conventions" with the full 6-payload reference, `BaseValues`-as-overrides + `TryGetDefaultValue` fallback semantics, and the five Q1–Q5 conventions (Q5 reshaped to factory pattern after second wrinkle).
- `features/entities/models/02-runtime-surface.md` — **new Model.** Read API (`IReadOnlyEntity` methods, inner-type read rule, polymorphic substitution). Subscriptions (per-variable + structural). Mutation API (`IMutableEntity`, factory-built modifier entries, raw-payload fallback rules). Lifecycle via `IEntityRegistry` (Spawn/Get/TryGet/Has/Destroy + snapshot restore). Read consistency / cache rebuild semantics. Authoring reference (`Variable`, `VariableValue`, `VariableModifier`, `EntityDefinition`). Worked example: villager equips/unequips sword, modifier stack composition + cleanup-on-source. Open items section.
- `features/effects/models/04-effect-flow.md` §4.4 — `IHandlerContext` gains `IEntityRegistry Entities` + `IModifierFactory Modifiers`; entity-mutation-surface preference clarified (handle default, raw-payload fallback for `SetBaseValuePayload` / `RemoveModifiersBySourcePayload`).
- `features/effects/models/04-effect-flow.md` §4.3 — `IEffectScope` gains `IEntityRegistry Entities` + `IModifierFactory Modifiers` so trigger-react closures can read entity variables and pre-build modifier entries for dispatched actions.
- `features/effects/models/03-ongoing-effects.md` §5 — recorded the canonical `RemoveModifiersBySourcePayload(target.Id, new ModifierSource(attachment.Id))` cleanup pattern.
- `features/effects/models/03-ongoing-effects.md` §2 — patched stale `HealOverTimeDefinition` example: `ctx.State.GetVariable` → `scope.Entities.Get<TDef>(self).GetVariable<int>(...)`; `SetVariableAction` → `DecrementTurnsRemainingAction` (handler does the `SetBaseValuePayload` write).
- `features/actions/archive/01-command-pipeline.md` §8 — patched stale `HealActorHandler` example: `scope.GetVariable<int>(Target, "life")` → `ctx.Entities.Get<ActorDefinition>(...).GetVariable<int>(Vars.Life)`; `SetVariableMutation<int>` → `SetBaseValuePayload`.
- `ledgers/OPEN-QUESTIONS.md` Core Model — struck `oq-entity-modifier-api` with resolution note pointing to entities/01.

Then a doc-clarity inventory pass surfaced operations a game author needs but couldn't find a documented entry point for. Walked three batches of follow-ups:

- **Batch 1** (`entities/02`): §2 typed `entity.Definition` access; §2.1 direct slice access (base value reads, modifier stacks, single-modifier-by-id); §3 subscriptions split into 3.1 per-variable / 3.2 structural / 3.3 per-mutation slice events / 3.4 entity removal / 3.5 lifetime; §4.5 batched mutations contract. Speculative items (untyped handle, variable enumeration, cross-entity query, spawn events, definition-swap observation) deferred — no current consumer in any snippet.
- **Batch 2** (`engine/04`): §5 ref-keyed `Get<TState>(reference)` and `EnumerateAll<TState>()` patterns; §6 Subscriptions — four channels (`Subscribe<TState>(handler)` for singletons, ref-keyed `Subscribe<TState>(reference, handler)`, `SubscribeAllReferences<TState>`, `SubscribeAny`) with `StateChangeEvent` enum; §7 Atomic Batches (`ExecuteBatch` contract — atomicity, mixed payloads, ordering, no-reads-during rule); §8 invariants 6–7; §9 follow-ups (symmetric unsubscribe for `SubscribeAll*`, `IAggregateProvider`).
- **Batch 3** (`entities/02` + `GLOSSARY`): §8.2 cross-context apply-aura example — modifier built in effect via `scope.Modifiers.Create`, dispatched via action payload, installed by handler; §1 InstanceId-vs-ActorId note linking to glossary; `GLOSSARY.md` sharpened `Actor` row, added explicit `ActorId` entry, rewrote `Instance ID` row to contrast.

**Cascade debt:** 2 entries opened against `effects/04` §4.3 → `effects/01` §IEffectScope sketch and `actions/01` §IEffectScope sketch (both missing `Entities` + `Modifiers` fields). Tracked as `ad-20260502-001` and `ad-20260502-002`.

Skipped from original cascade:
- `features/engine/models/02-gamerules-contract.md` §service table — `GameRules`-specific (3 services); `IModifierFactory`/`IEntityRegistry` live on `IHandlerContext`/`IEffectScope` and are fully covered by the effects/04 patch.

Pending external work:
- **Scaffold update** — extend `EntityModifierEntry` with optional `Id`/`Source` fields; `IMutableEntity.AddModifier(entry)` honours them when set, falls back to `ModifierId.New()` and `null` source otherwise. Single method, no overload. User will land this in the `MgCohen/Scaffold` repo separately.

Key Scaffold facts used during the walk:

- `EntityVariableState(BaseValues, ModifierStacks)` — `BaseValues` map is *override-only*; `ResolveEffectiveValues` falls back to `definition.TryGetDefaultValue(var)` for any variable not in the map.
- `WithModifier` inserts ascending by `Modifier.Order` (FIFO ties); `WithoutModifiersFromSource` sweeps by source equality.
- `ModifierSource(InstanceId source, int tag = 0)` — `Source` is required and non-null; `Tag` is an optional disambiguator. Equality on the `(Source, Tag)` tuple.
- `ModifierId(Guid id)` with `ModifierId.New() => Guid.NewGuid()` — non-deterministic by default. Card Framework cannot use this directly.
- Six payloads exist: `AddModifierPayload`, `RemoveModifierPayload`, `RemoveModifiersBySourcePayload`, `SetBaseValuePayload`, `AddEntityVariablePayload`, `RemoveEntityVariablePayload`.

## Part 3 — Decisions settled

- Q1 = C — both mutation surfaces supported; `IMutableEntity` via `StateEntity` is the recommended default for handlers, raw payloads via `ctx.Store.Execute(...)` are the documented fallback for batch authoring, history-replay tooling, and handle-less paths.
- Q2 = A — `AddEntityVariablePayload` is reserved for variables *not* declared in the entity's definition (per-instance ad-hoc state: token counters, custom buff bookkeeping). Definition-declared variables stay as definition defaults until written via `SetBaseValuePayload` or stacked via `AddModifierPayload`.
- Q3 = C — `SetBaseValuePayload` is live, but reserved for out-of-band base changes (definition-default override at creation, level-up / progression hooks, save-state load, authoring tools). Effect-tracked changes always go through modifiers so source attribution and removal are preserved.
- Q4 = A — `ModifierSource.Source` = the attachment entity's `InstanceId` (equipment, aura, status, ongoing-effect attachment per doc 24); `Tag` defaults to 0 and is opt-in author disambiguation for "one source contributing multiple modifier groups." Cleanup-on-source-death dispatches `RemoveModifiersBySourcePayload(target, new ModifierSource(attachment.Id))` per affected target.
- Q5 = factory pattern — engine provides a deterministic `IModifierFactory` service on `IHandlerContext` and `IEffectScope`. `ctx.Modifiers.Create(key, modifier, source)` returns a fully-populated `EntityModifierEntry` with a deterministic `ModifierId` baked in. Counter is engine-owned, snapshotted with engine state, restored on `LoadSnapshot`. Handlers call the factory and pass the entry to `entity.AddModifier(entry)`; constructing `EntityModifierEntry` directly (no-id form) is forbidden in framework / handler code. Reshaped from the original Q5 = B (`NextId`) once reading the bridge revealed `StateEntity.AddModifier` hardcodes `ModifierId.New()` internally; the user committed to a Scaffold update extending `EntityModifierEntry` with optional `Id`/`Source` so the factory's output flows through `IMutableEntity.AddModifier(entry)` cleanly.

## Part 4 — Outcome

`oq-entity-modifier-api` resolved. Five Q1–Q5 conventions on the Scaffold bridge documented in `entities/01` §"Mutation API conventions"; new `entities/02-runtime-surface.md` Model covers the full game-author API (read, subscribe, mutate, lifecycle, authoring reference, two worked examples). Slice-level subscription and atomic-batch operations documented in `engine/04` §6–§7. `ActorId` vs `InstanceId` convention explicit in `GLOSSARY` and at the entity-handle entry point. Stale prose in `actions/01` and `effects/03` patched to use the new surfaces.

After three batches the entity-and-state operations inventory has zero remaining ✅-eligible gaps for any operation an existing snippet uses; remaining items are deferred to known threads (`oq-engine-builder`, `bl-authoring-feature`, zones feature) or marked speculative pending a real consumer.

### Active question going out — patch cascade

`ad-20260502-001` and `ad-20260502-002` track the divergence between `effects/04` §4.3 (authoritative `IEffectScope` shape with `Entities` + `Modifiers`) and the older non-authoritative sketches in `effects/01-async-dispatch.md` and `actions/01-command-pipeline.md` line ~2028. Discharge in a future debt-pass.

### Follow-up open

- **Engine wiring for `IModifierFactory` + `IEntityRegistry`.** The Models commit to deterministic counters and a registry surface; the actual implementation (counter packing into `Guid`, snapshot slice placement, registry storage) is engine-implementation work. Pairs naturally with `oq-engine-builder`.
- **Scaffold update on `EntityModifierEntry`.** Adding optional `Id`/`Source` fields. User will land this externally; framework Models already document the assumed shape.
- **Authoring layer.** `EntityDefinition` Inspector authoring, custom-game-definition serialization, validation, definition versioning are out of scope here — captured under `bl-authoring-feature`.
- **Speculative entity-surface operations.** A4 untyped handle, A10 variable enumeration, A12 cross-entity query, C5 spawn events, C6 definition-swap observation — deferred until a real consumer (presentation projection, replay tool, zones feature) surfaces the need.

