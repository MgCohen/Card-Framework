# Stripping

Stripping holds the attributes that keep types and members in a stripped (IL2CPP) player, for assemblies that have no engine reference.

**Code:** `Assets/Stripping/` · assembly `Framework.Stripping` · no references (`noEngineReferences`)

## What it does
- `RequireDerivedAttribute` on a class keeps each derived class. `WireEvent` and `Block` use it, because channel ids depend on the full set of event types.
- `RequireImplementorsAttribute` on an interface keeps each implementor. `IRouted` and `ITypeKey` use it.
- `PreserveAttribute` keeps the marked member. `Network.Register<T,TPacked>` uses it, because `TryRegister` can reach it only through reflection.

## How it works
The UnityLinker matches these three attributes by class name, not by namespace. Thus an engine-free assembly can use them in place of the `UnityEngine.Scripting` attributes. Do not rename them. `StrippingAttributeTests` checks each name and target.

Channel ids follow the registration order. The attributes keep the same set of event types on both ends, but both ends must also run the same installs in the same order.

## Key types
| Type | Target | Keeps |
|---|---|---|
| `RequireDerivedAttribute` | class | each derived class |
| `RequireImplementorsAttribute` | interface | each implementor |
| `PreserveAttribute` | any | the marked member |

## Gaps
- A unit test cannot run the linker. Only a player build shows that the attributes work.
- `[RequireDerived]` keeps types, not members. If no code constructs a `WireEvent`, a stripped IL2CPP player can lose its nested `Packed` struct or its constructor. Registration then fails at startup with an error. Fix: the AutoPacker generator emits `[Preserve]` on `Packed` and on its constructor (Scaffold PR). Prove it with one IL2CPP player build with stripping level High.
- The asmdefs `Scaffold.Autopacker` and `Scaffold.Records` do not set `noEngineReferences` (Scaffold PR).
