---
description: Add or rename a glossary term — patches GLOSSARY.md, updates the changelog, optionally scaffolds a rename cascade.
argument-hint: <term> [definition] | <old-term> -> <new-term>
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are updating `Product/ledgers/GLOSSARY.md`. Most painful sessions in this project's history were rename cascades — this command is the entry point for getting them right.

User input: `$ARGUMENTS`

## Modes

The arguments determine the mode:

- **Add term:** `<term> <definition>` — single new term + its definition.
- **Update term:** `<term> [new definition]` — term already in glossary; revising the definition.
- **Rename term:** `<old-term> -> <new-term>` (the literal `->` separator) — propagate the rename.

If the form is ambiguous, ask. Don't guess.

## Steps

### Mode: Add or update

1. **Read** `Product/ledgers/GLOSSARY.md`. Find the appropriate section (Foundational Terms, Entity Model, Zones, etc.). If no obvious section, ask.

2. **Add or replace the row** in the relevant table. Preserve table column count and alignment.

3. **Append to the changelog section** at the bottom of `GLOSSARY.md`. If a `## Changelog` section doesn't exist yet, create one. Entry shape:
   ```
   - YYYY-MM-DD — added `<term>` / updated `<term>`. <one-line reason>.
   ```

4. **Patch the in-progress session note.** Append to Part 1 "Work done": `- glossary: added/updated <term>`.

5. **Show the diffs**, get confirmation, then write.

6. **Render the result.** (This template is the canonical source for promote-glossary Add/Update; `REPORT-STYLE.md §3.3 Mode A/B` mirrors it. Keep in sync.)

   **Mode A — Add term:**

   1. **Title** (H1) — `` # Glossary: added `<term>` ``
   2. **Definition** (blockquote) — `> <one-line definition>`
   3. **Section** (bold prose) — `**Section:** <glossary section name>`
   4. **Session note** (strip) — `📝 Session note: Part 1 "Work done" updated`

   **Mode B — Update term:**

   Same structure as Mode A, but the title reads `` # Glossary: updated `<term>` `` and the blockquote shows the new definition. Optionally add an `**Old:**` / `**New:**` pair when the change is a meaningful redefinition rather than a clarification.

   **No `🎯 Next steps` section.** Promote-glossary is one-shot. **No `📚` emoji** (excluded from the legend).

### Mode: Rename

1. **Verify source.** Confirm `<old-term>` exists in `GLOSSARY.md`. If it doesn't, ask whether the user meant *add* or whether the rename is for a term that was never glossary-tracked.

2. **Patch GLOSSARY.md.** Replace the row's term with `<new-term>`, preserving definition unless the user supplied a new one. Add a `## Changelog` entry:
   ```
   - YYYY-MM-DD — renamed `<old-term>` → `<new-term>`. <reason>.
   ```

3. **Cascade scan.** Run `grep -rl "<old-term>" Product/ --include='*.md'` (case-sensitive) and a separate case-insensitive pass (`grep -rli`). Combine results.

4. **Surface the cascade.** Print:
   - Number of files containing `<old-term>`.
   - Whether the count is ≥ the §5.8 threshold of 5 — if so, the resulting cascade entries will trigger automated debt-pass on next GHA fire (informational; no session is forced into pass-mode).
   - The grouped file list (by feature folder).

5. **Offer to scaffold ledger entries.** Ask the user: "Open one alignment-debt entry per affected file?" If yes:
   - Append entries to `Product/ledgers/ALIGNMENT-DEBT.md`. One entry per `(source=GLOSSARY rename, target=<file>)` pair, status `open`, today's date, reason `term renamed: <old> → <new>`.
   - Append `debt: N opened` to the session note's Part 1.
   - **Do not auto-edit the affected files.** That's `builder-rename-cascade` skill (Tier 2).

6. **Patch the session note.** Append to Part 1 "Work done": `- glossary: renamed <old> → <new> (<N> files affected)`.

7. **Render the result.** (This template is the canonical source for promote-glossary Rename; `REPORT-STYLE.md §3.3 Mode C` mirrors it. Keep in sync.)

   **Mode C — Rename term:**

   1. **Title** (H1) — `` # Glossary: renamed `<old>` → `<new>` ``
   2. **Section** (bold prose) — `**Section:** <glossary section name>`
   3. **Reason** (bold prose, optional) — `**Reason:** <why renamed>`
   4. **Cascade stat strip** — `📊 Cascade: <N> files affected`. Append ` · ⏳ awaiting automated discharge` when N ≥ 5 (informational; no pass-mode forcing).
   5. **Debt opened / Affected files** (H3 + table):
      ```
      ### 📋 Debt opened
      | Folder | Files |
      |---|---|
      | <folder> | <files> |
      ```
      Heading switches to `### 📊 Affected files` if the user declined debt scaffolding.
   6. **Session note** (strip) — `` 📝 Session note: Part 1 "Work done" updated `` (append `· <N> debt entries opened` when scaffolded).

   **Empty/edge states:**

   | Section | Empty / edge render |
   |---|---|
   | Cascade (0 files affected) | `📊 Cascade: 0 files affected — no follow-up needed`; omit the §5 file table |
   | Debt scaffolding declined | switch §5 heading to `### 📊 Affected files`; add `⏳ Debt scaffolding declined` strip below the table |
   | Cascade < 5 | drop the ` · ⏳ awaiting automated discharge` clause from the stat strip |

   **No `🎯 Next steps` section.** Promote-glossary is one-shot. **No `📚` emoji** (excluded from the legend).

## Notes

- **Generative, not enforcing.** This command opens the cascade — it doesn't close it. The actual file edits happen later, either manually or via `builder-rename-cascade` (Tier 2).
- Don't grep code (`Assets/`) here. Code-side renames are a separate concern (see `PRODUCT-BUILDER.md` §5.6.2 build gate).
- If `GLOSSARY.md` lacks a `## Changelog` section, create it. The proposal §6 specifies the changelog as part of the glossary.
- Multiple renames in one session are fine — append multiple changelog entries, one ledger sweep per rename.
- The `->` separator must be exact (with spaces around it, or use `-->`). Don't accept Unicode arrows silently — they're easy to typo and make grep brittle.
