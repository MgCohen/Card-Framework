# Agent instructions — Card Framework

Use this file as the project anchor for AI-assisted work. Keep it factual and short; move detail into the relevant feature folder under `PRODUCT/`.

## Folders

| Path | Use |
|------|-----|
| `PRODUCT/` | Canonical knowledge layer. Status governs role: `draft` (in-flight), `settled` (the contract), `implemented` (code matches contract). Do not write outside the prescribed structure — if unsure, write a spike. |
| `PLANS/` | Ephemeral implementation slices. Internals are not workflow-specified. |
| `Assets/` | Unity game code and assets. Implementation lives here. |
| `RESEARCH_BACKUP/` | Pre-migration snapshot of the old flat `RESEARCH/`. Reference only; never edit. |
| `ProductBuilder/` | Living workflow-about-the-workflow docs (`MIGRATION-GUIDE.md`, `FLOW-SIMULATION.md`, `AI-FIRST-AUTOMATION.md`, `PRODUCT-BUILDER-GAPS.md`). The canonical workflow itself lives at `PRODUCT/general/PRODUCT-BUILDER.md`. |

## Workflow

The full workflow is `PRODUCT/general/PRODUCT-BUILDER.md`. Follow it when working in `PRODUCT/`. The condensed rules:

### `PRODUCT/` layout

```
PRODUCT/
  README.md                  # index
  SESSION-CHECKPOINT.md      # replaceable handoff for the next session
  sessions/session-NN.md     # one per session, two-part, ~1 screen each
  general/
    VISION / GAME-ARCHETYPES / CAPABILITY-MAP / ARCHITECTURE / ROADMAP /
    RECOMMENDATIONS / GLOSSARY / OPEN-QUESTIONS  # orientation
    ALIGNMENT-DEBT.md / BACKLOG.md / FEATURE-MAP.md  # the three ledgers
    PRODUCT-BUILDER.md       # the workflow doc itself
    _templates/              # 7 doc-type templates
    decisions/NNNN-<slug>.md # project-wide ADRs
  features/<feature>/
    <feature>.md             # feature root (Purpose / Boundaries / Composition / …)
    notes/NN-<slug>.md       # Models (the durable design content)
    decisions/NNNN-<slug>.md # feature ADRs (per-feature counter)
  spikes/<slug>.md           # exploration; status active/concluded/graduated/archived
  support/
    samples/<slug>.md        # working sample games
    prior-art/<slug>.md      # historical reference, not actively informing decisions
```

### 7 doc types

`spike`, `feature`, `model`, `sample`, `adr`, `session`, `checkpoint`. Templates in `PRODUCT/general/_templates/`. Use them — front matter (`type`, `status`, `last-updated`, `builds-on`, `tags`) is mandatory on Models; `builds-on` is either a list of paths or the literal `root`.

### Naming

Per-feature counters, restart at `01` per feature. Path is the prefix; cross-references use full paths (`features/actions/notes/01-command-pipeline.md`), never `doc 28`.

### Three lifetimes for questions

- **Durable** unanswered design questions → `general/OPEN-QUESTIONS.md`.
- **In-flight** the next session opens with → `SESSION-CHECKPOINT.md` "active open question."
- **Ephemeral / scratch** answered in-session → `sessions/session-NN.md` part 2.

Don't dual-write across these.

### Two status gates

- **Research gate** (`draft → settled`): no open ledger entries against this path, stable ≥ 2 sessions, all glossary terms used appear in `general/GLOSSARY.md`.
- **Build gate** (`settled → implemented`): code in `Assets/` satisfies the contract; no open drift entries.

### Cascade discipline

When a Model is edited, every affected downstream doc gets one entry in `general/ALIGNMENT-DEBT.md`. **The ledger is the single source of truth** — Models do not carry inline `## Implications` sections. Each Model edit declares either `debt: N opened` or `debt: none` in the session note. Empty signal is forbidden.

Threshold: more than 5 open ledger entries forces the next session into pass-mode.

### Don't

- Don't write to `PRODUCT/` outside the prescribed structure. If unsure, write a spike.
- Don't number new docs alongside features (no `30-…` files at PRODUCT root). Use the feature folder.
- Don't promote anything to `settled` without the research gate. Don't promote to `implemented` without the build gate.
- Don't bypass the alignment-debt ledger by adding `## Implications` sections.

## Scaffold (packages and tooling)

When searching for internal packages or wiring UPM entries, **Scaffold** is the source of truth — not random third-party guesses.

- **Details and paths:** `DOCS/scaffold.md` (local clone, GitHub, `Assets/Packages/com.scaffold.*`, manifest URL shape, Gear-Engine as reference).
- **Quick refs:** local `C:\Unity\Scaffold`, remote https://github.com/MgCohen/Scaffold, example consumer `C:\Unity\Gear-Engine`.

If the user asks to "check Scaffold" or "get X from Scaffold", follow that doc: browse local `Assets/Packages/` first, then GitHub; mirror Gear-Engine's `manifest.json` pattern for new dependencies.

## Unity

- Game code and assets live under `Assets/`. Respect Unity conventions (scenes, prefabs, ScriptableObjects).
- Prefer small, reviewable changes; avoid drive-by refactors unrelated to the task.
