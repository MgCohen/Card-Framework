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

2. **Identify session number.** `ls Product/sessions/session-*.md | sort | tail -1` gives the latest. Increment by one. Pad to two digits (`session-13.md`, not `session-013.md`).

3. **Pre-create `Product/sessions/session-NN.md`** from `Product/_workflow/_templates/session.md`. Front matter:
   ```yaml
   ---
   type: session
   status: in-progress
   last-updated: <today, ISO date>
   date: <today, ISO date>
   tags: [#session]
   ---
   ```
   Title: `# Session NN — <today>`. In Part 1 "Active question coming in," paste the line from the checkpoint's "active open question" section verbatim. Leave the rest of the template empty for the session to fill in.

4. **Threshold check.** Count open entries in `ledgers/ALIGNMENT-DEBT.md` (entries that are not struck through and are marked `open`). If the count is **> 5**, this session is forced into pass-mode per `PRODUCT-BUILDER.md` §5.8 — surface this loudly in the briefing.

5. **Render the briefing per `Product/_workflow/REPORT-STYLE.md` §3.1 (start-session template).** Follow the locked render order, anchor headings, and empty-state strips exactly — do not invent extra sections, do not collapse or reorder.

   **Data sources for each section:**
   - **Title focus phrase** — derived from the active question (concise noun phrase).
   - **🎯 Suggested focus** — the checkpoint's "active open question" line, verbatim or lightly rewritten as a single sentence.
   - **🕐 Last session** — read the checkpoint's "Where we stopped" content. If the checkpoint doesn't carry a one-line bridge yet (older format), grep the prior session note's Part 2 outcome section. For session 01, render `🕐 First session — no carry-in context`.
   - **❓ Carry-over** — top entries from `OPEN-QUESTIONS.md` (table form: `Topic | Since`, where `Since` is the session number the entry was opened). If 0 open, render `❓ No open questions`.
   - **📊 Debt** — count from step 4. Append `· ⚠️ pass-mode` when count > 5. If 0 debt, render `📊 0 debt · clean slate`.
   - **📝 Session doc** — the path of the session note pre-created in step 3.
   - **Closing prompt** — `_Confirm or pivot._` (italic, no emoji).

6. **Wait for the user's confirm-or-pivot.** Do not begin the session work in this command — that's a follow-up turn.

## Notes

- Don't auto-edit the checkpoint or any ledger here. This command opens the session; the session itself does the work; `/builder:end-session` closes it.
- If the checkpoint is more than one session old (its `last-updated` doesn't match the previous session's date), warn in the briefing — the active question may be stale.
- If `Product/sessions/session-NN.md` already exists, do not overwrite — surface that and ask the user what to do.
