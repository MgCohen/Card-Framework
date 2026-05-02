---
description: End-of-session routine — runs linter + freshness check + chat-log scan; surfaces findings as interactive prompts.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are running the end-of-session routine. There is no "closing" a session — `SESSION-CHECKPOINT.md` is the live spine and is updated continuously as work happens. This skill is a routine bundle: run integrity checks, surface findings, ask the user what to do per category. **Interactive triage**, not a state gate.

## Steps

### 1. Linter

Run `/builder:lint` against `Product/`. Capture findings (rule code + path + detail). The linter is report-only; it doesn't write to any ledger.

### 2. Freshness check

Compare `SESSION-CHECKPOINT.md`'s git mtime against any `Product/features/**` or `Product/ledgers/**` file touched since CHECKPOINT's last edit:

```bash
checkpoint_mtime=$(git log -1 --format=%ct Product/SESSION-CHECKPOINT.md)
touched=$(git log --since="@$checkpoint_mtime" --name-only --format= -- 'Product/features/**' 'Product/ledgers/**' | sort -u)
```

If `$touched` is non-empty, CHECKPOINT may be stale relative to recent work.

### 3. Chat-log scan for unsaved sketches

Read the conversation transcript of the current session. Classify each artifact (Model body, ADR shape, contract sketch, glossary term) as:

- **Accepted** — user committed to it, but check it lands in a canonical file.
- **Not accepted** — user explicitly rejected it.
- **Uncertain** — neither clear; surface for confirmation.

For each Accepted artifact, grep the relevant Product file to verify it actually landed. If not, it's an unsaved sketch.

### 4. Interactive triage

Surface all three categories as one prompt to the user. Format:

```
# Session wrap

## Linter findings
<N findings, table from /builder:lint output. Or "Clean — no findings.">

## Freshness check
<CHECKPOINT is stale relative to: list of touched files. Or "CHECKPOINT is current.">

## Unsaved sketches
<K artifacts that the conversation accepted but aren't in any Product file. Or "All accepted artifacts are saved.">

For each category:
- Fix now (apply edits in this turn).
- Defer to OPEN-ITEMS (open an entry).
- Ignore.

What's the call?
```

### 5. Act on user direction

For each category the user routes:
- **Fix now** — apply edits in the current conversation (refresh CHECKPOINT, save sketches to canonical files, fix lint findings).
- **Defer to OPEN-ITEMS** — append a paragraph entry per item to `Product/ledgers/OPEN-ITEMS.md`.
- **Ignore** — do nothing. The user is taking responsibility for the gap.

Don't refuse or block. The user routes; you execute.

## Safety rails

- **No artifact at session boundary.** Don't create a session note. CHECKPOINT is the only spine.
- **Don't auto-fix without confirmation.** Surface findings; act on user direction.
- **Don't write to any deleted ledger.** ALIGNMENT-DEBT, OPEN-QUESTIONS, BACKLOG are gone — only OPEN-ITEMS exists.
- **Snapshots are opt-in.** If the user wants to freeze this state as a milestone, suggest `/builder:snapshot`. Otherwise, don't copy CHECKPOINT to `sessions/`.
