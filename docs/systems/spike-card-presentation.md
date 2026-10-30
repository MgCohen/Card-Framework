# Card Presentation (deleted spike)

A standalone uGUI playground for cards and zones, where views derive from a world model and drops become intents.

**Code:** deleted. Last version at commit `b13907e`, [`Assets/Spike/ECS/CardPresentation/`](https://github.com/MgCohen/Card-Framework/tree/b13907e/Assets/Spike/ECS/CardPresentation). Built May 2026.

## What it does
- Shows four zones (Deck, Hand, Play Field, Graveyard) in `Sandbox.unity`. Cards drag between zones.
- Keeps three layers apart: `CardViewModel` (plain C#), `UICardView` (prefab MonoBehaviour), `CardZone` (layout and per-card defaults).
- Layers card behavior: the zone sets defaults, VM flags (`Revealed`, `Frozen`, `Pinned`) override them, and the rule layer's legal moves gate dragging.
- Routes every move through `GameSystem.TrySubmit`. Views never write the world.
- Has no engine dependency. Ships 33 EditMode tests over `World`, `GameSystem` and `CardViewModel`. `Tools/CardPresentation/Build Sandbox` regenerates the prefabs and scene.

## How it works
```text
drag + drop on zone -> CardZone.OnDrop -> CanAccept(vm) -> view.Claimed = true
  -> vm.RequestMove(zoneId) -> GameSystem.TrySubmit (checks ValidActions)
  -> World.SetCardZone -> refresh VM -> CardChanged
  -> GameSystem.Recompute -> ValidActionsChanged -> zones re-run ConfigureCard
  -> every CardZone.Rebind: keep views still here, spawn arrivals, destroy leavers
     (skip Claimed views; their OnEndDrag destroys them)
```
`GameSystem` subscribes to `CardChanged` before the zones do, so legal moves are fresh when zones rebind.

## Key types
| Type | Job |
|---|---|
| `World` | Owns `CardEntity` records and one cached VM per card. Fires `CardChanged`. |
| `CardEntity` | Plain card data: id, `ZoneId`, display fields, intrinsic flags. |
| `GameSystem` | Builds `ValidActions` (`MoveAction` rows) from a zone-pair allow-list. Stack zones expose only the top card. `TrySubmit` applies a legal move. |
| `CardViewModel` | Display fields with change-checked setters and one `Changed` event. Routes `RequestMove`, `CanMoveTo`, `HasAnyMove` to the system. |
| `UICardView` | Renders a VM. Owns drag, flip, anchor-back and the ghost placeholder. |
| `CardZone` | Abstract zone: serialized `ZoneLayout`, `ZoneVisibility`, `ZoneAnchoring`, flags and `MaxCards`. Diff-rebinds from the world. Implements `IDropHandler`. |
| `DeckZone` / `HandZone` / `PlayfieldZone` / `GraveyardZone` | Presets that only set defaults in `Reset()`. |
| `PresentationSandbox` | Seeds the world and wires each zone with `SetWorld`. |

## Shapes worth keeping
One method merges zone defaults, VM overrides and rule-layer legality into the view's toggles.
```csharp
protected virtual void ConfigureCard(UICardView view, CardViewModel vm)
{
    bool hasMove = vm.HasAnyMove();
    view.Draggable   = hasMove && _draggable && !vm.Frozen;
    view.Actionable  = hasMove;
    view.AnchorsBack = (_anchoring == ZoneAnchoring.AnchorBack) || vm.Pinned;
    view.FaceDown    = (_visibility == ZoneVisibility.Hidden) && !vm.Revealed;
    view.Flippable   = _flippable;
    view.GhostAnchor = _ghostAnchor;
}
```
A drop only claims the view and sends an intent. The zone change arrives later from the world.
```csharp
public void OnDrop(PointerEventData e)
{
    var dragged = e.pointerDrag != null
        ? e.pointerDrag.GetComponent<UICardView>() : null;
    if (dragged == null || dragged.Model == null) return;
    if (!dragged.IsDragging) return;
    var source = dragged.OwningZone;
    if (source == null || source == this) return;
    if (!CanAccept(dragged.Model)) return;
    dragged.Claimed = true;
    dragged.Model.RequestMove(ZoneId);
}
```

## Relation to the Framework
- Informs a future Framework presentation feature. None exists yet. Zone membership needs a zone store, and sending intents needs a client `Submit` path. The Framework has neither.
- `World` and `CardChanged` become the client's read-only stores (`IReadOnlyTagStore`, the entity store, a future zone store) and a signal raised after apply handlers run.
- `GameSystem` splits in two. Legality moves to the server engine and reaches the client as data (open item `oq-legal-actions-shape`). `TrySubmit` becomes a client submit with no local write, so a claimed view must wait for the server's event.
- `CardEntity` string fields should come from card definitions in a `Catalog`. The per-card VM sits under the per-state view-models from ADR-0012, which ADR-0014 says need review first.

## Limits
- Rules are a hard-coded zone-pair table with zone ids 1 to 4 duplicated in `GameSystem` and `PresentationSandbox`.
- `World.AddCard` does not fire `CardChanged`, so the sandbox calls `Recompute` by hand after seeding.
- No animation, no drop-position insert or in-zone reorder, no hover, and no 2D or 3D world-space renderer.
- The README names presets (`StashZone`, `FlippableZone`, `DisplayZone`, `PileZone`) that the code no longer has. The spike doc names `GameSystem.MoveCard`, which is `TrySubmit` in the code.
