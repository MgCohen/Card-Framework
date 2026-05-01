---
description: Open a new session — read checkpoint + ledgers, render the briefing per REPORT-STYLE.md §3.1, pre-create the session note.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are starting a new Product Builder session. Workflow lives in `Product/_workflow/PRODUCT-BUILDER.md`. Do not invent steps — follow this skill exactly.

## Steps

1. **Read inputs.** In parallel:
   - `Product/SESSION-CHECKPOINT.md`
   - `Product/ledgers/OPEN-QUESTIONS.md`
   - `Product/ledgers/ALIGNMENT-DEBT.md`
   - `Product/ledgers/BACKLOG.md`

2. **Identify the session.** Check whether `SESSION-CHECKPOINT.md` frontmatter contains `schema-version: 2`.

   ### v1 checkpoint (no `schema-version: 2`) — legacy path

   `ls Product/sessions/session-*.md | sort | tail -1` gives the latest existing file (`session-LL.md`).

   **Resume detection (v1).** If **all** of the following are true, this is a dangling skeleton — *resume* it:
   - `session-LL.md` front matter has `status: in-progress`.
   - `SESSION-CHECKPOINT.md` `Last updated:` line still cites session `LL−1`.
   - `session-LL.md` has empty `### Work done` and `### Decisions settled` sections.

   In resume mode: set `NN = LL`, skip step 3, add `🔄 Resuming session NN — prior skeleton was never closed` under the H1 in step 5.

   Otherwise: set `NN = LL + 1`, pad to two digits, proceed to step 3 (v1 path).

   ### v2 checkpoint (has `schema-version: 2`) — parallel-safe path

   Read `head` from checkpoint frontmatter. This is the `parent` for the new session.

   **Argument check.** If the skill was invoked with a `--new` argument (e.g., `/builder:start-session --new`), skip the resume branch and proceed directly to **Normal start (v2)** below — but **still run the orphan-cleanup pass** first. The `--new` flag is the explicit user signal: "I want a fresh parallel session, not a resume."

   **Orphan-cleanup pass.** Before resume detection, scan `Product/sessions/` for all files with `status: in-progress`. For each, classify as **empty skeleton** (frontmatter `work-summary` is `""` or absent AND `### Work done` has no bullets AND `### Decisions settled` has no bullets) or **active session** (any work content present).

   Apply cleanup rules — single-skeleton invariant: at most one in-progress file, with `parent == checkpoint.head`, is allowed to survive.

   - **Empty skeleton, `parent ∉ {checkpoint.head} ∪ checkpoint.merged[]`** → orphan. Refers to a parent that no longer exists in the checkpoint lineage (typically left over from an abandoned session or a parallel-writer race like GHA debt-pass). Flip frontmatter to `status: discarded`, set `last-updated` to today, in-place edit only (do not rename or move). Track for the briefing.
   - **Empty skeleton, `parent == checkpoint.head`, but 2+ such skeletons exist** → keep the one with the latest `started-at`; flip the rest to `status: discarded`. Track for the briefing.
   - **Active session (any classification)** → never auto-discard. If 2+ active sessions exist, or an active session has `parent ∉ {checkpoint.head} ∪ checkpoint.merged[]`, surface loudly: `⚠️ <N> active in-progress session(s) found: <list with parents>. Cannot auto-clean — close or discard manually before starting a new session.` and stop. Wait for the user.

   After cleanup, the remaining in-progress set has at most one entry. Apply resume detection to that survivor.

   **Resume detection (v2).** From the surviving in-progress files (≤ 1 after cleanup):
   - Found, and its `parent` == `checkpoint.head` → resume mode. Use this session as-is, skip step 3. Add `🔄 Resuming <session-id> — prior skeleton was never closed` under the H1 in step 5.
   - Found, and its `parent` ∈ `checkpoint.merged[]` but ≠ `checkpoint.head` → a merge occurred since this session started. Surface: `⚠️ In-progress session <id> has parent <X> but checkpoint.head is now <Y> — a merge landed mid-session. Options: (a) close this session on its branch (end-session will skip checkpoint; merge-pass reconciles later), or (b) discard the skeleton and start fresh with the updated parent.` Wait for user decision before continuing.
   - None found → normal start.

   **Cleanup surfacing.** If the orphan-cleanup pass discarded any files, append to the briefing under the H1 in step 5: `🧹 Discarded <N> orphan skeleton(s): <comma-separated list>. (Empty skeletons left over from a parallel writer or abandoned session. Files remain on disk with status: discarded — delete manually if desired.)`

   **Normal start (v2).** Generate:
   ```
   session-id = session-<UTC-YYYYMMDD>-<HHMM>-<slug>
   ```
   `slug` = first 3–4 significant words from the active question, lower-case kebab-case (e.g., `gamestate-slice-catalog`). Fall back to `untitled` if the active question is empty. Examples: `session-20260502-0915-slice-catalog`, `session-20260502-1030-zone-topology`.

