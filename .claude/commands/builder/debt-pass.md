---
description: Auto-discharge `ALIGNMENT-DEBT.md` — mechanical edits, single commit, PR for review. Automation-only (CI or `--force`).
argument-hint: [--force]
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are running an unattended alignment-debt discharge pass. Workflow rules: `Product/_workflow/PRODUCT-BUILDER.md` §5.5 / §5.7 / §5.8. Plan: `PLANS/auto-debt-pass.md`. The PR you open is the only review gate — be conservative, surface ambiguity, never guess.

## Performance — turn count matters

Each tool call is **one turn**. The action runs with a bounded `--max-turns` budget; running out of turns mid-pass loses all in-progress work. Optimize aggressively:

- **Batch independent tool calls into one turn.** Multiple `Read` calls in parallel, multiple `Edit` calls in parallel, multiple `Bash` invocations in parallel — the harness supports it. Sequential calls are only correct when later calls depend on earlier results.
- **Use one bash call per logical operation, not per file.** Term replacements across one file: single `sed -i -E` with multiple `s///` patterns. Multi-file straggler scan: single `grep -rE 'a|b|c'` with regex alternation. Git stage + commit + push: single chained `git add ... && git commit ... && git push`.
- **Cache reads.** Read each file at most once per pass — extract everything you need on first read, don't re-read inside loops.

The agent's value is judgment (confidence-gate routing, auto-flag categorization, active question copy). Mechanical execution belongs in bash, not in many small tool calls.

## Pre-flight — invocation gate

This command is automation-only. Before any other work, allow the run only if **either**:

- `$GITHUB_ACTIONS` is set (CI invocation), **or**
- `$ARGUMENTS` contains `--force` (explicit local override — used when you want to inspect the diff before pushing).

Otherwise, exit immediately with: `debt-pass is automation-only — the GHA workflow runs on the threshold. Pass --force if you really want to discharge locally.` No tool calls beyond the env/argument check.

## Steps

1. **Sync.** `git fetch origin && git pull --ff-only`. **Refuse to start if the working tree is dirty** — exit with `working tree dirty, refusing to run` and stop. The agent runs from whatever ref the trigger placed us on: `main` for organic GHA fires (the `push: branches: [main]` trigger), the dispatched ref for `workflow_dispatch` (used by Layer 3 validation), and the developer's chosen branch for local runs (the runbook says to be on `main` before invoking — see [`Product/_workflow/_validation/debt-pass-runbook.md`](../../Product/_workflow/_validation/debt-pass-runbook.md)). Do not `git checkout main`; that would silently switch the dev away from a feature branch on accidental local invocation, and would break Layer 3 dispatch tests by switching away from the seeded branch.

2. **Threshold check.** Count open **cascade** entries in `Product/ledgers/ALIGNMENT-DEBT.md` — lines matching `^- id: ad-` (unresolved; resolved start with `- ~~`) AND **not** containing `source: linter`. Linter findings (`source: linter` or `source: linter/archive`) are mechanical link-cleanup; they don't count toward the threshold but are still discharged in the same pass when cascade tips the gate. If cascade count **< 5**, exit with `nothing to do (cascade=N, threshold=5)` and stop. The GHA threshold gate is the primary check; this is a defensive secondary.

3. **In-progress session pre-check.** Run `grep -rl 'status: in-progress' Product/sessions/`. For each match, read the file and classify:
   - **Empty skeleton** — `work-summary` is `""` AND `### Work done` has no bullets AND `### Decisions settled` has no bullets. This is a `start-session` placeholder that was never filled. Note the path as `ADOPTED_SKELETON` and continue — debt-pass will fill it in-place at step 9 rather than creating a new file.
   - **Non-empty session** — any actual work content is present. Exit with `session in progress (<filename>), refusing to run — close or discard it first` and stop.

   This check fires before any expensive discharge work on purpose. An empty skeleton is safe to adopt; an active session with real work is not.

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

   For each `mechanical` entry, also tag its **category** by its `source:` field — `linter` if `source:` starts with `linter` (i.e. `linter` or `linter/archive`), otherwise `cascade`. Track these counts (`<C>` cascade discharged, `<L>` linter discharged) — they feed the work-summary in step 9 so the PR title surfaces the discharge shape.

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

