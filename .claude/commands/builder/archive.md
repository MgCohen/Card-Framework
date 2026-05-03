---
description: Retire a doc to archive/ — flips status, writes front-matter, git mv, runs lint, opens a PR.
argument-hint: <feature>/<relative-doc-path> --status <Superseded|Cancelled> [--superseded-by <path>] [--reason <text>]
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are archiving a doc in the Product Builder workflow. Convention: `Product/_workflow/PRODUCT-BUILDER.md` §5.14. One commit, one PR, mechanical.

User input: `$ARGUMENTS`

## Steps

### 1. Parse arguments

Expected forms:
- `<feature>/<relative-doc-path> --status Superseded --superseded-by <path-to-successor> [--reason <text>]`
- `<feature>/<relative-doc-path> --status Cancelled [--reason <text>]`

The `<feature>/<relative-doc-path>` is relative to `Product/features/`. Examples:
- `engine/models/01-evaluation-loop.md`
- `actions/decisions/0003-intent-vocabulary.md`

If arguments are missing or ambiguous, print the expected form and stop. Do not guess.

### 2. Validate the source doc

```bash
# Resolve full path
src="Product/features/<feature>/<relative-doc-path>"
```

Check:
- The file exists.
- It is **not** already in an `archive/` directory (refuse: "already archived").
- `--status Superseded` requires `--superseded-by` — if missing, ask.
- `--status Superseded` requires `--superseded-by` to point at an existing, non-archived file. If that file is missing, print the path and stop.
- `--reason` is optional on the CLI; if not supplied, prompt the user for a one-line reason before proceeding.

### 3. Determine target path

```
archive_dir = Product/features/<feature>/archive/
target      = archive_dir/<original-filename>
```

- If `archive_dir` doesn't exist: create it with a `.gitkeep` first (silent, no user prompt).
- If `target` already exists: refuse with "target path already exists in archive/".

### 4. Read the source doc

Read the full file content. Extract:
- Current front matter (all fields).
- The H1 title line (first `# ...` line).

### 5. Build the archived front matter

Start from the existing front matter, then:
- Set `status: Superseded` or `status: Cancelled` (from argument).
- Add `superseded_by: <path>` (iff Superseded; relative path from the `archive/` doc to the successor, e.g. `../models/02-new-model.md`).
- Add `archived_on: <today's ISO date>`.
- Add `reason: <one-line reason>`.
- Keep all other original front-matter fields unchanged (`type`, `references`, `tags`, etc.).

### 6. Build the archived doc body

Prepend an archive header directly after the front matter:

```markdown
> **Archived <YYYY-MM-DD>.** Status: <Superseded by [successor title](relative-path) | Cancelled — reason>.
> Original content preserved below for historical record.

---
```

Then append the original doc body verbatim below the header. Do not edit the body content.

### 7. Show the planned change and confirm

Print:
```
git mv Product/features/<feature>/<relative-doc-path>
     → Product/features/<feature>/archive/<filename>

Front-matter changes:
  status: <old> → <Superseded|Cancelled>
  + superseded_by: <path>   (if Superseded)
  + archived_on: <date>
  + reason: <text>
```

Ask the user to confirm before writing anything.

### 8. Execute the move (on confirm)

In one bash invocation:

```bash
# Create archive/ if needed
mkdir -p Product/features/<feature>/archive

# Write the updated doc to the target path first, then remove the source
# (git mv equivalent via write + remove, since Write/Edit tools can't rename)
```

Use the `Write` tool to write the full archived content to the target path, then:

```bash
git rm Product/features/<feature>/<relative-doc-path>
git add Product/features/<feature>/archive/<filename>
# (if archive/ was just created, also add the .gitkeep)
```

### 9. Run /builder:lint --archive-only

Run the lint check immediately after the move. If any findings are reported, print them and ask whether to:
- **Continue** — open the PR with the findings surfaced in the PR body for manual cleanup
- **Abort** — `git reset HEAD` the staged changes and stop for manual fix

Most common finding after a fresh archive: rule A2 (stale inbound links from non-successor docs). Surface the specific links and flag which docs need updating.

### 10. Single commit

```bash
git commit -m "Archive <feature>/<filename> — <Superseded|Cancelled>

<reason>

https://claude.ai/code/<session>"
```

Stage only the files touched by this skill: source removal, target creation, and optional `.gitkeep`.

Never use `git add -A`, `--force`, `--amend`, or `--no-verify`.

### 11. Push and open PR

```bash
git push -u origin claude/archive-<feature>-<slug>-YYYY-MM-DD
```

Open a PR with:
- **Title:** `Archive <feature>/<filename> — <Superseded|Cancelled>`
- **Body:**

```markdown
## Summary
Moves `<source path>` into `<feature>/archive/` per the §5.14 archive convention.

## Change
- **Status:** <old status> → <Superseded|Cancelled>
- **Superseded by:** `<successor path>` (if Superseded)
- **Reason:** <reason>

## Lint
<Zero findings | N findings — see "Stale inbound links" below>

## Stale inbound links to fix before merging
- `<doc path>` line N — `[text](../archive/<filename>)` should update to `[text](<successor path>)`
_(omit if clean)_
```

Print the PR URL on completion.

## Safety rails

- **Never move a doc without user confirmation** (step 7).
- **Never move a doc with `--status` other than `Superseded` or `Cancelled`.**
- **Never overwrite an existing file in `archive/`** — stop if target exists.
- **Never edit the body content** of the archived doc — header only, body verbatim.
- **Never use `git push --force`, `--amend`, or `--no-verify`.**
- If lint reports findings and the user chooses Abort: reset staged changes and stop cleanly.
- This skill touches exactly one feature's `archive/`. Cross-feature archiving (e.g. a model that supersedes content in multiple features) requires manual handling.

## Notes

- The successor path in `superseded_by:` must be relative from the archived doc's location, not from repo root. Example: `../models/02-new-model.md` if the archived doc is in `features/engine/archive/` and the successor is in `features/engine/models/`.
- Spikes and Samples can also be archived using this skill — pass the correct path for their location. The same four front-matter fields apply.
- After the PR merges, anyone who linked to the old path gets a 404 on GitHub. Rule A2 lint findings tell you exactly which docs to update. Patch those in a follow-on commit on the same branch before merging the archive PR, or open a debt entry and discharge in a subsequent session.
