---
description: Reconcile parallel session branches — merge completed sessions, resolve ledger conflicts via stable IDs, re-project the checkpoint.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are running a merge-pass to reconcile parallel Product Builder sessions into `main`. Plan: `PLANS/parallel-sessions.md`. Projection rules: `.claude/commands/builder/project-checkpoint.md`. The PR you open is the human-review gate — be conservative, surface ambiguity, never guess.

## When to run merge-pass

**Merge-pass is manual — there is no automatic trigger.** You invoke it when:

1. **One or more parallel sessions have been closed** (their branches have a `status: completed` session note and have been pushed). Each parallel session runs on its own `claude/<topic>` branch, never on `main` directly.
2. **You want to fold those branches back into `main`** — so the checkpoint advances, other sessions can use the new `head` as their parent, and the merged branches stop appearing in `git branch -r --no-merged main`.

**Typical rhythm:**
- Open a parallel session → do the work → `/builder:end-session` (writes session note, commits, pushes branch, opens PR).
- When all parallel branches in a batch are ready → switch to `main` → `/builder:merge-pass`.
- merge-pass discovers the completed branches, merges each one in `started-at` order, re-projects the checkpoint, and opens a single PR.
- After the PR merges → `main` has the new checkpoint `head`; start the next session against the updated head.

**You do not need to merge immediately after every session.** Sessions on branches are self-contained — they can sit unmerged while you do other work. The only constraint: a new session that wants to build *on top of* a completed-but-not-yet-merged session must wait for the merge (or accept a `rebase-on-top` parent chain, which merge-pass handles).

**Shortcut — single branch, fast-forward.** If only one branch needs merging and there's no conflict, `git merge --no-ff origin/<branch>` from `main` and then `/builder:end-session` (main mode) is a lighter path. Use merge-pass when you have multiple branches or want the automated checkpoint re-projection.

## Steps

1. **Sync and safety check.**
   ```bash
   git fetch origin && git checkout main && git pull --ff-only
   git status --porcelain
   ```
   If the working tree is dirty: exit with `working tree dirty — refusing to run.`

2. **Discover completed session branches.**
   - List remote branches not yet merged to main: `git branch -r --no-merged main`.
   - For each candidate branch, find new session notes: `git diff --name-only main...<branch> -- 'Product/sessions/session-*.md'`.
   - Read the frontmatter of each found session note. Collect: `{ session-id, branch, parent, started-at, status, work-summary }`.
   - Skip any session with `status: in-progress` — not ready to merge. Note them in the PR body.
   - Skip any session with `status: discarded` — orphan from start-session's cleanup pass; never enters `merged[]`. No surfacing needed.

3. **Read checkpoint state.**
   Read `Product/SESSION-CHECKPOINT.md`. Extract `head` and `merged[]` from frontmatter.
   If checkpoint has no `schema-version: 2`: exit with `Checkpoint is v1 — run the schema-cutover PR first (PLANS/parallel-sessions.md §5).`

4. **Classify each candidate.**

   | Parent relationship | Classification |
   |---|---|
   | `session-id ∈ merged[]` | already-merged — skip silently |
   | `parent == checkpoint.head` | fast-forward eligible |
   | `parent ∈ merged[]` but ≠ `head` | rebase-on-top eligible |
   | `parent ∉ merged[]` | stale parent — surface for human, skip |

5. **Order eligible candidates.** Sort by `started-at` ascending. Tiebreak: lexicographic `session-id`.

6. **If no eligible candidates:** exit with `nothing to merge — all branches are in-progress, already-merged, or stale.`

7. **Create merge working branch.**
   ```bash
   git checkout -b claude/merge-pass-<YYYYMMDD-HHMM>
   ```

8. **Merge each eligible session in order.** For each:

   a. `git merge --no-ff origin/<branch>`.

   b. **If conflicts arise:**
      - In `Product/ledgers/*`: resolve per §A (ledger conflict rules).
      - Elsewhere (Model files, ADRs, skill specs, anything outside `Product/ledgers/`): do NOT auto-resolve. Stage the conflict markers. Note the file in the PR body `## Deferred` section. Continue with remaining sessions.

   c. **Append to merged list.** Add `session-id` to `merged[]`.

   d. **Update batch list.** Apply §B (batch update rules).

   e. **Flip session note status.** `status: completed` → `status: merged`. Stamp `merged-at: <today-ISO>`.

   f. **Commit.** Message: `Merge session <session-id> into merge-pass`.

