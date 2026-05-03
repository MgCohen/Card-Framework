---
description: End-of-session routine — runs linter + freshness check + chat-log scan; surfaces findings as interactive prompts.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are running the end-of-session routine. There is no "closing" a session — `SESSION-CHECKPOINT.md` is the live spine and is updated continuously as work happens. This skill is a routine bundle: run integrity checks, surface findings, ask the user what to do per category. **Interactive triage**, not a state gate.

## Output discipline

**No prose preamble. No mid-step narration.** Output begins at the H1 of the triage report in step 4. Tool calls in steps 1–3 are silent until step 4's render begins. Emoji anchors follow `Product/_workflow/EMOJI-LEGEND.md`.

## Steps

### 1. Linter

Run `/builder:lint` against `Product/`. Capture findings (rule code + path + detail). The linter is report-only; it doesn't write to any ledger.

### 2. Freshness check

Compare `SESSION-CHECKPOINT.md`'s git mtime against any `Product/features/**` or `Product/ledgers/**` file touched since CHECKPOINT's last edit:

```bash
checkpoint_mtime=$(git log -1 --format=%ct Product/SESSION-CHECKPOINT.md)
touched=$(git log --since="@$checkpoint_mtime" --name-only --format= -- 'Product/features/**' 'Product/ledgers/**' | sort -u)
```

If `$touched` is non-empty, CHECKPOINT may be stale relative to recent work.

### 3. Chat-log scan for unsaved sketches

Read the conversation transcript of the current session. Identify every artifact that has a concrete shape in the conversation — interfaces, protocols, records, model decisions, pseudocode, named contract shapes, ADR-worthy choices.

For each artifact, classify it:
- **Accepted** — the user gave an explicit positive verdict on the shape, or it was used/referenced as authoritative in subsequent turns.
- **Not accepted** — the shape was proposed but rejected, or explicitly set aside.
- **Uncertain** — discussed but no clear verdict; or left open by design.

Then, for each Accepted artifact, check its canonical `Product/` file (if one exists) and determine whether the shape is already captured accurately.

### 4. Render the triage report

Surface all three categories as one prompt. Render order is fixed — follow exactly.

**1. Title** (H1) — `# Session wrap`.

**2. Linter findings** (H2 + table when findings exist) —

```
## ⚠️ Linter findings
| Rule | Doc | Detail |
|---|---|---|
| <rule> | <path:line> | <short detail> |
```

Empty state (collapses the table): `✅ Linter clean — no findings`.

**3. Freshness check** (H2 + body) —

```
## 🕐 Freshness check
CHECKPOINT is stale relative to:
- `<path>` — touched <duration> after last CHECKPOINT edit
- `<path>` — touched <duration> after last CHECKPOINT edit
```

Empty state: `🕐 CHECKPOINT is current`.

**4. Sketches found this session** (H2 + table — load-bearing; this is what gates whether unsaved interfaces actually get persisted) —

```
## 🔍 Sketches found this session
| Artifact | Verdict | File match |
|---|---|---|
| `IHandlerRegistry` interface | ✅ Accepted | ❌ Not in file |
| `GameEngineBuilder` builder pattern | ✅ Accepted | ✅ Matches engine/01 §6 |
| `ICommandPipeline` stub | ⚠️ Uncertain | ❌ Not in file |
| Queue ordering sketch | ❌ Not accepted | — |

_Review and correct if needed. Accepted + not-in-file items will be written when you route them to "Fix now"._
```

Empty state (collapses the table): `🔍 No sketches surfaced — operational session with no design work`.

**5. Routing prompt** (H2 + bullet list + italic closing) —

```
## 🎯 What's next

For each category above:
- **Fix now** — apply edits in this turn (refresh CHECKPOINT, save sketches to canonical files, fix lint findings).
- **Defer to OPEN-ITEMS** — append a paragraph entry per item to `Product/ledgers/OPEN-ITEMS.md`.
- **Ignore** — do nothing. The user is taking responsibility for the gap.

_What's the call?_
```

### 5. Act on user direction

For each category the user routes:
- **Fix now** — apply edits in the current conversation:
  - **Sketches → canonical file.** Use the exact shape from the conversation — do not paraphrase a typed interface; write the interface. If no canonical file exists, create one via `/builder:new` first.
  - **Linter findings.** Apply the mechanical fix (broken-reference repair, structure violation, etc.) per the rule's guidance.
  - **CHECKPOINT freshness.** Update `## Where we stopped` and the active-focus framing.
- **Defer to OPEN-ITEMS** — append a paragraph entry per item to `Product/ledgers/OPEN-ITEMS.md` following the §6 entry shape (`id`, optional `kind`, **Title** — paragraph framing, References).
- **Ignore** — do nothing. The user is taking responsibility for the gap.

Don't refuse or block. The user routes; you execute.

## Safety rails

- **No artifact at session boundary.** Don't create a session note. CHECKPOINT is the only spine.
- **Don't auto-fix without confirmation.** Surface findings; act on user direction.
- **Don't write to any deleted ledger.** ALIGNMENT-DEBT, OPEN-QUESTIONS, BACKLOG are gone — only OPEN-ITEMS exists.
- **Snapshots are opt-in.** If the user wants to freeze this state as a milestone, suggest `/builder:snapshot`. Otherwise, don't copy CHECKPOINT to `sessions/`.
- **Emoji discipline.** Use only the legend's "both" or "chat only" entries. Retired emojis (🔀 parallel info, 📦 backlog reference, 📝 as session-note reference) must not appear in their retired senses.
