---
description: Close the session — verify session note, draft a refreshed checkpoint, summarize deltas.
argument-hint: [--auto-pr | --discard]
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are closing a Product Builder session. Workflow rules: `Product/_workflow/PRODUCT-BUILDER.md`. End-of-session is the highest-leverage automation point — most state writes happen here, and forgetting one breaks the spine.

## Modes

- **Default** — closes the session as `status: completed`. Full hygiene + checkpoint flow.
- **`--auto-pr`** — non-interactive close used by `/builder:debt-pass`. Skips the unsaved-body review gate and auto-creates a PR.
- **`--discard`** — closes the session as `status: discarded`. Use for schema-test, no-op, or abandoned sessions whose work shouldn't pollute the checkpoint. Skips the unsaved-body gate, the cascade-discipline check, and the checkpoint write. Still commits + pushes the session note (preserves git record). `merge-pass` skips discarded sessions per its existing rule (PR #67) — they never enter `merged[]`.

## Steps

1. **Identify the session note.** Find `Product/sessions/session-*.md` with `status: in-progress`. If none, surface that and stop. If multiple, surface and ask which one.

1a. **Detect branch mode.** Run `git rev-parse --abbrev-ref HEAD`.
   - Result is `main` or `master` → **main mode**: full single-writer flow (checkpoint written inline).
   - Any other branch → **branch mode**: checkpoint write is deferred to `/builder:merge-pass`. Steps 5 and 9 behave differently (see below).

1b. **Author `work-summary`.**
   - Read the "Active question going out" line from Part 1.
   - Pre-fill `work-summary:` as a one-line synthesis (e.g., `Drafted <X>; resolved <Y>; surfaced <Z>.`).
   - If `work-summary` is already non-empty in the frontmatter, skip the prompt — treat the existing value as confirmed and proceed.
   - Otherwise, show the pre-fill and ask the user to confirm or edit. Write the confirmed value into the session frontmatter's `work-summary:` field before proceeding.

