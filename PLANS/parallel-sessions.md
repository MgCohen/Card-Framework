---
type: plan
status: proposed
last-updated: 2026-05-01
tags: [#workflow, #builder, #parallel-sessions, #checkpoint]
---

# Parallel Sessions — implementation plan

Goal: support multiple Product Builder sessions running in parallel — possibly on different branches, possibly on different machines — with a mechanical merge step. Parallel sessions are **genuinely separate sessions** (each gets its own session note); merge-pass reconciles shared state.

## 1. Mental model

The single load-bearing idea: **the checkpoint is a projection, never a merge target.**

- **Sessions** are immutable source-of-truth. Each session note records what was done, what was resolved, what the next active question is.
- **Ledgers** (`ALIGNMENT-DEBT`, `OPEN-QUESTIONS`, `BACKLOG`, `GLOSSARY`, `FEATURE-MAP`) are append-mostly state. Git's 3-way merge handles them once the formats are tightened.
- **Checkpoint** = `project(merged_sessions, ledgers)`. Pure function of inputs. Rebuilt fresh by merge-pass after each merge. No section is hand-authored; everything is derived.

Why this works: parallel sessions never write to the checkpoint (they only write their own session note + ledger edits on their branch). Merge-pass only ever reconciles ledgers (via git) and re-projects the checkpoint. No "merging two checkpoints" problem ever exists.

## 2. Schemas

### 2.1 Session ID convention

```
session-<UTC-YYYYMMDD>-<HHMM>-<slug>.md
```

Examples: `session-20260502-0915-slice-catalog.md`, `session-20260502-1030-zone-topology.md`. Two machines never collide. Drops the `ls | sort | tail -1` pattern entirely.

### 2.2 Checkpoint frontmatter

```yaml
---
type: checkpoint
schema-version: 2
last-updated: 2026-05-02
head: session-20260502-1030-zone-topology
merged:
  - session-20260430-0900-engine-wiring
  - session-20260501-1430-debt-pass
  - session-20260502-0915-slice-catalog
  - session-20260502-1030-zone-topology
batch:
  - session-20260502-0915-slice-catalog
  - session-20260502-1030-zone-topology
tags: [#checkpoint]
---
```

- `head` — last session merged. Acts as the parent pointer for new sessions.
- `merged` — append-only list of every session-id ever merged. Used to detect already-merged and stale-parent cases.
- `batch` — sessions since the last single-merge sync point. Drives the `Where we stopped` and `Recent exit questions` projection rules. A single-session merge resets batch to `[that session]`; a parallel batch grows it.

### 2.3 Session frontmatter

```yaml
---
type: session
schema-version: 2
session-id: session-20260502-0915-slice-catalog
parent: session-20260501-1430-debt-pass     # checkpoint.head at start-session time
status: completed                           # in-progress | completed | merged
started-at: 2026-05-02T09:15:00Z
last-updated: 2026-05-02
work-summary: One-line summary of what this session delivered.
tags: [#session]
---
```

- `parent` — the only field that drives merge-pass classification.
- `work-summary` — the only authored line that the checkpoint projects. Set at `end-session` time.
- `status: merged` is the terminal state set by merge-pass.

### 2.4 Ledger format tightening

Required for clean git 3-way merge under parallel writes.

**`ALIGNMENT-DEBT.md`** — collapse two-section model into one. Add stable `id:`. Status inline.

```markdown
## Entries
- id: ad-20260501-001 | status: open | source: ... | target: ... | reason: ...
- ~~id: ad-20260501-002 | status: open | source: ... | target: ... | reason: ...~~ → resolved 2026-05-02 (slice-catalog session)
```

ID scheme: `ad-<YYYYMMDD>-<NNN>` (human-readable, sequential per day).

**`OPEN-QUESTIONS.md`** — keep section grouping; add stable `id:` per entry.

```markdown
- id: oq-gamestate-slice-catalog | status: open
  Slice catalog with read/write fences.
```

**`BACKLOG.md`** — add `id: bl-<slug>` per entry. Otherwise unchanged.

**`GLOSSARY.md`** — keep tables. `## Changelog` is the merge arbiter; changelog entries gain `branch:` annotation:

```markdown
- 2026-05-02 — added `OrderKey` (branch: claude/feature-zone-topology). Identity for ordered set membership.
```

**`FEATURE-MAP.md`** — accept frequent conflicts; auto-generation is on the backlog. Until then, two parallel sessions both touching `FEATURE-MAP` need manual resolution at merge-pass time. Surface, don't auto-resolve.

## 3. The projection rules

`project-checkpoint(merged[], batch[], session_notes, ledgers)` produces the entire checkpoint body. Pure function. Idempotent. Re-runnable.

| Section | Projection rule |
|---|---|
| `head` | Last entry in `merged[]` |
| `## Where we stopped` | One bullet per session in `batch[]`, in `started-at` order. Bullet text = `S(<slug> <date> <time>): <work-summary>` |
| `## 🎯 The active open question (singular)` | The "Active question going out" of the most-recent-by-`started-at` session in `batch[]`. Footer: `*(From <session-id>.)*` |
| `## Recent exit questions (parallel batch)` | Rendered only when `len(batch) > 1`. One bullet per *other* session in batch, each with its exit question. |
| `## ❓ Other still-open` | Top 8 entries from `OPEN-QUESTIONS.md` with `status: open` and not struck through, in section order. No editorial annotations. |
| `## Suggested next steps` | (1) active question, (2) recent exit questions if batch>1, (3) any open debt referenced by sessions in batch, (4) top open debt cluster. |
| `## Artifacts to read on resume` | `git diff --name-only` over each branch in `batch[]` against the pre-batch parent, filtered to `Product/features/**` and `Product/ledgers/**`. Annotated with the session-slug that touched each file. |

The only authoring surface is the `work-summary:` line per session. Everything else is mechanical.

## 4. Skill changes

### 4.1 `/builder:start-session` — modify

- Drop `ls | sort | tail -1`. Read `head` from checkpoint frontmatter.
- Generate `session-id = session-<UTC-now-YYYYMMDD-HHMM>-<slug>`. Slug derived from active question; fall back to `untitled`.
- Pre-create `Product/sessions/<session-id>.md` with v2 frontmatter, `parent: <checkpoint.head>`, `status: in-progress`.
- Resume detection: in-progress note exists with `parent == current checkpoint.head`. If parent doesn't match head, surface stale-parent warning.
- Print parallel-safe banner: `Branch: <branch> · parent: <head>. Parallel-safe — merge-pass will reconcile.`

### 4.2 `/builder:end-session` — modify

- Author writes `work-summary:` (one line). Claude pre-fills from "Active question going out" and asks for confirmation.
- If on a feature branch (`git rev-parse --abbrev-ref HEAD` ≠ `main`): **do not touch the checkpoint.** Print: `Branch session — checkpoint update deferred to /builder:merge-pass.`
- If on `main`: invoke `_project-checkpoint` inline (single-merge fast-forward).
- Flip `status: in-progress` → `completed`. Stamp `work-summary`.
- Commit + push the session note + ledger edits.
- Closing report adds: `Branch: <branch> · parent: <session-id> · merge: pending` when on a branch.

### 4.3 `/builder:debt-pass` — modify

- Drop `session-NN+1` filename collision check. Use new session-id convention.
- Branch name: `claude/debt-pass-YYYY-MM-DD-HHMM` to allow same-day re-runs.
- Don't update checkpoint directly. Write a session note with `work-summary: Auto debt-pass — N entries discharged.` and let merge-pass handle the projection.

### 4.4 `/builder:new` — modify

Step 11 (patch session note): find in-progress session by frontmatter `status: in-progress`, not by filename pattern. Match on `parent == current checkpoint.head` if multiple in-progress notes exist.

### 4.5 `/builder:promote-glossary` — modify

Changelog entries gain `branch:` annotation (see §2.4). Otherwise unchanged.

### 4.6 `/builder:bootstrap` — modify

- Initialize ledgers in v2 format (single-section ALIGNMENT-DEBT with IDs, etc.).
- Initial checkpoint frontmatter: `head: <session-01-id>`, `merged: []` until session 01 closes.

### 4.7 `/builder:merge-pass` — new skill

See `.claude/commands/builder/merge-pass.md` for the full spec.

### 4.8 `_project-checkpoint` — internal helper

See `.claude/commands/builder/_project-checkpoint.md`. Pure function — input: `merged[]`, `batch[]`, session notes, ledger state. Output: full checkpoint body. Idempotent. Safe for diagnostic re-projection.

## 5. Migration

The 19 existing sequential sessions stay in place. No backfilling.

**Cutover ritual (one PR):**

1. Stamp every existing `session-NN.md` frontmatter with `schema-version: 1`. Keep filenames unchanged.
2. Reformat ledgers in place:
   - `ALIGNMENT-DEBT.md` — fold `## Open` and `## Resolved` into single `## Entries`. Add `id:` to all entries.
   - `OPEN-QUESTIONS.md` — add `id:` to all entries.
   - `BACKLOG.md` — add `id:` to all entries.
   - `GLOSSARY.md` — no structural change; verify changelog format.
3. Write a synthetic seed session: `Product/sessions/session-20260501-2359-cutover.md` with v2 frontmatter, `parent: session-19`, `status: merged`, `work-summary: Cutover to parallel-session schema v2.`
4. Rewrite `SESSION-CHECKPOINT.md` frontmatter to v2:
   ```yaml
   schema-version: 2
   head: session-20260501-2359-cutover
   merged: [session-20260501-2359-cutover]   # pre-cutover sessions implicit
   batch: [session-20260501-2359-cutover]
   ```
   Body becomes the projected checkpoint. Keep today's `Where we stopped` prose as the synthetic session's `work-summary`.
5. Verify `Product/sessions/session-*.md` gitignore still works for new naming (glob matches — confirmed).

After cutover, all new sessions use v2 schema. v1 history is preserved but never re-projected.

## 6. Implementation order

| Step | Scope | Notes |
|---|---|---|
| 1. Schema cutover PR | Mechanical: ledger reformat + IDs, frontmatter v1 stamping, synthetic seed session, checkpoint frontmatter v2. | Foundation. One PR, no skill changes yet. |
| 2. Build `_project-checkpoint` helper | Implement projection rules from §3. | Pure logic. Validate against current checkpoint shape. |
| 3. Update `start-session` + `end-session` to v2 | Parent pointer write, work-summary field, no checkpoint write on branches. Test on `main` only. | Validates schema for single-writer flow before adding parallelism. |
| 4. Update `debt-pass` + `new` to v2 | ID-based collision checks, session-id filenames. | Required before next debt-pass fires. |
| 5. Build `/builder:merge-pass` | Full spec from §4.7. | Largest scope; deserves its own session. |
| 6. Live test single-machine | Two branches on one machine, close both as separate sessions, run merge-pass. | Iron out edge cases. |
| 7. Live test cross-machine | Repeat 6 from two physical machines via push/pull. | Final acceptance. |
| 8. Documentation | Update `_workflow/PRODUCT-BUILDER.md` with parallel-session model, merge-pass cadence, schema reference. Update `REPORT-STYLE.md` with merge-pass report template. | Lock convention into workflow doc. |

## 7. Worked example (full simulation)

### 7.1 Starting checkpoint

```markdown
---
type: checkpoint
schema-version: 2
last-updated: 2026-05-01
head: session-20260501-1430-debt-pass
merged: [..., session-20260501-1430-debt-pass]
batch: [session-20260501-1430-debt-pass]
---

## Where we stopped
- S(debt-pass 05-01 14:30): auto-discharged 7 alignment entries; cascade walk completed for `Trigger` rename.

## 🎯 The active open question (singular)
**`GameState` slice catalog** — slices with read/write fences per slice.
*(From session-20260501-1430-debt-pass.)*

## ❓ Other still-open
- Zone topology: stack vs. queue
- Effect resolution order
- ...
```

### 7.2 Session A — `session-20260502-0915-slice-catalog`

```markdown
---
session-id: session-20260502-0915-slice-catalog
parent: session-20260501-1430-debt-pass
status: completed
started-at: 2026-05-02T09:15:00Z
work-summary: Drafted GameState slice catalog (6 slices with R/W fences); resolved card-identity question.
---
## Active question going out
Effect resolution order under simultaneous triggers — stack slice ordering undefined.
```

### 7.3 Session B — `session-20260502-1030-zone-topology`

```markdown
---
session-id: session-20260502-1030-zone-topology
parent: session-20260501-1430-debt-pass     # same parent as A
status: completed
started-at: 2026-05-02T10:30:00Z
work-summary: Drafted zone topology (queue/stack/set + OrderKey); promoted glossary; surfaced shuffle determinism.
---
## Active question going out
Zone shuffle determinism — per-zone seed vs. per-game seed.
```

### 7.4 Merge-pass run

Order by `started-at`: A (09:15), then B (10:30).

**Merge step 1 — A:** `A.parent == head` → fast-forward. Append A to `merged[]`. `batch[] = [A]`.

**Merge step 2 — B:** `B.parent ≠ head` (now A) but `∈ merged[]` → rebase-on-top. Ledger conflicts resolve via stable-id rules. Append B to `merged[]`. Both share pre-batch parent → `batch[] = [A, B]`.

### 7.5 Final projected checkpoint (excerpt)

```markdown
## Where we stopped
- S(slice-catalog 05-02 09:15): Drafted GameState slice catalog (6 slices with R/W fences); resolved card-identity question.
- S(zone-topology 05-02 10:30): Drafted zone topology (queue/stack/set + OrderKey); promoted glossary; surfaced shuffle determinism.

## 🎯 The active open question (singular)
**Zone shuffle determinism** — per-zone seed vs. per-game seed.
*(From session-20260502-1030-zone-topology, most recent merge.)*

## Recent exit questions (parallel batch)
- **Effect resolution order under simultaneous triggers** — from session-20260502-0915-slice-catalog.
```

### 7.6 What this proves

- No work lost: both sessions' `work-summary` appear; full narratives live in session notes forever.
- Re-running merge-pass produces identical output (projection is pure).
- No "stale next-steps" bug: next-steps derive from current exit questions, not carried.
- Conflict surface is exactly the ledgers; checkpoint never conflicts (regenerated, never merged); session notes never conflict (each writes its own filename).

## 8. Risks and open questions

- **Projection logic complexity.** §3 `Suggested next steps` rule is the fuzziest. May need iteration after first real run.
- **`Other still-open` heuristic.** "Top 8 by section order" may be worse than today's curated list. Add `priority: high|normal` field to OPEN-QUESTIONS if needed (v2.1).
- **Cross-machine clock skew.** UTC timestamps limit damage; worst case is some `Suggested next steps` order ambiguity. Tiebreak by parent chain + lex order.
- **`FEATURE-MAP.md` will conflict often.** Accept manual resolution until auto-generation lands.
- **Non-Model file conflicts surface for human.** Two sessions editing the same Model file is manual resolve. Flag explicitly so users don't expect magic.

## 9. Out of scope

- LLM-driven prose merging (rejected — projection is rich enough).
- Override blocks for hand-authored checkpoint sections (rejected — same reason).
- Carrying `Suggested next steps` across merges (rejected — derived, not carried).
- Backfilling pre-cutover sessions to v2 schema.
- Auto-generating `FEATURE-MAP.md` (separate backlog item).
