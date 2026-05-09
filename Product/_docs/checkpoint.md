---
type: doc-rules
target: checkpoint
status: accepted
references: [_workflow/PRODUCT-BUILDER.md]
tags: [#workflow, #rules, #checkpoint]
---

# Checkpoint — rulebook + skeleton

`SESSION-CHECKPOINT.md` is the **live operational spine** — kept current as work happens, not refreshed only at session boundaries. Single source of truth for "where we are." No open/close lifecycle. No schema beyond the skeleton.

## Non-negotiable gates

1. **Single file.** There is one checkpoint at `Product/SESSION-CHECKPOINT.md`. Per-session checkpoint files were retired; opt-in milestones go to `Product/sessions/` via `/builder:snapshot` (see Snapshot below).
2. **Live, not archival.** Updated continuously as work happens. Not refreshed only at session end.
3. **Short.** If the file grows past two screens, trim. Detail belongs in feature docs and `OPEN-ITEMS.md` entries.

## Conventions

- Lives at `Product/SESSION-CHECKPOINT.md`.
- Frontmatter: `type: checkpoint`, `tags: [#checkpoint]`. **No `status:` or `last-updated:` field** — git is the source of truth for recency.
- Body sections (per skeleton): Where we stopped, Active focus, Open items snapshot, Scratch (ephemerals), How to use this file.
- `## Scratch` is for ephemerals only. End of session: answer (delete), promote (move to `OPEN-ITEMS.md`), or leave for next session.
- Cross-references use `Product/`-rooted absolute paths.

## Snapshot — opt-in milestone

When the user wants to freeze the current state as a referenceable milestone, `/builder:snapshot` copies the checkpoint into `Product/sessions/<UTC-YYYYMMDD>-<HHMM>-<slug>.md` with `type: snapshot` front matter and a *"Frozen — do not edit"* header. Snapshots are sparse and opt-in; most sessions produce none.

## Heuristics

- **Update continuously.** Don't batch updates to session end.
- **Two-screen budget.** If it grows past two screens, trim.
- **Promote durable items.** Anything in Scratch that survives a session goes to `OPEN-ITEMS.md`.

## Skeleton

    ---
    type: checkpoint
    tags: [#checkpoint]
    ---

    # Session Checkpoint

    **Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

    ---

    ## Where we stopped

    <one paragraph synthesizing the most recent design move and what just landed>

    ---

    ## 🎯 Active focus

    `<oq-id or thread name>` — <one-sentence focus framing>

    ---

    ## ❓ Open items snapshot

    See `Product/ledgers/OPEN-ITEMS.md` for the full list. The three highest-leverage durable items right now:

    - `<oq-id>` — <short framing>
    - `<oq-id>` — <short framing>
    - `<oq-id>` — <short framing>

    ---

    ## 📝 Scratch (ephemerals)

    *Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

    <scratch entries, or empty>

    ---

    ## How to use this file

    - **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
    - **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
    - **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
    - **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
