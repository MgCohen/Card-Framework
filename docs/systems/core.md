# Core

Core gives every feature one way to declare a closed set of typed keys and look them up by a stable integer slot.

**Code:** `Assets/Core/` · assembly `Framework.Core` · depends on `Scaffold.Records` (no Framework assemblies)

## What it does
- Defines `TypeKey`, an abstract record whose `Slot` is the key's identity on the wire and in stores.
- Defines `SlotRegistry<TKey>`, which maps slot to key and, for code-defined keys, type to key.
- Rejects a second key that claims a slot already in use.
- Serves as the base for `Tag` (Tags), `EntityType` (Entities) and `GameState` (States).
- Has no engine references and no state beyond the two dictionaries.

## How it works
1. A feature declares an abstract record over `TypeKey`, such as `EntityType(int Slot) : TypeKey(Slot)`.
2. Concrete keys are sealed records that fix the slot, such as `Card() : EntityType(4)`.
3. The feature subclasses `SlotRegistry<TKey>` (for example `EntityTypeRegistry`) and registers its members.
4. `Register<T>()` creates the key and indexes it by slot and by type. `Register(member)` indexes it by slot only, for keys that come from data.
5. Apply handlers resolve a slot from an event with `BySlot`. Code resolves a known key with `Of<T>()`.

Records compare by value, so `new Card() == registry.BySlot(4)` holds. Keys stay cheap to create anywhere.

## Key types
| Type | Job |
|---|---|
| `TypeKey` | Abstract record base. Carries the integer `Slot`. |
| `SlotRegistry<TKey>` | Slot and type lookup for one key family. Throws on a duplicate slot. |

## Example
From `Assets/Core/Tests/SlotRegistryTests.cs`:
```csharp
abstract record Kind(int Slot) : TypeKey(Slot);
sealed record Alpha() : Kind(1);

var reg = new SlotRegistry<Kind>();
reg.Register<Alpha>();

Assert.IsInstanceOf<Alpha>(reg.BySlot(1));
Assert.AreSame(reg.BySlot(1), reg.Of<Alpha>());
```

## Gaps
- `SlotRegistry<TKey>` cannot enumerate its members or unregister one.
- `Of<T>()` throws a bare `KeyNotFoundException` for keys added with `Register(member)`. Data-defined keys have no type path.
- Nothing coordinates slot ranges across features or between framework and game keys. Slot numbers are chosen by hand (for example the test tags use 1001 to 1003).
