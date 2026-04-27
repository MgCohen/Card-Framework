# External Package Research

Deep-dive technical analyses of external packages being evaluated for use in the Card Framework. Each document covers source-level mechanics, alignment with framework design, what needs to be built on top, and open questions.

Use these alongside the framework design docs — not as standalone references.

## Entries

| File | Package | Date | Summary |
|---|---|---|---|
| [scaffold-states-deep-dive.md](scaffold-states-deep-dive.md) | `com.scaffold.states` | 2026-04-27 | Store, slices, mutators, staged-commit pipeline, snapshot. Alignment with StateMutation / ResolutionScope / GameState. |

## Convention for new entries

- One file per package.
- Name: `<package-short-name>-deep-dive.md`
- Include: package structure, key source read, mechanical summary, alignment table, what needs to be built on top, open questions, related framework docs.
- Update this README table when adding a new entry.
