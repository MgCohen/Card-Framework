---
type: index
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

1. [`SESSION-CHECKPOINT.md`](SESSION-CHECKPOINT.md) — the live spine. Active focus, open items snapshot, scratch section.
2. [`ledgers/OPEN-ITEMS.md`](ledgers/OPEN-ITEMS.md) — durable loose ends (questions, deferred work, known TODOs).
3. The Feature root (`features/<f>/<f>.md`) for any area the active focus touches.

`Product/sessions/` is a sparse milestone archive (snapshots only). Read entries there only when a specific milestone is relevant — not on routine entry.

Then form a model of state before responding:

- What was last in flight?
- What's in the active focus?
- Are there mid-progress drafts? (status `draft` Models in `features/<f>/models/`)
- Did anything in the `## Scratch` section need promotion or fixup?

Brief the human on this state in 4–6 lines. Do not jump straight into work without surfacing what you found.

## Reading the human's opening

| Human says… | You do… |
|---|---|
| *"What's next?" / "Where are we?" / no specific direction* | Synthesize state from the bootstrap reads. Surface the active focus. List 2–3 candidate threads from OPEN-ITEMS with one-line rationale. Ask which to pull. **Don't guess.** |
| *"Continue X" / "Resume" / "Pick up where we left off"* | Load X's mid-progress state. Propose the next concrete step. Confirm before writing. |
| *"Design X" / "Add a feature"* | Determine: does X fit an existing feature, need a new feature folder (see *When you add a feature*), or start as a Spike? Surface the choice; do not decide silently. |
| *"I want to discuss the workflow itself"* | Open `_workflow/PRODUCT-BUILDER.md`. Otherwise leave it closed. |
| *Specific direction* (e.g. "write the effect-flow Model") | Confirm scope, then proceed under the rules below. |

---

## Always-on rules

You may not violate these without explicit human approval, even if asked.

1. **Never write to `Product/` outside the prescribed structure.** If unsure where something belongs, write a Spike in `spikes/`. Don't number new docs at `Product/` root.
2. **Front matter is mandatory.** Every doc starts with `type` and `status`. Models additionally require `references:` — a list of upstream paths or the literal `root`. Empty list (`[]`) is forbidden; `root` is the explicit "no upstream" signal. (No `last-updated:` field — git is the source of truth.)
3. **Strict Model section order.** Every Model has the canonical sections (Problem, Concepts, Contracts, Invariants, Examples, Open follow-ups, divider, Options considered, Rationale, Consequences, optional ADR) in canonical order. Empty sections may carry the stub `*n/a — purely descriptive Model.*` but cannot be reordered or omitted. The linter (`/builder:lint`) validates structure.
4. **Per-feature counters restart at `01`.** Filenames are bare (`01-command-pipeline.md`); the path is the prefix. Cross-references use full paths (`features/actions/models/01-command-pipeline.md`), never "doc 28".
5. **Cascade is detected, not tracked.** When you edit a Model, run `/builder:lint` (or `/builder:deps <path>`) to surface affected dependents. Fix in place. No ledger.
6. **Feature-internal decisions stay in the Model's provenance section.** ADRs are project-wide / cross-cutting only and live at `Product/decisions/`. Don't create `features/<f>/decisions/`.
7. **Status flips are deliberate human actions.** Never flip `draft → settled` or `settled → implemented` without checking the gate and confirming with the human.
8. **OPEN-ITEMS is the durable ledger; CHECKPOINT is the live spine.** No dual-write. Each fact lives in exactly one.
9. **Update CHECKPOINT continuously as work happens.** Don't batch updates to session boundaries; the agent keeps it current mid-session.

---

## Mid-session triggers — act without being asked

Watch for these conditions and act when they fire. Tell the human what you're doing; don't ask permission for every step.

| Trigger | Action |
|---|---|
| About to use a term not in `ledgers/GLOSSARY.md` | Add it now (entry + changelog line). A `settled` doc may not contain undefined terms — adding the term up-front prevents blocking the research gate later. |
| About to commit a Model edit | Run `/builder:lint` or `/builder:deps <path>` to surface affected dependents; fix in place. If a cascade target needs design judgment rather than mechanical edit, open an OPEN-ITEMS entry. |
| A major decision settles (feature-internal) | Add it to the relevant Model's provenance section (`## Options considered` / `## Rationale` / `## Consequences`). Don't create a per-feature ADR file. |
| A major decision settles (cross-cutting) | Write a project-wide ADR at `decisions/NNNN-<slug>.md`. Citing target across multiple features is the marker. |
| A settled decision is being reopened | Don't edit the old version in place. For a Model, write a new Model and set the old's status to `superseded by NN-<slug>`. For an ADR, write a new ADR. Run the linter to surface affected dependents. |
| A feature gains/loses a Model or shifts dependencies | Refresh `ledgers/FEATURE-MAP.md`. |
| Concrete work is deferred ("we'll do this later") | Append to `ledgers/OPEN-ITEMS.md` with a trigger condition. Don't leave it as a comment in the checkpoint scratch. |
| A durable open question surfaces | Append to `ledgers/OPEN-ITEMS.md`. Ephemeral questions go in `SESSION-CHECKPOINT.md ## Scratch`. |
| A Spike is reaching its end | Update its status (`concluded` / `graduated` / `archived`). Add forward links if it informed a Model/ADR. Spikes are static once resolved — don't re-edit them as downstream evolves. |
| A token is renamed | Run `/builder:promote-glossary <old> -> <new>` — appends to `_workflow/retired-tokens.json` so the linter can surface lingering uses with the replacement. |

