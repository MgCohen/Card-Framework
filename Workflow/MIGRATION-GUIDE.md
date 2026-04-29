---
type: plan
status: drafting
last-updated: 2026-04-29
builds-on: [WORKFLOW-PROPOSAL.md, FLOW-SIMULATION.md]
tags: [#workflow, #migration]
---

# Migration Guide — From Flat RESEARCH/ to the Proposed Workflow

**Purpose.** Spell out exactly where each existing artifact lands, what needs to be created, what needs to be backfilled, and in what order. The workflow proposal punted migration to §9; this is that doc.

**Scope.** One-time migration of the current 28-doc flat `RESEARCH/` (plus session checkpoint, prior art, sample game material) into the proposed `general/` + `features/` + `spikes/` + `support/` layout. Forward-going workflow lives in `WORKFLOW-PROPOSAL.md`.

---

## 1. Final structure (target)

```
RESEARCH/
  README.md                       # rewritten as index
  SESSION-CHECKPOINT.md           # stays at root
  sessions/
    session-01.md … session-12.md # backfilled retroactively (lightweight)

  general/
    WORKFLOW.md                   # promoted from Workflow/WORKFLOW-PROPOSAL.md
    GLOSSARY.md                   # from doc 09
    OPEN-QUESTIONS.md             # from doc 06
    ALIGNMENT-DEBT.md             # NEW — empty at start, filled forward
    VISION.md                     # from doc 00 (vision portion)
    SCOPE.md                      # from doc 00 (scope portion) or folded into VISION
    CAPABILITY-MAP.md             # from doc 02
    ARCHITECTURE.md               # from doc 03
    ROADMAP.md                    # from doc 05
    RECOMMENDATIONS.md            # from doc 07
    _templates/
      spike.md
      model.md
      sample.md
      adr.md
      session.md
      checkpoint.md
      plan.md

  features/
    entities/
      notes/
        01-definition-instance-model.md     # from doc 10
      decisions/
        # backfilled ADRs (see §4)
    zones/
      notes/
        01-zone-model.md                    # from doc 12 (carries doc 11's provenance)
      decisions/
    actions/
      notes/
        01-command-pipeline.md              # synthesized from docs 14, 15, 17, 18, 19
        02-availability-snapshot.md         # from doc 28
      decisions/
    triggers/
      notes/
        01-trigger-fold.md                  # from doc 21
      decisions/
    effects/
      notes/
        01-async-dispatch.md                # from doc 22
        02-replacement-prevention.md        # from doc 23
        03-ongoing-effects.md               # from doc 24
      decisions/
    engine/
      notes/
        01-evaluation-loop.md               # from doc 20
        02-gamerules-contract.md            # from doc 25
        03-snapshot-pipeline.md             # from doc 29
      decisions/
    presentation/
      notes/
        01-presentation-loop.md             # from doc 27
      decisions/

  spikes/
    scaffold-state-fit.md                   # from doc 04
    deckmech-command-pipeline.md            # from doc 13
    sabberstone-architecture.md             # from Old Docs/
    fireplace-architecture.md               # from Old Docs/
    brimstone-architecture.md               # from Old Docs/
    scaffold-states-deep-dive.md            # already in Old Docs/ as external research
    scaffold-entities-deep-dive.md          # already in Old Docs/ as external research

  support/
    samples/
      pile-counter.md                       # from doc 26
    prior-art/
      card-architecture-1.md                # from Old Docs/
      card-game-architecture-*.plan.md      # from Old Docs/ (3 of them)
      card-game-base-architecture-*.plan.md # from Old Docs/ (3 of them)
```

**Notes on placement choices:**

- **Doc 16 (action terminology discussion)** doesn't survive as a doc. Its content folds into `general/GLOSSARY.md` entries plus the session-12 (or wherever the rename landed) session note. Terminology debates aren't durable — the *result* is.
- **Doc 08 (core model decisions)** is a mega-decisions doc. Each decision becomes an ADR; the doc itself either disappears or becomes an index pointing to the ADRs.
- **Doc 11 (zone model decisions)** folds into `features/zones/notes/01-zone-model.md` as the provenance section below the divider. Same pattern for doc 14 → `features/actions/notes/01-command-pipeline.md`.
- **Doc 26 → Sample.** Pile Counter is the only sample so far. Status: `locked` (per session 9 decision), but re-check the four-part bar against current state of `features/engine/notes/02-gamerules-contract.md`.
- **`Workflow/` folder** does not survive migration. Once the workflow is accepted, `WORKFLOW-PROPOSAL.md` → `general/WORKFLOW.md`; this guide becomes a session note for the migration session(s); `FLOW-SIMULATION.md` and `AI-FIRST-AUTOMATION.md` either fold into `general/` or get retired.

---

## 2. Doc-by-doc mapping table

| Current | Target | Notes |
|---------|--------|-------|
| `00-vision-and-scope.md` | `general/VISION.md` (+ optionally split `SCOPE.md`) | Split or fold per §1 above. |
| `01-game-archetypes.md` | `general/CAPABILITY-MAP.md` (merge) **or** keep separate as `general/GAME-ARCHETYPES.md` | Decide if archetypes is its own ref or merges into capability map. |
| `02-framework-capability-map.md` | `general/CAPABILITY-MAP.md` | Direct rename. |
| `03-architecture-directions.md` | `general/ARCHITECTURE.md` | Direct rename. |
| `04-scaffold-package-fit.md` | `spikes/scaffold-state-fit.md` | Reframe as Spike with `concluded` or `graduated` status; forward-link to `features/engine/`. |
| `05-initial-roadmap.md` | `general/ROADMAP.md` | Direct rename. |
| `06-open-questions.md` | `general/OPEN-QUESTIONS.md` | Direct rename; restructure into the three-lifetimes form (only durable opens here). |
| `07-current-recommendations.md` | `general/RECOMMENDATIONS.md` | Direct rename; refresh as latest synthesis. |
| `08-core-model-decisions.md` | Split → ADRs across `features/{entities,zones,actions}/decisions/` | Mega-decisions doc dissolves; one ADR per atomic decision. |
| `09-core-glossary.md` | `general/GLOSSARY.md` | Direct rename; add changelog section per §6 of proposal. |
| `10-entity-definition-instance-model.md` | `features/entities/notes/01-definition-instance-model.md` | Add front matter; convert to Model template with provenance below divider. |
| `11-zone-model-decisions.md` | Folded into `features/zones/notes/01-zone-model.md` | Provenance section. Atomic decisions become ADRs in `features/zones/decisions/`. |
| `12-zone-model.md` | `features/zones/notes/01-zone-model.md` | Becomes the descriptive top half. |
| `13-deckmech-command-pipeline-reference.md` | `spikes/deckmech-command-pipeline.md` | Pure reference/exploration of an external library; classic spike. Status `concluded`. |
| `14-action-flow-decisions.md` | Folded into `features/actions/notes/01-command-pipeline.md` provenance + ADRs in `features/actions/decisions/` | Mega-decisions doc dissolves. |
| `15-action-request-command-history-model.md` | Merge into `features/actions/notes/01-command-pipeline.md` | Same Model. |
| `16-action-terminology-discussion.md` | Dissolved | Result lives in glossary; debate doesn't survive. |
| `17-action-definition-proposal.md` | Merge into `features/actions/notes/01-command-pipeline.md` | Original "Proposal" doc type folds into Model. |
| `18-action-flow-snippets.md` | Merge into `features/actions/notes/01-command-pipeline.md` | The end-to-end snippets become the Model's "Examples" section. |
| `19-handler-mutations-and-semantics.md` | Merge into `features/actions/notes/01-command-pipeline.md` | Or keep separate as `features/actions/notes/02-handler-semantics.md` if doc 01 grows too long. |
| `20-engine-evaluation-loop.md` | `features/engine/notes/01-evaluation-loop.md` | Direct rename; convert to Model. |
| `21-trigger-reaction-system.md` | `features/triggers/notes/01-trigger-fold.md` | Direct rename; convert to Model. |
| `22-async-decisions-and-suspension.md` | `features/effects/notes/01-async-dispatch.md` | Direct rename; convert to Model. Atomic decisions → ADRs. |
| `23-replacement-prevention-effects.md` | `features/effects/notes/02-replacement-prevention.md` | Direct rename. |
| `24-continuous-ongoing-effects.md` | `features/effects/notes/03-ongoing-effects.md` | Direct rename. |
| `25-gamerules-shape.md` | `features/engine/notes/02-gamerules-contract.md` | Direct rename; convert to Model. |
| `26-pile-counter-sample-game.md` | `support/samples/pile-counter.md` | Convert to Sample template (Subject + Projection layers). Lock-bar re-check. |
| `27-presentation-loop.md` | `features/presentation/notes/01-presentation-loop.md` | Direct rename. |
| `28-action-availability-and-mapping.md` | `features/actions/notes/02-availability-snapshot.md` | Direct rename. |
| `29-snapshot-production-pipeline.md` | `features/engine/notes/03-snapshot-pipeline.md` | Direct rename. ADRs (service-driven, typed templates, on-demand validation, GameRules-class) backfilled into both `features/engine/decisions/` and `features/actions/decisions/`. |

---

## 3. Old Docs / prior-art mapping

| Current | Target |
|---------|--------|
| `Old Docs/SabberStone-Architecture-Analysis.md` | `spikes/sabberstone-architecture.md` |
| `Old Docs/Fireplace-Architecture-Analysis.md` | `spikes/fireplace-architecture.md` |
| `Old Docs/Brimstone-Architecture-Analysis.md` | `spikes/brimstone-architecture.md` |
| `Old Docs/Card_architecture_1.md` | `support/prior-art/card-architecture-1.md` |
| `Old Docs/card_game_architecture_*.plan.md` (×2) | `support/prior-art/` |
| `Old Docs/card_game_base_architecture_*.plan.md` (×3) | `support/prior-art/` |
| External research on Scaffold States/Entities (commit `1493e01`, `50b852e`) | `spikes/scaffold-states-deep-dive.md`, `spikes/scaffold-entities-deep-dive.md` |

**Distinction.** Architecture analyses (SabberStone, Fireplace, Brimstone, Scaffold) are **spikes** — we read them as part of design exploration. They produced findings that fed real decisions. Old plan-docs and `card_architecture_1.md` are **prior art** — pre-current-research artifacts kept for reference, not actively informing decisions.

---

## 4. ADR backfill scope

The proposal calls for ADRs in `features/<feature>/decisions/`. The current 28 docs contain dozens of decisions. Backfilling all of them is expensive; backfilling none loses the searchability the workflow promises.

**Recommendation: backfill the load-bearing ones, skip the rest.** A decision is load-bearing if it is *cited downstream* — referenced in subsequent docs as a constraint. A decision that nobody refers to is just a snapshot of a debate; it can stay folded into the Model's provenance.

**Heuristic for backfill candidates.** From the SESSION-CHECKPOINT "Key decisions settled" list — those are the load-bearing ones, by definition. Roughly 13 ADRs:

1. `features/engine/decisions/0001-gamerules-as-class-with-services.md`
2. `features/engine/decisions/0002-gamerules-zero-state.md`
3. `features/engine/decisions/0003-snapshot-in-state.md`
4. `features/engine/decisions/0004-snapshot-writer-iavailableactions.md`
5. `features/actions/decisions/0001-typed-action-template.md`
6. `features/actions/decisions/0002-on-demand-validation.md`
7. `features/actions/decisions/0003-onincomingcommand-flow-only.md`
8. `features/actions/decisions/0004-validation-in-handler.md`
9. `features/actions/decisions/0005-generic-primitive-rule-constraint.md`
10. `features/actions/decisions/0006-player-intent-mapper.md`
11. `features/actions/decisions/0007-five-validation-layers.md`
12. `features/engine/decisions/0005-state-pattern.md`
13. `features/engine/decisions/0006-mtg-sba-known-difference.md`

Plus the renames as ADRs (since they cascaded across many docs):

14. `features/actions/decisions/0008-intent-vocabulary.md` (OriginRequest→OriginIntent etc.)
15. `general/decisions/0001-naming-alignment-pass.md` *(or fold into a single ADR with multi-rename body — open question whether `general/decisions/` exists at all; the proposal §1 doesn't show it. Could go in `features/actions/decisions/` since most renames were action-side.)*

Plus the trigger system:

16. `features/triggers/decisions/0001-trigger-fold-semantics.md`
17. `features/triggers/decisions/0002-onbefore-onafter.md`

Plus async/effects:

18. `features/effects/decisions/0001-async-layer-hierarchy.md`
19. `features/effects/decisions/0002-suspension-model.md`
20. `features/effects/decisions/0003-replacement-priority.md`

Roughly 20 ADRs total. Write each in template form (§4.4 of proposal): one screen, options-considered + decision + consequences. **One session** if focused, **two** if interleaved with structural moves.

**Open question — global vs feature ADRs.** The proposal §1 only shows `features/<feature>/decisions/`. Workflow-level decisions (this migration; choice of ADR backfill scope; promotion-gate thresholds) have nowhere to live. **Suggestion**: add `general/decisions/` for project-wide decisions, mirroring per-feature counters. *Or* treat workflow decisions as out-of-band (in this `Workflow/` folder, pre-migration, and as ADRs against the workflow itself once `general/WORKFLOW.md` exists).

---

## 5. Migration sequence

**One session per phase, ideally.** Keeps each phase reviewable as a PR.

### Phase 1 — Skeleton (pure plumbing, no content moves)

1. Create directories: `RESEARCH/general/`, `RESEARCH/general/_templates/`, `RESEARCH/features/{entities,zones,actions,triggers,effects,engine,presentation}/{notes,decisions}/`, `RESEARCH/spikes/`, `RESEARCH/support/{samples,prior-art}/`, `RESEARCH/sessions/`.
2. Drop `.gitkeep` in empty leaves so git tracks the structure.
3. Write the 7 templates into `general/_templates/`.
4. Create empty `general/ALIGNMENT-DEBT.md` with header.
5. Commit + PR titled "Workflow migration phase 1: skeleton."

**Exit criteria:** structure exists; nothing moved yet.

### Phase 2 — `general/` content (lowest-risk moves)

6. `git mv` orientation docs: 00, 01, 02, 03, 05, 06, 07, 09 → `general/` with new names per §2 table.
7. Add front matter to each. Status: `settled` for all (they describe project-wide reality, not pending decisions).
8. Update `RESEARCH/README.md` to point at the new structure.
9. Commit + PR.

**Exit criteria:** `general/` is populated; cross-refs in remaining flat docs not yet fixed (handled in phase 6).

### Phase 3 — Spikes + prior-art

10. `git mv` doc 04 → `spikes/scaffold-state-fit.md`. Add front matter, set status `graduated`, add forward-link to `features/engine/`.
11. `git mv` doc 13 → `spikes/deckmech-command-pipeline.md`. Status `concluded`.
12. `git mv` Old Docs/ contents per §3 table.
13. Commit + PR.

### Phase 4 — Features (the big move)

This is the heaviest phase. Split if needed: one PR per feature folder.

14. Move and convert per §2 table. For each Model:
    - Add front matter (`type: model`, `status: settled`).
    - Restructure into the §4.2 template (Problem / Concepts / Contracts / Invariants / Examples / Implications / Open follow-ups, then divider, then Options/Rationale/Consequences if applicable).
    - Most docs only need front matter + light reorganization, not rewrites.
15. For docs that merge (e.g., 14+15+17+18 → `features/actions/notes/01-command-pipeline.md`): merge content, delete originals (`git rm`), make sure no unique content is lost.
16. For doc 11 → fold into doc 12's provenance section.
17. Commit + PR (or one per feature).

**Exit criteria:** all 28 numbered docs are gone or moved; `features/` is populated.

### Phase 5 — Sample lock + ADR backfill

18. `git mv` doc 26 → `support/samples/pile-counter.md`. Restructure into Sample template (Subject / Why this sample / Projection / Not exercised / Open mappings).
19. Re-check the four-part lock-bar against current state of `features/engine/notes/02-gamerules-contract.md`. Set status accordingly.
20. Write the ~20 ADRs from §4 above. One commit per feature's ADR set, or one PR for the lot.

### Phase 6 — Cross-reference sweep

21. Grep `RESEARCH/` for `doc NN` and `NN-` references. Replace with paths (`features/actions/notes/01`, etc.).
22. Grep for old filenames. Replace with new paths.
23. Update `SESSION-CHECKPOINT.md` "Artifacts to read on resume" table.
24. Update README.
25. Commit + PR.

**Exit criteria:** no references to old doc numbers anywhere in `RESEARCH/`.

### Phase 7 — Session backfill (lightweight)

26. For each historical session (1–12): write a thin `sessions/session-NN.md` from PR descriptions + commits. Two-part template, but very short — these are reconstructions, not records.
27. Commit + PR.

**Exit criteria:** `sessions/` has 12 entries, each ~one screen.

### Phase 8 — Workflow promotion + cleanup

28. `git mv Workflow/WORKFLOW-PROPOSAL.md RESEARCH/general/WORKFLOW.md`. Strip `status: proposed`, set `status: accepted`. Strip §7 (vs. today) and §9 (out of scope) — those were proposal-only sections.
29. Decide: do `Workflow/FLOW-SIMULATION.md` and `Workflow/AI-FIRST-AUTOMATION.md` graduate into `general/` (e.g., `general/AI-FIRST-PLAN.md`) or stay in `Workflow/` as historical context, or get retired entirely?
30. Delete `Workflow/` if everything graduated.
31. Final commit + PR titled "Workflow accepted and migrated."

---

## 6. Risk register

| Risk | Mitigation |
|------|-----------|
| Lost content during merges (docs 14+15+17+18 → 1 file) | Diff carefully; use `git log --follow` post-move to verify no rogue content disappeared. PR review checks coverage. |
| Cross-ref breakage during phase 4–6 | Phase 6 sweep is mandatory before merge. Grep-based, not eyeball-based. |
| ADR backfill rabbit-hole | Stop at the 20-doc list. Future ADRs are for forward-going decisions; backfill is a one-time bounded task. |
| Sample lock-bar fails post-restructure | Acceptable — set Pile Counter to `draft`, re-lock when stable. Better than a fake lock. |
| `git mv` not preserving history on rename+content-edit | Make the moves and content edits in *separate commits*. `git mv` first (pure rename), then edit. |
| Disagreement on placements (e.g., doc 19 in actions vs. its own folder) | Resolve before phase 4 starts. This guide is the place to lock it. |
| Phase 4 PR is too large to review | Split per feature — one PR per `features/<feature>/`. ~7 PRs. |
| `general/decisions/` open question (§4) | Decide before backfill starts. Default: skip it; put workflow ADRs in `Workflow/` until promotion, then in `general/` as a flat file. |

---

## 7. Out of scope for this migration

- Updating `DOCS/` and `PLANS/` — they're not part of the research workflow scope.
- Code-side renames in `Assets/` — separate effort.
- Promoting any settled Models to `DOCS/` (the §5.6 promotion gate) — eligibility checks happen post-migration on a per-doc basis.
- Workflow tooling automation — covered in `AI-FIRST-AUTOMATION.md`, separate work stream.
