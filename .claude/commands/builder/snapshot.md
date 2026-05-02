---
description: Opt-in milestone — freeze SESSION-CHECKPOINT.md as a snapshot under Product/sessions/.
argument-hint: [<slug>]
allowed-tools: Read, Write, Bash
---

You are taking a milestone snapshot of the current `SESSION-CHECKPOINT.md`. Use when the user explicitly wants to freeze the current state as a referenceable milestone — e.g., before a major design pivot, after a settling-heavy session, or whenever they say "save a snapshot of where we are."

This skill is **opt-in only**. End-session does not call it. Most sessions do not produce snapshots — `Product/sessions/` is a sparse milestone archive, not a per-session log.

## Steps

1. **Read** `Product/SESSION-CHECKPOINT.md`.

2. **Determine the slug.** If the user passed one as `$ARGUMENTS`, use it. Otherwise infer from the active focus line in CHECKPOINT (kebab-case, ≤ 6 words).

3. **Compute the filename** as `session-<UTC-YYYYMMDD>-<HHMM>-<slug>.md`. Use `date -u +%Y%m%d-%H%M`.

4. **Copy the checkpoint contents into `Product/sessions/<filename>`**, with one front-matter tweak:
   - Replace `type: checkpoint` with `type: snapshot`.
   - Keep tags as-is, add `#snapshot` if not present.
   - Add a header line below the front matter: *"Snapshot of `SESSION-CHECKPOINT.md` taken YYYY-MM-DD HH:MM UTC. Frozen — do not edit."*

5. **Confirm** with the user: print the path of the new snapshot and its first ~10 lines.

## Safety rails

- **Don't modify** `SESSION-CHECKPOINT.md`. The snapshot is a read-only copy.
- **Don't auto-commit.** The user decides when to commit and push.
- **Filename uniqueness.** If the target filename already exists (same minute), append `-2`, `-3`, etc.
