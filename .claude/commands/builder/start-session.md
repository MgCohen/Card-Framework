---
description: Open a new session — read CURRENT + ledgers, brief the user on where we are.
allowed-tools: Read, Bash, Glob, Grep
---

You are starting a new Product Builder session. Workflow lives in `Product/_workflow/PRODUCT-BUILDER.md`. This skill is **read-only**: brief the user on state, propose threads, wait for direction. Do not create any artifact.

## Steps

1. **Read the live state**, in order:
   - `Product/SESSION-CHECKPOINT.md` — the live spine. Active focus, open items snapshot, scratch section.
   - `Product/ledgers/OPEN-ITEMS.md` — durable loose ends.
   - `Product/ledgers/GLOSSARY.md` — vocabulary.
   - `Product/ledgers/FEATURE-MAP.md` — feature dependency snapshot.

2. **Render the briefing** as a concise message to the user:

   ```
   # Where we are

   <one-paragraph synthesis of current state from CHECKPOINT>

   ## Active focus
   <Active focus line from CHECKPOINT>

   ## Top open items
   <up to 3 highest-leverage entries from OPEN-ITEMS.md, by id + title>

   ## Scratch (mid-conversation, unpromoted)
   <contents of CHECKPOINT ## Scratch section, or "none">

   ## Suggested threads
   1. <Top open item or active focus continuation>
   2. <Second viable thread>
   3. <Third viable thread>

   Which one?
   ```

3. **Wait for direction.** Don't guess; don't open work without explicit confirmation.

## Safety rails

- **Read-only.** Do not modify any file. Do not create any artifact.
- **No orphan cleanup, no schema validation.** The lightweight checkpoint format has no schema to validate.
- **Don't read the workflow doc** (`_workflow/PRODUCT-BUILDER.md`) unless the user asks about the workflow itself.
