---
type: feature
status: draft
references: [<upstream features, or the literal `root`>]
tags: []
---

# <feature> — feature

## What it is
High-level prose framing the feature in 30 seconds. SHOULD include a canonical artifact — a small visual or structural rendering that lets a reader grasp the shape without opening Models. Pick the form that fits:

| Feature shape | Artifact form |
|---|---|
| Sequential pipeline / phase order | Flow diagram or arrow chain |
| Component composition / orchestration | ASCII layout or component sketch |
| State machine / lifecycle | State diagram |

If no form fits cleanly, the stub `*n/a — descriptive only.*` is permitted.

## Vocabulary
**Feature-owned terms only.** Terms defined in another feature, in `ledgers/GLOSSARY.md`, or in upstream prior art do **not** belong here — link to their canonical home with a one-line pointer ("see `features/triggers/`", "see `GLOSSARY.md > Trigger`") instead of restating.

| Term | Meaning |
|---|---|
| `<term>` | <one-line definition> |

## Architecture
Cross-Model flow: how the Models in this feature fit together — phase order, interaction sequence, what calls what.

Size to the Model count:

| Model count | Architecture depth |
|---|---|
| **1** | Thin — often a single sentence pointing at the Model; the Model itself carries the structure. Stub `*n/a — see model 01.*` permitted. |
| **2–3** | Medium — a paragraph or short diagram showing how the Models compose. The load-bearing section that makes the feature legible without opening every Model. |
| **4+** | Deep — sub-headers per phase or sub-system; sequence diagram or component map. The reader needs the architecture to navigate. |

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

## Open
Pointers to `ledgers/OPEN-ITEMS.md` items relevant to this feature.
