---
type: feature
status: draft
references: [<upstream features, or the literal `root`>]
tags: []
---

# <feature> — feature

## Purpose
What this feature is and why it exists. One paragraph.

## Boundaries
What this feature owns — and what it explicitly doesn't. Demarcation against neighboring features.

## Composition
Models that make up this feature.
- features/<feature>/models/01-<slug> — short purpose
- features/<feature>/models/02-<slug> — short purpose

## Public surface
What a consumer of this feature sees. Contracts, services, types — the outer boundary that other features touch.

## Dependencies
- **Upstream:** features/X — consumed
- **Downstream:** features/Y — consumes this

## Settled decisions
Pointers to load-bearing ADRs in this feature's `decisions/`.

## ❓ Open
Pointers to `ledgers/OPEN-ITEMS.md` items relevant to this feature.
