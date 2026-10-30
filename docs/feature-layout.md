# Feature layout

Where Framework features live, how they're structured, and how spikes graduate into them.

## Where things live

- **Framework features** → `Assets/<Feature>/`, directly under `Assets/`. Locked design, used or planned for use.
- **Exploratory work** → `Assets/Spike/<Spike>/`. Deletable by definition. When a spike is deleted, record how it worked in `docs/systems/spike-<name>.md`.
- **Shared primitives** → `Assets/Core/` (IDs, math, attribute markers — pure data, zero behavior). If it has behavior, it belongs in a feature.
- **Not features** → `Assets/Scenes/`, `Assets/Settings/`, `Assets/TextMesh Pro/` (Unity assets) and `Assets/Tools/` (gate tooling). Don't add a `Runtime/` folder directly under these.
- **Global tests** → `Assets/Testing/` (acceptance + integration tiers that span features, sharing a neutral `Support/` module). This is **not** a feature — it observes the framework from outside. A feature's own `Tests/` layer (below) stays per-feature and local. See [testing.md](testing.md).

## Vertical slice

Every Framework feature is a vertical slice with up to four asmdefs. Skip any layer the feature doesn't need:

```
Assets/<Feature>/
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
- **Validation-only slices.** Features that exist mainly as internal harnesses (e.g. Wire) are still features under `Assets/`; the demo/harness goes in `Sample/`.
- **Docs.** Each feature has a page in `docs/systems/<feature>.md`. Update it in the same PR as the code.

## Spike → Framework promotion

A spike graduates when **both** are true:

1. Design is locked (no open questions; tests/sample pass).
2. Another feature needs it, or it's the immediate target of upcoming work.

On graduation: **move** the code (don't copy), delete the spike folder, update references in one PR. Spikes that never meet the bar stay as spikes — that's what spikes are for.
