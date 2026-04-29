---
type: index
last-updated: 2026-04-29
tags: [#index, #operating-manual]
---

# Product/ — operating manual

You are an AI agent working in the Card Framework. **This file is your operating manual.** Read it on every new conversation, then load the live state, then act.

## Where you are

The Card Framework is a Unity card-game framework. Two layers connected by status gates:

- **Design** lives in `Product/`. Status governs role: `draft` (in flight), `settled` (the canonical contract), `implemented` (code in `Assets/` matches the contract).
- **Code** lives in `Assets/`. The artifact `implemented` Models point at.

Drift between contract and code is the failure mode this workflow exists to prevent. The rules below are load-bearing — skipping them defers cost into a future consistency pass; it never saves a session.

The formal workflow specification with rationale is at [`_workflow/PRODUCT-BUILDER.md`](_workflow/PRODUCT-BUILDER.md). **Don't read it in normal operation** — this file is sufficient. Open it only when the workflow itself is in question.

---

## On entering a session — always do this first

Before responding to the human's opening, read in order:

1. [`SESSION-CHECKPOINT.md`](SESSION-CHECKPOINT.md) — what's settled, the active question, suggested next steps.
2. [`ledgers/ALIGNMENT-DEBT.md`](ledgers/ALIGNMENT-DEBT.md) — count open entries. **If > 5 open, this session is pass-mode**: discharge debt, do not open new design threads.
3. [`sessions/session-NN.md`](sessions/) — the most recent session note (or last 2 if the previous session branched late).
4. The Feature root (`features/<f>/<f>.md`) for any area the active question touches.

Then form a model of state before responding:

- What was last in flight?
- What is the active open question?
- Are there mid-progress drafts? (status `draft` Models in `features/<f>/models/`)
- Are there debts to discharge?
- Did the previous session leave a trigger primed (e.g. a Spike about to graduate, a BACKLOG item whose trigger fired)?

Brief the human on this state in 4–6 lines. Do not jump straight into work without surfacing what you found.

## Reading the human's opening

| Human says… | You do… |
|---|---|
| *"What's next?" / "Where are we?" / no specific direction* | Synthesize state from the bootstrap reads. Surface the active open question. List 2–3 candidate threads (active question, debt discharge, BACKLOG triggers if any have fired) with one-line rationale. Ask which to pull. **Don't guess.** |
| *"Continue X" / "Resume" / "Pick up where we left off"* | Load X's mid-progress state. Propose the next concrete step. Confirm before writing. |
| *"Design X" / "Add a feature"* | Determine: does X fit an existing feature, need a new feature folder (see *When you add a feature*), or start as a Spike? Surface the choice; do not decide silently. |
| *"I want to discuss the workflow itself"* | Open `_workflow/PRODUCT-BUILDER.md`. Otherwise leave it closed. |
| *Specific direction* (e.g. "write the effect-flow Model") | Confirm scope, then proceed under the rules below. |

---

## Always-on rules

You may not violate these without explicit human approval, even if asked.

1. **Never write to `Product/` outside the prescribed structure.** If unsure where something belongs, write a Spike in `spikes/`. Don't number new docs at `Product/` root.
2. **Front matter is mandatory.** Every doc starts with `type`, `status`, `last-updated`. Models additionally require `builds-on:` — a list of upstream paths or the literal `root`. Empty list (`[]`) is forbidden; `root` is the explicit "no upstream" signal.
3. **Per-feature counters restart at `01`.** Filenames are bare (`01-command-pipeline.md`); the path is the prefix. Cross-references use full paths (`features/actions/models/01-command-pipeline.md`), never "doc 28".
4. **The alignment-debt ledger is the single source of truth for cascade.** Models and Features do **not** carry inline `## Implications` sections.
5. **Status flips are deliberate human actions.** Never flip `draft → settled` or `settled → implemented` without checking the gate and confirming with the human.
6. **The three ledgers are independent.** No dual-write across `OPEN-QUESTIONS.md`, `ALIGNMENT-DEBT.md`, `BACKLOG.md`. Each fact lives in exactly one.
7. **One thread per session.** Adjacent issues you spot go to `BACKLOG.md` with a trigger condition — not into the current change.
8. **The session note records the trail truthfully.** What it claims must match what happened.

---

## Mid-session triggers — act without being asked

Watch for these conditions and act when they fire. Tell the human what you're doing; don't ask permission for every step.

| Trigger | Action |
|---|---|
| About to use a term not in `ledgers/GLOSSARY.md` | Add it now (entry + changelog line). A `settled` doc may not contain undefined terms — adding the term up-front prevents blocking the research gate later. |
| About to commit a Model edit | Walk `builds-on:` for downstream docs. Open one entry per affected doc in `ledgers/ALIGNMENT-DEBT.md` (`date \| source \| target \| reason \| open`). Declare `debt: N opened` (or `debt: none`) in the session note. **Empty signal is forbidden** — `debt: none` must be visibly distinct from "I forgot to think about it." |
| A major decision settles | Write an ADR. Feature-scoped → `features/<f>/decisions/NNNN-<slug>.md`. Project-wide → `decisions/NNNN-<slug>.md`. Per-feature counter; per-project counter for general. |
| A settled decision is being reopened | Don't edit the old ADR. Write a new one (`NNNN supersedes MMMM`), set old ADR's status to `superseded by NNNN`, walk `builds-on:` and open one debt entry per dependent doc — supersession is itself a cascade event. |
| A feature gains/loses a Model or shifts dependencies | Refresh `ledgers/FEATURE-MAP.md` (table + mermaid + breakdown). |
| Concrete work is deferred ("we'll do this later") | Append to `ledgers/BACKLOG.md` with a trigger condition. Don't leave it as a comment in the session note. |
| A durable open question surfaces | Append to `ledgers/OPEN-QUESTIONS.md`. Ephemeral questions answered in-session go in the session note part 2 — not in OPEN-QUESTIONS. |
| A Spike is reaching its end | Update its status (`concluded` / `graduated` / `archived`). Add forward links if it informed a Model/ADR. Spikes are static once resolved — don't re-edit them as downstream evolves. |
| Ledger crosses 5 open entries | Tell the human: the next session must be pass-mode. Don't quietly continue design work. |

---

## Doc types

Seven canonical types. Templates in `_workflow/_templates/`.

- **Spike** (`spikes/`) — time-boxed investigation; pre-design exploration. Status: `active` / `concluded` / `graduated` / `archived`. Not a decision.
- **Feature** (`features/<f>/<f>.md`) — root description of a feature folder. The canonical contract once `settled`. Status: `draft` / `settled` / `implemented` / `superseded` / `abandoned`.
- **Model** (`features/<f>/models/`) — durable design of a sub-system. Top half describes; bottom half (below `---`) records provenance (options weighed, rationale, consequences). Status as Feature.
- **Sample** (`support/samples/`) — concrete reference instance projected onto the design. Status: `draft` / `locked`. Lock requires settled abstractions, no debt against the sample, ≥ 1 session stable on contract surface.
- **ADR** (`features/<f>/decisions/` or `decisions/`) — one atomic decision per file. Status: `proposed` / `accepted` / `superseded by NNNN`.
- **Session note** (`sessions/session-NN.md`) — append-only narrative; two parts capped ~50 lines each.
- **Checkpoint** (`SESSION-CHECKPOINT.md`) — replaceable handoff for next session. Rewritten every session end.

(*Plan* in `PLANS/` — implementation slices. Internals deliberately not workflow-specified at v1.)

## Status semantics

| Status | Means | Treat as |
|---|---|---|
| `draft` | In flight; design not committed | Provisional. Edit freely. |
| `settled` | Research gate passed; this **is** the contract | Authoritative. Edits open debt entries against downstream. |
| `implemented` | Build gate passed; code in `Assets/` matches | Authoritative for both. Edits open drift entries against code. |
| `superseded` | Replaced by newer doc | Historical. Read the new one. |
| `abandoned` | Decided against | Historical. Body kept as record. |

### Research gate (`draft → settled`)
- Zero open ledger entries against this path in `ledgers/ALIGNMENT-DEBT.md`.
- Contract surface (Problem / Concepts / Contracts / Invariants / Examples) stable ≥ 2 sessions.
- All glossary terms it uses appear in `ledgers/GLOSSARY.md`.

### Build gate (`settled → implemented`)
- Status is currently `settled`.
- Code in `Assets/` satisfies the contract (a sample exercises it and produces the documented behavior).
- No open drift entries.

### Feature ↔ Model coupling
A Feature can't sit at a higher tier than its lowest *qualifying* Model (qualifying = `settled` or `implemented`; drafts/abandoneds/supersededs don't count). Forward flips are deliberate human actions; backward reverts (drift forces a Model from `implemented` → `settled`) are automatic.

