---
type: feature
status: draft
references: [<upstream features, or the literal `root`>]
tags: []
---

# <feature> — feature

## What it is
High-level prose framing the feature, with a canonical artifact (flow diagram, sequence sketch, ASCII pipeline, etc.) that gives a reader the shape in 30 seconds.

## Vocabulary
Feature-owned terms (table). Cross-feature terms — defined elsewhere — get a one-line pointer ("see triggers/", "see GLOSSARY.md") rather than restated definitions.

| Term | Meaning |
|---|---|
| `<term>` | <one-line definition> |

## Architecture
Cross-Model flow: how the Models in this feature fit together. Phase order, interaction sequence, what calls what. The load-bearing section that makes the feature legible without opening every Model.

## Composition
Models that make up this feature (table form, always present regardless of count).

| Model | Role |
|---|---|
| `models/01-<slug>.md` | <one-line role> |
| `models/02-<slug>.md` | <one-line role> |

## Invariants
Cross-Model rules — properties that must hold across the feature's Models. Named, so individual Models can reference them by name rather than restating. Model-local invariants live in the Models themselves.

- **<Invariant name>.** <one-line rule>

## Boundaries
What this feature owns — and what it explicitly doesn't. Demarcation against neighboring features.

**Owns:** <list>

**Does not own:**
- <thing> → <feature that does>

## Dependencies
- **Upstream:** features/X — consumed
- **Downstream:** features/Y — consumes this

## ❓ Open
Pointers to `ledgers/OPEN-ITEMS.md` items relevant to this feature.
