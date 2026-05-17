---
type: spike
status: active
forward-link: Product/features/presentation/presentation.md
tags: [#spike, #presentation, #cards, #zones, #reactive, #ui]
---

# Card presentation — spike

## Premise

The engine work has settled the data + command + stream + mirror loop end-to-end (see `vertical-slice-plan.md`). What's still open is how *card and zone presentation* hangs together on top of that — what shape the view layer takes, where drag/drop/flip live, how view state stays reactive against world mutations, and what's reusable when we eventually need 2D / 3D renderers alongside uGUI.

A standalone playground that exercises card-table interactions (deck, hand, play field, graveyard, drag/drop between them) before committing to a presentation model. Live under `Assets/Spike/CardPresentation/`; entirely self-contained, no dependency on the slice.

## What I tried

Worked through this incrementally via design-pass sessions. Each step kept the previous renderable so we could press Play and see the impact.

1. **Card-centric programmatic UI** (initial). Single `CardSandbox` MonoBehaviour built four rows of cards in code, each row demoing a behavior combo (draggable, anchor-back, flippable, etc.). Validated the toggles but conflated layout, behavior, and game-state into one driver.

2. **Decoupled VM + prefab-driven view.** Pulled out a typed `CardViewModel` (no Unity-internal coupling) and refactored `UICardView` to be a prefab populated via `[SerializeField]` refs. The view layer learned to only render data; the driver only built data.

3. **Zone as first-class orchestrator.** Promoted the row concept to a `CardZone` abstract MonoBehaviour: owns layout, decides per-card behavior defaults, manages the card lifecycle. Subclasses (`HandZone` / `StashZone` / `FlippableZone` / `DisplayZone` / `PileZone`) became thin `Reset()` presets — all real behavior lived on the base. Prefab-per-zone authored via an editor menu (`Tools/CardPresentation/Build Sandbox`).

4. **Configuration surface generalized.** Folded all zone behaviors into serialized enums + bools on the base: `ZoneLayout` (Stack / Spread / Free), `ZoneVisibility` (Visible / Hidden), `ZoneAnchoring` (Free / AnchorBack), plus `Draggable` / `Flippable` / `Droppable` / `GhostAnchor` / `MaxCards`. The VM gained intrinsic override flags (`Revealed`, `Frozen`, `Pinned`) that beat zone defaults at configure time.

5. **Drag-drop between zones.** `CardZone` implements `IDropHandler`; uGUI bubbles drop events from `CardTrack` to the zone root. Source/destination transfer via `Claimed` flag on the dragged view, `ReleaseCard` / `AcceptCard` on zones. Discovered several uGUI quirks (see findings).

6. **`CanAccept(vm)` rule gate.** Added a virtual hook combining `Droppable` with `MaxCards`, called from `OnDrop` before transfer. Play Field demos this with `MaxCards = 3`.

7. **Table-layout demo scene.** Switched from the scroll-based config showcase to a positioned four-zone table (Deck / Hand / Play Field / Graveyard) — the shape of a real card game. Subclasses retired down to the four that actually appear.

8. **Reactive World → VM → View flow.** Final restructure: drops no longer mutate zone state directly. The drop emits an intent through the dragged card's VM, the intent routes through `GameSystem` to `World`, the world fires `CardChanged`, all zones diff-rebind their card lists from the world. The view layer became fully *derived* from world state.

## Findings

### Architectural locks

- **Card owns implementation, zone owns defaults, VM owns overrides.** Drag / flip / anchor handlers live on `UICardView`. The zone decides which toggles are *on* by default for its slot. Per-card intrinsics on the VM (`Revealed` / `Frozen` / `Pinned`) override the zone defaults. This three-layer split survived every iteration.

- **Subclass-per-variant is presets-only.** All zone behavior lives on the base; subclasses exist for prefab identity and a single `Reset()` that pre-fills the defaults. Avoids both god-config (one configurable type with branching) and deep hierarchies (one class per behavior combo).

- **Reactive flow direction.** `view → intent → VM.RequestMove → GameSystem.MoveCard → World.SetCardZone → CardChanged → zones.Rebind`. The view layer is derived; mutations only land through the system. The shape matches the eventual engine wire-up — replace `GameSystem` with a real rule layer + replace `World` with the engine's entity store and the rest stays.

- **Smart diff-rebind, not tear-down rebind.** On `CardChanged`, zones don't destroy and respawn everything. They keep views for cards still in this zone, spawn for arrivals, destroy departures. Mid-drag views (`Claimed = true`) are skipped — their `OnEndDrag` self-destructs.

- **Per-zone card prefab.** Each zone holds its own `_cardPrefab` reference. A card moving between zones is rendered by the destination's prefab — automatic visual swap with zero special-casing.

- **`CanAccept(vm)` is where rules live.** Virtual gate combining `Droppable` + `MaxCards`, called before the drop emits its intent. Subclasses can override for kind-specific rules ("spells only", "matches the active player's color", etc.) when the engine layer arrives.

### uGUI mechanics caught the hard way

- **`pointerDrag` is set on interface presence, not behavior.** Even if `OnBeginDrag` early-returns, uGUI still tracks the GameObject as `pointerDrag`. Result: a face-down (non-draggable) card whose drag is rejected by our handler still arrives at a drop receiver as `pointerDrag`. Gated drops on an explicit `IsDragging` flag exposed by the card.

- **`raycastTarget` on the receiver's surface.** `CardTrack` (the card area) must have `raycastTarget = true` so drops land somewhere uGUI can find `IDropHandler` through hierarchy bubble. Setting it to `false` (because "the Image is decorative") silently breaks drops.

- **`CanvasGroup.blocksRaycasts` during drag.** The dragged card sits between the cursor and the drop receivers underneath; without setting `blocksRaycasts = false` at `OnBeginDrag`, no receiver ever fires.

- **`SetParent(.., worldPositionStays: false)` after a drag leaves anchored-position garbage.** With LayoutGroup parents this gets overwritten on the next layout pass; without one (Stack / Free layouts), the card lands wherever the screen-space position math placed it — usually off-screen. Capturing `anchoredPosition` at `OnBeginDrag` and restoring at `OnEndDrag` is the fix.

- **Face-down and Draggable are orthogonal.** Initial implementation gated drag on `_faceDown` (intuition: hidden cards aren't grabbable). Real CCGs let you draw from a face-down deck — they're independent axes. Drag respects `Draggable` + `Frozen`; visibility is a different question.

### Layout discipline

- **`LayoutGroup` defaults bite.** Unity 6's `HorizontalLayoutGroup` defaults `childForceExpandWidth = true` + `childControlWidth = true`. A button with `LayoutElement.preferredWidth = 120` next to a flexible-width title stretches across the screen unless you explicitly set `childForceExpand = false`. Every `LayoutGroup` we create now sets all four flags explicitly.

- **Ghost-anchor placeholders for slot preservation.** A `LayoutElement`-bearing invisible child slotted into the dragged card's home position at `OnBeginDrag`, destroyed at `OnEndDrag`. Without it, an HLG zone reflows when a card lifts — disorienting. Standard CCG pattern.

## Outcome

Active. The spike is a working prototype, not a settled design — but the architectural shape has converged enough that the eventual presentation model can absorb the locks above. Concrete graduation paths:

- Fold the **reactive flow direction** + **smart diff-rebind** into `features/presentation/presentation.md` once the existing per-`MatchState` ViewModel work and the per-entity card VM here can be reconciled. They're at different scopes (state-shape vs. card-shape) but share the same "view subscribes to a slice, presenter rebuilds on change" pattern.

- The **zone-as-orchestrator** + **subclass-as-preset** pattern is a candidate for its own model under `features/presentation/models/`.

- The **uGUI mechanics** findings are reference material, not architecture — could live as a notes file under support or stay here for provenance.

Still open: second renderer (2D / 3D world-space cards against the same VM), the actual engine adapter that replaces `World` + `GameSystem` with real state, animation / tween layer, drop-position-aware insertion + within-zone reorder, hover affordances.
