---
description: Auto-discharge `ALIGNMENT-DEBT.md` — mechanical edits, single commit, PR for review.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are running an unattended alignment-debt discharge pass. Workflow rules: `Product/_workflow/PRODUCT-BUILDER.md` §5.5 / §5.7 / §5.8. Plan: `PLANS/auto-debt-pass.md`. The PR you open is the only review gate — be conservative, surface ambiguity, never guess.

## Performance — turn count matters

Each tool call is **one turn**. The action runs with a bounded `--max-turns` budget; running out of turns mid-pass loses all in-progress work. Optimize aggressively:

- **Batch independent tool calls into one turn.** Multiple `Read` calls in parallel, multiple `Edit` calls in parallel, multiple `Bash` invocations in parallel — the harness supports it. Sequential calls are only correct when later calls depend on earlier results.
- **Use one bash call per logical operation, not per file.** Term replacements across one file: single `sed -i -E` with multiple `s///` patterns. Multi-file straggler scan: single `grep -rE 'a|b|c'` with regex alternation. Git stage + commit + push: single chained `git add ... && git commit ... && git push`.
- **Cache reads.** Read each file at most once per pass — extract everything you need on first read, don't re-read inside loops.

The agent's value is judgment (confidence-gate routing, auto-flag categorization, active question copy). Mechanical execution belongs in bash, not in many small tool calls.

## Steps

1. **Sync.** `git fetch origin && git pull --ff-only`. **Refuse to start if the working tree is dirty** — exit with `working tree dirty, refusing to run` and stop. The agent runs from whatever ref the trigger placed us on: `main` for organic GHA fires (the `push: branches: [main]` trigger), the dispatched ref for `workflow_dispatch` (used by Layer 3 validation), and the developer's chosen branch for local runs (the runbook says to be on `main` before invoking — see [`Product/_workflow/_validation/debt-pass-runbook.md`](../../Product/_workflow/_validation/debt-pass-runbook.md)). Do not `git checkout main`; that would silently switch the dev away from a feature branch on accidental local invocation, and would break Layer 3 dispatch tests by switching away from the seeded branch.

2. **Threshold check.** Count open entries in `Product/ledgers/ALIGNMENT-DEBT.md` (lines matching `^- id: ad-` — unresolved entries start with `- id:`, resolved entries start with `- ~~`). If **< 5**, exit with `nothing to do (count=N, threshold=5)` and stop. The GHA threshold gate is the primary check; this is a defensive secondary.

3. **In-progress session pre-check.** Run `grep -rl 'status: in-progress' Product/sessions/`. If any file matches, exit with `session in progress (<filename>), refusing to run — close or discard it first` and stop. **This check fires here, before any expensive discharge work, on purpose** — a concurrent in-progress session means the workspace is not in a clean state for an unattended pass.

4. **Branch collision check.**
   a. `git fetch origin --prune`.
   b. Enumerate candidates: `git ls-remote --heads origin 'claude/debt-pass-*'`.
   c. For each match, classify via `gh pr list --head <branch> --state all --json number,state,url`:
      - **PR open** → exit with `prior pass in PR #X awaiting review, skipping` and stop.
      - **PR closed without merging** → `git push origin --delete <branch>`, continue.
      - **PR merged** or **no PR found** → treat as stale, `git push origin --delete <branch>`, continue.
   d. Once all matches are resolved, derive the timestamp with `date -u +%Y-%m-%d-%H%M` and `git checkout -b claude/debt-pass-<YYYY-MM-DD-HHMM>`. Timestamp-based names don't collide; the branch-collision check above handles any prior-run branches.