---

## Three ledgers

`ledgers/` carries three independent ledgers. They have distinct shapes and lifecycles. Don't dual-write.

### `OPEN-QUESTIONS.md` — durable design questions
- **Open when:** a design question surfaces that we can't answer in-session and isn't ephemeral.
- **Close when:** the question is answered (strikethrough + link to resolving doc).
- **Entry shape:** title + paragraph framing + linked context + status.

### `ALIGNMENT-DEBT.md` — cascade from edits
- **Open when:** a Model or Feature edit affects a downstream doc. **One entry per affected doc.**
- **Close when:** the downstream doc reconciles (strikethrough + commit link).
- **Entry shape:** `- 2026-04-29 | source: <path> | target: <path> | reason: <one line> | open`.
- **Threshold:** > 5 open entries forces the next session into pass-mode.

### `BACKLOG.md` — deferred concrete work
- **Open when:** work is deliberately deferred ("we know what to do, we're not doing it yet").
- **Close when:** work begins (strikethrough + PR/commit link).
- **Entry shape:** title + 2-line context + trigger condition + references + status.

> A question is *"we don't know what to do."* Debt is *"this edit affected that doc."* Backlog is *"we know what; we're not yet."* If you're unsure which to use, you probably mean a question.

---

## When you add a feature

Mid-project (not bootstrap), a new feature is a deliberate event with a five-step ceremony:

