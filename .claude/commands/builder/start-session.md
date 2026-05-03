---
description: Open a new session — read CURRENT + ledgers, brief the user on where we are.
allowed-tools: Read, Bash, Glob, Grep
---

You are starting a new Product Builder session. Workflow lives in `Product/_workflow/PRODUCT-BUILDER.md`. This skill is **read-only**: brief the user on state, propose threads, wait for direction. Do not create any artifact.

## Output discipline

**No prose preamble. No mid-step narration.** Output begins at the H1 of the briefing in step 2. Tool calls in step 1 are silent until step 2's render begins. Emoji anchors follow `Product/_workflow/EMOJI-LEGEND.md` — strict 1:1 with workflow concepts.

## Steps

1. **Read the live state** — single parallel batch, four reads in one turn:
   - `Product/SESSION-CHECKPOINT.md` — the live spine. Active focus, open items snapshot, scratch section.
   - `Product/ledgers/OPEN-ITEMS.md` — durable loose ends.
   - `Product/ledgers/GLOSSARY.md` — vocabulary.
   - `Product/ledgers/FEATURE-MAP.md` — feature dependency snapshot.

2. **Render the briefing.** Render order is fixed — follow exactly.

   **1. Title** (H1) — `# Where we are`. No backticks or file paths in the H1.

   **2. Where we stopped bridge** — derive from CHECKPOINT's `## Where we stopped` paragraph:
   - Single concise paragraph → strip: `🕐 Last session: <one-line summary>`. Hard one-line — no semicolons stacking three clauses; pick the headline outcome.
   - Multi-thread context → H3 + bullet list:
     ```
     ### 🕐 Last session
     - <bridge line>
     - <bridge line>
     ```
   - Empty CHECKPOINT (first session) → `🕐 First session — no carry-in context`.

   **3. Active focus** (H2) — `## 🎯 Active focus`. Body: paste CHECKPOINT's `## 🎯 Active focus` line verbatim (the `oq-id` plus one-sentence framing).

   **4. Top open items** (H3 + bullet list) — top 3 highest-leverage entries from `OPEN-ITEMS.md`:
   ```
   ### ❓ Top open items
   - `<oq-id>` — <short framing>
   - `<oq-id>` — <short framing>
   - `<oq-id>` — <short framing>
   ```
   Use the same three the CHECKPOINT highlights when present; otherwise pick from `OPEN-ITEMS.md` by leverage. Empty state: `❓ No open items`.

   **5. Scratch** (H2 + body) — `## 📝 Scratch (mid-conversation, unpromoted)`. Body: contents of CHECKPOINT's `## 📝 Scratch (ephemerals)` section, or `*none*` when empty. If the scratch section has named sub-headings (mid-conversation kick-start briefs, etc.), render the heading + a one-paragraph synthesis rather than dumping verbatim — keep the briefing short.

   **6. Suggested threads** (H2 + numbered list) — three viable threads to direct the session:
   ```
   ## Suggested threads
   1. <Top open item or active-focus continuation>
   2. <Second viable thread>
   3. <Third viable thread>
   ```
   Rank by: (a) active-focus continuation if obvious, (b) durable open items by leverage, (c) scratch entries that have ripened into work-shaped questions.

   **7. Closing prompt** (italic) — `_Which one?_`

3. **Wait for direction.** Don't guess; don't open work without explicit confirmation.

## Safety rails

- **Read-only.** Do not modify any file. Do not create any artifact.
- **No orphan cleanup, no schema validation.** The lightweight checkpoint format has no schema to validate.
- **Don't read the workflow doc** (`_workflow/PRODUCT-BUILDER.md`) unless the user asks about the workflow itself.
- **Emoji discipline.** Use only the legend's "both" or "chat only" entries. Retired emojis (🔀 parallel info, 📦 backlog reference) must not appear.
