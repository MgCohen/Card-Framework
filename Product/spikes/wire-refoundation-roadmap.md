---
type: spike-roadmap
status: active
created: 2026-05-18
tags: [#roadmap, #refoundation, #wire]
---

# Wire-First Refoundation Roadmap

Rebuild the framework outward from the wire layer. Wire is the most defining constraint and the hardest to retrofit; everything else can be designed against a stable foundation once it's locked. The original VerticalSlice locked everything *above* the wire while leaving the wire itself stubbed; this roadmap inverts that.

## Sequence

1. **Wire (AutoPacker sample).** Send → pack → unpack → receive. Validates pack/unpack symmetry, handler injection, schema-version basics. All six original WireEvent families round-trip with realistic payloads. → [`wire-autopacker-sample.md`](wire-autopacker-sample.md).
2. **Tag value + identity allocation.** Tag value typing redesigned against the new wire shape. Stable slot allocation (tag slots, entity type slots, definition ids) — the same registry problem solved once.
3. **Entities + definitions + catalogs.** Single concrete `Entity` revalidated against the new tag shape. Definition layer + per-type catalogs included.
4. **Service pattern.** Generalize the per-role service shape (command / effect / request / state). Decide what's load-bearing and what's incidental.
5. **Existing services reviewed.** State (from the StateMachine spike) and request services re-checked against the new service pattern. Most should pass with payload-shape changes only.
6. **Engine reassembly.** Verify the layers compose. Hooks (Before/After/Replacement) and block scopes preserved from VerticalSlice.
7. **Views.** Card presentation / VM layer. Can move in parallel from step 3 onward — already isolated from engine asmdef.

## Things that survive VerticalSlice review (probably)

- Hooks (Before / After / Replacement) and the Command/Effect dispatch model.
- Block-scope structure on the stream (BlockStart / BlockEnd as brackets).
- CardPresentation (VM/View/Zone) — independent of wire.
- Match-state push model (stateless states, per-role service) — payload-free wire event.

## Anchors

- The original VerticalSlice (`Assets/Spike/ECS/VerticalSlice/`) stays as reference; this is parallel work, not in-place migration.
- AutoPacker (`com.scaffold.autopacker`) is the source-gen layer; no hand-written codec table.
- `IPackingHandler` is the context-injection seam — catalogs, registries, services flow in here.
- First "real" implementation lives under `Assets/Framework/` (not `Assets/Spike/`).
