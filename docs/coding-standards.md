# Coding standards

C# / Unity-specific conventions for code under `Assets/`. Add rules as they earn their place — one rule per surprising mistake, not speculation.

## Identifiers

**Prefer `int` or `Guid` over `string` for identity.** Identity, references, and bookkeeping keys default to integer or Guid types. Strings are reserved for human-readable names, debug output, or genuine last-resort fallback. Strings as IDs invite typos, allocation churn, and silent collisions.

## Open / extensible type sets

**Use `int` (or a `readonly struct` wrapper around `int`, TagKey-style) instead of `enum` for open or author-extensible type sets.** Entity types, kind codes, anything authors can add to → integer-backed, never a C# `enum`. C# `enum` is closed by definition; using it for extensible sets blocks downstream authoring and forces breaking changes when the set grows.
