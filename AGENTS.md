# Card Framework — repo guide

Two things you might be doing here as an AI agent:

- **Designing or refining the framework.** This project uses the Product Builder workflow. Your operating manual is [`Product/README.md`](Product/README.md). On any new conversation, read it on first contact, then `Product/SESSION-CHECKPOINT.md` (the live spine), then act. `Product/sessions/` is a sparse milestone archive — read entries there only when explicitly relevant. Don't read the workflow spec (`Product/_workflow/PRODUCT-BUILDER.md`) for daily operation — the operating manual is sufficient.
- **Working on Unity code under `Assets/`.** Respect Unity conventions (scenes, prefabs, ScriptableObjects). Prefer small, reviewable changes; avoid drive-by refactors. The code under `Assets/` is the artifact `implemented` Models in `Product/` point at — don't change it without knowing which contract it satisfies.

`RESEARCH_BACKUP/` is a pre-migration snapshot. Read-only, never edit.

## Scaffold (packages and tooling)

When wiring UPM entries or pulling internal packages, **Scaffold** is the source of truth — not third-party guesses.

- Details and paths: `DOCS/scaffold.md`.
- Quick refs: local `C:\Unity\Scaffold`, remote https://github.com/MgCohen/Scaffold, example consumer `C:\Unity\Gear-Engine`.

If asked to "check Scaffold" or "get X from Scaffold," browse local `Assets/Packages/` first, then GitHub; mirror Gear-Engine's `manifest.json` pattern for new dependencies.