5. **Discharge loop — batched.** Optimize for low turn count (each tool call is one turn; max-turns is bounded). The discharge is a deterministic transformation once you have the data, so batch everything.

   a. **Parse all entries in one pass.** Read `Product/ledgers/ALIGNMENT-DEBT.md` once and extract `(source, target, reason, date)` tuples for every line in `## Open` ending in `| open`.

   b. **Read all source + target paths in parallel.** Single turn with multiple `Read` tool calls (one per unique path). Cache the contents — do not re-read inside the per-entry loop.

   c. **Confidence-check pass.** For each entry, classify as `mechanical` or `skipped` (if `reason` contains `rewrite`, `redesign`, `reshape`, `restructure`, or `reconsider`). Build two lists in memory. Skipped entries stay `open` in the ledger and feed the PR body's `## Skipped — needs a session` section.

   d. **Mechanical edit batch.** Group mechanical entries by target file. For each target file:
      - **Term replacements** (most common — e.g. `replace OldFooBar with NewFooBar`): apply *all* replacements for that file in a single `sed -i -E` bash call:
        ```bash
        sed -i -E '
          s/\bOldFooBar\b/NewFooBar/g
          s/\bLegacyBaz\b/ModernBaz/g
        ' Product/path/to/target.md
        ```
        Use `\b` word boundaries to avoid partial matches. One bash invocation per target file (parallel across files where the agent can run multiple bash calls in one turn).
      - **Section-specific edits** (less common — e.g. `update §6 to ...`): single `Edit` tool call per change. Acceptable when batching doesn't help because the edit isn't a global replace.

   e. **Ledger strikethrough batch.** Apply all strikethroughs in a single `Edit` tool call (replace_all=false, with one large old_string → new_string spanning the affected entries) — or one `awk`/`python` pass if the entry count is large enough that a single Edit's old_string would be unwieldy. Strikethrough format (v2):
      ```markdown
      - ~~id: ad-YYYYMMDD-NNN | status: open | source: ... | target: ... | reason: ...~~ → resolved YYYY-MM-DD (auto debt-pass: <one-line note>)
      ```
      All entries live in `## Entries` — no section move required.

6. **Hygiene scan — batched.** Extract retired terms from each discharged entry's `reason` field (terms named with `replace with X`, `→ X`, `swap to X`, etc.). Then:

   a. **Single multi-term grep.** Use one bash call with regex alternation across all retired terms:
      ```bash
      grep -rnE '\b(OldFooBar|LegacyBaz|DeprecatedZed|RetireWidget)\b' Product/ \
        --exclude='ledgers/ALIGNMENT-DEBT.md' \
        --exclude-dir='_workflow/_validation'
      ```
      Two exclusions: the ledger itself, because its strikethrough resolution-note format echoes the retired term verbatim and would false-positive every pass; and the validation harness directory, which deliberately documents fixture term names — `OldFooBar`, `LegacyBaz`, etc. — and must remain stable across debt-pass runs. Use `\b` word boundaries to avoid substring matches.

   b. **Batched replacement.** If stragglers are found, group hits by file. For each file, apply all replacements in a single `sed -i -E` bash call (same shape as step 5d term-replacement batch). One bash invocation per straggler file.

   c. **Batched ledger entry append.** Open new ledger entries for all stragglers caught **and immediately strike them through** (opened-and-closed in this same pass). Apply via a single `Edit` tool call appending to the appropriate `## Resolved` block (or one if multiple sections). All stragglers feed the PR body's `## Stragglers caught` section.

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

8. **Checkpoint — branch mode, skip.** Debt-pass runs on a feature branch. Do not write `SESSION-CHECKPOINT.md`. The checkpoint will be reprojected by `/builder:merge-pass` when this branch merges, using `_project-checkpoint` with the session note written in step 9 as input. Print: `Checkpoint update deferred to /builder:merge-pass.`

