---
type: plan
status: drafting
tags: [#workflow, #simplification]
---

# Simplify doc workflow — cleanup sketch

Working doc for the workflow simplification pass. Captures locked decisions, pending feedback, and the rough edit set. Updated as the conversation continues.

## Diagnosis (recap)

The artifacts (Models, Features, ADRs, ledgers, glossary, sessions) earn their keep. What's burning is the **forcing-function machinery** that turns those artifacts into mandatory next-session shapes:

- `§5.7 step 1b` retired-token grep auto-opens cascade entries; many are false positives.
- `§5.8` threshold trips on its own commit, so a single rich design session forces the next session into pass-mode.
- Session-note "active question coming in" gets copied verbatim across automated passes, lying about continuity.
- Per-edit ceremony stacks (Model edit + ADR + glossary + ledger + session-note assertion) for even small decisions.
- The workflow doc itself has churned ~10 times in the last 50 commits, eroding the stable mental model.

**Core insight:** the docs are now well-structured enough that the *references themselves* are the index. ALIGNMENT-DEBT was a workaround for an earlier era when docs weren't indexed. With proper structure, cascade is *detected on demand*, not *tracked*.

Companion line: if you noticed it's broken enough to write a ledger entry, you've already done the expensive part — describing what needs to change. Just change it.

---

## Locked decisions (do these)

### 1. One reference field — `references:`

Replace `builds-on:` with a single `references:` field. Drop the `depends-on` / `informed-by` split — direction matters in the graph; flavor doesn't change behavior. If a typed split ever proves load-bearing later, it's an additive change.

```yaml
references: [features/entities/01, features/cards/01]
```

### 2. Drop `last-updated:` from front matter

Git is the source of truth. Front-matter `last-updated:` always drifts. The linter computes it from `git log -1 --format=%cs <path>` when needed. If we want it visible in-doc for eyeballing, the linter mirrors it back in — but the authoritative answer comes from git, always.

### 3. ADRs become project-wide-only

Reserve ADRs for project-wide / cross-cutting decisions (Objects-over-IDs is the model citizen). Feature-internal decisions collapse into the relevant Model's provenance section (`## Options considered`, `## Rationale`, `## Consequences`).

- **Kills:** `features/<feature>/decisions/` directories.
- **Survives:** top-level `decisions/` for cross-cutting ADRs only (~5–10 total project-lifetime).
- **Tradeoff:** Model provenance sections grow to absorb prior feature-ADR content. One file with 4 decisions in narrative order beats 4 ADR files plus the Model.

**Open sub-task:** triage every existing ADR. Each one is either:
  - **Cross-cutting** — stays as project-wide ADR (move to `Product/decisions/` if not already there).
  - **Feature-internal** — content folds into the related Model's provenance section; ADR file archived with a forward link.
  - **Already obsolete** — archived without migration.

### 4. Strict Model template structure

Canonical section order, linter-enforced. Every Model has, in this order:

```
## Problem
## Concepts
## Contracts
## Invariants
## Examples
## Open follow-ups
---
## Options considered
## Rationale
## Consequences
## ADR  (optional — present only if a project-wide ADR was opened)
```

Sections may be empty with a stub (`*n/a — purely descriptive Model.*`) but cannot be reordered or omitted. Linter validates presence + order. Variable *size* is fine; variable *structure* is not.

### 5. Retire ALIGNMENT-DEBT + debt-pass + threshold

Cascade is detected on demand by the linter, not tracked.

- Retire `Product/ledgers/ALIGNMENT-DEBT.md` (kept as archive, no new entries).
- Retire `/builder:debt-pass` skill.
- Retire `.github/workflows/debt-pass.yml` (already disabled in PR #82; this step deletes the file).
- Retire `PLANS/auto-debt-pass.md` (mark `status: abandoned`).
- Drop §5.7, §5.8, §5.9 (cascade walking on supersession) from PRODUCT-BUILDER.md.
- Replace with a one-paragraph rule: *"Cascade is detected, not tracked. Run the linter or `/builder:deps`. Fix in place. If a cascade needs design judgment rather than a mechanical edit, open an OPEN-QUESTIONS entry."*

The OPEN-QUESTIONS ledger absorbs the one edge case (cascade-needs-design vs cascade-is-mechanical) — no separate machinery.

---

## Linter responsibilities (consolidated)

`/builder:lint` becomes the single on-demand consistency tool. Responsibilities:

1. **Structure check** — every Model has the canonical sections in the canonical order (per #4 above).
2. **References resolve** — every `references:` path exists; every inline path-shaped link in the body exists.
3. **Header–body consistency** — auto-populate `references:` from path-shaped tokens in the body; flag mismatches.
4. **Retired-tokens check** — grep for tokens in `Product/_workflow/retired-tokens.json`; report hits with their replacement.
5. **Reverse graph** — compute "who references this" from the union of all `references:` fields. Expose as `/builder:deps <path>`.
6. **Git-derived dates** — `last-updated` per doc on demand from `git log`.

Step zero before adding new responsibilities: **harden the linter** (track `bl-debt-pass-gha-silent-failure` lessons — make sure it always produces signal, never fails silently).

---

## Edit set (rough scope)

One PR per locked decision is too granular; one PR for everything is too risky. Suggested grouping:

**PR 1 — front matter consolidation**
- `references:` replaces `builds-on:` repo-wide (mechanical rename + schema doc update).
- Drop `last-updated:` from front matter repo-wide.
- Update §3 of `PRODUCT-BUILDER.md` to match.
- Update all 7 templates in `_workflow/_templates/`.

**PR 2 — retire ALIGNMENT-DEBT machinery**
- Delete `.github/workflows/debt-pass.yml`.
- Mark `PLANS/auto-debt-pass.md` `status: abandoned`.
- Remove `/builder:debt-pass` skill.
- Archive `Product/ledgers/ALIGNMENT-DEBT.md` (move to `Product/ledgers/archive/` or similar; preserve content).
- Drop §5.7, §5.8, §5.9 from `PRODUCT-BUILDER.md`; add the one-paragraph "detected, not tracked" rule.
- Drop `debt: N opened` assertion from session-note template.

**PR 3 — ADR consolidation**
- Triage every ADR (project-wide vs feature-internal vs obsolete).
- Migrate feature-internal ADR content into Model provenance sections.
- Archive migrated ADR files with forward links to the absorbing Model section.
- Move surviving cross-cutting ADRs to `Product/decisions/` (if not already there).
- Delete empty `features/<feature>/decisions/` directories.
- Update `PRODUCT-BUILDER.md` §4.5 + §5 to reflect new ADR scope.

**PR 4 — strict template + linter hardening**
- Update Model template (§4.3) with canonical section order; mark required sections.
- Extend linter with structure check, header-body consistency, reverse graph, git-derived dates.
- Harden linter against silent failures.
- Add `/builder:deps <path>` command.

Order: PR 1 → PR 2 → PR 4 → PR 3 (do ADR migration last so the linter and template are in place to validate the migrated Models).

Or collapse to one PR if we trust the diff. TBD when sketching.

---

## Pending feedback (more to come)

User flagged additional thoughts coming. Add them here as they land before we move from sketch → execution.

- *(reserved)*

---

## Out of scope for this pass

- Drift detection (post-merge code-vs-contract automation, §5.12). Orthogonal; not affected by these changes.
- Parallel-sessions plan (`PLANS/parallel-sessions.md`). Stays as-is — its merge model survives this simplification, just with fewer things to merge.
- Bootstrapping ceremony (§5.0). Not affected.
- Status state machine (§5.13). Not affected.
