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

Three tiers:

- **Root** — operational, live, "what's happening right now." Minimal.
- **`general/`** — cross-cutting, durable reference and conventions. The shared vocabulary the topics depend on.
- **Topic folders** — one per system, each self-contained with its own notes, decisions, and local numbering.

```
RESEARCH/
  README.md                # index — points to general/ and topics
  SESSION-CHECKPOINT.md    # replaceable spine — live operational state

  general/                 # cross-cutting / general-purpose
    WORKFLOW.md            # this doc, once accepted
    GLOSSARY.md            # canonical vocabulary (was 09)
    OPEN-QUESTIONS.md      # canonical ledger (was 06)
    ALIGNMENT-DEBT.md      # open §7 implications across all topics
    _templates/
      discussion.md
      proposal.md
      model.md
      sample.md
      adr.md
      session.md

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
  triggers-effects/        # 21, 22, 23, 24
    notes/
    decisions/
  engine/                  # 25
    notes/
    decisions/
  presentation/            # 27, 28
    notes/
    decisions/

  samples/                 # whole-system stress tests (was 26)
  prior-art/               # frozen reference (was Old Docs/)
  sessions/                # one short file per session

DOCS/                      # durable, implementation-ready docs (gated)
PLANS/                     # implementation slices (gated)
  _TEMPLATE.md
```

### Numbering

**Per-topic counters.** Each topic's `notes/` and `decisions/` restart at `01`. Filenames look like `actions/notes/03-action-definition-proposal.md` and `actions/decisions/0002-async-layer-hierarchy.md`. The path is the domain prefix; no need to bake it into filenames.

**Sessions** stay globally monotonic (`sessions/session-01.md`, `session-02.md`, …). Sessions are the chronological spine — they cross topics by nature.

**Cross-references.** Use the path: `actions/notes/03` or just `actions/03` as shorthand. Old "doc N" references get rewritten in the migration.

### Where cross-cutting docs live

When a doc spans multiple systems (e.g., `16-action-terminology-discussion` touches naming hygiene broadly): it lives in the topic that triggered it (here, `actions/`) and gets `tags: [#cross-cutting]` in front matter so it's findable. Genuinely global references (glossary, open questions, alignment debt, workflow) live in `general/`.

---

## 2. Doc types

| Type | Purpose | Lives in | Status values |
|------|---------|----------|---------------|
| **Discussion** | Frame a contested problem; list candidates; **do not decide** | `<topic>/notes/` | `discussion`, `closed` |
| **Proposal / Decision** | Commit a position with rationale and implications | `<topic>/notes/` | `proposed`, `settled`, `superseded` |
| **Model** | Durable description of a system | `<topic>/notes/` (→ `DOCS/` on promotion) | `draft`, `settled`, `superseded` |
| **Sample Game** | Concrete game projected onto framework — floor or ceiling | `samples/` | `draft`, `locked` |
| **ADR** | One atomic decision per file | `<topic>/decisions/` | `proposed`, `accepted`, `superseded by NNNN` |
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

Two parts, each capped at ~one screen (~50 lines). If either overflows, the content belongs in a numbered doc or an ADR, not the session note.

```markdown
# Session NN — <date>

<front matter: type=session>

## Part 1 — Content (cap ~50 lines)

### Active question coming in
One line.

### Work done
- doc <topic>/notes/NN written
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
| `<topic>/decisions/NNNN-*.md` | ADRs (per-topic counter) | When a decision settles |

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
| Mega "decisions" docs (08, 11, 14) | ADRs in `<topic>/decisions/`; mega-docs become indexes |
| Checkpoint and `06-open-questions` overlap | Checkpoint links to `general/OPEN-QUESTIONS.md`; no duplication |
| Alignment debt implicit across §7 sections | Explicit `general/ALIGNMENT-DEBT.md` |
| Sessions visible only via PR titles | `sessions/` log |
| Glossary updated silently | Glossary has a changelog |
| Old Docs/ as prior-art folder | Rename to `prior-art/` (cosmetic) |
| Sample games inline (doc 26) | `samples/` folder, room for a second floor |
| Cross-references say "doc 28" | Cross-references use paths: `actions/15` |

---

## 8. Open questions about the workflow itself

Resolved in this draft:
- ~~Numbering scheme inside notes~~ → Per-topic counters, restart at `01` per topic.
- ~~ADR numbering scheme~~ → Per-topic counters in each `<topic>/decisions/`. Path is the prefix.
- ~~Open-questions vs checkpoint canonical~~ → Three lifetimes, three homes (see §6). OPEN-QUESTIONS is the durable ledger; checkpoint is the working surface; ephemeral questions stay in-session.
- ~~Session-note length~~ → Two parts, each capped ~one screen (~50 lines). See §4.6.

Still open:

1. When is a sample game `locked` vs `draft`? Define the bar.
2. Do we backfill ADRs for major past decisions, or only forward from now?
3. Topic boundaries for the initial migration — do we accept the proposed split (foundations / entities / zones / actions / triggers-effects / engine / presentation) or refine? Specifically: split `triggers-effects` or keep merged; does `engine` swallow `gamerules`; does action-availability (current doc 28) live in `presentation/` or `actions/`.
4. Do we want a top-level `topics/` parent folder, or do topic folders sit directly under `RESEARCH/` next to `general/`? (Proposal: directly under, to keep paths short.)

---

## 9. Adoption order (incremental)

Each step is non-disruptive on its own. Stop at any line; the rest still works.

1. **Create `general/`** and move `09-core-glossary.md` → `general/GLOSSARY.md`, `06-open-questions.md` → `general/OPEN-QUESTIONS.md`. Add empty `general/ALIGNMENT-DEBT.md`. ~10 minutes + reference sweep.
2. **`general/_templates/` folder** with the 8 templates above. ~30 minutes.
3. **Topic folders + migration.** Move the 26 remaining numbered docs into topic folders, restart counters per topic. One commit per topic to keep diffs reviewable. Update cross-references in the same pass. ~1 session.
4. **Front matter + status fields** on all migrated docs. Mechanical pass, can run alongside step 3.
5. **`sessions/` folder** + retroactive 1-line entries for sessions 1–7 reconstructed from PR titles. ~30 minutes.
6. **Seed `general/ALIGNMENT-DEBT.md`** from current §7 lists (today: doc 28 §7 → docs 25, 26, 27 — now `actions/...` and `presentation/...` paths).
7. **ADRs for top-tier past decisions** in their topic folders:
   - `triggers-effects/decisions/0001-async-layer-hierarchy.md` (Effect / GameAction / CompositeAction)
   - `engine/decisions/0001-gamerules-contract.md` (sync, observer-shaped, zero state)
   - `presentation/decisions/0001-snapshot-stores-templates.md`
   - `actions/decisions/0001-playerintent-boundary.md`
   - `engine/decisions/0002-fifo-queue-evaluator.md`
   Lower-tier decisions stay in their proposal docs.
8. **Glossary changelog** — start now; don't backfill renames.
9. **`PLANS/_TEMPLATE.md`** — write when the first slice approaches.
10. **Promotion gate to `DOCS/`** — codify when a first promotion candidate appears.

Steps 1–8 are doable inside the research phase without touching `Assets/`. Steps 9–10 trigger when crossing the research → implementation boundary.

---

## 10. Decision needed

Pick a cut:

- **All of it, in adoption order.**
- **Minimum viable:** front matter + `_templates/` + `sessions/` + `ALIGNMENT-DEBT.md` (steps 1–4).
- **Templates only, leave structure alone:** step 2.
- **Custom cut:** specify.

Once chosen, the next session executes the adoption.
