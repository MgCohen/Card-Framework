# Card Framework — repo guide

Two things you might be doing here as an AI agent:

- **Designing or refining the framework.** This project uses the Product Builder workflow. Your operating manual is [`PRODUCT/README.md`](PRODUCT/README.md). On any new conversation, read it on first contact, then `PRODUCT/SESSION-CHECKPOINT.md` and the most recent `PRODUCT/sessions/session-NN.md`, then act. Don't read the workflow spec (`PRODUCT/general/PRODUCT-BUILDER.md`) for daily operation — the operating manual is sufficient.
- **Working on Unity code under `Assets/`.** Respect Unity conventions (scenes, prefabs, ScriptableObjects). Prefer small, reviewable changes; avoid drive-by refactors. The code under `Assets/` is the artifact `implemented` Models in `PRODUCT/` point at — don't change it without knowing which contract it satisfies.

`RESEARCH_BACKUP/` is a pre-migration snapshot. Read-only, never edit.

## Scaffold (packages and tooling)

When wiring UPM entries or pulling internal packages, **Scaffold** is the source of truth — not third-party guesses.

- Details and paths: `DOCS/scaffold.md`.
- Quick refs: local `C:\Unity\Scaffold`, remote https://github.com/MgCohen/Scaffold, example consumer `C:\Unity\Gear-Engine`.

If asked to "check Scaffold" or "get X from Scaffold," browse local `Assets/Packages/` first, then GitHub; mirror Gear-Engine's `manifest.json` pattern for new dependencies.
