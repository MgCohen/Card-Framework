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

6. **Render the result per `Product/_workflow/REPORT-STYLE.md` §3.3 (Mode A — Add term, or Mode B — Update term).** Title encodes the action and term; include the new definition as a blockquote and the section as bold prose. End with the 📝 session-note strip.

### Mode: Rename

1. **Verify source.** Confirm `<old-term>` exists in `GLOSSARY.md`. If it doesn't, ask whether the user meant *add* or whether the rename is for a term that was never glossary-tracked.

2. **Patch GLOSSARY.md.** Replace the row's term with `<new-term>`, preserving definition unless the user supplied a new one. Add a `## Changelog` entry:
   ```
   - YYYY-MM-DD — renamed `<old-term>` → `<new-term>`. <reason>.
   ```

3. **Cascade scan.** Run `grep -rl "<old-term>" Product/ --include='*.md'` (case-sensitive) and a separate case-insensitive pass (`grep -rli`). Combine results.

4. **Surface the cascade.** Print:
   - Number of files containing `<old-term>`.
   - Whether the count exceeds the §5.8 threshold of 5 — if so, the rename triggers a debt-pass session for the next session.
   - The grouped file list (by feature folder).

5. **Offer to scaffold ledger entries.** Ask the user: "Open one alignment-debt entry per affected file?" If yes:
   - Append entries to `Product/ledgers/ALIGNMENT-DEBT.md`. One entry per `(source=GLOSSARY rename, target=<file>)` pair, status `open`, today's date, reason `term renamed: <old> → <new>`.
   - Append `debt: N opened` to the session note's Part 1.
   - **Do not auto-edit the affected files.** That's `builder-rename-cascade` skill (Tier 2).

6. **Patch the session note.** Append to Part 1 "Work done": `- glossary: renamed <old> → <new> (<N> files affected)`.

7. **Render the result per `Product/_workflow/REPORT-STYLE.md` §3.3 (Mode C — Rename term).** Title encodes the rename; render the `📊 Cascade:` stat strip with the file count and the `· ⚠️ pass-mode trigger (threshold N)` clause when count > 5. Render the `### 📋 Debt opened` table when scaffolding was confirmed; switch the heading to `### 📊 Affected files` and add a `⏳ Debt scaffolding declined` strip otherwise. Apply the empty-state strips per the template's edge-state table.

## Notes

- **Generative, not enforcing.** This command opens the cascade — it doesn't close it. The actual file edits happen later, either manually or via `builder-rename-cascade` (Tier 2).
- Don't grep code (`Assets/`) here. Code-side renames are a separate concern (see `PRODUCT-BUILDER.md` §5.6.2 build gate).
- If `GLOSSARY.md` lacks a `## Changelog` section, create it. The proposal §6 specifies the changelog as part of the glossary.
- Multiple renames in one session are fine — append multiple changelog entries, one ledger sweep per rename.
- The `->` separator must be exact (with spaces around it, or use `-->`). Don't accept Unicode arrows silently — they're easy to typo and make grep brittle.
