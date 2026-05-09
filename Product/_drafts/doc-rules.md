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

- **Identity** — substantive criteria for the decision "is this a [type]?". Each rule states the positive form + its failure mode inline. Drives classification, not formatting.
- **Decision flow** — stepwise walk for applying the criteria.
- **Conventions** — formatting / placement / structure that applies *once* a doc is classified as this type. Doesn't bear weight on the decision.
- **Anti-patterns** *(Model only, currently)* — concrete failure shapes to recognise and reject before they ship.
- **Heuristics** — judgement calls / soft rules; default behavior stated.

**Per-rule status (inline emoji):** 🟢 locked · 🟡 draft · 🔴 open.

## Audit obligation

These rules apply at creation **and** retroactively. When an Identity rule changes, every doc of that type is re-checked against the new wording. Rules that exist only at creation time drift; rules that gate ongoing existence stay current. Audits are the cheapest moment to catch drift; promotion of a rule from draft → locked triggers a mandatory audit pass over existing docs of that type.

---

# Doc Type: Model

The unit of canonical design. One model = one central element explained in depth.

## Identity (decision criteria)

- 🟢 **Has exactly one central element.** Exactly one. **The "and" test:** if the element description uses "X and Y" as peers (e.g. "GameRulesDefinition and GameRulesRuntime", "Entity, Definition, And Instance"), that is two (or more) models, not one. Fails if: title is a concept (lifecycle, validation, loop, pipeline, system, surface, contract) without a concrete thing behind it; multiple peer elements tied only by a theme (= feature, not model); a blob of related types presented together because they share a folder.
- 🟢 **Central element is a named C# type** — a class, interface, record, struct, payload, service, or other concrete declared type. **Point-at-the-type test:** name the type. If you cannot, it is not a model. Fails if: subject is a pattern, mechanism, process, sub-step of a larger flow, phase of a lifecycle, behavior, relationship, convention, or usage recipe of an existing type. Examples that are *not* models even if load-bearing: "before-fold" (a step inside `ITriggerPipeline.RunBefore`), "decision-routing" (a behavior of `ICommandAwaiter`), "attachment-entity" (a usage pattern of `EntityDefinition`), "async dispatch" (a property of `IEffectScope.Dispatch`), "evaluation loop" (a process the `GameEngine` runs), "snapshot pipeline" (a process producing `AvailableActionsSnapshot`), "framework slice tier" (a convention over `Store`), "runtime surface" (a collection of types). Each belongs as content inside the type that owns it.
- 🟢 **Central element is load-bearing** — has its own behavior (methods, logic, runtime semantics) **OR** substantial design space (authoring contract, evolution rules, integration story, multi-consumer needs, validation/serialization concerns). Fails if: bare enum, fieldless signal, simple payload that's just shape, single-purpose value type with no design weight. Place those as support inside the model that defines or uses them.
- 🟢 **Body shape is model work** — focused on one central element with its supports. Fails if: body reads as feature-root work (synthesis / architecture / vocabulary across multiple peer elements) **OR** contains ADR-shaped content (options A/B/C considered + rationale + consequences for a cross-feature decision). Those are other doc types masquerading as a model — split synthesis to feature root, decision to ADR.
- 🟢 **The model is the central element's canonical home.** Fails if: defines rules / behaviors / extensions of an element defined elsewhere (→ that element's home), recipe composing existing surfaces (→ feature root), method extension to a class defined elsewhere (→ class's home).
- 🟢 **Title and filename agree, both name the type.** The H1 title and the filename slug must both resolve to the same C# type name. **Title-filename equality test:** does the title's element name (with conventions — drop `I`, hyphenate camelcase) equal the filename slug? If no, the doc is misclassified or mistitled — re-run the point-at-the-type test before renaming.

## Decision flow

1. **Identify the candidate central element.** Exactly one. If the description uses "X and Y" as peers, stop — that is two models, not one.
2. **Point-at-the-type test.** Can you name the C# class / interface / record / struct / payload / service? Yes → continue. No (it's a pattern, mechanism, sub-step, behavior, or convention) → **NOT A MODEL.** Fold the content into the type that owns it, or into feature root for cross-element synthesis.
3. **Title-filename equality test.** Does the title's element name (drop `I`, hyphenate camelcase) equal the filename slug? Yes → continue. No → mistitled or misclassified — go back to step 1.
4. **Check load-bearing.** Behavior present OR design space present → continue. Pure data with no design space → **NOT A MODEL.** Place as support.
5. **Check body shape.** Body focused on the central element + its supports → continue. Body is synthesis-across-peers + ADR design pass → **NOT A MODEL.** Split into feature root + ADR.
6. **Independence check (soft rule; default = support).** Always reached via parent + small + no design depth → **SUPPORT.** Substantial design depth OR independent consumers OR own supports → **MODEL.**

## Anti-patterns (recognise and reject)

| Smell | What it looks like | Why it fails | Where the content goes |
|---|---|---|---|
| **Process / pipeline / loop / surface / contract title** | "Evaluation Loop", "Snapshot Pipeline", "Runtime Surface", "GameRules Contract" | Title names a process or topic, not a type. Fails point-at-the-type test. | Rename to the type, or dissolve if it's a piece of another type's behavior. |
| **Multi-type bundle** | "GameRulesDefinition and GameRulesRuntime", "Entity, Definition, And Instance Model", title with `&` / `+` / `,` between concepts | Two or more peers presented as one. Fails the "and" test. | Split — one model per type. |
| **Pattern / convention as model** | "Framework Slice Tier", "Cleanup conventions", "Modifier source convention" | Body describes rules/conventions over types defined elsewhere. Fails canonical-home rule. | Feature root (cross-cutting conventions) or the owning type's model. |
| **Bare enum or single value type** | A model whose central element is just an `enum` or a fieldless signal | Fails load-bearing — no design weight, no behavior. | Support inside the model that emits or consumes it. |
| **Title-filename mismatch** | Title says `Game Engine`, filename says `evaluation-loop` | Naming is inconsistent — one of the two is wrong, both should match the type. | Fix the wrong side. If the title is the type and filename is the topic, rename the file. |
| **Recipe composing existing surfaces** | A model that doesn't define a new type but tells you how to use existing ones in combination | Fails canonical-home rule — content belongs at the level that owns the composition. | Feature root, or examples inside the relevant type's model. |

## Conventions (once decided)

- 🟢 Lives at `Product/features/<feature>/models/<NN>-<slug>.md`.
- 🟢 Filename uses a numbered prefix (`01`, `02`, ...) within the feature.
- 🟢 Frontmatter: `type: model`, `status:`, `references:`, `tags:`.
- 🟢 Title names the central element (`# <Element Name> — model`). The element name is the C# type name. Filename slug derives from the type — drop the `I` prefix on interfaces (`ICommandHandler` → `command-handler.md`), keep records and classes verbatim (`HistoryEnvelope` → `history-envelope.md`).
- 🟢 Topic-shaped or process-shaped titles ("decision-routing", "before-fold", "async-dispatch", "lifecycle", "pipeline-mechanics") are misclassifications. If the title is not a type name, the file is either misclassified (not a model) or correctly a model but mistitled. Re-run the point-at-the-type test before renaming.
- 🟢 Body sections (per the model template): What it is, Contracts, Invariants, Decisions, Examples, Open follow-ups, Options considered, Rationale, Consequences.
- 🟢 Cross-references use shorthand (`features/engine/models/01` or `engine/01`).

## Heuristics

- 🟡 **Default = support.** Promotion to model needs a reason: substantial depth, independent consumption, supports forming around the element.
- 🟡 **Piece-of-a-thing trap.** If the candidate is a step inside a larger flow, a phase of a lifecycle, a behavior of a service, or a usage pattern of an existing type, it is a *piece of a thing* — not a thing. Pieces fold into the thing that owns them. Tell: candidate name reads as a verb, gerund, or adjective-noun describing what happens during a larger element's execution.
- 🟡 **Bundle-of-things trap.** If the candidate covers two or more peer types presented together (Definition + Runtime, Entity + Definition + Instance), it is a bundle — split into one model per type. Tell: the description uses "and" / "+" / "/" between concepts, or the title contains a comma.
- 🟡 **Title-content mismatch — fix the title first.** When body has a clear central element but title is process-shaped or concept-shaped, rename the title to name the element. Reclassification is the harder fallback, used only when the body has no central element to name.
- 🟡 **Test for design space:** would the doc body be substantively more than `definition + field list`? If no, no design space → support.
- 🟡 **Test for independence:** do consumers reach for the element directly, or always via a parent? Direct → promote candidate. Always via parent → support.
- 🟡 **Test for body shape:** does the body discuss one element in depth, or synthesize across many? One-in-depth → model. Synthesis-across-many → feature root. Decision-with-options → ADR. A doc doing both feature work and ADR work bundled is two docs.

---

# Doc Type: Feature

The capability-level entry point. May or may not have models beneath it.

## Identity (decision criteria)

- 🟢 **Coherent capability with a name** — a thing the framework offers, not a grouping of unrelated stuff. Fails if: scope is an internal concern of one existing feature.
- 🟢 **Needs multiple models OR integrates an external system.** Fails if: scope is one focused thing — that's a single model under an existing feature, not a feature.
- 🟢 **Has consumers across other features** (other features, game authors, runtime, presentation). Fails if: nothing outside the feature references it — likely should fold.

## Decision flow

1. **Coherent capability with a name?** No → fold into existing feature.
2. **Needs multiple models OR integrates an external system?** No → likely a single model under an existing feature.
3. **Real consumers across other features?** No → reconsider; might be internal to one feature.

## Conventions (once decided)

- 🟢 Lives at `Product/features/<feature>/<feature>.md` (root file matches folder name).
- 🟢 Frontmatter: `type: feature`, `status:`, `references:`, `tags:`.
- 🟢 Body sections: What it is, Vocabulary, Architecture (synthesis), Invariants, Boundaries, Dependencies, Open.
- 🟢 Vocabulary section lists feature-internal terms only — cross-feature terms link to canonical home or `GLOSSARY.md`.
- 🟢 Models live under `Product/features/<feature>/models/` with numbered prefix.

## Heuristics

- 🟡 **Feature-root-only is allowed** for infrastructure / package-integration features. No models needed if the feature root carries the whole story.
- 🟡 **A feature with one model** keeps both files separate. Feature root carries synthesis; model carries depth. If the synthesis would be redundant, reconsider the feature.
- 🟡 **Consider splitting** when models cluster around two distinct capabilities consumers reference separately.

---

# Doc Type: Spike

Pre-settled exploration with a planned outcome.

## Identity (decision criteria)

- 🟢 **Explores an open design question.** Fails if: doc captures knowledge for later consultation without a planned outcome — that's research.
- 🟢 **Has a planned outcome** — fold into model(s) or be abandoned. Fails if: no resolution path named.
- 🟢 **Has a resolution path** naming where the content goes when resolved (a `forward-link:` or "When this lands" pointer).

## Decision flow

1. **Has a planned outcome?** No → research. Yes → spike.
2. **Drifted spike** (started as spike, no longer has resolution path) → reclassify to research.
3. **End-of-life:** status `concluded` / `accepted` / `graduated` / `superseded` → content folded; file disposition (archive / delete / leave) TBD.

## Conventions (once decided)

- 🟢 Lives under `Product/spikes/` with a descriptive filename (no numbered prefix).
- 🟢 Frontmatter: `type: spike`, `status:` (`draft` / `open` / `concluded` / `accepted` / `superseded`), `tags:`.
- 🟢 Carries either a `forward-link:` field or a "When this lands" section pointing at the target.

## Heuristics

- 🟡 Spikes are ephemeral — once concluded, fold content into model(s) or abandon.
- 🟡 If a spike's resolution path disappears (the open question turns out not to need a settled outcome), it's drifted into research → reclassify.

---

# Doc Type: Research / Reference

Captured external knowledge: studied games, codebase analyses, prior-art surveys, pattern catalogs.

## Identity (decision criteria)

- 🟢 **Captures external knowledge** for later consultation. Fails if: doc is internal design content — that's a model, spike, or draft.
- 🟢 **No planned outcome / resolution path.** Fails if: has a "when this lands, X gets settled" pointer — that's a spike, reclassify.
- 🟢 **Can be revisited and refined in place** — not archived as superseded.

## Decision flow

1. **Has a planned outcome?** No → research. Yes → spike.
2. **Drifted spike** (started as spike, no longer has resolution path) → reclassify to research.

## Conventions (once decided)

- 🟢 Lives under `Product/research/` with a descriptive filename.
- 🟢 Frontmatter: `type: research`. **No `status:` field** (research isn't a workflow item that ends).
- 🟢 Tagged with `#research` plus a topic tag (`#prior-art`, `#pattern`, `#codebase`).

## Heuristics

- 🟡 Models cite research when a design decision drew on it. Research is the source; the model is canonical for usage.
- 🟡 A second pass on the same topic appends or rewrites in place — don't archive prior-pass research as superseded.

---

# Doc Type: Draft

In-flight content that would churn its canonical home if iterated there.

## Identity (decision criteria)

- 🟢 **Target shape is unstable;** multiple iterations expected before stabilizing.
- 🟢 **Iterating directly at the canonical home would churn it;** the draft amortizes the iteration.
- 🟢 **Has a named target home** where the content will land once stable.

## Decision flow

1. **Target shape unstable + multiple iterations expected?** Yes → draft. No → direct edit at target.

## Conventions (once decided)

- 🟢 Lives under `Product/_drafts/` with a descriptive filename.
- 🟢 Frontmatter: `type: draft`, `status: in-flight`.
- 🟢 States its target home explicitly in body (near top).
- 🟢 Promotes in one commit when stable; original draft file is deleted.

## Heuristics

- 🟡 Drafts amortize iteration — iterate freely, promote in one commit when stable.
- 🟡 Drafts can carry meta-content (open questions about the draft itself) that wouldn't belong at the target home.
- 🔴 Stale draft (no movement in N sessions) — surface for "promote, abandon, or rewrite." (N TBD.)

---

# Doc Type: Decision (ADR)

Project-wide or cross-feature decision history.

## Identity (decision criteria)

- 🟢 **Records a decision with cross-feature reach** (multiple features cite it or are constrained by it). Fails if: single-feature decision — folds into the model's `## Options considered` instead.
- 🟢 **Captures alternatives + chosen option + rationale.**
- 🟢 **Decision is durable** — append-only history, not iterated content. Fails if: the decision is still being iterated; that's draft territory.

## Decision flow

1. **Cross-feature reach?** Yes → ADR. No → fold into the model that owns the affected surface.

## Conventions (once decided)

- 🟢 Lives under `Product/decisions/` with sequential numbering (`0001-<slug>.md`, `0002-<slug>.md`, ...).
- 🟢 Frontmatter: `type: adr`, `status:` (`proposed` / `accepted` / `superseded`).
- 🟢 Append-only — status flips, history isn't rewritten. Supersede with a new ADR rather than rewriting an accepted one.

## Heuristics

- 🟡 If unsure, default to fold-into-model. Easier to elevate to ADR later than to demote.

---

# Doc Type: Glossary

Cross-feature term definitions.

## Identity (decision criteria)

- 🟢 **Defines a term used by multiple features.** Fails if: term is internal to one feature — lives in that feature's vocabulary table instead.
- 🟢 **One-line definition + canonical-home pointer** — no restated prose.

## Decision flow

1. **Used by multiple features?** Yes → glossary. No → feature vocabulary.

## Conventions (once decided)

- 🟢 One file: `Product/ledgers/GLOSSARY.md`.
- 🟢 Each entry: `| term | one-line definition + canonical-home pointer |`.
- 🟢 Cross-references use stable section names, not numeric anchors (e.g., `§Lifecycle states`, not `§4.1`).

## Heuristics

- 🟡 If a term feels feature-internal but might grow cross-feature later, leave it in feature vocabulary; promote when a second feature actually uses it.

---

# Doc Type: Ledger

Registry/index files maintained as event-driven indices.

## Identity (decision criteria)

- 🟢 **Indexes or registers things that exist elsewhere** — doesn't carry original content.
- 🟢 **Updated event-driven** (model lands, feature ships, dependency shifts, item resolves).
- 🟢 **Has a single, named purpose** (feature index, open-items log, term glossary, etc.).

## Decision flow

1. **Indexes existing content vs. carries new content?** Indexes → ledger. New content → some other doc type.

## Conventions (once decided)

- 🟢 Lives under `Product/ledgers/` with a descriptive ALL-CAPS filename (`FEATURE-MAP.md`, `OPEN-ITEMS.md`, `GLOSSARY.md`).
- 🟢 Hand-maintained at v1; auto-generated at v2 (TBD trigger ~15 models).
- 🟢 Frontmatter `type: ledger` (or `feature-map`, `glossary` for specific kinds).
- 🟢 OPEN-ITEMS: resolution = delete the entry (the resolving doc owns the answer).

## Heuristics

- 🟡 If unsure whether to add to a ledger or write a new doc, the test is "does this point AT existing content?" If yes, ledger. If no, full doc.

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
