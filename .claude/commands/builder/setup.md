---
description: Walk the orientation pass — VISION, ROADMAP, ARCHITECTURE, candidate features, seed FEATURE-MAP and OPEN-QUESTIONS. Run once during session 01.
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

You are running the orientation pass for a new project under the Product Builder workflow. This is **once-per-project**, after `/builder:bootstrap` has scaffolded the skeleton, and during `sessions/session-01.md`. It enforces §5.0 steps 2–6 of `Product/_workflow/PRODUCT-BUILDER.md` so no project moves into session-02 with empty orientation docs.

## Pre-flight

1. **Skeleton must exist.** Verify all of: `Product/general/`, `Product/ledgers/`, `Product/features/`, `Product/sessions/session-01.md`. If any is missing, **stop** — point the user at `/builder:bootstrap`.
2. **Detect completion.** Setup is **complete** when all are true:
   - `Product/general/VISION.md` exists with `status: settled` and a non-empty body.
   - `Product/general/ROADMAP.md` exists with `status: settled` and a non-empty body.
   - `Product/general/ARCHITECTURE.md` exists with `status: settled` and a non-empty body.
   - `Product/ledgers/FEATURE-MAP.md` has ≥ 1 candidate-feature row (not just the header stub).
   - At least one `Product/features/<name>/` folder exists (beyond `.gitkeep`).
   If complete, print "Setup already complete; nothing to do." and stop. Don't re-prompt.
3. **Resume mode.** Otherwise, partial state is fine. List which artifacts exist and which are missing in a 4–6 line summary, then proceed step by step. Skip steps already complete. Don't overwrite an artifact that's already `settled` without explicit user confirmation.

## Steps

Walk the user through each step in order. **Confirm each step's diff before writing.** Do not batch all writes at the end — confirm and write per step so the user can pause/resume cleanly.

### 1. VISION.md

Path: `Product/general/VISION.md`.

If it already exists with `status: settled` and has the required sections, summarize it back to the user in 4 lines and ask whether to skip or revise. Otherwise prompt for:

- **Goal** — one paragraph: what is this project building?
- **Design North Star** — the one question the project answers.
- **Initial Non-Goals** — what we're explicitly *not* building yet.
- **Important Constraints** — invariants the project must preserve.
- **Success Shape** — what a downstream user can do once this works.
- **Risks To Keep Visible** — the failure modes we're tracking.
- **Working Principle** — the one or two rules that keep design honest.

Each section must be non-empty. If the user can't fill one in, ask whether to defer setup. Don't write a stub.

Front matter:
```yaml
---
type: reference
status: settled
last-updated: <today, ISO date>
tags: [#general, #vision, #scope]
---
```

### 2. ROADMAP.md

Path: `Product/general/ROADMAP.md`.

Prompt for **at least 3 phases** in order. Each phase needs:
- A name (Phase N: <title>).
- **Goal** — one line.
- **Outputs** or **Questions to answer** — bulleted list.
- **Exit criteria** — bulleted list.

Encourage but don't require the seven-phase shape from the Card Framework template (Research → Vocabulary → Minimal Sim → Authoring → Presentation → Presets → Tooling → Multiplayer → Scale). Surface that template as a suggestion if the user is stuck.

Front matter:
```yaml
---
type: reference
status: settled
last-updated: <today, ISO date>
tags: [#general, #roadmap]
---
```

### 3. ARCHITECTURE.md

Path: `Product/general/ARCHITECTURE.md`.

Prompt for at minimum:
- **Core Direction** — the architectural shape (mermaid diagram encouraged but not required).
- **Layers** — what tiers exist and what each owns.
- **Invariants** — non-negotiable properties (determinism, single-writer state, etc.).
- **Trade-offs** — tensions the architecture is choosing between.

Free-form prose is fine. Don't enforce a strict heading set beyond the four above.

Front matter:
```yaml
---
type: reference
status: settled
last-updated: <today, ISO date>
tags: [#general, #architecture]
---
```

