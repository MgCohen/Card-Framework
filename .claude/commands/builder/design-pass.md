---
description: Structured Q-by-Q design walk for non-mechanical Model edits. Enumerates questions in dependency order, presents options + lean + rationale per Q, waits for verdict, then advances. Locks design space progressively without committing edits until all Qs settle.
allowed-tools: Read, Write, Edit, Glob, Grep
---

You are running a design pass. This workflow settles open design questions before any doc edits happen. **Design unit ≠ patch unit** — this skill produces locked verdicts, not Model edits. Patches land in a follow-up session once all Qs settle.

## Phase 1 — Setup

1. **Identify the topic.** The topic is either supplied by the user in the invocation argument (e.g., `/builder:design-pass §6 boundary redesign`) or derived from the active session note's "Active question coming in" section. If neither is present, ask the user to state the design question before proceeding.

2. **Read relevant context.** In parallel:
   - Find the in-progress session note (`Product/sessions/session-NN.md` — the file with `status: in-progress`)
   - Any Model docs, ADRs, or feature notes the user referenced or that the topic names
   - `Product/ledgers/OPEN-QUESTIONS.md` for related open threads

3. **Enumerate design questions** in dependency order. Questions that constrain later questions must come first (e.g., "what *is* the entity" before "where does it live"). Surface the proposed list to the user before starting the walk:
   ```
   ## Design questions (N total)
   - Q1: <question>
   - Q2: <question>
   ...
   ```
   Wait for the user to confirm the order or request reordering/additions before advancing to Phase 2.

## Phase 2 — Question walk

Repeat steps 4–6 for each Q in sequence. Do not advance to the next Q until the current one is locked.

4. **Present the question.** Format:
   ```
   ## Q<N> — <question>

   **Options**
   - A. <option description>
   - B. <option description>
   [- C. <option description>]

   **Lean:** <which option you recommend and the core reason, 1–2 sentences>
   ```
   Include exactly as many options as are meaningfully distinct — usually 2–3. Never pad to three for symmetry. If a prior verdict forecloses options that would have appeared here, note the constraint before the option list.

5. **Wait for verdict.** Close with `_Your verdict on Q<N>?_` (italic, no emoji). Stop — do not continue until the user responds.

6. **Confirm and lock.** On receipt of the verdict:
   - Confirm in one sentence: `Q<N> = <option> — <short implication>`.
   - If this verdict forecloses an option on a later Q, note it inline: `Note: this forecloses option X on Q<M>.`
   - If the user's verdict is "I don't know" or introduces a new option not in the set, discuss freely — but do not count the Q as locked and do not advance the counter until a clear verdict is given.
   - If two Qs turn out to be the same question from different angles, merge them, note the merge, and renumber remaining Qs.
   - Advance to the next Q (step 4).

## Phase 3 — Close

7. **Render the closing summary** once all Qs are locked. Follow `Product/_workflow/REPORT-STYLE.md` §3.6 (design-pass template).

8. **Update the session note** (the `status: in-progress` file found in step 2). Append to the relevant sections of Part 1:
   - **Decisions settled** — each locked Q in the shape `Q<N> = <option> — <implication>`, one bullet per Q.
   - **Work done** — add the line: `Design pass only — no doc deltas this session (zero Models/ADRs touched). Patches deferred to a fresh session per "design unit ≠ patch unit."`
   - **Active question going out** — the patch cascade: which Models and ADRs need updating, and in what order.

## Notes

- Do not edit any Model doc, ADR, or feature file during this skill. The design-pass output is the locked decision list and the session-note update. All other edits come after, in a separate session.
- Questions foreclosed by an earlier verdict may be skipped — but state the skip explicitly: `Q<N> = skipped — foreclosed by Q<M> verdict`.
- If no session note is in-progress, complete the walk and render the closing summary, but skip step 8 and surface a warning that the session note must be updated manually.