9. **Write the session note (v2).** Derive timestamp from `date -u +%Y%m%d-%H%M`. Target path: `Product/sessions/session-<YYYYMMDD>-<HHMM>-debt-pass.md`. Read `checkpoint.head` from `Product/SESSION-CHECKPOINT.md` for the `parent:` field. Scaffold front matter:
   ```yaml
   ---
   type: session
   schema-version: 2
   session-id: session-<YYYYMMDD>-<HHMM>-debt-pass
   parent: <checkpoint.head>
   status: completed
   started-at: <today>T<HHMM>:00Z
   last-updated: <today>
   date: <today>
   work-summary: "Auto debt-pass: <N> entries discharged, <M> stragglers caught, <K> skipped."
   tags: [#session, #debt-pass]
   ---
   ```
   Title: `# Debt-pass — <today> (auto)`.
   - **Active question coming in** — copy verbatim from the prior checkpoint's `## 🎯 The active open question (singular)`.
   - **Work done** — bullets: `<N> debt entries discharged`, `<M> stragglers caught`, `<K> skipped — needs a session`, `retired-tokens.json: <list of new terms>`.
   - **Decisions settled** — none (debt-pass is mechanical; debit `none`).
   - **Active question going out** — copy verbatim from the prior checkpoint's active question. Debt-pass does not introduce its own.
   - **Followups created** — none routed beyond the skipped entries (already in the ledger).
   - **References** — list of touched docs (from the discharge + hygiene loops).

10. **Defensive empty-pass check.** Run `git status --porcelain Product/` — if zero changes, exit with `empty pass — nothing to commit, refusing to open PR` and stop. Per §3 of the plan: never open a PR with zero `Product/` changes.

11. **Single commit + push — chained.** Stage, commit, and push in **one bash invocation** to keep this to a single turn:
    ```bash
    git add Product/ledgers/ALIGNMENT-DEBT.md \
            Product/_workflow/retired-tokens.json \
            <other touched paths from steps 5–6> \
      && git add -f Product/sessions/session-<YYYYMMDD>-<HHMM>-debt-pass.md \
      && git commit -m "Auto debt-pass <YYYY-MM-DD> — <N> closed, <M> stragglers, <K> skipped" \
      && git push -u origin claude/debt-pass-<YYYY-MM-DD-HHMM>
    ```
    Note: the session note requires `git add -f` — `Product/sessions/session-*.md` is gitignored; `-f` force-tracks it at commit time.
    - Stage all `Product/` changes (entry edits, registry update, ledger strikethroughs, checkpoint, session note) by **explicit paths**. Never `git add -A`. Never use `--force`, `--amend`, or `--no-verify`.
    - Subject: `Auto debt-pass NN — <N> closed, <M> stragglers, <K> skipped`
    - Body: bulleted summary mirroring the PR sections in step 12. Include `https://claude.ai/code/<session>` trailer per harness convention.
    - The chain (`&&`) means a failure at any link aborts the rest — safer than three independent calls.

12. **Open the PR.** `gh pr create --base main --head claude/debt-pass-<YYYY-MM-DD-HHMM> --title "Auto debt-pass <YYYY-MM-DD> — <N> closed, <M> stragglers, <K> skipped" --body @-`, body shape:

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
- Run if any session file has `status: in-progress` at pre-flight (step 3) — refuse early to avoid running alongside an active session.

If any rule would be violated, stop and surface — never silently bypass.

## Notes

- This command is invoked unattended from `.github/workflows/debt-pass.yml` via `anthropics/claude-code-action@v1`. The PR is the human-review surface; the command itself has no interactive prompts.
- The threshold gate lives in the GHA workflow (count ≥ 5 from awk/grep on the ledger). Step 2 in this command is a defensive secondary check — it should rarely fire on its own.
- If `/builder:end-session` later exposes a headless mode (see `BACKLOG.md`), step 7's local fill rules can delete and that step delegates instead. The active question is the only piece that meaningfully carries forward; everything else is mechanical bookkeeping.
- Manual run for debugging: `claude -p "/builder:debt-pass"` from a clean working tree on `main` with `gh` authenticated. The branch-collision check makes re-runs safe — a prior PR will be detected and the run will skip.
- Layer 2 local validation: see [`Product/_workflow/_validation/debt-pass-runbook.md`](../../Product/_workflow/_validation/debt-pass-runbook.md) for the pre-seeded fixture, override prompt, six-watchpoint checklist, and cleanup sequence — re-run after any change to this command.
