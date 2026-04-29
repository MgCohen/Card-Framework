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

## 2. Slash commands (high-leverage, build first)

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
1. Append entry to `general/ALIGNMENT-DEBT.md` with date, reason, affected paths, status `open`.
2. Add a `## Implications for prior docs (§7)` section to the changing Model if it doesn't have one.
3. Patch the affected docs' frontmatter with a `has-pending-debt: true` flag (read by `/research:debt-check`).

**Why.** Implicit alignment debt was the workflow's killer feature in the FLOW-SIMULATION analysis. This makes opening debt cheap.

### `/research:close-debt <entry-id>`

**Trigger.** The debt-pass session is doing the actual reconciliation.

**Behavior.**
1. Strike through the entry in `ALIGNMENT-DEBT.md`, add resolution date.
2. Clear the `has-pending-debt` flag on the affected doc(s).
3. Patch session note "followups closed" list.

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

---

## 3. Hooks (lower-leverage, build only when adoption is proven)

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

## 4. Skill-shaped workflows (multi-step, judgment-laden)

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
3. Show the user the three options from §4.3 of the proposal: re-lock, unlock-to-draft, accept-divergence.
4. Apply chosen path:
   - Re-lock: re-check four-part bar, update sample, close debt entries.
   - Unlock: status `locked` → `draft`, leave note.
   - Accept-divergence: append explicit "known-divergent" note to sample, close debt with `accepted-divergence` resolution.

This handles session 11's Pile Counter cascade explicitly, instead of the silent re-validation that actually happened.

### `research-promote`

End-to-end promotion to DOCS. Composes `/research:promote-to-docs` with cross-ref sweep. Build later — promotion is rare today.

---

## 5. Subagents

Cases where a subagent (separate context) earns its keep:

### `research-explorer` (an Explore-style agent, project-tuned)

Pre-loaded with the project's structure: knows about `features/`, knows the doc types, knows ADR conventions. Used for: "find all ADRs that touch the snapshot pipeline," "what models depend on `features/effects/notes/01`," "summarize the trigger system across notes + ADRs."

The `Explore` agent already exists. This is the same idea but with a project-specific knowledge prompt baked in.

### `research-reviewer`

Reviews a freshly-landed Model or ADR. Checklist:
- Front matter valid?
- All template sections present (or explicitly omitted with rationale)?
- §7 implications listed → debt entries opened?
- ADR linked from Model and vice versa?
- Glossary changelog updated for any new term?

Run before commit, reads diff, posts findings. Cheap insurance.

---

## 6. CLAUDE.md additions

The repo's root `AGENTS.md` (or a new `RESEARCH/CLAUDE.md`) should encode the workflow as durable instructions:

1. The 7 doc types and their templates.
2. The naming convention (`features/<feature>/notes/NN-<slug>.md`).
3. The three-lifetimes-for-questions rule.
4. The promotion gate.
5. Pointer to `general/_templates/`.
6. *Critical*: "Don't write to `RESEARCH/` outside the prescribed structure. If unsure, write a spike."

Memory-side, equivalent guidance: "When working in `RESEARCH/`, follow `general/WORKFLOW.md`."

---

## 7. Sequencing — what to build first

The 80/20 picks based on FLOW-SIMULATION patterns:

**Tier 1 (build immediately post-migration):**
- `/research:start-session`
- `/research:new`
- `/research:end-session`
- `/research:promote-glossary`
- CLAUDE.md / AGENTS.md additions

These four commands plus durable instructions cover the bulk of every session and prevent the most common drift.

**Tier 2 (build after a few sessions running on Tier 1):**
- `/research:open-debt` and `/research:close-debt`
- `/research:settle-decision`
- `research-debt-pass` skill
- `research-rename-cascade` skill

These automate the recurring patterns (debt, rename cascades, ADR creation) that FLOW-SIMULATION identified as load-bearing.

**Tier 3 (build only when needed):**
- All hooks
- `/research:promote-to-docs`
- `research-spike-graduate`, `research-sample-relock`
- `research-reviewer` subagent

Lower-frequency operations. Build when forgetting them becomes a real cost, not preemptively.

**Don't build:**
- Anything that blocks. Warn-only, always.
- Anything that auto-commits. Diffs go to the user.
- Anything that auto-rewrites prose. Generative scaffolding only — humans/AI decide content.

---

## 8. Open questions about the automation layer

- **Where do these skills/commands live?** Under `.claude/` per-project, or as a shared plugin? Per-project is simpler; plugin lets other Card Framework forks pick up the workflow.
- **Do hooks survive PRs?** If a hook lives in `.claude/settings.json` and the PR changes settings, the user needs to re-approve permissions. Friction. Probably keep hooks minimal and global, slash commands per-project.
- **What's the test story?** Skills are essentially prompts; "tests" are eval runs. Probably overkill for this volume. A manual smoke-test ("run `/research:start-session` after each major skill change") is enough.
- **How do we keep the skills aligned with the workflow doc?** If the workflow evolves and skills don't, they drift. Probably: skills reference `general/WORKFLOW.md` by section number; ADR-on-workflow-change updates skills as a checklist item.
- **Is `/research:` the right namespace?** Or `/r:`? Or no namespace at all? Ergonomics matter; pick when building.
- **Should ADRs be auto-numbered or human-named?** Auto-numbered is simpler; human-named is more searchable. Suggest auto-number with mandatory slug (`0007-typed-action-template.md`).

---

## 9. What this spike doesn't cover

- **Cross-project workflow tooling.** This spec assumes Card Framework. A generic version (publishable plugin) is a separate concern.
- **Migration tooling.** The MIGRATION-GUIDE phase 1–8 is one-shot work; doesn't merit per-step skills. Maybe one skill for the cross-ref sweep (phase 6); the rest is just `git mv` + edits.
- **Live-collaboration workflows.** Multi-Claude orchestration patterns (one Claude writing the Model, another reviewing it) are out of scope here. Worth its own spike if it ever becomes a real mode.
- **DOCS/ and PLANS/ workflow.** Both are out of scope of the research workflow proposal. Their automations come later.
