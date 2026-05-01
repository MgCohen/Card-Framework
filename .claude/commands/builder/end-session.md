---
description: Close the session — verify session note, draft a refreshed checkpoint, summarize deltas.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are closing a Product Builder session. Workflow rules: `Product/_workflow/PRODUCT-BUILDER.md`. End-of-session is the highest-leverage automation point — most state writes happen here, and forgetting one breaks the spine.

## Steps

1. **Identify the session note.** Find `Product/sessions/session-*.md` with `status: in-progress`. If none, surface that and stop. If multiple, surface and ask which one.

1a. **Detect branch mode.** Run `git rev-parse --abbrev-ref HEAD`.
   - Result is `main` or `master` → **main mode**: full single-writer flow (checkpoint written inline).
   - Any other branch → **branch mode**: checkpoint write is deferred to `/builder:merge-pass`. Steps 5 and 9 behave differently (see below).

1b. **Author `work-summary` (v2 sessions only).** If the session note has `schema-version: 2` in its frontmatter:
   - Read the "Active question going out" line from Part 1.
   - Pre-fill `work-summary:` as a one-line synthesis (e.g., `Drafted <X>; resolved <Y>; surfaced <Z>.`).
   - Show the pre-fill and ask the user to confirm or edit. Write the confirmed value into the session frontmatter's `work-summary:` field before proceeding.

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

5. **Write the refreshed checkpoint.**

   **Branch mode:** Skip checkpoint write entirely. Print: `Branch session — checkpoint update deferred to /builder:merge-pass.` Proceed to step 6.

   **Main mode:** Write directly — no diff, no approval gate. The PR is the review surface.
   - **v2 checkpoint:** Invoke `/builder:project-checkpoint` (rules at `.claude/commands/builder/project-checkpoint.md`). Inputs: `merged[] + [this session-id]`, `batch[] = [this session-id]` (single-merge fast-forward), this session's frontmatter and "Active question going out," current ledger state. Build the full projected body.
   - **v1 checkpoint:** Read the current `SESSION-CHECKPOINT.md`. Read the session note's "Active question going out" — that becomes the new active open question. Read `ledgers/OPEN-QUESTIONS.md` and `ledgers/BACKLOG.md`. Generate the new checkpoint following the §4.7 template: Where we stopped / Active open question / Other still-open / Suggested next steps / Artifacts to read on resume. Bump `last-updated` and session counter.

6. **Flip the session note status.** `status: in-progress` → `status: completed`. Bump `last-updated`.

7. **Commit and push.** Auto-run after the checkpoint is written and the session-note status is flipped.
   - Stage all `Product/` changes (created + modified). Use explicit paths from the deltas in step 4 — never `git add -A`. Skip anything outside `Product/` unless the user explicitly added it during the session.
   - **The session note itself requires `git add -f`** — `Product/sessions/session-*.md` is gitignored (see `.gitignore`) so the in-progress skeleton stays quiet during the session. Only the deliberate end-of-session add force-tracks it. Already-tracked session files (from before this convention) re-stage normally; the `-f` is harmless either way.
   - Commit format:
     - Subject: `Session NN — <short summary, ≤72 chars>`
     - Body: bulleted deltas (docs created, models edited, ADRs landed, debt opened/closed, ledger updates).
     - Co-author trailer per the harness convention.
   - Push to the current branch (`git push`). If no upstream is set, use `-u origin <branch>`.
   - If a PR is already open for this branch, no further action — push updates it. If no PR exists and the user has been opening PRs per session, suggest opening one (don't auto-create unless explicitly requested).
   - Print the commit SHA and the PR URL (or "no PR" + suggestion).

8. **Render the closing report per `Product/_workflow/REPORT-STYLE.md` §3.2 (end-session template).** Follow the locked render order, anchor headings, and empty-state strips exactly.

   **Data sources for each section:**
   - **Title** — `# Closed Session NN`.
   - **🕐 Outcome** — table (`Topic | What was done`) of work-units finished this session. Derive from the session note's Part 1 "Work done" + git diff. If no doc deltas, render the empty-state strip: `🕐 Outcome: no doc deltas this session`.
   - **📊 Deltas** — counts from step 4 (docs created, models edited, ADRs landed, debt +N/−M, glossary updates).
   - **📝 Session note** — `📝 Session note: \`session-NN.md\` → completed`.
   - **📤 Commit** — `📤 Commit: \`<sha>\` → \`<branch>\``, plus `· PR: [#NN](<url>)` when a PR exists; drop the PR clause otherwise.
   - **⚠️ Pass-mode warning** — render only when post-close debt count > 5 (per §5.8): `⚠️ Pass-mode required next session — debt <N> / threshold <T>`.
   - **🎯 Next steps** — bullet list, derived from the session note's Part 1 "Active question going out" + the checkpoint's "Suggested next steps" section.
   - **🔀 Branch info** (branch mode only) — `Branch: <branch> · session: <session-id> · parent: <parent> · merge: pending → /builder:merge-pass`.

9. **Update the checkpoint's "Where we stopped" section** with the same one-line bridge content used as the seed for next session's `🕐 Last session:` line. This must happen as part of the checkpoint refresh in step 5 (re-stating here so the data flow is explicit).

   **Branch mode:** This step is skipped — the checkpoint is not written in branch mode. The `work-summary` on the session note (step 1b) serves the same purpose for merge-pass.

## Notes

- If the cascade-discipline check fails, surface, don't silently fix.
- If session-note caps are blown (>50 lines per part), trim suggestions go to the user; don't trim automatically.
- Auto-commit and push are part of the close — run them once the checkpoint diff is approved. Never `--force`, never `--no-verify`. If a pre-commit hook fails, surface the failure; do not bypass.
- Stage only `Product/` paths from the session deltas. If the workspace has unrelated dirty files (out-of-session work the user was holding), surface them before staging and ask.
- If `ledgers/ALIGNMENT-DEBT.md` has more than 5 open entries after this session closes, flag it loudly — the next session is forced into pass-mode (§5.8).
