---
type: note
status: draft
references: [_workflow/PRODUCT-BUILDER.md, MIGRATION-MAP.md]
tags: [#migration, #patterns, #standards-candidate]
---

# Migration patterns — observed conventions

Captured in flight while migrating existing docs to the new Feature/Model template. Each entry is a candidate for codification as standard. Format: pattern → observed in → questions/tradeoffs.

**Migration complete (2026-05-07): all 8 features.** Patterns below codify what worked. Standards-candidate review queue at the bottom.

---

## P1 — Below-divider provenance, four modes (refined)

When a Model's ADR situation is final, the below-divider sections take one of four shapes:

| ADR situation | Below-divider shape | Observed in |
|---|---|---|
| **No ADR, no historical provenance** | Empty stubs (`*n/a — purely descriptive Model.*` for each section). | `effects/03-ongoing-effects` (no design alternatives weighed at this Model's scope) |
| **No ADR, historical provenance present** | Brief Q1–QN restructure of the existing inline provenance into `## Options considered`. | `entities/01` (Q1–Q6 from "Core Model Decisions" mega-section), `entities/02` (Q1–Q5 runtime-surface design decisions), `engine/05-game-factory` (Q1 + Q1a–Q1g sub-decisions), `engine/06-match-lifecycle` (Q1–Q6) |
| **Folded ADR(s) only** | Full Q1–QN content folded into `## Options considered`; provenance fully in the Model. **No `## Decisions` section above the divider.** | `cards/01` (Q1–Q4 from cards/0001), `triggers/01` (Q1–Q2), `effects/01-04` (Q1 each fold), `engine/01-04` (Q1 each fold), `actions/01` (Q1–Q5 from 5 ADRs), `actions/02` (Q1–Q2 from 2 ADRs) |
| **Folded ADR(s) + elevated ADR** | Folded ADRs go below divider as Q1–QN; elevated ADR gets a one-line pointer in `## Decisions` above the divider; below-divider includes a sub-Q with brief framing pointing at the elevated ADR for full options. | `zones/01` (Q1–Q8 mix: Q1–Q6 from original Zone Model decisions; Q7 folded from zones/0001; Q8 folded from zones/0003; Q4 visibility points at elevated `decisions/0006`) |
| **Special — supersede-then-fold** | The folded ADR's content is preserved as Q1 with the chosen-then-rejected status made explicit; original date + reversal date both recorded. | `effects/04-effect-flow` (Q1 captures supersession of effects/0002 — original DecisionId model accepted 2026-04-29, superseded 2026-05-07 by type-only routing). See P14. |

**Rule:** the below-divider provenance is always `## Options considered → ## Rationale → ## Consequences`, even when content is sparse. Empty stubs are fine when there's nothing to say.

---

## P2 — `## Decisions` section is for *project-wide* (elevated) ADR pointers

Per spec §4.3, `## Decisions` is optional. We use it **only** when one or more *project-wide* (elevated) ADRs apply. Folded decisions don't get a `## Decisions` section — their content lives below the divider.

**Observed:** Only `cards/01` (initially had it, later removed when ADR was reverted to fold), `actions/01` (points to elevated 0007), `zones/01` (points to elevated 0006). Models with only folded ADRs: no `## Decisions`.

**Reasoning:** folded ADRs ARE the below-divider provenance. Pointing at them from above the divider would be redundant.

---

## P3 — Cross-Model invariants in Feature, Model-local in Model, with explicit pointer

Feature `## Invariants` lists feature-level rules that span Models or shape downstream consumers. Model `## Invariants` lists Model-local rules with a one-line pointer up:

> `(See `entities.md ## Invariants` for feature-level rules — Store-canonical, definition immutability on handle, modifier source as InstanceId, deterministic ModifierId, eager cache rebuild.)`

**Observed in:** universal across all 8 migrated features. Average split: 5–10 cross-Model invariants on Feature root; 3–8 Model-local on each Model.

---

## P4 — Path convention: Product-root-relative

All markdown link URLs and front-matter `references:` entries use Product-root-relative paths (`features/zones/models/01-zone-model.md`, not `../../zones/models/01-zone-model.md`). This works with the linter convention even though raw markdown renderers won't resolve them.

**Observed in:** universal across all 8 features. **Codify in spec.**

---

## P5 — `references:` field convention (refined)

**Settled:**
- **Models**: mandatory; either upstream paths or literal `root` for foundational Models.
- **Feature roots**: include upstream Feature/Model paths when the feature has dependencies; use `root` when foundational (entities only).

**Observed in:** all 8 features. Spec §3 says Feature roots "don't need to repeat" — we ignored this and included references explicitly. Worked well; auto-derivation from contained Models would lose the explicit dep statement at the Feature level.

**Recommendation:** codify "Feature roots SHOULD include upstream paths in `references:`" in the spec.

---

## P6 — Canonical artifact in `## What it is` is essential, not optional

Every Feature root and Model opens with prose + a canonical artifact (code snippet, ASCII diagram, mermaid, or pseudocode). The form depends on the shape:

| Doc type | Artifact form | Example |
|---|---|---|
| Feature root | ASCII flow diagram or sequence sketch | `cards.md`, `engine.md`, `effects.md` |
| Class-shaped Model | C# class signature snippet | `cards/01-card-model`, `entities/02-runtime-surface` |
| Algorithm-shaped Model | Pseudocode block | `engine/01-evaluation-loop`, `triggers/01-trigger-fold` |
| Conceptual Model | Mermaid graph or working-model quote | `entities/01-definition-instance-model` (mermaid) |
| Pipeline-shaped Model | Sequence diagram | `actions/01-command-pipeline`, `presentation/01-presentation-loop` |

**Observed in:** universal across all 18 Models and 8 Feature roots. **Codify "every Feature root and Model SHOULD have a canonical artifact in `## What it is`" in spec §4.2 / §4.3.**

---

## P7 — `## Examples` posture (refined)

Spec allows `*n/a — see Contracts.*` stub. Used when contracts are self-explanatory and a worked example would be ceremony. Models with rich behavior get worked examples (often multi-step traces).

**Observed:**
- `effects/03` uses stubs (purely descriptive Model — no specific examples beyond what's shown in Contracts).
- `presentation/01` uses a 13-step end-to-end trace.
- `cards/01`, `entities/02`, `actions/01`, `engine/01`, `engine/06` use worked code examples.
- `triggers/01` mixes inline triggers + traced fold.
- `zones/01` uses a table of zone examples + small code samples.

**Pattern:** if you can write a real example that adds info beyond what `## Contracts` already shows, do it. If the only example would echo `## Contracts`, use the stub. **`## Examples` should never duplicate Contracts.**

---

## P8 — Pre-existing "Useful Invariants" → `## Invariants` (with cross-Model lift)

Several existing Models had a "Useful Invariants" section already. Migration:
1. Lift the cross-Model / feature-level ones to Feature `## Invariants`.
2. Keep Model-local ones in Model `## Invariants`.
3. Add the cross-reference pointer (P3).

**Observed in:** cards/01 (lifted 4 to Feature, kept 1 local), entities/01 (lifted 5, kept 6), entities/02 (no pre-existing section; extracted 6 model-local invariants from scattered prose), zones/01 (lifted 7, kept 4), engine/04 (lifted 5, kept 7).

---

## P9 — Section ordering: spec is canonical

Feature: `What it is | Vocabulary | Architecture | Composition | Invariants | Boundaries | Dependencies | Open` (8 sections)
Model: `What it is | Contracts | Invariants | Decisions? | Examples | Open follow-ups | --- | Options considered | Rationale | Consequences` (5 required + 1 optional + 3 below-divider)

**Observed:** universal across all 26 migrated docs. The audit pass (final commit) verified zero exceptions.

---

## P10 — `## Open` (Feature) vs `## Open follow-ups` (Model)

Spec uses different names. Feature uses `## Open` (no emoji); Model uses `## Open follow-ups`. **Resolved during audit:** the regenerated `_workflow/_templates/feature.md` had `## ❓ Open` (emoji from old template); fixed to match spec.

---

## P11 — Empty `features/<feature>/decisions/` removed during migration

Per spec §4.5, no `features/<feature>/decisions/` directory exists post-migration. Once empty (all ADRs folded or elevated), the directory + any `.gitkeep` is removed.

**Observed in:** all 8 features. Universal. **Codify "Migration of a feature ends with `features/<feature>/decisions/` removed" in spec §4.5.**

---

## P12 — Front-matter date stays as historical record when folding/elevating ADRs

When folding an ADR's content into a Model's below-divider provenance (or elevating to project-wide), preserve the original ADR's date in a one-line note:

> Folded: `*Originally landed as `features/cards/decisions/0001-card-shape.md` (2026-05-01); folded into Model provenance per §4.5 on 2026-05-07 (single-feature scope...).*`
>
> Elevated: `Date: 2026-05-01 (originally landed as `features/zones/decisions/0002`; elevated to project-wide on 2026-05-07 per §4.5 — visibility mechanism requires coordinated implementation across zones, cards, and presentation).`

**Observed in:** cards/01, all elevated ADRs (decisions/0006, 0007), and the supersession case (effects/04 Q1).

---

## P13 — `## Boundaries` reordered to position 6 (was 2)

Per spec §4.2, Boundaries moved from second-position to sixth-position in the Feature template. Migration reorders consistently.

**Observed in:** all 8 Feature roots.

---

## P14 — Supersede-then-fold pattern

When a folded ADR was reversed by later Model design (decision-content overridden, not just relocated):

1. Fold the ADR's content into the Model's below-divider provenance as a Q with explicit chosen-then-rejected status.
2. Record the original date AND the reversal date in the Q intro.
3. Make the rejection rationale explicit (not just "see new design") so the historical decision is preserved as a learning artifact.
4. The replacement option becomes the chosen one within the same Q (or a sibling Q referenced from the Q intro).
5. Also note in `## Consequences > Reverts/replaces`.

**Observed in:** `effects/04-effect-flow.md` Q1 — captures supersession of `effects/0002-suspension-model` (DecisionId-based suspension was originally accepted 2026-04-29; reversed 2026-05-07 by type-only routing in the new Model).

**Note:** this is distinct from §4.3's superseded-status convention (which preserves the old doc as a separate file with a header). Migration's "supersede-then-fold" relocates+supersedes in one motion since the original ADR file is being deleted as part of the §4.5 fold operation. Git history preserves the file's substance.

---

## P15 — Cross-feature inbound-ref sweep is part of migration

Each migration must sweep all docs across the project for inbound references to the feature's old ADR paths and update them. Common targets:
- Other Feature roots' Boundaries / Dependencies / Open sections
- Other Models' below-divider provenance (when they discuss decisions made in this feature)
- `ledgers/OPEN-ITEMS.md` entry references
- `ledgers/GLOSSARY.md` definitions citing ADRs
- `ledgers/FEATURE-MAP.md` annotations
- `_archive/sessions/*.md` — historical, leave as-is (per archive convention)
- Other elevated ADRs' references / Proposal fields

**Observed in:** every migration. Tools: `grep -rn "<feature>/decisions/" Product/ | grep -v "_archive\|_drafts\|MIGRATION-"`.

---

## P16 — Spec self-consistency drift

The spec (`PRODUCT-BUILDER.md`) carried forward references to `features/<feature>/decisions/` in §1 (numbering example, "Where cross-cutting docs live"), §2 (Doc types table — ADR row), and §6 (Cross-cutting infrastructure table) even after §4.5 codified that no such directory exists. Migration audit caught and fixed all four occurrences.

**Pattern:** when the spec changes invalidate prior examples elsewhere in the spec, sweep the whole spec text for stale references in the same edit. Don't trust localized changes to fully propagate.

---

## P17 — Three-mode Architecture sizing

The Feature `## Architecture` section's depth varies systematically with the feature's Model count:

| Model count | Architecture depth | Pattern |
|---|---|---|
| 1 Model | Thin / synthetic | A short paragraph naming the cross-Model flow that's mostly *implicit in the single Model*. Avoid duplicating the Model's content. |
| 2–3 Models | Medium | A real flow description with phase order and inter-Model interactions. |
| 4+ Models | Deep / numbered | A multi-step phase arc connecting all Models with explicit ordering. |

**Observed:**
- 1-Model Features (cards, presentation, triggers, zones): synthetic; 1–3 paragraphs naming the Model's role + how it integrates with cross-feature mechanisms.
- 2-Model Features (entities, actions): real flow; 2–4 numbered steps tying Models together.
- 4+ Model Features (effects, engine): deep; 6–7-step numbered arc spanning all Models.

**Codify:** Architecture in 1-Model Features should not duplicate the Model's content; it should name the cross-feature flow + the Model's place in it.

---

## P18 — Vocabulary discipline (cross-feature exclusion list at top)

Feature `## Vocabulary` opens with a one-line "cross-feature terms — X, Y, Z — live in their owning features and `Product/ledgers/GLOSSARY.md`" exclusion list, then a table of feature-owned terms only.

**Observed in:** all 8 Feature roots. Saves 30–60% of Vocabulary table size compared to including every referenced term.

**Codify:** "Vocabulary opens with cross-feature exclusion list, then table of feature-owned terms only" in spec §4.2.

---

## P19 — Mechanical-reshape vs full-reshape decision

Some Models came with already-aligned structure (numbered sections, existing provenance below a divider) that only needed header renames + minor restructuring. Others needed substantial rework. The decision factor:

- **Mechanical reshape** when the existing Model has clean numbered sections that map cleanly to template sections (e.g., entities/02, engine/04, engine/05, engine/06).
- **Full reshape** when the existing Model is a kitchen-sink consolidation of multiple historical docs (e.g., actions/01 — 2645 lines, multiple pasted-in provenance sections, redundant content). For these, do a tighter rewrite that preserves the contract and folds historical content explicitly.

**Observed:** actions/01 was the most extreme case (2645 → 535 lines). engine/01 went 440 → 380 lines (mostly rename). entities/01 went 568 → 314 lines (substantial trim of redundant Q&A).

**Pattern:** when a Model is >2x the typical Model size, expect substantive rewrite, not just header reshuffle.

---

## Standards-candidate review queue

After full migration, these patterns are ready for codification:

- [ ] **P1** — codify three-mode (now four-mode) below-divider convention into spec §4.3
- [ ] **P2** — clarify in spec that `## Decisions` is for elevated ADR pointers only
- [ ] **P3** — codify cross-Model invariant split + pointer pattern in spec §4.2 + §4.3
- [ ] **P4** — codify Product-root-relative path convention in spec §3 (or new §3a)
- [ ] **P5** — clarify Feature root `references:` field convention (recommendation: SHOULD include upstream paths)
- [ ] **P6** — make "canonical artifact in What it is" a SHOULD-have in spec §4.2 + §4.3
- [ ] **P10** — ✅ DONE (audit fix: `_templates/feature.md` emoji removed)
- [ ] **P11** — codify decisions-directory removal as part of migration in spec §4.5
- [ ] **P12** — codify date-preservation pattern when folding/elevating ADRs in spec §4.5
- [ ] **P14** — add "supersede-then-fold" as a recognized migration pattern in spec §4.5
- [ ] **P15** — codify cross-feature inbound-ref sweep as part of any ADR migration
- [ ] **P16** — ✅ DONE (audit fix: spec stale references swept)
- [ ] **P17** — codify Architecture sizing convention in spec §4.2
- [ ] **P18** — codify Vocabulary cross-feature-exclusion preface in spec §4.2

## Final migration stats

| Feature | Models | ADRs in | ADRs out | Status |
|---|---|---|---|---|
| cards | 1 | 1 (folded) | 0 | ✅ done |
| entities | 2 | 0 | 0 | ✅ done |
| zones | 1 | 3 | 1 (elevated to `decisions/0006`) | ✅ done — 2 folded + 1 elevated |
| triggers | 1 | 2 (folded) | 0 | ✅ done |
| effects | 4 | 3 | 0 | ✅ done — 2 folded + 1 supersede-then-fold |
| engine | 6 | 6 (folded) | 0 | ✅ done |
| actions | 2 | 8 | 1 (elevated to `decisions/0007`) | ✅ done — 7 folded + 1 elevated |
| presentation | 1 | 0 | 0 | ✅ done |
| **TOTAL** | **18** | **23** | **2 elevated** | **8 features migrated** |

(Note: total ADRs is 23 not 24 — earlier audit miscounted by 1; effects had 3 not 4.)

Project-wide ADRs grew from 5 (`0001-adopt-workflow` through `0005-objects-over-ids`) to 7 (added `0008-zone-visibility-model` — number bumped past `0006-two-pipeline-architecture` which landed on main during the migration; `0007-five-validation-layers`).

`features/<feature>/decisions/` directories: all removed.
