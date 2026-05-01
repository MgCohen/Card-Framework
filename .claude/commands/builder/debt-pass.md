---
description: Auto-discharge `ALIGNMENT-DEBT.md` — mechanical edits, single commit, PR for review.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are running an unattended alignment-debt discharge pass. Workflow rules: `Product/_workflow/PRODUCT-BUILDER.md` §5.5 / §5.7 / §5.8. Plan: `PLANS/auto-debt-pass.md`. The PR you open is the only review gate — be conservative, surface ambiguity, never guess.

## Steps

1. **Sync.** `git fetch origin && git pull --ff-only`. **Refuse to start if the working tree is dirty** — exit with `working tree dirty, refusing to run` and stop. The agent runs from whatever ref the trigger placed us on: `main` for organic GHA fires (the `push: branches: [main]` trigger), the dispatched ref for `workflow_dispatch` (used by Layer 3 validation), and the developer's chosen branch for local runs (the runbook says to be on `main` before invoking — see [`Product/_workflow/_validation/debt-pass-runbook.md`](../../Product/_workflow/_validation/debt-pass-runbook.md)). Do not `git checkout main`; that would silently switch the dev away from a feature branch on accidental local invocation, and would break Layer 3 dispatch tests by switching away from the seeded branch.

2. **Threshold check.** Count open entries in `Product/ledgers/ALIGNMENT-DEBT.md` (lines under `## Open` ending in `| open` — strikethrough/resolved entries don't count). If **< 5**, exit with `nothing to do (count=N, threshold=5)` and stop. The GHA threshold gate is the primary check; this is a defensive secondary.

3. **Session-note collision pre-check.** Read the prior session number `NN` from `Product/SESSION-CHECKPOINT.md`'s session counter. Verify `Product/sessions/session-NN+1.md` does **not** already exist. If it does (e.g. a `/builder:start-session` resume left an in-progress stub), exit with `session-NN+1 already exists, refusing to overwrite — resolve manually` and stop. **This check fires here, before any expensive discharge work, on purpose** — the same refusal lives at step 9 as a defense-in-depth backstop, but Layer 3 sixth dispatch on 2026-04-30 showed an agent doing 6 steps of discharge + registry edits before discovering the collision and refusing. That run wasted ~$1.50 of subscription credits. Failing fast at pre-flight reuses the same refusal logic without the wasted work.

4. **Branch collision check.**
   a. `git fetch origin --prune`.
   b. Enumerate candidates: `git ls-remote --heads origin 'claude/debt-pass-*'`.
   c. For each match, classify via `gh pr list --head <branch> --state all --json number,state,url`:
      - **PR open** → exit with `prior pass in PR #X awaiting review, skipping` and stop.
      - **PR closed without merging** → `git push origin --delete <branch>`, continue.
      - **PR merged** or **no PR found** → treat as stale, `git push origin --delete <branch>`, continue.
   d. Once all matches are resolved, `git checkout -b claude/debt-pass-YYYY-MM-DD-01` (always `-01` — collisions are resolved before this step).

5. **Discharge loop.** Walk open entries top to bottom. For each:
   a. Parse the entry: `source`, `target`, `reason`, date.
   b. Read `source` and `target` paths.
   c. **Confidence check on `reason`.** If the `reason` text is too vague for a confident mechanical edit (e.g. uses "rewrite," "redesign," "reshape," "restructure," "reconsider," or names a non-mechanical change), **leave the entry `open`**, record it for the PR body's `## Skipped — needs a session` section, and continue. Better to discharge 9 of 10 and surface the holdout than to guess.
   d. Otherwise, apply the mechanical edit named in `reason` to the target doc. Strike through the ledger entry with the resolution date and a one-line resolution note:
      ```markdown
      - ~~YYYY-MM-DD | source: ... | target: ... | reason: ... | open~~ → resolved YYYY-MM-DD (auto debt-pass: <one-line note>)
      ```
      Move resolved entries from `## Open` to `## Resolved`.

6. **Hygiene scan.** Extract retired terms from each discharged entry's `reason` field (terms named with `replace with X`, `→ X`, `swap to X`, etc.). For each:
   a. `grep -r '<term>' Product/ --exclude='ledgers/ALIGNMENT-DEBT.md' --exclude-dir='_workflow/_validation'` to find stragglers the original cascade walk missed. (Two exclusions: the ledger itself, because its strikethrough resolution-note format echoes the retired term verbatim and would false-positive every pass; and the validation harness directory, which deliberately documents fixture term names — `OldFooBar`, `LegacyBaz`, etc. — and must remain stable across debt-pass runs. Without the harness exclusion, the hygiene scan progressively destroys its own test fixture.)
   b. For each straggler hit, apply the same mechanical replacement.
   c. Open a new ledger entry **and immediately strike it through** (opened-and-closed in this same pass), recording it for the PR body's `## Stragglers caught` section.

7. **Update the retired-tokens registry.** For each retired term discovered in this pass, append to `Product/_workflow/retired-tokens.json`:
   ```json
   {
     "term": "<retired term>",
     "retired": "YYYY-MM-DD",
     "replaced_by": "<replacement>",
     "source_entry": "<session-NN / doc-name §section>"
   }
   ```
   **Idempotent:** skip terms already present (match on `term` + `retired` date). Create the file as `[]` if it doesn't exist. Debt-pass writes; `builder-lint` reads.

8. **Refresh the checkpoint.** Read `Product/_workflow/_templates/checkpoint.md` for the structural shell. Then read the prior `Product/SESSION-CHECKPOINT.md` to lift forward context. Fill the new checkpoint as follows:
   - Front matter: bump `last-updated` to today, increment session counter to `NN+1` (where `NN` is the prior session number).
   - `## Where we stopped` — one-liner: `Auto debt-pass: <N> entries discharged, <M> stragglers caught, <K> skipped.`
   - `## 🎯 The active open question (singular)` — copy verbatim from the prior checkpoint. Debt-pass does not change the active design thread.
   - `## ❓ Other still-open` — copy from prior checkpoint.
   - `## Suggested next steps` — copy from prior checkpoint.
   - `## Artifacts to read on resume` — copy from prior checkpoint.
   Write to `Product/SESSION-CHECKPOINT.md`. **No in-command approval prompt — the PR is the gate.**

   *Note:* when `/builder:end-session` gains a headless mode (see `BACKLOG.md` entry `/builder:end-session headless mode`), step 8 delegates to it and the local fill rules above delete.

9. **Write the session note.** Target path: `Product/sessions/session-NN.md` (where `NN` is the new session number from step 8). **Defense-in-depth collision check** (primary check is at step 3): if the file already exists, refuse with `session-NN already exists, refusing to overwrite — resolve manually` and stop. The pre-flight check at step 3 should catch this earlier; this re-check only fires if a parallel process created the file mid-run. Otherwise, scaffold from `Product/_workflow/_templates/session.md`. Front matter:
   ```yaml
   ---
   type: session
   status: completed
   last-updated: <today>
   date: <today>
   tags: [#session, #debt-pass]
   ---
   ```
   Title: `# Session NN — <today> (auto debt-pass)`.
   - **Active question coming in** — copy verbatim from the prior checkpoint's `## 🎯 The active open question (singular)`.
   - **Work done** — bullets: `<N> debt entries discharged`, `<M> stragglers caught`, `<K> skipped — needs a session`, `retired-tokens.json: <list of new terms>`.
   - **Decisions settled** — none (debt-pass is mechanical; debit `none`).
   - **Active question going out** — copy verbatim from the prior checkpoint's active question. Debt-pass does not introduce its own.
   - **Followups created** — none routed beyond the skipped entries (already in the ledger).
   - **References** — list of touched docs (from the discharge + hygiene loops).

10. **Defensive empty-pass check.** Run `git status --porcelain Product/` — if zero changes, exit with `empty pass — nothing to commit, refusing to open PR` and stop. Per §3 of the plan: never open a PR with zero `Product/` changes.

11. **Single commit + push.** Stage all `Product/` changes (entry edits, registry update, ledger strikethroughs, checkpoint, session note) by explicit paths. Never `git add -A`. Never use `--force`, `--amend`, or `--no-verify`.
    - Subject: `Auto debt-pass NN — <N> closed, <M> stragglers, <K> skipped`
    - Body: bulleted summary mirroring the PR sections in step 12. Include `https://claude.ai/code/<session>` trailer per harness convention.
    - Push: `git push -u origin claude/debt-pass-YYYY-MM-DD-01`.

12. **Open the PR.** `gh pr create --base main --head claude/debt-pass-YYYY-MM-DD-01 --title "Auto debt-pass NN — <N> closed, <M> stragglers, <K> skipped" --body @-`, body shape:

    ```markdown
    ## Summary
    Auto debt-pass: <N> entries closed, <M> stragglers caught, <K> skipped.

    ## Mechanical discharge
    - <entry one-liner> → <target file>
    - ...

    ## Stragglers caught
    - <term> in <file>:<line> → <replacement>
    - ...
    _(omit section if empty)_

    ## Review carefully — large diffs
    - <file> (+X / -Y lines)
    - ...
    _(omit section if empty)_

    ## Review carefully — non-mechanical edits
    - <entry text>
    - ...
    _(omit section if empty; flag triggered when reason contains rewrite/redesign/reshape/restructure)_

    ## Status changes
    - <doc> status moved <from> → <to>
    - ...
    _(omit section if empty; e.g. samples bumped locked → draft for re-lock cooldown)_

    ## Skipped — needs a session
    - <entry text>  (reason: vague / non-mechanical)
    - ...
    _(omit section if empty)_

    ## Next session active question
    <copied verbatim from session note Part 1>
    ```

    Print the PR URL on completion.

## Auto-flag rules (deterministic — no judgment)

Apply these to populate the PR body sections:

- **`## Review carefully — large diffs`** — any single file with `>50` lines changed (sum of additions + deletions).
- **`## Review carefully — non-mechanical edits`** — any discharged entry whose `reason` contains any of: `rewrite`, `redesign`, `reshape`, `restructure`. Inline the entry text.
- **`## Stragglers caught`** — any new debt entries opened-and-closed during step 5 (hygiene scan).
- **`## Status changes`** — any doc with `type: sample | model | decision` whose `status:` front-matter field changed (e.g. sample `locked → draft`, model `settled → draft`). Session notes and other types are excluded — only lockbar-managed docs trigger this flag.
- **`## Skipped — needs a session`** — entries left open per step 5c.
- **`## Mechanical discharge`** — everything else (the quiet bulk).

## Safety rails (hard refusals)

The agent **will not**:

- Touch anything outside `Product/`.
- Flip `status: draft → settled` or `settled → implemented` on any doc.
- Promote a `proposed` ADR to `accepted`.
- Strike entries in `OPEN-QUESTIONS.md` that weren't named in a ledger entry.
- Open a PR if `git status --porcelain Product/` shows zero changes (defensive empty-pass guard, step 10).
- Use `git push --force`, `git commit --amend`, or any `--no-verify` flag.
- Run if the working tree is dirty on entry (step 1).
- Run if a prior debt-pass PR is open (step 4c).
- Run if `Product/sessions/session-NN+1.md` already exists at pre-flight (step 3) — refuse early to avoid wasted discharge work.

If any rule would be violated, stop and surface — never silently bypass.

## Notes

- This command is invoked unattended from `.github/workflows/debt-pass.yml` via `anthropics/claude-code-action@v1`. The PR is the human-review surface; the command itself has no interactive prompts.
- The threshold gate lives in the GHA workflow (count ≥ 5 from awk/grep on the ledger). Step 2 in this command is a defensive secondary check — it should rarely fire on its own.
- If `/builder:end-session` later exposes a headless mode (see `BACKLOG.md`), step 7's local fill rules can delete and that step delegates instead. The active question is the only piece that meaningfully carries forward; everything else is mechanical bookkeeping.
- Manual run for debugging: `claude -p "/builder:debt-pass"` from a clean working tree on `main` with `gh` authenticated. The branch-collision check makes re-runs safe — a prior PR will be detected and the run will skip.
- Layer 2 local validation: see [`Product/_workflow/_validation/debt-pass-runbook.md`](../../Product/_workflow/_validation/debt-pass-runbook.md) for the pre-seeded fixture, override prompt, six-watchpoint checklist, and cleanup sequence — re-run after any change to this command.