3. **Pre-create the session note.** (Skipped in resume mode.)

   **v1 path** — target: `Product/sessions/session-NN.md`

   Front matter:
   ```yaml
   ---
   type: session
   status: in-progress
   last-updated: <today, ISO date>
   date: <today, ISO date>
   tags: [#session]
   ---
   ```
   Title: `# Session NN — <today>`. In Part 1 "Active question coming in," paste the checkpoint's active open question verbatim. Leave the rest empty.

   **v2 path** — target: `Product/sessions/<session-id>.md`

   Front matter:
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

4. **Debt count.** Count open entries in `ledgers/ALIGNMENT-DEBT.md` (lines not struck through, marked `open`). This is informational — the GHA debt-pass workflow handles discharge automatically. If the count is high (≥ 5), surface it in the briefing as `📊 N debt · ⏳ awaiting automated discharge` so the user knows pass is pending. Do **not** force the session into pass-mode and do **not** auto-invoke `/builder:debt-pass`.

5. **Render the briefing per `Product/_workflow/REPORT-STYLE.md` §3.1.** Follow the locked render order, anchor headings, and empty-state strips exactly.

   **Data sources for each section:**
   - **Title focus phrase** — derived from the active question (concise noun phrase).
   - **🎯 Suggested focus** — checkpoint's "active open question," verbatim or lightly rewritten as one sentence.
   - **🕐 Last session** — derived from checkpoint's `## Where we stopped`:
     - Single bullet → one-line strip: `🕐 Last session: <text>`.
     - Multiple bullets (parallel batch) → H3 heading + bullet list: `### 🕐 Last session` with one bullet per entry, verbatim.
     - No bullets → grep the prior session note's outcome section for a bridge line.
     - Session 01 → render `🕐 First session — no carry-in context`.
   - **❓ Carry-over** — top entries from `OPEN-QUESTIONS.md` (table: `Topic | Since`, where `Since` is the session number / session-id the entry was opened). If 0 open, render `❓ No open questions`.
   - **📊 Debt** — count from step 4. Append `· ⏳ awaiting automated discharge` when count ≥ 5 (informational; GHA debt-pass handles it). If 0, render `📊 0 debt · clean slate`.
   - **📦 Backlog** — top open entries from `BACKLOG.md` (table: `Title | Trigger`, where `Title` is the item title without id prefix, and `Trigger` is the trigger condition truncated to ≤8 words). Cap at 5 entries. If 0 open, render `📦 No backlog items`.
   - **📝 Session doc** — path of the pre-created session note.
   - **🔀 Parallel info** (v2 only, omit when on `main`) — `Branch: <current-branch> · parent: <checkpoint.head> · Parallel-safe — merge-pass will reconcile.`
   - **Closing prompt** — `_Confirm or pivot._`

6. **Wait for the user's confirm-or-pivot.** Do not begin session work in this command — that's a follow-up turn.

## Notes

- Don't auto-edit the checkpoint or any ledger here.
- **No remote effects.** Creates the session note locally only. Do not `git add`, commit, or push. The session note stays gitignored until `/builder:end-session` force-adds it with `git add -f`.
- **No prose preamble.** Output goes straight to the H1 — no narration before or between sections.
- If the checkpoint is more than one session old (its `last-updated` doesn't match the previous session's date), warn in the briefing — the active question may be stale.
- If `Product/sessions/<session-id>.md` (v2) or `Product/sessions/session-NN.md` (v1) already exists and doesn't match the resume-detection signature, do not overwrite — surface that and ask the user what to do.