2. **Verify session-note hygiene.**
   - Both parts cap ~50 lines each. Warn if Part 1 or Part 2 exceeds 50 (don't block).
   - Part 1 has an "Active question coming in" — must be non-empty.
   - Part 1 "Work done" — list non-empty *or* a single line saying "no doc deltas this session" if nothing was created.
   - Part 1 "Active question going out" — required. Empty signals an unfinished session note; warn.

2a. **Unsaved-body check.** (Skipped when invoked with `--auto-pr` or `--discard`.)

   Read the conversation history of this session. Identify every artifact that has a concrete shape in the conversation — interfaces, protocols, records, model decisions, pseudocode, named contract shapes, ADR-worthy choices.

   For each artifact, classify it:
   - **Accepted** — the user gave an explicit positive verdict on the shape, or it was used/referenced as authoritative in subsequent turns.
   - **Not accepted** — the shape was proposed but rejected, or explicitly set aside.
   - **Uncertain** — discussed but no clear verdict; or left open by design.

   Then, for each Accepted artifact, check its canonical `Product/` file (if one exists) and determine whether the shape is already captured accurately.

   Render a review table and **wait for the user to confirm before writing anything:**

   ```
   ### 🔍 Sketches found this session

   | Artifact | Verdict | File match |
   |---|---|---|
   | `IHandlerRegistry` interface | ✅ Accepted | ❌ Not in file |
   | `GameEngineBuilder` builder pattern | ✅ Accepted | ✅ Matches engine/01 §6 |
   | `ICommandPipeline` stub | ⚠️ Uncertain | ❌ Not in file |
   | Queue ordering sketch | ❌ Not accepted | — |

   _Review and correct if needed. Accepted + not-in-file items will be written now._
   ```

   The user may correct any row before confirming. Once confirmed:
   - **Accepted + not in file** → write the artifact into its canonical `Product/` doc now. If no canonical file exists, create one via `/builder:new` first. Use the exact shape from the conversation — do not paraphrase a typed interface; write the interface.
   - **Accepted + matches file** → no action.
   - **Uncertain** → only write if the user explicitly upgraded it to Accepted during review.
   - **Not accepted** → skip.

   **If no artifacts are found** → proceed directly to step 3. No-op for operational sessions with no design work.

3. **Cascade-discipline check** (per `PRODUCT-BUILDER.md` §5.7). Skipped when invoked with `--discard` — discarded sessions don't carry cascade obligations forward.
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

   **`--discard`:** Skip checkpoint write entirely. Print: `Discarded session — checkpoint untouched.` Proceed to step 6.

   **Branch mode:** Skip checkpoint write entirely. Print: `Branch session — checkpoint update deferred to /builder:merge-pass.` Proceed to step 6.

   **Main mode:** Write directly — no diff, no approval gate. The PR is the review surface. Invoke `/builder:project-checkpoint` (rules at `.claude/commands/builder/project-checkpoint.md`). Inputs: `merged[] + [this session-id]`, `batch[] = [this session-id]` (single-merge fast-forward), this session's frontmatter and "Active question going out," current ledger state. Build the full projected body.

6. **Flip the session note status.** `status: in-progress` → `status: completed` (or `status: discarded` when invoked with `--discard`). Bump `last-updated`.

7. **Commit and push.** Auto-run after the checkpoint is written and the session-note status is flipped.
   - Stage all `Product/` changes (created + modified). Use explicit paths from the deltas in step 4 — never `git add -A`. Skip anything outside `Product/` unless the user explicitly added it during the session.
   - **The session note itself requires `git add -f`** — `Product/sessions/session-*.md` is gitignored (see `.gitignore`) so the in-progress skeleton stays quiet during the session. Only the deliberate end-of-session add force-tracks it. Already-tracked session files (from before this convention) re-stage normally; the `-f` is harmless either way.
   - Commit format:
     - Subject: `Session NN — <short summary, ≤72 chars>`
     - Body: bulleted deltas (docs created, models edited, ADRs landed, debt opened/closed, ledger updates).
     - Co-author trailer per the harness convention.
   - Push to the current branch (`git push`). If no upstream is set, use `-u origin <branch>`.
   - If a PR is already open for this branch, no further action — push updates it. If no PR exists: with `--auto-pr`, auto-create it (`gh pr create --base main --head <branch> --title "<session-id> — <work-summary>" --body "<Work-done bullets + step-4 deltas>"`); otherwise, suggest opening one.
   - Print the commit SHA and the PR URL (or "no PR" + suggestion).

8. **Render the closing report.** Output begins now. Render order is fixed — follow exactly.

   **1. Title** (H1) — `# Closed <session-id>` (or `# Discarded <session-id>` when invoked with `--discard` — the discarded session is recorded in git but excluded from `merged[]` by merge-pass).

   **2. Outcome** (H3 + table) — work-units finished this session. Derive from session note's Part 1 "Work done" + git diff:
   ```
   ### 🕐 Outcome
   | Topic | What was done |
   |---|---|
   | <topic> | <what was done> |
   ```
   Always rendered as a table when there are entries (even single-row). Empty state (no doc deltas): `🕐 Outcome: no doc deltas this session`.

   **3. Deltas** (stat strip) — counts from step 4: `📊 Deltas: <X> docs · <Y> models · <Z> ADRs · debt +<O>/−<C> · <G> glossary`.

   **4. Session note** (strip) — `` 📝 Session note: `<session-id>.md` → completed ``.

   **5. Commit** (strip) — `` 📤 Commit: `<sha>` → `<branch>` ``. Append ` · PR: [#NN](<url>)` when a PR exists; drop the PR clause otherwise.

   **6. Debt-pass note** (strip, conditional) — render only when post-close debt cascade count ≥ 5 (per §5.8): `⏳ Debt ≥ threshold — automated discharge will run on next GHA fire (cascade <N> / threshold 5).`

   **7. Next steps** (H3 + bullet list) — derived from session note's Part 1 "Active question going out" + the checkpoint's "Suggested next steps":
   ```
   ### 🎯 Next steps
   - <step>
   - <step>
   ```

   **8. Branch info** (strip, branch mode only) — `🔀 Branch: <branch> · session: <session-id> · parent: <parent> · merge: pending → /builder:merge-pass`.

   **Symmetry note:** the 🕐 Outcome content seeds next session's start-session `🕐 Last session:` line — same data, different label per POV. End-session generates it; start-session consumes it.

   **Heading-level rationale:** end-session uses H3 for both 🕐 Outcome and 🎯 Next steps. The H1 title carries the headline ("session closed"); supporting sections sit at H3.

9. **Update the checkpoint's "Where we stopped" section** with the same one-line bridge content used as the seed for next session's `🕐 Last session:` line. This must happen as part of the checkpoint refresh in step 5 (re-stating here so the data flow is explicit).

   **Branch mode:** This step is skipped — the checkpoint is not written in branch mode. The `work-summary` on the session note (step 1b) serves the same purpose for merge-pass.

## Notes

- If the cascade-discipline check fails, surface, don't silently fix.
- If session-note caps are blown (>50 lines per part), trim suggestions go to the user; don't trim automatically.
- Auto-commit and push are part of the close — run them once the checkpoint diff is approved. Never `--force`, never `--no-verify`. If a pre-commit hook fails, surface the failure; do not bypass.
- Stage only `Product/` paths from the session deltas. If the workspace has unrelated dirty files (out-of-session work the user was holding), surface them before staging and ask.
- If `ledgers/ALIGNMENT-DEBT.md` has 5+ open entries after this session closes, the closing report's debt-pass note will surface that the GHA discharge will fire next. No session is forced into pass-mode.
