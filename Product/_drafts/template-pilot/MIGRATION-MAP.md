---
type: note
status: draft
references: [_workflow/PRODUCT-BUILDER.md]
tags: [#migration, #template-pilot]
---

# Migration Map — Existing Docs → New Feature/Model Template

Built from 8 parallel audits (one per feature) against the spec at commit `2f9c462`. Working document — opinionated; flag-and-discuss the **UNCLEAR** items before any actual doc edits.

**Scope.** All 8 features under `Product/features/` (`actions`, `cards`, `effects`, `engine`, `entities`, `presentation`, `triggers`, `zones`). 7 Feature roots present (cards missing). 19 Models. 24 ADRs in `features/<feature>/decisions/` directories.

---

## 1. Section rename table (mechanical, applies uniformly)

### Feature root

| Old section | → New section | Notes |
|---|---|---|
| `## Purpose` | `## What it is` | Add canonical artifact (flow diagram / sequence / pipeline sketch). Most current Purpose sections are 1-paragraph prose; will need expansion. |
| *(none)* | `## Vocabulary` | **NEW.** Lift feature-owned terms from Models (most are scattered in Model `## Concepts`, "Quick Vocabulary" tables, or just inline prose). |
| *(none)* | `## Architecture` | **NEW.** Cross-Model flow / phase order. Synthesize from Model bodies — most features have an implicit pipeline that's never been named at Feature level. |
| `## Composition` (bullets) | `## Composition` (table) | Always-table form. Existing Composition sections vary — some have tables, some bullets, some single-line refs. |
| *(none)* | `## Invariants` | **NEW.** Lift cross-Model rules from Model `## Invariants` / "Useful Invariants" sections. |
| `## Boundaries` | `## Boundaries` | Reorder later in doc (was 2nd, now 6th). |
| `## Public surface` | **DROP** — split | Types → `## Vocabulary`; behaviors → `## Architecture`; surface lives in per-Model `## Contracts`. |
| `## Dependencies` | `## Dependencies` | No change. |
| `## Settled decisions` | **DROP** | Vestigial — no `features/<feature>/decisions/` directory exists post-migration. |
| `## Open` | `## Open` | No change. |

### Model

| Old section | → New section | Notes |
|---|---|---|
| `## Problem` / "Why this doc exists" / "Model Summary" | `## What it is` | Add canonical artifact if missing (snippet / pseudocode / diagram). Most Models already have one. |
| `## Concepts` / "Quick Vocabulary" | **DROP** — split | Cross-Model concepts → Feature `## Vocabulary`; local concepts fold into `## What it is` / `## Contracts`. |
| `## Contracts` | `## Contracts` | Many existing Models lack an explicit Contracts heading — content is scattered across "Shape" / "API" / "Surface" sections. Consolidate. |
| `## Invariants` / "Useful Invariants" | `## Invariants` | Items that are cross-Model lift to Feature `## Invariants`; only Model-local stay. |
| `## ADR` | `## Decisions` | Renamed; **only kept if a project-wide ADR applies** — drop entirely if none. |
| `## Examples` | `## Examples` | Several Models have worked examples in dedicated sections — keep. Stub `*n/a — see Contracts.*` allowed. |
| `## Open follow-ups` / "Open Items" / "Open Questions" / "Next Questions" | `## Open follow-ups` | Naming is wildly inconsistent — normalize. |
| Provenance below divider | `## Options considered` / `## Rationale` / `## Consequences` | A few Models already have these (entities/01 has a "Provenance" mega-section); most don't. The historical record can be folded back to provenance from ADRs (see §2). |

---

## 2. ADR triage

24 ADRs total. Disposition under §4.5's hard rule (≥2 features = elevate to `Product/decisions/`; single feature = fold into Model provenance).

### Elevate to `Product/decisions/` (project-wide counter)

| Current path | Title | Reason for cross-feature scope | New path |
|---|---|---|---|
| `actions/decisions/0002` | On-demand validation | Touches presentation (AffordanceMap UI gating) + engine (rule-side eligibility) | `decisions/000N-on-demand-validation.md` |
| `actions/decisions/0003` | OnIncomingCommand flow-only | Defines flow contract between actions pipeline and engine GameRules | `decisions/000N-onincomingcommand-flow.md` |
| `actions/decisions/0007` | Five validation layers | Spans engine (rule eligibility) + actions (handler.Validate, IntentMapper) + presentation (AffordanceMap) | `decisions/000N-five-validation-layers.md` |
| `cards/decisions/0001` | Card shape | Anchors Card to entities `StateEntity` bridge + coordinates visibility/ref-rotation with zones | `decisions/000N-card-shape.md` |
| `effects/decisions/0003` | Replacement priority | Depends on triggers fold semantics (`triggers/01-trigger-fold`) | `decisions/000N-replacement-priority.md` |
| `engine/decisions/0003` | Snapshot in GameState | Crosses engine (state owner) + presentation (consumer) + actions (snapshot shape) | `decisions/000N-snapshot-in-state.md` |
| `engine/decisions/0004` | Snapshot writer is `IAvailableActions` | Engine owns the service; effects also write via it mid-execution | `decisions/000N-snapshot-writer.md` |
| `triggers/decisions/0002` | OnBefore<T> / OnAfter<T> replace CommandPreHook | Replaces hook types in actions + integrates with engine evaluator loop | `decisions/000N-trigger-typed-folds.md` |
| `zones/decisions/0002` | Visibility model | Cards `CardVisibilityState` + `CardRef` rotation + presentation transport-boundary filtering | `decisions/000N-zone-visibility-model.md` |

**9 ADRs → `Product/decisions/`.** Renumbering: existing `Product/decisions/0001-adopt-workflow.md` is the only project-wide ADR currently. New numbering starts at `0002` and runs sequentially in elevation order (TBD — could go alphabetical, dependency order, or chronological).

### Fold into Model provenance (single feature)

| Current path | Title | Target Model |
|---|---|---|
| `actions/decisions/0001` | Typed `ActionTemplate` per command | `actions/models/02-availability-snapshot.md` |
| `actions/decisions/0004` | Validation in handler | `actions/models/01-command-pipeline.md` |
| `actions/decisions/0005` | Generic command primitives, rule-enforced constraints | `actions/models/01-command-pipeline.md` |
| `actions/decisions/0006` | PlayerIntent → IIntentMapper → GameCommand | `actions/models/01-command-pipeline.md` |
| `actions/decisions/0008` | Intent vocabulary rename | `actions/models/01-command-pipeline.md` |
| `effects/decisions/0001` | Async layer hierarchy | `effects/models/01-async-dispatch.md` |
| `engine/decisions/0001` | GameRules as class with services | `engine/models/02-gamerules-contract.md` |
| `engine/decisions/0002` | GameRules zero state | `engine/models/02-gamerules-contract.md` |
| `engine/decisions/0005` | State pattern (immutable, redux-like) | `engine/models/04-gamestate-slices.md` |
| `engine/decisions/0006` | MTG-strict SBA known difference | `engine/models/01-evaluation-loop.md` |
| `triggers/decisions/0001` | Trigger fold semantics | `triggers/models/01-trigger-fold.md` |
| `zones/decisions/0001` | Zone shape | `zones/models/01-zone-model.md` |
| `zones/decisions/0003` | Insertion API | `zones/models/01-zone-model.md` |

**13 ADRs → fold into Models.** Each gets its content (Decision / Options / Rationale / Consequences) merged into the target Model's below-divider sections. The original ADR file gets deleted; git history preserves the record.

### Special: silently superseded

| Current path | Title | Disposition |
|---|---|---|
| `effects/decisions/0002` | Suspension model — explicit decision IDs | **SUPERSEDE first, then fold.** Per `effects/models/04-effect-flow.md §5.1`, type-only routing replaced explicit DecisionId. Apply §4.3 supersession convention: flip ADR status to `superseded by effects/04`, add header line, then fold the option content into Model 04's provenance. The original "DecisionId" decision becomes Option A (rejected because…) in 04's `## Options considered`. |

### UNCLEAR — flag for discussion

| Current path | Title | Question |
|---|---|---|
| `effects/decisions/0001` | Async layer hierarchy | The agent tagged SINGLE because the ADR text covers effects-internal async. But the layer types (`GameAction`, `CompositeAction`, `GameSignal`) live in `actions/` and the hierarchy is consumed by triggers + handlers across features. **Could go either way.** Lean: SINGLE — ADR is about effect orchestration, not about the type definitions themselves. |

---

## 3. Per-feature migration plan

Sized as per-feature deltas. Mechanical = template rename only. Synthesis = new content needed.

### `actions/` (heaviest)

- **Feature root (`actions.md`).** Full reshape: Purpose → What it is (add canonical artifact — pipeline diagram likely already in Model 01); add Vocabulary (lift Model 01's "Quick Vocabulary" 12-term table); add Architecture (synthesize the Intent → Template → Mapper → Command → Handler → State → Envelope → Evaluator flow that's currently embedded in Model 01); convert Composition to table; add Invariants (~5 cross-Model rules already visible in Model 01 §16); drop Public surface (redistribute) and Settled decisions (vestigial).
- **Models (2).** `01-command-pipeline.md`: rich content already; mostly section renames + Concepts split. `02-availability-snapshot.md`: rename "Why This Doc Exists" → "What it is"; consolidate floor/ceiling examples into `## Examples`; "Implications Across Existing Docs" table → drop or move to `## Open follow-ups`.
- **ADRs.** 5 fold (0001, 0004, 0005, 0006, 0008) + 3 elevate (0002, 0003, 0007). Heaviest folding job.

### `cards/`

- **Feature root: AUTHOR FROM SCRATCH.** Audit produced seed paragraph + 8-term vocabulary + dependency analysis (depends on entities; couples tightly with zones). Use that to draft all 8 sections.
- **Model (1).** `01-card-model.md`: rename sections; consolidate "Card Class / CardRef / CardMapState / OwnershipState / Card Visibility / Mutations" into `## Contracts` with sub-headings; "Useful Invariants" → `## Invariants`; add `## Examples` (currently has none).
- **ADRs.** 1 elevate (0001 → cross-feature card shape).

### `effects/`

- **Feature root.** Full reshape. Add Vocabulary (Effect / GameAction / CompositeAction / Dispatch x2 / Fold x3 outcomes / Attachment / Decision / Mutator / Envelope — agent listed). Add Architecture (the 6-phase effect lifecycle arc the agent identified). Add Invariants (consolidate the 9 in Model 04 §10 + 6 more visible across Models 01–03).
- **Models (4).** `01-async-dispatch.md`, `02-replacement-prevention.md`, `03-ongoing-effects.md` are well-shaped — mostly section renames. `04-effect-flow.md` is exceptionally dense (~450 lines, 11 numbered sections including "9 pinned invariants") — **flag for possible split** during migration; not in scope here.
- **ADRs.** 1 fold (0001) + 1 supersede-then-fold (0002) + 1 elevate (0003).

### `engine/` (largest by Models)

- **Feature root.** Full reshape. Add Vocabulary (8 terms from agent: OriginCommandId, Drain, IntakeResult/DrainResult, EngineLifecycle, MatchOutcome, Snapshot, GameRulesDefinition vs Runtime, MatchSetup). Add Architecture (the Factory → Lifecycle → Loop → GameRules-hooks → Snapshot → State flow that the agent diagrammed). Add Invariants (8 cross-Model rules: lifecycle gate, zero-rule-state, service injection, snapshot-in-state, framework-vs-game slices, lifecycle mirror, signals-in-envelopes, ADR-0005 immutability).
- **Models (6).** `01-evaluation-loop.md` through `06-match-lifecycle.md` are all well-structured. Mostly section renames + provenance backfill.
- **ADRs.** 4 fold (0001, 0002, 0005, 0006) + 2 elevate (0003, 0004).

### `entities/`

- **Feature root.** Reshape. Add Vocabulary (7+ terms — InstanceId vs ActorId, StateEntity<TDef>, ModifierId, Modifier source attribution, Composition handler, Variable / VariableValue, BaseValues vs effective value). Architecture (the Spawn → Base → Modifier flow + zone-movement identity preservation + snapshot/restore lifecycle the agent identified). Invariants (4 cross-Model: definition immutability, modifier source, cache eagerness, deterministic ModifierId).
- **Models (2).** `01-definition-instance-model.md` has a nested "Provenance: Core Model Decisions" mega-section that already maps to below-divider provenance — keep it; just normalize the headings. `02-runtime-surface.md` is well-structured; mostly renames.
- **ADRs.** None. Clean slate.

### `presentation/`

- **Feature root.** Reshape. Heavy overlap with the single Model — agent flagged the Feature should stay as index, Model as content. Lift 4 invariants currently embedded in Model prose (snapshot-as-state, validation layered, View never raw, intents atomic). Add Vocabulary (PlayerIntent, AffordanceMap, AvailableActionsSnapshot, Validation layers, IntentMapper).
- **Model (1).** `01-presentation-loop.md` is dense — 15 numbered sections — and needs trim more than reshape. Consolidate "Layer Responsibilities / State Shape / Snapshot Element / AffordanceMap / View / Store" into `## Contracts` with sub-headings; "End-to-End Flow Trace" → `## Examples`; "Decisions Locked" → `## Decisions` (if any are project-wide ADRs) or fold to provenance.
- **ADRs.** None. Clean slate.

### `triggers/`

- **Feature root.** Reshape. Add Vocabulary (GameCommand hierarchy, Trigger<T>, TriggerMatch, TriggerFoldResult, DispatchOrigin enum, TriggerPhase). Architecture is thin (1 Model) — surface registry-query → filter → pipeline-execution flow + ITriggerSystem-vs-ITriggerPipeline distinction. 4 invariants the agent named (Before→Execute→After phase order, replacement creates fresh fold, after-phase no-ops, handlers never dispatch GameAction).
- **Model (1).** `01-trigger-fold.md`: 9 numbered sections — mostly section renames. "§3: Context — Deleted" should become a provenance note, not a current section.
- **ADRs.** 1 fold (0001) + 1 elevate (0002).

### `zones/`

- **Feature root.** Already in NEW-template-aligned shape (7 of 8 sections). Mostly add Vocabulary (Zone / ZoneDefinition / ZoneContentsState / InsertPosition / CardRef / InstanceId / membership-vs-detail visibility) + tighten Architecture. 4 feature-level invariants already in Model "Useful Invariants" §18 — lift the cross-Model ones.
- **Model (1).** `01-zone-model.md`: 18 numbered sections + 11 invariants + nested "Provenance" — already follows new shape closely. Mostly section renames.
- **ADRs.** 2 fold (0001, 0003) + 1 elevate (0002).

---

## 4. Special cases

### 4.1 `cards/` Feature root — author from scratch

The feature folder has Models and ADRs but no `cards.md`. Audit produced enough material to seed all 8 sections — vocabulary list, candidate "What it is" paragraph, dependency analysis. Author this **before** Model migration so cross-Model invariants and vocabulary have a home.

### 4.2 `effects/decisions/0002` — silent supersession

`04-effect-flow.md §5.1` settled type-only routing, replacing the explicit DecisionId model that ADR-0002 proposed. ADR's status is still `accepted` — exactly the failure mode §4.3's supersession convention exists for. Two-step migration: (i) flip ADR-0002 to `superseded by effects/04` per §4.3 with the one-line header convention; (ii) fold both versions (the proposed DecisionId, the chosen type-only) into Model 04's `## Options considered` so the trail is visible.

### 4.3 `presentation/` Feature/Model overlap

Only one Model, dense (15 sections). Risk: migrating naively duplicates content between Feature root and Model. Plan: Feature root stays terse (index + Vocabulary + Architecture summary + Invariants); Model carries the deep content (Contracts / Examples / Open follow-ups). Auditor confirmed current division is roughly right — preserve it.

### 4.4 `effects/04-effect-flow.md` density

~450 lines, 11 numbered sections including a "9 pinned invariants" subsection. Migration is mechanical for now (rename sections), but **flag for possible split into 2 Models** as a follow-up after the migration lands. Out of scope here.

### 4.5 Renumbering elevated ADRs

9 ADRs elevate to `Product/decisions/`. Existing `decisions/0001-adopt-workflow.md` is the only current resident. New project-wide counter assignments need to happen — proposed ordering: by dependency (ADRs that other ADRs reference go first) rather than alphabetical or chronological. TBD — call when we get there.

### 4.6 ADR move semantics

§5.14 (archive convention) covers superseded/cancelled docs. **None of these ADRs are being superseded or cancelled** (except `effects/0002`) — they're being **relocated under a new doc convention**. Decision content survives intact. Git captures the move/delete; no archive ceremony needed for the relocation pattern.

---

## 5. Recommended migration order

Bottom-up by dependency graph, leaves first, so vocabulary stabilizes before consumers depend on it. Each migration is its own commit.

1. **`cards/`** — smallest scope (1 Model, 1 ADR), and it's the migration pilot for "author Feature root from scratch." Learning here calibrates the rest.
2. **`entities/`** — clean ADR slate, 2 Models. Foundation for `cards/` and most others.
3. **`zones/`** — already partly in new shape; lightest reshape. 1 Model, 3 ADRs (1 elevate).
4. **`triggers/`** — 1 Model, 2 ADRs (1 elevate). Architecture section is thin.
5. **`effects/`** — 4 Models, 3 ADRs. Includes the supersession case (0002). Heavier synthesis.
6. **`engine/`** — 6 Models, 6 ADRs (2 elevate). Largest by Models; richest vocabulary.
7. **`actions/`** — 2 Models, 8 ADRs (3 elevate). Heaviest ADR triage.
8. **`presentation/`** — 1 Model, no ADRs. Last because its consumers (engine/actions) are now stable; presentation depends on everything upstream.

After feature migrations: cleanup pass — verify all `features/<feature>/decisions/` directories are empty/removed; run `/builder:lint` for retired-token sweep + reference integrity; update `ledgers/FEATURE-MAP.md` if dependencies shifted.

---

## 6. What this map deliberately does NOT do

- **Author content.** Synthesis tasks (Vocabulary tables, Architecture flow text, Invariants lifts) are catalogued as "needs authoring" — actual authoring happens during per-feature migration.
- **Decide elevated ADR numbering.** Picked when those ADRs land in `Product/decisions/`.
- **Touch the spec (`PRODUCT-BUILDER.md`).** §5.6's "settled" framing was left as-is during the spec fold; if migration surfaces tension, revisit then.
- **Schedule.** Each feature migration is its own commit; cadence is up to the user. No deadline implied by the order.

---

## 7. Decisions needed before migration starts

1. **Confirm the 9 ADR elevations** in §2. Especially scrutinize: `engine/0004` (snapshot-writer) — UNCLEAR-trending-CROSS in the audit; `effects/0001` (async-layer-hierarchy) — UNCLEAR. Either could flip to fold instead.
2. **Confirm migration order** in §5 — particularly whether `presentation/` last is right given its tight coupling to `actions/` and `engine/`.
3. **Decide elevated ADR numbering scheme** — dependency order vs. alphabetical vs. chronological. Probably defer until needed.
4. **`effects/04-effect-flow.md` split** — accept the flag for follow-up, or address during migration?
