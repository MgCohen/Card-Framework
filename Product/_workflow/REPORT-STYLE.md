---
type: model
status: draft
last-updated: 2026-04-30
builds-on: [PRODUCT-BUILDER.md, EMOJI-LEGEND.md]
tags: [#workflow, #meta]
---

# Workflow Report Style

**Purpose.** Conventions for chat-rendered workflow reports (start-session, end-session, promote-glossary, etc.). Defines the primitives (strips, anchor headings, tables), chat-only emoji extensions, and per-skill templates.

**Scope.** Chat output only. Committed `.md` artifacts are governed by `EMOJI-LEGEND.md` (strict) plus their own template conventions; this doc does *not* apply to files written to disk.

---

## 1. Relationship to `EMOJI-LEGEND.md`

`EMOJI-LEGEND.md` is the canonical source of truth for emoji semantics across **committed artifacts**. Chat reports use those same emojis for the same concepts — never overload, never reassign.

Chat reports MAY add **chat-only extensions** — emoji that have a stable role in chat output but don't earn a permanent legend slot. Rules:

- Documented in §2 below, with the same precision as the legend (1:1, single concept, single-codepoint preferred).
- Never appear in committed `.md` files.
- Same 1:1 rule — no overloading with legend symbols, no overlap between extensions.
- **Promotable:** if a chat-only emoji starts appearing across multiple skills with consistent semantics, propose moving it to `EMOJI-LEGEND.md` via the legend's §5 expansion process.

**Current chat-only extensions:**

| Emoji | Concept | Used in |
|---|---|---|
| 🕐 | session outcome / carry-over summary (the bridge line between sessions) | start-session, end-session |
| 📤 | commit/push action | end-session |
| 🔄 | resume signal (re-entering a pre-created skeleton that was never closed) | start-session |
| 🔒 | locked design decision (a settled verdict from a design-pass walk) | design-pass |

---

## 2. Primitives

The reusable building blocks of report shapes. Same emoji rules as the legend: section headers and stat lines only, no inline emoji in prose, no emoji on individual list items.

### 2.1 Title
```
# <verb> Session <NN> — <focus>
```
H1. The verb makes the action explicit (`Starting`, `Closing`). The focus is a short noun phrase.

### 2.2 Anchor heading
H2 form (most prominent — sentence-length content rendered large):
```
## <emoji> <Label>: <content>
```

H3 form (section opener with structured content underneath):
```
### <emoji> <Label>
<content — usually a table or short summary>
```

### 2.3 Strip
```
<emoji> <Label>: <content>
```
Single line, leading emoji anchor, sometimes a label, then content. Used for one-line facts that don't warrant a heading. Strips stack with blank lines between for visual separation.

### 2.4 Stat strip
```
📊 <fact> · <fact> · <fact>
```
Dot-separated key facts. ⚠️ for fired gates may appear inline here (still legend-compliant — stat lines are a permitted location). Counts and mode-flags live here.

### 2.5 Table
Used under an H3 anchor heading. Columns are skill-specific. Render even when the section has a single row, if the per-skill template specifies "always table" (e.g., start-session carry-over).

### 2.6 Closing prompt
```
_Confirm or pivot._
```
Italic. No emoji. Closes the report. Per-skill templates may override the wording.

### 2.7 Result strip
For light skills whose entire output is a single confirmable operation. Pattern:
```
**<verb>:** <subject> [(<metadata>)]
[📝 Session note: <what was patched>]
```

The bold-prose `**<verb>:**` anchor (e.g., `**Created:**`, `**Renamed:**`, `**Patched:**`) carries the action; metadata in parentheses adds type/status detail when useful. The optional 📝 strip below confirms a session-note patch when the skill made one. No H1, no other sections — this is the entire report.

---

## 3. Per-skill templates

### 3.1 start-session

**Render order:**

1. **Title** (H1) — `# Starting Session NN — <focus>`
1a. **Resume strip** (conditional, directly under H1) — `🔄 Resuming session NN — prior skeleton was never closed`. Rendered only when the skill enters resume mode (per `start-session.md` step 2). Omitted in the normal case.
2. **Suggested focus** (H2 anchor heading) — `## 🎯 Suggested focus: <one-sentence focus statement>`
3. **Last session** (strip) — `🕐 Last session: <one-line summary of prior session's outcome>`
4. **Carry-over** (H3 anchor heading + table) —
   ```
   ### ❓ Carry-over
   | Topic | Since |
   |---|---|
   | <topic> | S<NN> |
   ```
5. **Debt** (stat strip) — `📊 <N> debt · threshold <T>` (plus `· ⚠️ pass-mode` when gate fired)
6. **Session doc** (strip) — `` 📝 Current session doc: `Product/sessions/session-NN.md` ``
7. **Closing prompt** (italic) — `_Confirm or pivot._`

**Empty-state strips** (replace the corresponding section):

| Section | Empty render |
|---|---|
| Last session (S01) | `🕐 First session — no carry-in context` |
| Carry-over (0 open) | `❓ No open questions` |
| Debt (0 entries) | `📊 0 debt · clean slate` |

**Adaptation rule:** the skeleton is fixed and renders the same regardless of session-type. Sections adapt to *data* (counts, gate state), never to inferred session-type — start-session does not classify the session.

**Rendering constraints:**

- **No prose preamble.** Output begins at the H1. No "All inputs read…" / "Now creating the session note…" narration before or between sections.
- **No inline code or file paths in the 🎯 H2.** H2 line-height enlarges around `backtick spans` inconsistently, so file paths and identifiers in the focus statement render as visual jitter. Keep the H2 to plain prose; if a path needs to be named, drop it into the body of the briefing or the 📝 strip below.
- **🕐 Last session is hard one-line.** When the prior session has multiple threads, pick the headline outcome — don't pack three clauses joined by semicolons. Detail belongs in the carry-over table or the prior session note, not on this strip.

### 3.2 end-session

**Render order:**

1. **Title** (H1) — `# Closed Session NN`
2. **Outcome** (H3 anchor heading + table) —
   ```
   ### 🕐 Outcome
   | Topic | What was done |
   |---|---|
   | <topic> | <what was done> |
   ```
   Always rendered as a table when there are entries; even single-row sessions use the table form for visual uniformity.
3. **Deltas** (stat strip) — `📊 Deltas: <X> docs · <Y> models · <Z> ADRs · debt +<O>/−<C> · <G> glossary`
4. **Session note** (strip) — `` 📝 Session note: `session-NN.md` → completed ``
5. **Commit** (strip) — `` 📤 Commit: `<sha>` → `<branch>` `` (plus `· PR: [#NN](<url>)` when applicable)
6. **Pass-mode warning** (strip, conditional) — `⚠️ Pass-mode required next session — debt <N> / threshold <T>`
7. **Next steps** (H3 anchor heading + bullet list) —
   ```
   ### 🎯 Next steps
   - <step>
   - <step>
   ```

**Empty-state strips** (replace the corresponding section):

| Section | Empty render |
|---|---|
| Outcome (no doc deltas) | `🕐 Outcome: no doc deltas this session` |
| PR (no PR open) | drop the `· PR: …` suffix from the 📤 strip |
| Pass-mode warning (debt ≤ threshold) | omit the ⚠️ line entirely |

**Symmetry note:** the 🕐 Outcome content seeds next session's start-session `🕐 Last session:` line — same data, different label per POV (`Outcome:` here, `Last session:` there). End-session generates it; start-session consumes it.

**Heading-level rationale:** end-session uses H3 for both 🕐 Outcome and 🎯 Next steps. The H1 title carries the headline ("session closed"); supporting sections sit at H3. *Differs from start-session*, where 🎯 Suggested focus is H2 because the focus *is* the headline of the briefing.

### 3.3 promote-glossary

Three modes (add, update, rename), each with its own render shape. Title encodes the action and term, so no separate H2 anchor heading is needed.

**Mode A — Add term**

1. **Title** (H1) — `` # Glossary: added `<term>` ``
2. **Definition** (blockquote) — `> <one-line definition>`
3. **Section** (bold prose) — `**Section:** <glossary section name>`
4. **Session note** (strip) — `📝 Session note: Part 1 "Work done" updated`

**Mode B — Update term**

Same structure as Mode A, but the title reads `` # Glossary: updated `<term>` `` and the blockquote shows the new definition. Optionally, a `**Old:**` / `**New:**` pair if the change is a meaningful redefinition rather than a clarification.

**Mode C — Rename term**

1. **Title** (H1) — `` # Glossary: renamed `<old>` → `<new>` ``
2. **Section** (bold prose) — `**Section:** <glossary section name>`
3. **Reason** (bold prose, optional) — `**Reason:** <why renamed>`
4. **Cascade stat strip** — `📊 Cascade: <N> files affected` (plus `· ⚠️ pass-mode trigger (threshold <T>)` when N > threshold)
5. **Debt opened / Affected files** (H3 anchor heading + table) —
   ```
   ### 📋 Debt opened
   | Folder | Files |
   |---|---|
   | <folder> | <files> |
   ```
   Heading switches to `### 📊 Affected files` if user declined debt scaffolding.
6. **Session note** (strip) — `` 📝 Session note: Part 1 "Work done" updated `` (plus `· <N> debt entries opened` when scaffolded)

**Empty/edge-state strips:**

| Section | Empty / edge render |
|---|---|
| Cascade (0 files affected) | `📊 Cascade: 0 files affected — no follow-up needed`; omit the §5 file table |
| Debt scaffolding declined | switch §5 heading to `### 📊 Affected files`; add `⏳ Debt scaffolding declined` strip below the table |
| Pass-mode not triggered (N ≤ threshold) | drop `· ⚠️ pass-mode trigger (…)` from the stat strip |

**No `🎯 Next steps` section.** Promote-glossary is one-shot. The implicit next-step (resolve debt entries in a future session) is already conveyed by the ⚠️ warning when present.

**No `📚` emoji.** Glossary is excluded from the legend (§4 of `EMOJI-LEGEND.md`); the H1 title carries the "this is glossary work" signal.

### 3.4 new

Uses the §2.7 result-strip pattern.

- **Verb:** `Created`
- **Subject:** the relative target path
- **Metadata:** `(<type>, <status>)` for Model / ADR / spike / sample / plan; status omitted for plan (no front matter)
- **Session-note strip:** always rendered *except* for `plan` type (plans don't trigger session-note patches)

**Examples:**

```
**Created:** `Product/features/engine/models/04-evaluation-loop.md` (Model, draft)

📝 Session note: Part 1 "Work done" updated
```

```
**Created:** `Product/decisions/0009-async-snapshot-pipeline.md` (ADR, proposed)

📝 Session note: Part 1 "Work done" updated
```

```
**Created:** `PLANS/effect-flow-canonical.md`
```

**Failure / refusal cases** (path exists, feature folder missing, etc.) halt before the result strip is reached and surface the error inline; they don't render this template.

### 3.5 bootstrap

*Deferred — once-per-project skill, lower priority. Will be captured in a future iteration when the bootstrap shape is designed.*

### 3.7 lint

Uses the §2.4 stat strip plus a per-rule findings table. No H2 focus heading — the H1 title carries the action.

**Render order:**

1. **Title** (H1) — `# Lint — <scope>` (scope = "all rules" / "archive integrity" / "path checks" per flag passed)
2. **Summary stat strip** — `📊 <N> new findings · <M> already in ledger · <K> rules checked`
3. **Findings** (H3 anchor heading + table, omitted if zero findings) —
   ```
   ### ⚠️ Findings
   | Rule | Doc | Detail |
   |---|---|---|
   | A1 | features/engine/archive/01-old.md | superseded_by path missing |
   ```
4. **Debt opened** (strip, omitted if zero) — `📋 Debt opened: <N> new entries in ALIGNMENT-DEBT.md`
5. **Pass-mode warning** (strip, conditional) — `⚠️ Pass-mode required — debt <N> / threshold <T>`
6. **Clean strip** (replaces §3–§5 when zero findings) — `✅ Clean — no findings`

**Empty-state behavior:** when the linter finds nothing, the entire §3–§5 block collapses to a single `✅ Clean — no findings` strip. The H1 and summary stat strip still render.

**Archive-convention note.** Docs in `archive/` and the convention they carry (`superseded_by`, `archived_on`, `reason`) are documented in `PRODUCT-BUILDER.md §5.14`. The linter is the enforcement layer; this report template is the output layer. No `Supersedes:` field lives on active docs — the back-reference is synthesized by the linter from `archive/*.md → superseded_by` pointers.

### 3.6 design-pass

Rendered at the end of Phase 3 once all questions are locked. Not rendered mid-walk.

**Render order:**

1. **Title** (H1) — `# Design Pass — <topic>`
2. **Scope** (stat strip) — `📊 <N> questions settled · no doc edits`
3. **Locked decisions** (H3 anchor heading + bullet list) —
   ```
   ### 🔒 Locked decisions
   - Q1 = <option> — <implication>
   - Q2 = <option> — <implication>
   ...
   ```
4. **Patch cascade** (H3 anchor heading + bullet list) —
   ```
   ### 🎯 Patch cascade
   - <Model or ADR path> — <what changes>
   ```
   Omitted entirely when no downstream docs need patching.
5. **Session note** (strip) — `📝 Session note: Part 1 "Decisions settled" + "Active question going out" updated`
6. **Closing prompt** (italic) — `_Start a fresh session to apply these patches._`

**Empty-state strips:**

| Section | Empty render |
|---|---|
| Patch cascade (no downstream edits) | omit the section entirely |
| Session note (no in-progress session) | `⚠️ No in-progress session note — update manually` |

**No prose preamble.** Output begins at the H1 — same constraint as other skill reports.

---

## 4. Changelog

- **2026-04-30** — initial draft. Primitives and start-session template locked. 🕐 added as chat-only extension.
- **2026-04-30** — end-session template locked. 📤 added as chat-only extension; 🕐 scope clarified as the bridge line spanning end-session and start-session.
- **2026-04-30** — promote-glossary template locked (three modes: add / update / rename).
- **2026-04-30** — `new` template locked. Added §2.7 *Result strip* primitive for light skills with single-operation output; reused by `new`.
- **2026-04-30** — bootstrap deferred (§3.5 placeholder); will be captured in a future iteration.
- **2026-04-30** — start-session §3.1 rendering constraints added: no prose preamble, no inline code in the 🎯 H2, 🕐 Last session is hard one-line.
- **2026-04-30** — start-session resume mode: 🔄 added as chat-only extension; §3.1 grew a step 1a resume strip rendered when the skill detects a dangling pre-created skeleton (per `start-session.md` step 2).
- **2026-05-01** — design-pass template added (§3.6). 🔒 added as chat-only extension for locked design decisions.
- **2026-05-01** — lint template added (§3.7). Archive convention documented in §3.7 note (see `PRODUCT-BUILDER.md §5.14` for full convention).