---

## Doc types

Six canonical types. Templates in `_workflow/_templates/`.

- **Spike** (`spikes/`) — time-boxed investigation; pre-design exploration. Status: `active` / `concluded` / `graduated` / `archived`. Not a decision.
- **Feature** (`features/<f>/<f>.md`) — root description of a feature folder. The canonical contract once `settled`. Status: `draft` / `settled` / `implemented` / `superseded` / `abandoned`.
- **Model** (`features/<f>/models/`) — durable design of a sub-system. Top half describes (Problem / Concepts / Contracts / Invariants / Examples / Open follow-ups); bottom half (below `---`) records provenance (Options considered / Rationale / Consequences / optional ADR link). Strict section order; the linter validates. Status as Feature.
- **Sample** (`support/samples/`) — concrete reference instance projected onto the design. Status: `draft` / `locked`. Lock requires settled abstractions, linter passes against the sample, ≥ 1 session stable on contract surface.
- **ADR** (`decisions/NNNN-<slug>.md`) — one atomic project-wide / cross-cutting decision per file. Status: `proposed` / `accepted` / `superseded by NNNN`. Feature-internal decisions don't get an ADR — they collapse into the Model's provenance section.
- **Checkpoint** (`SESSION-CHECKPOINT.md`) — single live spine. Continuously updated as work happens.

(*Plan* in `PLANS/` — implementation slices. Internals deliberately not workflow-specified.)
(*Snapshot* in `sessions/<UTC-YYYYMMDD>-<HHMM>-<slug>.md` — opt-in milestone copy of CHECKPOINT, created via `/builder:snapshot`. Sparse archive, not a per-session log.)

## Status semantics

| Status | Means | Treat as |
|---|---|---|
| `draft` | In flight; design not committed | Provisional. Edit freely. |
| `settled` | Research gate passed; this **is** the contract | Authoritative. Edits surface in linter. |
| `implemented` | Build gate passed; code in `Assets/` matches | Authoritative for both. Edits create drift against code. |
| `superseded` | Replaced by newer doc | Historical. Read the new one. |
| `abandoned` | Decided against | Historical. Body kept as record. |

### Research gate (`draft → settled`)
- Linter passes against this doc (no broken references, no structure violations).
- Contract surface (Problem / Concepts / Contracts / Invariants / Examples) stable ≥ 2 sessions.
- All glossary terms it uses appear in `ledgers/GLOSSARY.md`.

### Build gate (`settled → implemented`)
- Status is currently `settled`.
- Code in `Assets/` satisfies the contract (a sample exercises it and produces the documented behavior).
- No open drift against the code.

### Feature ↔ Model coupling
A Feature can't sit at a higher tier than its lowest *qualifying* Model (qualifying = `settled` or `implemented`; drafts/abandoneds/supersededs don't count). Forward flips are deliberate human actions; backward reverts (drift forces a Model from `implemented` → `settled`) are automatic.

---

## Two ledgers

`ledgers/` carries two independent ledgers. Distinct shapes and lifecycles. Don't dual-write.

### `OPEN-ITEMS.md` — loose ends

Single ledger of durable opens — design questions, deferred work, known TODOs.

