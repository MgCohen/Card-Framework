# Template pilot — notes

Three pilot files written under the proposed Feature/Model template:

- `commands.md` — Feature umbrella
- `01-command-handler.md` — class-and-contract Model
- `02-command-pipeline.md` — algorithm Model

Notes on what worked and what fought the template, observed while writing them.

---

## Feature: `commands.md`

**What came out smoothly:**

- `## Architecture` was the load-bearing section — it captured the *flow* without leaking into per-Model details. This is what was missing from current Features.
- `## Invariants` as feature-level cross-Model rules works. Naming them ("Single intake", "One handler per type") gives Models something to reference by name.
- `## Vocabulary` table felt right *because* I noted what's *not* here — the cross-feature pointer is real signal.
- `## Boundaries` doing double duty as both ownership and reference list was natural. Probably fine.

**What fought the template:**

- `## Vocabulary` and `## Architecture` overlap — the "what each term means" feeds straight into "how the flow works." Came close to being one section.
- `## Composition` table is thin (just two real Models in the pilot). For a feature with one or two Models, this might just be a sentence in `## Architecture`.

---

## Class-and-contract Model: `01-command-handler.md`

**What came out smoothly:**

- `## What it is` + canonical artifact pair worked exactly as intended — open the doc, see the snippet, know the shape in 30 seconds.
- `## Contracts` carried real weight — listing the methods with semantics + forbidden behaviors is the developer's checklist.
- `## Examples` with a real worked code block was the most concretely useful section. I'd push back on making this optional.
- `## Decisions` pointing at a project-wide ADR feels right above the divider.

**What fought the template:**

- `## Shape` was thin and felt like overhead. The class was already in `## What it is`; Shape ended up being a paragraph about generic params and a forward reference to `IHandlerContext`. Could have been folded into Contracts.
- `## Invariants` overlapped with `commands.md ## Invariants` — I had to explicitly call out which were Model-local vs. cross-Model. Workable but wants a stronger convention.

---

## Algorithm Model: `02-command-pipeline.md`

**What came out smoothly:**

- The pseudocode in `## What it is` was the right canonical artifact for an algorithm Model — better than a class signature would have been. Confirms my worry that "Shape" reads static-biased was real, but the workaround (use pseudocode in What-it-is) was natural.
- `## Contracts` worked here because the algorithm exposes an `ICommandPipeline` interface — even an algorithm Model has a callable surface.
- `## Examples` with a traced execution was *more* useful than the class-and-contract Model's example. Tracing through a real flow validates that the algorithm makes sense.
- `## Open follow-ups` had a real, content-rich item.

**What fought the template:**

- `## Shape` was the worst-fitting section here. I ended up putting `ICommandPipeline` in Shape and the algorithm pseudocode in What-it-is, which felt arbitrary — both are "what the thing is." Had to invent a distinction (Shape = the API surface, What-it-is = the algorithm) that the template doesn't actually mandate.
- `## Decisions` was empty — no project-wide ADRs apply. The empty stub felt like ceremony. Maybe `## Decisions` should be omittable when empty rather than required-with-stub.

---

## Synthesis

**Things that worked, keep them:**

- `## What it is` opener + canonical artifact. Real win. Both Models opened with the right thing.
- `## Contracts` for both class-shaped *and* algorithm-shaped Models — the API surface exists either way.
- `## Examples` as a required section. It was the most useful section in both. Pulls weight.
- `## Decisions` above the divider — felt natural in `01-command-handler` (linked to ADR-0005). Felt like ceremony in `02-command-pipeline` (empty).
- `## Invariants` at both Feature and Model level. The split worked — Feature owns cross-Model; Model owns local. But it wants a clearer convention to avoid duplication.
- The Feature `## Architecture` section. New, load-bearing, did real work.

**Things that fought us, fix them:**

1. **`## Shape` is the weakest section in both pilots.** In `01-command-handler` it duplicated What-it-is. In `02-command-pipeline` it forced an artificial split between "the API surface" and "the algorithm." Recommendation: **drop `## Shape` as a separate section.** Let `## What it is` carry the canonical artifact (snippet *or* algorithm pseudocode *or* diagram, whichever fits), and let `## Contracts` carry the full surface specification. Two sections instead of three.

