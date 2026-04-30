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

Bootstrap is the **structural half** of `PRODUCT-BUILDER.md` §5.0 — skeleton, templates, ledger stubs, ADR-0001, session-01 stub. The **orientation half** (VISION, ROADMAP, ARCHITECTURE, candidate features, seeding FEATURE-MAP and OPEN-QUESTIONS) is `/builder:setup`'s responsibility. Don't dump the whole tree at once — confirm each step.

### 1. Gather project info

Ask:
- Project name (`$ARGUMENTS` if supplied).

That's all. **Do not ask for a vision blurb or candidate features here** — those are `/builder:setup`'s responsibility. Bootstrap is purely structural scaffolding; the orientation pass happens after.

### 2. Create skeleton

```
Product/
  _workflow/_templates/
  general/
  decisions/
  ledgers/
  features/
  spikes/
  support/
    samples/
    prior-art/
  sessions/
```

Drop `.gitkeep` in every empty leaf so git tracks the structure. **Don't create per-feature folders here** — `/builder:setup` step 4 does that once the user names the candidate features.

### 3. Drop templates into `_workflow/_templates/`

Seven files extracted from `PRODUCT-BUILDER.md` §4.1–§4.7:
- `spike.md`, `feature.md`, `model.md`, `sample.md`, `adr.md`, `session.md`, `checkpoint.md`.

(§4.8 Plan and §6 ledger entry shapes are spec-only — no template files.)

### 4. Initialize ledger stubs

All five ledgers land as empty stubs with headers + entry-shape comments. **Don't seed content here** — `/builder:setup` populates `FEATURE-MAP.md` and `OPEN-QUESTIONS.md` once the user names candidates. `BACKLOG.md`, `GLOSSARY.md`, and `ALIGNMENT-DEBT.md` are append-only during sessions.

- `Product/ledgers/ALIGNMENT-DEBT.md` — empty, headers + entry-shape comment.
- `Product/ledgers/BACKLOG.md` — empty, headers + entry-shape comment.
- `Product/ledgers/FEATURE-MAP.md` — stub with placeholder text "No features yet — run `/builder:setup` to seed candidates."
- `Product/ledgers/GLOSSARY.md` — minimal frame with empty `## Changelog` section.
- `Product/ledgers/OPEN-QUESTIONS.md` — empty, headers + entry-shape comment.

### 5. Project-wide ADR scaffold

Create `Product/decisions/0001-adopt-workflow.md` from a project template. The Card Framework ADR at `Product/decisions/0001-adopt-workflow.md` is the reference shape. Status `accepted`. Body should cite the new project's `_workflow/PRODUCT-BUILDER.md`.

### 6. Open session 01

`Product/sessions/session-01.md` from the session template. Status `in-progress`. Active question coming in: *"what is this project?"*. Date: today. Leave the rest of the template empty — `/builder:setup` fills "Work done" as it walks the orientation pass.

### 7. Drop README

`Product/README.md` as the index pointing at the new structure. Mirror the Card Framework version.

### 8. Copy AGENTS.md instructions

If the user wants — and if there's no existing `AGENTS.md` at repo root — drop one with the Workflow section from the Card Framework `AGENTS.md`. If `AGENTS.md` exists, suggest the additions but don't auto-merge.

### 9. Report

Print:
- `Skeleton ready. Workflow doc: Product/_workflow/PRODUCT-BUILDER.md still needs to be authored or copied — bootstrap doesn't ship it.`
- `Next step: copy or write PRODUCT-BUILDER.md, then run /builder:setup to walk the orientation pass (VISION, ROADMAP, ARCHITECTURE, candidate features, FEATURE-MAP, OPEN-QUESTIONS).`
- `/builder:start-session will refuse to open session-02+ until /builder:setup completes.`

## Notes

- **The workflow doc itself isn't shipped by this command.** Bootstrap creates the folders, templates, ledgers, and session-01 stub — but `PRODUCT-BUILDER.md` is project-meta and either gets copied from another project or authored fresh. Surface this clearly at the end.
- Refuse aggressively if `Product/` is non-empty. Bootstrap is destructive-adjacent (creates many files); a wrong invocation could pollute an existing project.
- The Card Framework version lives at `Product/_workflow/PRODUCT-BUILDER.md` and is the canonical reference for what "the workflow" means. New projects should either copy that or ship a project-specific variant.
