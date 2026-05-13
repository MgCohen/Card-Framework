# Vertical Slice — How to Run

## Headless tests

1. Open the project in Unity 6 (`6000.4.0f1` or matching).
2. `Window → General → Test Runner`.
3. Switch to the **EditMode** tab.
4. Run all tests under `VerticalSlice.Tests`. All eleven should pass.

## Unity scene

1. Create a new empty scene (`Assets → Scenes` → new scene).
2. Create an empty `GameObject` in the hierarchy.
3. Attach `Assets/Sample/VerticalSlice/Unity/SliceBootstrap.cs` to it.
4. Press Play.

You should see three IMGUI panels:

- **Primary State** — the engine's live entity tags.
- **Mirror State** — rebuilt purely from the wire stream, with the current cursor position.
- **Event Log** — every wire entry the engine has emitted; `▶` marks entries the mirror has applied.

Above the panels:

- **Reset** — recreates the two entities (regenerates the entire event log from scratch).
- **Cast Fireball** — invokes `Fireball.Cast(world, caster, target)` which executes a `DealDamage` command.
- **Target HP = 25** — a direct (out-of-`Execute`) tag write; demonstrates the synthetic `BlockType.System` auto-wrap.
- **Divine Shield on Target** — toggle. With it on, casting Fireball should produce a `divineShield: true → false` TagChange and **no** HP change.
- **Scrub slider** — drag left to rewind the mirror through the log; drag right (or to the end) to fast-forward. The label flips to `(rewound)` when below head. The primary state panel is unaffected by scrubbing.

## Acceptance walk

The seven acceptance criteria from `Product/spikes/vertical-slice-plan.md`:

| # | Validate |
|---|---|
| AC-1 | After Reset, two entities visible in the Primary panel. |
| AC-2 | Click "Target HP = 25". Primary shows `hp = 25`. Mirror reflects. Event log shows `BlockStart(System) → TagChange → BlockEnd`. |
| AC-3 | Every action grows the Event Log panel. |
| AC-4 | Mirror always matches Primary when slider is at head. |
| AC-5 | Drag slider left. Mirror tags revert; with full rewind, mirror entities disappear. Drag right; they reappear. Primary panel does not change. |
| AC-6 | Click "Cast Fireball". Log shows `BlockStart(Power, DealDamage) → TagChange(target, hp, 30, 24) → BlockEnd`. Primary HP drops by 6. Mirror follows. |
| AC-7 | Toggle Divine Shield on. Click "Cast Fireball". Log shows `BlockStart(Power, DealDamage) → TagChange(target, divineShield, True, False) → BlockEnd`. HP unchanged on both panels. |
