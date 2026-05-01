---
description: Open a new session — read checkpoint + ledgers, render the briefing, pre-create the session note.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are starting a new Product Builder session. Workflow lives in `Product/_workflow/PRODUCT-BUILDER.md`. Do not invent steps — follow this skill exactly.

## Output discipline

**No prose preamble. No mid-step narration.** Output begins at the H1 of the briefing in step 5. Do not write text before, between, or after tool calls in steps 1–4. Tool calls are silent until step 5's render begins.

## Steps

1. **Read inputs and discover sessions.** Single parallel batch — five tool calls in one turn:
   - `Read Product/SESSION-CHECKPOINT.md`
   - `Read Product/ledgers/OPEN-QUESTIONS.md`
   - `Read Product/ledgers/ALIGNMENT-DEBT.md`
   - `Read Product/ledgers/BACKLOG.md`
   - `Grep` pattern `^status: in-progress` in `Product/sessions/`, `output_mode=files_with_matches` — yields the in-progress file list for step 2.

   Do not separately `ls` or `Glob` the sessions directory. The grep above is the canonical discovery call.

2. **Identify the session.** Read `head` from the checkpoint's frontmatter — this is the `parent` for any new session.

   **Argument check.** If invoked with `--new` (e.g., `/builder:start-session --new`), skip the resume branch and proceed to **Normal start** below — but still run the orphan-cleanup pass. The `--new` flag is the explicit signal: "fresh parallel session, not a resume."

   **Orphan-cleanup pass.** From the in-progress files (step 1's grep result), read each file and classify:
   - **Empty skeleton** — frontmatter `work-summary` is `""` or absent AND `### Work done` has no bullets AND `### Decisions settled` has no bullets.
   - **Active session** — any work content present.

   Single-skeleton invariant: at most one in-progress file with `parent == checkpoint.head` is allowed to survive.

   - **Empty skeleton, `parent ∉ {checkpoint.head} ∪ checkpoint.merged[]`** → orphan. Refers to a parent no longer in the lineage (typically left over from an abandoned session or a parallel-writer race like GHA debt-pass). Flip frontmatter to `status: discarded`, set `last-updated` to today, in-place edit only (do not rename or move). Track for the briefing.
   - **Empty skeleton, `parent == checkpoint.head`, but 2+ such skeletons exist** → keep the one with the latest `started-at`; flip the rest to `status: discarded`. Track.
   - **Active session (any classification)** → never auto-discard. If 2+ active sessions exist, or an active session has `parent ∉ {checkpoint.head} ∪ checkpoint.merged[]`, surface loudly: `⚠️ <N> active in-progress session(s) found: <list with parents>. Cannot auto-clean — close or discard manually before starting a new session.` and stop.

   After cleanup, the remaining in-progress set has at most one entry.

   **Resume detection.** From the surviving in-progress files (≤ 1):
   - Survivor has `parent == checkpoint.head` → resume mode. Use as-is, skip step 3. Render `🔄 Resuming <session-id>` strip in step 5.
   - Survivor has `parent ∈ checkpoint.merged[]` but ≠ `checkpoint.head` → a merge occurred since this session started. Surface: `⚠️ In-progress session <id> has parent <X> but checkpoint.head is now <Y> — a merge landed mid-session. Options: (a) close this session on its branch (end-session will skip checkpoint; merge-pass reconciles later), or (b) discard the skeleton and start fresh with the updated parent.` Wait for user decision.
   - No survivor → normal start.

   **Normal start.** Generate `session-id = session-<UTC-YYYYMMDD>-<HHMM>-<slug>`:
   - `<UTC-YYYYMMDD>` — use `currentDate` from your environment context. Do not shell out to `date` for the date portion.
   - `<HHMM>` — single `date -u +%H%M` Bash call. Do not call `date` more than once per invocation.
   - `<slug>` — first 3–4 significant words from the active question, lower-case kebab-case (e.g., `gamestate-slice-catalog`). Fall back to `untitled` if the active question is empty.

3. **Pre-create the session note.** (Skipped in resume mode.) Target: `Product/sessions/<session-id>.md`.

   ```yaml
   ---
   type: session
   schema-version: 2
   session-id: <generated-id>
   parent: <checkpoint.head>
   status: in-progress
   started-at: <ISO-8601-UTC-now>
   last-updated: <today-ISO>
   work-summary: ""
   tags: [#session]
   ---
   ```

   Title: `# <session-id>`. In Part 1 "Active question coming in," paste the checkpoint's active open question verbatim. Leave the rest empty.

4. **Debt count.** From `Product/ledgers/ALIGNMENT-DEBT.md`, count two numbers — both informational, both rendered in step 5:
   - **Cascade open** — lines starting with `- id: ad-` AND not containing `source: linter`. This is the threshold-relevant count.
   - **Linter open** — lines starting with `- id: ad-` AND containing `source: linter` (or `source: linter/archive`). Reported separately so the user sees noise debt as noise.

   Informational only — GHA debt-pass handles discharge automatically. If cascade count ≥ 5, the debt strip in step 5 gets `⏳ awaiting automated discharge` appended (linter alone never trips the gate). **Never** auto-invoke `/builder:debt-pass` and **never** gate the session.

5. **Render the briefing.** Output begins now. Render order is fixed — follow exactly. (This template is the canonical source for start-session; `REPORT-STYLE.md §3.1` mirrors it. Keep both in sync if either changes.)

   **1. Title** (H1) — `# Starting <session-id> — <focus>`. `<focus>` is a concise noun phrase derived from the active question. **No backticks or file paths in the H1** (line-height jitter).

   **1a. Resume strip** (conditional) — `🔄 Resuming <session-id> — prior skeleton was never closed`. Only in resume mode.

   **1b. Cleanup strip** (conditional) — `🧹 Discarded <N> orphan skeleton(s): <comma-separated list>. (Empty skeletons left over from a parallel writer or abandoned session. Files remain on disk with status: discarded — delete manually if desired.)` Only when orphan-cleanup discarded files.

   **2. Suggested focus** (H2) — `## 🎯 Suggested focus: <one-sentence focus statement>`. Plain prose — no backticks, no inline code.

   **3. Last session** — derive from checkpoint's `## Where we stopped`:
   - Single bullet → strip: `🕐 Last session: <one-line summary>`. Hard one-line — no semicolons stacking three clauses; pick the headline outcome.
   - Multiple bullets (parallel batch) → H3 + bullet list:
     ```
     ### 🕐 Last session
     - S(<slug> <date>): <work-summary>
     - S(<slug> <date>): <work-summary>
     ```
   - No bullets → grep prior session note's outcome section for a bridge line.
   - First session ever → `🕐 First session — no carry-in context`.

   **4. Carry-over** (H3 + table) — top open entries from `OPEN-QUESTIONS.md`:
   ```
   ### ❓ Carry-over
   | Topic | Since |
   |---|---|
   | <topic> | <session-id or NN> |
   ```
   `Since` = the session that opened the entry. Empty state (replaces the table): `❓ No open questions`.

   **5. Debt** (strip) — `📊 <cascade> cascade · <linter> linter` (drop the ` · <linter> linter` clause if linter count is 0). Append ` · ⏳ awaiting automated discharge` when cascade ≥ 5. Empty state (both 0): `📊 0 debt · clean slate`.

   **5a. Backlog** (H3 + table, conditional) — top open entries from `BACKLOG.md`:
   ```
   ### 📦 Backlog
   | Title | Trigger |
   |---|---|
   | <title> | <trigger ≤8 words> |
   ```
   Cap at 5. Empty state: `📦 No backlog items`.

   **6. Session doc** (strip) — `` 📝 Current session doc: `Product/sessions/<session-id>.md` ``

   **7. Parallel info** (strip, omit when on `main`) — `🔀 Branch: <current-branch> · parent: <checkpoint.head> · Parallel-safe — merge-pass will reconcile.`

   **8. Closing prompt** (italic) — `_Confirm or pivot._`

   **Adaptation rule:** the skeleton is fixed. Sections adapt to *data* (counts, gate state), never to inferred session-type — start-session does not classify the session.

6. **Wait for the user's confirm-or-pivot.** Do not begin session work in this command — that's a follow-up turn.

## Notes

- Don't auto-edit the checkpoint or any ledger here.
- **No remote effects.** Creates the session note locally only. Do not `git add`, commit, or push. The session note stays gitignored until `/builder:end-session` force-adds it with `git add -f`.
- If the checkpoint is more than one session old (its `last-updated` doesn't match the previous session's date), warn in the briefing — the active question may be stale.
- If `Product/sessions/<session-id>.md` already exists and doesn't match the resume-detection signature, do not overwrite — surface that and ask the user what to do.
- Schema is v2-only. Projects on v1 must run the schema-cutover PR (see `PLANS/parallel-sessions.md` §5) before this skill works.
