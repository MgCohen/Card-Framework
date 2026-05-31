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

- **Operations:** `Insert(id, …)`, `ById`, `TryById`, `Remove` (reverse `IdOf` only where genuinely needed).
- **A Store never allocates ids.** The id comes from data (`def.Id`) or from a **server-only
  authoring service** that owns the allocator.
- **Today:** `EntityStore`, `Catalog`, `RequestStore`, `RequestService._pending`,
  `TagStore` (composite key `(entityId, slot)`).

### Cell — a single value

The cardinality-1 case of a Store: same write-discipline, just one value.

- **Operations:** `Get`, internal `Set`.
- **Today:** `StateStore` (`Current`); future "current turn / phase / active player".

## Type-set authoring (a Registry's members)

Type-sets — the things a Registry holds — are declared as **records over a shared
`TypeKey(int Slot)` base** and registered at the composition root. Two authoring paths,
always both available:

- **Code:** `record Card : EntityType(4)` → `Register<Card>()`. Gets generics
  (`Create<Card>()`, `Of<Card>()`, `is Card`) and may add behavior or a value dimension.
- **Data:** asset → instance → `Register(instance)`. Identity only.

Code authoring is the **superset**; data authoring is **identity-only** — a thing that
doesn't exist at compile time can't be a generic argument, and that's the only thing it gives
up. The wire carries only the `int` slot; receivers rehydrate via `BySlot`. Registration is
**explicit** at the composition root (framework baselines in Bootstrap layers, game types in
game layers) — no constructor side-effects, no static field-init.

> **Rule of thumb:** a type-set is data-authorable iff its wire discriminates by `int` slot,
> not by C# `Type`. Identity-only sets (`EntityType`, `Tag` keys) extend in both code and
> data; behavior-bearing sets lean code.

## State conforms

State is two lookups wearing one feature's name:

- its **kinds** (`PlayingState`, …) → **Registry** (slot-based),
- its **current cursor** → **Cell**.

This puts State on slot-discriminated wire (one `StatePushEvent { int Slot }` + registry)
instead of type-discriminated (`StatePushEvent<TState>`, one channel per state) — which also
collapses N channels/handlers down to one.

## Current gaps (code vs standard)

| Lookup | Pattern | Gap |
|---|---|---|
| `EntityTypeRegistry`, `TagRegistry` | Registry | static global → scoped instance; unify `Register<T>`/`Register(inst)`; drop `TagKey` ctor self-registration; drop `EntityType.Name` |
| `EntityType` | Registry member | `readonly struct` → record over `TypeKey`; delete the `EntityTypes` static table |
| State | Registry + Cell | type-discriminated → slot-discriminated wire |
| `EntityStore` | Store | remove `AllocateId` + auto-id `Insert`; allocation moves to `EntityService` |
| all Stores | Store | align verbs on `Insert` / `ById` / `TryById` / `Remove` |
| `Session._peers/_players/_ready` | Store (ad-hoc) | list + linear scan → keyed Stores (low priority, session-scoped) |

Out of scope: `TagStore`'s value **boxing** (`Map<int,int,object>`) is a storage-representation
issue tracked separately, not a pattern question.