2. **`## Decisions` should be optional.** Required-with-stub creates ceremony when no project-wide ADR applies. Suggest: omit the section entirely when empty; linter accepts presence-or-absence.

3. **`## Open follow-ups` placement is fine, but the build-kit framing was a stretch.** It's not "stuff you need to build" — it's "stuff you can't build because it's undecided." Re-read of the section title clarifies it; no rename needed. Keep where it is.

4. **`## Vocabulary` in Features needs scope discipline.** Mine listed only feature-owned terms and *named* the cross-feature terms it's *not* defining ("see triggers/", "see GLOSSARY.md"). That worked. Consider making this convention explicit: feature vocabulary tables must include a "what's *not* defined here" line.

5. **`## Composition` table was thin in pilot.** Two real Models + one stubbed = 3 rows. For a feature with 2 Models, this might be a sentence in Architecture. Consider: required only when ≥3 Models.

6. **Below-divider sections (Options/Rationale/Consequences) felt right *for these settled-shape Models*.** Provenance was useful. But these are draft pilots, not 6-month-old Models. Real test of whether provenance ossifies happens later.

---

## Revised template proposal (post-pilot)

```
# <subject> — model

## What it is        ← orient + canonical artifact (snippet / pseudo / diagram)
## Contracts         ← full API surface, methods, signatures, forbidden
## Invariants        ← Model-local rules (cross-Model rules in Feature)
## Decisions         ← linked ADRs (omit if none)
## Examples          ← worked walkthrough
## Open follow-ups   ← unresolved threads

---

## Options considered
## Rationale
## Consequences
```

5 required + 1 optional above the divider. Tighter than before. Each section earned its keep in the pilot.

---

## Round 2 — revised template applied

Changes made to the pilot files:

- Dropped `## Shape` from both Models.
  - `01-command-handler.md`: typing notes + `IHandlerContext` framing folded into the preamble of `## Contracts`.
  - `02-command-pipeline.md`: `ICommandPipeline` interface + handler-registry framing folded into `## Contracts` alongside the method specs.
- Dropped empty `## Decisions` section from `02-command-pipeline.md`. `01-command-handler.md` keeps it (real ADR-0005 pointer).

How the revised docs feel after the pass:

- **`01-command-handler.md`** — tighter. `## Contracts` now opens with the typing/context framing, then dives into method specs. No section feels redundant. `## What it is` gives orientation; `## Contracts` gives the spec. Clean two-step.
- **`02-command-pipeline.md`** — every section earns its keep. `## What it is` (algorithm pseudocode) → `## Contracts` (interface + method specs) → `## Invariants` → `## Examples` → `## Open follow-ups`. No empty stubs. The pseudocode-in-What-it-is + interface-in-Contracts split honors the algorithm/API-surface distinction naturally.

For class-shaped Models, `## What it is`'s snippet and `## Contracts`'s opening overlap — both reference the interface. That's intended layering (orientation vs. spec) but worth calling out in the spec so authors don't try to deduplicate.

For algorithm-shaped Models, the two sections naturally diverge — pseudocode vs. interface — and the split feels right.

## Confirmed final shape

```
## What it is        (required)
## Contracts         (required)
## Invariants        (required)
## Decisions         (optional — omit when empty)
## Examples          (required)
## Open follow-ups   (required)

---

## Options considered
## Rationale
## Consequences
```

5 required + 1 optional above the divider. 3 below. Ready to fold into `PRODUCT-BUILDER.md §4.3` when we move from pilot to spec.

## Still-open questions

- **`## Examples` always required?** It pulled weight in both pilots. Could it be omittable for tiny Models? My lean: keep required — forces honesty about whether the contract has been concretely walked through.
- **Feature `## Composition` table required for ≤2 Models?** A sentence in `## Architecture` might do. Suggested rule: required only when ≥3 Models.
- **Does provenance age well?** Pilots are draft, not 6-month-old Models. Real test of whether `## Options considered / Rationale / Consequences` ossify or stay useful comes later.
