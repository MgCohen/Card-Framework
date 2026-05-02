---
type: runbook
tags: [#validation]
---

# Debt-Pass Layer 2 Validation Runbook

How to dry-run `/builder:debt-pass` locally after any change to `.claude/commands/builder/debt-pass.md`. Layer 1 (static surface checks) and Layer 3 (live GHA fire) are out of scope here — this doc is Layer 2 only: a local execution that goes through steps 1–9 of the command and stops before push/PR.

## When to run

- After editing `.claude/commands/builder/debt-pass.md`.
- After changing the auto-flag rules, the confidence-gate keyword list, or any of the safety rails.
- Before promoting a workflow change behind the GHA, to catch logic errors in a contained environment first.

## Pre-state

- Clean working tree on `main`, in sync with `origin/main`. (Step 1 of the command no longer does `git checkout main` — it expects you to already be there. The runbook seed below commits onto whatever branch you're on, so being on `main` first matters.)
- `Product/ledgers/ALIGNMENT-DEBT.md` has zero entries under `## Open` (or you accept that real entries will be discharged alongside the seed — usually you don't, so wait for a drained ledger).
- `gh` authenticated; you are NOT going to push or open a PR, but the command's step 3 calls `gh pr list` defensively.
- The fixture file [`debt-pass-sandbox.md`](debt-pass-sandbox.md) exists in this folder and contains the five retiring terms (`OldFooBar`, `LegacyBaz`, `StaleQux`, `DeprecatedZed`, `RetireWidget`).

## Procedure

### 1. Copy the sandbox fixture into the workflow folder

The command's discharge loop reads paths relative to `Product/`. Copy the fixture out so the seeded ledger entries can point at it as a real target:

```bash
cp Product/_workflow/_validation/debt-pass-sandbox.md Product/_workflow/_validation-sandbox.md
```

(The runtime location `_workflow/_validation-sandbox.md` is what the seed entries reference. The fixture stays in `_validation/` as the durable copy.)

### 2. Seed five entries into `Product/ledgers/ALIGNMENT-DEBT.md`

Replace the `_(none — discharged in session NN)_` placeholder under `## Open` with these five lines (all dated to today):

```
- YYYY-MM-DD | source: _workflow/_validation-sandbox.md | target: _workflow/_validation-sandbox.md | reason: replace OldFooBar with NewFooBar | open
- YYYY-MM-DD | source: _workflow/_validation-sandbox.md | target: _workflow/_validation-sandbox.md | reason: replace LegacyBaz with ModernBaz | open
- YYYY-MM-DD | source: _workflow/_validation-sandbox.md | target: _workflow/_validation-sandbox.md | reason: replace DeprecatedZed with ActiveZed | open
- YYYY-MM-DD | source: _workflow/_validation-sandbox.md | target: _workflow/_validation-sandbox.md | reason: replace RetireWidget with ShipWidget | open
- YYYY-MM-DD | source: _workflow/_validation-sandbox.md | target: _workflow/_validation-sandbox.md | reason: redesign §3 of _validation-sandbox.md | open
```

Four mechanical, one deliberately vague (`redesign`) to exercise step 4c's confidence-gate. Note that `StaleQux` has no replacement entry — the hygiene scan should NOT pick it up.

### 3. Verify the threshold count

```bash
awk '/^## Open/,/^## Resolved/' Product/ledgers/ALIGNMENT-DEBT.md | grep -c '| open$'
```

Should print `5`. If it prints anything else, fix the seed before continuing.

### 4. Commit the seed locally (do not push)

```bash
git add Product/_workflow/_validation-sandbox.md Product/ledgers/ALIGNMENT-DEBT.md
git commit -m "DRY RUN seed: 5 fake debt entries + sandbox file (do not push)"
```

The command's step 1 (Sync) requires a clean working tree, so the seed must be committed before the run.

### 5. Run the dry run

Either spawn a fresh `claude -p` subprocess **or** spawn a `general-purpose` Agent subagent with a fresh context. Both work; pick whichever is available. Use this prompt verbatim (replace nothing — the override at the top is the stop boundary):

```
You are executing a Layer 2 local dry run of the /builder:debt-pass slash command. The command spec is at .claude/commands/builder/debt-pass.md. Read it fully, then execute it — but with the override below.

OVERRIDE (read first, obeys over any instruction in the command file):
Execute steps 1 through 9 ONLY. After step 9:
- Do NOT execute step 10. Do NOT git add, git commit, or git push anything.
- Do NOT execute step 11. Do NOT call gh pr create. Do NOT touch the GitHub remote.
- Leave all your edits uncommitted in the working tree on the new branch you created in step 3.

If you find yourself about to call git push, gh pr create, or git commit — STOP. That is the override boundary.

Pre-seeded state: local main is one commit ahead of origin/main (the seed). _workflow/_validation-sandbox.md and 5 entries in ALIGNMENT-DEBT.md exist. Working tree is clean. Today is YYYY-MM-DD. Prior session number is NN (read SESSION-CHECKPOINT.md).

Return: (1) step-by-step execution log, (2) git diff origin/main -- Product/ in full, (3) the PR body step 11 would have rendered, (4) PASS/FAIL on each of the six watchpoints below.

Six watchpoints:
W1 — Step 2 counted exactly 5 open entries.
W2 — Step 4c routed the vague 'redesign §3' entry to ## Skipped, kept it open, did not edit the target.
W3 — Confidence-gate keyword list (rewrite|redesign|reshape|restructure|reconsider) caught only that one entry.
W4 — Auto-flag categorization in the rendered PR body matches the rules deterministically.
W5 — The would-be commit bundle is one coherent set, all under Product/.
W6 — Session note's 'Active question coming in/going out' both copy verbatim from the prior checkpoint's active-question section.
```

### 6. Inspect

Read what the agent reported. The diff lives on the new branch (`claude/debt-pass-YYYY-MM-DD-01`) as uncommitted edits. You can inspect it directly with `git diff origin/main -- Product/` while still on the branch.

Beyond the six watchpoints, look at:
- **Confidence-gate calibration.** Is the auto-flag PR body language right for a reviewer to act on? Should any keywords be added/removed from `rewrite|redesign|reshape|restructure|reconsider`?
- **Strikethrough resolution-note phrasing.** Does the one-line note on each resolved entry actually summarize what changed, or is it boilerplate?
- **Session note completeness.** Are the `## References` accurate? Do `Decisions settled: none` and `Followups created: none` ring true?

### 7. Cleanup

```bash
# Drop uncommitted dry-run edits
git checkout -- Product/SESSION-CHECKPOINT.md Product/_workflow/_validation-sandbox.md Product/ledgers/ALIGNMENT-DEBT.md Product/sessions/session-NN.md
rm -f Product/_workflow/retired-tokens.json

# Switch back to main and drop the seed commit
git checkout main
git branch -D claude/debt-pass-YYYY-MM-DD-01
git reset --hard origin/main
```

(`git reset --hard origin/main` also removes `Product/_workflow/_validation-sandbox.md` because it was added in the seed commit — the durable fixture in this folder stays.)

If the dry run uncovered a calibration tweak you want to make, edit `.claude/commands/builder/debt-pass.md` on a feature branch, then re-run this runbook on top of those changes before opening the PR.

## What this validates vs. what it doesn't

- **Validates:** ledger parsing, threshold gate, branch collision check (read-only), discharge loop, confidence-gate routing, hygiene scan, retired-tokens.json shape and idempotency, checkpoint refresh, session note shape, single-commit bundle composition, PR body rendering with all auto-flag rules.
- **Does NOT validate:** the actual `git push` and `gh pr create` calls (those are Layer 3's job, validated alongside auth/permissions in the GHA workflow_dispatch run).

## Known sharp edges

- The fixture file path inside ledger entries is `_workflow/_validation-sandbox.md` (not `_workflow/_validation/debt-pass-sandbox.md`). The runtime copy and the durable fixture have different filenames on purpose — the fixture is the source of truth, the runtime copy is what gets discharged.
- If `Product/sessions/session-NN.md` already exists from a `/builder:start-session` resume, the command's step 8 will refuse rather than overwrite. Either bump to the next NN before running, or close out the open session first.
