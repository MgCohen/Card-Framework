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

## Examples (within models)

Worked code traces inside model bodies.

- 🟡 **Examples exercise the canonical surface,** typically through implementation. Cross-doc examples are fine — an example in model A using methods defined in model B is valid because examples *demonstrate composition*, they don't define surfaces.
- 🟡 **Pile Counter is the canonical reference example** — full trace lives at `support/samples/pile-counter.md`; other models reference it for end-to-end flow.
- 🔴 **When does an example earn its own doc** (vs. living inline in a model)? Working principle: when it's referenced from multiple models or when it's substantial enough to dwarf the model body.

---

## Open questions about the rules themselves

- **Cross-cutting "system" identification.** When a system spans multiple features (e.g. lifecycle = engine + state + actions), which feature owns the system model? Currently: the feature most central to the system's purpose (lifecycle → engine, even though state/EngineState slice is involved).
- **Boundary between feature root and model when a feature has only one model.** Should the model fold into the feature root? Currently: keep separate so the feature root stays a synthesis layer and the model carries the design.
- **What about content that's framework-convention but doesn't belong to any feature?** e.g. naming conventions, file layout, the canonical-home rule itself. Lives in `_workflow/PRODUCT-BUILDER.md`.

---

## Promotion target

When this draft stabilizes, the rules promote into `_workflow/PRODUCT-BUILDER.md`. Each section above maps to a corresponding section in the canonical workflow doc; the open-questions section either resolves (rule locks) or graduates to a separate "design pass needed" entry.