9. **Re-project the checkpoint.** Using the final `merged[]` and `batch[]` built in step 8, invoke `/builder:project-checkpoint` (or follow its rules directly from `.claude/commands/builder/project-checkpoint.md`). Write the result directly to `Product/SESSION-CHECKPOINT.md` — the PR is the review gate, no prompt here.

10. **Commit checkpoint and status flips.**
    Stage and commit: `Product/SESSION-CHECKPOINT.md` and all session notes flipped to `merged`.
    Message: `Merge-pass <YYYY-MM-DD>: checkpoint projected from <N> sessions`.

11. **Defensive empty-pass check.**
    `git status --porcelain Product/` — if no changes vs. main, exit: `empty pass — nothing to commit.`

12. **Push and open PR.**
    ```bash
    git push -u origin claude/merge-pass-<YYYYMMDD-HHMM>
    ```
    Then open the PR with `gh pr create --base main`:

    ```markdown
    ## Summary
    Merged <N> parallel session(s) into main.

    ## Sessions merged
    - `<session-id>` — <work-summary> (branch: `<source-branch>`)
    ...

    ## New checkpoint head
    `<checkpoint.head after this pass>`

    ## Ledger changes
    - ALIGNMENT-DEBT: <N> discharged, <M> new opens
    - OPEN-QUESTIONS: <N> resolved, <M> new entries
    - BACKLOG: <N> new entries
    - GLOSSARY: <N> terms added/updated
    _(omit empty lines)_

    ## Deferred — manual resolution needed
    - `<file>` — conflict between `<session-A>` and `<session-B>`. Resolve before merging.
    _(omit section if empty)_

    ## Stale parents — not merged
    - `<session-id>` — parent `<X>` not in merged[]. Investigate before merging.
    _(omit section if empty)_

    ## In-progress — not ready
    - `<session-id>` on `<branch>` — still in-progress, skipped.
    _(omit section if empty)_
    ```

    Print the PR URL on completion.

---

## §A Ledger conflict resolution rules

Apply per-ledger when `git merge` produces conflicts in `Product/ledgers/`.

**`ALIGNMENT-DEBT.md`** — group entries by `id` field.
- Both branches discharge same `id` (both struck through): keep one, prefer the earlier `resolved` date.
- Both branches add new open entries: union — include both.
- One branch strikes the other's open entry: take the strikethrough.

**`OPEN-QUESTIONS.md`** — group by `id` field.
- Both branches strike the same `id`: keep one strikethrough.
- Both add new entries: append both, preserve section grouping.

**`BACKLOG.md`** — append both sides; deduplicate by `id` field.

**`GLOSSARY.md`** — `## Changelog` is the merge arbiter.
- If two branches edit the same term table row: the entry with the more recent changelog date wins; open an alignment-debt entry for the loser: `glossary edit lost in merge (branch: <branch>) — reapply manually`.

**`FEATURE-MAP.md`** — never auto-resolve. Stage conflict markers. Surface in PR body `## Deferred` section.

---

## §B batch[] update rules

`batch[]` tracks sessions since the last single-merge sync point.

- **Single fast-forward** (one session, `parent == head`): reset `batch[] = [this session]`. New sync point.
- **Parallel batch** (multiple sessions all share the same `parent == head` at the start of this run): `batch[] = [all sessions, ordered by started-at]`.
- **Sequential within one run** (sessions with different parents, merged one after another): reset `batch[]` on each fast-forward; append on each rebase-on-top. After the full run, `batch[]` contains only the final sequential chain.

---

## Safety rails

The agent will not:

- Touch anything outside `Product/` and the session branch files being merged.
- Auto-resolve conflicts outside `Product/ledgers/` and `Product/SESSION-CHECKPOINT.md`.
- Merge any session with `status: in-progress`.
- Use `--force`, `--no-verify`, `--amend`, or `--squash`.
- Run if the working tree is dirty on entry (step 1).
- Run if the checkpoint is v1 (step 3).
- Open a PR with zero `Product/` changes (step 11).
- Proceed silently past a non-ledger conflict — always surface it.
- Two sessions editing the same Model file will conflict; merge-pass surfaces this for human resolution. This is correct — Model-content reconciliation requires judgment, not automation.
