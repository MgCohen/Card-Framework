---
description: Structured Q-by-Q design walk for non-mechanical Model edits. Enumerates questions in dependency order, presents options + lean + rationale per Q, waits for verdict, then advances. Locks design space progressively without committing edits until all Qs settle.
allowed-tools: Read, Write, Edit, Glob, Grep
---

You are running a design pass. This workflow settles open design questions through a structured Q-by-Q walk. The output is a locked decision list captured in the session note. Patches and edits are not part of the walk itself, but they can follow immediately in the same session — the goal is to clear the air and build shared context, not to gate later work.

## Phase 1 — Setup

1. **Identify the topic.** The topic is either supplied by the user in the invocation argument (e.g., `/builder:design-pass §6 boundary redesign`) or derived from the active session note's "Active question coming in" section. If neither is present, ask the user to state the design question before proceeding.

2. **Read relevant context.** In parallel:
   - Find the in-progress session note (`Product/sessions/session-NN.md` — the file with `status: in-progress`)
   - Any Model docs, ADRs, or feature notes the user referenced or that the topic names
   - `Product/ledgers/OPEN-QUESTIONS.md` for related open threads

3. **Open with a context summary + question list, then ask Q1 immediately.**

   Render in this order — no user confirmation gate:

   **a. Context summary** — structured, not a paragraph. Include all of the following that apply:
   - **What this is:** one-paragraph explainer of the concept being designed
   - **Known sketches:** paste or quote any existing code/type sketches from Model docs, session notes, or samples that are directly relevant — show the actual snippet, not just a reference
   - **Key design points:** bullet list of constraints, invariants, or settled decisions that the questions must respect
   - **File references:** link every doc that contains relevant context
   - **Examples / cases:** if a concrete game scenario (Pile Counter, MTG-like, etc.) illuminates why a question matters, include it

   **b. Question list** — one line each, no options yet:
   ```
   ## Design questions (N total)
   - Q1: <one-line question>
   - Q2: <one-line question>
   ...
   ```
   Order by dependency — questions that constrain later ones come first.

   **c. Ask Q1 immediately.** Do not wait for the user to approve the list or reorder. Go straight into the Phase 2 format for Q1.

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

7. **Render the closing summary** once all Qs are locked.

   **Render order (rendered at end of Phase 3, not mid-walk):**

   1. **Title** (H1) — `# Design Pass — <topic>`
   2. **Scope** (stat strip) — `📊 <N> questions settled · no doc edits`
   3. **Locked decisions** (H3 + bullet list):
      ```
      ### 🔒 Locked decisions
      - Q1 = <option> — <implication>
      - Q2 = <option> — <implication>
      ```
   4. **Patch cascade** (H3 + bullet list, omit when no downstream patches):
      ```
      ### 🎯 Patch cascade
      - <Model or ADR path> — <what changes>
      ```
   5. **Session note** (strip) — `📝 Session note: Part 1 "Decisions settled" + "Active question going out" updated`. If no in-progress session: `⚠️ No in-progress session note — update manually`.
   6. **Closing prompt** (italic) — `_Decisions locked — ready to keep iterating._`

   **No prose preamble** — output begins at the H1.

8. **Update the session note** (the `status: in-progress` file found in step 2). Append to the relevant sections of Part 1:
   - **Decisions settled** — each locked Q in the shape `Q<N> = <option> — <implication>`, one bullet per Q.
   - **Work done** — add a one-line summary of the design pass: `Design pass on <topic> — N decisions locked.` Subsequent patch/edit work in the same session appends normally.
   - **Active question going out** — the patch cascade: which Models and ADRs need updating, and in what order.

## Notes

- Do not edit any Model doc, ADR, or feature file during the Q-by-Q walk itself — patches mid-walk would race the verdicts. The design-pass output is the locked decision list and the session-note update. Once the walk closes, follow-on edits and patches can land in the same session.
- Questions foreclosed by an earlier verdict may be skipped — but state the skip explicitly: `Q<N> = skipped — foreclosed by Q<M> verdict`.
- If no session note is in-progress, complete the walk and render the closing summary, but skip step 8 and surface a warning that the session note must be updated manually.
