# Agent instructions — Card Framework

Use this file as the project anchor for AI-assisted work. Keep it factual and short; move detail into `DOCS/` or `RESEARCH/`.

## Folders

| Path | Use |
|------|-----|
| `PLANS/` | Active plans, milestones, task breakdowns, and decisions-in-progress. |
| `DOCS/` | Durable project docs: architecture, conventions, APIs, onboarding. |
| `RESEARCH/` | Notes, spikes, comparisons, links, and scratch investigation — promote stable findings into `DOCS/`. |

## Scaffold (packages and tooling)

When searching for internal packages or wiring UPM entries, **Scaffold** is the source of truth—not random third-party guesses.

- **Details and paths:** `DOCS/scaffold.md` (local clone, GitHub, `Assets/Packages/com.scaffold.*`, manifest URL shape, Gear-Engine as reference).
- **Quick refs:** local `C:\Unity\Scaffold`, remote https://github.com/MgCohen/Scaffold, example consumer `C:\Unity\Gear-Engine`.

If the user asks to “check Scaffold” or “get X from Scaffold”, follow that doc: browse local `Assets/Packages/` first, then GitHub; mirror Gear-Engine’s `manifest.json` pattern for new dependencies.

## Unity

- Game code and assets live under `Assets/`. Respect Unity conventions (scenes, prefabs, ScriptableObjects).
- Prefer small, reviewable changes; avoid drive-by refactors unrelated to the task.

## Optional setup (not created by default)

- **`.cursor/rules/`** — Persistent editor/agent rules (coding standards, file patterns).
- **`README.md`** — Human-facing repo overview if you open-source or onboard others.

When starting a large feature, add a short plan under `PLANS/` and link it here if it should always be considered.
