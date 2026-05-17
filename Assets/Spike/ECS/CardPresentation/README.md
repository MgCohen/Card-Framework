# Card Presentation

Standalone playground for **card + zone presentation** — view-layer shape on top of the (eventual) ECS world. Drag/drop between zones, VM-driven views, prefab-authored zones. Self-contained: no engine asmdef dependency, so the shape can iterate without disturbing the slice.

## What this spike proves

- **VM ↔ View separation.** `CardViewModel` is plain C# (no Unity coupling). `UICardView` is a prefab populated via `[SerializeField]` refs. Driver only builds data; view only renders.
- **Zone as first-class orchestrator.** `CardZone` abstract MonoBehaviour owns layout + per-card behavior defaults + card lifecycle. Subclasses (`HandZone` / `StashZone` / `FlippableZone` / `DisplayZone` / `PileZone`) are thin `Reset()` presets — all real behavior lives on the base.
- **Generalized config surface.** Serialized enums + bools on the zone base — `ZoneLayout` (Stack/Spread/Free), `ZoneVisibility`, `ZoneAnchoring`, plus `Draggable` / `Flippable` / `Droppable` / `GhostAnchor` / `MaxCards`. VM gets intrinsic override flags (`Revealed`, `Frozen`, `Pinned`) that beat zone defaults at configure time.
- **Drag/drop between zones works.** `CardZone : IDropHandler` — uGUI bubbles drop events from `CardTrack` to the zone root. `CanAccept(vm)` virtual hook gates transfers (combined with `Droppable` + `MaxCards`).

## High-level idea

```
[CardZone : MonoBehaviour, IDropHandler]
     ├─ ZoneLayout / Visibility / Anchoring     ← serialized config
     ├─ Draggable / Flippable / Droppable / MaxCards
     ├─ AcceptCard(vm) / ReleaseCard(vm) / CanAccept(vm)
     │
     └─▶ spawns UICardView prefab per VM
              ├─ CardViewModel (plain C#: Art, BodyText, Revealed, Frozen, Pinned)
              └─ uGUI tree (CardTrack, drag handles, flip)

[HandZone] [StashZone] [FlippableZone] [DisplayZone] [PileZone]
    │            │             │              │            │
    └──── each: Reset() preset on the base; no overrides of behavior ────┘
```

Five zone presets demoed side-by-side in the sandbox scene. Drag a card from Hand → Play Field; Play Field has `MaxCards = 3` to demo the rule gate.

## How to validate

1. Open the project in Unity.
2. Menu → `Tools/CardPresentation/Build Sandbox` rebuilds zone prefabs + `Sandbox.unity` from scratch (or open the scene directly if it already exists).
3. Open `Sandbox.unity` and Play.
4. Drag cards between zones. Watch the rule gate trigger when Play Field is full. Toggle Revealed / Frozen / Pinned on a VM and see the override semantics beat the zone defaults.

Self-contained: nothing else in the project needs to be running.

## Spike doc

Built from [`Product/spikes/card-presentation.md`](../../../../Product/spikes/card-presentation.md) — incremental design-pass log (5+ steps), uGUI quirks discovered, the rationale for the VM/View/Zone trichotomy, and what's parked (2D/3D renderer split, animation routing, world-space variants). Status: **active**.
