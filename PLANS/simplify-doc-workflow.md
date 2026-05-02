---
type: plan
status: drafting
tags: [#workflow, #simplification]
---

# Simplify doc workflow — cleanup sketch

Working doc for the workflow simplification pass. Captures locked decisions, pending feedback, and the rough edit set. Updated as the conversation continues.

## Diagnosis (recap)

The artifacts (Models, Features, ADRs, ledgers, glossary, sessions) earn their keep. What's burning is the **forcing-function machinery** that turns those artifacts into mandatory next-session shapes:

- `§5.7 step 1b` retired-token grep auto-opens cascade entries; many are false positives.
- `§5.8` threshold trips on its own commit, so a single rich design session forces the next session into pass-mode.
- Session-note "active question coming in" gets copied verbatim across automated passes, lying about continuity.
- Per-edit ceremony stacks (Model edit + ADR + glossary + ledger + session-note assertion) for even small decisions.
- The workflow doc itself has churned ~10 times in the last 50 commits, eroding the stable mental model.

**Core insight:** the docs are now well-structured enough that the *references themselves* are the index. ALIGNMENT-DEBT was a workaround for an earlier era when docs weren't indexed. With proper structure, cascade is *detected on demand*, not *tracked*.

Companion line: if you noticed it's broken enough to write a ledger entry, you've already done the expensive part — describing what needs to change. Just change it.

---

## Locked decisions (do these)

### 1. One reference field — `references:`

Replace `builds-on:` with a single `references:` field. Drop the `depends-on` / `informed-by` split — direction matters in the graph; flavor doesn't change behavior. If a typed split ever proves load-bearing later, it's an additive change.

```yaml
references: [features/entities/01, features/cards/01]
```

### 2. Drop `last-updated:` from front matter

Git is the source of truth. Front-matter `last-updated:` always drifts. The linter computes it from `git log -1 --format=%cs <path>` when needed. If we want it visible in-doc for eyeballing, the linter mirrors it back in — but the authoritative answer comes from git, always.

### 3. ADRs become project-wide-only

Reserve ADRs for project-wide / cross-cutting decisions (Objects-over-IDs is the model citizen). Feature-internal decisions collapse into the relevant Model's provenance section (`## Options considered`, `## Rationale`, `## Consequences`).

- **Kills:** `features/<feature>/decisions/` directories.
- **Survives:** top-level `decisions/` for cross-cutting ADRs only (~5–10 total project-lifetime).
- **Tradeoff:** Model provenance sections grow to absorb prior feature-ADR content. One file with 4 decisions in narrative order beats 4 ADR files plus the Model.

**Open sub-task:** triage every existing ADR. Each one is either:
  - **Cross-cutting** — stays as project-wide ADR (move to `Product/decisions/` if not already there).
  - **Feature-internal** — content folds into the related Model's provenance section; ADR file archived with a forward link.
  - **Already obsolete** — archived without migration.

### 4. Strict Model template structure

Canonical section order, linter-enforced. Every Model has, in this order:

```
## Problem
## Concepts
## Contracts
## Invariants
## Examples
## Open follow-ups
---
## Options considered
## Rationale
## Consequences
## ADR  (optional — present only if a project-wide ADR was opened)
```

Sections may be empty with a stub (`*n/a — purely descriptive Model.*`) but cannot be reordered or omitted. Linter validates presence + order. Variable *size* is fine; variable *structure* is not.

### 5. Retire ALIGNMENT-DEBT + debt-pass + threshold

Cascade is detected on demand by the linter, not tracked.

- Retire `Product/ledgers/ALIGNMENT-DEBT.md` (move to `Product/_archive/ledgers/ALIGNMENT-DEBT.md`; preserve content; no new entries).
- Retire `/builder:debt-pass` skill.
- Retire `.github/workflows/debt-pass.yml` (already disabled in PR #82; this step deletes the file).
- Retire `PLANS/auto-debt-pass.md` (mark `status: abandoned`).
- Drop §5.5, §5.7, §5.8, §5.9 (alignment-debt-as-focus + cascade walking) from PRODUCT-BUILDER.md.
- Replace with a one-paragraph rule: *"Cascade is detected, not tracked. Run the linter or `/builder:deps`. Fix in place. If a cascade needs design judgment rather than a mechanical edit, open an OPEN-ITEMS entry."*

The OPEN-ITEMS ledger absorbs the one edge case (cascade-needs-design vs cascade-is-mechanical) — no separate machinery.

### 6. Lighten session workflow — checkpoint is the only spine

Session notes were earned by one bad day and grew disproportionate to actual use. Audit of the commit log: every recent `sessions/` commit is *writing* a note or fixing its metadata; almost none read one back to recover content. The five jobs session notes did:

- Active-question handoff → checkpoint does this.
- Decision audit trail → git history + commit messages + Model provenance (per #3 ADR collapse) do this.
- Sub-problem / scratch trail → unique value, but rarely re-read; lives in live conversation transcript.
- Citation anchor → commit SHA / Model section / OPEN-QUESTIONS id work fine.
- Projection input for parallel-sessions merge → built for a workflow that didn't pan out.

Four of five jobs are double-bookkept. Cut.

**New shape:**

- **One live working doc** (`SESSION-CHECKPOINT.md`, or rename to `CURRENT.md`). Continuously updated as decisions land — not at end-of-session. No open/close lifecycle, no `started-at`, no `parent`, no `head/merged/batch`, no schema-version.
- **`/builder:start-session`** — lightweight. Reads CURRENT + ledgers, prints "where we are / what's next." Creates no artifact.
- **`/builder:end-session`** — runs linter + freshness check + chat-log scan, surfaces findings as interactive prompts (per G5). Creates no artifact. Name kept (rename has churn cost).
- **`/builder:snapshot`** — opt-in. When user says "this state is a milestone," copies CURRENT into `sessions/<date>-<slug>.md`. Otherwise nothing lands there.
- **`Product/sessions/`** becomes a sparse milestone archive. Legacy per-session notes move to `Product/_archive/sessions/` (per G2) — `sessions/` starts empty so first snapshots aren't anchored to legacy style.

**Kills:**
- Session-note schema (§4.6).
- Session lifecycle states (in-progress / completed / merged / discarded).
- `--discard` flag.
- Pre-push hook for in-progress note checkpointing.
- Orphan-cleanup pass in start-session.
- §5.3 collapses to a paragraph.
- `PLANS/parallel-sessions.md` → mark `status: abandoned`. Merge machinery was built around session notes; nothing to merge except git itself.

**Discipline shift:** the rule changes from *"session note is the trail; checkpoint is the spine"* to *"checkpoint is the only spine; agent keeps it current as work happens."* Slightly more discipline mid-session, much less ceremony around boundaries.

### 7. Unify OPEN-QUESTIONS + BACKLOG → one ledger

The doctrinal split (questions are *I don't know*; backlog is *I know but not doing*) is fuzzy in practice. Collapse to one ledger of loose ends.

- **One file:** `Product/ledgers/OPEN-ITEMS.md` (replaces both `OPEN-QUESTIONS.md` and `BACKLOG.md`).
- **Self-contained entries** — each is a paragraph, not a one-liner. What's known, what's open, why it matters, links to related docs. Reading any single entry tells the full story. Higher cost-per-entry is itself a forcing function — only open one when it's worth tracking.
- **Optional `kind:` tag** per entry (`question` / `task` / `defer`) if filtering is useful. File is one regardless.
- **Resolution = delete the entry.** No strikethrough archive. The resolving doc owns the answer (Model section, ADR, feature note for "we're not doing this," etc.). Git keeps deletion history if ever needed: `git log --diff-filter=D ledgers/OPEN-ITEMS.md`.
- **Ephemeral mid-conversation questions** live in CURRENT.md `## Scratch` section (per #6). End of session: scratch gets answered (delete), promoted to OPEN-ITEMS (worth tracking), or left for next session.

**Kills:**
- `OPEN-QUESTIONS.md` + `BACKLOG.md` — merged.
- Strikethrough-on-resolve convention.
- "Three lifetimes, three homes" table in §6 — collapses to two homes (OPEN-ITEMS for durable, CURRENT.md for active + ephemeral).
- Doctrinal split text in §6 ("Three ledgers, three shapes") — rewrite as two (OPEN-ITEMS + GLOSSARY; ALIGNMENT-DEBT gone per #5; FEATURE-MAP earmarked for derivation per future considerations).

---

## Future considerations (not locked)

Captured here so we don't lose them. Revisit after the locked cleanup lands and the workflow has stabilized for several sessions.

### FEATURE-MAP as a derived dependency graph

Today `FEATURE-MAP.md` is hand-maintained. Once the linter exposes the reverse reference graph (per #4), most of FEATURE-MAP becomes derivable. The end vision the user wants:

- One canonical doc showing every feature, its Models, and its dependencies.
- Edges at two granularities: feature → feature (rolled-up), and model → model (fine-grained).
- A model in feature C may reference a model in feature A even if the *features* don't depend on each other — that needs to be visible. The reverse-graph data already supports this; the rendering needs to expose both layers.
- The graph is load-bearing for **build ordering**: when implementation starts, walk the dependency graph bottom-up to find the path of least resistance — implement the leaves first, then their consumers, etc.

Likely shape: linter generates `Product/ledgers/FEATURE-MAP.md` from `references:` data + folder structure. Hand-maintained pieces (descriptions, status annotations) live in a separate small input file the linter reads and merges. Mermaid render auto-updated.

Defer until the locked cleanup lands and the user wants to do the graph work in earnest on this project.

### Archive strategy overall

Today archives are scattered: per-feature `archive/` subfolders (per §5.14), `RESEARCH_BACKUP/` at repo root, and now `Product/_archive/sessions/` (provisional, per G2). No coherent story. Worth a focused pass after the locked cleanup lands:

- Single global archive root vs. per-area archive subfolders?
- What goes where (decisions, models, samples, sessions, raw research)?
- Lint rules (§5.14 archive integrity) need updating to whatever the new shape is.
- Possibly migrate `RESEARCH_BACKUP/` into the new structure.

Defer until decisions #1–#7 land and the doc shape is stable.

### MCP-backed CURRENT.md for cross-machine / parallel sessions

Earlier proposal: store CURRENT in an MCP server so multiple sessions / machines share one source of truth. Hydrate at session start, push back at session end.

Deferred — solves a problem that doesn't exist today (solo work, one machine, one active session). Adds infrastructure cost and loses git audit trail for the spine. Revisit if/when parallel sessions become a real workflow.

---

## Linter responsibilities (consolidated)

`/builder:lint` becomes the single on-demand consistency tool. Responsibilities:

1. **Structure check** — every Model has the canonical sections in the canonical order (per #4 above).
2. **References resolve** — every `references:` path exists; every inline path-shaped link in the body exists.
3. **Header–body consistency** — auto-populate `references:` from path-shaped tokens in the body; flag mismatches.
4. **Retired-tokens check** — grep for tokens in `Product/_workflow/retired-tokens.json`; report hits with their replacement.
5. **Reverse graph** — compute "who references this" from the union of all `references:` fields. Expose as `/builder:deps <path>`.
6. **Git-derived dates** — `last-updated` per doc on demand from `git log`.

Step zero before adding new responsibilities: **harden the linter against silent failures.** Concretely:
- Explicit non-zero exit code on any internal error (file not found, parse failure, etc.) — no silent catch-and-continue.
- Structured output (machine-readable summary line: `lint: P passed, F failed, S skipped`) so absence of output = visible failure.
- Smoke test against a known-broken fixture committed under `.claude/skills/builder/lint/fixtures/` — at least one Model with a broken `references:` and one with a structure violation. CI or manual run validates the linter still flags them.
- Continue-on-error for *findings* (so one broken doc doesn't stop the rest of the scan), but exit-on-error for *infrastructure failures* (missing input dir, malformed front matter the linter itself can't parse). The two failure modes need different treatment.

---

## Edit set (rough scope)

One PR per locked decision is too granular; one PR for everything is too risky. Suggested grouping:

**PR 1 — front matter consolidation**
- `references:` replaces `builds-on:` repo-wide (mechanical rename + schema doc update).
- Drop `last-updated:` from front matter repo-wide.
- Update §3 of `PRODUCT-BUILDER.md` to match.
- Update all 7 templates in `_workflow/_templates/`.

**PR 2 — retire ALIGNMENT-DEBT machinery**
- Delete `.github/workflows/debt-pass.yml`.
- Mark `PLANS/auto-debt-pass.md` `status: abandoned`.
- Remove `/builder:debt-pass` skill.
- Archive `Product/ledgers/ALIGNMENT-DEBT.md` → `Product/_archive/ledgers/ALIGNMENT-DEBT.md` (preserve content).
- Drop §5.7, §5.8, §5.9 from `PRODUCT-BUILDER.md`; add the one-paragraph "detected, not tracked" rule.
- Drop `debt: N opened` assertion from session-note template.

**PR 3+4 — strict template + linter + per-feature Model/ADR rollout** *(merged per G3)*
- *Commit 1:* define strict Model template (new front matter, canonical section order, provenance shape that absorbs feature-internal ADRs); update §4.3 of `PRODUCT-BUILDER.md`; update §4.5 for new ADR scope; update §5 cross-references.
- *Commit 2:* extend linter — structure check, header-body reference sync, reverse graph, git-derived dates, retired-tokens validation; harden against silent failures; add `/builder:deps <path>`.
- *Commits 3..N (one per feature):* for each `features/<name>/`:
  - Rewrite every Model into the new template.
  - Fold each sibling feature-internal ADR into the relevant Model's provenance section.
  - Archive ADR file to `Product/_archive/decisions/<feature>/<NNNN>-<slug>.md` (preserves feature scoping in archive); ADR keeps a forward link to the absorbing Model section.
  - Move any cross-cutting ADRs to `Product/decisions/` (criterion per #3 — applies framework-wide, cited from multiple features, deserves a one-pager).
  - Delete empty `features/<feature>/decisions/` directory.
- *Commit final:* run linter against full tree; resolve any reference-resolution failures (broken paths, missing references, structure violations).

**PR 5a — ledger consolidation**
- Create `Product/ledgers/OPEN-ITEMS.md`; merge `OPEN-QUESTIONS.md` + `BACKLOG.md` content (drop strikethrough archive — those entries are already resolved).
- Update §6 of `PRODUCT-BUILDER.md`: collapse "three ledgers, three shapes" → two; collapse "three lifetimes, three homes" → two.
- Drop ledger entry shape blocks for OQ + BACKLOG; add the new OPEN-ITEMS shape (paragraph-style entries).
- Update any cross-references (e.g., session note template references) to point at OPEN-ITEMS.

**PR 5b — session workflow lightening**
- Strip `SESSION-CHECKPOINT.md` front matter (drop schema-version / head / merged / batch).
- Rewrite `/builder:start-session` skill — read-only briefing, no artifact creation, no orphan cleanup.
- Rewrite `/builder:end-session` skill — runs linter + freshness check + chat-log scan; surfaces findings as interactive prompts (per G5). Preserves existing chat-log scan for unsaved sketches (per G1). No session-note creation. Name kept.
- Add `/builder:snapshot` skill — opt-in copy of CURRENT into `sessions/<date>-<slug>.md`.
- Drop §4.6 (session-note template) from PRODUCT-BUILDER.md.
- Collapse §5.1–§5.3 (start / during / end of session) to a single paragraph: *"checkpoint is the only spine; agent keeps it current as work happens."*
- Mark `PLANS/parallel-sessions.md` `status: abandoned`.
- Remove pre-push hook for in-progress session notes (`.githooks/pre-push`).
- Move existing session notes `Product/sessions/session-*.md` → `Product/_archive/sessions/` (per G2). Add `Product/sessions/README.md` with the cutover note.

**PR 6 — root instruction refresh** *(follow-up per G6)*
- Update `AGENTS.md` to reflect the steady-state workflow (no debt-pass, no session lifecycle, OPEN-ITEMS, etc.).
- Update root `CLAUDE.md` likewise; ensure project-rules section still applies.
- Verify no stale references to deleted commands/skills/ledgers.

Order: PR 1 → PR 2 → PR 5a → PR 5b → PR 3+4 → PR 6.

Rationale for ordering: front matter rename first (mechanical, repo-wide). Then ALIGNMENT-DEBT retirement (decoupled). Then ledger consolidation (decoupled). Then session lightening (decoupled, high relief). Finally the combined template + linter + per-feature Model/ADR rollout — the heaviest PR, but coherent because Model rewrites and ADR folds happen in lockstep per feature.

Or collapse to one PR if we trust the diff. TBD when sketching.

---

## Inventory — what changes when all 7 decisions land

### Slash commands (`.claude/commands/builder/`)

| Command | Action | Driven by | Notes |
|---|---|---|---|
| `bootstrap` | Edit (medium) | all | Update once-per-project scaffold to new shape |
| `start-session` | Edit (shrink) | #6 | Read-only briefing; no artifact creation, no orphan cleanup |
| `end-session` | Edit (shrink) | #6, #1, #5 | Runs linter + freshness check + chat-log scan for unsaved sketches; surfaces findings as interactive prompts (fix / defer to OPEN-ITEMS / ignore). No session-note write. Drop `--discard`. Keep name |
| `new` | Edit | #3, #4 | Drop session + (feature-internal) ADR templates from picker. For Models: writes the canonical section headers as empty stubs with `*n/a — fill in*` placeholders so the agent fills content into the predetermined structure |
| `design-pass` | Edit (light) | #7 | No in-flight persistence — verdicts live in conversation context; on user "apply," patches go directly into affected Model(s) in one follow-up turn |
| `archive` | Edit (light) | #5, #6 | Drop ALIGNMENT-DEBT integration; session lifecycle awareness gone |
| `promote-glossary` | Edit (light) | #6 | Drop branch-annotation from changelog; possibly absorb retired-tokens append (see G4) |
| `merge-pass` | Delete | #6 | Parallel sessions abandoned |
| `project-checkpoint` | Delete | #6 | Was projection helper; CURRENT is hand-managed now |
| `debt-pass` | Delete | #5 | Cascade ledger gone |

### Skills (`.claude/skills/`)

| Skill | Action | Driven by | Notes |
|---|---|---|---|
| `builder/lint` | Edit (significant) | #1, #2, #4, #5 | Drop ALIGNMENT-DEBT writes (becomes a report). Add: structure check, header-body sync, reverse graph + `/builder:deps`, git-derived `last-updated`, retired-tokens validation. **Step zero: harden against silent failures** |
| `builder/lint-workspace/` | Keep in place | — | Eval data tied to lint-skill iteration history; lives next to the skill, not a doc artifact |

### Hooks / GHA

| Item | Action | Driven by |
|---|---|---|
| `.githooks/pre-push` | Delete | #6 |
| `.github/workflows/debt-pass.yml` | Delete | #5 (already disabled) |

### Templates (`Product/_workflow/_templates/`)

| Template | Action | Driven by |
|---|---|---|
| `model.md` | Edit (significant) — new front matter, strict section order, provenance absorbs feature-internal ADRs | #1, #2, #3, #4 |
| `feature.md` | Edit — front matter; drop `decisions/` pointer | #1, #2, #3 |
| `adr.md` | Edit — re-scope to project-wide only | #3 |
| `spike.md` | Edit (light) — front-matter cleanup | #1, #2 |
| `sample.md` | Edit (light) — front-matter cleanup | #1, #2 |
| `checkpoint.md` | Edit (heavy) — strip lifecycle metadata; add `## Scratch` | #6 |
| `session.md` | Delete | #6 |

### Other workflow docs (`Product/_workflow/`)

| Doc | Action |
|---|---|
| `REFERENCE-CONVENTIONS.md` | Edit (light) — collapse to one `references:` field per #1; drop `builds-on:` discussion |
| `EMOJI-LEGEND.md` | Keep as-is |
| `retired-tokens.json` | Keep — writer changes from debt-pass to `/builder:promote-glossary` per G4 |

### Project content (`Product/general/`, `Product/support/`)

| Area | Action |
|---|---|
| `general/*.md` (VISION, ARCHITECTURE, ROADMAP, RECOMMENDATIONS, etc.) | Subject to PR 1 front-matter rename only; content unchanged |
| `support/samples/*` | Subject to PR 1 front-matter rename; otherwise unchanged |
| `support/prior-art/*` | Same |
| `Product/spikes/*` | Same |
| `Product/decisions/*` (project-wide ADRs) | Subject to PR 1 front-matter rename; otherwise unchanged (these are the surviving ADR home per #3) |

### Ledgers (`Product/ledgers/`)

| Ledger | Action |
|---|---|
| `ALIGNMENT-DEBT.md` | Archive (#5) |
| `OPEN-QUESTIONS.md` + `BACKLOG.md` | Merge → delete originals; new `OPEN-ITEMS.md` (#7) |
| `GLOSSARY.md` | Edit (light) — drop branch annotations from changelog |
| `FEATURE-MAP.md` | Keep as-is — derivation deferred (future considerations) |

### Plans (`PLANS/`)

| Plan | Action |
|---|---|
| `auto-debt-pass.md` | Status: abandoned |
| `parallel-sessions.md` | Status: abandoned |
| `simplify-doc-workflow.md` | Live |

### Workflow doc (`Product/_workflow/PRODUCT-BUILDER.md`)

| Section | Action |
|---|---|
| §3 Front matter | Rewrite — `references:`, drop `last-updated:` and `builds-on:` |
| §4 Templates | Rewrite — strict Model structure, ADR scope change, drop session template |
| §5.0 Bootstrap | Edit — new ledger shape (OPEN-ITEMS only), drop pre-push install, drop ALIGNMENT-DEBT init |
| §5.1 Starting a session | Collapse — one sentence: "read CURRENT + ledgers; see what's next" |
| §5.2 During a session | Collapse — one paragraph: "agent keeps CURRENT current as work happens; ephemerals to `## Scratch`; design walks hold verdicts in context" |
| §5.3 Ending a session | Collapse — one paragraph: "`/builder:end-session` runs linter + freshness check + chat-log scan; surfaces findings interactively" |
| §5.4 Reopening decisions | Edit (light) — supersession survives; cascade walk on-demand |
| §5.5 Alignment-debt as focus | Delete entirely — debt-pass is gone |
| §5.6 Status gates | Keep |
| §5.7–§5.9 Cascade machinery | Delete — replaced by one paragraph |
| §5.10 Linter | Rewrite — expanded responsibilities |
| §5.11 New feature | Edit — drop `decisions/` subfolder creation |
| §5.12 Drift detection | Keep |
| §5.13 Lifecycle reference | Edit — automation table shrinks |
| §5.14 Archive convention | Keep (light edit) |
| §6 Cross-cutting | Rewrite — three ledgers → two, three lifetimes → two |

### Settings

| File | Action |
|---|---|
| `.claude/settings.json` | Keep |
| `.claude/settings.local.json` | Edit (light) — clean up stale Windows path permissions |

### To create

| New surface | Why | Decision |
|---|---|---|
| `/builder:snapshot` | Opt-in milestone — copy CURRENT to `sessions/<date>-<slug>.md` | #6 |
| `/builder:deps <path>` | Forward + reverse reference walk for one doc | #4 |
| `Product/ledgers/OPEN-ITEMS.md` | The unified ledger | #7 |

---

## Open gaps

To lock one-by-one before edit work starts. Each gets a verdict line appended below.

| # | Gap | Status |
|---|---|---|
| G1 | CURRENT.md freshness enforcement (discipline / soft warn / pre-commit) | **locked: soft warn at end-session.** Keep `end-session`'s existing chat-log scan for unsaved sketches — high value, preserve. Add a freshness check: compare CURRENT's git timestamp against touched `Product/features/**` and `Product/ledgers/**` files; warn (don't block) if CURRENT is stale. Escalate to pre-commit hook only if soft warn proves insufficient. |
| G2 | Existing session notes — leave or quarantine | **locked: move legacy notes to global archive + add README.** Move all existing `Product/sessions/session-*.md` to `Product/_archive/sessions/` (provisional location — see future considerations on archive strategy). Leaves `Product/sessions/` empty so the first on-demand snapshots aren't anchored to legacy style. Add a one-line README in `Product/sessions/` explaining: *"Snapshots are opt-in milestones via `/builder:snapshot`. Legacy per-session notes archived to `Product/_archive/sessions/`."* |
| G3 | ADR migration hand-work — triage scope | **locked: one big PR, staged commit-by-feature, combined with Model template rollout.** PR 3 and PR 4 merge: define new strict Model template + harden linter as the first commit, then walk feature-by-feature — for each feature, one commit that rewrites every Model into the new template *and* folds sibling ADRs into the provenance sections in the same pass. Project-wide ADRs (cross-cutting) stay in top-level `Product/decisions/`. Feature-internal ADR files archived with a forward link to the absorbing Model section after fold. |
| G4 | Retired-tokens registry — who writes it now | **locked: `/builder:promote-glossary` writes it on rename.** Existing `<old-term> -> <new-term>` syntax extended to also append `Product/_workflow/retired-tokens.json` (term, retired date, replacement, one-line context). Add `--rename-only` mode for token renames that don't touch GLOSSARY (rare). Linter consumes the registry for actionable error messages ("X was retired YYYY-MM-DD; use Y instead"). |
| G5 | `end-session` linter integration — gate or soft | **locked: interactive triage.** There is no "closing" a session anymore (no per-session artifact to gate). `end-session` is just a routine bundle — runs linter + freshness check + chat-log scan for unsaved sketches. Findings surface as interactive prompts: *"linter found N issues; freshness check found M; chat scan found K unsaved sketches — fix now / defer to OPEN-ITEMS / ignore?"* Agent waits for direction per category. Not a refusal-style gate; an interaction point that the user routes through. |
| G6 | `AGENTS.md` + root `CLAUDE.md` — final reference sweep | **locked: separate follow-up PR after main cleanup.** Land all cleanup PRs first; then one small PR refreshes both root instruction files against the steady-state workflow. Avoids rewriting the same sections multiple times as decisions land in different PRs, and produces a single coherent before/after diff. |
| G7 | `design-pass` output discipline — new target shape | **locked: no in-flight persistence — conversation context is the buffer.** Design-pass walks Q-by-Q, locks verdicts in the active conversation, never writes to a buffer file mid-walk. Verdicts often revise during a walk (you discover something's wrong while answering a later Q); persisting in-flight wastes tokens and forces buffer rewrites. **Apply trigger:** when the walk concludes and the user says "apply" (or equivalent), the agent patches each affected Model in a single follow-up turn. If a walk gets interrupted before apply, verdicts evaporate; that's correct (locks were tentative, re-walking refines). Same risk profile as `## Scratch` ephemerals. |

---

## Pending feedback (more to come)

User flagged additional thoughts coming. Add them here as they land before we move from sketch → execution.

- *(reserved)*

---

## Out of scope for this pass

- Drift detection (post-merge code-vs-contract automation, §5.12). Orthogonal; not affected by these changes.
- Status state machine (§5.13). Not affected.
- §5.14 Archive convention (per-feature `archive/`). Light edit only; broader archive rethink deferred to future considerations.

## Success criteria

Done when **all** of:
1. PRs 1, 2, 5a, 5b, 3+4, 6 merged to main.
2. `/builder:lint` passes against the full `Product/` tree (no broken references, no structure violations, no unresolved retired tokens).
3. One real working session (any new design or implementation work) runs cleanly under the new shape — start with `/builder:start-session`, work, end with `/builder:end-session` — without falling back to deleted commands or stale conventions.
4. `AGENTS.md` and root `CLAUDE.md` describe the steady-state workflow (no stale references to deleted artifacts).
