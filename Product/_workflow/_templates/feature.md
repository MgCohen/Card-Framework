# <feature> — feature

<front matter: type=feature, status=draft>

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
Pointers to `ledgers/OPEN-QUESTIONS.md` items relevant to this feature.
