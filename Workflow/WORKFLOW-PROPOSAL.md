---
type: model
status: proposed
last-updated: 2026-04-29
builds-on: [SESSION-CHECKPOINT.md, 06-open-questions.md]
tags: [#workflow, #meta]
---

# Research Workflow — Proposal

**Purpose.** Codify the research workflow we've been running, plug the gaps we noticed (no doc-type templates, no per-decision log, no session log, no glossary changelog, no alignment-debt tracker, no place for exploratory work), and lock the patterns into shared conventions so new sessions inherit the method, not just the artifacts.

**Scope.** Research-phase workflow only (RESEARCH/). Plans (PLANS/) and durable docs (DOCS/) get minimal touchpoints — full conventions come when we cross those gates.

---

## 1. Folder layout

Four kinds of work at the root:

- **Operational** — live "what's happening right now" files (`README.md`, `SESSION-CHECKPOINT.md`) and the session log (`sessions/`).
- **`general/`** — cross-cutting reference: project orientation (vision, scope, architecture, roadmap), shared vocabulary, ledgers, conventions, and templates.
- **`features/`** — one folder per system being designed, each self-contained with its own notes, decisions, and local numbering.
- **`spikes/`** — ideation and exploration. Time-boxed investigations that may graduate into feature design, get archived, or stay as reference.
- **`support/`** — material that supports the design but isn't the design: stress-test samples and frozen prior art.

```
RESEARCH/
  README.md                  # index — points to general/, features/, spikes/
  SESSION-CHECKPOINT.md      # replaceable spine — live operational state
  sessions/                  # per-session narrative (append-only)

  general/                   # cross-cutting reference + project orientation
    WORKFLOW.md              # this doc, once accepted
    GLOSSARY.md              # canonical vocabulary
    OPEN-QUESTIONS.md        # canonical ledger
    ALIGNMENT-DEBT.md        # open §7 implications across all features
    VISION.md                # project goal, non-goals, principles
    SCOPE.md                 # boundaries (or fold into VISION)
    CAPABILITY-MAP.md        # systems the design will need
    ARCHITECTURE.md          # high-level architecture direction
    ROADMAP.md               # phases and sequencing
    RECOMMENDATIONS.md       # current synthesis
    _templates/
      spike.md
      model.md
      sample.md
      adr.md
      session.md
      checkpoint.md
      plan.md

  features/                  # all per-system design work; each feature has a root <feature>.md
    entities/
      entities.md            # feature root — top-level summary, indexes Models + ADRs
      notes/
      decisions/
    zones/
      zones.md
      notes/
      decisions/
    actions/
      actions.md
      notes/
      decisions/
    triggers/                # subfeature of effects, big enough to own a folder
      triggers.md
      notes/
      decisions/
    effects/
      effects.md
      notes/
      decisions/
    engine/
      engine.md
      notes/
      decisions/
    presentation/
      presentation.md
      notes/
      decisions/

  spikes/                    # flat — most spikes are single docs

  support/
    samples/                 # whole-system stress tests
    prior-art/               # frozen external reference (was Old Docs/)
```

`DOCS/` and `PLANS/` sit outside `RESEARCH/` as gated promotion targets — see §2 (Doc types) and §5.6 (promotion gate).

### Numbering

**Per-feature counters.** Each feature's `notes/` and `decisions/` restart at `01`. Filenames look like `features/actions/notes/03-action-definition.md` and `features/actions/decisions/0002-async-layer-hierarchy.md`. The path is the domain prefix; no need to bake it into filenames.

**Feature roots** use the feature name (no counter): `features/engine/engine.md`. One per feature folder. Sits at the top, summarizes what's beneath. See §4.2.

**Sessions** stay globally monotonic (`sessions/session-01.md`, `session-02.md`, …). Sessions are the chronological spine — they cross features by nature.

**Spikes** stay flat with descriptive filenames (`spikes/scaffold-state-fit.md`, `spikes/sabberstone-patterns.md`). No counter — spikes don't have natural ordering and most are single-doc.

**Cross-references.** Use the path: `features/actions/notes/03` or `features/actions/03` shorthand. The `features/` and `spikes/` prefixes are non-negotiable in references — keeps grep precise and globs clean.

### Where cross-cutting docs live

When a doc spans multiple systems (e.g., a terminology discussion touching naming hygiene broadly): it lives in the feature that triggered it and gets `tags: [#cross-cutting]` in front matter so it's findable. Genuinely global references (glossary, open questions, alignment debt, workflow, project orientation) live in `general/`.

### Feature names are project-specific

The folder names shown above (`entities/`, `zones/`, `actions/`, `triggers/`, `effects/`, `engine/`, `presentation/`) are *this* project's features. Another project applying this workflow picks its own — one folder per major system, sized so it can host a meaningful local discussion without overflowing into adjacents. The workflow only mandates the structure (`features/<name>/notes/`, `features/<name>/decisions/`), not these specific names.

A rule of thumb: if a feature folder has fewer than ~3 docs after several sessions, it might belong as a tag inside a sibling. If it grows past ~15 docs without natural sub-clusters, consider splitting.

---

## 2. Doc types

| Type | Purpose | Lives in | Status values |
|------|---------|----------|---------------|
| **Spike** | Time-boxed investigation; pre-design exploration | `spikes/` | `active`, `concluded`, `graduated`, `archived` |
| **Feature** | Top-level summary of a feature; indexes its Models, ADRs, opens — source of truth for the subsystem | `features/<feature>/<feature>.md` (→ `DOCS/` on promotion) | `draft`, `settled`, `superseded` |
| **Model** | Durable description of a sub-system within a feature, with decision provenance baked in | `features/<feature>/notes/` (→ `DOCS/` on promotion) | `draft`, `settled`, `superseded` |
| **Sample** | Concrete reference instance projected onto the design — floor or ceiling | `support/samples/` | `draft`, `locked` |
| **ADR** | One atomic decision per file | `features/<feature>/decisions/` | `proposed`, `accepted`, `superseded by NNNN` |
| **Session note** | What happened in one session (append-only) | `sessions/` | n/a (immutable) |
| **Checkpoint** | Replaceable handoff for next session | `RESEARCH/` root | n/a (replaceable) |
| **Plan** | Implementation slice | `PLANS/` | `drafting`, `active`, `done`, `abandoned` |

A Model is the unified design artifact: it describes a system **and** records the decisions baked into it (options weighed, rationale, consequences). The descriptive content sits at the top; provenance sits below a divider as track record.

Major decisions that warrant searchability also get an **ADR** — one-page summary, dated and indexed. The ADR links back to the Model; the Model links forward to the ADR.

---

## 3. Front matter

Every doc starts with:

```yaml
---
type: spike | feature | model | sample | adr | session | checkpoint | plan
status: <one of the type's status values>
last-updated: 2026-04-29
builds-on: [features/actions/03, features/triggers/02]    # optional, paths
superseded-by: features/presentation/02                   # optional, path
tags: [#async, #triggers]                                 # optional
---
```

Reader knows in 2 seconds what they're holding.

---

## 4. Templates

### 4.1 Spike

A spike is a time-boxed investigation — pre-design exploration. Read prior art, probe a technical question, sketch a possible direction. Spikes are *not* decisions; they produce material that informs decisions.

**Spike vs. Model.** A spike is *investigation* (open-ended; we don't yet know what we're committing to). A Model commits to a design — a problem with a chosen approach. Spike often produces the framing for a Model. Sequence: spike → model → ADR. Or spike → model directly when exploration makes the choice clear.

*Vocabulary note: spikes open with `## Premise` rather than `## Problem` because the framing is open — we're testing a hypothesis, not yet responding to a defined problem. Models use `## Problem` because by that stage the question is named.*

```markdown
# <name> — spike

<front matter: type=spike, status=active>

## Premise
What we're exploring and why. One paragraph.

## What I tried
Investigation steps, code read, prior art consulted, prototypes sketched.

## Findings
What we learned.

## Outcome
One paragraph. If graduated, include a forward link to the Model / ADR it informed.
```

**Status transitions.** When a spike informs subsequent work, set its status to `graduated` and add a forward link to the Model / ADR it informed. The spike doc stays put — preserves the trail. `concluded` means findings recorded with no immediate follow-up. `archived` means it didn't go anywhere; kept for future reference.

### 4.2 Feature

A **Feature** doc is the root description of a feature folder. It says what the feature *is*, what Models compose it, what's settled vs. open, and what other features it depends on. It's the source of truth that Models, ADRs, and spikes underneath it support — and the artifact that promotes to `DOCS/` once stable.

A feature root lives at `features/<feature>/<feature>.md` (e.g., `features/engine/engine.md`). One per feature folder. No counter; the name matches the folder.

**Feature vs. Model.** A Feature is the *container* — its scope is the whole feature. A Model is a *sub-system* within the feature (engine has Models for the evaluation loop, the GameRules contract, the snapshot pipeline). The Feature doc summarizes composition and public surface; the Models carry the design content.

```markdown
# <feature> — feature

<front matter: type=feature, status=draft>

## Purpose
What this feature is and why it exists. One paragraph.

## Boundaries
What this feature owns — and what it explicitly doesn't. Demarcation against neighboring features.

## Composition
Models that make up this feature.
- features/<feature>/notes/01-<slug> — short purpose
- features/<feature>/notes/02-<slug> — short purpose

## Public surface
What a consumer of this feature sees. Contracts, services, types — the outer boundary that other features touch.

## Dependencies
- **Upstream:** features/X — consumed
- **Downstream:** features/Y — consumes this

## Settled decisions
Pointers to load-bearing ADRs in this feature's `decisions/`.

## Open
Pointers to `general/OPEN-QUESTIONS.md` items relevant to this feature.

## Implications for prior docs (§7)
- Models within this feature whose framing this doc affects.
```

A Feature doc starts as a stub when the folder is first created — sometimes only Purpose + Boundaries, with everything else empty. It thickens as Models land beneath it. When the Feature is `settled` and clears the §5.6 promotion gate, it (along with its Models) graduates to `DOCS/`.

### 4.3 Model

The unified design artifact. Top half describes the system; bottom half records the provenance (options weighed, rationale, consequences). A reader gets the model first; clicks down for the "why."

```markdown
# <subject> — model

<front matter: type=model, status=draft>

## Problem
What we're modeling and why this exists.

## Concepts
Types and entities in this system, glossary-style.

## Contracts
Interfaces, methods, return shapes, forbidden behaviors.

## Invariants
What must always be true.

## Examples
At minimum, walked through against the floor sample.

## Implications for prior docs (§7)
- features/<feature>/<n>: ...

## Open follow-ups
Unresolved threads.

---

*Below: provenance — what was considered, why we chose this. Track record.*

## Options considered          # (omit if no real alternatives)
- **A** — rejected because ...
- **B** — rejected because ...
- **C** — chosen.

## Rationale                    # (omit if obvious)
Why the chosen approach.

## Consequences                 # (omit if obvious)
What this enables; what it constrains.

## ADR
Linked: features/<feature>/decisions/NNNN-<title>.md
```

A purely descriptive Model (e.g., a system that doesn't have meaningful alternatives) can omit the provenance sections entirely. A decision-heavy Model fills them all in. Section order is canonical so readers always know where to look.

### 4.4 Sample

A **sample** is a concrete reference instance projected onto the abstractions being designed. It exists to stress-test the design against real cases — a floor (simplest case the design must support) or a ceiling (complex case that bounds expressiveness).

In this project: a card game's rules and how its elements map onto framework concepts. In other projects it could be a client codebase using a library, a protocol implementation, a sample program for a language, an integration for an API — anything concrete enough to project the design onto.

A sample doc has two layers:

- **Subject** — the concrete thing being described. Usually stable once written.
- **Projection** — how the subject's elements map onto the design abstractions. Moves whenever the design moves. This is the load-bearing layer.

```markdown
# <sample name> — sample

<front matter: type=sample, status=draft>

## Subject
The concrete thing this sample describes, in plain language. No design-doc terminology.

## Why this sample
Floor / ceiling / stresses-which-feature.

## Projection
How the subject's elements map onto the design abstractions.

## What this does NOT exercise
Honest list — keeps us from over-claiming coverage.

## Open mappings
Ambiguous bits, unresolved projections.
```

**`draft` → `locked` transition.** A sample becomes `locked` when **all** are true:

1. **Subject finalized** — the concrete description is fully specified; we aren't still debating what the example is.
2. **Projection uses only `settled` abstractions** — every design concept the projection depends on is itself `settled`, not `draft` or `proposed`. (No locking onto sand.)
3. **No open implications against the sample** in `general/ALIGNMENT-DEBT.md`.
4. **Stable ≥ 1 session** — at least one session has passed without edits to either the sample or its dependencies. Soft cooldown.

After locking, design changes that affect the sample create an explicit `general/ALIGNMENT-DEBT.md` entry naming the locked sample. Resolving the entry is a deliberate decision: re-lock with the new shape, unlock back to `draft`, or accept the divergence (rare).

### 4.5 ADR

```markdown
# ADR-NNNN — <title>

<front matter: type=adr, status=proposed>

Date: 2026-04-29
Context-doc: features/<feature>/notes/NN-<title>.md
Proposal: features/<feature>/notes/NN-<title>.md

## Context
Two paragraphs max.

## Options considered
- A: ... — rejected because ...
- B: ... — rejected because ...
- C: ... — chosen.

## Decision
One paragraph.

## Consequences
- Enables: ...
- Constrains: ...
- Reverts/replaces: ADR-NNNN
```

### 4.6 Session note

Two parts, each capped at ~one screen (~50 lines). If either overflows, the content belongs in a numbered doc or an ADR, not the session note.

*Active question coming in* mirrors the active question in `SESSION-CHECKPOINT.md` at session start; *active question going out* is what the refreshed checkpoint will hold at session end.

```markdown
# Session NN — <date>

<front matter: type=session>

## Part 1 — Content (cap ~50 lines)

### Active question coming in
One line.

### Work done
- doc features/<feature>/notes/NN written
- alignment debt opened on docs X/Y/Z
- ADR-NNNN landed

### Decisions settled
- ADR-NNNN: <one line>

### Active question going out
One line — what the next session opens with.

## Part 2 — Links & open threads (cap ~50 lines)

### Followups created
- alignment debt: ...
- new open question (promoted): ...

### Ephemeral questions (scratch, not promoted)
- Things we chewed on but answered in-session.
- Kept here for traceability; not a durable record.

### References
- Docs touched: ...
- Related sessions: ...
```

### 4.7 Checkpoint

Keep the current shape; add a rule.

```markdown
# Research Session Checkpoint

<front matter: type=checkpoint>

**Last updated:** YYYY-MM-DD (session NN)
**Replaceable.** History lives in `sessions/`. Never append here.

## Where we stopped
## The active open question (singular)
## Other still-open
## Suggested next steps
## Artifacts to read on resume
```

### 4.8 Plan

```markdown
# Plan: <slice name>

<front matter: type=plan, status=drafting>

## Slice
Smallest useful chunk.

## Prerequisites
Research findings this depends on (link to docs/ADRs).

## Exit criteria
Concrete checks that mean "done."

## Risks
What could derail.

## Check-in cadence
Per session / per week / etc.
```

---

## 5. Global workflow

A session is a **branching unit**, not a linear one. It opens with an entry context, spawns sub-problems, sketches, and questions as work surfaces them, and lands somewhere. Doc types are tools you reach for mid-flight as branches demand them — not categories you commit to up front.

### 5.0 Bootstrapping a project

Once-per-project phase before §5.1 applies. Output is a working `RESEARCH/` skeleton with enough orientation to start session 02 with a real question.

1. **Create the skeleton.** `general/`, `features/`, `spikes/`, `support/{samples,prior-art}/`, `sessions/`. Drop templates into `general/_templates/`. Initialize empty ledgers: `general/OPEN-QUESTIONS.md`, `general/ALIGNMENT-DEBT.md`, `general/GLOSSARY.md` (with empty changelog).
2. **Write `general/VISION.md` first.** What is this project? Non-goals. Principles. One paragraph each. This is the only doc that has no upstream — everything else builds on it.
3. **Identify candidate features.** Don't write Feature docs yet — just commit to the names. Create empty `features/<feature>/` folders for each. Cheap to add or rename later; expensive to thrash on once Models nest.
4. **Write `general/CAPABILITY-MAP.md`.** What systems will need to exist to deliver the vision? This justifies the feature list from step 3.
5. **Write `general/ARCHITECTURE.md` and `general/ROADMAP.md`.** Direction and sequencing. Both stay terse — they'll be revised as feature design lands.
6. **Seed `general/OPEN-QUESTIONS.md`.** The durable questions you can already name from day one (state ownership, async story, multiplayer, etc.). Don't try to be exhaustive.
7. **Open `sessions/session-01.md`.** This first session *is* the bootstrap. Active question coming in: *"what is this project?"* Active question going out: *"what's the first feature we model?"*
8. **Don't write feature design yet.** No Feature roots, Models, ADRs, or Samples. Bootstrap is purely orientation. Feature design starts in session 02.

A bootstrap session usually fits in one sitting. If it sprawls, the project is too big or the vision isn't clear yet — pause and split.

### 5.1 Starting a session

Open `SESSION-CHECKPOINT.md`. Take stock — note the entry context: the active question, alignment debt, PR feedback, or whatever brought you here. That's it. No commitment to a single direction.

### 5.2 During a session

Sessions branch. That's normal, not an exception. As work surfaces sub-problems, sketches, or new questions:

- **Address inline** if it's small and feeds the active thread.
- **Spawn a doc** if it deserves one. Pick the doc type from §2:
  - Open-ended exploration / prior-art survey → **Spike** (in `spikes/`).
  - System needs design (decisions, structure, or both) → **Model**.
  - New concrete reference instance → **Sample** (in `support/samples/`).
- **Capture for later** if it's out of scope: promote to `general/OPEN-QUESTIONS.md` so it doesn't get lost.

When a major decision settles → write an ADR in `features/<feature>/decisions/`.
When the model changes → §7 "Implications for prior docs" section + append each item to `general/ALIGNMENT-DEBT.md`.
When a spike concludes or graduates → update its status and add forward links.
New vocabulary → patch `general/GLOSSARY.md` and append to its changelog.

Branch freely; the session log captures the trail. Returning to the original thread after a detour is normal — just record both in the session note.

### 5.3 Ending a session

The session note records the **trail**, not a linear narrative: what was created, what was touched, what branched, what was answered, what's still open.

1. Write `sessions/session-NN.md` (two parts, ~50 lines each cap).
2. Refresh `SESSION-CHECKPOINT.md` with the landing state (rewrite, don't append).
3. Update `general/OPEN-QUESTIONS.md` — strikethrough resolved with link to resolving doc; add new opens.
4. Update `general/GLOSSARY.md` changelog if vocabulary changed.
5. Commit + PR on `claude/<topic>` branch.

### 5.4 Reopening a settled decision

Don't edit the old ADR. Write a new ADR (`NNNN supersedes MMMM`), set the old ADR's status to `superseded by NNNN`, note the reason in the session log.

### 5.5 Alignment-debt discharge as a focus

A session can be entirely alignment-pass-shaped. Same flow — the entry context is "discharge entries from `general/ALIGNMENT-DEBT.md`," and the work is mechanical updates. Listed separately because it's a recognizable session pattern, not a different workflow.

### 5.6 Promotion gate: RESEARCH → DOCS

A research doc can be promoted to `DOCS/` when **all** are true:
- Status is `settled`.
- No open §7 implications against it in `general/ALIGNMENT-DEBT.md`.
- Referenced by an active `PLANS/` doc **or** an implementation in `Assets/`.
- Stable for ≥ 2 sessions with no edits to the contract.

**Feature roots are the typical promotion target.** When a Feature passes the gate, it goes to `DOCS/` along with the Models it indexes (which travel with it as supporting material). Models can promote independently if the Feature root isn't ready but the Model is stable on its own — but the default path is feature-shaped, not model-shaped.

Promotion is a deliberate move, not a default.

---

## 6. Cross-cutting infrastructure

| File | Role | Cadence |
|------|------|---------|
| `SESSION-CHECKPOINT.md` | Current working surface; active focus + focus questions; references OPEN-QUESTIONS items by link with current context | End of every session |
| `general/WORKFLOW.md` | This doc, once accepted | When workflow conventions change |
| `general/GLOSSARY.md` | Canonical vocabulary + changelog | When a term lands or renames |
| `general/OPEN-QUESTIONS.md` | Durable ledger of all unresolved project questions; strikethrough + link on resolve | When questions open or resolve |
| `general/ALIGNMENT-DEBT.md` | Open §7 implications across all features | Append on doc landing; tick off on alignment pass |
| `sessions/session-NN.md` | Per-session narrative (append-only). Two parts, each capped ~one screen: (1) content — active question in/out, work done, decisions settled; (2) links + ephemeral/followup questions | End of every session |
| `features/<feature>/<feature>.md` | Feature root — top-level summary, indexes Models + ADRs, source of truth for the subsystem | When a Model lands, an ADR settles, or feature scope shifts |
| `features/<feature>/decisions/NNNN-*.md` | ADRs (per-feature counter) | When a decision settles |
| `spikes/<name>.md` | Time-boxed investigations | When new exploration starts; status updated on conclude/graduate/archive |

### Three lifetimes, three homes for questions

| Question kind | Lifetime | Home |
|---|---|---|
| **Durable** open question | Project lifetime, until resolved | `general/OPEN-QUESTIONS.md` |
| **Active** focus question | Current session(s) | `SESSION-CHECKPOINT.md` (may link to an OPEN-QUESTIONS item with current context) |
| **Ephemeral** session question — "wait, do we mean X or Y?" | One conversation; dies on answer | Inline in the draft doc, or scratch in the session note. Promoted to OPEN-QUESTIONS only if it turns out to be durable. |

**Single-source rule still holds.** OPEN-QUESTIONS owns the canonical text. The checkpoint owns the current framing/context. Ephemeral questions never enter OPEN-QUESTIONS unless promoted.

**Single-source rule.** Each piece of state lives in exactly one of these files. The checkpoint *links to* the others; it does not duplicate them.

---

## 7. What changes vs. today

| Current | Proposed |
|---------|----------|
| Flat `RESEARCH/` with 28 chronologically numbered docs | `features/` + `general/` + `spikes/` + `support/` + minimal root |
| Docs invent their own shape | 7 doc types with templates + front matter |
| No status on docs | `status:` in front matter |
| Discussion + Proposal + Model as separate types in early drafts | Folded into one **Model** type with provenance below a divider |
| Mega "decisions" docs (08, 11, 14) | ADRs in `features/<feature>/decisions/`; mega-docs become Model indexes |
| Foundations (vision, scope, architecture, roadmap) numbered alongside feature docs | Folded into `general/` as project orientation, separate from feature design work |
| Spike-shaped exploration numbered into the regular sequence (docs 04, 13) | Lives in `spikes/` with its own doc type and status lifecycle |
| Checkpoint and `06-open-questions` overlap | Checkpoint links to `general/OPEN-QUESTIONS.md`; no duplication |
| Alignment debt implicit across §7 sections | Explicit `general/ALIGNMENT-DEBT.md` |
| Sessions visible only via PR titles | `sessions/` log at root (operational) |
| Glossary updated silently | Glossary has a changelog |
| Old Docs/ as prior-art folder | Renamed and grouped: `support/prior-art/` |
| Sample games inline (doc 26) | `support/samples/` folder, room for a second floor |
| Cross-references say "doc 28" | Cross-references use paths: `features/actions/15` |

---

## 8. Open questions about the workflow itself

Resolved in this draft:
- ~~Numbering scheme inside notes~~ → Per-feature counters, restart at `01` per feature.
- ~~ADR numbering scheme~~ → Per-feature counters in each `features/<feature>/decisions/`. Path is the prefix.
- ~~Open-questions vs checkpoint canonical~~ → Three lifetimes, three homes (see §6). OPEN-QUESTIONS is the durable ledger; checkpoint is the working surface; ephemeral questions stay in-session.
- ~~Session-note length~~ → Two parts, each capped ~one screen (~50 lines). See §4.6.
- ~~Doc-type count~~ → 8 types. Original spike→model→ADR was extended with **Feature** (root summary per feature, source of truth that promotes to DOCS/) once it became clear Models alone left no canonical home for the feature itself. Discussion folded into Model's "Options considered" section; Proposal merged into Model. The unified Model artifact carries both descriptive content (top) and decision provenance (bottom, below divider).
- ~~Topic-folder design (triggers vs effects)~~ → One folder per feature. `triggers/` and `effects/` are separate. Triggers depend heavily on effects but warrant their own folder.
- ~~Sample `locked` bar~~ → Four-part bar in §4.4: subject finalized, projection uses only `settled` abstractions, no open alignment debt against it, stable ≥ 1 session. Sample template abstracted to subject/projection so the workflow is project-agnostic.
- ~~Topics-vs-features naming~~ → `features/`, product-dev framing.
- ~~Topics nesting layout~~ → Feature folders nest under `features/`. Cleaner globs, more deterministic structure for AI tooling, clearer top-level separation.
- ~~Foundations placement~~ → Folded into `general/` as project-orientation files (`VISION`, `SCOPE`, `ARCHITECTURE`, `ROADMAP`, etc.). Reference-shaped, not feature-design-shaped.
- ~~Spikes as a category~~ → Top-level `spikes/` folder + new `Spike` doc type with `active`/`concluded`/`graduated`/`archived` lifecycle. Captures exploration distinct from feature design.
- ~~`support/` wrapper~~ → `support/samples/` and `support/prior-art/`. `sessions/` stays at root (operational, written every session, not background reference).

No open questions remain on the workflow design. Migration logistics (when, how, ADR backfill scope, etc.) are a separate concern to be planned once the target workflow is locked.

---

## 9. Out of scope

**Migration from the current 28-doc flat layout** — backfill of ADRs for past decisions, where existing docs land in the new feature folders, reference-sweep ordering, and adoption sequencing. These get planned once the target workflow is locked. Until then, the proposal describes the destination, not the path to it.
