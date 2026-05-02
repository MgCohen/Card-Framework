---
description: Show forward + reverse references for a single doc — what it depends on, what depends on it.
argument-hint: <path>
allowed-tools: Read, Bash, Glob, Grep
---

You are running `/builder:deps <path>`. Goal: surface the dependency graph around a single doc so the user knows the blast radius before editing it. **Read-only.** Output is a stdout table.

Two views:

- **Forward edges** — what `<path>` references. Read from its front-matter `references:`.
- **Reverse edges** — who references `<path>`. Computed by walking every other doc's `references:` fields and the inline path-shaped tokens in their bodies.

## Steps

1. **Resolve the input path.** `$ARGUMENTS` is the path; normalize to `Product/`-rooted absolute (e.g., `Product/features/cards/models/01-card-model.md`). If it doesn't resolve, exit with an error.

2. **Read forward references.** Parse the doc's YAML front matter. Extract the `references:` field. Each entry is a path under `Product/`.

3. **Compute reverse references** in two passes:
   - Front-matter `references:` walk: for every `Product/**/*.md` (excluding `_archive/` and `sessions/`), parse its front matter, check if `references:` includes the input path. If yes, it's a reverse edge.
   - Inline body links: `grep -rln '<input-path>' Product/ --exclude-dir=_archive --exclude-dir=sessions`. Anything that mentions the path inline (in a markdown link, a code reference, etc.) is a reverse edge — flag separately as "inline reference."

4. **Render the report:**

   ```
   # /builder:deps Product/features/cards/models/01-card-model.md

   ## Forward references (what this depends on)
   - features/entities/models/01-definition-instance-model.md

   ## Reverse references (who depends on this)
   - features/zones/models/01-zone-model.md (front matter)
   - features/effects/models/04-effect-flow.md (inline)
   - support/samples/pile-counter.md (inline)

   Summary: 1 upstream, 3 downstream (1 contract dependency, 2 inline mentions)
   ```

5. **Empty cases.** If no forward edges: *"No upstream — this doc is `references: root` or has no contract dependencies."* If no reverse edges: *"No downstream — nothing currently depends on this doc."*

## Safety rails

- **Read-only.** Do not modify any file.
- **Don't follow transitively by default.** Surface direct edges only. The user can re-run `/builder:deps` on a dependent doc to walk further.
- **Skip archived docs.** `Product/_archive/` and `Product/sessions/` are historical — don't include them in the reverse walk.