9. **Write the session note (v2).** Two paths depending on whether an empty skeleton was adopted in step 3:

   **Path A — no adopted skeleton.** Derive timestamp from `date -u +%Y%m%d-%H%M`. Target path: `Product/sessions/session-<YYYYMMDD>-<HHMM>-debt-pass.md`. Read `checkpoint.head` from `Product/SESSION-CHECKPOINT.md` for the `parent:` field. Scaffold front matter:
   ```yaml
   ---
   type: session
   schema-version: 2
   session-id: session-<YYYYMMDD>-<HHMM>-debt-pass
   parent: <checkpoint.head>
   status: in-progress
   started-at: <today>T<HHMM>:00Z
   last-updated: <today>
   date: <today>
   work-summary: "Auto debt-pass: <C> cascade + <L> linter discharged, <M> stragglers caught, <K> skipped."
   tags: [#session, #debt-pass]
   ---
   ```
   Title: `# Debt-pass — <today> (auto)`. Use the cascade/linter counts from step 5c. If either count is 0, still render both terms (e.g. `0 cascade + 13 linter discharged`) — the explicit zero is informative, not noise.

   **Path B — adopted empty skeleton (`ADOPTED_SKELETON` set in step 3).** Update the existing file in-place. Do not rename or move it. Patch the frontmatter:
   - Set `work-summary: "Auto debt-pass: <C> cascade + <L> linter discharged, <M> stragglers caught, <K> skipped."` (always render both `<C>` and `<L>` even when one is 0).
   - Add `#debt-pass` to `tags` if not already present.
   - Set `last-updated: <today>`.
   - Leave `session-id`, `parent`, `started-at`, `status` unchanged.

   The title and `### Active question coming in` are already filled by `start-session` — do not overwrite them.

   **Both paths — body sections to fill:**
   - **Active question coming in** — (Path A only) copy verbatim from the prior checkpoint's `## 🎯 The active open question (singular)`. Path B already has this.
   - **Work done** — bullets: `<C> cascade entries discharged`, `<L> linter entries discharged`, `<M> stragglers caught`, `<K> skipped — needs a session`, `retired-tokens.json: <list of new terms>`, `debt: none` (debt-pass closes entries; it never opens new ones). Render both cascade and linter bullets even when one is 0.
   - **Decisions settled** — none (debt-pass is mechanical; write `none`).
   - **Active question going out** — copy verbatim from the prior checkpoint's active question. Debt-pass does not introduce its own.
   - **Followups created** — none routed beyond the skipped entries (already in the ledger).
   - **References** — list of touched docs (from the discharge + hygiene loops).

10. **Defensive empty-pass check.** Run `git status --porcelain Product/` — if zero changes, exit with `empty pass — nothing to commit, refusing to open PR` and stop. Per §3 of the plan: never open a PR with zero `Product/` changes.

11. **Delegate close to end-session.** Invoke `/builder:end-session --auto-pr`. End-session will:
    - Find the in-progress session note written in step 9.
    - Skip the work-summary prompt (`work-summary` is pre-filled in frontmatter).
    - Detect branch mode → skip checkpoint write.
    - Run hygiene checks (all sections pre-filled; `debt: none` satisfies the cascade gate).
    - Flip the session note to `completed`.
    - Stage all `Product/` changes by explicit paths; `git add -f` the session note.
    - Commit, push, and auto-create the PR with title `<session-id> — <work-summary>` and body derived from the Work-done bullets and step-4 deltas.

    Print the commit SHA and PR URL on completion.

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
- Run if any session file has `status: in-progress` at pre-flight (step 3) **and has actual work content** — refuse early to avoid stomping an active session. Empty skeletons (blank `work-summary`, empty Work done + Decisions settled) are adopted in-place at step 9; they are not a refusal trigger.

If any rule would be violated, stop and surface — never silently bypass.

## Notes

- This command is invoked unattended from `.github/workflows/debt-pass.yml` via `anthropics/claude-code-action@v1`. The PR is the human-review surface; the command itself has no interactive prompts.
- The threshold gate lives in the GHA workflow (count ≥ 5 from awk/grep on the ledger). Step 2 in this command is a defensive secondary check — it should rarely fire on its own.
- Manual run for debugging: `claude -p "/builder:debt-pass --force"` from a clean working tree on `main` with `gh` authenticated. `--force` is required outside CI — see the pre-flight invocation gate. The branch-collision check makes re-runs safe — a prior PR will be detected and the run will skip.
- Layer 2 local validation: see [`Product/_workflow/_validation/debt-pass-runbook.md`](../../Product/_workflow/_validation/debt-pass-runbook.md) for the pre-seeded fixture, override prompt, six-watchpoint checklist, and cleanup sequence — re-run after any change to this command.
