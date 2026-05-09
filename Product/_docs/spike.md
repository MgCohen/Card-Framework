---
type: doc-rules
target: spike
status: accepted
references: [_workflow/PRODUCT-BUILDER.md, _docs/model.md]
tags: [#workflow, #rules, #spike]
---

# Spike — rulebook + skeleton

Pre-settled exploration with a planned outcome. A time-boxed investigation — pre-design exploration. Read prior art, probe a technical question, sketch a possible direction.

**Spike vs. model.** A spike is *investigation* (open-ended; we don't yet know what we're committing to). A model commits to a design — a problem with a chosen approach. Spike often produces the framing for a model. Sequence: spike → model → ADR. Or spike → model directly when exploration makes the choice clear.

**Spike vs. research.** A spike has a planned outcome (folds into model[s] or is abandoned). Research captures external knowledge for later consultation with no resolution path. If the outcome path is missing, it's research.

## Non-negotiable gates

1. **Explores an open design question.** A focused investigation, not a knowledge dump. Knowledge for later consultation without a planned outcome → research.
2. **Has a planned outcome.** A spike is committed to either folding into a model or being abandoned. No resolution path named → research.
3. **Has a resolution-path pointer.** A `forward-link:` field or "When this lands" section pointing at the target.

## Decision flow

1. **Has a planned outcome?** No → research. Yes → spike.
2. **Drifted spike** (started as spike, no longer has resolution path) → reclassify to research.
3. **End-of-life:** status `concluded` / `accepted` / `graduated` / `superseded` → content folded; file disposition (archive / delete / leave) project-specific.

## Conventions (once classified as a spike)

- Lives under `Product/spikes/` with a descriptive filename (no numbered prefix).
- Frontmatter: `type: spike`, `status:` (`draft` / `open` / `concluded` / `accepted` / `superseded`), `tags:`.
- Carries either a `forward-link:` field or a "When this lands" section pointing at the target.
- Vocabulary: spikes open with `## Premise` rather than `## Problem` because the framing is open — we're testing a hypothesis, not yet responding to a defined problem.
- Cross-references use `Product/`-rooted absolute paths.

## Status transitions

- **`graduated`** — informed subsequent work (a model or ADR); add a forward link. Spike doc stays put — preserves the trail.
- **`concluded`** — findings recorded with no immediate follow-up.
- **`archived`** — didn't go anywhere; kept for future reference.
- **History doesn't update.** A spike's status reflects only its own resolution. If the model it graduated into is later superseded, the spike stays `graduated` — the supersession chain on the destination tells the rest of the story. Spikes are static once resolved.

## Heuristics

- Spikes are ephemeral — once concluded, fold content into model(s) or abandon.
- If a spike's resolution path disappears (the open question turns out not to need a settled outcome), it has drifted into research → reclassify.

## Audit obligation

When a gate or rule changes, every existing spike is re-checked against the new wording.

## Skeleton

    ---
    type: spike
    status: active
    forward-link: <path to model/ADR this informs, or "TBD">
    tags: []
    ---

    # <name> — spike

    ## Premise

    What we're exploring and why. One paragraph.

    ## What I tried

    Investigation steps, code read, prior art consulted, prototypes sketched.

    ## Findings

    What we learned.

    ## Outcome

    One paragraph. If graduated, include a forward link to the model / ADR it informed.