- **Entries** are self-contained paragraphs (not one-liners). Each tells the full story: what's known, what's open, why it matters, links to related docs.
- **Optional `kind:`** tag per entry — `question` (we don't know yet), `task` (we know what to do, deferred), `defer` (decided not now, revisit on trigger).
- **Resolution = delete the entry.** No strikethrough archive. The resolving doc (Model section, ADR, feature note for "we're not doing this") owns the answer. Git keeps deletion history.

### `GLOSSARY.md` — vocabulary

Canonical terms with definitions and a changelog. Renames go through `/builder:promote-glossary` (also writes `_workflow/retired-tokens.json` for the linter).

### `FEATURE-MAP.md`

Bird's-eye view: feature index table, mermaid dependency graph, model-level breakdown. Hand-maintained for now (auto-generation tracked in OPEN-ITEMS as `bl-feature-map-generator`).

---

## Sessions

`SESSION-CHECKPOINT.md` is the **only spine.** No open/close lifecycle, no per-session artifact, no schema. The agent keeps the checkpoint current as work happens.

- **`/builder:start-session`** — read-only briefing. Reads CHECKPOINT + ledgers, prints "where we are / suggested threads," waits for direction.
- **During** — branch freely. Update CHECKPOINT as decisions land. Ephemeral mid-conversation items go in CHECKPOINT's `## Scratch` section.
- **`/builder:end-session`** — interactive triage routine. Runs linter + freshness check + chat-log scan; surfaces findings as prompts (fix / defer to OPEN-ITEMS / ignore).
- **`/builder:snapshot`** — opt-in milestone copy of CHECKPOINT into `sessions/<UTC-YYYYMMDD>-<HHMM>-<slug>.md`. Use when freezing a state is worth referencing later.

---

## When you add a feature

Mid-project (not bootstrap), a new feature is a deliberate event with a four-step ceremony:

1. Create `features/<feature>/` with a `models/` subfolder. (No `decisions/` subfolder — feature-internal decisions live in Model provenance.)
2. Write the feature root stub (`<feature>.md`) — `Purpose` + `Boundaries` is enough to start.
3. Identify upstream features. The first Model under this feature carries `references:` pointing at upstream Models or the literal `root`.
4. Patch `ledgers/FEATURE-MAP.md` — promote the corresponding `considered` row, or add a new feature row + edges. Patch `general/ROADMAP.md` if scheduling shifts.

---

## Folder layout

```
Product/
  README.md                  # this file (operating manual)
  SESSION-CHECKPOINT.md      # live spine — updated continuously
  sessions/                  # sparse milestone archive (snapshots only)
  _archive/                  # retired ledgers, legacy session notes, etc.
  _workflow/
    PRODUCT-BUILDER.md       # the formal spec — workflow rules + rationale
    REFERENCE-CONVENTIONS.md
    EMOJI-LEGEND.md
    retired-tokens.json
    _templates/              # 6 doc-type templates
  ledgers/
    OPEN-ITEMS.md            # durable loose ends
    GLOSSARY.md              # canonical vocabulary + changelog
    FEATURE-MAP.md           # bird's-eye view + mermaid graph
  general/
    VISION / GAME-ARCHETYPES / ARCHITECTURE / ROADMAP /
    RECOMMENDATIONS         # orientation
  decisions/NNNN-<slug>.md   # project-wide ADRs (cross-cutting only)
  features/<feature>/
    <feature>.md             # feature root (Purpose / Boundaries / Composition / …)
    models/NN-<slug>.md      # Models (provenance section absorbs feature-internal decisions)
    archive/                 # superseded / cancelled docs in this feature
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
2. **Edit the Model.** Update `## Contracts`, `## Invariants`, and the affected `## Examples`. If the change required weighing alternatives, add a sub-section under `## Options considered` capturing the decision.
3. **Walk downstream.** Run `/builder:deps Product/features/actions/models/01-command-pipeline.md`. Reverse-graph output: two dependents (`features/effects/models/01-effect.md`, `support/samples/pile-counter.md`).
4. **Fix in place.** Open each dependent and reconcile to the new contract. If a dependent needs *design judgment* rather than mechanical edit, open an OPEN-ITEMS entry instead.
5. **Status check.** If `01-command-pipeline.md` was `implemented`, the change creates drift against `Assets/` code — flag this to the human; either patch the code or revert the Model to `settled`. If `settled`, the edit is a deliberate cascade event but status stays.
6. **Update CHECKPOINT.** Reflect the changed state in `SESSION-CHECKPOINT.md` if the active focus shifted.
7. **Commit + PR** on a `claude/<topic>` branch.

The point is that step 3 is *visible*. Surfacing the dependents on demand replaces the old "track every cascade in a ledger" ceremony.

---

## When you need more depth

- **The full spec with rationale:** [`_workflow/PRODUCT-BUILDER.md`](_workflow/PRODUCT-BUILDER.md). Open this only when the workflow itself is in question — not for daily operation.
- **Templates:** [`_workflow/_templates/`](_workflow/_templates/) — 6 doc types.
- **Why a particular thing was decided (feature-internal):** the relevant Model's provenance section (below the `---` divider).
- **Why a cross-cutting decision was made:** the relevant ADR in `decisions/`.
- **Bird's-eye view of features and dependencies:** [`ledgers/FEATURE-MAP.md`](ledgers/FEATURE-MAP.md).
- **Linter / dependency-graph queries:** `/builder:lint`, `/builder:deps <path>`.
