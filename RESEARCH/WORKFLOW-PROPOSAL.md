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

```
RESEARCH/
  README.md                # index, grouped by topic
  WORKFLOW.md              # this doc, once accepted
  SESSION-CHECKPOINT.md    # replaceable spine (current)
  GLOSSARY.md              # canonical vocabulary (was 09)
  OPEN-QUESTIONS.md        # canonical ledger (was 06)
  ALIGNMENT-DEBT.md        # open §7 implications

  _templates/              # ready-to-copy templates
    discussion.md
    proposal.md
    model.md
    sample.md
    adr.md
    session.md

  notes/                   # numbered research docs (00, 01, ...)
  decisions/               # ADRs, numbered 0001-, 0002-, ...
  sessions/                # one short file per session
  samples/                 # sample-game docs (was inline doc 26)
  prior-art/               # frozen reference (was Old Docs/)

DOCS/                      # durable, implementation-ready docs (gated)
PLANS/                     # implementation slices (gated)
  _TEMPLATE.md
```

Numbering inside `notes/` continues forward (next is 29). ADRs and sessions get their own monotonic counters.

---

## 2. Doc types

| Type | Purpose | Lives in | Status values |
|------|---------|----------|---------------|
| **Discussion** | Frame a contested problem; list candidates; **do not decide** | `notes/` | `discussion`, `closed` |
| **Proposal / Decision** | Commit a position with rationale and implications | `notes/` | `proposed`, `settled`, `superseded` |
| **Model** | Durable description of a system | `notes/` (→ `DOCS/` on promotion) | `draft`, `settled`, `superseded` |
| **Sample Game** | Concrete game projected onto framework — floor or ceiling | `samples/` | `draft`, `locked` |
| **ADR** | One atomic decision per file | `decisions/` | `proposed`, `accepted`, `superseded by NNNN` |
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

### 4.4 Sample Game

```markdown
# <game> — sample

<front matter: type=sample, status=draft>

## Game rules (player-facing)
Plain English, no framework terms.

## Why this sample
Floor / ceiling / stresses-which-feature.

## Projection onto framework
Map game elements to framework concepts.

## What this does NOT exercise
Honest list.

## Open mappings
Ambiguous bits.
```

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

```markdown
# Session NN — <date>

<front matter: type=session>

## Active question coming in
One line.

## Work done
- doc NN written
- alignment debt opened on docs X/Y/Z
- ADR-NNNN landed

## Decisions settled
- ADR-NNNN: <one line>

## Active question going out
One line — what the next session opens with.

## Followups created
- alignment debt: ...
- new open question: ...
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
| `SESSION-CHECKPOINT.md` | Replaceable handoff; single active question | End of every session |
| `GLOSSARY.md` | Canonical vocabulary + changelog | When a term lands or renames |
| `OPEN-QUESTIONS.md` | Categorized ledger; strikethrough + link on resolve | When questions open or resolve |
| `ALIGNMENT-DEBT.md` | Open §7 implications across docs | Append on doc landing; tick off on alignment pass |
| `sessions/session-NN.md` | Per-session narrative (append-only) | End of every session |
| `decisions/NNNN-*.md` | ADRs | When a decision settles |

**Single-source rule.** Each piece of state lives in exactly one of these files. The checkpoint *links to* the others; it does not duplicate them.

---

## 7. What changes vs. today

| Current | Proposed |
|---------|----------|
| Docs invent their own shape | Doc-type templates + front matter |
| No status on docs | `status:` in front matter |
| Mega "decisions" docs (08, 11, 14) | ADRs; mega-docs become indexes |
| Checkpoint and `06-open-questions` overlap | Checkpoint links to OPEN-QUESTIONS; no duplication |
| Alignment debt implicit across §7 sections | Explicit `ALIGNMENT-DEBT.md` |
| Sessions visible only via PR titles | `sessions/` log |
| Glossary updated silently | Glossary has a changelog |
| Old Docs/ as prior-art folder | Rename to `prior-art/` (cosmetic) |
| Sample games inline (doc 26) | `samples/` folder, room for a second floor |

---

## 8. Open questions about the workflow itself

1. Do we keep numbering monotonic across `notes/`, or restart per system?
2. ADR numbering: monotonic forever, or per-domain prefix (`async-0001`, `zone-0001`)?
3. Is `OPEN-QUESTIONS.md` the canonical ledger, or is the checkpoint? (Proposal: canonical is OPEN-QUESTIONS; checkpoint links.)
4. When is a sample game `locked` vs `draft`? Define the bar.
5. Do session notes need a max length cap (e.g., one screen)?
6. Do we backfill ADRs for major past decisions, or only forward from now?

---

## 9. Adoption order (incremental)

Each step is non-disruptive on its own. Stop at any line; the rest still works.

1. **Front matter + status fields** on the 28 existing docs. Mechanical pass, ~1 session.
2. **`_templates/` folder** with the 8 templates above. ~30 minutes.
3. **`sessions/` folder** + retroactive 1-line entries for sessions 1–7 reconstructed from PR titles. ~30 minutes.
4. **`ALIGNMENT-DEBT.md`** seeded from current §7 lists (today: doc 28 §7 → docs 25, 26, 27).
5. **`decisions/` folder** + ADRs for the top-tier decisions already made:
   - Async layer hierarchy (Effect / GameAction / CompositeAction)
   - GameRules contract (sync, observer-shaped, zero state)
   - Snapshot stores templates not commands
   - PlayerIntent → IntentMapper → GameCommand boundary
   - FIFO queue + evaluator pattern
   Lower-tier decisions stay in their proposal docs.
6. **Glossary changelog** — start now; don't backfill renames.
7. **Folder reshuffle** (`notes/`, `samples/`, `prior-art/`). Most disruptive; do last or skip.
8. **`PLANS/_TEMPLATE.md`** — write when the first slice approaches.
9. **Promotion gate to `DOCS/`** — codify when a first promotion candidate appears.

Steps 1–6 are non-disruptive. Step 7 is optional polish. Steps 8–9 trigger when crossing the research → implementation boundary.

---

## 10. Decision needed

Pick a cut:

- **All of it, in adoption order.**
- **Minimum viable:** front matter + `_templates/` + `sessions/` + `ALIGNMENT-DEBT.md` (steps 1–4).
- **Templates only, leave structure alone:** step 2.
- **Custom cut:** specify.

Once chosen, the next session executes the adoption.
