---
description: Internal helper — re-project SESSION-CHECKPOINT.md from session notes and ledger state. Called by merge-pass and end-session (main mode). Not user-invocable.
allowed-tools: Read, Write, Bash, Grep
---

# `_project-checkpoint` — Internal Projection Helper

Called by `/builder:merge-pass` (step 8) and `/builder:end-session` when on `main`. Produces a complete new `Product/SESSION-CHECKPOINT.md` from inputs. Pure function — idempotent, re-runnable.

**Do not invoke directly** unless diagnosing a corrupted checkpoint. The caller controls the review gate (merge-pass: the PR; end-session: the step 5 diff-and-confirm).

## Inputs (provided by the caller's earlier steps)

The caller must have already read and passed:

- `merged[]` — final ordered list of all merged session-ids (including this pass).
- `batch[]` — sessions in the current batch (see merge-pass §9 for update rules).
- For each session in `batch[]`: `session-id`, `started-at`, `work-summary` frontmatter field, and the "Active question going out" section body.
- Current `Product/ledgers/OPEN-QUESTIONS.md` — for `Other still-open`.
- Current `Product/ledgers/ALIGNMENT-DEBT.md` — for `Suggested next steps` debt items.

## Projection — apply in this order

### 1. Frontmatter

```yaml
---
type: checkpoint
schema-version: 2
last-updated: <today-ISO>
head: <last entry in merged[]>
merged:
  - <every session-id in merged[], one per line>
batch:
  - <every session-id in batch[], one per line>
tags: [#checkpoint]
---
```

### 2. `## Where we stopped`

One bullet per session in `batch[]`, ordered by `started-at` ascending:

```
- S(<slug> <YYYY-MM-DD> <HH:MM>): <work-summary>
```

`slug` = the trailing `-<slug>` suffix of `session-id`. Date and time are UTC from `started-at`.

### 3. `## 🎯 The active open question (singular)`

From the session in `batch[]` with the **latest `started-at`**:

```
**<Active question going out text>**
*(From <session-id>.)*
```

### 4. `## Recent exit questions (parallel batch)`

Render **only when `len(batch) > 1`**. One bullet per session in `batch[]` *other than the most-recent*, ordered by `started-at` ascending:

```
- **<Active question going out>** — from `<session-id>`.
```

Omit this section entirely when batch has exactly one session.

### 5. `## ❓ Other still-open`

Read `Product/ledgers/OPEN-QUESTIONS.md`. Collect up to 8 entries with `status: open` and not struck through, in section order. Verbatim entry text — no editorial annotations. If 0 open entries: `_(none)_`.

### 6. `## Suggested next steps`

Build mechanically:

1. **Active question** (always slot 1) — same text as section 3 above.
2. **Recent exit questions** — if `len(batch) > 1`, one bullet per other session's exit question (same as section 4 above).
3. **Batch debt** — for each alignment-debt entry opened by a session in `batch[]` and still unresolved (status: open): `Discharge debt \`<id>\` — <reason truncated to 10 words>`.
4. **Top open debt cluster** — only if no batch debt: up to 2 oldest open entries from `ALIGNMENT-DEBT.md`, same format.

### 7. `## Artifacts to read on resume`

For each session in `batch[]`:
- Run: `git diff --name-only <session.parent>..<branch-head-for-that-session> -- Product/features/** Product/ledgers/**`
- Format: `- \`<path>\` (<session-slug>)`
- If the same path appears in multiple sessions, list both slugs: `(<slug-A>, <slug-B>)`.
- Fallback when branch is gone: derive file list from the session note's `## References` section.

### 8. `## How to use this file`

Copy this section verbatim from the prior checkpoint. Never regenerate it.

## Output

Write the complete body to `Product/SESSION-CHECKPOINT.md`. The caller owns the review gate — do not prompt here.
