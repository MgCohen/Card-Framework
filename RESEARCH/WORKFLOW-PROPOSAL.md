---
type: proposal
status: draft
last-updated: 2026-04-29
builds-on: [SESSION-CHECKPOINT, 06-open-questions]
tags: [#workflow, #meta]
---

# Research Workflow — Proposal

**Purpose.** Codify the research workflow we've been running, plug the gaps we noticed (no doc-type templates, no per-decision log, no session log, no glossary changelog, no alignment-debt tracker), and lock the patterns into shared conventions so new sessions inherit the method, not just the artifacts.

**Scope.** Research-phase workflow only (RESEARCH/). Plans (PLANS/) and durable docs (DOCS/) get minimal touchpoints — full conventions come when we cross those gates.

---

## 1. Folder layout

Four tiers at the root:

- **Operational** — live "what's happening right now" files (`README.md`, `SESSION-CHECKPOINT.md`). Minimal.
- **`general/`** — cross-cutting, durable reference and conventions. The shared vocabulary the topics depend on.
- **`topics/`** — one folder per system/feature, each self-contained with its own notes, decisions, and local numbering.
- **Supporting** — `samples/`, `prior-art/`, `sessions/`.

```
RESEARCH/
  README.md                  # index — points to general/ and topics/
  SESSION-CHECKPOINT.md      # replaceable spine — live operational state

  general/                   # cross-cutting / general-purpose
    WORKFLOW.md              # this doc, once accepted
    GLOSSARY.md              # canonical vocabulary (was 09)
    OPEN-QUESTIONS.md        # canonical ledger (was 06)
    ALIGNMENT-DEBT.md        # open §7 implications across all topics
    _templates/
      discussion.md
      proposal.md
      model.md
      sample.md
      adr.md
      session.md

  topics/                    # all per-feature research
    foundations/             # 00, 01, 02, 03, 05, 07
      notes/
      decisions/
    entities/                # 08, 10
      notes/
      decisions/
    zones/                   # 11, 12
      notes/
      decisions/
    actions/                 # 13, 14, 15, 16, 17, 18, 19, 20
      notes/
      decisions/
    triggers/                # trigger system (subfeature of effects, but big enough to own a topic)
      notes/
      decisions/
    effects/                 # effects, async orchestration, ongoing/continuous effects
      notes/
      decisions/
    engine/                  # 25
      notes/
      decisions/
    presentation/            # 27, 28
      notes/
      decisions/

  samples/                   # whole-system stress tests (was 26)
  prior-art/                 # frozen reference (was Old Docs/)
  sessions/                  # one short file per session

DOCS/                        # durable, implementation-ready docs (gated)
PLANS/                       # implementation slices (gated)
  _TEMPLATE.md
```

### Numbering

**Per-topic counters.** Each topic's `notes/` and `decisions/` restart at `01`. Filenames look like `topics/actions/notes/03-action-definition-proposal.md` and `topics/actions/decisions/0002-async-layer-hierarchy.md`. The path is the domain prefix; no need to bake it into filenames.

**Sessions** stay globally monotonic (`sessions/session-01.md`, `session-02.md`, …). Sessions are the chronological spine — they cross topics by nature.

**Cross-references.** Use the path: `topics/actions/notes/03` or just `topics/actions/03` as shorthand. The `topics/` prefix is non-negotiable in references — keeps grep precise and globs clean.

### Where cross-cutting docs live

When a doc spans multiple systems (e.g., a terminology discussion touching naming hygiene broadly): it lives in the topic that triggered it and gets `tags: [#cross-cutting]` in front matter so it's findable. Genuinely global references (glossary, open questions, alignment debt, workflow) live in `general/`.

### Topic names are project-specific

The folder names shown above (`foundations/`, `entities/`, `zones/`, `actions/`, `triggers/`, `effects/`, `engine/`, `presentation/`) are *this* project's topics. Another project applying this workflow picks its own topics — one folder per major feature/system, sized so a topic can host a meaningful local discussion without overflowing into adjacents. The workflow only mandates the structure (`topics/<name>/notes/`, `topics/<name>/decisions/`), not these specific names.

A rule of thumb: if a topic folder has fewer than ~3 docs after several sessions, it might belong as a tag inside a sibling. If it grows past ~15 docs without natural sub-clusters, consider splitting.

---

## 2. Doc types

| Type | Purpose | Lives in | Status values |
|------|---------|----------|---------------|
| **Discussion** | Frame a contested problem; list candidates; **do not decide** | `topics/<topic>/notes/` | `discussion`, `closed` |
| **Proposal / Decision** | Commit a position with rationale and implications | `topics/<topic>/notes/` | `proposed`, `settled`, `superseded` |
| **Model** | Durable description of a system | `topics/<topic>/notes/` (→ `DOCS/` on promotion) | `draft`, `settled`, `superseded` |
| **Sample** | Concrete reference instance projected onto the design — floor or ceiling | `samples/` | `draft`, `locked` |
| **ADR** | One atomic decision per file | `topics/<topic>/decisions/` | `proposed`, `accepted`, `superseded by NNNN` |
| **Session note** | What happened in one session (append-only) | `sessions/` | n/a (immutable) |
| **Checkpoint** | Replaceable handoff for next session | `RESEARCH/` root | n/a (replaceable) |
| **Plan** | Implementation slice | `PLANS/` | `drafting`, `active`, `done`, `abandoned` |

Major decisions get **both** a Proposal doc (full discussion, alternatives, snippets) **and** an ADR (one-page summary). The ADR links back to the proposal; the proposal links forward to the ADR.

---

## 3. Front matter

Every doc starts with:

```yaml
---
type: proposal | discussion | model | sample | adr | session | checkpoint | plan
status: <one of the type's status values>
last-updated: 2026-04-29
builds-on: [17, 20, 21]      # optional
superseded-by: 28            # optional
tags: [#async, #triggers]    # optional
---
```

Reader knows in 2 seconds what they're holding.

---

## 4. Templates

### 4.1 Discussion

```markdown
# <topic> — discussion

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

### 4.2 Proposal / Decision

```markdown
# <topic> — proposal

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
- doc 25: type flip on DrainResult.Snapshot
- doc 27: AffordanceMap reads templates not commands

## Open follow-ups
Unresolved threads.

## ADR
Linked: decisions/0023-...
```

### 4.3 Model

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
At minimum, walked through against the floor sample game.

## Implications for prior docs (§7)
...
```

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
3. **No open implications against the sample** in `ALIGNMENT-DEBT.md`.
4. **Stable ≥ 1 session** — at least one session has passed without edits to either the sample or its dependencies. Soft cooldown.

After locking, design changes that affect the sample create an explicit `ALIGNMENT-DEBT.md` entry naming the locked sample. Resolving the entry is a deliberate decision: re-lock with the new shape, unlock back to `draft`, or accept the divergence (rare).

### 4.5 ADR

```markdown
# ADR-NNNN — <title>

Status: proposed | accepted | superseded by NNNN
Date: 2026-04-29
Context-doc: notes/22-async-decisions-and-suspension.md

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

```markdown
# Session NN — <date>

<front matter: type=session>

## Part 1 — Content (cap ~50 lines)

### Active question coming in
One line.

### Work done
- doc topics/<topic>/notes/NN written
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

### 5.1 Starting a new topic

1. Open `SESSION-CHECKPOINT.md`. Confirm the active question is the topic, or pick from "other still-open."
2. Decide doc type:
   - Vocabulary or shape contested → **Discussion** doc first.
   - Well-framed and committing → **Proposal** doc.
   - New system's structure → **Model** doc.
   - New game projection → **Sample** doc.
3. Copy the template from `_templates/`. Fill front matter.
4. New vocabulary → also patch `GLOSSARY.md` and append to its changelog.

### 5.2 During a session

- Stay focused on the active question. Defer side threads to "other still-open."
- When a major decision settles → write an ADR in `decisions/`.
- Anything that changes the model → §7 "Implications for prior docs" section, and append each item to `ALIGNMENT-DEBT.md`.
- Settle one thing well rather than half-settle three.

### 5.3 Ending a session

1. Write `sessions/session-NN.md` (5 short sections, 2 minutes).
2. Refresh `SESSION-CHECKPOINT.md` (rewrite, don't append).
3. Update `OPEN-QUESTIONS.md` — strikethrough resolved with link to resolving doc; add new opens.
4. Update `GLOSSARY.md` changelog if vocabulary changed.
5. Commit + PR on `claude/<topic>` branch.

### 5.4 Reopening a settled decision

Don't edit the old ADR. Write a new ADR (`NNNN supersedes MMMM`), set the old ADR's status to `superseded by NNNN`, note the reason in the session log.

### 5.5 Alignment-debt discharge

A session can be entirely a **mechanical alignment pass** — discharging entries from `ALIGNMENT-DEBT.md`. Counts as progress. Tick off entries; reference the commit.

### 5.6 Promotion gate: RESEARCH → DOCS

A research doc can be promoted to `DOCS/` when **all** are true:
- Status is `settled`.
- No open §7 implications against it in `ALIGNMENT-DEBT.md`.
- Referenced by an active `PLANS/` doc **or** an implementation in `Assets/`.
- Stable for ≥ 2 sessions with no edits to the contract.

Promotion is a deliberate move, not a default.

---

## 6. Cross-cutting infrastructure

| File | Role | Cadence |
|------|------|---------|
| `SESSION-CHECKPOINT.md` | Current working surface; active topics + focus questions; references OPEN-QUESTIONS items by link with current context | End of every session |
| `general/WORKFLOW.md` | This doc, once accepted | When workflow conventions change |
| `general/GLOSSARY.md` | Canonical vocabulary + changelog | When a term lands or renames |
| `general/OPEN-QUESTIONS.md` | Durable ledger of all unresolved project questions; strikethrough + link on resolve | When questions open or resolve |
| `general/ALIGNMENT-DEBT.md` | Open §7 implications across all topics | Append on doc landing; tick off on alignment pass |
| `sessions/session-NN.md` | Per-session narrative (append-only). Two parts, each capped ~one screen: (1) content — active question in/out, work done, decisions settled; (2) links + ephemeral/followup questions | End of every session |
| `topics/<topic>/decisions/NNNN-*.md` | ADRs (per-topic counter) | When a decision settles |

### Three lifetimes, three homes for questions

| Question kind | Lifetime | Home |
|---|---|---|
| **Durable** open question | Project lifetime, until resolved | `general/OPEN-QUESTIONS.md` |
| **Active** topic / focus question | Current session(s) | `SESSION-CHECKPOINT.md` (may link to an OPEN-QUESTIONS item with current context) |
| **Ephemeral** session question — "wait, do we mean X or Y?" | One conversation; dies on answer | Inline in the draft doc, or scratch in the session note. Promoted to OPEN-QUESTIONS only if it turns out to be durable. |

**Single-source rule still holds.** OPEN-QUESTIONS owns the canonical text. The checkpoint owns the current framing/context. Ephemeral questions never enter OPEN-QUESTIONS unless promoted.

**Single-source rule.** Each piece of state lives in exactly one of these files. The checkpoint *links to* the others; it does not duplicate them.

---

## 7. What changes vs. today

| Current | Proposed |
|---------|----------|
| Flat `RESEARCH/` with 28 chronologically numbered docs | Topic folders + `general/` + minimal root |
| Docs invent their own shape | Doc-type templates + front matter |
| No status on docs | `status:` in front matter |
| Mega "decisions" docs (08, 11, 14) | ADRs in `topics/<topic>/decisions/`; mega-docs become indexes |
| Checkpoint and `06-open-questions` overlap | Checkpoint links to `general/OPEN-QUESTIONS.md`; no duplication |
| Alignment debt implicit across §7 sections | Explicit `general/ALIGNMENT-DEBT.md` |
| Sessions visible only via PR titles | `sessions/` log |
| Glossary updated silently | Glossary has a changelog |
| Old Docs/ as prior-art folder | Rename to `prior-art/` (cosmetic) |
| Sample games inline (doc 26) | `samples/` folder, room for a second floor |
| Cross-references say "doc 28" | Cross-references use paths: `topics/actions/15` |

---

## 8. Open questions about the workflow itself

Resolved in this draft:
- ~~Numbering scheme inside notes~~ → Per-topic counters, restart at `01` per topic.
- ~~ADR numbering scheme~~ → Per-topic counters in each `<topic>/decisions/`. Path is the prefix.
- ~~Open-questions vs checkpoint canonical~~ → Three lifetimes, three homes (see §6). OPEN-QUESTIONS is the durable ledger; checkpoint is the working surface; ephemeral questions stay in-session.
- ~~Session-note length~~ → Two parts, each capped ~one screen (~50 lines). See §4.6.
- ~~Topic-folder design (triggers vs effects)~~ → One folder per feature. `triggers/` and `effects/` are separate. Triggers depend heavily on effects but warrant their own topic.
- ~~Sample `locked` bar~~ → Four-part bar in §4.4: subject finalized, projection uses only `settled` abstractions, no open alignment debt against it, stable ≥ 1 session. Sample template abstracted to subject/projection so the workflow is project-agnostic.
- ~~Topics nesting layout~~ → Topic folders nest under `topics/`. Cleaner globs, more deterministic structure for AI tooling, clearer top-level separation. ~7 extra characters per cross-reference is the only cost.

No open questions remain on the workflow design. Migration logistics (when, how, ADR backfill scope, etc.) are a separate concern to be planned once the target workflow is locked.

---

## 9. Out of scope

**Migration from the current 28-doc flat layout** — backfill of ADRs for past decisions, where existing docs land in the new topic folders, reference-sweep ordering, and adoption sequencing. These get planned once the target workflow is locked. Until then, the proposal describes the destination, not the path to it.
