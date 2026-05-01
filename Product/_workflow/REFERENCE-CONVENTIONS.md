---
type: model
status: accepted
last-updated: 2026-05-01
builds-on: [_workflow/PRODUCT-BUILDER.md]
tags: [#workflow, #meta]
---

# Reference Conventions

How docs under `Product/` reference each other. One rule, mechanically enforced by `/builder:lint`.

## The rule

Every cross-doc reference is a **`Product/`-rooted absolute path**, no leading slash, forward slashes only. This applies to:

- `builds-on:` front-matter entries
- Markdown link targets — `[text](path)`
- Bare path mentions in prose

Relative paths (`../`, `./`, `/`-prefixed) and URL-encoded paths (`%20`, `%28`) are not used.

**Why one rule.** Maintenance is the core principle. A single canonical form per target means:

- `grep -rn 'features/engine/models/01-evaluation-loop.md' Product/` finds every reference, anywhere, in one shot.
- Renames are a single global `sed` — no per-source-depth variants.
- Moving a source file does not silently break its outbound links.
- The linter does not need to resolve, decode, or normalize anything — just check whether the path exists.

The cost is that links are not clickable in standard markdown renderers (GitHub web, IDE preview) without a path-aware tool. For a workflow whose primary readers are AI agents and a path-aware linter, that trade is worth it.

## Format by use case

### `builds-on:` front-matter

Comma-separated list of `Product/`-rooted absolute paths in `[brackets]`. Use the literal `root` for top-level Models with no parent.

```yaml
---
type: model
status: settled
builds-on: [features/engine/models/01-evaluation-loop.md, features/actions/models/02-availability-snapshot.md]
---
```

```yaml
builds-on: root
```

```yaml
builds-on: [_workflow/PRODUCT-BUILDER.md]
```

### Markdown links between docs

Standard `[text](path)` with absolute path. Anchors append unencoded after `#`.

```markdown
See [evaluation loop §3](features/engine/models/01-evaluation-loop.md#section-3) for the drain logic.

The [trigger fold](features/triggers/models/01-trigger-fold.md) handles ordering.
```

### Bare path mentions in prose

Same absolute form, no link wrapper. Must resolve.

```markdown
The trigger fold is defined in features/triggers/models/01-trigger-fold.md §2.
```

### Illustrative or example paths (linter exempt)

Wrap in inline backticks `` `…` `` or place inside a fenced code block. The linter does not resolve these.

```markdown
A new model would live at `features/<feature>/models/<NN>-<slug>.md`.
```

````markdown
```bash
find Product/features/ -name 'archive' -type d
```
````

### Section/anchor references

Append `#section-<n>` for numbered sections, or `#<lowercase-kebab-heading>` for named sections, after the path. No URL encoding — if a heading would require encoding, rename the heading.

```markdown
[available actions §4](features/actions/models/02-availability-snapshot.md#section-4)
[zone model "Open Questions Left For Later"](features/zones/models/01-zone-model.md#open-questions-left-for-later)
```

### Ledger cross-references (IDs)

When pointing at an open-question, backlog, or alignment-debt entry, prefer **ID + path** together. The ID is for human cross-reference; the path is what the linter and grep follow.

```markdown
See oq-engine-builder (ledgers/OPEN-QUESTIONS.md) for the wiring gap.
Tracked as ad-20260501-027 (ledgers/ALIGNMENT-DEBT.md).
```

ID-only references are fine inside the same ledger (no path noise on every line). Cross-ledger references should include the path.

### External URLs

Untouched. Full `https://…` with whatever encoding the target site requires. The convention applies only to internal `Product/` references.

## Filename hygiene

Filenames under `Product/` must use:

- ASCII letters, digits, hyphens, underscores, and dots only.
- No spaces, parentheses, brackets, percents, or non-ASCII characters.
- Lowercase preferred. Numbered prefixes (`01-`, `02-`) are used for ordered Models and ADRs.

Rationale: removes the need for URL encoding anywhere, makes grep and sed predictable.

## Linter enforcement

`/builder:lint` enforces this convention through five rules (see `.claude/skills/builder/lint/SKILL.md` for the regex and finding shapes):

| Rule | What it checks | Source |
|---|---|---|
| S1 | Absolute path mentioned in a link or prose must exist on disk | `linter` |
| S2 | Every `builds-on:` entry resolves to an existing, non-archived file | `linter` |
| S3 | No relative paths (`../`, `./`, `/`-prefixed) in links or `builds-on:` | `linter` |
| S4 | No filenames with spaces, parens, percents, or brackets | `linter` |
| A1–A4 | Archive integrity — orthogonal to this convention | `linter/archive` |

S3 and S4 are pure regex / find checks — they do no path resolution and have no judgment calls. S1 and S2 reduce to existence checks because the convention removes every other variable.

## Non-goals

- **Clickable navigation in standard renderers.** Out of scope. If clickability becomes important, that is a renderer-side preprocessing step (e.g. for GitHub Pages), not a per-link convention burden.
- **Anchor verification.** The linter checks paths, not anchors. A broken `#section-3` against an existing file is not flagged.
- **External URL verification.** Out of scope. Use a separate link-checker if needed.
