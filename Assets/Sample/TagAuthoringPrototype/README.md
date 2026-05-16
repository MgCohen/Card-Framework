# Tag Authoring Prototype

Pattern C — data-defined tags — for the typed-hybrid tag system. Demonstrates designer-authored `ScriptableObject` tags coexisting with code-defined Pattern A / Pattern B tags in the same process-global `TagRegistry`.

> Spike companion to [`Product/spikes/tag-identity-typed-hybrid.md`](../../../Product/spikes/tag-identity-typed-hybrid.md). Scoped to authoring + bake + presentation lookup. Constraints, defaults, and `TagRegistryAsset` (auto slot allocation) are explicitly out of scope.

## What's here

```
TagAuthoringPrototype/
├── Authoring/
│   ├── TagDefinition.cs           — abstract base SO + typed generic base
│   ├── IntTag.cs / BoolTag.cs / FloatTag.cs / StringTag.cs   — typed assets
│   ├── TagPresentation.cs         — optional UI metadata (icon / label / color)
│   └── TagAuthoringRegistry.cs    — bake step + slot→def lookup
├── Demo/
│   └── TagAuthoringDemo.cs        — MonoBehaviour + IMGUI panel
└── Editor/
    └── TagAuthoringSceneBuilder.cs — Tools menu: builds scene + sample assets
```

The prototype reuses `VerticalSlice.Engine.Tags.TagKey<T>` / `TagRegistry` and `VerticalSlice.Engine.Entities.Entity` — Pattern C is a thin authoring layer that drops into the existing substrate.

## Running it

**Two paths — pick one.**

### Path 1 — Out-of-the-box (no editor steps)

1. Open any scene with an empty GameObject.
2. Add `TagAuthoringDemo` component.
3. Leave the four `TagDefinition` fields blank.
4. Play.

`Awake` builds in-memory `TagDefinition` instances for the four unwired fields (slots 1001–1004) and bakes them. IMGUI panel shows Pattern A + Pattern C tags side by side, with presentation (color swatches, labels) for the data-defined ones.

### Path 2 — Authored assets (demonstrates the full editor authoring story)

1. Menu → `Tools / TagAuthoringPrototype / Build Sample Scene`.
2. Confirm save dialog if prompted.
3. The builder creates `Demo/_Generated/HealthTag.asset` + 3 siblings, builds `Demo/Sample.unity`, drops a `TagAuthoringDemo` GameObject in it, and wires the assets onto its fields.
4. Open `Demo/Sample.unity`. Inspector shows assets in the `TagAuthoringDemo` fields.
5. Play.

Now the prototype reads its tags from disk-authored assets. Edit `HealthTag.asset` → tweak label / color → enter play mode → see the change reflected in the panel.

## What the demo proves

| Property | How it shows up in the panel |
|---|---|
| Pattern A and Pattern C share one slot space | `demoLevel` (slot 500, code-defined) sits next to `health` (slot 1001, data-defined); both go through the same `TagRegistry` |
| Implicit conversion `TagDefinition<T> → TagKey<T>` | `entity.Set(healthTag, 30)` and `entity.Get(healthTag)` read identically to `entity.Set(Tags.Health, 30)` — no `.Key` boilerplate |
| Presentation lookup is decoupled from the engine | Color swatches + labels come from `TagAuthoringRegistry.GetDefinition(tag)?.Presentation`; the engine's `TagKey` doesn't know about it |
| Per-type authoring asset | `IntTag` / `BoolTag` / `StringTag` are separate `[CreateAssetMenu]` types — designer right-clicks → "Tags / Int Tag" etc. |
| Optional presentation block | `TagPresentation` is one optional inline block; empty fields = no UI authored |

## Authoring flow (designer perspective)

1. **Create.** Right-click in `Assets/` → Create → Tags → Int Tag (or Bool / Float / String). A new asset appears with default fields.
2. **Fill in.**
   - `Id` — stable string label (used on the wire and as a designer-facing handle).
   - `Slot` — pick a free int in the 1000+ range. Collision throws at bake.
   - `Presentation` (optional) — icon, label, tooltip, color.
3. **Wire.** Drag the asset onto any `[SerializeField] IntTag fooField;` in a controller, or load via `Resources.LoadAll<TagDefinition>(...)` at bake time.
4. **Bake.** Once at app start: `TagAuthoringRegistry.Bake(allAssets)`. Materializes `TagKey<T>` instances, claims slots in `TagRegistry`, fills the def-lookup map.
5. **Use.** From code, `entity.Get(fooField)` and `entity.Set(fooField, value)` read identically to code-defined tags. From visual scripting, the dropdown sees every baked `TagDefinition` alongside the code-defined keys (visual-scripting integration handled by your separate package).

## What this prototype does *not* do

Per design-pass discussion — explicitly scoped out:

- **No `TagRegistryAsset` (auto slot allocation).** Designers pick the slot int by hand. Range-pick + threshold automation can be revisited after a first version proves the shape.
- **No constraints (clamp / min / max / enum).** Tags are pure typed slots. Gameplay rules (Health ≤ MaxHealth, etc.) live where they belong — in handlers and SBAs, not in tag definitions.
- **No defaults on the tag.** Initial values are an entity-archetype / card-definition concern, not a tag concern.
- **No editor utility to find all `TagDefinition` assets at edit time.** The fallback path uses in-memory instances; the scene-builder hands assets to the controller explicitly. If a project-wide collect/load is wanted later, an editor utility can scan `AssetDatabase.FindAssets("t:TagDefinition")` and a runtime utility can `Resources.LoadAll<TagDefinition>(...)`.

## Future evolution

When a second extension axis shows up alongside `TagPresentation` (analytics, audit, network policy, etc.), the natural refactor is:

```csharp
// Today:
[SerializeField] TagPresentation presentation;

// Later, if needed:
[SerializeReference] List<TagModule> modules;   // TagPresentation becomes one module type
```

Mechanical migration; no call-site change in `TagAuthoringRegistry.GetDefinition(...)`.
