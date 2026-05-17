# Vertical Slice

Foundational tag-bag ECS — proves the **Entity / Command / Stream / Mirror** refoundation loop end-to-end in one Unity scene. Substrate that the other ECS spikes (EntitySetup, TypedPayloads, TagAuthoring, CardPresentation) build on or reference.

## What this spike proves

- `Entity` is one concrete class — `int Id` + sparse `TagKey<T> → value` dict. No subclasses. "Kind" is a marker tag (`IsCard`, `IsMinion`, etc.).
- `World` is god-free substrate: no verbs. Dispatch goes through per-role services on `GameEngine` (`CommandService`, `EffectService`, request, zone, player, …).
- Commands mutate state. Effects compose commands. Hooks subscribe via `CommandService` and run Before / After / Replacement.
- Every mutation flows into a **block-structured wire stream** (6 concrete `WireEvent` types, no shared base). A pure **Mirror** rebuilds world state from the wire alone — scrubbable, independent of Primary.
- The engine asmdef is Unity-agnostic; the 71-test suite runs headless from EditMode.

## High-level idea

```
[UI button]
   ↓ invokes
[Effect handler]
   ↓ issues
[Command]  ──opens──▶ [Block] ──emits──▶ [Stream]  ──feeds──▶ [Mirror]
   ↓ Apply
[Entity.Set] ──fires──▶ [TagChange event into Block]
   ↓
[Hooks Before/After] (Replacement priority can cancel/replace)
```

Six-card scenario: Fireball + Healing Touch + Tactician (Choose-One) cast through Jaina/Garrosh; Garrosh (Armor), Argent Squire (Divine Shield), Spite Imp (Backlash), Abomination + Tomb Tender (Deathrattle) each demo one hook category.

## How to validate

**Headless tests** — `Window → General → Test Runner → EditMode`, run `VerticalSlice.Tests` (71 tests, all should pass).

**Interactive scene** — Open `Unity/Sample.unity`, hit Play. Three IMGUI panels (Primary / Mirror / Event Log) plus cast buttons + a scrub slider. Full button-by-button walkthrough in [`Unity/README.md`](Unity/README.md).

Rebuild the scene any time via `Tools/VerticalSlice/Build Sample Scene`.

## Spike doc

Built from [`Product/spikes/vertical-slice-plan.md`](../../../../Product/spikes/vertical-slice-plan.md) — original 5-phase plan plus the architectural locks discovered post-plan (services, scopes, typed Effect payloads, first-class Request, zones/players). Status: **concluded 2026-05-14**.
