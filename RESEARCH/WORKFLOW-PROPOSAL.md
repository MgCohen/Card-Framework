---
type: proposal
status: draft
last-updated: 2026-04-29
builds-on: [SESSION-CHECKPOINT.md, 06-open-questions.md]
tags: [#workflow, #meta]
---

# Research Workflow — Proposal

**Purpose.** Codify the research workflow we've been running, plug the gaps we noticed (no doc-type templates, no per-decision log, no session log, no glossary changelog, no alignment-debt tracker), and lock the patterns into shared conventions so new sessions inherit the method, not just the artifacts.

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
      discussion.md
      proposal.md
      model.md
      sample.md
      adr.md
      session.md
      checkpoint.md
      plan.md

  features/                  # all per-system design work
    entities/
      notes/
      decisions/
    zones/
      notes/
      decisions/
    actions/
      notes/
      decisions/
    triggers/                # subfeature of effects, big enough to own a folder
      notes/
      decisions/
    effects/
      notes/
      decisions/
    engine/
      notes/
      decisions/
    presentation/
      notes/
      decisions/

  spikes/                    # flat — most spikes are single docs

  support/
    samples/                 # whole-system stress tests
    prior-art/               # frozen external reference (was Old Docs/)

DOCS/                        # durable, implementation-ready docs (gated)
PLANS/                       # implementation slices (gated)
  _TEMPLATE.md
```

### Numbering

**Per-feature counters.** Each feature's `notes/` and `decisions/` restart at `01`. Filenames look like `features/actions/notes/03-action-definition-proposal.md` and `features/actions/decisions/0002-async-layer-hierarchy.md`. The path is the domain prefix; no need to bake it into filenames.

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
| **Discussion** | Frame a contested problem; list candidates; **do not decide** | `features/<feature>/notes/` | `discussion`, `closed` |
| **Proposal / Decision** | Commit a position with rationale and implications | `features/<feature>/notes/` | `proposed`, `settled`, `superseded` |
| **Model** | Durable description of a system | `features/<feature>/notes/` (→ `DOCS/` on promotion) | `draft`, `settled`, `superseded` |
| **Sample** | Concrete reference instance projected onto the design — floor or ceiling | `support/samples/` | `draft`, `locked` |
| **ADR** | One atomic decision per file | `features/<feature>/decisions/` | `proposed`, `accepted`, `superseded by NNNN` |
| **Session note** | What happened in one session (append-only) | `sessions/` | n/a (immutable) |
| **Checkpoint** | Replaceable handoff for next session | `RESEARCH/` root | n/a (replaceable) |
| **Plan** | Implementation slice | `PLANS/` | `drafting`, `active`, `done`, `abandoned` |

Major decisions get **both** a Proposal doc (full discussion, alternatives, snippets) **and** an ADR (one-page summary). The ADR links back to the proposal; the proposal links forward to the ADR.

---

## 3. Front matter

Every doc starts with:

```yaml
---
type: spike | discussion | proposal | model | sample | adr | session | checkpoint | plan
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

**Spike vs. Discussion.** A spike is *investigation* (open-ended; we don't yet know what we're choosing between). A discussion is *framing contested options* (we know the choice space; we don't yet know which option). Spike often produces the framing for a discussion. Sequence: spike → discussion → proposal → ADR. Or spike → proposal directly if exploration makes the answer clear.

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
One paragraph. If graduated, include a forward link to the doc / discussion / ADR it informed.
```

**Status transitions.** When a spike informs subsequent work, set its status to `graduated` and add a forward link to the feature doc / discussion / ADR it informed. The spike doc stays put — preserves the trail. `concluded` means findings recorded with no immediate follow-up. `archived` means it didn't go anywhere; kept for future reference.

### 4.2 Discussion

```markdown
# <subject> — discussion

<front matter: type=discussion, status=discussion>

## Problem
What's confusing or contested. Why it matters now.

## Candidates
| Name | Meaning | Risk |
|------|---------|------|

## Tradeoffs
Short prose per candidate.

## Questions for next pass
1. ...
```

### 4.3 Proposal / Decision

```markdown
# <subject> — proposal

<front matter: type=proposal, status=proposed>

## Problem
One paragraph.

## Options considered
- **A** — rejected because ...
- **B** — rejected because ...
- **C** — chosen.

## Decision
What we're going with.

## Rationale
Why.

## Consequences
What this enables; what it constrains.

## Implications for prior docs (§7)
- features/engine/04: type flip on DrainResult.Snapshot
- features/presentation/02: AffordanceMap reads templates not commands

## Open follow-ups
Unresolved threads.

## ADR
Linked: features/<feature>/decisions/0023-<title>.md
```

### 4.4 Model

```markdown
# <system> — model

<front matter: type=model, status=draft>

## Concepts
Types and entities in this system, glossary-style.

## Contracts
Interfaces, methods, return shapes, forbidden behaviors.

## Invariants
What must always be true.

## Examples
At minimum, walked through against the floor sample.

## Implications for prior docs (§7)
...
```

### 4.5 Sample

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

### 4.6 ADR

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

### 4.7 Session note

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

### 4.8 Checkpoint

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

### 4.9 Plan

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

### 5.1 Starting a new piece of work

1. Open `SESSION-CHECKPOINT.md`. Confirm the active question is the focus, or pick from "other still-open."
2. Decide doc type:
   - Open-ended exploration / prior-art survey / "see what's there" → **Spike**.
   - Vocabulary or shape contested with known options → **Discussion** doc first.
   - Well-framed and committing → **Proposal** doc.
   - New system's structure → **Model** doc.
   - New concrete reference instance → **Sample** doc.
3. Copy the template from `general/_templates/`. Fill front matter.
4. New vocabulary → also patch `general/GLOSSARY.md` and append to its changelog.

### 5.2 During a session

- Stay focused on the active question. Defer side threads to "other still-open."
- When a major decision settles → write an ADR in the relevant `features/<feature>/decisions/`.
- Anything that changes the model → §7 "Implications for prior docs" section, and append each item to `general/ALIGNMENT-DEBT.md`.
- When a spike concludes or graduates → update its status and add forward links if it informed other work.
- Settle one thing well rather than half-settle three.

### 5.3 Ending a session

1. Write `sessions/session-NN.md` (two parts, ~50 lines each cap).
2. Refresh `SESSION-CHECKPOINT.md` (rewrite, don't append).
3. Update `general/OPEN-QUESTIONS.md` — strikethrough resolved with link to resolving doc; add new opens.
4. Update `general/GLOSSARY.md` changelog if vocabulary changed.
5. Commit + PR on `claude/<topic>` branch.

### 5.4 Reopening a settled decision

Don't edit the old ADR. Write a new ADR (`NNNN supersedes MMMM`), set the old ADR's status to `superseded by NNNN`, note the reason in the session log.

### 5.5 Alignment-debt discharge

A session can be entirely a **mechanical alignment pass** — discharging entries from `general/ALIGNMENT-DEBT.md`. Counts as progress. Tick off entries; reference the commit.

### 5.6 Promotion gate: RESEARCH → DOCS

A research doc can be promoted to `DOCS/` when **all** are true:
- Status is `settled`.
- No open §7 implications against it in `general/ALIGNMENT-DEBT.md`.
- Referenced by an active `PLANS/` doc **or** an implementation in `Assets/`.
- Stable for ≥ 2 sessions with no edits to the contract.

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
| Docs invent their own shape | Doc-type templates + front matter |
| No status on docs | `status:` in front matter |
| Mega "decisions" docs (08, 11, 14) | ADRs in `features/<feature>/decisions/`; mega-docs become indexes |
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
- ~~Session-note length~~ → Two parts, each capped ~one screen (~50 lines). See §4.7.
- ~~Topic-folder design (triggers vs effects)~~ → One folder per feature. `triggers/` and `effects/` are separate. Triggers depend heavily on effects but warrant their own folder.
- ~~Sample `locked` bar~~ → Four-part bar in §4.5: subject finalized, projection uses only `settled` abstractions, no open alignment debt against it, stable ≥ 1 session. Sample template abstracted to subject/projection so the workflow is project-agnostic.
- ~~Topics-vs-features naming~~ → `features/`, product-dev framing.
- ~~Topics nesting layout~~ → Feature folders nest under `features/`. Cleaner globs, more deterministic structure for AI tooling, clearer top-level separation.
- ~~Foundations placement~~ → Folded into `general/` as project-orientation files (`VISION`, `SCOPE`, `ARCHITECTURE`, `ROADMAP`, etc.). Reference-shaped, not feature-design-shaped.
- ~~Spikes as a category~~ → Top-level `spikes/` folder + new `Spike` doc type with `active`/`concluded`/`graduated`/`archived` lifecycle. Captures exploration distinct from feature design.
- ~~`support/` wrapper~~ → `support/samples/` and `support/prior-art/`. `sessions/` stays at root (operational, written every session, not background reference).

No open questions remain on the workflow design. Migration logistics (when, how, ADR backfill scope, etc.) are a separate concern to be planned once the target workflow is locked.

---

## 9. Out of scope

**Migration from the current 28-doc flat layout** — backfill of ADRs for past decisions, where existing docs land in the new feature folders, reference-sweep ordering, and adoption sequencing. These get planned once the target workflow is locked. Until then, the proposal describes the destination, not the path to it.
