---
type: model
status: accepted
last-updated: 2026-04-30
builds-on: [PRODUCT-BUILDER.md]
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

| Emoji | Concept | Notes |
|---|---|---|
| 🎯 | active question | singular per session/report |
| 📋 | alignment-debt entry/list | the `ALIGNMENT-DEBT` ledger concept |
| ❓ | open-questions entry | the `OPEN-QUESTIONS` ledger concept |
| ⚠️ | workflow gate fired | pass-mode required, threshold crossed, validation gate triggered |
| ✅ | locked | doc/section sealed against further changes |
| 🛑 | blocker | work halted, awaiting decision/intervention |
| ⏳ | deferred | parked for later — explicitly *not* doing now |
| 📝 | session note reference | path or mention of `sessions/session-NN.md` |
| 📊 | stat line / counts | numeric summary (`12/5`, `6 entries`) |

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
- 🔒 — visually conflicts with ✅ (locked); deliberately unused to avoid drift.
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
