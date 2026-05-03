---
description: Scaffold a new project under the Product Builder workflow — once-per-project.
argument-hint: <project-name>
allowed-tools: Read, Write, Edit, Bash, Glob
---

You are bootstrapping a new project under the Product Builder workflow. **This is once-per-project — refuse if `Product/` already exists with content.** For Card Framework specifically, this command is dormant; the project was migrated, not bootstrapped.

User input: `$ARGUMENTS`

## Pre-flight

1. Check whether `Product/` exists and contains anything beyond the skeleton `.gitkeep` files. If yes, **stop** — this command is for greenfield projects. Suggest the user is in the wrong project.
2. Check whether the source workflow doc is reachable. If a sibling project has `Product/_workflow/PRODUCT-BUILDER.md`, this command can copy from it. Otherwise the templates need to come from somewhere; surface the dependency and stop.

## Steps

Walk the user interactively through the `PRODUCT-BUILDER.md` §5.0 bootstrap. Don't dump the whole tree at once — confirm each step.

### 1. Gather project info

Ask:
- Project name (`$ARGUMENTS` if supplied).
- One-paragraph vision blurb.
- Candidate features (5–12 likely). Comma-separated. Examples: `entities, zones, actions, triggers, effects, engine, presentation`.

### 2. Create skeleton

```
Product/
  README.md
  SESSION-CHECKPOINT.md
  _workflow/
    PRODUCT-BUILDER.md
    EMOJI-LEGEND.md
    REFERENCE-CONVENTIONS.md
    retired-tokens.json
    _templates/
  ledgers/
  decisions/
  general/
  features/<feature>/
    models/
    decisions/
    archive/
  spikes/
  support/
    samples/
    prior-art/
  sessions/
```

Drop `.gitkeep` in every empty leaf so git tracks the structure. For each feature, `archive/` gets its own `.gitkeep` — it starts empty and receives docs only when a status flips to `Superseded` or `Cancelled` (§5.14). `sessions/` gets a `.gitkeep` — it stays empty until `/builder:snapshot` is invoked (§4.6).

### 3. Drop templates into `_workflow/_templates/`

Seven files extracted from `PRODUCT-BUILDER.md` §4.1–§4.5 + §4.7 + §5.14:
- `spike.md`, `feature.md`, `model.md`, `sample.md`, `adr.md`, `checkpoint.md`, `archive.md`.

(§4.6 Snapshot and §4.8 Plan are descriptive prose only — no template files.)

Also seed `_workflow/retired-tokens.json` as an empty array `[]`. The registry grows as renames land; `/builder:lint --tokens-only` reads it.

### 4. Initialize ledgers

- `Product/ledgers/OPEN-ITEMS.md` — empty, with the standard preamble (single ledger of loose ends; resolution = delete the entry).
- `Product/ledgers/FEATURE-MAP.md` — stub with the feature index table (one row per candidate feature, status `considered`, model count `—`) and an empty mermaid block.
- `Product/ledgers/GLOSSARY.md` — minimal frame with `## Changelog` section ready.

### 5. Project-wide ADR scaffold

Create `Product/decisions/0001-adopt-workflow.md` from the ADR template (`_workflow/_templates/adr.md`). Status `accepted`. Body should cite the new project's `_workflow/PRODUCT-BUILDER.md`.

### 6. Write VISION.md

`Product/general/VISION.md` from the supplied blurb. Don't elaborate — let the first session expand it. Add `general/ARCHITECTURE.md` and `general/ROADMAP.md` as terse stubs (per §5.0 step 5); they thicken as feature design lands.

### 7. Seed `SESSION-CHECKPOINT.md`

Create `Product/SESSION-CHECKPOINT.md` from the checkpoint template (`_workflow/_templates/checkpoint.md`). Active focus: *"what is this project?"*. The "Where we stopped" paragraph notes the bootstrap. The "Open items snapshot" links back to whatever durable items the user named in step 1.

### 8. Drop README

`Product/README.md` as the index pointing at the new structure. Mirror the Card Framework version.

### 9. Copy AGENTS.md instructions

If the user wants — and if there's no existing `AGENTS.md` at repo root — drop one with the Workflow section from the Card Framework `AGENTS.md`. If `AGENTS.md` exists, suggest the additions but don't auto-merge.

### 10. Report

Print:
- `Filesystem ready. <N> features scaffolded.`
- `Workflow doc: Product/_workflow/PRODUCT-BUILDER.md needs to be authored or copied — bootstrap doesn't ship it.`
- `Next step: copy or write PRODUCT-BUILDER.md, then run /builder:start-session.`

## Notes

- **The workflow doc itself isn't shipped by this command.** Bootstrap creates the folders, templates, ledgers, and the SESSION-CHECKPOINT seed — but `PRODUCT-BUILDER.md` is project-meta and either gets copied from another project or authored fresh. Surface this clearly at the end.
- Refuse aggressively if `Product/` is non-empty. Bootstrap is destructive-adjacent (creates many files); a wrong invocation could pollute an existing project.
- The Card Framework version lives at `Product/_workflow/PRODUCT-BUILDER.md` and is the canonical reference for what "the workflow" means. New projects should either copy that or ship a project-specific variant.
- Per the doc-workflow simplification rollout, mandatory per-session notes are retired. The bootstrap output is `SESSION-CHECKPOINT.md`, not a `session-01.md` artifact. The first interactive run starts with `/builder:start-session`.
