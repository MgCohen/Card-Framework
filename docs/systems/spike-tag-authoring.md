# Tag Authoring Prototype (deleted spike)

A `ScriptableObject` layer that lets designers author typed tags as assets ("Pattern C") that share one slot space with code-defined tags.

**Code:** deleted. Last version at commit `b13907e`, [`Assets/Spike/ECS/TagAuthoringPrototype/`](https://github.com/MgCohen/Card-Framework/tree/b13907e/Assets/Spike/ECS/TagAuthoringPrototype). Built May 2026.

## What it does
- Defines one asset type per value type: `IntTag`, `BoolTag`, `FloatTag`, `StringTag`, each with a `[CreateAssetMenu]` entry under "Tags".
- Each asset holds a string `Id` (the wire label), a hand-picked `Slot` (convention: 1000+) and an optional `TagPresentation` (icon, label, tooltip, color).
- A bake step turns each asset into a `VerticalSlice.Engine.Tags.TagKey<T>`, which claims its slot in the global `TagRegistry` next to Pattern A and Pattern B tags.
- Call sites read the same for both kinds: `entity.Set(healthTag, 30)` and `entity.Set(Tags.Health, 30)`.
- UI code looks up presentation from a key, slot or id. The engine never reads presentation.
- A demo MonoBehaviour and an editor menu (`Tools/TagAuthoringPrototype/Build Sample Scene`) show code and data tags side by side. 39 EditMode tests.

## How it works
1. A designer creates a tag asset and fills in `Id`, `Slot` and optional presentation.
2. At app start, code calls `TagAuthoringRegistry.Bake(defs)`. It skips nulls, throws on an empty `Id` or a slot ≤ 0, then calls `Materialize()` on each def.
3. `TagDefinition<T>.Materialize()` creates `new TagKey<T>(Slot, Id)` once. The `TagKey` constructor registers with `TagRegistry`, which throws on a slot collision.
4. The registry stores the def by slot and by id for `GetDefinition(...)` lookups.
5. Gameplay code passes the def where a `TagKey<T>` is expected, through an implicit conversion or the `Entity` extension overloads. Reading `Key` before bake throws.

## Key types
| Type | Job |
|---|---|
| `TagDefinition` | Abstract `ScriptableObject` base: `Id`, `Slot`, `Presentation`, `ValueType`, `Materialize()`. |
| `TagDefinition<T>` | Typed base; caches the `TagKey<T>` and converts implicitly to it. |
| `IntTag` / `BoolTag` / `FloatTag` / `StringTag` | Concrete asset types with menu entries. |
| `TagPresentation` | Serializable UI block; `IsEmpty` checks icon, label and tooltip only. |
| `TagAuthoringRegistry` | Static bake step plus slot / id / key lookup of defs. |
| `EntityTagDefinitionExtensions` | `Get` / `Has` / `Set` / `Remove` overloads that take a def. |
| `TagAuthoringDemo` | IMGUI demo; builds in-memory defs (slots 1001-1004) when no asset is wired. |

## Shapes worth keeping
A data asset materializes the same runtime key a code tag uses, once, and converts to it implicitly.
```csharp
public abstract class TagDefinition<T> : TagDefinition
{
    [NonSerialized] TagKey<T>? _key;
    public TagKey<T> Key => _key
        ?? throw new InvalidOperationException($"TagDefinition '{Id}' (slot {Slot}) is not baked. ...");
    public sealed override Type ValueType => typeof(T);
    public sealed override TagKey Materialize() => _key ??= new TagKey<T>(Slot, Id);
    public static implicit operator TagKey<T>(TagDefinition<T> def) => def.Key;
}
```
Extension overloads let C# infer `T` from the def, because generic inference does not always apply the implicit conversion.
```csharp
public static T    Get<T>(this Entity e, TagDefinition<T> def)          => e.Get(def.Key);
public static void Set<T>(this Entity e, TagDefinition<T> def, T value) => e.Set(def.Key, value);
```

## Differences from the Framework
- The prototype builds `VerticalSlice` `TagKey<T>` objects with boxed values of any type. Framework tags are `unmanaged` only, so `StringTag` has no direct equivalent.
- Slots are hand-picked ints in one process-global registry. The Framework has no designer-authored tag assets yet.
- The tag's `Id` string is the wire label in `VerticalSlice` events. Framework events are `[Packed]` ints.

## Limits
- `TagRegistry` has no reset. Tests allocate slots from 70000 upward to avoid collisions across the Unity domain.
- Designers pick slots by hand. Nothing allocates slots or finds all tag assets at edit time.
- Tags carry no defaults or constraints (min, max, clamp). Those belong to archetypes, handlers and SBAs.
- Tests set the private serialized fields through reflection, since `InitRuntime` is `internal`.
