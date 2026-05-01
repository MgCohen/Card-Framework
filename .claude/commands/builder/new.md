---
description: Scaffold a new doc from a template — type, feature (where applicable), and title.
argument-hint: <type> [feature] <title>
allowed-tools: Read, Write, Bash, Glob
---

You are scaffolding a new doc in the Product Builder workflow. Templates live in `Product/_workflow/_templates/`. Workflow rules live in `Product/_workflow/PRODUCT-BUILDER.md`.

User input: `$ARGUMENTS`

## Steps

1. **Parse arguments.** Expected forms:
   - `model <feature> <title>` — Model under `Product/features/<feature>/models/`.
   - `adr <feature> <title>` — feature ADR under `Product/features/<feature>/decisions/`.
   - `adr general <title>` — project-wide ADR under `Product/decisions/`.
   - `spike <title>` — spike under `Product/spikes/`. No counter.
   - `sample <title>` — sample under `Product/support/samples/`. No counter.
   - `plan <title>` — plan under `PLANS/`. No counter, no template (plan is descriptive, see `PRODUCT-BUILDER.md` §4.8).

   If the user-supplied form doesn't match, print the expected forms and ask. Do not guess.

2. **Validate feature** (Model / feature ADR only). The folder `Product/features/<feature>/` must exist. If it doesn't, print the available features and ask. Do not create new feature folders here — that's `/builder:new-feature` (Tier 2). If `Product/features/<feature>/archive/` does not exist, create it with a `.gitkeep` before writing the new doc (silent, no user prompt needed).

3. **Pick the counter** (Model and ADR only).
   - For Models: `ls Product/features/<feature>/models/` → find max `NN-` prefix → increment, pad to 2 digits.
   - For feature ADRs: `ls Product/features/<feature>/decisions/` → find max `NNNN-` prefix → increment, pad to 4 digits.
   - For project-wide ADRs: same logic on `Product/decisions/`. Note `0001-adopt-workflow.md` is reserved.
   - Spikes/samples don't get a counter — use a kebab-case slug from the title.

4. **Slugify the title.** Lower-case, kebab-case, no punctuation. Examples: `"Effect Flow Canonical"` → `effect-flow-canonical`. Title length cap: 6 words max in the slug.

5. **Resolve target path.** Combine type + feature + counter + slug:
   - Model: `Product/features/<feature>/models/NN-<slug>.md`
   - Feature ADR: `Product/features/<feature>/decisions/NNNN-<slug>.md`
   - General ADR: `Product/decisions/NNNN-<slug>.md`
   - Spike: `Product/spikes/<slug>.md`
   - Sample: `Product/support/samples/<slug>.md`
   - Plan: `PLANS/<slug>.md`

6. **Refuse to overwrite.** If the target already exists, print the path and stop. Don't clobber.

7. **Read the template** from `Product/_workflow/_templates/<type>.md` (no template for plan — emit a minimal H1 + status line).

8. **Pre-fill front matter.** Replace the template's `<front matter: ...>` placeholder with real YAML:
   - `type:` matches the doc type.
   - `status:` `draft` for Model/ADR/spike/plan/sample. (ADRs may go straight to `proposed` if the user wants — ask if not specified.)
   - `last-updated:` today's ISO date.
   - `builds-on:` for Models — prompt the user for paths or `root`. Empty list `[]` is forbidden.
   - `tags:` lowercase hashtag list. Suggest based on type and feature.

9. **Replace the title placeholder.** Template H1s look like `# <subject> — model`. Substitute in the user's title.

10. **Write the file.** Show the planned diff first; on confirm, write.

11. **Patch the in-progress session note.** Find the session with `status: in-progress` via `grep -rl 'status: in-progress' Product/sessions/`. If multiple match, use the most recently modified (`ls -t`). Append a line to its Part 1 "Work done" section: `- <type> <relative path> created`. Skip this step for `plan` type — plans don't get session-noted.

12. **Render the result per `Product/_workflow/REPORT-STYLE.md` §3.4 (new template).** Use the §2.7 result-strip pattern: `**Created:**` followed by the path and `(<type>, <status>)` parenthetical (omit the status for `plan` type). Append the `📝 Session note: Part 1 "Work done" updated` strip on the next line, except for `plan` type (no session-note patch).

## Notes

- Don't write any content body beyond the template scaffolding. The author writes the content.
- Don't touch ledgers from this command — `ALIGNMENT-DEBT.md` and `FEATURE-MAP.md` updates are Tier 2 commands' responsibility.
- If the user passes `feature` for an ADR scope but means `general`, suggest the correction; don't silently route.
- Plan files are not workflow-specified per `PRODUCT-BUILDER.md` §4.8; this command writes a thin shell only.
