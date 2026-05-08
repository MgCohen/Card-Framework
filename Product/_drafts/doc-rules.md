---
type: draft
status: in-flight
tags: [#workflow, #rules]
---

# Doc Rules — draft

AI-first rules governing each doc type in the Product corpus. Written so an agent authoring or reviewing docs can apply them mechanically.

Promotion target: `_workflow/PRODUCT-BUILDER.md`. When stable, this lands there.

**Per-rule status (inline emoji):** 🟢 locked · 🟡 draft · 🔴 open.

---

# Doc Type: Model

`Product/features/<feature>/models/<NN-slug>.md` — the unit of canonical design. One model = one central element explained in depth.

## Definitions

- **central element** — the one concrete thing the model is about (class, interface, service, contract, structured type).
- **load-bearing** — has its own behavior (methods, logic, runtime semantics) **OR** substantial design space (authoring contract, evolution rules, integration story, multi-consumer needs, validation/serialization concerns).
- **support** — content inside a model existing for the central element (sub-types, payloads, helpers, mechanics).
- **canonical home** — the one place where an element is first defined.

## Required (must hold)

- 🟢 The model has exactly one central element.
- 🟢 The central element is concrete (class, interface, service, contract, structured type).
- 🟢 The central element is load-bearing (behavior **OR** substantial design space).
- 🟢 The model's title names the central element.
- 🟢 The model is the central element's canonical home.

## Forbidden (must not hold)

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
3. **Check title match.**
   - Title doesn't name the central element → rename, or split content until they match.
4. **Independence check (soft rule; default = support):**
   - Always reached via parent + small + no design depth → **SUPPORT.**
   - Substantial design depth **OR** independent consumers **OR** own supports → **MODEL.**

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

`Product/features/<feature>/<feature>.md` — the capability-level entry point. May or may not have models beneath it.

## Definitions

- **feature root** — the `<feature>.md` file at the feature folder root.
- **synthesis** — real explanation of how the feature's models compose; cross-model invariants; integration narrative. Not a TOC.

## Required

- 🟢 Lives at `Product/features/<feature>/<feature>.md`.
- 🟢 Frontmatter `type: feature`.
- 🟢 Root contains: what-it-is, vocabulary, architecture (synthesis), invariants, boundaries, dependencies, open.
- 🟢 Vocabulary section lists feature-internal terms only — cross-feature terms link to canonical home.

## Forbidden

- 🟡 Feature root is just a TOC pointing at models without integration narrative.
- 🟢 Feature has multiple models with no cross-model invariants or composition story.
- 🟡 Feature root restates content already in models (link, don't duplicate).

## Decision flow

When asking "does X earn its own feature?":

1. **Is X a coherent capability?** Cross-cutting code or a single concern with a name.
   - No → fold into an existing feature.
2. **Does X need multiple models OR integration of external surfaces?**
   - Yes → feature.
   - No → likely a single model under an existing feature.
3. **Are there real consumers across other features?**
   - Yes → feature.

## Heuristics

- 🟡 **Feature-root-only is allowed** for infrastructure / package-integration features. No models needed if the feature root carries the whole story.
- 🟡 **A feature with one model** keeps both files separate. Feature root carries synthesis; model carries depth.
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

`Product/spikes/<descriptive-slug>.md` — pre-settled exploration with a planned outcome.

## Definitions

- **resolution path** — explicit "when X happens, this folds into Y" pointer or `forward-link:` to the target home.

## Required

- 🟢 Lives under `Product/spikes/`.
- 🟢 Frontmatter `type: spike`.
- 🟢 Has a resolution path — names where the doc's content lands when resolved.
- 🟢 Has a `status:` field tracking lifecycle (`draft` / `open` / `concluded` / `accepted` / `superseded`).

## Forbidden

- 🟢 No resolution path → it's drifting toward research; reclassify.
- 🟡 Spike content fully folded into model(s) but file still in `spikes/` — should archive or delete (TBD).

## Decision flow

When deciding "spike or research?":

1. **Does the doc explore an open design question with a planned outcome?**
   - Yes → **spike.**
2. **Does the doc capture knowledge for later consultation, with no settled outcome aimed at?**
   - Yes → **research.**

When a spike's lifecycle ends:

- Status `concluded` / `accepted` / `graduated` → content folded into model(s); file disposition TBD.
- Status `superseded` → newer spike or model replaces it; file disposition TBD.

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

`Product/research/<descriptive-slug>.md` — captured external knowledge: studied games, codebase analyses, prior-art surveys, pattern catalogs.

## Definitions

- **research** — reference material consulted, not resolved. No planned outcome.

## Required

- 🟢 Lives under `Product/research/`.
- 🟢 Frontmatter `type: research`. **No `status:` field** (research isn't a workflow item that ends).
- 🟢 Tagged with `#research` plus a topic tag (`#prior-art`, `#pattern`, `#codebase`).

## Forbidden

- 🟢 Has a "when this lands, X gets settled" pointer → it's a spike, reclassify.
- 🟢 Lives outside `Product/research/` (e.g., project-root research dirs) — should import or delete.

## Decision flow

When asking "research or spike?":

1. **Does the doc have a resolution path?**
   - Yes → **spike.**
   - No → **research.**
2. **Drifted spike** (originally spike, no longer has resolution path) → reclassify to research.

## Heuristics

- 🟡 Research can be revisited and refined in place — not archived as superseded.
- 🟡 Models cite research when a design decision drew on it. Research is the source; the model is canonical for usage.

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

`Product/_drafts/<descriptive-slug>.md` — in-flight content that would churn its canonical home if iterated there.

## Definitions

- **target home** — explicit destination where the draft lands when stable.

## Required

- 🟢 Lives under `Product/_drafts/`.
- 🟢 Frontmatter `type: draft`, `status: in-flight`.
- 🟢 States its target home explicitly (in body, near top).

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

`Product/decisions/<NNNN>-<slug>.md` — project-wide or cross-feature decision history.

## Required

- 🟢 Lives under `Product/decisions/` with sequential numbering (`0001-...`, `0002-...`).
- 🟢 Frontmatter includes `status:` (`proposed` / `accepted` / `superseded`).
- 🟢 Append-only — status flips, history isn't rewritten.

## Forbidden

- 🟢 Single-feature decision in `decisions/` — folds into the model's `## Options considered` instead.
- 🟢 Rewriting an accepted ADR's body — supersede with a new ADR instead.

## Decision flow

When asking "ADR or fold-into-model?":

1. **Cross-feature reach?** (Multiple features cite the decision.)
   - Yes → **ADR.**
   - No → fold into the model that owns the affected surface.

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

`Product/ledgers/GLOSSARY.md` — cross-feature term definitions.

## Required

- 🟢 One file: `ledgers/GLOSSARY.md`.
- 🟢 Each entry: term + one-line definition + canonical home pointer.
- 🟢 Cross-feature terms only.

## Forbidden

- 🟢 Restating prose from feature/model docs — link to canonical home instead.
- 🟢 Feature-internal terms — those live in the feature's vocabulary table.
- 🟡 Section anchors using volatile numeric refs (e.g., `§4.1`) — use section names instead.

## Anchor cases

- ✅ `EngineLifecycle` glossary entry — one-line + pointer to `Match Lifecycle §Lifecycle states`.

---

# Doc Type: Ledger

`Product/ledgers/<NAME>.md` — registry/index files (`FEATURE-MAP.md`, `OPEN-ITEMS.md`, `GLOSSARY.md`).

## Required

- 🟢 Lives under `Product/ledgers/`.
- 🟢 Hand-maintained at v1; auto-generated at v2 (TBD trigger ~15 models).
- 🟢 Update cadence is event-driven (model lands, feature ships, dependency shifts, item resolves).
- 🟢 OPEN-ITEMS: resolution = delete the entry (the resolving doc owns the answer).

## Forbidden

- 🟢 OPEN-ITEMS entry resolves but isn't deleted.
- 🟡 Ledger restates content that lives elsewhere — index, don't duplicate.

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
