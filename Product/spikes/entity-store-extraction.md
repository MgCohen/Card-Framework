---
type: spike
status: validated
tags: [#spike, #entities, #world, #queries, #indices, #architecture, #benchmark]
---

# Entity Store Extraction — Query Service + Indices

> **Status: validated (2026-05-15).** Bench in `Assets/Spike/ECS/VerticalSlice/QueryBench/` measured the indexed shape against the LINQ baseline across three populations. Gate criterion (≥5× speedup at Hearthstone-sized / compound / mid-selectivity) met at **10.3–10.9× LINQ**. Two design changes the bench forced are folded into the recommendation; see [Bench results](#bench-results-2026-05-15). Extraction is cleared to proceed.

A redesign of how entity storage and entity queries sit in the engine. Pulls the entity dictionary out of `World` and onto a dedicated `EntityStore` service, mirroring how effects and commands already live on their own services. Once the store is the sole reader of entity state, it can maintain tag-indexed views and expose a low-allocation query API — and there's no public side-door to bypass it.

## Why this exists

Two things are colliding on the entity-based-architecture branch:

1. **Queries are about to explode in number.** The entity model leans on tags as the only discriminator — "all minions on `p1`'s board," "all face-up cards in any graveyard," "the entity carrying this aura's parent." Triggers, auras, SBAs, and targeting all re-ask the same shapes constantly. Today every such query is `world.Entities.Values.Where(...)`: a full-table LINQ scan, with a `WhereEnumerator` heap allocation per call.
2. **`World` is supposed to be a god-free zone** ([World.cs:16](../../Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/World.cs:16)). It already pushed verbs out onto services — `EffectService` for effects, `CommandService` for commands. But it still owns the entity dictionary *and* exposes it publicly:

   ```csharp
   public IReadOnlyDictionary<int, Entity> Entities => _entities;
   ```

   That public surface is the loophole. Any caller can sidestep whatever query API we build and run their own scan, which means indices can never be a hard invariant — they're just a fast path that callers may or may not take.

These two together make the extraction the right move *before* the index work, not after. If we add indices to the current `World`, we get the speed but not the discipline; if we extract first, the index API is the only door.

## The split

`World` shrinks back to genuine substrate. Three responsibilities total:

- `EventLog Log` — append-only wire stream.
- Block-id allocator + open-block stack — used by `EffectService` / `CommandService` to thread parent ids ([World.cs:32-58](../../Assets/Spike/ECS/VerticalSlice/Engine/Pipeline/World.cs:32)).
- That's it.

`EntityStore` takes over:

- `Dictionary<int, Entity>` and `_nextId`.
- `Create` / `Destroy` (currently on `World`).
- Tag-event subscription that emits `TagSetEvent` / `TagChangeEvent` to the log.
- Index registration (`IndexMarker`, `IndexBy`) and maintenance.
- The query API (`With`, `Where`, compound queries).
- `Get(id)` and any "all entities" enumeration (internal-only).

Authors never touch `EntityStore` directly. They reach it through the scope they already have — `ICommandScope`, `IEffectScope`, `IHookScope` — same as how they reach `EffectService` today. The store is wired into scopes at construction time alongside the world reference.

## What the surface looks like

```csharp
public sealed class EntityStore
{
    // Setup — bootstrap declares which tags get indexed.
    public void IndexMarker(TagKey<bool> key);
    public void IndexBy<T>(TagKey<T> key) where T : notnull;

    // Reads — the only public way to find entities.
    public Entity? Get(int id);
    public MarkerQuery   With(TagKey<bool> marker);             // struct enumerable
    public ValueQuery<T> Where<T>(TagKey<T> key, T value);      // struct enumerable

    // Writes — internal to services. Authors reach these through scopes.
    internal Entity Create();
    internal void   Destroy(Entity e);
}
```

`MarkerQuery` and `ValueQuery<T>` are `readonly struct`s wrapping `HashSet<int>.Enumerator` (itself a struct). Compound queries chain via `.And(...)` returning another struct enumerable, picking the smallest indexed set as the driver and filtering the rest against each entity's `Has`/`Get`. **No `IEnumerable<Entity>` returned anywhere on the public surface** — that's the lever; LINQ-flavoured callers can't form. If they need a list, they build it explicitly with a pooled buffer.

Scope surface stays familiar:

```csharp
// inside an effect / command / hook
scope.With(Tags.IsMinion)                              // marker query
scope.Where(Tags.Zone, ZoneId.Hand)                    // value query
scope.With(Tags.IsMinion).And(Tags.Owner, p1)          // compound
scope.Get(entityId)
```

Scope methods just forward to the store.

## How it works end-to-end

**Setup (Bootstrap).** Build `World`, build `EntityStore` (handed the world's `Log` so it can append tag events), call `IndexMarker` / `IndexBy` once per tag we want indexed (`IsMinion`, `IsCard`, `Zone`, `Owner`, `ParentZone`, …). Pass both into `EffectService` / `CommandService` so scopes can forward.

**Creating an entity.** `EntityStore.Create()` (called by a command via its scope, or by setup code) allocates the `Entity`, subscribes to its `TagSet` / `TagChanged` events, adds it to `_entities`, appends `EntityCreatedEvent`. No index work yet — the entity has no tags.

**Setting a tag.** `entity.Set(Tags.IsMinion, true)` fires the internal `TagSet` event. The store's handler does two things in order:
1. Appends `TagSetEvent` to the log (existing behaviour, just relocated from `World.OnTagSet`).
2. If the tag's slot is indexed, updates the bucket: `_markerIndex[slot].Add(entity.Id)` for markers, `_valueIndex[slot][value].Add(entity.Id)` for value indices.

`TagChanged` does the same plus removing from the old bucket when a value index moves.

**Querying.** `store.With(Tags.IsMinion)` returns a `MarkerQuery` over `_markerIndex[Tags.IsMinion.Slot]`. The query's enumerator walks the HashSet (already exactly the live minion ids), resolves each to `Entity` via the entity dict, yields. Zero allocations.

**Destroying.** Walk the entity's `_slots`; for each indexed slot, remove the id from its bucket. Then remove from `_entities`, unsubscribe events, append `EntityDestroyedEvent`.

The index is **always coherent with the dictionary** because the only write paths into either go through `EntityStore`, and both updates happen in the same call.

## Why this beats "just add indices to World"

| | Indices on current `World` | Extracted `EntityStore` |
|---|---|---|
| Speed | Same — HashSet lookup either way | Same |
| Allocation | Same — struct enumerator either way | Same |
| Bypass risk | `world.Entities.Values.Where(...)` works and skips the index | Public surface doesn't expose `IEnumerable<Entity>`; no LINQ entry point exists |
| Discipline | Indices are a fast path *if you use them* | Indices are the only path |
| Consistency with rest of engine | `World` keeps growing verbs | Matches `EffectService` / `CommandService` pattern — `World` stays substrate |
| Diagnostics | Two places to ask "where do entities live?" | One |

The bypass risk isn't theoretical — it's how most of the test files read entities today (`world.Entities.Values.Where(...)`). That's fine for tests, but if it stays the idiomatic shape, hot-path code copies it. Closing the door now is cheaper than policing it later.

## Migration shape

Roughly:

1. Add `EntityStore` with the public/internal split. Move `_entities`, `_nextId`, `Create`, `Destroy`, `Get`, the tag-event handlers. Keep `World.Log` as-is, and pass it into the store's ctor so the existing wire events still fire from the same `Log` reference.
2. Drop `World.Entities`, `World.All()`, `World.Where(marker)`. (`World.Create` / `World.Destroy` get removed too — callers move to the store via scope.)
3. Add `IndexMarker` / `IndexBy` to `EntityStore`. Bootstrap declares them.
4. Add `With` / `Where` / `Get` to each scope interface, forwarding to the store. Update existing call sites in tests + sample cards (`Fireball`, `OnCast`, `DeathSBA`, etc.).
5. Tests that reach into `world.Entities.Values` rewrite to `store.With(...)` or to a deliberately-allocating helper if they really want a materialized list for assertion.

Tests are the bulk of the diff. The engine-internal change is small.

## Compound query — design research

This is the one piece of the sketch that doesn't reduce to "relocate existing code." It's worth its own pass because retrofitting the wrong shape is more painful than the rest combined.

### Constraints

- **Unity 6 (6000.4) → C# 9.** No `[InlineArray]` (C# 12), no `required` members (C# 11), no primary ctors. `Span<T>`, `ref struct`, and `stackalloc` are available. `allowUnsafeCode` is `false` on `VerticalSlice.Engine.asmdef`, so no `fixed` buffers.
- **Heterogeneous predicates.** `.And(TagKey<bool>)` and `.And<T>(TagKey<T>, T)` mix in any order. Predicate carries a slot id plus either "just check `Has`" (marker) or "check `Equals(value)`" (value).
- **Realistic depth.** Survey of likely card-game queries: 1-3 predicates total ("my minions on board," "face-up cards in any graveyard owned by opponent"). 4+ is rare. **Design for 4, accept degradation past that.**
- **Goal isn't zero allocation per se.** The actual win is iteration count (200-entity scan → 8-entity scan). If a compound query allocates a tiny array of predicates once per call but still iterates the small driver set, the math still wins by a wide margin. Don't contort the design to hit zero alloc if the cost is ergonomics.

### Options on the table

**A. Generic type chain — `Q<T1>`, `Q<T1, T2>`, …**

```csharp
scope.With(Tags.IsMinion)                          // → Q
     .And(Tags.Owner, p1)                          // → Q<PlayerId>
     .And(Tags.Zone, ZoneId.Board)                 // → Q<PlayerId, ZoneId>
```

Pros: typed, no boxing, no allocation. Cons: type names explode, can't store the result in a variable usefully, IDE hovers become unreadable. Reuse across call sites is awkward. **Reject** — the ergonomics tax compounds across every author writing a card.

**B. Readonly struct with 4 named predicate fields**

```csharp
public readonly struct Query
{
    private readonly HashSet<int> _driver;
    private readonly Dictionary<int, Entity> _entities;
    private readonly Predicate _p0, _p1, _p2, _p3;
    private readonly int _count;

    public Query And(TagKey<bool> marker) => /* copy + append */;
    public Query And<T>(TagKey<T> key, T value) where T : notnull => /* copy + append, boxes value */;
}

private readonly struct Predicate
{
    public readonly int Slot;
    public readonly object? Expected;   // null = marker (just check Has)
    public Predicate(int slot, object? expected) { Slot = slot; Expected = expected; }
}
```

Pros: zero heap allocation; passes by value; iteration enumerator stays a struct. Cons: boxes the predicate value once per `.And<T>(key, value)` call (one box per chain link, not per entity); hard cap at 4 predicates; copying a struct with 4 predicate fields on each `.And` is ~80 bytes copied per chain step. **Workable, slightly ugly.**

**C. `ref struct` query with stackalloc'd predicate buffer**

```csharp
public ref struct QueryRef
{
    private readonly HashSet<int> _driver;
    private readonly Dictionary<int, Entity> _entities;
    private Span<Predicate> _predicates;
    private int _count;
}

// call site
Span<Predicate> buf = stackalloc Predicate[8];
foreach (var e in scope.Query(buf).With(Tags.IsMinion).And(Tags.Owner, p1)) { ... }
```

Pros: truly zero allocation including the `_predicates` storage; predicate count flexible up to buffer size. Cons: every call site has to write the `stackalloc` line — **brutal** ergonomics; `ref struct` can't be captured in lambdas, fields, or async; one stray store of the query into a non-ref-struct context = compile error the author won't understand. **Reject** for author-facing surface; viable as an internal-only fast path.

**D. Pooled `List<Predicate>` reused per call**

```csharp
public Query Build() => new Query(_driver, _entities, _predicatePool.Rent());
// query.Dispose() returns it; foreach disposes via using
```

Pros: unlimited depth; predicate list stored once and reused. Cons: requires `using var q = ...` or explicit dispose; pool is process-state (thread safety, lifetime questions); a forgotten dispose leaks slots. **Adds a footgun the engine doesn't have today.** Reject unless option B's 4-predicate cap actually bites.

**E. Pre-built `QueryDefinition` registered at setup**

```csharp
static readonly QueryDef MinionsOnBoardOf = QueryDef
    .With(Tags.IsMinion)
    .And(Tags.Zone, ZoneId.Board)
    .Parameter<PlayerId>(Tags.Owner);                  // ← bind at call time

foreach (var e in scope.Run(MinionsOnBoardOf, p1)) { ... }
```

This is the real-ECS shape (Unity DOTS, Arch, Flecs). Pros: predicates built once, no boxing per query, fits naturally with caching driver-set selection. Cons: heavier authoring; every distinct query shape needs a named definition; "dynamic" parameters (the player varies) need a binding step that re-introduces some of the same questions. **Right answer if we end up with hundreds of queries; overkill if we have dozens.**

### Recommendation

**Start with B (named predicate fields, cap 4).** It's the smallest design that gets us alloc-free *iteration* — which is the part that matters — at the cost of a one-time predicate-value box per `.And<T>` call. The 4-predicate cap will hold for the queries we can currently name in the slice.

If the benchmark (next section) shows the per-call boxing or the struct-copy cost dominates, escalate to **E** — but only then. We don't have evidence yet that this matters at our scale, and E significantly raises the authoring bar.

C and D are kept on the shelf as internal-only fast paths if a specific hot query needs them. Neither should be the default author surface.

## Validation plan

The whole extraction is gated on a benchmark. Build it first, in isolation, before touching `World`.

### `VerticalSlice.QueryBench` — standalone test assembly

A new asmdef under `Assets/Spike/ECS/VerticalSlice/QueryBench/`:

```json
{
  "name": "VerticalSlice.QueryBench",
  "references": [],
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": ["Editor"],
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

No reference to `VerticalSlice.Engine`. The bench builds its own minimal `Entity`, `Tag`, `EntityStore`, and query shapes so the prototype is provably free of accidental coupling to the live engine. If the bench wins on its own terms, we know the shape transplants; if it doesn't, we haven't touched anything yet.

### What's in the assembly

```
QueryBench/
├── VerticalSlice.QueryBench.asmdef
├── Model/
│   ├── BenchEntity.cs           // minimal entity with tag dict
│   ├── BenchTagKey.cs           // slot-based tag key
│   └── BenchWorld.cs            // dict + LINQ baseline
├── Implementations/
│   ├── Baseline_Linq.cs         // current world.Where(...).Where(...) shape
│   ├── Indexed_Single.cs        // marker / value index, single-predicate query
│   ├── Indexed_OptionB.cs       // readonly struct, 4 named predicate fields
│   └── Indexed_OptionC.cs       // ref struct + stackalloc buffer (for comparison)
├── Fixtures/
│   └── PopulationBuilder.cs     // synthetic populations, configurable shapes
└── Tests/
    ├── CorrectnessTests.cs      // every implementation returns the same set
    ├── AllocationTests.cs       // GC.GetAllocatedBytesForCurrentThread deltas
    └── ThroughputTests.cs       // Stopwatch over N iterations
```

### Populations

Three population shapes, each parameterised by total entity count:

1. **Hearthstone-sized.** 200 entities total. ~8 minions on board, ~10 in each hand, rest in decks/graveyards. Realistic for the target game.
2. **Big-board.** 500 entities. Larger boards, deeper graveyards. Stress mid-game with summon-heavy decks.
3. **Synthetic stress.** 5,000 entities. Not a real card game; verifies that the indexed shape scales linearly with result size, not population size.

For each population, queries at three selectivities:

- **High-selectivity** (~1% of population): "the active hero of player 1" — index returns a 1-2 entity set.
- **Mid-selectivity** (~5%): "my minions on board."
- **Low-selectivity** (~25%): "all cards owned by p1."

### Benchmarks to run

Per (population × selectivity × compound depth 1/2/3), four implementations side by side:

| Implementation | Iteration cost | Per-query allocation |
|---|---|---|
| `Baseline_Linq` | O(N) | LINQ enumerator + closure |
| `Indexed_Single` | O(K) | zero |
| `Indexed_OptionB` | O(K) + 3 predicate checks | one box per value-typed `.And` |
| `Indexed_OptionC` | O(K) + 3 predicate checks | zero (caller stackalloc) |

Three measurements per cell:
- **Wall time** for 10,000 iterations via `Stopwatch`.
- **Allocations** via `GC.GetAllocatedBytesForCurrentThread()` delta over 1,000 iterations.
- **Correctness** — every implementation must return the same id set for the same query (asserted in `CorrectnessTests`).

### Success criteria

Indexed shape ships only if, at the **Hearthstone-sized population with mid-selectivity and 2-predicate depth**:

- Wall time at least 5× faster than the LINQ baseline.
- Allocations per query at most 32 bytes for Option B (one box for one value-typed predicate).
- Option B and Option C agree on correctness across all populations.

If those numbers don't hold, the extraction is a wash and the spike goes back to draft. We do not pay the test-rewrite cost for a refactor that doesn't measurably help.

### Modeling the event-dispatch cost without touching production code

The bench's `BenchEntity` mirrors production `Entity` shape on purpose: it carries internal `TagSet` / `TagChanged` events that a store can subscribe to. That lets the bench measure the event-dispatch cost **without referencing `VerticalSlice.Engine` at all** — everything mirrored lives inside the bench's own assembly.

Two store variants run side by side per implementation:

| Variant | What it models |
|---|---|
| `BenchStore_Pure` | Index maintained by direct calls inside `Set`. No events. Optimistic upper bound. |
| `BenchStore_WithEvents` | Index maintained via `OnTagSet`/`OnTagChanged` subscription on `BenchEntity`, plus a fake-log `List<int>.Add` to model `_log.Append`. Same dispatch shape as production. |

The delta between the two columns **is** the event-dispatch cost. A few-percent gap means integration is safe; a chasm means we revisit before any production code moves. Zero changes outside `Assets/Spike/ECS/VerticalSlice/QueryBench/` to cover this.

### Validation deliverable

A short note appended to this spike — pass/fail against the success criteria, plus the actual numbers — before any extraction code lands on a feature branch. If pass: extraction proceeds. If fail: spike status flips to `rejected` with the numbers as the reason.

## Bench results (2026-05-15)

Ran via a standalone `dotnet test` host (`C:\tmp\QueryBenchHost\`) that compiles the bench `.cs` files into a net9.0 NUnit assembly — no Unity needed, no engine references. The bench code at `Assets/Spike/ECS/VerticalSlice/QueryBench/` is identical to what would run in Unity Editor.

### Throughput — compound query (broad-to-narrow author order)

Query: `With(IsMinion).And(Owner, P1).And(Zone, Board)` — the natural shape authors will write.

| Population (entities) | LINQ µs/q | OptionB Pure µs/q | OptionB WithEvents µs/q | Speedup |
|---|---|---|---|---|
| Hearthstone (200) | 11.49 | 1.12 | 1.06 | **10.3–10.9×** |
| BigBoard (502)    | 29.33 | 2.14 | 3.24 | **9.1–13.7×**  |
| Stress (5,002)    | 89.27 | 3.89 | 3.09 | **23–29×**    |

### Allocation — compound query

| Implementation | Bytes / query |
|---|---|
| LINQ baseline | 184 B (Where enumerator + closure) |
| OptionB (Pure or WithEvents) | **48 B** — one box per value-typed `.And` |

The 48 B/q lower bound corresponds to two boxes (one per value predicate; `PlayerId` and `ZoneId` each box at ~24 B on .NET 9). Halved from the first-cut 96 B/q by boxing the value once at `.And<T>()` and reusing the reference for both the bucket lookup and the predicate field.

### Mutation cost — Pure vs WithEvents (event dispatch + log append)

Building a Hearthstone-sized population (~200 entities, each with 3–6 tag writes):

| Variant | µs / population |
|---|---|
| `IndexedStore_Pure` (event-free index update) | 157 |
| `IndexedStore_WithEvents` (production shape) | 165 |
| **Delta** | **~5%** |

Event dispatch + log append cost the production design ~5% on the mutation path. Well within the "acceptable" budget — the spike's stretch concern about integration regressing the numbers does not bear out.

### Two design changes the bench forced

**1. Option C is dead.** `stackalloc T[N]` requires `T` to be an unmanaged type — the GC can't track refs in stackalloc'd regions. Our `OptionCPredicate` carried an `object?` (the predicate's expected value) which makes the struct managed, so `stackalloc OptionCPredicate[4]` fails to compile (`CS0208`). There is no clean way around this for heterogeneous predicate values. **Option C as drawn in the design-research section above doesn't work; removed from the bench.** This actually strengthens the case for Option B — it was the recommended path on ergonomic grounds anyway, and it's now the *only* viable struct-based path.

**2. Dynamic driver selection is the load-bearing optimization.** First-draft Option B drove off whichever predicate the author called first. For natural broad-to-narrow query shapes, this is exactly wrong: `With(IsMinion)` returns ~196 of 200 entities at Hearthstone scale (almost everything is a minion), so the "indexed" iteration was effectively a full-table scan. Early gate-test result with first-predicate-as-driver was **1.15× LINQ** — fail.

   Fix: each predicate carries its indexed `HashSet<int>` reference, and `GetEnumerator` picks the smallest set as driver at iteration time. The other predicates become per-entity filters. With this change, the same author-written shape went to **10.3× LINQ** at Hearthstone. The natural narrow-first ordering (`Where(Zone, Board).And(IsMinion).And(Owner, P1)`) hit ~11×, essentially the same — confirming dynamic selection makes author order irrelevant for perf.

   **This is now load-bearing in the design.** It means each `Predicate` carries an extra 8-byte HashSet reference, and `GetEnumerator` does a small-N min-find over the predicates' set counts. Cheap, but worth calling out — it's not in the first version of the sketch.

### What this means for the recommendation

- **Option B with dynamic driver selection is the design** that ships to production. Author writes broad-to-narrow; the framework picks the fast path.
- **Allocation cost is one box per value-typed predicate** — predictable, bounded, and dominated by the work saved on iteration. Don't chase further alloc reduction unless a specific hot query proves otherwise.
- **Cap of 4 predicates holds.** The bench used 3-deep compounds. No query in the slice's vocabulary needs more; the `InvalidOperationException` at >4 is a fine guardrail.
- **Production-shape (`WithEvents`) is essentially free** vs the optimistic lower bound — ~5% mutation overhead, query throughput indistinguishable. The event-driven index update path is the one we ship.

### Open questions resolved by the bench

- ~~"Does the per-call boxing on `.And<T>(key, value)` matter?"~~ No. 48 B/q, dwarfed by the 200ns saved per query at Hearthstone scale.
- ~~"Is event-dispatch + log-append cost acceptable in production?"~~ Yes. ~5% mutation overhead, no query-side cost.
- ~~"Is Option C worth the call-site ergonomics tax?"~~ Moot — Option C doesn't compile.
- ~~"Does the 4-predicate cap hurt at our scale?"~~ No. Card-game queries top out at 3-deep in the populations we tested.

### Bench artifacts

- Source: `Assets/Spike/ECS/VerticalSlice/QueryBench/` (asmdef, `noEngineReferences: true`).
- Standalone host: `C:\tmp\QueryBenchHost\QueryBenchHost.csproj` — pulls the bench `.cs` files via `<Compile Include="...QueryBench/**/*.cs" />`, no copy. Run with `dotnet test`. **Not checked in** — it's a throwaway host. Recreate from the .csproj snippet in this section if needed.
- 18 tests total: 3 correctness (all impls agree on id sets), 3 allocation gates, 12 throughput (×3 populations × multiple shapes including the 5× gate).

## Prior art — existing libraries surveyed (2026-05-15)

Before committing to our own implementation, web-searched for existing C#/.NET libraries that solve the same problem. The honest answer: **the query technique we landed on is well-trodden, but no off-the-shelf library fits our data shape.** Below is what's out there and why each doesn't drop in.

### The mainstream C# ECSes — Friflo, Arch, DefaultEcs, Leopotam, Flecs.NET

All the popular C# ECS libraries [(benchmark comparison)](https://github.com/friflo/ECS.CSharp.Benchmark-common-use-cases) share the same fundamental shape:

- **Entities are component bags.** Components are strong types (`struct Position : IComponent`, `struct Velocity : IComponent`).
- **Tags are zero-data marker types.** In Friflo: `struct Pulsating : ITag {}`. You can ask "all entities with tag X" but the tag carries no value.
- **Queries discriminate by type membership.** "Give me all entities with `Position` and `Velocity` and tag `Active`." Storage is organised by archetype (or sparse set) for blazing iteration speed.

This is fundamentally different from our model. Our framework's deliberate design choice is:

- **Tags are data, not types.** `Zone = ZoneId.Hand` is a tag with a value. `Owner = PlayerId.P1` ditto. Mechanics are driven by reading tag values at runtime, not by type-dispatch on component presence. This matches how Sabberstone describes their "Tag-based Entities (ECS-lite)" approach in [Product/research/sabberstone-architecture.md](../research/sabberstone-architecture.md) — and how Hearthstone's GameTag system actually works in practice.
- **Queries discriminate by value, not just by presence.** "Minions where `Zone = Board` AND `Owner = P1`" is our bread-and-butter shape. In an ECS, you'd express this as a `Zone` component carrying a value and then post-filter with a lambda — which is the linear scan we're trying to avoid.

So we'd be **fighting the abstraction**, not using it, if we adopted any of these. The archetype storage trick is designed for component-set membership; it doesn't help when the discriminator is a value.

#### Performance reference points (Friflo is the high bar)

Even though we don't adopt it, [Friflo.Engine.ECS](https://github.com/friflo/Friflo.Engine.ECS) is worth pinning as a sanity check: it's the fastest of the mainstream ECSes on compound queries — **48 µs for a 5-component compound query over 100,000 entities** in the benchmark suite. That's archetype-storage-flavoured, type-driven dispatch, no boxing because components are typed.

Our bench hits **3.09 µs at 5,000 entities** (compound, 3-deep, value-typed predicates). Different shape, different problem — Friflo's number is for entirely different work — but it tells us our absolute numbers are in a reasonable neighbourhood for a tag-based design, not pathologically slow.

### Mindex — the closest architectural cousin

[Mindex](https://github.com/zumten/dotnet-mindex) is a general C# library for "indexed search over big in-memory collections." It's the closest existing thing to what we built:

- Each indexed field gets its own per-value lookup structure.
- Compound search via a `SearchCriteria<T>`-wrapped object combining multiple fields.
- **It does the same dynamic driver-selection optimization** — scores each candidate index and picks the most selective one for the compound query. Exactly the technique we discovered the hard way in the bench.

So our optimization isn't novel. It's the standard play for indexed in-memory queries — same heuristic database query planners use (smallest set as outer loop, filter via inner predicates).

Why we don't adopt Mindex:

- Built for **static, read-only datasets** — analyse once, query many. Our entity store mutates constantly (every tag write).
- Strongly-typed `SearchCriteria<T>` classes — works for fixed schemas, awkward for our open-ended tag bag where any entity can carry any tag.
- No event-driven index maintenance — would need to be wrapped to track mutations, which is most of what `EntityStore` does.

It's the right idea applied to the wrong domain for our needs. Useful as a sanity-check that the algorithm is well-known.

### LiteDB-style indexed in-memory collections

Embedded-DB libraries (LiteDB, RealmDB, IndexedDB-flavoured C# wrappers) all maintain B-tree indices over typed fields and do query-planner-style selection. Same algorithm, much heavier machinery; not the right scale for an entity bag inside a card game.

### What we keep from this survey

1. **The technique (dynamic driver selection over per-tag indices) is standard.** We're not inventing it. We're applying a well-trodden algorithm to a data shape that the existing libraries don't model.
2. **The data shape is the differentiator.** Tag-as-data is the framework's deliberate divergence from ECS. Every existing library normalises to tag-as-type, which doesn't fit. This is the reason we're building rather than adopting.
3. **Friflo's "QueryDefinition registered at setup" pattern** is the escalation path if we ever outgrow ad-hoc query construction. The spike's [Option E](#options-on-the-table) anticipated this; Friflo is the working reference for what that API looks like in practice.
4. **Mindex's autoindex-scoring** validates our dynamic driver selection. Worth a footnote in the eventual production code so future readers can find the prior art.

### Other query libraries surveyed (not just ECS)

The first prior-art pass leaned heavily on ECS frameworks because they're the obvious cousin. Second pass widened to general indexed-collection libraries:

- **[CodexMicroORM's `ConcurrentIndexedList<T>`](https://www.c-sharpcorner.com/article/indexing-in-memory-collections-for-blazing-fast-access/)** — multi-property indexed list with thread-safe concurrent reads. Compound queries done via `Union`/`Intersect` of per-property indexed lookups (not via a unified query expression). Reports **40–64× faster than LINQ baseline** on single-property lookups. Different compound-query model — separate-lookups-then-set-op vs our single chained query — and uses string `nameof` property names, which doesn't fit our open-ended tag bag. Useful corroboration that the index-per-property pattern is well-trodden outside the ECS world.
- **[Cysharp ZLinq](https://github.com/Cysharp/ZLinq)** — zero-allocation LINQ via `readonly ref struct` enumerators. **Not a replacement for our query layer** — it speeds up LINQ-style filtering on a flat collection but doesn't add indices. The right tool if we ever wanted "keep LINQ syntax, lose the allocation"; not the right tool when the goal is "skip the full-table scan via indices." Could be relevant for the few `LinqStore`-style call sites we keep (e.g., test assertions that materialize lists), if zero-alloc there ever matters.
- **Embedded DBs (LiteDB, RealmDB)** — full query planners with B-tree indices. Same algorithm family as us, much heavier machinery. Wrong scale.

None of these change the conclusion. The closest cousin remains Mindex; the others either solve a different problem (ZLinq), tackle the wrong cardinality (LiteDB), or commit to a static schema (`ConcurrentIndexedList<T>` with `nameof`). The widened survey reinforces the original verdict: build, don't adopt.

### Bottom line

We're not rebuilding the wheel — we're building a **different** wheel because no existing wheel fits the tag-bag shape. Our algorithm is the standard one, our absolute numbers are in a reasonable neighbourhood, and the closest cousin (Mindex) confirms our approach by independent convergence. Proceed with the extraction.

## Design rationale — why our tag model differs from ECS components

The prior-art survey above flagged a real question worth answering head-on: **what's actually different between our `TagKey<T>` and an ECS component?** Structurally, almost nothing — but the small difference that remains drives every other choice in the design. This section pins down the rationale so future readers don't have to re-derive it.

### Three patterns in the wild

Same idea — "minion on the board owned by P1" — expressed three different ways across the card-engine and ECS landscape:

#### Pattern A — tag-as-type (mainstream C# ECS: Friflo, Arch, DefaultEcs, Leopotam)

The tag *is* a type. Empty struct, no fields. Storage groups entities by their exact type-set (archetype).

```csharp
struct IsMinion : ITag { }
struct OnBoard  : ITag { }
struct OwnedByP1 : ITag { }

entity.AddTag<IsMinion>();
entity.AddTag<OnBoard>();
entity.AddTag<OwnedByP1>();

var query = store.Query().AllTags(Tags.Get<IsMinion, OnBoard, OwnedByP1>());
```

Values that need to be *read* (not just matched) live in components:

```csharp
struct Health : IComponent { public int Value; }
entity.Add(new Health { Value = 30 });
```

#### Pattern B — tag-as-int-enum (Hearthstone, Sabberstone)

The tag is a slot named by an enum. Value is always `int`.

```csharp
enum GameTag { ATK, HEALTH, ZONE, CONTROLLER, CARDTYPE, ... }

entity[GameTag.CARDTYPE]   = (int)CardType.MINION;
entity[GameTag.ZONE]       = (int)Zone.BOARD;
entity[GameTag.CONTROLLER] = (int)PlayerId.P1;

var minionsOnBoardOfP1 = entities.Where(e =>
    e[GameTag.CARDTYPE]   == (int)CardType.MINION &&
    e[GameTag.ZONE]       == (int)Zone.BOARD     &&
    e[GameTag.CONTROLLER] == (int)PlayerId.P1);
```

#### Pattern C — tag-as-typed-key (us)

The tag is a typed key. Identity is a *value* (a `TagKey<T>` instance) but the value type is enforced by the type parameter.

```csharp
public static readonly TagKey<bool>     IsMinion = new("IsMinion");
public static readonly TagKey<ZoneId>   Zone     = new("Zone");
public static readonly TagKey<PlayerId> Owner    = new("Owner");
public static readonly TagKey<int>      Health   = new("Health");

entity.Set(Tags.IsMinion, true);
entity.Set(Tags.Zone, ZoneId.Hand);
entity.Set(Tags.Owner, PlayerId.P1);

scope.With(Tags.IsMinion).And(Tags.Zone, ZoneId.Board).And(Tags.Owner, PlayerId.P1);
```

### Side-by-side — same query, three models

| | Storage | Query expression | Query cost (Hearthstone-sized) |
|---|---|---|---|
| **A: type tags** (Friflo) | Archetype bucketing | `Query.AllTags<IsMinion, OnBoard, OwnedByP1>()` | Sub-µs (pointer walk over archetype chunks) |
| **B: int-enum tags** (Sabberstone) | `Dict<GameTag,int>` per entity | Full LINQ `.Where(...)` scan | ~10 µs (full scan, manual checks) |
| **C: typed-key tags** (us, post-extraction) | `Dict<int,object?>` per entity + indices | `scope.With(IsMinion).And(Zone, Board).And(Owner, P1)` | ~1 µs (smallest indexed set + filter) |

Pattern A wins on raw speed but pays schema friction — every enum value becomes a type, every value change becomes an archetype migration. Pattern B wins on schema friction but loses type safety and indexing. **Pattern C is the deliberate middle ground.**

### The honest framing — our tag is an ECS component with value-identity instead of type-identity

This is the framing that took a beat to land but is worth pinning. Compare the declarations:

```csharp
// ECS component — identity is a TYPE
struct Health : IComponent { public int Value; }
entity.Set(new Health { Value = 30 });
int h = entity.Get<Health>().Value;
```

```csharp
// Our typed-key tag — identity is a VALUE
public static readonly TagKey<int> Health = new("Health");
entity.Set(Tags.Health, 30);
int h = entity.Get(Tags.Health);
```

Structurally these are **the same idea**. Both name a slot, both carry typed data, both let you read it back type-safely. **An ECS component IS a tag-with-data.** Our `TagKey<T>` is structurally an ECS component with one change: the identity has moved from being a compile-time type to a runtime value.

That single change cascades.

### Four things you can do when identity is a value

**1. Pass the slot identity around at runtime.**

```csharp
// In our model — slot identity is just a field reference
TagKey<int> any = condition ? Tags.Attack : Tags.Health;
entity.Set(any, 5);   // works

// In ECS — slot identity is a type, can't be selected at runtime
// without System.Type and reflection
if (condition) entity.Set(new Attack { Value = 5 });
else           entity.Set(new Health { Value = 5 });
```

This is what makes "effects-as-data" possible. A "deals damage" effect carries a `TagKey<int> targetTag` field; a "buffs stat" effect carries `TagKey<int> statTag`. Cards describe their mechanics with data, not generic parameters.

**2. The wire alphabet stays untyped.**

`TagSetEvent` is `(int slot, object value)` — every tag write serializes identically. No per-type codec, no type registry, no recompile to add a tag. ECS-shaped wire formats need a `ComponentChanged<T>` per component type or a reflection-backed dispatcher.

**3. No archetype migration on add/remove.**

In archetype ECS, adding `Buffed` to an entity that didn't have it physically moves the entity to a new archetype — copying all its other components into a new memory layout. A mid-game "every card gains +1/+1 this turn" (1000 buffs applied, removed at end of turn) is a thousand migrations. In our model both ops are one dict write.

**4. Markers and value-tags are the same mechanism.**

```csharp
TagKey<bool>   IsMinion = new("IsMinion");   // marker
TagKey<int>    Health   = new("Health");     // carries data
TagKey<ZoneId> Zone     = new("Zone");       // carries an enum
```

All declared, stored, and queried the same way. ECS uses two different machineries — empty-struct `ITag` for markers, non-empty `IComponent` structs for values. Our single mechanism halves the surface area authors learn.

### What this costs us

ECS components beat our tags on raw per-read performance. Honest accounting:

| | ECS component | Our `TagKey<T>` |
|---|---|---|
| Read | Pointer arithmetic into chunked array, no allocations | Dictionary lookup + unbox of `object?` |
| Write | Direct memory write, no allocations | Dictionary write + one box for value types |
| Cache locality | Excellent — chunked by archetype | Bad — `Dictionary<int, object?>` is scattered |
| Iteration | Pointer walk over chunks | Hash lookup per id |

For raw component-iteration workloads (1M physics objects), ECS wins by 10-100×.

For our workload (dozens of mutations per turn, queries hitting indexed buckets), the per-read box doesn't matter — the bench showed the entire event-dispatch + log-append + index-update path adds ~5% to the mutation cost, with the box buried inside.

### Why the trade is right for this domain

In order of how much each actually matters:

1. **Wire serialization stays trivial.** Tags round-trip as `(int, object)`. No type registry, no per-type codec.
2. **Effects-as-data.** Cards describe effects by referencing tags as runtime values. Pattern A requires reflection or generics-per-mechanic to express the same thing.
3. **Bridge / hook introspection.** Existing patterns (`DeathrattleBridge`, etc.) read tag values from event payloads by slot id, without knowing T. Untyped wire is load-bearing.
4. **No migration cost.** Buff for one turn = one dict write, not an archetype migration.
5. **One mechanism instead of two.** Markers and values share machinery.

For a card framework with these characteristics, **value-as-identity wins.** For a physics simulation where "iterate Position+Velocity at memory bandwidth" is the only question, type-as-identity wins. We're in the first kind of system, not the second — confirmed by the fact that every shipping card engine surveyed (Hearthstone, Sabberstone, MTG Arena, Slay-the-Spire) lands at Pattern B or C, never Pattern A.

### The premise this section commits to

Everything downstream in this spike — the index extraction, the dynamic driver selection, the per-tag bucket maintenance — only makes sense if Pattern C is the right model for the framework. If we ever revisit that and decide Pattern A is worth the migration cost, the extraction work in this spike gets thrown out and we'd port to one of the existing C# ECSes (Friflo being the strongest candidate). That's a much bigger change than the one this spike proposes, and nothing observed so far justifies it. **Pattern C stays. The extraction follows from it.**

## Boxing avoidance — investigated post-bench (2026-05-15)

The bench showed Option B allocates **48 B/q** in the gate scenario — two boxes for the two value-typed predicates (`PlayerId.P1` and `ZoneId.Board`). Followup question raised: could we avoid those boxes entirely with a wrapper like `ITagValue<T> : ITagValue`?

### Why the suggested `ITagValue<T> : ITagValue` doesn't help

The intuition is: parameterise tag values through a generic interface, store them via the non-generic parent. In C#:

```csharp
public interface ITagValue { object GetUntyped(); }
public interface ITagValue<T> : ITagValue { T Get(); }

public sealed class TagValueInt  : ITagValue<int>    { public int    Value; public int    Get() => Value; }
public sealed class TagValueZone : ITagValue<ZoneId> { public ZoneId Value; public ZoneId Get() => Value; }
```

Storage: `Dictionary<int, ITagValue>`. **This is strictly worse than boxing.** Every write allocates a wrapper *class instance* — same allocation cost as a box, plus extra type metadata and an interface dispatch on read. We've replaced one heap object with another, slightly larger, heap object. The interface generics don't buy us anything because storage is the non-generic `ITagValue`, which is just a reference.

Pooling wrappers doesn't rescue it: a wrapper instance has to live in the entity's dict slot for the lifetime of the value, so you can't reuse the instance unless you rewrite the value in place — which means mutable wrappers and a coordinated lifecycle. Not worth it.

### What does work — a discriminated-union struct

The realistic approach is a value-type union that stores primitives inline and reserves an `object` slot for reference values:

```csharp
public readonly struct TagValue : IEquatable<TagValue>
{
    public readonly TagKind Kind;        // discriminator
    public readonly long    Primitive;   // holds bool/int/byte-enum/float, etc.
    public readonly object? Reference;   // null for primitive kinds

    public static TagValue From(bool v)   => new(TagKind.Bool, v ? 1 : 0, null);
    public static TagValue From(int v)    => new(TagKind.Int,  v,         null);
    public static TagValue FromEnum<T>(T v) where T : unmanaged, Enum
    {
        long p = 0; Unsafe.As<long, T>(ref p) = v;
        return new TagValue(TagKind.Enum, p, null);
    }
    public static TagValue From(object o) => new(TagKind.Reference, 0, o);

    public bool   AsBool() => Primitive != 0;
    public int    AsInt()  => (int)Primitive;
    public T      AsEnum<T>() where T : unmanaged, Enum { long p = Primitive; return Unsafe.As<long, T>(ref p); }
}

public enum TagKind : byte { None, Bool, Int, Long, Float, Enum, Reference }
```

Size: ~24 B on 64-bit (discriminator + 8 B primitive + 8 B reference, padded). Stored *inline* in `Dictionary<int, TagValue>` because the dictionary's internal entry array holds value types unboxed. Read/write of value-typed tags becomes struct copy, no heap touch.

This is the standard pattern; [Unio](https://benjamin-abt.com/blog/2026/03/02/unio-high-performance-discriminated-unions-csharp/) and the upcoming C# 15 union types use the same shape (typed generic fields + discriminator byte). `Unsafe.As<TFrom, TTo>` for the enum case is what lets a single `long` slot hold any unmanaged value type up to 8 bytes without boxing.

### Where the savings would land

If we adopted `TagValue` throughout the stack:

| Path | Today | With `TagValue` |
|---|---|---|
| `entity.Set(Tags.Zone, ZoneId.Hand)` | 1 box (~24 B) into `Dict<int, object?>` | struct copy, 0 alloc |
| `entity.Get(Tags.Zone)` | dict lookup + unbox | dict lookup + struct deref |
| Predicate construction (`.And<T>(key, value)`) | 1 box per value predicate (currently 48 B/q) | struct copy, 0 alloc |
| Value-index bucket lookup | `Dict<object, HashSet<int>>` lookup (boxed key) | `Dict<TagValue, HashSet<int>>` lookup (struct key) |
| Predicate matching in enumerator | `Equals(rawObject, expectedObject)` virtual call | `tagValue.Equals(expected)` static struct call |

So: **zero-allocation queries become achievable, and mutations save a box per write.**

### Why we didn't do it (yet)

Two reasons, both worth being honest about:

1. **The 48 B/q allocation happens at query-construction time, not in hot iteration.** At our scale — call it 100 queries per turn (generous) — that's ~5 KB/turn from query boxing. A single gen-0 GC threshold is ~256 KB. We're 50× under that. Eliminating it would be aesthetic, not impactful.

2. **The deeper refactor — replacing `object?` slot storage everywhere — touches every store, every predicate, every index, every event payload (the wire layer also carries `object`).** Doable, but a much bigger change than the index extraction itself. Hard to justify without bench evidence that boxing is the bottleneck, which the current bench numbers say it isn't.

The honest accounting is: **this is a known follow-up optimization, not a blocker.** The right time to do it is when a real perf scenario shows the allocation pressure mattering — late-game with many active effects, or a streaming production trace from a real game. Not before.

### What this means for the spike

- The extraction proceeds with `Dictionary<int, object?>` storage and one box per value-typed `.And<T>()`. The 48 B/q is acknowledged and acceptable.
- A `TagValue` discriminated-union struct is **recorded as the next-tier optimization**. The implementation sketch above is concrete enough to land in a few hours; nothing about the current design blocks it later.
- The wire event signature (`TagSetEvent(int slot, object value)`) is the lever that gates the full conversion — converting that to `TagSetEvent(int slot, TagValue value)` would cascade through the mirror, serialization, and every existing test. If we ever do the conversion, do it as a single sweep, not incrementally.

If the bench is re-run later under production-trace pressure and shows query-construction allocation dominating, this is where to start. If not, leave it.

## Tradeoffs and open questions

- **Naming.** `EntityStore` vs `EntityService` vs `EntityManager`. `Service` aligns with `EffectService` / `CommandService`. `Store` reads better at call sites (`scope.Store.Get(...)` is dumb; we wouldn't expose it that way anyway, but the noun matters). Leaning `EntityStore` for the type, with scopes hiding the noun entirely.
- **`World` becomes very small.** `Log` + block tracking is all that's left. Tempting to fold those somewhere too, but block-id state genuinely is "global to the run" and doesn't belong on a service that conceptually scopes to entities. Leave `World` as the substrate seam; rename later if it stops earning the name.
- **Value-indices on value types box.** `Dict<object, HashSet<int>>` boxes `ZoneId`/`PlayerId` on insertion. For card-game cardinality (dozens of distinct zone ids ever, low write rate) this is fine. If we ever index by a high-churn value-type key, specialize with a generic value-index type.
- **Index opt-in vs opt-out.** Current sketch is opt-in (`IndexMarker` per tag at setup). Alternative: index every tag automatically and pay the bookkeeping cost everywhere. Opt-in keeps the cost honest and forces a decision per tag, which we want at this scale.
- **Index backfill.** If `IndexMarker`/`IndexBy` is called after entities already exist, the index starts empty. Two options: require all indices to be declared before any `Create` (enforce with a "frozen-after-first-create" flag), or have registration walk `_entities` and backfill. Backfill is one-time and trivial; enforce-order is louder when violated. Default to backfill, keep the option to flip later.
- **Driver-set selection for compounds.** Always pick the smallest indexed set as driver. For the bench, this is hardcoded — picking dynamically requires sampling set sizes per query, which is fine but worth measuring. If the benchmark shows the driver choice doesn't matter at our scale, skip the dynamic logic.
- **Does `EntityStore` own `EventLog`, or does `World`?** Sketch above keeps `Log` on `World` and hands the store a reference. Alternative: move `Log` onto the store, since the store is the thing emitting most events. Probably leave it on `World` — commands and effects also append to it, and a shared substrate seam is cleaner than threading the log through three services.

## What this unblocks

- Indexed queries become a hard invariant, not a fast path callers may forget.
- The "entity-based architecture" direction has a real home: every query that the new model wants to express has somewhere to live and a budget that holds at runtime.
- `World` stops drifting into a god object. Same move we already made for verbs; now repeated for state.
- Diagnostics get a single seam — "show me every entity with tag X right now" is one call against the store, not a recursive scan.
