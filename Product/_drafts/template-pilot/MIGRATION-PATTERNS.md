---
type: note
status: draft
references: [_workflow/PRODUCT-BUILDER.md, MIGRATION-MAP.md]
tags: [#migration, #patterns, #standards-candidate]
---

# Migration patterns — observed conventions

Captured in flight while migrating existing docs to the new Feature/Model template. Each entry is a candidate for codification as standard. Format: pattern → observed in → questions/tradeoffs.

---

## P1 — Below-divider provenance, three modes

When the elevation/fold decision is final, the Model's below-divider sections take one of three shapes:

| ADR situation | Below-divider shape |
|---|---|
| **No ADR** (clean slate) | Empty stubs **or** brief Q1–QN restructure if the Model body itself contains decision content (e.g. entities/01's nested "Provenance" mega-section). |
| **Elevated ADR** (project-wide) | Stubbed pointers like `*See [decisions/00NN-...](decisions/00NN-...) §Options considered.*` — full content lives in the ADR. **No `## Decisions` section above the divider** when the ADR is elevated and the Model is otherwise descriptive. |
| **Folded ADR** (single-feature) | Full Q1–QN content folded into `## Options considered`; provenance fully in the Model. **No `## Decisions` section above the divider.** |

**Observed in:** cards (folded), entities/01 (no ADR but has historical provenance), entities/02 (no ADR; runtime-surface decisions captured fresh).

**Open question:** when a Model has both elevated AND folded ADRs (e.g. zones will have 2 folded + 1 elevated), do we merge all provenance below the divider, or split (folded inline, elevated as pointer)?

---

## P2 — `## Decisions` section is for *project-wide* ADR pointers

Per spec §4.3, `## Decisions` is optional. We use it **only** when one or more *project-wide* (elevated) ADRs apply. Folded decisions don't get a `## Decisions` section — their content lives below the divider.

**Observed in:** cards Model would have used it if elevation had stuck (it didn't). Entities Models have no `## Decisions`.

**Reasoning:** folded ADRs ARE the below-divider provenance. Pointing at them from above the divider would be redundant.

---

## P3 — Cross-Model invariants in Feature, Model-local in Model, with explicit pointer

Feature `## Invariants` lists feature-level rules that span Models or shape downstream consumers. Model `## Invariants` lists Model-local rules with a one-line pointer up:

> `(See `entities.md ## Invariants` for feature-level rules — Store-canonical, definition immutability on handle, modifier source as InstanceId, deterministic ModifierId, eager cache rebuild.)`

**Observed in:** cards/01, entities/01, entities/02. The pointer line keeps the split discoverable without duplicating content.

---

## P4 — Path convention: Product-root-relative

All markdown link URLs and front-matter `references:` entries use Product-root-relative paths (`features/zones/models/01-zone-model.md`, not `../../zones/models/01-zone-model.md`). This works with the linter convention even though raw markdown renderers won't resolve them.

**Observed in:** universal so far.

---

## P5 — `references:` field convention

- Models: mandatory; either upstream paths or literal `root` for foundational Models.
- Feature roots: include upstream feature paths when the feature has dependencies; use `root` when foundational (entities).

**Observed in:** cards.md (lists upstream entities + zones), entities.md (`root`), entities/01 (`root`), entities/02 (lists Model 01).

**Spec note:** §3 says "Feature roots derive their `Dependencies` section from the union of contained Models' `references:`, so they don't need to repeat it in front matter." Loose reading — we are repeating. Tighter reading would drop the field on Feature roots. Leaving as-is for now; flag for review.

---

## P6 — Canonical artifact in `## What it is` is essential, not optional

Every reshaped Model and Feature root opens with prose + a canonical artifact (code snippet, ASCII diagram, mermaid, or pseudocode). Empty-prose openings consistently feel weak.

**Observed in:** cards.md (ASCII diagram), entities.md (ASCII flow), cards/01 (`Card = StateEntity + IReference + accessors` quote), entities/01 (mermaid + working-model quote), entities/02 (interface signature).

**Standard candidate:** "Every Feature root and every Model SHOULD have a canonical artifact in `## What it is`. The form depends on the shape (diagram for flow, snippet for class, pseudocode for algorithm, quote for conceptual)."

---

## P7 — `## Examples` for Models with no obvious worked example

Spec allows `*n/a — see Contracts.*` stub. Used when contracts are self-explanatory and a worked example would be ceremony. Models with rich behavior (entities/02) get real worked examples.

**Observed in:** cards/01 has worked examples (small but real), entities/01 has a small computation example, entities/02 has full equip/aura examples.

**Pattern:** if you can write a 3-line example that actually adds info, do it. If the only example would be `// see Contracts §X`, use the stub.

---

## P8 — Pre-existing "Useful Invariants" → `## Invariants` (with cross-Model lift)

Several existing Models have a "Useful Invariants" section already. Migration:
1. Lift the cross-Model / feature-level ones to Feature `## Invariants`.
2. Keep Model-local ones in Model `## Invariants`.
3. Add the cross-reference pointer (P3).

**Observed in:** cards/01 (lifted 4 to Feature, kept 1 local), entities/01 (lifted 5 to Feature, kept 6 local), zones/01 will be similar.

---

## P9 — Section ordering: spec is canonical

Feature: `What it is | Vocabulary | Architecture | Composition | Invariants | Boundaries | Dependencies | Open`
Model: `What it is | Contracts | Invariants | Decisions? | Examples | Open follow-ups | --- | Options considered | Rationale | Consequences`

**Observed:** universal so far. The linter will enforce; humans should not improvise.

---

## P10 — `## Open` (Feature) vs `## Open follow-ups` (Model)

Spec uses different names. Feature uses `## Open` (no emoji); Model uses `## Open follow-ups`. The regenerated `_workflow/_templates/feature.md` still has `## ❓ Open` (emoji from old template) — minor inconsistency to fix.

**Followup:** drop emoji from `_templates/feature.md` to match spec text.

---

## P11 — Empty `features/<feature>/decisions/` removed during migration

Per spec §4.5, no `features/<feature>/decisions/` directory exists post-migration. Once empty (all ADRs folded or elevated), the directory + any `.gitkeep` is removed.

**Observed in:** cards (deleted), entities (deleted .gitkeep).

**Standard candidate:** "Migration of a feature ends with `features/<feature>/decisions/` removed."

---

## P12 — Front-matter date stays as historical record when folding ADRs

When folding an ADR's content into a Model's below-divider provenance, preserve the original ADR's date in a one-line note:

> `*Originally landed as `features/cards/decisions/0001-card-shape.md` (2026-05-01); folded into Model provenance per §4.5 on 2026-05-07 (single-feature scope...).*`

**Observed in:** cards/01 (folded ADR-0001).

---

## P13 — `## Boundaries` reordered to position 6 (was 2)

Per spec §4.2, Boundaries moved from second-position to sixth-position in the Feature template. Migration reorders. (Spec rationale: "orientation first, then specifics, then the demarcation against neighbors.")

**Observed in:** cards.md, entities.md.

---

## Standards-candidate review queue (revisit after all migrations done)

- [ ] P1 — codify three-mode below-divider convention into spec
- [ ] P2 — clarify in spec that `## Decisions` is for elevated ADR pointers only
- [ ] P3 — codify cross-Model invariant split + pointer pattern in spec
- [ ] P5 — clarify Feature root `references:` field convention (drop or keep?)
- [ ] P6 — make "canonical artifact in What it is" a SHOULD-have in spec
- [ ] P10 — drop emoji from `_templates/feature.md`
- [ ] P11 — codify decisions-directory removal as part of migration

(Patterns added as discovered during zones / triggers / effects / engine / actions / presentation migrations.)
