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

A doc under `features/<feature>/models/`. The unit of canonical design.

- 🟢 **A model owns a canonical surface or system.** "Canonical" = this is where the surface/system is defined for the first time, the one place to read the contract.
- 🟢 **Implementation isn't disqualifying — irrelevance is.** A load-bearing, framework-required implementation that realizes a system *can* live in that system's model alongside the abstraction. Example: `StartMatchCommand` / `EndMatchCommand` + handlers + signals are framework-required implementations of the lifecycle system, and live in `engine/06` because they realize the system defined there.
- 🟢 **NOT a model** (under the strict reading):
  - Implementations of surfaces defined elsewhere, when the implementations don't form a coherent system on their own.
  - Method extensions to a class defined elsewhere → go to the class's canonical home.
  - Behaviors / rules of a surface defined elsewhere → go to the surface's canonical home.
  - Patterns / recipes using existing surfaces → feature root.
- 🟢 **Rules / behaviors / extensions move to their owner.** If a model accumulates content that describes how a surface defined elsewhere works or extends, that content belongs in the surface's canonical home.
- 🟡 **Naming should match content.** When a model's canonical concern sharpens, its title may need to follow. Example: "Evaluation Loop" → "Game Engine" once the model's scope became "the GameEngine class" rather than "the loop algorithm."
- 🟡 **Each cross-doc reference should point at exactly one canonical home.** No "see X or Y" for the same surface.
- 🔴 **When does a model split vs. stay merged?** Heuristic TBD. Working principle: split when two surfaces in the same model don't reference each other and have separate consumers; merge when content is "load-bearing implementation of the same system."

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

A doc capturing external knowledge: studied games, explored codebases, prior-art surveys, pattern catalogs. Currently scattered (some live in `spikes/`, some in project-root `RESEARCH_BACKUP/`); home TBD — likely `Product/research/` (or `Product/reference/`).

- 🟡 **Research has no resolution path.** Unlike spikes, research isn't aimed at a settled outcome. It's reference material; consulted, not resolved.
- 🟡 **Models / features cite research** when a design decision drew on it. Research is the source the canonical doc references; the canonical doc is what authors and AI consult day-to-day.
- 🟡 **Research can be revisited and refined.** A second pass on the same game with new context appends or rewrites in place — not archived as superseded.
- 🟡 **Spike vs research test.** Does the doc have a "when this lands, X gets settled" pointer? Yes → spike. No → research. (A doc that started as a spike but no longer has a resolution path has drifted into research; reclassify.)
- 🔴 **Canonical home for research** TBD. Working candidates: `Product/research/<topic>.md` (per-topic), `Product/research/games/<game>.md` (per-game studies), `Product/research/patterns/<pattern>.md` (pattern catalogs).
- 🔴 **What about `RESEARCH_BACKUP/` outside Product?** Some research already lives there, outside the Product workflow entirely. Decide: import into `Product/research/`, link to from Product, or leave where it is.

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

- **`engine/06` (Match Lifecycle System).** Defines the lifecycle FSM, the gate rules, and the framework-required implementation that realizes them (commands, handlers, signals, `MatchOutcome`). Multiple consumers reference it: `engine/01` for the gate, `engine/04` for `EngineLifecycle`, effects-cancellation question, presentation observers. *Why it earns:* defines a coherent system; implementations live with the system because they realize it.
- **`engine/01` (Game Engine).** Defines the `GameEngine` class — the orchestrator. Loop, queue, drain, `Dispatch`, lifecycle-method sugar. *Why it earns:* canonical home for `GameEngine`, no other doc owns the class.
- **`engine/03` (Snapshot Production).** Defines `IAvailableActions` service and the `AvailableActionsState` slice. *Why it earns:* canonical home for the production service. (The snapshot *shape* lives in `actions/02` — different system.)
- **`engine/05` (Game Factory).** Defines `IGameFactory`, `MatchSetup`, `IMatchBuilder`, the two-layer DI split. *Why it earns:* canonical home for match construction.
- **`actions/01` (Command Pipeline).** Defines the action-flow vocabulary (`PlayerIntent`, `GameCommand`, `CommandHandler`, `CommandResult`, `HistoryEnvelope`) and now the `HistoryState` slice. *Why it earns:* canonical home for the command-pipeline contract.

### Model — does not earn its file ❌

- **`effects/02-replacement-prevention` (current state).** Composes `OnBefore<T>` (canonical in `triggers/01`) + `IEffectScope` (canonical in `effects/04`). The "three before-fold outcomes" is a *rule* about how the existing pipeline composes, not a new surface. *Verdict:* folds into `effects.md` feature root.
- **`effects/03-ongoing-effects` (current state).** Author explicitly says: *"No new pipeline concept is needed — the attachment uses the same entity, trigger, and action machinery as everything else."* Recipe over existing surfaces. *Verdict:* folds into `effects.md` feature root.
- **Pre-refactor `engine/06` (Match Lifecycle Types).** Before reframing, the doc mixed canonical types with method-extensions to `GameEngine` (defined in `engine/01`) and gate behavior (a rule about `Dispatch`). The leaks belonged in `engine/01`; the patterns (setup, disposal split) belonged in `engine.md`. *Outcome:* leaks moved out, doc reframed as the "lifecycle system" and earned its file under that framing.

### Feature — earns its file ✅

- **`state` (feature-root only).** Integrates Scaffold.State as the framework's state primitive. No models — the feature root is the canonical doc. *Why it earns:* load-bearing infrastructure with no canonical home before; consumers across every other feature reference it.
- **`engine`.** Multi-model feature with a feature root that carries synthesis (vocabulary, architecture spanning seven models, cross-model invariants), plus recipes/conventions (setup commands, disposal split). *Why it earns:* coherent capability; feature root is a real synthesis, not a TOC.

### Feature — split candidate 🔴

- *(none surfaced yet — entry kept open for when one appears)*

### Spike → Model promotion ✅

- **`spikes/effect-pipeline.md`.** Currently exploring effect-level orchestration (`oq-effect-pipeline`). Carries a "When this lands" pointer at where the canonical model would live. *Why it's a good spike:* explicit resolution path, scoped to one open question.

### Spike that's actually research 🔴

- *(survey TBD — likely candidates: any doc in `spikes/` or `RESEARCH_BACKUP/` that captures studied games / patterns / codebases without a "when this lands" pointer. These should reclassify to `Product/research/` once that home is decided.)*

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
- **Research home.** `Product/research/` not yet established. Pick a structure (per-topic? per-game? per-pattern?) and audit `spikes/` + `RESEARCH_BACKUP/` for content that should reclassify.

---

## Promotion target

When this draft stabilizes, the rules promote into `_workflow/PRODUCT-BUILDER.md`. Each section above maps to a corresponding section in the canonical workflow doc; the open-questions section either resolves (rule locks) or graduates to a separate "design pass needed" entry.