### 4. Candidate features

Prompt: comma-separated list of 5–12 likely features. Surface the project's `Success Shape` (from VISION) as seed inspiration. Examples: `entities, zones, actions, triggers, effects, engine, presentation`.

For each `<name>`:
- Create `Product/features/<name>/`.
- Create `Product/features/<name>/models/` and `Product/features/<name>/decisions/`.
- Drop a `.gitkeep` in each empty leaf.

**Do NOT write Feature roots (`<name>.md`) here.** Feature roots land when a feature gets its first design session — that's a §5.11 ceremony, not a bootstrap step.

### 5. Seed FEATURE-MAP.md

Path: `Product/ledgers/FEATURE-MAP.md`.

Replace the bootstrap stub with:

- **Layer 1 — Feature index** table. One row per candidate, status `considered`, models `0`, depends-on blank.
- **Layer 2 — Dependency graph.** Empty mermaid block — populated as features depend on each other.
- **Layer 3 — Model-level breakdown.** Empty placeholder ("No Models yet — populated as features land their first Model.").

Don't ask the user for dependency edges yet. They'll surface as the first Models declare `builds-on:`.

### 6. Seed OPEN-QUESTIONS.md

Path: `Product/ledgers/OPEN-QUESTIONS.md`.

Prompt for **at least 3 durable open questions** the project carries from day one. Examples: state ownership model, async vs sync resolution story, multiplayer story, authoring format. Each entry follows the §6 shape:

```markdown
- **Title** — one-paragraph framing of the question. What's known, what's unknown, what's blocked on it.
  Linked context: `<paths or "none yet">`.
  Status: open
```

If the user genuinely has fewer than 3, accept it but flag in the report — sparse OPEN-QUESTIONS is a smell, not a blocker.

### 7. Patch session-01.md

Edit `Product/sessions/session-01.md`:

- Append to Part 1 "Work done":
  - `- VISION.md drafted and settled`
  - `- ROADMAP.md drafted and settled`
  - `- ARCHITECTURE.md drafted and settled`
  - `- N candidate features scaffolded: <comma-separated list>`
  - `- FEATURE-MAP.md seeded with N considered rows`
  - `- OPEN-QUESTIONS.md seeded with N entries`
- Set Part 1 "Active question going out": `what's the first feature we model?`
- Bump `last-updated` to today.

Don't flip session-01 status — it stays `in-progress` until `/builder:end-session` closes it.

### 8. Final report

Print, exactly:

```
Setup complete.
- VISION / ROADMAP / ARCHITECTURE: settled.
- <N> candidate features scaffolded: <list>.
- FEATURE-MAP: <N> considered rows.
- OPEN-QUESTIONS: <N> entries seeded.
- session-01.md: active question going out updated.

Next: close session-01 with /builder:end-session, then /builder:start-session for session-02.
```

If any step was deferred (user skipped one), surface that in the report and note that `/builder:start-session` will continue to gate session-02+ until it's filled in.

## Notes

- **Once-per-project.** Refuse aggressively if setup is already complete. Re-running on a populated project is destructive-adjacent.
- **Resumable.** A user who runs `/builder:setup` halfway, walks away, and returns the next day should pick up cleanly. State is read from the filesystem, not from a marker file.
- **No silent writes.** Every step shows a diff and waits for confirmation. The orientation docs are load-bearing — overwriting them by accident would be worse than re-prompting.
- **Don't write Feature roots.** Tempting in step 4, but Feature design starts in session 02 — keeping it out preserves the "session 01 = orientation only" rule from §5.0 step 8.
- **Don't update SESSION-CHECKPOINT.md.** That's `/builder:end-session`'s job.
- **Don't seed BACKLOG, GLOSSARY, or ALIGNMENT-DEBT.** They're appended-to during sessions, not seeded at setup. (Bootstrap stubs them empty; that's enough.)
