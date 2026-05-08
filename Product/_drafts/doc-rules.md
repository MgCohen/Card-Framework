---
type: draft
status: in-flight
tags: [#workflow, #rules]
---

# Doc Rules — draft

AI-first rules governing each doc type in the Product corpus. Written so an agent authoring or reviewing docs can apply them mechanically.

Promotion target: `_workflow/PRODUCT-BUILDER.md`. When stable, this lands there.

## Section structure

Each doc type below follows the same shape:

- **Definitions** — vocabulary used by the section.
- **Identity** — substantive criteria for the decision "is this a [type]?". Drives classification, not formatting.
- **Forbidden** — what disqualifies the doc, both identity-wise and convention-wise.
- **Decision flow** — explicit walk through identity criteria.
- **Conventions** — formatting / placement / structure that applies *once* a doc is classified as this type. These don't bear weight on the decision.
- **Anti-pattern checklist** — review-mode tickbox items.
- **Heuristics** — judgement calls; default behavior stated.
- **Patterns** — abstract, timeless ✅/❌ shapes.
- **Anchor cases** — small set of stable real corpus references.

**Per-rule status (inline emoji):** 🟢 locked · 🟡 draft · 🔴 open.

---

# Doc Type: Model

The unit of canonical design. One model = one central element explained in depth.

## Definitions

- **central element** — the one concrete thing the model is about (class, interface, service, contract, structured type).
- **load-bearing** — has its own behavior (methods, logic, runtime semantics) **OR** substantial design space (authoring contract, evolution rules, integration story, multi-consumer needs, validation/serialization concerns).
- **support** — content inside a model existing for the central element (sub-types, payloads, helpers, mechanics).
- **canonical home** — the one place where an element is first defined.

## Identity (decision criteria)

- 🟢 The model has exactly one central element.
- 🟢 The central element is concrete (class, interface, service, contract, structured type).
- 🟢 The central element is load-bearing (behavior **OR** substantial design space).
- 🟢 The model is the central element's canonical home — first-time definition.

## Forbidden

- 🟢 Title is a concept (lifecycle, validation, loop, pipeline, system) without a concrete central element behind it.
- 🟢 Multiple peer central elements (→ that's a feature, not a model).
- 🟢 Central element is data **with no design space** — bare enum, fieldless signal, simple payload that's just shape, single-purpose value type.
- 🟢 Defines rules / behaviors / extensions of an element defined elsewhere (→ goes to that element's canonical home).
- 🟢 Recipe / pattern composing existing surfaces (→ feature root).
- 🟢 Method extension to a class defined elsewhere (→ class's canonical home).

## Decision flow

When asking "does X earn its own model?":

1. **Identify the candidate central element.**
   - No single concrete element → **NOT A MODEL.** Escalate to feature, or fold into existing model.
2. **Check load-bearing.**
   - Behavior present (methods, logic, runtime semantics) → continue.
   - Design space present (authoring contract, evolution, integration, multi-consumer concerns) → continue.
   - Neither → **NOT A MODEL.** Place as support inside the model that defines or uses it.
3. **Independence check (soft rule; default = support):**
   - Always reached via parent + small + no design depth → **SUPPORT.**
   - Substantial design depth **OR** independent consumers **OR** own supports → **MODEL.**

## Conventions (once decided)

- 🟢 Lives at `Product/features/<feature>/models/<NN>-<slug>.md`.
- 🟢 Filename uses a numbered prefix (`01`, `02`, ...) within the feature.
- 🟢 Frontmatter: `type: model`, `status:`, `references:`, `tags:`.
- 🟢 Title names the central element (`# <Element Name> — model`).
- 🟢 Body sections (per the model template): What it is, Contracts, Invariants, Decisions, Examples, Open follow-ups, Options considered, Rationale, Consequences.
- 🟢 Cross-references use shorthand (`features/engine/models/01` or `engine/01`).

## Anti-pattern checklist (for review)

Flag a doc claimed to be a model if:

- [ ] Title is a concept word (system, lifecycle, loop, validation, pipeline) with no element name attached
- [ ] Body is a list of types tied by theme, no central thing
- [ ] Subject is a single bare enum, fieldless signal, or simple payload record with no design discussion
- [ ] Most content is rules about an element defined in another doc
- [ ] Body says "this composes existing surfaces" or "no new concept needed"
- [ ] Doc body would be `definition + field list` with nothing substantive beyond — no design space

## Heuristics (judgement calls)

- 🟡 **Default = support.** Promotion to model needs a reason: substantial depth, independent consumption, supports forming around the element.
- 🟡 **Test for design space:** would the doc body be substantively more than `definition + field list`? If no, no design space → support.
- 🟡 **Test for independence:** do consumers reach for the element directly, or always via a parent? Direct → promote candidate. Always via parent → support.
- 🟡 **When a model gets too big,** look for sub-elements that could promote (split). If multiple peers without a clear central, you've discovered a feature.
- 🟡 **When models in the same feature feel coupled,** check if they're really one model with separated supports.

## Patterns (abstract, timeless)

✅ **"X class as a model"** — title names a class, body explains responsibilities + sub-types + design choices.

✅ **"X service as a model"** — title names a service interface, body explains its contract methods + write protocol + slice/state managed.

✅ **"X contract as a model"** — title names a typed contract, body explains methods consumers implement + anti-scope + composition.

✅ **"X data type as a model" (when has design space)** — title names a structured type, body explains authoring contract + evolution + integration. Pure-data central element passes when design space justifies a focused doc (e.g., a game-config bundle, a snapshot type with typed sub-templates).

❌ **"X system as a model" without a central service** — collection of types tied by the X theme, no central load-bearing element. Fix: identify the load-bearing element if any (rename), or dissolve into supports.

❌ **"X enum as a model"** — bare enum dedicated doc with no design space. Fix: place enum as support inside the model that uses its parent element.

❌ **"X loop as a model" without a central orchestrator** — describes how multiple existing surfaces interact. Fix: fold into feature root as a recipe; if there's a real orchestrator class, retitle the model after it.

## Anchor cases (stable real examples)

- ✅ `engine/01` — central: `GameEngine` class. Behavior + design space.
- ✅ `engine/03` — central: `IAvailableActions` service. Behavior + design space.
- ✅ `engine/05` — central: `IGameFactory`. Behavior + design space.
- ✅ `engine/07` — central: `GameDefinition` bundle. Pure data, but rich design space (Build walk, `ISliceProvider`, layout blocks).
- ✅ `actions/02` — central: `AvailableActionsSnapshot` + `ActionTemplate` hierarchy. Pure data, but rich design space.
- ✅ `cards/01` — central: `Card` class. Behavior + design space.
- 🔵 `EngineLifecycle` enum — pure data, no design space → support, never a model.
- 🔵 `MatchOutcome` hierarchy — pure data, small design space, always reached via `EndMatchCommand` → support by soft rule.

---

# Doc Type: Feature

The capability-level entry point. May or may not have models beneath it.

## Definitions

- **feature root** — the `<feature>.md` file at the feature folder root.
- **synthesis** — real explanation of how the feature's models compose; cross-model invariants; integration narrative. Not a TOC.

## Identity (decision criteria)

- 🟢 Represents a coherent capability with its own name (a thing the framework offers, not just a grouping of unrelated stuff).
- 🟢 Either has multiple models worth integrating, OR documents an external system / package the framework depends on.
- 🟢 Has identifiable consumers (other features, game authors, runtime, presentation).

## Forbidden

- 🟡 Feature root is just a TOC pointing at models without integration narrative.
- 🟢 Feature has multiple models with no cross-model invariants or composition story (→ they're not really one feature).
- 🟡 Feature root restates content already in models (link, don't duplicate).
- 🟡 Single-concern that fits inside an existing feature — should fold, not stand alone.

## Decision flow

When asking "does X earn its own feature?":

1. **Is X a coherent capability with a name?**
   - No → fold into an existing feature.
2. **Does X need multiple models, OR integrate an external system?**
   - Yes → feature.
   - No → likely a single model under an existing feature.
3. **Are there real consumers across other features?**
   - Yes → feature.
   - No → reconsider; might be internal to one feature.

## Conventions (once decided)

- 🟢 Lives at `Product/features/<feature>/<feature>.md` (root file matches folder name).
- 🟢 Frontmatter: `type: feature`, `status:`, `references:`, `tags:`.
- 🟢 Body sections: What it is, Vocabulary, Architecture (synthesis), Invariants, Boundaries, Dependencies, Open.
- 🟢 Vocabulary section lists feature-internal terms only — cross-feature terms link to canonical home or `GLOSSARY.md`.
- 🟢 Models live under `Product/features/<feature>/models/` with numbered prefix.

## Anti-pattern checklist (for review)

- [ ] Feature root is mostly bullet points pointing at models, no narrative
- [ ] Multiple models exist but no shared invariants, no architecture diagram, no composition story
- [ ] Vocabulary table restates terms defined elsewhere
- [ ] Feature has only one model and the root is redundant with it

## Heuristics

- 🟡 **Feature-root-only is allowed** for infrastructure / package-integration features. No models needed if the feature root carries the whole story.
- 🟡 **A feature with one model** keeps both files separate. Feature root carries synthesis; model carries depth. If the synthesis would be redundant, reconsider the feature.
- 🟡 **Consider splitting** when models cluster around two distinct capabilities consumers reference separately.

## Patterns

✅ **"Capability with multiple models"** — feature root provides synthesis + cross-model invariants; each model owns one central element.

✅ **"Infrastructure integration feature"** — feature-root-only; documents how the framework uses an external package/library/system. No models needed unless a framework-specific extension emerges.

❌ **"TOC-only feature root"** — root just lists models without explaining how they compose. Fix: write the synthesis or fold the feature.

❌ **"Feature for a single model"** — when the feature root and the lone model would say nearly the same thing, the feature is over-stated. Fix: fold the model into a sibling feature or expand the synthesis into something distinct.

## Anchor cases

- ✅ `engine` — multi-model feature, rich feature-root synthesis spanning seven models, cross-model invariants, recipes (setup, disposal split).
- ✅ `state` — feature-root-only; documents Scaffold.State integration end-to-end. Sets the precedent that infrastructure features can ship without models.

---

# Doc Type: Spike

Pre-settled exploration with a planned outcome.

## Definitions

- **resolution path** — explicit "when X happens, this folds into Y" pointer or `forward-link:` to the target home.

## Identity (decision criteria)

- 🟢 Explores an open design question.
- 🟢 Has a planned outcome — fold into model(s) or be abandoned.
- 🟢 Has a resolution path naming where the content goes when resolved.

## Forbidden

- 🟢 No resolution path → it's drifting toward research; reclassify.
- 🟡 Spike content fully folded into model(s) but file still in `spikes/` — should archive or delete (TBD).

## Decision flow

When deciding "spike or research?":

1. **Does the doc have a planned outcome?**
   - Yes → **spike.**
   - No → **research.**
2. **Drifted spike** (originally spike, no longer has resolution path) → reclassify to research.

When a spike's lifecycle ends:

- Status `concluded` / `accepted` / `graduated` → content folded into model(s); file disposition TBD.
- Status `superseded` → newer spike or model replaces it; file disposition TBD.

## Conventions (once decided)

- 🟢 Lives under `Product/spikes/` with a descriptive filename (no numbered prefix).
- 🟢 Frontmatter: `type: spike`, `status:` (`draft` / `open` / `concluded` / `accepted` / `superseded`), `tags:`.
- 🟢 Carries either a `forward-link:` field or a "When this lands" section pointing at the target.

## Anti-pattern checklist (for review)

- [ ] Frontmatter `type: spike` but no resolution pointer anywhere
- [ ] Status is `concluded` but no link to where the content folded
- [ ] Body reads as a knowledge-bank survey (game / library study) — likely research

## Heuristics

- 🟡 Spikes are ephemeral — once concluded, fold content into model(s) or abandon.
- 🟡 If a spike's resolution path disappears (the open question turns out not to need a settled outcome), it's drifted into research → reclassify.

## Patterns

✅ **"Active design exploration with resolution"** — spike doc carrying a design pass in progress + "when this lands, target is X."

❌ **"Codebase / library / game survey with no resolution path"** — that's research; reclassify to `Product/research/`.

## Anchor cases

- ✅ `spikes/effect-pipeline.md` — active design exploration with `oq-effect-pipeline` resolution pointer.

---

# Doc Type: Research / Reference

Captured external knowledge: studied games, codebase analyses, prior-art surveys, pattern catalogs.

## Definitions

- **research** — reference material consulted, not resolved. No planned outcome.

## Identity (decision criteria)

- 🟢 Captures external knowledge (games, codebases, libraries, patterns) for later consultation.
- 🟢 No resolution path / planned outcome.
- 🟢 Can be revisited and refined in place — not archived as superseded.

## Forbidden

- 🟢 Has a "when this lands, X gets settled" pointer → it's a spike, reclassify.
- 🟢 Lives outside `Product/research/` (e.g., project-root research dirs) — should import or delete.

## Decision flow

When asking "research or spike?":

1. **Does the doc have a planned outcome?**
   - No → **research.**
   - Yes → **spike.**
2. **Drifted spike** (started as spike, no longer has resolution path) → reclassify to research.

## Conventions (once decided)

- 🟢 Lives under `Product/research/` with a descriptive filename.
- 🟢 Frontmatter: `type: research`. **No `status:` field** (research isn't a workflow item that ends).
- 🟢 Tagged with `#research` plus a topic tag (`#prior-art`, `#pattern`, `#codebase`).

## Anti-pattern checklist (for review)

- [ ] Doc under `spikes/` but body is a survey of an external library/game with no design question
- [ ] Frontmatter has `type: research` but also `status: concluded` (drop the status)
- [ ] Doc lives outside `Product/research/`

## Heuristics

- 🟡 Models cite research when a design decision drew on it. Research is the source; the model is canonical for usage.
- 🟡 A second pass on the same topic appends or rewrites in place — don't archive prior-pass research as superseded.

## Patterns

✅ **"Codebase / library study"** — `research/<library-or-game>-architecture.md`.

✅ **"Pattern catalog"** — `research/patterns/<pattern>.md` (no current examples; structure pre-empts).

✅ **"Game design study"** — `research/games/<game>-mechanics.md` (no current examples).

❌ **"External survey labeled as a spike"** — fix: reclassify, drop status field, update tags.

## Anchor cases

- ✅ `research/sabberstone-architecture.md`, `research/fireplace-architecture.md`, `research/brimstone-architecture.md` — codebase studies of Hearthstone simulators.
- ✅ `research/deckmech-command-pipeline.md` — external command-pipeline reference that fed `features/actions/`.

---

# Doc Type: Draft

In-flight content that would churn its canonical home if iterated there.

## Definitions

- **target home** — explicit destination where the draft lands when stable.

## Identity (decision criteria)

- 🟢 Target shape is unstable; multiple iterations expected before stabilizing.
- 🟢 Iterating directly at the canonical home would churn it; the draft amortizes the iteration.
- 🟢 Has a named target home where the content will land once stable.

## Forbidden

- 🔴 Stale draft (no movement in N sessions) — surface for "promote, abandon, or rewrite." (N TBD.)
- 🟡 Draft becomes its own permanent home — promote when stable; don't let drafts replace canonical docs.

## Decision flow

When deciding "draft or direct edit?":

1. **Is the target shape unstable?**
   - Yes → **draft.** Iterate without churning canonical doc.
   - No → direct edit at target home.
2. **Multiple iterations expected before stability?**
   - Yes → **draft.**
   - No → direct edit.

## Conventions (once decided)

- 🟢 Lives under `Product/_drafts/` with a descriptive filename.
- 🟢 Frontmatter: `type: draft`, `status: in-flight`.
- 🟢 States its target home explicitly in body (near top).
- 🟢 Promotes in one commit when stable; original draft file is deleted.

## Anti-pattern checklist (for review)

- [ ] Draft has no stated target home
- [ ] Draft hasn't moved in many sessions (stale)
- [ ] Content stable but still in `_drafts/` (should have promoted)

## Heuristics

- 🟡 Drafts amortize iteration — iterate freely, promote in one commit when stable.
- 🟡 Drafts can carry meta-content (open questions about the draft itself) that wouldn't belong at the target home.

## Patterns

✅ **"Multi-iteration design that would churn the canonical doc"** — start as draft, promote when stable.

❌ **"One-edit change drafted instead of edited"** — overhead without benefit; just edit.

## Anchor cases

- ✅ `_drafts/doc-rules.md` — this very draft. Target: `_workflow/PRODUCT-BUILDER.md`.

---

# Doc Type: Decision (ADR)

Project-wide or cross-feature decision history.

## Identity (decision criteria)

- 🟢 Records a decision with cross-feature reach (multiple features cite it or are constrained by it).
- 🟢 Captures the alternatives considered + the chosen option + rationale.
- 🟢 Decision is durable — append-only history, not iterated content.

## Forbidden

- 🟢 Single-feature decision in `decisions/` — folds into the model's `## Options considered` instead.
- 🟢 Rewriting an accepted ADR's body — supersede with a new ADR, don't rewrite.

## Decision flow

When asking "ADR or fold-into-model?":

1. **Cross-feature reach?** (Multiple features cite the decision or are constrained by it.)
   - Yes → **ADR.**
   - No → fold into the model that owns the affected surface.

## Conventions (once decided)

- 🟢 Lives under `Product/decisions/` with sequential numbering (`0001-<slug>.md`, `0002-<slug>.md`, ...).
- 🟢 Frontmatter: `type: adr`, `status:` (`proposed` / `accepted` / `superseded`).
- 🟢 Append-only — status flips, history isn't rewritten.

## Anti-pattern checklist (for review)

- [ ] ADR is referenced from only one feature (likely should fold into model)
- [ ] Body rewrites a previously accepted decision (should supersede instead)
- [ ] Numbering gap or non-sequential (renumber)

## Heuristics

- 🟡 If unsure, default to fold-into-model. Easier to elevate to ADR later than to demote.

## Patterns

✅ **"Cross-feature architectural decision"** — affects how multiple features compose; lands as standalone ADR.

❌ **"Single-model design choice as ADR"** — fold into the model's `## Options considered`.

## Anchor cases

- ✅ ADR-0006 (two-pipeline architecture) — engine + actions + effects.
- ✅ ADR-0007 (five validation layers) — engine + actions + presentation.
- 🔵 Match-lifecycle decisions — single-feature; folded into model provenance, not an ADR.

---

# Doc Type: Glossary

Cross-feature term definitions.

## Identity (decision criteria)

- 🟢 Defines a term used by multiple features.
- 🟢 Each entry is a one-line definition + canonical-home pointer (no restated prose).

## Forbidden

- 🟢 Restates prose from feature/model docs — link to canonical home instead.
- 🟢 Feature-internal terms — those live in the feature's vocabulary table.
- 🟡 Section anchors using volatile numeric refs (e.g., `§4.1`) — use section names instead.

## Conventions (once decided)

- 🟢 One file: `Product/ledgers/GLOSSARY.md`.
- 🟢 Each entry: `| term | one-line definition + canonical-home pointer |`.
- 🟢 Cross-references use stable section names, not numeric anchors.

## Anti-pattern checklist (for review)

- [ ] Glossary entry restates prose from a feature/model doc
- [ ] Glossary entry has no canonical-home pointer
- [ ] Glossary entry uses `§N.M` numeric anchor (volatile)

## Anchor cases

- ✅ `EngineLifecycle` glossary entry — one-line + pointer to `Match Lifecycle §Lifecycle states`.

---

# Doc Type: Ledger

Registry/index files maintained as event-driven indices.

## Identity (decision criteria)

- 🟢 Indexes or registers things that exist elsewhere — doesn't carry original content.
- 🟢 Updated event-driven (model lands, feature ships, dependency shifts, item resolves).
- 🟢 Each ledger has a single, named purpose (feature index, open-items log, term glossary, etc.).

## Forbidden

- 🟢 OPEN-ITEMS entry resolves but isn't deleted — resolution = delete (the resolving doc owns the answer).
- 🟡 Ledger restates content that lives elsewhere — index, don't duplicate.

## Conventions (once decided)

- 🟢 Lives under `Product/ledgers/` with a descriptive ALL-CAPS filename (`FEATURE-MAP.md`, `OPEN-ITEMS.md`, `GLOSSARY.md`).
- 🟢 Hand-maintained at v1; auto-generated at v2 (TBD trigger ~15 models).
- 🟢 Front-matter `type: ledger` (or `feature-map`, `glossary` for specific kinds).

## Anti-pattern checklist (for review)

- [ ] Resolved OPEN-ITEMS entries left in place
- [ ] Ledger duplicates content from a feature/model
- [ ] Ledger out of date relative to recent feature/model landings

## Anchor cases

- ✅ `FEATURE-MAP.md` — bird's-eye index of features, dependency graph, model breakdown.
- ✅ `OPEN-ITEMS.md` — single ledger of loose ends; resolution = delete.
- ✅ `GLOSSARY.md` — cross-feature term definitions.

---

# Open questions about the rules themselves

- 🔴 **Cross-cutting "system" identification.** When a system spans multiple features (e.g. lifecycle = engine + state + actions), which feature owns the central element? Currently: the feature most central to the system's purpose.
- 🔴 **Concluded design spikes.** Several spikes (`entities-bridge-replacement`, `entities-state-unification`, `scaffold-state-fit`, `domain-model-proposal`) are folded into models. Archive (`_archive/`), delete (history in git), or leave (status field carries the state)?
- 🔴 **Stale-draft policy.** N-session threshold for surfacing drafts that haven't moved.
- 🔴 **Auto-generation trigger** for FEATURE-MAP.
- 🔴 **Where do historical rule applications live?** Things like "engine/06 was reclassified on 2026-05-08" don't belong in this rules doc (it's timeless). Candidates: a `RULE-APPLICATIONS.md` ledger, append to `SESSION-CHECKPOINT.md`, or fold into commit messages only.

---

# Promotion target

When stable, this draft promotes into `_workflow/PRODUCT-BUILDER.md`. Each section maps to a corresponding section in the canonical workflow doc; the open-questions section either resolves (rule locks) or graduates to a separate "design pass needed" entry.
