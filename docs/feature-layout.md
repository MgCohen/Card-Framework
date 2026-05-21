# Feature layout

Where Framework features live, how they're structured, and how spikes graduate into them.

## Where things live

- **Graduated features** → `Assets/Framework/<Feature>/`. Locked design, used or planned for use.
- **Exploratory work** → `Assets/Spike/<Spike>/`. Deletable by definition.
- **Shared primitives** → `Assets/Framework/Core/` (IDs, math, attribute markers — pure data, zero behavior). If it has behavior, it belongs in a feature.

## Vertical slice

Every Framework feature is a vertical slice with up to four asmdefs. Skip any layer the feature doesn't need:

```
Assets/Framework/<Feature>/
  Runtime/   Framework.<Feature>.asmdef
  Editor/    Framework.<Feature>.Editor.asmdef
  Tests/     Framework.<Feature>.Tests.asmdef
  Sample/    Framework.<Feature>.Sample.asmdef
```

The layout mirrors Unity's UPM package convention so a feature can be lifted out as a package later.

## Rules

- **Naming.** Asmdef file and assembly name: `Framework.<Feature>[.Editor|.Tests|.Sample]`. PascalCase, dotted. Root C# namespace matches.
- **One asmdef per layer, with one exception.** Inside `Runtime/`, split by subfeature folders (`View/`, `Service/`, …) as needed, but stay one asmdef. The exception is **optional-dep sub-asmdefs**: when a subfeature pulls in a heavy or optional package that the rest of the feature doesn't need, give it its own asmdef under `Runtime/<SubFeature>/Framework.<Feature>.<SubFeature>.asmdef`. Consumers who don't need that package skip the sub-asmdef. `Framework.Multiplayer` uses this for the Relay / Lobby / Discovery sub-features (each pulls in Unity Services packages the core doesn't touch). Never split Runtime into multiple asmdefs just to organize code — only to gate optional deps.
- **Dependency direction.** A feature's `Runtime` may depend on other features' `Runtime` only. `Editor`, `Tests`, `Sample` are leaves — nothing depends on them.
- **Validation-only slices.** Features that exist mainly as internal harnesses (e.g. Wire) still live under `Framework/`; the demo/harness goes in `Sample/`.

## Spike → Framework promotion

A spike graduates when **both** are true:

1. Design is locked (no open questions; tests/sample pass).
2. Another feature needs it, or it's the immediate target of upcoming work.

On graduation: **move** the code (don't copy), delete the spike folder, update references in one PR. Spikes that never meet the bar stay as spikes — that's what spikes are for.
