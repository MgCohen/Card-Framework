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

2. **Validate feature** (Model / feature ADR only). The folder `Product/features/<feature>/` must exist. If it doesn't, print the available features and ask. Do not create new feature folders here — feature creation follows the `PRODUCT-BUILDER.md` §5.11 ceremony (folder + feature root stub + FEATURE-MAP update + CHECKPOINT note) and is done manually. If `Product/features/<feature>/archive/` does not exist, create it with a `.gitkeep` before writing the new doc (silent, no user prompt needed).

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

8. **Pre-fill front matter.** As of the templates sweep (2026-05-03), Spike / Feature / Model / Sample / ADR / Archive / Checkpoint templates already contain real YAML front matter — fill in the placeholders rather than constructing the block from scratch:
   - `type:` matches the doc type.
   - `status:` `draft` for Model/ADR/spike/plan/sample. (ADRs may go straight to `proposed` if the user wants — ask if not specified.)
   - `references:` for Models — prompt the user for paths or `root`. Empty list `[]` is forbidden. (No `last-updated:` field; git is the source of truth per `PRODUCT-BUILDER.md` §3.)
   - `tags:` lowercase hashtag list. Suggest based on type and feature.

9. **Replace the title placeholder.** Template H1s look like `# <subject> — model`. Substitute in the user's title.

10. **Write the file.** Show the planned diff first; on confirm, write.

11. **Render the result.**

    Result-strip pattern:
    - **Verb:** `Created`
    - **Subject:** the relative target path (in backticks)
    - **Metadata:** `(<type>, <status>)` for Model / ADR / spike / sample (status omitted for `plan` — no front matter)

    The act of creating the file is git-evident; nothing else needs to be written to a ledger or to CHECKPOINT.

    **Examples:**

    ```
    **Created:** `Product/features/engine/models/04-evaluation-loop.md` (Model, draft)
    ```

    ```
    **Created:** `Product/decisions/0009-async-snapshot-pipeline.md` (ADR, proposed)
    ```

    ```
    **Created:** `PLANS/effect-flow-canonical.md`
    ```

    Failure / refusal cases (path exists, feature folder missing, etc.) halt before this render and surface the error inline; they don't render this template.

## Notes

- Don't write any content body beyond the template scaffolding. The author writes the content.
- Don't touch ledgers from this command — `FEATURE-MAP.md` and `OPEN-ITEMS.md` updates are the author's responsibility (per the §5.11 feature-creation ceremony, FEATURE-MAP rows are added when a feature is promoted from `considered` to `draft`; OPEN-ITEMS entries land when durable opens surface). Glossary edits go through `/builder:promote-glossary`.
- If the user passes `feature` for an ADR scope but means `general`, suggest the correction; don't silently route.
- Plan files are not workflow-specified per `PRODUCT-BUILDER.md` §4.8; this command writes a thin shell only.
