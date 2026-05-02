---
type: model
status: accepted
references: [_workflow/PRODUCT-BUILDER.md]
tags: [#workflow, #meta]
---

# Emoji Legend

**Purpose.** Single source of truth for emoji used as section anchors across workflow chat reports (e.g., start-session, end-session) and committed artifacts (session notes, doc-type templates). Strict 1:1 mapping between emoji and workflow concept — every grep for an emoji returns exactly one concept.

**Scope.** Both ephemeral chat output and committed `.md` artifacts. Skills and templates reference this doc as the canonical legend; do not redefine emojis elsewhere.

---

## 1. AI-first design

This legend is sized and shaped for AI consumption, not human working memory. Two consequences shape every rule below:

- **Legend size is cheap.** The legend is loaded into context when needed; there is no working-memory ceiling. 9 entries is fine. 15 would be fine. The cost scales linearly with context tokens, which is small.
- **Ambiguity is expensive.** Every overlap or fuzzy meaning forces decision-making at write time. A precise legend with strict 1:1 mapping is *cheaper* than a smaller legend with overloaded meanings.

The design rule is **precision over parsimony**. Add slots for any concept that recurs and would be grepped separately. Do not add slots for fuzzy or one-off concepts.

---

## 2. The legend

| Emoji | Concept | Scope | Notes |
|---|---|---|---|
| 🎯 | active question | both | singular per session/report |
| ❓ | open-questions entry | both | the `OPEN-ITEMS` ledger concept |
| ⚠️ | workflow gate fired | both | threshold crossed, validation gate triggered |
| ✅ | locked | both | doc/section sealed against further changes |
| 🛑 | blocker | both | work halted, awaiting decision/intervention |
| ⏳ | deferred / awaiting | both | parked for later, or awaiting external action (e.g., automated discharge) |
| 📝 | session note reference | both | path or mention of `sessions/<session-id>.md` |
| 📊 | stat line / counts | both | numeric summary (`12/5`, `6 entries`) |
| 🕐 | session outcome / carry-over | chat only | the bridge line between sessions (start-session 🕐 Last session, end-session 🕐 Outcome) |
| 📤 | commit/push action | chat only | end-session commit/push report line |
| 🔄 | resume signal | chat only | re-entering a pre-created skeleton that was never closed (start-session) |
| 🔒 | locked design decision | chat only | settled verdict from a design-pass walk |
| 🧹 | cleanup action | chat only | start-session orphan-skeleton sweep surfacing |
| 🔀 | branch / parallel-session info | chat only | start-session and end-session branch-mode strip |
| 📦 | backlog reference | chat only | start-session backlog table heading |
| 🔍 | artifact / sketch review | chat only | end-session unsaved-body review table (interfaces, protocols, sketches discussed in conversation) |

**Scope:**
- **Both** — the emoji appears in committed `.md` artifacts (session notes, doc-type templates, etc.) AND in chat reports. Strict 1:1 across both.
- **Chat only** — the emoji appears only in chat reports rendered by skills. Never written into committed files. Same 1:1 rule applies — no overlap with any "both" entry.

All entries are single-codepoint or codepoint+VS16; token cost is ~1–2 tokens per use.

---

## 3. Rules of use

1. **Section headers and stat lines only.** Never inline in prose. Never on individual list items.
2. **One emoji per heading.** No stacking (`## 🎯⚠️ …` is forbidden).
3. **Strict 1:1.** An emoji means *exactly one* concept. If a new concept is close but distinct, it gets its own slot or it goes to bold prose — never a second meaning on an existing emoji.
4. **Bold prose for everything else.** Concepts not in the legend use bold-keyword anchors (`**blocker:**`, `**ADR-0003**`, `**GLOSSARY**`). These grep cleanly too, just in a different vocabulary.

---

## 4. Explicitly NOT in legend

Documented to make drift visible. If you find yourself reaching for one of these, use bold prose instead — or propose a new slot via §5.

- 🎬 / 🚀 / 🔥 / 🎉 — decoration, no semantic role.
- 📚 glossary — bold prose (`**GLOSSARY**`).
- 📐 ADR / decision record — bold prose (`**ADR-0003**`).
- 🧭 scope / scaffold boundary — bold prose.
- ☑️ / ✔️ — visual collision with ✅.
- 👉 / ✋ — italic `_Confirm or pivot._` line carries the prompt without an emoji.
- Multi-codepoint / ZWJ emoji (👨‍💻, 🏳️‍🌈) — tokenize unpredictably (5+ tokens), rendering varies.

---

## 5. How to expand

A new concept earns a legend slot only if it passes **all three** tests:

1. **Recurrence** — appears in ≥3 different skill outputs or artifact types.
2. **Grep separation** — would be grepped *separately* from any existing legend entry.
3. **Stable noun** — a stable named concept in the workflow vocabulary, not a passing notion.

If any test fails, use bold prose anchors instead.

**To propose an addition:**
1. Identify the concept and cite ≥3 occurrences across skills/artifacts.
2. Pick a candidate emoji — single-codepoint preferred, no visual collisions with existing entries (avoid look-alikes of 📝, 📋, ✅).
3. Update this doc: add the row to §2, update §6 changelog, add to §4 if any adjacent emoji should be explicitly excluded.
4. Update consuming skills/templates to use the new symbol.

**To retire a slot:**
1. If a concept stops recurring or its grep value drops to near-zero, retire it.
2. Move the symbol to §4 with a brief note explaining the retirement.
3. Update consuming skills/templates to drop usage.
4. Record the retirement in §6.

---

## 6. Changelog

- **2026-04-30** — initial legend (9 entries: 🎯 📋 ❓ ⚠️ ✅ 🛑 ⏳ 📝 📊).
- **2026-05-01** — absorbed chat-only extensions from retired `REPORT-STYLE.md`. Added 🕐 📤 🔄 🔒 🧹 🔀 📦 with a `Scope: chat only` tag. 🔒 moved from §4 (excluded) to §2 — design-pass legitimized its use. ⏳ broadened from "deferred" to "deferred / awaiting" to cover the awaiting-automated-discharge case.
- **2026-05-01** — added 🔍 (chat only) for end-session unsaved-body review table. Pre-existing in `end-session.md` step 2a but undocumented; absorbed during the post-retirement review.
