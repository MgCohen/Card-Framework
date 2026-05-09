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
3. **End-of-life:** status `graduated` (informed a Model/ADR that has since landed) or `concluded` (closed without producing a separate doc; findings live in the spike body).

## Conventions (once classified as a spike)

- Lives under `Product/spikes/` with a descriptive filename (no numbered prefix). A spike at `status: draft` lives in `_drafts/` until investigation begins.
- Frontmatter: `type: spike`, `status:` (`draft` / `active` / `graduated` / `concluded`), `tags:`.
- Carries either a `forward-link:` field or a "When this lands" section pointing at the target.
- Vocabulary: spikes open with `## Premise` rather than `## Problem` because the framing is open — we're testing a hypothesis, not yet responding to a defined problem.
- Cross-references use `Product/`-rooted absolute paths.

## Status transitions

- **`draft`** — framing is being written; the investigation hasn't started yet. Lives in `_drafts/`.
- **`active`** — investigation in progress. Lives in `spikes/` (promoted from `_drafts/` in the same commit that flips status).
- **`graduated`** — the spike informed a Model or ADR that has since landed (Model `settled` or ADR `accepted`). Forward link to that doc is mandatory.
- **`concluded`** — investigation closed without producing a separate Model or ADR. Findings live in the spike body itself.
- **History doesn't update.** A spike's status reflects only its own resolution. If the Model it graduated into is later superseded, the spike stays `graduated` — the supersession chain on the destination tells the rest of the story. Spikes are static once resolved.
- **Graduation cascade.** When a downstream Model flips to `settled` or an ADR flips to `accepted`, the spikes that reference it are candidates for `active → graduated`. Confirmed per spike, not automatic — see `_workflow/PRODUCT-BUILDER.md` §Cascade.

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
