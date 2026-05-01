---
type: plan
status: complete
last-updated: 2026-05-01
tags: [#automation, #builder, #debt-pass]
---

# Auto debt-pass — implementation plan

Three components, build in order. Goal: when `Product/ledgers/ALIGNMENT-DEBT.md` crosses the §5.8 threshold, an unattended agent opens a PR that drains it. Human review at the PR is the only gate.

## 1. The discharge command — `/builder:debt-pass`

Lives at `.claude/commands/builder/debt-pass.md`, matching the existing `/builder:*` family (`start-session`, `end-session`, `new`, `promote-glossary`, `bootstrap`). Invoked by the trigger; can also run manually. Assumes a clean working tree on `main`.

**Steps the command prompt encodes:**

1. `git fetch && git checkout main && git pull`. Refuse to start if the working tree is dirty.
2. Count open entries in `Product/ledgers/ALIGNMENT-DEBT.md`. Bail with `nothing to do` if `< 5`.
3. **Branch collision check.**
   a. `git fetch origin --prune`.
   b. Enumerate candidates: `git ls-remote --heads origin 'claude/debt-pass-*'`.
   c. For each match, classify via `gh pr list --head <branch> --state all --json number,state,url`:
      - PR open → exit cleanly with `prior pass in PR #X awaiting review, skipping`.
      - PR closed without merging → delete the stale branch on remote (`git push origin --delete <branch>`), continue.
      - PR merged or no PR found → treat as stale, delete on remote, continue.
   d. Once all matches are classified-and-resolved, `git checkout -b claude/debt-pass-YYYY-MM-DD-01` (always `-01` since collisions are resolved before this step).
4. Walk open entries top to bottom. For each: read source doc, read target doc, apply the mechanical edit named in the entry's `reason`, strike through the entry with the resolution date.
5. **Hygiene scan** — extract retired terms from each discharged entry's `reason` field, grep `Product/` for them, catch stragglers the original cascade walk missed. Open new struck-through entries inline (opened-and-closed in same pass).
6. **Update the retired-tokens registry** — append each retired term discovered in this batch to `Product/_workflow/retired-tokens.json`. Idempotent: skip terms already present. Debt-pass writes the registry; it never reads it (consumption is `builder-lint`'s job, when that lands).
7. **Refresh the checkpoint.** Open `Product/_workflow/_templates/checkpoint.md` for the structural shell. Fill it as follows:
   - `## Where we stopped` — one-liner: "Auto debt-pass: N entries discharged."
   - `## 🎯 The active open question (singular)` — copy verbatim from the prior `SESSION-CHECKPOINT.md` (debt-pass does not change the active design thread).
   - `## ❓ Other still-open` — copy from prior checkpoint.
   - `## Suggested next steps` — copy from prior checkpoint.
   - `## Artifacts to read on resume` — copy from prior checkpoint.
   - Bump `last-updated` and the session counter to `NN+1`.
   Write to `Product/SESSION-CHECKPOINT.md`. The PR is the human-review gate; no in-command approval prompt.

   *Future:* when `/builder:end-session` gains a headless mode (see backlog entry `/builder:end-session headless mode`), this step delegates to it and the local fill rules above delete.
8. Write `Product/sessions/session-NN.md` with the standard cap. The session note's "active question going out" copies verbatim from the prior `SESSION-CHECKPOINT.md`'s `## 🎯 The active open question (singular)` — debt-pass does not introduce its own active question.
9. Single commit, all `Product/` paths (including the registry update). Push to remote.
10. `gh pr create` with the body shape in §4.

### Retired-tokens registry shape

`Product/_workflow/retired-tokens.json`. Append-only; entries identify a term, when it was retired, and what replaced it.

```json
[
  {
    "term": "IGameStateProvider",
    "retired": "2026-04-30",
    "replaced_by": "IStateScope (Scaffold com.scaffold.states)",
    "source_entry": "session-13 / 04-effect-flow §4.1"
  },
  {
    "term": "IStateMutator<TCommand>",
    "retired": "2026-04-30",
    "replaced_by": "Mutator<TState, TPayload> (Scaffold)",
    "source_entry": "session-13 / 04-effect-flow §4.2"
  }
]
```

`source_entry` points back at the session + Model section that retired the term, so `builder-lint` can produce real diagnostics ("`IGameStateProvider` was retired on 2026-04-30; use `IStateScope` instead — see 04-effect-flow §4.1") rather than a bare "found a bad token."

**Auto-flagging in the PR body** — deterministic rules, no judgment:

- Any file with `> 50` lines changed → `## Review carefully — large diffs`.
- Any ledger entry whose `reason` contains `rewrite`, `redesign`, `shape`, `restructure` → `## Review carefully — non-mechanical edits` with the entry text inlined.
- Any new debt entries opened during the run (stragglers) → `## Stragglers caught` with file + reason.
- Any sample whose status moved `locked → draft` → `## Status changes`.
- Everything else lands quietly under `## Mechanical discharge` with a count + file list.

The PR is the review gate. A reviewer with the auto-flags can spend their attention only on the highlighted sections.

## 2. The trigger

GitHub Action on `push` to `main`, filtered to changes in the ledger file. Commit-driven — fires the moment a session-end push tips the count over threshold, before any next session has a chance to run against stale docs.

**Workflow config** (`.github/workflows/debt-pass.yml`):

```yaml
on:
  push:
    branches: [main]
    paths: [Product/ledgers/ALIGNMENT-DEBT.md]
  workflow_dispatch:

jobs:
  debt-pass:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    permissions:
      contents: write
      pull-requests: write
    steps:
      - uses: actions/checkout@v4
      - name: Threshold check
        id: check
        run: |
          count=$(awk '/^## Open/,/^## Resolved/' Product/ledgers/ALIGNMENT-DEBT.md | grep -c '| open$' || true)
          echo "count=$count" >> "$GITHUB_OUTPUT"
          [ "$count" -ge 5 ] && echo "fire=true" >> "$GITHUB_OUTPUT" || echo "fire=false" >> "$GITHUB_OUTPUT"
      - name: Run debt-pass
        if: steps.check.outputs.fire == 'true'
        uses: anthropics/claude-code-action@v1
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        with:
          anthropic_api_key: ${{ secrets.ANTHROPIC_API_KEY }}
          prompt: "/builder:debt-pass"
          claude_args: |
            --max-turns 30
            --model claude-opus-4-7
```

**YAML-level guarantees** (each is load-bearing; do not drop):

- `permissions: contents: write, pull-requests: write` — required for the agent to push the discharge branch and open its PR. Default `GITHUB_TOKEN` is read-only.
- `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}` — passed through so the agent's `gh pr list` / `gh pr create` calls authenticate non-interactively. (Verify during §5 step 1 whether the official action exposes this via `with:` instead of `env:`; adjust if so.)
- `timeout-minutes: 20` — bounds blast radius if the agent wedges. Sized for a realistic 5–15 entry discharge with headroom; combined with `--max-turns 30` as a second backstop.
- `anthropics/claude-code-action@v1` — first-party action handles install + invocation + auth in one step. Floating major pin gets patch/minor fixes automatically; tighten to `@v1.x.y` if surprises emerge.
- `--max-turns 30` — caps conversation length independently of wall-clock timeout.
- `workflow_dispatch:` — manual run from the Actions tab for debugging.

**Self-loop guard.** The agent pushes to its own `claude/debt-pass-*` branch and opens a PR; its commits never hit `main` until a human merges. So the `branches: [main]` filter prevents the agent from re-triggering itself. The merge of the PR *is* a push to `main`, but that push *closes* entries, dropping the count below threshold — the next workflow run no-ops cleanly. Self-correcting. (Bonus layer: GitHub by default does not fire workflows for pushes authored by `GITHUB_TOKEN`, so the agent's own pushes during the workflow can't re-trigger anything regardless.)

## 3. Safety rails

Encoded in the command prompt as hard refusals. The agent will not:

- Touch anything outside `Product/`.
- Flip `status: draft → settled` or `settled → implemented`.
- Promote a `proposed` ADR to `accepted`.
- Strike entries in `OPEN-QUESTIONS.md` that weren't named in a ledger entry.
- Open a PR if the diff has zero `Product/` changes (defensive against an empty pass).
- `--force`, `--amend`, `--no-verify`.

If a ledger entry's `reason` is too vague for a confident mechanical edit, the agent leaves it `open`, notes it in the PR body under `## Skipped — needs a session`, and moves on. Better to discharge 9 of 10 and surface the holdout than to guess.

## 4. PR body template

```markdown
## Summary
Auto debt-pass: N entries closed, M stragglers caught, K skipped.

## Mechanical discharge
- <list of (entry, target file)>

## Stragglers caught
- <new entries opened-and-closed in this pass>

## Review carefully — large diffs
- <files with >50 lines changed>

## Review carefully — non-mechanical edits
- <ledger entries flagged by reason text>

## Status changes
- <samples downgraded to draft, etc.>

## Skipped — needs a session
- <entries the agent couldn't confidently discharge>

## Next session active question
<copied from session note Part 1>
```

## 5. Sequencing

1. Build the command (`.claude/commands/builder/debt-pass.md`). Test manually: queue a fake debt entry, run `/builder:debt-pass`, inspect the PR. Iterate on the auto-flag rules until the PR body reads usefully. During this phase, also verify two action-specific details: (a) `prompt: "/builder:debt-pass"` invokes the slash command directly (vs. needing a meta-instruction wrapper), and (b) whether `GH_TOKEN` belongs in `env:` or `with:` for `anthropics/claude-code-action@v1`.
2. Wire the GHA workflow. Watch one or two real firings (the first time the ledger crosses threshold) before trusting it unattended.

Backlog entries for this work (`/builder:debt-pass command`, and the dependent `/builder:end-session headless mode`) land in `Product/ledgers/BACKLOG.md` as part of the same PR as this plan, so the doc trail is visible to `/builder:start-session` orientation before implementation begins.

Total work: command prompt is ~150 lines, routine is one command. Half a session.

## What this plan doesn't try to solve

- **Pass-mode for non-mechanical sessions.** If the active question is design work, this doesn't apply. Threshold-driven discharge only.
- **Drift detection** (the post-merge `builder-drift-detection` automation in §5.12) — orthogonal. Different trigger, different scope, different skill.
- **Cross-project use.** Command paths and ledger paths are hard-coded to this project's `Product/` layout. Generalising is a Tier 3 concern.

## Open questions for iteration

- ~~Should the routine fire daily, or only on threshold-trip detected by a post-commit hook?~~ → **Resolved: GHA on `push` to `main`** (commit-driven; daily lag breaks the "next session is forced into pass-mode" guarantee).
- ~~Should the agent run the hygiene scan only against retired terms named in this batch, or against a permanent list of "known bad" tokens?~~ → **Resolved: batch-scoped scan, with side-effect append to `Product/_workflow/retired-tokens.json`.** Debt-pass produces the registry; `builder-lint` consumes it (when that command lands). Clean separation of "discharge this ledger" vs "scan the project."
- ~~One commit per PR, or one commit per discharged entry?~~ → **Resolved: single commit.** All `Product/` changes (entry edits, registry update, ledger strikethroughs, checkpoint, session note) bundled. The PR body's auto-flagging carries per-entry detail; that's a better surface than commit history because it's organised by *type of attention needed* rather than *order of discharge*.
- ~~What's the right `-NN` collision policy if a debt-pass branch already exists?~~ → **Resolved: skip and no-op, with PR-state distinction.** If a prior `claude/debt-pass-*` branch exists, check the associated PR: open → skip with "PR #X waiting for review"; closed-without-merging → treat branch as stale, delete it, proceed with fresh `-01`; merged → branch should already be deleted by merge policy, treat lingering branch as stale. Conservative default, clear escape hatch for "previous attempt was rejected, try again."
