---
type: checkpoint
tags: [#checkpoint]
---

# Session Checkpoint

**Live spine.** This is the single source of truth for "where we are." Updated continuously as work happens. No open/close lifecycle.

---

## Where we stopped

Doc-workflow simplification rollout has fully landed (PRs #82, #83, plus the 5a/5b/3+4/6 follow-ups, all merged into `main`). Last design work: design pass on `oq-engine-builder` resolved — locked the two-layer split (DI installer + `IGameFactory`), wrote new Model `features/engine/models/05-game-factory.md`, cascaded patches to `engine/02`, `engine/04`, pile-counter §9.8, glossary, and open items.

---

## 🎯 Active focus

`oq-game-definition` — definition-declared slice schema + `GameDefinition` bundle. Track B from the engine-builder pass; the next natural design pass after game-factory landed.

---

## ❓ Open items snapshot

See [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) for the full list. The three highest-leverage durable items right now:

- `oq-game-definition` — `GameDefinition` bundle + definition-declared slice schema. Load-bearing for asset-driven match construction.
- `oq-gamerules-runtime-definition` — reconcile `GameRules` constructor-injected service shape (engine/02) with serializable-data shape (engine/05). Tightly scoped follow-up from the engine-builder pass.
- `oq-entity-destruction` — Cleanup lifecycle for destroyed entities. Load-bearing for ongoing effects that self-destruct.

---

## 📝 Scratch (ephemerals)

*Mid-conversation questions worth holding onto for the current session, but not yet promoted to OPEN-ITEMS. End of session: answer (delete), promote (move to OPEN-ITEMS), or leave for next session.*

### Workflow drift cleanup — kick-start brief for a dedicated session

**The problem.** The doc-workflow simplification rollout (PRs 1, 2, 5a, 5b, 3+4, 6 — all merged into `main`) updated the canonical workflow surface (`Product/_workflow/PRODUCT-BUILDER.md`, ledgers, root docs) but didn't fully propagate to `.claude/commands/builder/*.md` and `.claude/skills/builder/*/SKILL.md`. The skill files still operate against the pre-simplification model in places — writing to retired ledgers, patching session notes that no longer have a lifecycle, prefilling deprecated front-matter fields. Running these skills as written would produce wrong outputs.

**What's already done.** PR #90 (merged) + PR #91 (open) cover one slice:
- `design-pass.md` — full session-note workflow refresh (CHECKPOINT replaces session note as durable spine).
- `bootstrap.md` — `OPEN-QUESTIONS` → `OPEN-ITEMS` in the ledger seed list.
- `new.md`, `promote-glossary.md`, `archive.md` — dropped in-progress session-note patches and `ALIGNMENT-DEBT` references in `promote-glossary`.

**What's left (the actual drift).**

1. *Front-matter conventions (PR 1 of the simplification — `builds-on` → `references`, drop `last-updated`).*
   - `new.md` step 8 still prefills `last-updated:` (retired) and `builds-on:` (renamed to `references:`).
   - Possibly other Models / templates still carry `last-updated:` in their front matter (not yet audited).

2. *Session lifecycle (PR 5b — sessions retired, CHECKPOINT is the live spine).*
   - `bootstrap.md` step 2 skeleton still includes a `sessions/` folder.
   - `bootstrap.md` step 3 still lists `session.md` in the templates set.
   - `bootstrap.md` step 7 still creates `session-01.md` with `status: in-progress`.
   - `Product/_workflow/_templates/session.md` likely needs to retire (or its purpose redefined).

3. *Possible additional drift (not yet audited).*
   - `.claude/skills/builder/*/SKILL.md` — only `lint` was read this session; others might carry the same drift.
   - `Product/_workflow/PRODUCT-BUILDER.md` itself — referenced by skill files; may have its own internal drift.
   - `_templates/checkpoint.md` and `_templates/model.md` — verify they reflect current conventions.

**How to approach the next session.**

1. Start by auditing `Product/_workflow/PRODUCT-BUILDER.md`. It's the canonical source the skill files reference. If it's stale, fix it first — otherwise skill fixes will keep diverging.
2. Run a retired-token grep across `.claude/`: `last-updated:`, `builds-on:`, `ALIGNMENT-DEBT`, `OPEN-QUESTIONS`, `BACKLOG`, `Part 1`, `status: in-progress`, `session-NN`. Categorize hits as legitimate (retired-ledger reminders, like `end-session.md`'s safety rail) vs. drift.
3. Audit `_templates/` — retire `session.md` if session notes are gone; verify other templates reflect current front-matter and section conventions.
4. Sweep affected files in concern-aligned batches — front-matter sweep, session-lifecycle sweep, retired-ledger sweep. Each can be its own PR.
5. Re-run `/builder:lint` after the sweep to verify everything resolves cleanly.

This is finish-the-rollout chore work. Once done, the workflow surface is internally consistent and the skill files won't quietly steer future sessions toward retired patterns.

---

## How to use this file

- **Update continuously as work happens** — agent keeps this current; not refreshed only at session end.
- **Keep it short** — if it grows past two screens, trim. Detail belongs in feature docs and OPEN-ITEMS entries.
- **`## Scratch`** is for ephemerals — things being chewed on right now. Promote to OPEN-ITEMS if they survive a session.
- **No artifact created at session end.** `/builder:snapshot` makes an opt-in milestone copy in `sessions/` when the user asks.
