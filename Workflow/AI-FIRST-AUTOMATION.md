---
type: spike
status: active
last-updated: 2026-04-29
builds-on: [WORKFLOW-PROPOSAL.md, FLOW-SIMULATION.md, MIGRATION-GUIDE.md]
tags: [#workflow, #automation, #ai-first]
---

# AI-First Automation — Skills, Workflows, and Hooks for the Proposed Workflow

**Premise.** The proposed workflow assumes a human (or AI) navigating it by convention. But this project is AI-collaborated end-to-end — most of the work in `RESEARCH/` was produced in Claude Code sessions. If we leave the workflow as pure convention, every session re-pays the cost of remembering it. If we encode the conventions as automations — slash commands, hooks, and a small library of skills — the workflow runs *itself* most of the time, and the human only intervenes for judgment calls.

This is a spike: we're not committing to any of these. The premise is "what would AI-first look like?" The findings should let us pick the high-leverage subset to actually build.

---

## 1. Design principles

Before listing automations, the principles they should obey:

1. **Generative over enforcing.** Skills that *create* the right artifact (templates, scaffolding, debt-tracking entries) outperform hooks that *block* the wrong artifact. Friction kills adoption.
2. **Mechanical work first, judgment work never.** Automate alignment-debt scanning, doc-template scaffolding, glossary changelog updates, status transitions. Don't automate "is this the right ADR to write" — that's the human/AI thinking part.
3. **Cheap to invoke.** A slash command beats a hook beats a separate script. If a skill takes more than 30 seconds of friction to use, it won't get used.
4. **Inspectable output.** Every automation produces visible diffs the user reviews before committing. No silent edits.
5. **Idempotent.** Running a skill twice produces the same result. No "already ran" footguns.
6. **Composable.** A "settle decision" skill can call a "write ADR" skill and a "patch glossary" skill. Don't build monoliths.

---

## 2. Catalog

The full automation surface in one table. Single source of truth for "what exists, what's planned, what's stale." Status legend:

- **documented** — full inline writeup below.
- **sketched** — one-line idea or partial spec; needs detail before building.
- **superseded** — replaced or merged into another entry.

| Name | Type | Trigger | Status | Section |
|------|------|---------|--------|---------|
| `/research:bootstrap` | command | New project (once-per-project) | sketched | §3 |
| `/research:start-session` | command | Beginning of every session | documented | §3 |
| `/research:new <type> <feature> <title>` | command | Creating a doc | documented | §3 |
| `/research:settle-decision <model-path>` | command | Model decision becomes ADR-worthy | documented | §3 |
| `/research:open-debt <reason> <paths…>` | command | Model edit cascades | documented | §3 |
| `/research:close-debt <entry-id>` | command | Reconciling debt | documented | §3 |
| `/research:supersede-cascade <old-adr> <new-adr>` | command | ADR superseded | sketched | §3 |
| `/research:abandon-model <model-path> <reason>` | command | Model abandoned (Q9) | sketched | §3 |
| `/research:add-to-backlog <title>` | command | Deferring concrete work (Q7) | sketched | §3 |
| `/research:promote-glossary <term> [def]` | command | New term or rename | documented | §3 |
| `/research:rebuild-feature-map` | command | Models added/removed (Q6 deferred) | sketched | §3 |
| `/research:lint` | command | Manual or CI (Q13) | sketched | §3 |
| `/research:end-session` | command | Wrapping up a session | documented | §3 |
| `/research:promote-to-docs <model-path>` | command | Model passes promotion gate | documented | §3 |
| `PostToolUse` front-matter check | hook | Write/Edit to `RESEARCH/**.md` | sketched | §4 |
| `Stop` end-session reminder | hook | Claude stops mid-session | sketched | §4 |
| `PreToolUse` git-commit sanity | hook | Bash `git commit` in `RESEARCH/` | sketched | §4 |
| `research-debt-pass` | skill | Discharging debt at threshold | documented | §5 |
| `research-rename-cascade` | skill | Glossary rename | documented | §5 |
| `research-spike-graduate` | skill | Spike informs Model | documented | §5 |
| `research-sample-relock` | skill | Locked sample dependency changes | documented | §5 |
| `research-feature-map-rebuild` | skill | Richer FEATURE-MAP regen + analysis | sketched | §5 |
| `research-promote` | skill | End-to-end DOCS/ promotion | sketched | §5 |
| `research-explorer` | subagent | Project-scoped search | documented | §6 |
| `research-reviewer` | subagent | Pre-commit Model/ADR review | documented | §6 |

**Outstanding mechanics** (rules without a clear automation home yet) — see §9:
- Session-note `debt: N opened` assertion enforcement.
- Sample cooldown commit-message convention (`contract edit` vs. `hygiene edit`).
- ADR auto-numbering across `general/decisions/` and `features/<feature>/decisions/`.

---

## 3. Slash commands (high-leverage, build first)

Each is a `.claude/commands/<name>.md` file the user can invoke as `/research:<name>`.

### `/research:start-session`

**Trigger.** Beginning of every session.

**Behavior.**
1. Read `SESSION-CHECKPOINT.md`.
2. Read `general/OPEN-QUESTIONS.md` and `general/ALIGNMENT-DEBT.md`.
3. Identify the session counter (max `sessions/session-NN.md` + 1).
4. Surface a 5-line briefing: active question coming in, top 3 alignment-debt items, top 3 open questions. Ask the user to confirm or pivot.
5. Pre-create `sessions/session-NN.md` (draft, type=session) with the active question filled in.

**Why.** Today, every session opens with re-reading the checkpoint to recover state. This compresses that into one command. The pre-created session note means the trail starts capturing immediately, not at session-end.

### `/research:new <type> <feature> <title>`

**Trigger.** "I need a new doc."

**Behavior.**
1. Type ∈ {spike, model, sample, adr, plan}. (Session and checkpoint have their own commands.)
2. For models/ADRs: feature must be one of `features/*/`. Auto-pick next counter (`02`, `03`, …) in `notes/` or `decisions/`.
3. Spike: lands in `spikes/`, no counter, slug from title.
4. Sample: lands in `support/samples/`.
5. Generate file from `general/_templates/<type>.md` with front matter pre-filled (`type`, `status`, `last-updated`).
6. Open the file in the editor (or just Read it back).

**Why.** Prevents template drift. Enforces front matter without nagging.

### `/research:settle-decision <model-path>`

**Trigger.** A Model just made a real decision; ADR-worthy.

**Behavior.**
1. Read the Model's "Options considered" + "Rationale" + "Consequences" sections.
2. Generate an ADR draft in `features/<feature>/decisions/NNNN-<slug>.md` carrying the same content in ADR template form.
3. Patch the Model's frontmatter or trailing "ADR" link.
4. Patch `sessions/session-NN.md` "Decisions settled" list.
5. Show the user the three pending edits as a diff for review before write.

**Why.** Today, "and now write the ADR" is a manual second pass. This makes ADR-writing a one-command tax instead of a five-minute one.

### `/research:open-debt <reason> <affected-paths…>`

**Trigger.** A Model just changed; downstream docs need re-checking.

**Behavior.**
1. Append **one entry per affected doc** to `general/ALIGNMENT-DEBT.md`: source path, target path, date, reason, status `open`. (Per [WORKFLOW-PROPOSAL.md §5.7](WORKFLOW-PROPOSAL.md) the ledger is the single source of truth — no inline `## Implications` section gets written to the Model.)
2. Append a `debt: N opened` line to the current `sessions/session-NN.md` content section (or update it if a `debt:` line already exists for this edit).

**Why.** Implicit alignment debt was the workflow's killer feature in the FLOW-SIMULATION analysis. This makes opening debt cheap.

### `/research:close-debt <entry-id>`

**Trigger.** The debt-pass session is doing the actual reconciliation.

**Behavior.**
1. Strike through the entry in `ALIGNMENT-DEBT.md`, add resolution date.
2. Patch session note "followups closed" list.

**Why.** Symmetry with `open-debt`. Closing debt is mechanical; should be one command.

### `/research:promote-glossary <term> [definition]`

**Trigger.** New vocabulary just got named, or an existing term got renamed.

**Behavior.**
1. Add or update entry in `general/GLOSSARY.md`.
2. Append to glossary changelog with date and reason.
3. (Optional) `grep` for the old term across `RESEARCH/` and offer to scaffold an alignment-debt entry for the rename.

**Why.** Sessions 3, 5, 6 of FLOW-SIMULATION were all rename cascades. The bottleneck is human-eyeballed grep. Lifting it into one command shrinks rename sessions by half.

### `/research:end-session`

**Trigger.** Wrapping up.

**Behavior.**
1. Read `sessions/session-NN.md` (which was being filled live by other commands).
2. Verify both parts ≤ 50 lines. Warn if not.
3. Read all `general/*.md` for changes; flag if checkpoint isn't refreshed yet.
4. Generate refreshed `SESSION-CHECKPOINT.md` *draft* — pull active question (going out), top open questions, suggested next steps. **Critical: surface as draft for human approval, never write directly.**
5. Show a one-screen summary of session deltas: docs created, ADRs landed, debt opened/closed, glossary updates.
6. (Optional) suggest a commit message and PR title.

**Why.** End-of-session is the highest-leverage automation point — most state writes happen here, and forgetting one breaks the spine.

### `/research:promote-to-docs <model-path>`

**Trigger.** A Model passes the §5.6 promotion gate.

**Behavior.**
1. Verify the four conditions: status=settled, no open debt, referenced by an active plan or implementation, stable ≥ 2 sessions.
2. If all pass: `git mv` to `DOCS/`, update front matter, leave a tombstone in `RESEARCH/` pointing at the new home (or just rely on git history — this is a real choice for the workflow).
3. If any fail: print the failing condition, abort.

**Why.** Promotion is rare and easy to fluff. Making it a command makes it deliberate AND verified.

### `/research:lint`

**Trigger.** Manual run, CI step, or end-of-session sweep.

**Behavior.**
1. Scan `RESEARCH/` for stale references: broken paths, terms used without glossary entries, Models pointing at `superseded` ADRs without acknowledgment, missing or invalid `builds-on:` (must be path-list or literal `root`), Sample edits without the cooldown commit-message marker (§4.4).
2. For each finding: append a ledger entry to `general/ALIGNMENT-DEBT.md` with `source=linter`, target=affected doc, reason=type of staleness, status `open`.
3. Idempotent: before opening a new entry, check whether an open entry already exists for this (linter, target, reason) tuple.
4. Output structured findings to stdout + count of new entries opened.

**Why.** Q13 locked report-only enforcement. The linter feeds the ledger; the §5.8 threshold of 5 catches accumulation. Blocking commits creates bypass habits — generation-and-record beats blocking.

### `/research:rebuild-feature-map`

**Trigger.** Models added or removed; or end-of-session cleanup if `general/FEATURE-MAP.md` is suspected stale. Q6 defers active build until ~15 Models or first drift incident.

**Behavior.**
1. Walk `features/*/` to collect: feature root status, Model count, ADR count, contained docs.
2. Walk `builds-on:` across all Models; project up to feature-level edges.
3. Regenerate the three layers of `general/FEATURE-MAP.md`:
   - **Layer 1 (table):** one row per feature with purpose snippet (pulled from feature root), status, model count, deps.
   - **Layer 2 (mermaid):** dependency graph of features, edges from union of contained Models' `builds-on:`.
   - **Layer 3 (breakdown):** per-feature listing of Models, ADRs, statuses.
4. Diff against current; only write if changed.

**Why.** Sketched here so the implementation is on the BACKLOG with a clear shape. Picks up ready when the deferral trigger hits.

### `/research:supersede-cascade <old-adr> <new-adr>`

**Trigger.** An ADR is being superseded.

**Behavior.**
1. Verify both ADR paths exist; new ADR is `proposed` or `accepted`.
2. Set old ADR status to `superseded by NNNN`.
3. Walk the `builds-on:` graph: find every doc whose `builds-on:` references the old ADR or the Model that contains it.
4. For each affected doc, open a ledger entry: source=new ADR path, target=affected doc, reason="supersession of MMMM may invalidate framing."
5. Patch the new ADR's `## Consequences > Reverts/replaces` field with the cascade scope (list of affected paths).
6. Patch the current `sessions/session-NN.md` content section: `decisions settled: ADR-NNNN supersedes MMMM (cascade: N entries opened)`.

**Why.** §5.9 backtracking is impossible to do reliably by hand. Highest-value automation candidate after the open/close-debt commands. Without it, supersessions silently invalidate downstream framing.

### `/research:abandon-model <model-path> <reason>`

**Trigger.** A Model is being abandoned — the line of inquiry was wrong; no replacement exists. Distinct from supersession.

**Behavior.**
1. Verify the Model is currently `draft` (abandoning a `settled` Model is unusual — warn, but proceed).
2. Flip status to `abandoned`. Bump `last-updated`.
3. Prepend a one-line abandonment header at the top of the body: *"Status: abandoned YYYY-MM-DD — `<reason>`. Original attempt below for record."*
4. Walk `builds-on:` references: find docs that build on this Model. Open ledger entries (source=this Model, target=dependent doc, reason="abandoned upstream — re-evaluate dep").
5. Patch session note: `model abandoned: <path> (cascade: N entries opened)`.

**Why.** Q9 added `abandoned` as a distinct status. Wrong handling either loses the trail or pollutes the dep graph. Encoding the steps prevents both.

### `/research:add-to-backlog <title>`

**Trigger.** Deferring concrete work mid-session (per Q7).

**Behavior.**
1. Prompt for: title, 2-line context, trigger condition (when to revisit), references (paths or links).
2. Append entry to `general/BACKLOG.md` with date and status `open`.
3. Patch the calling session's note: record the deferral in "followups."

**Why.** Q7 added the BACKLOG ledger. Cheap-to-invoke entry-opening is the matching primitive — without it, deferred work falls back to scattering notes.

### `/research:bootstrap`

**Trigger.** Starting a new project under this workflow. Once-per-project.

**Behavior.** Walks the §5.0 8-step bootstrap interactively:
1. Ask for: project name, vision blurb, candidate features (5–12 likely).
2. Create skeleton: `general/`, `features/<each>/`, `spikes/`, `support/{samples,prior-art}/`, `sessions/`.
3. Drop all 9 templates into `general/_templates/`.
4. Initialize empty ledgers: `OPEN-QUESTIONS.md`, `ALIGNMENT-DEBT.md`, `BACKLOG.md`, `FEATURE-MAP.md` (stub with vision + empty mermaid), `GLOSSARY.md` (empty changelog).
5. Create `general/decisions/` with `0001-adopt-workflow.md` from a project template.
6. Write `general/VISION.md` from the supplied blurb.
7. Open `sessions/session-01.md` with active question coming in: *"what is this project?"*
8. Report: filesystem ready; first session can begin.

**Why.** Bootstrap is once-per-project; getting it right matters more than convenience. A skill encodes the steps so they can't be forgotten or reordered. Becomes one of the highest-leverage skills if the workflow is adopted across multiple projects.

---

## 4. Hooks (lower-leverage, build only when adoption is proven)

Hooks fire on Claude Code events. They run whether you want them or not — high cost when wrong, so use sparingly.

### `PostToolUse` on `Write`/`Edit` to `RESEARCH/**.md`

- Verify front matter exists. Block save (or warn) if missing — *warn, don't block*. Adoption matters more than purity.
- Refresh `last-updated:` automatically if the doc was changed.

### `Stop` (session-end auto-checkpoint)

- After Claude stops responding, scan for changed `RESEARCH/` files. Print a one-line reminder if `SESSION-CHECKPOINT.md` wasn't touched: *"Hint: end-session refresh not run. /research:end-session"*.
- Friction cost is real here. Build only if forgetting end-session becomes a recurring problem.

### `PreToolUse` on `Bash` for `git commit` in `RESEARCH/`

- Sanity-check: is there a session note for this session? If not, warn.
- Could be annoying. Skip unless missing-session-notes becomes a real pattern.

### Generally: hooks should warn, not block

The proposal favors generative over enforcing. Blocking hooks fight the user. Even a malformed front matter is fixable later — losing a thought because a hook ate the save is unrecoverable.

---

## 5. Skill-shaped workflows (multi-step, judgment-laden)

Skills (under `.claude/skills/<name>/`) bundle instructions + light tooling. Use when the task needs a sequence of steps with judgment at each branch.

### `research-debt-pass`

A full alignment-debt session as a skill. Steps:

1. Read `general/ALIGNMENT-DEBT.md`. Pick top entries (or honor a user-passed list).
2. For each entry, read the "implications" doc and each affected doc.
3. Propose patches as diffs. User approves each.
4. Run `/research:close-debt` per resolved entry.
5. End with a session-note draft.

This is what session 6 of FLOW-SIMULATION looks like, fully automated except for the diff approvals.

### `research-rename-cascade`

Glossary rename → cascade across the project. Steps:

1. User specifies old → new term.
2. `/research:promote-glossary` first.
3. Grep `RESEARCH/` for old term (case-sensitive AND -insensitive variants).
4. Generate per-file diff proposals.
5. Apply approved diffs in one commit per logical scope (per-feature is a reasonable grain).
6. Open alignment-debt entry per file touched, close in same session.
7. Session-note draft.

This is sessions 3 + 5 + 6 collapsed.

### `research-spike-graduate`

A spike just informed a Model. Steps:

1. User passes spike path + model path.
2. Set spike status `active` → `graduated`.
3. Add forward-link to model in spike's `## Outcome` section.
4. Add backward-link to spike in model's frontmatter `builds-on:`.
5. Confirm both edits.

Trivial but the kind of thing that's *always* forgotten. Strong candidate.

### `research-sample-relock`

A locked sample's design dependencies changed. Steps:

1. User passes sample path.
2. Read `general/ALIGNMENT-DEBT.md` for entries naming this sample.
3. Show the user the three options from §4.4 of the proposal: re-lock, unlock-to-draft, accept-divergence.
4. Apply chosen path:
   - Re-lock: re-check four-part bar, update sample, close debt entries.
   - Unlock: status `locked` → `draft`, leave note.
   - Accept-divergence: append explicit "known-divergent" note to sample, close debt with `accepted-divergence` resolution.

This handles session 11's Pile Counter cascade explicitly, instead of the silent re-validation that actually happened.

### `research-feature-map-rebuild`

Richer than `/research:rebuild-feature-map`. The slash command does the mechanical regen; this skill adds analysis steps that surface design problems:

1. **Detect orphan Models** — Models whose `builds-on:` chain doesn't reach a `root` Model. Flag with suggested cause (typo? actual orphan?).
2. **Detect dependency cycles** between features. Surface as findings — cycles are usually a factoring smell.
3. **Detect feature size signals** — folders past ~15 Models with no obvious sub-clustering. Suggest splits.
4. **Detect stale Models** — `settled` Models whose `builds-on:` targets are themselves `draft` or `superseded`. Flag for review.
5. Run the basic regen if dispositions changed.
6. Report findings as **backlog candidates**, not direct ledger entries — these are *suggestions*, not debt.

**Why.** Hand-maintained FEATURE-MAP eventually drifts; the analysis layer is what makes the auto-version actually trustworthy. Build alongside the basic regen, or skip until the basic version has been used and the seams show.

### `research-promote`

End-to-end promotion to DOCS. Composes `/research:promote-to-docs` with cross-ref sweep. Build later — promotion is rare today.

---

## 6. Subagents

Cases where a subagent (separate context) earns its keep:

### `research-explorer` (an Explore-style agent, project-tuned)

Pre-loaded with the project's structure: knows about `features/`, knows the doc types, knows ADR conventions. Used for: "find all ADRs that touch the snapshot pipeline," "what models depend on `features/effects/notes/01`," "summarize the trigger system across notes + ADRs."

The `Explore` agent already exists. This is the same idea but with a project-specific knowledge prompt baked in.

### `research-reviewer`

Reviews a freshly-landed Model or ADR. Checklist:
- Front matter valid? (`builds-on:` present and either path-list or `root`?)
- All template sections present (or explicitly omitted with rationale)?
- Cascade considered → ledger entries opened in `general/ALIGNMENT-DEBT.md`, OR session note carries `debt: none` for this edit?
- ADR linked from Model and vice versa?
- Glossary changelog updated for any new term?

Run before commit, reads diff, posts findings. Cheap insurance.

---

## 7. CLAUDE.md additions

The repo's root `AGENTS.md` (or a new `RESEARCH/CLAUDE.md`) should encode the workflow as durable instructions:

1. The 7 doc types and their templates.
2. The naming convention (`features/<feature>/notes/NN-<slug>.md`).
3. The three-lifetimes-for-questions rule.
4. The promotion gate.
5. Pointer to `general/_templates/`.
6. *Critical*: "Don't write to `RESEARCH/` outside the prescribed structure. If unsure, write a spike."

Memory-side, equivalent guidance: "When working in `RESEARCH/`, follow `general/WORKFLOW.md`."

---

## 8. Sequencing — what to build first

The 80/20 picks based on FLOW-SIMULATION patterns and the locked Q-set:

**Tier 1 (build immediately post-migration):**
- `/research:bootstrap` — only if a *new* project is being started. Post-migration this is dormant for Card Framework, but it's tier-1 for the workflow's reuse story.
- `/research:start-session`
- `/research:new`
- `/research:end-session`
- `/research:promote-glossary`
- CLAUDE.md / AGENTS.md additions

These cover the bulk of every session and prevent the most common drift.

**Tier 2 (build after a few sessions running on Tier 1):**
- `/research:open-debt` and `/research:close-debt`
- `/research:settle-decision`
- `/research:add-to-backlog` (Q7)
- `/research:lint` (Q13 — debt-feeding linter)
- `research-debt-pass` skill
- `research-rename-cascade` skill

These automate the recurring patterns (debt, rename cascades, ADR creation, deferral capture, staleness detection) that the locked decisions depend on.

**Tier 3 (build only when needed):**
- `/research:supersede-cascade` — first ADR supersession will make this immediately valuable; until then, dormant.
- `/research:abandon-model` — needed when first draft Model is abandoned.
- `/research:rebuild-feature-map` and `research-feature-map-rebuild` — Q6 trigger: ~15 Models or first drift incident.
- All hooks
- `/research:promote-to-docs`, `research-promote` skill
- `research-spike-graduate`, `research-sample-relock`
- `research-explorer`, `research-reviewer` subagents

Lower-frequency operations. Build when forgetting them becomes a real cost, not preemptively.

**Don't build:**
- Anything that blocks. Warn-only, always.
- Anything that auto-commits. Diffs go to the user.
- Anything that auto-rewrites prose. Generative scaffolding only — humans/AI decide content.

---

## 9. Outstanding mechanics

Rules from the locked Q-set that need an automation home but don't have one yet. Each is a candidate for incorporation into existing skills/commands, or for promotion to its own.

### Session-note `debt: N opened` assertion enforcement

**The rule (per §5.7 of WORKFLOW-PROPOSAL).** Every Model edit, before commit, must have either a matching `debt: N opened` line in `sessions/session-NN.md` content section, or `debt: none`. Empty/missing is forbidden.

**Where it could live:**
- **Inside `/research:open-debt`** — when opening debt, append the line. Already in updated behavior (§3) — covers the "edit cascades" path.
- **Inside `/research:end-session`** — at session-end, scan diff for Model edits without matching `debt:` lines; surface for the user to either open debt or assert `debt: none`.
- **As a hook** — pre-commit warn-only when a Model file in the diff has no matching `debt:` line in the session note.

**Recommended:** layer 1 in `/research:open-debt` (covers normal flow), layer 2 in `/research:end-session` (catches misses).

### Sample cooldown commit-message convention

**The rule (per §4.4).** Sample edits commit with either `contract edit — cooldown reset` or `hygiene edit — no cooldown reset` in the message.

**Where it could live:**
- **Templated commit message** in a hypothetical `/research:edit-sample` skill that prompts for the type.
- **Linter check** — `/research:lint` includes a rule: "Sample doc edited in last commit; commit message must contain one of the two markers."

**Recommended:** linter rule. Cheaper than a dedicated skill; surfaces violations as ledger entries.

### ADR auto-numbering

**The rule.** ADR filenames use a 4-digit counter scoped to their parent decisions/ folder. `general/decisions/` and each `features/<feature>/decisions/` have independent counters.

**Where it could live:**
- **Inside `/research:settle-decision`** — when generating the ADR file, glob the target folder, find the max NNNN, increment.
- **Inside `/research:new adr <feature>`** — same logic when creating an ADR directly.

**Recommended:** both. Trivial logic, costs nothing to duplicate.

### General observation

Each outstanding mechanic is small. The cost isn't in defining them; it's in ensuring no rule has *zero* automation home. The §9 list should shrink over time — items move into §3 / §5 detailed sections as their owning skills are built.

---

## 10. Open questions about the automation layer

- **Where do these skills/commands live?** Under `.claude/` per-project, or as a shared plugin? Per-project is simpler; plugin lets other Card Framework forks pick up the workflow.
- **Do hooks survive PRs?** If a hook lives in `.claude/settings.json` and the PR changes settings, the user needs to re-approve permissions. Friction. Probably keep hooks minimal and global, slash commands per-project.
- **What's the test story?** Skills are essentially prompts; "tests" are eval runs. Probably overkill for this volume. A manual smoke-test ("run `/research:start-session` after each major skill change") is enough.
- **How do we keep the skills aligned with the workflow doc?** If the workflow evolves and skills don't, they drift. Probably: skills reference `general/WORKFLOW.md` by section number; ADR-on-workflow-change updates skills as a checklist item.
- **Is `/research:` the right namespace?** Or `/r:`? Or no namespace at all? Ergonomics matter; pick when building.
- **Should ADRs be auto-numbered or human-named?** Auto-numbered is simpler; human-named is more searchable. Suggest auto-number with mandatory slug (`0007-typed-action-template.md`).

---

## 11. What this spike doesn't cover

- **Cross-project workflow tooling.** This spec assumes Card Framework. A generic version (publishable plugin) is a separate concern.
- **Migration tooling.** The MIGRATION-GUIDE phase 1–8 is one-shot work; doesn't merit per-step skills. Maybe one skill for the cross-ref sweep (phase 6); the rest is just `git mv` + edits.
- **Live-collaboration workflows.** Multi-Claude orchestration patterns (one Claude writing the Model, another reviewing it) are out of scope here. Worth its own spike if it ever becomes a real mode.
- **DOCS/ and PLANS/ workflow.** Both are out of scope of the research workflow proposal. Their automations come later.
