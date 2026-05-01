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

2. **Identify session number.** `ls Product/sessions/session-*.md | sort | tail -1` gives the latest existing file (call it `session-LL.md`).

   **Resume detection.** If **all** of the following are true, this is a dangling pre-created skeleton — *resume* it instead of bumping:
   - `session-LL.md` front matter has `status: in-progress`.
   - `SESSION-CHECKPOINT.md` `Last updated:` line still cites session `LL−1` (i.e., the checkpoint hasn't advanced to cover `LL`).
   - `session-LL.md` has empty `### Work done` and `### Decisions settled` sections (no real work landed in the skeleton).

   In resume mode: set `NN = LL`, **skip step 3** (the skeleton already exists; do not overwrite), and add a `🔄 Resuming session NN — prior skeleton was never closed` strip directly under the H1 in the rendered briefing (step 5).

   Otherwise (normal case): set `NN = LL + 1`, pad to two digits (`session-13.md`, not `session-013.md`), and proceed to step 3.

3. **Pre-create `Product/sessions/session-NN.md`** from `Product/_workflow/_templates/session.md`. (Skipped in resume mode.) Front matter:
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
   - **📦 Backlog** — every open entry from `ledgers/BACKLOG.md` (entries not struck through). Table form: `Item | Trigger`, where `Item` is the entry's bold title and `Trigger` is the entry's `Trigger:` line condensed to one line. Render in ledger order — do not re-sort or filter. If 0 open, render `📦 Backlog empty`.
   - **📝 Session doc** — the path of the session note pre-created in step 3.
   - **Closing prompt** — `_Confirm or pivot._` (italic, no emoji).

6. **Wait for the user's confirm-or-pivot.** Do not begin the session work in this command — that's a follow-up turn.

## Notes

- Don't auto-edit the checkpoint or any ledger here. This command opens the session; the session itself does the work; `/builder:end-session` closes it.
- **No remote effects.** The skill creates `session-NN.md` locally and renders the briefing — nothing else. Do not `git add`, `git commit`, `git push`, or open a PR. The skeleton stays gitignored until `/builder:end-session` force-adds and commits the filled-in session note alongside the rest of the work (`Product/sessions/session-*.md` is in `.gitignore`; end-session uses `git add -f`). This keeps stop hooks and `git status` quiet during the session without depending on per-user hook config.
- **No prose preamble in the rendered briefing.** Output goes straight to the H1 — no "All inputs read…" / "Now creating the session…" narration before or between sections.
- If the checkpoint is more than one session old (its `last-updated` doesn't match the previous session's date), warn in the briefing — the active question may be stale.
- If `Product/sessions/session-NN.md` already exists *and* doesn't match the resume-detection signature in step 2 (e.g., it has real content, or the checkpoint already advanced past it), do not overwrite — surface that and ask the user what to do.
