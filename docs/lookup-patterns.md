# Lookup patterns

Conventions for keyed lookups in code under `Assets/Framework/`. Every registry, store,
cursor, and dispatch table is one of **four named patterns** across **two families**, split
on one axis: **frozen at composition** vs **live and handler-written**. Pick the pattern by
what the lookup *is*, not by what's convenient at the call site.

## Family A — Frozen maps

Built once at composition, immutable after, hold no mutable session state.

### Registry — discriminator → domain identity

Maps a small, authored set of "kinds" to canonical members.

- **Key:** `int Slot` (and/or C# `Type` for code members).
- **Operations:** `Register<T>()`, `Register(member)`, `BySlot`, `TryBySlot`, `Of<T>()` (code members only).
- **Ownership:** a scoped instance (DI singleton per scope) — **never a static global**. Static
  registries can't be isolated in tests and leak state across server/client scopes.
- **Today:** `EntityTypeRegistry`, `TagRegistry`, State's kinds.

### Router — discriminator → handler(s)/channel

Routes a message to its handler or channel. Holds **no domain state** — pure dispatch.

- **Key:** `Type` or `byte`.
- Built from registrations/DI; immutable after wiring.
- **Standardize the contract, not the class.** Implementations may specialize the key
  (`Type` vs `byte`), arity (single vs list), and backing (dict vs array) for performance —
  the byte-indexed array on the hot receive path is deliberate. Don't force them onto one base.
- **Today:** `HandlerRegistry` (`Type → handlers`), `Network._byType` (`Type → channel`),
  `Dispatcher._channels` (`byte → channel`).

## Family B — Live data

Mutable, written **only** by wire handlers, reconstructable from the wire, identical on both sides.

### Store — id → entry (many)

A keyed repository of entries — definitions or live instances.

- **Reads are canonical:** `ById` (throws on miss), `TryById` / `TryGetById` (try-get). These
  do the same thing everywhere, so they carry the same name.
- **The write verb signals its dup-contract — don't force one word.** `Insert` is
  idempotent/overwrite (apply-handlers re-run safely on wire replay); `Add` is strict (throws on
  a duplicate — authored-once sets like `Catalog` where a dup is a config bug). A typed read
  (`Get<T>` returning `null` on miss, e.g. `RequestStore`) is a genuinely different contract, not
  a misnamed `ById`. Reverse lookup (`IdOf`/`GetId`) only where a store genuinely needs it.
- **Standardize the contract, not the class** (as with Router). Stores back onto different
  containers (plain `Dictionary`, composite `Map`, single Cell) and their write/dup-contracts
  diverge by use case; the only identical surface is the read pair. That's too thin to justify a
  shared base — there is **no `Store<T>` base**. Conform by convention, not inheritance.
- **A Store never allocates ids.** The id comes from data (`def.Id`) or from a **server-only
  authoring service** that owns the allocator (e.g. `EntityService`, `RequestService`).
- **Writes are `internal` (apply-handlers only, via `InternalsVisibleTo`); reads are `public`.**
  Game logic authors through services (which emit) and queries through read-only views
  (`IReadOnlyEntityStore`, …, surfaced on `GameEngine`); only apply-handlers mutate the store.
  This is what makes "the handler is the sole writer" a compile-time guarantee instead of a
  convention.
- **Today:** `EntityStore`, `Catalog`, `RequestStore`, `RequestService._pending`, `TagStore`.
  `TagStore` is a per-value-type set of typed maps (`Map<int,int,T>` keyed `(entityId, slot)`,
  one per registered value type) — values stored inline, **no boxing**.

### Cell — a single value

The cardinality-1 case of a Store: same write-discipline, just one value.

- **Operations:** `Get`, internal `Set`.
- **Today:** `StateStore` (`Current`); future "current turn / phase / active player".

## Type-set authoring (a Registry's members)

Type-sets — the things a Registry holds — are declared as **records over a shared
`TypeKey(int Slot)` base** and registered at the composition root. Two authoring paths,
always both available:

- **Code:** `record Card : EntityType(4)` → `Register<Card>()`. The record *is* the identity —
  usable as `Of<Card>()` and `is Card`, and (for zero-data sets like State) as a generic verb,
  e.g. `Push<PlayingState>()`.
- **Data:** asset → instance → `Register(instance)`. Identity only.

Code authoring is the **superset**; data authoring is **identity-only** — a thing that doesn't
exist at compile time can't be a generic argument, and that's the only thing it gives up. The
wire carries only the `int` slot; receivers rehydrate via `BySlot`. Registration is **explicit**
at the composition root (framework baselines in Bootstrap layers, game types in game layers) —
no constructor side-effects, no static field-init.

**Members are not creation APIs.** The standard governs *storage, identity, and registration* —
not how instances are made. Entities, for example, are created from their *definition* (which
carries its `EntityType`), not via a `Create<Card>()` generic; the kind comes from the data. Add
a per-set verb only where a real consumer needs one (State's `Push<TState>()`), never for symmetry.

### Tags carry a value dimension

A `Tag<T> : Tag : TypeKey` adds an irreducible value type on top of identity. Two consequences:

- **No single-generic call form.** `Set<Power>(eid, value)` is *not expressible* in C# — the
  value type can't be inferred from the tag type alone (would need C# 11 static-abstract members,
  unavailable in Unity). Use instance-based ops where `T` infers from the key:
  `Set<T>(eid, Tag<T> key, T value)`, `Get<T>(eid, Tag<T> key)`; `Has`/`Remove` take the
  non-generic base key. Keys resolve via `Of<Power>()` (or game-side static aliases).
- **Value types are code-only** (each needs a wire channel + handler + the per-`T` store map).
  **Tag keys are code or data** — a key is `(slot, value-type)` with the value type chosen from
  the code-registered set.

> **Rule of thumb:** a type-set is data-authorable iff its wire discriminates by `int` slot,
> not by C# `Type`. Identity-only sets (`EntityType`, `Tag` keys) extend in both code and data;
> behavior-bearing sets (`State`) lean code. For Tags: data adds keys over existing value types;
> new value types are code.

## State conforms

State is two lookups wearing one feature's name:

- its **kinds** (`PlayingState`, …) → **Registry** (slot-based),
- its **current cursor** → **Cell**.

This puts State on slot-discriminated wire (one `StatePushEvent { int Slot }` + registry)
instead of type-discriminated (`StatePushEvent<TState>`, one channel per state) — which also
collapses N channels/handlers down to one.

## Current gaps (code vs standard)

The conformance PRs (PR1–PR6, see [conformance-plan.md](conformance-plan.md)) closed the registry,
type-set-record, State-slot-wire, and Store-discipline gaps. One lookup remains off-standard:

| Lookup | Pattern | Gap |
|---|---|---|
| `Session._peers/_players/_ready` | Store (ad-hoc) | list + linear scan → keyed Stores (low priority, session-scoped) |
