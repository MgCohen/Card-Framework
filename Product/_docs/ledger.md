---
type: doc-rules
target: ledger
status: accepted
references: [_workflow/PRODUCT-BUILDER.md]
tags: [#workflow, #rules, #ledger]
---

# Ledger — rulebook

Registry / index files maintained as event-driven indices. Ledgers don't carry original content — they index things that exist elsewhere. Examples: `FEATURE-MAP.md`, `OPEN-ITEMS.md`, `GLOSSARY.md`.

A **glossary** is a kind of ledger (cross-feature term definitions, indexed centrally). Same rules apply.

## Non-negotiable gates

1. **Indexes existing content.** Doesn't carry original content. Each entry points at content owned by another doc.
2. **Updated event-driven.** Model lands, feature ships, dependency shifts, item resolves — the ledger updates. Not iterated continuously.
3. **Single, named purpose.** Feature index, open-items log, term glossary — one concern per ledger.

## Decision flow

1. **Indexes existing content vs. carries new content?** Indexes → ledger. New content → some other doc type.
2. **Used by multiple features (for terms)?** Yes → glossary entry. No → feature vocabulary table.

## Conventions (once classified as a ledger)

- Lives under `Product/ledgers/` with a descriptive ALL-CAPS filename (`FEATURE-MAP.md`, `OPEN-ITEMS.md`, `GLOSSARY.md`).
- Frontmatter: `type: ledger` (or `feature-map`, `glossary` for specific kinds).
- Hand-maintained at v1; auto-generated at v2 (TBD trigger ~15 models).
- Cross-references use `Product/`-rooted absolute paths.

## Per-ledger conventions

### `GLOSSARY.md`

- Each entry: `| term | one-line definition + canonical-home pointer |`.
- Cross-references use stable section names, not numeric anchors (e.g. `§Lifecycle states`, not `§4.1`).
- One-line definition + canonical-home pointer — no restated prose.

### `OPEN-ITEMS.md`

- Resolution = delete the entry. The resolving doc owns the answer; the ledger entry is removed.

### `FEATURE-MAP.md`

- Lists features and their model counts; updated when a feature lands or models change.

## Heuristics

- **If unsure whether to add to a ledger or write a new doc**, the test is "does this point AT existing content?" If yes → ledger. If no → full doc.
- **A term feels feature-internal but might grow cross-feature later** — leave it in feature vocabulary; promote to glossary when a second feature actually uses it.

## Audit obligation

When a gate or rule changes, every existing ledger is re-checked against the new wording.