1. Create `features/<feature>/` with `models/` and `decisions/` subfolders.
2. Write the feature root stub (`<feature>.md`) — `Purpose` + `Boundaries` is enough to start.
3. Identify upstream features. The first Model under this feature carries `builds-on:` pointing at upstream Models or the literal `root`.
4. Patch `general/CAPABILITY-MAP.md` (new system identified), `ledgers/FEATURE-MAP.md` (new node + edges), `general/ROADMAP.md` if scheduling shifts.
5. Record `feature: <name> created` in the session note.

---

## On ending a session

When the active thread lands or the session reaches a natural close:

1. Write `sessions/session-NN.md` (two parts, each capped ~50 lines).
   - **Part 1 (Content):** active question coming in, work done, decisions settled, `debt: N opened` or `debt: none`, active question going out.
   - **Part 2 (Links & open threads):** followups created, ephemeral questions (scratch), references.
2. **Rewrite** `SESSION-CHECKPOINT.md` (don't append) with the landing state — where we stopped, the active open question, suggested next steps, artifacts to read on resume.
3. Update `ledgers/OPEN-QUESTIONS.md`: strikethrough resolved questions with link to resolving doc; add new opens.
4. Update `ledgers/GLOSSARY.md` changelog if vocabulary changed.
5. Commit on `claude/<topic>` branch and open PR.

If the human hasn't said *"let's wrap up,"* ask before running this — but recognize the trigger conditions: the active thread landed, debt threshold crossed, or human signals end-of-session ("let's stop", "good for today").

---

## Folder layout

```
Product/
  README.md                  # this file (operating manual)
  SESSION-CHECKPOINT.md      # live state — rewrite, never append
  sessions/session-NN.md     # one per session, two-part, ~1 screen each
  general/
    PRODUCT-BUILDER.md       # the formal spec — workflow rules + rationale
    VISION / GAME-ARCHETYPES / CAPABILITY-MAP / ARCHITECTURE / ROADMAP /
    RECOMMENDATIONS / GLOSSARY / OPEN-QUESTIONS               # orientation
    ALIGNMENT-DEBT / BACKLOG / FEATURE-MAP                    # the three ledgers
    _templates/              # 7 doc-type templates
    decisions/NNNN-<slug>.md # project-wide ADRs
  features/<feature>/
    <feature>.md             # feature root (Purpose / Boundaries / Composition / …)
    models/NN-<slug>.md      # Models
    decisions/NNNN-<slug>.md # feature ADRs (per-feature counter)
  spikes/<slug>.md           # exploration; flat, no counter
  support/
    samples/<slug>.md        # concrete reference instances
    prior-art/<slug>.md      # frozen external reference
```

External:
- `PLANS/` — implementation slices. Internals not workflow-specified.
- `Assets/` — Unity code. The artifact `implemented` Models point at.
- `RESEARCH_BACKUP/` — pre-migration snapshot. Read-only.

---

## Worked example — a Model edit, end-to-end

> Human: *"Move command-handler validation to run before the layer pipeline."*

1. **Locate the contract.** Search `features/actions/models/`. Found `01-command-pipeline.md`. Read it.
2. **Edit the Model.** Update `## Contracts`, `## Invariants`, and the affected `## Examples`. Bump `last-updated`.
3. **Walk downstream.** Grep `builds-on:` for `features/actions/models/01-command-pipeline.md`. Two hits: `features/effects/models/01-async-dispatch.md`, `support/samples/pile-counter.md`.
4. **Open debt entries.** Two new lines in `ledgers/ALIGNMENT-DEBT.md`, one per affected doc, status `open`.
5. **Status check.** If `01-command-pipeline.md` was `implemented`, the entries are drift entries against `Assets/` code — flag this to the human; either patch the code or revert the Model to `settled`. If `settled`, the edit is a deliberate cascade event but status stays.
6. **Session note.** Part 1 "Work done": `debt: 2 opened`. Part 2 references the entries.
7. **Commit + PR** on `claude/<topic>` branch.

The point of step 4 is that it is *visible*. Counting open entries gives the threshold honest signal.

---

## When you need more depth

- **The full spec with rationale:** [`_workflow/PRODUCT-BUILDER.md`](_workflow/PRODUCT-BUILDER.md). Open this only when the workflow itself is in question — not for daily operation.
- **Templates:** [`_workflow/_templates/`](_workflow/_templates/) — 7 doc types.
- **Why a particular thing was decided:** the relevant ADR in `features/<f>/decisions/` or `decisions/`.
- **Bird's-eye view of features and dependencies:** [`ledgers/FEATURE-MAP.md`](ledgers/FEATURE-MAP.md).
