# Coding standards

C# / Unity-specific conventions for code under `Assets/`. Add rules as they earn their place — one rule per surprising mistake, not speculation.

## Identifiers

**Prefer `int` or `Guid` over `string` for identity.** Identity, references, and bookkeeping keys default to integer or Guid types. Strings are reserved for human-readable names, debug output, or genuine last-resort fallback. Strings as IDs invite typos, allocation churn, and silent collisions.

## Open / extensible type sets

**Use `int` (or a `readonly struct` wrapper around `int`, TagKey-style) instead of `enum` for open or author-extensible type sets.** Entity types, kind codes, anything authors can add to → integer-backed, never a C# `enum`. C# `enum` is closed by definition; using it for extensible sets blocks downstream authoring and forces breaking changes when the set grows.

## Comments

**Comments warn; they don't narrate.** No file-, class-, or method-top header blocks that restate what the signature and body already say (`// Command surface over X`, `// Repository for entities`, `// Data only`, per-member narration, structural enumeration of a type hierarchy). A comment earns its place only as a *single line on a specific line* that warns of something the code itself does not show:

- an invariant a same-assembly caller could violate (e.g. `// Sole writer: apply-handlers only.` above an `internal` store mutator — `internal` says "same assembly," not "handler only");
- why an `unsafe` / pointer / `sizeof` block is sound;
- a deliberate no-op or a subtle ordering requirement.

If the code already states it (`if (_isServer) …` is self-evidently server-only), the comment is redundant — drop it. Architectural rationale and the "why" of a design live in the design docs (`docs/`, `Product/`), not in code. When in doubt, omit.
