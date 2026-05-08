---
type: draft
status: in-flight
tags: [#workflow, #rules]
---

# Doc Rules — draft

Working draft of the rules that govern each kind of doc in the Product corpus. Iterate here; promote to `_workflow/PRODUCT-BUILDER.md` when stable. Goal: each rule should be tight enough that an AI authoring or auditing the corpus can apply it without ambiguity.

> **Status legend per rule:**
> - 🟢 **locked** — applied in practice, surviving multiple iterations.
> - 🟡 **draft** — articulated but not fully applied / tested.
> - 🔴 **open** — known gap, needs design.

---

## Model

A doc under `features/<feature>/models/`. The unit of canonical design — focused, deep, narrow. One model = one central element explained in depth.

### What a model IS

🟢 **Hard rule.** A model has a single **central element** that is both:
1. **Concrete** — a class, interface, service, contract, or structured type. Something you can name and point at.
2. **Load-bearing** — carries logic, behavior, or its own design space. Not just data.

🟢 **The title names the central element.** Reading the title tells you what the model is about; reading the body explains that one element in depth.

🟢 **The body covers the central element + the supports that exist for it.** Sub-types, payloads, helpers, mechanics that only make sense in the central element's context all live inside the model — not as peers, but as parts of how the central element works.

🟢 **A model is the canonical home for its central element.** First-time definition; the one place to read the contract.

### What a model is NOT

🔴 **Not a model: a concept without a central element.** "Lifecycle," "validation," "loop," "subscription model" as standalone topics aren't models if no concrete load-bearing thing centralizes them. The concept is just a name for a collection of related things — that collection belongs to a feature (across multiple models) or as supports inside an existing model.

🔴 **Not a model: pure data types alone.** Enums, simple records, signals, payload types, value objects, outcome hierarchies — these are concrete but not load-bearing. They have shape but no logic, no behavior, no real design space of their own. They're supporting content within the model that defines or uses them.

🔴 **Not a model: an amalgamation tied by a theme.** Multiple peer concrete elements with no unifying central element belong in a feature (with multiple focused models), not a single model. If you can't name *the* central element, you're looking at a feature.

🔴 **Not a model: rules / behaviors / extensions of a surface defined elsewhere.** Move them to the surface's canonical home.

🔴 **Not a model: a recipe / pattern using existing surfaces.** Belongs in the feature root or the canonical home of the surfaces being composed.

🔴 **Not a model: a method extension to a class defined elsewhere.** Goes to the class's canonical home.

### How to evaluate

When deciding whether some content earns its own model, walk the flow:

1. **Can you point at a single concrete element as the core?**
   - No → not a model. It's a feature, content for an existing model, or unorganized. Stop.
   - Yes → continue.

2. **Does the element carry load-bearing logic, behavior, or design space?**
   - No (pure data — enum, record, signal, payload) → not a model. Support inside the model that defines or uses it. Stop.
   - Yes → it's a candidate. Continue.

3. **Does the title match the central element?**
   - No → rename the title or split the content until they match.
   - Yes → continue.

4. **Should the candidate be its own model, or supporting content under a parent?** Soft rule, design call. **Default: support under the parent.** Promote when:
   - Substantial design depth of its own.
   - Independent consumption (consumers reach for it without going through a parent).
   - Authoring concerns benefit from a focused doc.
   - Supporting elements are forming around it.

   *"Could be a model" ≠ "should be a model."* Defaulting to support keeps the corpus from over-fragmenting.

### Worked examples — quick reference

See **§Examples — good and bad calls** below for full reasoning. Quick table:

| Case | Verdict | Why |
|---|---|---|
| `GameEngine` class as a model (engine/01) | ✅ | Concrete + load-bearing (drain, queue, gate enforcement) |
| `IAvailableActions` service as a model (engine/03) | ✅ | Concrete + load-bearing (write protocol, production semantics) |
| `IGameFactory` as a model (engine/05) | ✅ | Concrete + load-bearing (bootstrap walk, two-layer DI split) |
| `Card` class as a model (cards/01) | ✅ | Concrete + load-bearing (resolution, slice access, ref rotation) |
| "Match Lifecycle" as a model (engine/06) | ❌ | Concept-only — no central element. Amalgamation of enum + commands + handlers + signals + outcome under the "lifecycle" umbrella. |
| "Framework Slice Tier" as a model (engine/04) | ❌ | Convention/index, no central element. Tier rules + catalog table. |
| `EngineLifecycle` enum alone | ❌ | Concrete but pure data. Support inside the model where the lifecycle implementation lives. |
| `MatchOutcome` closed hierarchy | ❌ | Concrete but pure data (payload). Support next to `EndMatchCommand`. |
| `MatchStartedSignal` / `MatchEndedSignal` | ❌ | Concrete but pure data (fieldless / outcome-carrying). Support. |
| `StartMatchCommand` / `EndMatchCommand` records | ❌ | Concrete but pure data (intent shapes). Support. |

---

## Feature

A doc under `features/<feature>/<feature>.md`. The capability-level entry point.

- 🟢 **Feature root owns the synthesis** — what the feature is, vocabulary, architecture (how the models compose), invariants, boundaries, dependencies.
- 🟢 **Feature root is the home for cross-model conventions and recipes** — patterns that use multiple models or that don't earn their own model. Example: `engine.md` owns the setup-commands recipe and the disposal-split convention.
- 🟡 **A feature can be feature-root-only.** Infrastructure / package-integration features can ship without models. Example: `state/state.md` documents the Scaffold.State integration end-to-end with no models. A model is added when a framework-specific extension emerges.
- 🟡 **Feature roots should be a real synthesis, not a TOC.** Reading the feature root should give an AI enough context to use the feature; models add depth for authors working on that feature.
- 🔴 **When does a feature split?** Heuristic TBD. Working principle: when models cluster around two distinct capabilities that consumers reference separately.

---

## Spike

A doc under `spikes/`. Pre-settled exploration.

- 🟡 **Spikes are ephemeral.** They explore an open design space; outcome is either folded into model(s) or abandoned.
- 🟡 **Spikes carry a resolution path.** "When this lands, the canonical home will be X." Without that, the spike will orphan.
- 🔴 **Spike → model promotion ceremony** TBD.

---

## Research / Reference

A doc capturing external knowledge: studied games, explored codebases, prior-art surveys, pattern catalogs. Lives flat under `Product/research/`.

- 🟢 **Research has no resolution path.** Unlike spikes, research isn't aimed at a settled outcome. It's reference material; consulted, not resolved.
- 🟢 **Spike vs research test.** Does the doc have a "when this lands, X gets settled" pointer? Yes → spike. No → research. (A doc that started as a spike but no longer has a resolution path has drifted into research; reclassify.)
- 🟡 **Models / features cite research** when a design decision drew on it. Research is the source the canonical doc references; the canonical doc is what authors and AI consult day-to-day.
- 🟡 **Research can be revisited and refined.** A second pass on the same game with new context appends or rewrites in place — not archived as superseded.
- 🟡 **Frontmatter:** `type: research`, no `status:` (research isn't a workflow item that ends). Tag with `#research` and a topic tag (`#prior-art`, `#pattern`, `#codebase`, etc.).

---

## Draft

A doc under `_drafts/`. In-flight content not yet at its canonical home.

- 🟡 **Drafts iterate without churning canonical docs.** Use when the target shape is unstable.
- 🟡 **Drafts have a target home.** Each draft states where it goes when it stabilizes.
- 🟡 **Promote in one commit.** When stable, the draft lands at its target home and the draft file is deleted.
- 🔴 **Stale-draft policy** TBD. Drafts that haven't moved in N sessions should surface for "promote, abandon, or rewrite."

---

## Decision (ADR)

A doc under `decisions/`. Project-wide or cross-feature decision history.

- 🟡 **ADRs are append-only.** Status flips (proposed → accepted → superseded) but the history doesn't get rewritten.
- 🟡 **Single-feature decisions fold into model provenance** rather than getting their own ADR. The "Options considered" section in the model is the home.
- 🔴 **ADR vs. Q-folded-in-model criteria** — currently informal. Working rule: cross-feature → ADR; single-feature → fold into the model that owns the surface affected.

---

## Glossary

A doc at `ledgers/GLOSSARY.md`. Cross-feature term definitions.

- 🟡 **One-line definitions only.** Plus a canonical-home pointer.
- 🟡 **Don't restate.** If a feature already defines a term canonically, GLOSSARY links there rather than duplicating prose.
- 🟡 **Cross-feature terms only.** Feature-internal terms live in the feature's vocabulary table.

---

## Ledger

A doc under `ledgers/` — registry / index files like `FEATURE-MAP.md`, `OPEN-ITEMS.md`, `GLOSSARY.md`.

- 🟡 **Hand-maintained at v1; auto-generated at v2.** Update cadence is event-driven (model lands, feature ships, dependency shifts, item resolves).
- 🟡 **Resolution = delete the entry** (for `OPEN-ITEMS.md`). The resolving doc owns the answer.
- 🔴 **Auto-generation trigger** TBD (~15 models per FEATURE-MAP).

---

## Examples — good and bad calls

Concrete cases that ground the rules. Each entry: the doc (or proposed doc), the verdict, and the reasoning.

### Model — earns its file ✅

Each entry: doc, central element, why it passes.

- **`engine/01` (Game Engine).** Central: `GameEngine` class. Load-bearing: drain logic, queue management, lifecycle gate enforcement, intake contract. *Why it earns:* canonical home for `GameEngine`; no other doc owns the class; consumers reach for it directly.
- **`engine/02` (GameRules Contract).** Central: `GameRulesRuntime<TDefinition>` (paired with `GameRulesDefinition`). Load-bearing: two contract methods (`FilterTriggers`, `Evaluate`), anti-scope, composition story. *Why it earns:* the contract games implement; rich design space of its own.
- **`engine/03` (Snapshot Production).** Central: `IAvailableActions` service. Load-bearing: write protocol (`Set` / `Append` / `Clear`), production semantics, validation posture. *Why it earns:* canonical home for the production service. (The snapshot *shape* lives in `actions/02` — different element, different model.)
- **`engine/05` (Game Factory).** Central: `IGameFactory` (with `MatchSetup`, `IMatchBuilder` as supports). Load-bearing: bootstrap walk, two-layer DI split, container open-generic resolution. *Why it earns:* canonical home for match construction; substantial design pass.
- **`engine/07` (Game Definition).** Central: `GameDefinition` bundle. Load-bearing: bundle shape, `ISliceProvider` contract, six-step Build walk. *Why it earns:* the top-level contract for shipping a game.
- **`actions/01` (Command Pipeline).** Central: the command pipeline contract (with `GameCommand`, `CommandHandler`, `CommandResult`, `HistoryEnvelope`, `HistoryState` as supports). Load-bearing: five-layer validation, envelope production, mutator dispatch. *Why it earns:* canonical home for the action-flow vocabulary; substantial design pass.
- **`actions/02` (Availability Snapshot).** Central: `AvailableActionsSnapshot` + `ActionTemplate` typed hierarchy. Load-bearing: typed-template-per-command-type design pass, snapshot-as-state decision. *Why it earns:* concrete + structured + has its own design space.
- **`cards/01` (Card Model).** Central: `Card` class + `CardRef`. Load-bearing: resolution, slice access, ref rotation, privacy. *Why it earns:* the framework's primary handle; consumers reach for it from many features.

### Model — does not earn its file ❌

- **`engine/06` (Match Lifecycle), current state.** Fails the central-element test. "Lifecycle" is a concept; no `LifecycleService` / `LifecycleController` / load-bearing element centralizes the doc. The contents are an amalgamation: `EngineLifecycle` enum + gate rules (rules about `Dispatch`) + `StartMatchCommand` / `EndMatchCommand` (data) + their handlers + two signals (data) + `MatchOutcome` (data). Tied together by the "lifecycle" theme but no central thing realizes it. *Verdict:* dissolves; everything becomes supporting content under `GameEngine` in `engine/01` (the central element that *does* drive the lifecycle).
- **`engine/04` (Framework Slice Tier), current state.** Fails the central-element test. Contents are tier conventions (rules) + a catalog index (a table) + the external-keying convention (a rule). No `IFrameworkTier` element. *Verdict:* contents redistribute — catalog index and tier rules into `state.md` or the engine feature root.
- **`effects/02` (Replacement and Prevention), current state.** Fails. Composes `OnBefore<T>` (canonical in `triggers/01`) + `IEffectScope` (canonical in `effects/04`). The "three before-fold outcomes" is a *rule* about how existing surfaces compose. *Verdict:* folds into `effects.md` feature root.
- **`effects/03` (Ongoing Effects), current state.** Fails. Author explicitly says: *"No new pipeline concept is needed — the attachment uses the same entity, trigger, and action machinery as everything else."* Recipe over existing surfaces. *Verdict:* folds into `effects.md` feature root.
- **`presentation/01` (Presentation Loop), current state.** Fails. "Loop" is a concept; the doc itself describes itself as a "synthesis layer over decisions made in sibling features." The few new surfaces it sketches (`IIntentMapper`, `IGameStore`) are loosely-defined, not the title. *Verdict:* folds into `presentation.md` feature root; if `IIntentMapper` later earns a model, it lands there as its own focused doc.

### Concrete-but-not-load-bearing — pure data (support, not a model) 🔵

These have central elements (concrete types) but fail the load-bearing test. They're support inside the model that defines or uses them.

- **`EngineLifecycle` enum** — just states; no logic. Lives inside whatever model holds the lifecycle implementation.
- **`MatchOutcome` closed hierarchy** — payload type; pattern-matched but no behavior. Lives next to `EndMatchCommand`.
- **`MatchStartedSignal` / `MatchEndedSignal`** — fieldless / outcome-carrying signals. Live next to the handlers that emit them.
- **`StartMatchCommand` / `EndMatchCommand` records** — intent shapes carrying parameters. Live next to the methods that dispatch them.
- **`PlayerIntent`** — small type, always reached via `IntentGateway` / `IIntentMapper`. Lives inside `actions/01`.
- **`HistoryEnvelope`** (current state) — pseudocode, no fields finalized; structured payload not load-bearing logic. Lives inside `actions/01` until typed and rich.

### Borderline — concrete + load-bearing but currently support 🟡

These pass the hard rule (concrete + load-bearing) but the soft rule (depth/independence) keeps them as supporting content for now. Could promote later if their design grows.

- **`StartMatchHandler` / `EndMatchHandler`** — have validation + signal emission + state-flip logic. The logic is mechanical glue between command and effect; not enough independent design space to promote. Stay as supports.
- **`ActionTemplate` typed hierarchy** — currently a major sub-section of `actions/02`. Could split out as its own model if the typed-template design grew further (e.g., game-extension story, codegen, validation). For now correctly inside `actions/02` because the snapshot and the templates were settled together.

### Feature — earns its file ✅

- **`state` (feature-root only).** Integrates Scaffold.State as the framework's state primitive. No models — the feature root is the canonical doc. *Why it earns:* load-bearing infrastructure with no canonical home before; consumers across every other feature reference it.
- **`engine`.** Multi-model feature with a feature root that carries synthesis (vocabulary, architecture spanning seven models, cross-model invariants), plus recipes/conventions (setup commands, disposal split). *Why it earns:* coherent capability; feature root is a real synthesis, not a TOC.

### Feature — split candidate 🔴

- *(none surfaced yet — entry kept open for when one appears)*

### Spike → Model promotion ✅

- **`spikes/effect-pipeline.md`.** Currently exploring effect-level orchestration (`oq-effect-pipeline`). Carries a "When this lands" pointer at where the canonical model would live. *Why it's a good spike:* explicit resolution path, scoped to one open question.

### Research ✅

- **`research/sabberstone-architecture.md`, `research/fireplace-architecture.md`, `research/brimstone-architecture.md`** — codebase studies of three Hearthstone simulators. *Why research:* prior-art knowledge banks. No resolution path; consulted as reference when designing analogous surfaces.
- **`research/deckmech-command-pipeline.md`** — external command-pipeline reference that fed `features/actions/`. *Why research:* the doc itself says "reference material … not an implementation plan."

### Reclassified spike → research ✅

- All four prior-art docs above were originally `type: spike, status: concluded` under `spikes/`. They never had a resolution path — they were always reference material. Migration: moved to `research/`, frontmatter updated to `type: research`.

### Draft → canonical promotion ✅

- **This doc (`_drafts/doc-rules.md`).** Iterates on rules across multiple doc types in one place. Promotion target named: `_workflow/PRODUCT-BUILDER.md`. *Why it's a good draft:* moving rules ought to churn the canonical workflow doc less often; a draft amortizes the iteration into one promote commit.

### Folded ADR ✅

- **Match-lifecycle decisions.** Single-feature scope; no project-wide ADR — the design folded into `engine/06`'s `## Options considered` / `## Rationale`. *Why folded:* no cross-feature reach.
- **ADR-0006 (two-pipeline architecture).** Cross-feature (engine + actions + effects). Lives in `decisions/`. *Why standalone:* every affected feature references it.

### Glossary entry — done right ✅

- **`EngineLifecycle`.** One-line: "Public match-lifecycle enum on `GameEngine`: `NotStarted`/`Started`/`Ended`." Plus pointer to `Match Lifecycle §Lifecycle states`. *Why it's good:* no restating; canonical home pointer.

### Glossary entry — drift risk 🔴

- *Section refs in GLOSSARY have decayed before* (e.g. `§4.1` numbering when sections renumber). Working fix: use section names instead of numbers. Caught and updated this session for the lifecycle terms.

---

## Open questions about the rules themselves

- **Cross-cutting "system" identification.** When a system spans multiple features (e.g. lifecycle = engine + state + actions), which feature owns the system model? Currently: the feature most central to the system's purpose (lifecycle → engine, even though state/EngineState slice is involved).
- **Boundary between feature root and model when a feature has only one model.** Should the model fold into the feature root? Currently: keep separate so the feature root stays a synthesis layer and the model carries the design.
- **What about content that's framework-convention but doesn't belong to any feature?** e.g. naming conventions, file layout, the canonical-home rule itself. Lives in `_workflow/PRODUCT-BUILDER.md`.
- **Concluded design spikes.** `spikes/entities-bridge-replacement.md`, `spikes/entities-state-unification.md`, `spikes/scaffold-state-fit.md`, `spikes/domain-model-proposal.md` carry status `concluded` / `accepted` / `graduated` / `superseded`. Content is folded into models. Open: archive (move to `_archive/`), delete (history is in git), or leave (status field carries the state)?

---

## Promotion target

When this draft stabilizes, the rules promote into `_workflow/PRODUCT-BUILDER.md`. Each section above maps to a corresponding section in the canonical workflow doc; the open-questions section either resolves (rule locks) or graduates to a separate "design pass needed" entry.
