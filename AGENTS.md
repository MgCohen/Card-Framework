# Card Framework — repo guide

Start with [`CLAUDE.md`](CLAUDE.md): the project map and the hard rules (branches, the gate, the rulebook).

- **How the code works:** [`docs/architecture.md`](docs/architecture.md), then one page per feature in [`docs/systems/`](docs/systems/README.md).
- **Working on Unity code under `Assets/`.** Each Framework feature is a folder directly under `Assets/`. Respect Unity conventions (scenes, prefabs, ScriptableObjects, `.meta` files). Prefer small, reviewable changes; avoid drive-by refactors. Update the feature's `docs/systems/` page in the same PR.

## Scaffold (packages and tooling)

When wiring UPM entries or pulling internal packages, **Scaffold** is the source of truth — not third-party guesses.

- Details and paths: [`docs/scaffold.md`](docs/scaffold.md).
- Quick refs: local `C:\Unity\Scaffold`, remote https://github.com/MgCohen/Scaffold, example consumer `C:\Unity\Gear-Engine`.

If asked to "check Scaffold" or "get X from Scaffold," browse local `Assets/Packages/` first, then GitHub; mirror Gear-Engine's `manifest.json` pattern for new dependencies.
