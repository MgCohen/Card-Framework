---
type: session
schema-version: 1
status: completed
date: 2026-05-01
tags: [#session]
---

# Session 17 — 2026-05-01

## Part 1 — Content (cap ~50 lines)

### 🎯 Active question coming in
**`GameState` slice catalog.** Pivoted in-session — user requested a thorough audit of the doc set instead. Slice catalog stays the durable next focus.

### Work done
- Comprehensive doc audit across 13 Models, 7 feature roots, 3 key ADRs, FEATURE-MAP, GLOSSARY, ARCHITECTURE, pile-counter sample. **41 findings** (19 Critical / 17 Moderate / 5 Minor) categorized into 5 buckets: mentioned-but-never-sketched, stub models, conflicts, deferred-never-picked-up, undocumented flow segments.
- Cross-referenced findings against ledgers: **8 already tracked, 7 partial, 26 genuinely new.**
- Triaged 26 new findings into ledgers:
  - **ALIGNMENT-DEBT: 7 entries / 5 cascade groups** — engine/01 stale `OnQueueDrained`/`Evaluate`/`OnIncomingCommand` signatures, triggers/01 §4 old `GameRules` interface, triggers/01 §2 `react: Func<T,Context,T>` vs `FoldResult`, effects/01 §7 `scope.Dispatch` missing awaiter pre-check, engine/02 §6 `IHandlerRegistry.Validate` still on `GameState`, presentation/01 §12 + pile-counter §9.1 `Scope` survivors from S15-16 discharge.
  - **OPEN-QUESTIONS: 11 new still-open** across Core Model (entity destruction lifecycle, modifier mutation API), Zones (zone-model internals, attachment storage location), Rules & Scheduling (`IHandlerRegistry`, `GameEngineBuilder`, engine startup, `ICommandPipeline`, `ITriggerSystem`/`ITriggerPipeline` unification, intent→template lookup, `GameRules` service wiring, regular-card `Register` entry point, `HistoryEnvelope` typed definition).
  - **BACKLOG: 2 entries** — `interaction` and `authoring` feature folders are entirely absent.
- Identified workflow root cause: §5.7 cascade walk was author-bounded; no protocol for "new doc supersedes old doc"; Model-level supersession only documented for ADRs.
- Patched `PRODUCT-BUILDER.md`: §3 (supersession recording covers Models), §4.3 (added `superseded` status convention + "Detecting silent supersession"), §5.4 (extended to ADR-or-Model), §5.7 (added mandatory **1a `builds-on:` walk / 1b retired-token grep / 1c shadow-check** substeps), §5.9 (generalized to ADR-or-Model, references §5.7 1b).
- `debt: 7 opened`

### Decisions settled
- §5.7 cascade walk now has 3 mandatory substeps; mental search alone is not sufficient.
- Model supersession uses existing single-source mechanism (`status: superseded by NN-<slug>` on old doc + `## Consequences > Reverts/replaces` on new); `supersedes:`/`superseded-by:` front-matter fields rejected per §3 single-source rule.
- All 7 new debt entries derive from cascade gaps the new §5.7 1b/1c rules would have caught at authorship time.

### 🎯 Active question going out
**Pass-mode discharge of 7 alignment-debt entries** (forced by §5.8 threshold). After clearance, return to `GameState` slice catalog.

## Part 2 — Links & open threads (cap ~50 lines)

### Followups created
- alignment debt: 7 entries / 5 cascade groups (see Part 1)
- open-questions: 11 new still-open items across Core Model / Zones / Rules & Scheduling
- backlog: `interaction` (gesture-to-intent layer) + `authoring` (effect recipe / card catalog) feature folders

### Ephemeral questions (scratch, not promoted)
- Should `supersedes:` be a front-matter field? — rejected after re-reading §3 single-source rule; existing `status: superseded by <ref>` mechanism covers it.
- Is engine/01 fully superseded by engine/02? — no, only the inline `GameRules` interface sketches are stale; the evaluation-loop machinery is still authoritative. Hence debt entry, not full supersession.
- 41 findings vs 26 new — gap of 15: 8 already tracked + 7 partially tracked under broader topics.

### References
- Docs touched: `_workflow/PRODUCT-BUILDER.md`, `ledgers/ALIGNMENT-DEBT.md`, `ledgers/OPEN-QUESTIONS.md`, `ledgers/BACKLOG.md`
- Related sessions: S15 (design pass producing §6 redesign), S16 (cascade discharge — partial; missed 2 Scope targets caught in this audit)
- PR: [#42](https://github.com/MgCohen/Card-Framework/pull/42)
