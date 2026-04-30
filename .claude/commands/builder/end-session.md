---
description: Close the session — verify session note, draft a refreshed checkpoint, summarize deltas.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are closing a Product Builder session. Workflow rules: `Product/_workflow/PRODUCT-BUILDER.md`. End-of-session is the highest-leverage automation point — most state writes happen here, and forgetting one breaks the spine.

## Steps

1. **Identify the session note.** Find the most recent `Product/sessions/session-NN.md` with `status: in-progress`. If none, surface that and stop. If multiple, surface and ask which one.

2. **Verify session-note hygiene.**
   - Both parts cap ~50 lines each. Warn if Part 1 or Part 2 exceeds 50 (don't block).
   - Part 1 has an "Active question coming in" — must be non-empty.
   - Part 1 "Work done" — list non-empty *or* a single line saying "no doc deltas this session" if nothing was created.
   - Part 1 "Active question going out" — required. Empty signals an unfinished session note; warn.

3. **Cascade-discipline check** (per `PRODUCT-BUILDER.md` §5.7).
   - Find Models and ADRs touched in this session: `git diff --name-only` against the session-start commit, filtered to `Product/features/**/{notes,decisions}/*.md` and `Product/decisions/*.md`.
   - For each touched Model edit, the session note's Part 1 must contain either `debt: N opened` or `debt: none`. Empty signal is forbidden.
   - If missing, print the affected docs and ask: open debt entries via `/builder:open-debt`, or assert `debt: none`. Don't auto-add either.

4. **Read session deltas.** Build a summary, in this shape (omit empty rows):
   - Docs created: `<list of paths>` (from `git status` / `git diff --name-status`, filter to `A`).
   - Models edited: `<list>` (filter to `M`, path under `features/**/notes/`).
   - ADRs landed: `<list>` (filter to `A`, path under `**/decisions/`).
   - Debt opened: `<count from session note's "debt: N opened" lines>`.
   - Debt closed: `<count from grep '\[closed\]' on ALIGNMENT-DEBT.md diff>`.
   - Glossary updates: `<terms>` (grep changelog section of `ledgers/GLOSSARY.md`).
   - Backlog entries opened: `<titles>`.

5. **Draft the refreshed checkpoint.** **Critical: do not overwrite `Product/SESSION-CHECKPOINT.md` directly.** Build a draft and surface it for human approval first.
   - Read the current `SESSION-CHECKPOINT.md`.
   - Read the session note's Part 1 "Active question going out" — that becomes the new "active open question" in the checkpoint.
   - Read `ledgers/OPEN-QUESTIONS.md` and `ledgers/BACKLOG.md` for items to surface.
   - Generate the draft following the §4.7 template structure: Where we stopped / Active open question / Other still-open / Suggested next steps / Artifacts to read on resume.
   - Bump the `last-updated` and the session counter line.
   - Show the diff against the current checkpoint. **Wait for the user's approval** before writing.

6. **Flip the session note status.** `status: in-progress` → `status: completed`. Bump `last-updated`.

7. **Commit and push.** Auto-run after the checkpoint is approved/applied and the session-note status is flipped.
   - Stage all `Product/` changes (created + modified). Use explicit paths from the deltas in step 4 — never `git add -A`. Skip anything outside `Product/` unless the user explicitly added it during the session.
   - Commit format:
     - Subject: `Session NN — <short summary, ≤72 chars>`
     - Body: bulleted deltas (docs created, models edited, ADRs landed, debt opened/closed, ledger updates).
     - Co-author trailer per the harness convention.
   - Push to the current branch (`git push`). If no upstream is set, use `-u origin <branch>`.
   - If a PR is already open for this branch, no further action — push updates it. If no PR exists and the user has been opening PRs per session, suggest opening one (don't auto-create unless explicitly requested).
   - Print the commit SHA and the PR URL (or "no PR" + suggestion).

8. **Print the one-screen summary.** Concise. Five lines max:
   - `Session NN closed. <docs> created, <models> models edited, <adrs> ADRs.`
   - `Debt opened/closed: N / M.`
   - `Active question going out: <one line>.`
   - `Checkpoint refresh: applied.`
   - `Committed <sha>; pushed to <branch>` (+ PR URL if applicable).

## Notes

- Never auto-write the checkpoint. Always diff-and-confirm before applying.
- If the cascade-discipline check fails, surface, don't silently fix.
- If session-note caps are blown (>50 lines per part), trim suggestions go to the user; don't trim automatically.
- Auto-commit and push are part of the close — run them once the checkpoint diff is approved. Never `--force`, never `--no-verify`. If a pre-commit hook fails, surface the failure; do not bypass.
- Stage only `Product/` paths from the session deltas. If the workspace has unrelated dirty files (out-of-session work the user was holding), surface them before staging and ask.
- If `ledgers/ALIGNMENT-DEBT.md` has more than 5 open entries after this session closes, flag it loudly — the next session is forced into pass-mode (§5.8).
