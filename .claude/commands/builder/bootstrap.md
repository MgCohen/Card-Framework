---
description: Scaffold a new project under the Product Builder workflow — once-per-project.
argument-hint: <project-name>
allowed-tools: Read, Write, Edit, Bash, Glob
---

You are bootstrapping a new project under the Product Builder workflow. **This is once-per-project — refuse if `PRODUCT/` already exists with content.** For Card Framework specifically, this command is dormant; the project was migrated, not bootstrapped.

User input: `$ARGUMENTS`

## Pre-flight

1. Check whether `PRODUCT/` exists and contains anything beyond the skeleton `.gitkeep` files. If yes, **stop** — this command is for greenfield projects. Suggest the user is in the wrong project.
2. Check whether the source workflow doc is reachable. If a sibling project has `PRODUCT/general/PRODUCT-BUILDER.md`, this command can copy from it. Otherwise the templates need to come from somewhere; surface the dependency and stop.

## Steps

Walk the user interactively through the §5.0 8-step bootstrap from `PRODUCT-BUILDER.md`. Don't dump the whole tree at once — confirm each step.

### 1. Gather project info

Ask:
- Project name (`$ARGUMENTS` if supplied).
- One-paragraph vision blurb.
- Candidate features (5–12 likely). Comma-separated. Examples: `entities, zones, actions, triggers, effects, engine, presentation`.

### 2. Create skeleton

```
PRODUCT/
  general/
    _templates/
    decisions/
  features/<feature>/
    notes/
    decisions/
  spikes/
  support/
    samples/
    prior-art/
  sessions/
```

Drop `.gitkeep` in every empty leaf so git tracks the structure.

### 3. Drop templates into `general/_templates/`

Seven files extracted from `PRODUCT-BUILDER.md` §4.1–§4.7:
- `spike.md`, `feature.md`, `model.md`, `sample.md`, `adr.md`, `session.md`, `checkpoint.md`.

(§4.8 Plan and §6 ledger entry shapes are spec-only — no template files.)

### 4. Initialize ledgers

- `PRODUCT/general/ALIGNMENT-DEBT.md` — empty, headers + entry-shape doc.
- `PRODUCT/general/BACKLOG.md` — empty, seeded with two known deferred items: "Tier 2 commands per `ProductBuilder/AI-FIRST-AUTOMATION.md` §8" and "Tier 3 / hooks / skills as triggered."
- `PRODUCT/general/FEATURE-MAP.md` — initial table with one row per feature (status `draft`, model count `0`), plus an empty mermaid block.
- `PRODUCT/general/GLOSSARY.md` — minimal frame with `## Changelog` section ready.
- `PRODUCT/general/OPEN-QUESTIONS.md` — empty.

### 5. Project-wide ADR scaffold

Create `PRODUCT/general/decisions/0001-adopt-workflow.md` from a project template. The Card Framework ADR at `PRODUCT/general/decisions/0001-adopt-workflow.md` is the reference shape. Status `accepted`. Body should cite the new project's `general/PRODUCT-BUILDER.md`.

### 6. Write VISION.md

`PRODUCT/general/VISION.md` from the supplied blurb. Front matter `type: reference`, `status: settled`. Don't elaborate — let the first session expand it.

### 7. Open session 01

`PRODUCT/sessions/session-01.md` from the session template. Status `in-progress`. Active question coming in: *"what is this project?"*. Date: today.

### 8. Drop README

`PRODUCT/README.md` as the index pointing at the new structure. Mirror the Card Framework version.

### 9. Copy AGENTS.md instructions

If the user wants — and if there's no existing `AGENTS.md` at repo root — drop one with the Workflow section from the Card Framework `AGENTS.md`. If `AGENTS.md` exists, suggest the additions but don't auto-merge.

### 10. Report

Print:
- `Filesystem ready. <N> features scaffolded.`
- `Workflow doc: PRODUCT/general/PRODUCT-BUILDER.md needs to be authored or copied — bootstrap doesn't ship it.`
- `Next step: copy or write PRODUCT-BUILDER.md, then begin session 01.`

## Notes

- **The workflow doc itself isn't shipped by this command.** Bootstrap creates the folders, templates, ledgers, and session-01 stub — but `PRODUCT-BUILDER.md` is project-meta and either gets copied from another project or authored fresh. Surface this clearly at the end.
- Refuse aggressively if `PRODUCT/` is non-empty. Bootstrap is destructive-adjacent (creates many files); a wrong invocation could pollute an existing project.
- The Card Framework version lives at `PRODUCT/general/PRODUCT-BUILDER.md` and is the canonical reference for what "the workflow" means. New projects should either copy that or ship a project-specific variant.
