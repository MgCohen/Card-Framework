# Lint Run Transcript — eval-1-full-lint (without_skill)

Date: 2026-05-01
Mode: --all (default)
Scope: Product/ docs

---

## Steps taken

### 1. Project structure survey

- Listed all files under `/home/user/Card-Framework/` to understand the repo layout.
- Identified: `Product/`, `RESEARCH_BACKUP/`, `ProductBuilder/`, `DOCS/`, `Assets/`, `Packages/`, `PLANS/`.
- Listed all `*.md` under `Product/`: 102 files across `_workflow/`, `decisions/`, `features/`, `general/`, `ledgers/`, `sessions/`, `spikes/`, `support/`.
- Located 7 `archive/` directories, one per feature under `features/`: all empty.

### 2. Read the lint skill specification

- Read `/home/user/Card-Framework/.claude/skills/builder/lint/SKILL.md` to understand the rules:
  - A1–A4: archive integrity
  - S1: broken bare path references
  - S2: broken builds-on frontmatter entries
- Key: `_workflow/_validation/` is excluded (contains intentional test fixtures).

### 3. Archive integrity checks (A1–A4)

- **A1**: `grep -rn '^superseded_by:' Product/features/*/archive/` — no output. All archive dirs are empty.
- **A2**: `grep -rn 'archive/' Product/ --include='*.md'` — found mentions only in workflow prose and BACKLOG (not live links into archive/).
- **A3a**: `grep -rn 'status: Superseded|status: Cancelled' Product/ --include='*.md' | grep -v '/archive/'` — found only in `PRODUCT-BUILDER.md:741` and `_templates/archive.md:3` (template code blocks, not frontmatter). No active doc has a retired status.
- **A3b**: `find Product/features -path '*/archive/*.md'` — no results. All archive dirs are empty.
- **A4**: No `superseded_by` entries anywhere. N/A.

**Result: All archive integrity checks clean.**

### 4. S1 — Broken path references

- Ran `grep -rnoE 'features/[a-z]+/[a-z/._-]+\.md|Product/[a-z/._-]+\.md' Product/ --include='*.md'`.
- Noted the skill's regex uses `[a-z/._-]` which excludes digits — many paths with numbers (e.g. `01-command-pipeline.md`) would not match. Re-ran with corrected regex `features/[a-z]+/[a-z0-9/._-]+\.md` to capture numeric paths.
- Extracted unique paths and verified each against the filesystem:
  - `features/actions/decisions/0002-async-layer-hierarchy.md` — MISSING (PRODUCT-BUILDER.md:91, prose naming example)
  - `features/actions/models/03-action-definition.md` — MISSING (PRODUCT-BUILDER.md:91, prose naming example)
  - `features/engine/models/04-evaluation-loop.md` — MISSING (REPORT-STYLE.md:238, sample output block)
  - `features/engine/archive/01-old.md` — MISSING (REPORT-STYLE.md:272, sample lint table)
  - `features/engine/models/_test-fixture.md` — MISSING (ALIGNMENT-DEBT.md:29, already logged as ad-20260501-026)
  - All others: OK.
- Also checked non-`features/` markdown links: `[text](path)` link extraction via `grep -oP '(?<=\]\()([^)]+)(?=\))'`.
  - Found stale %20-encoded spike links in `general/GAME-ARCHETYPES.md` lines 56–58.
  - Found broken relative link `../ledgers/OPEN-QUESTIONS.md` in `features/engine/models/04-gamestate-slices.md` lines 83/150/162.
  - Relative links in session files: all OK.
  - Relative links in `features/engine/decisions/0002-gamerules-zero-state.md`: `../../effects/models/04-effect-flow.md` resolves correctly.
- Prior-art docs (`support/prior-art/*.plan.md`): reference `Docs/` and `Assets/Scripts/` paths that don't exist. Classified as historical records outside scope.

### 5. S2 — builds-on references

- Collected all `builds-on:` from `Product/features/` (14 entries, all non-archive).
- Verified each referenced path exists on disk: all 13 unique paths resolved. No S2 issues in features/.
- Expanded scope to all Product/ docs (since task said "check everything"):
  - `PRODUCT-BUILDER.md`: `builds-on: [SESSION-CHECKPOINT.md, ledgers/OPEN-QUESTIONS.md]` — both at Product/ root, both exist. OK.
  - `REPORT-STYLE.md`: `builds-on: [PRODUCT-BUILDER.md, EMOJI-LEGEND.md]` — neither exists at Product/ root. Actual files are at `_workflow/PRODUCT-BUILDER.md` and `_workflow/EMOJI-LEGEND.md`.
  - `EMOJI-LEGEND.md`: `builds-on: [PRODUCT-BUILDER.md]` — not at Product/ root. Actual file at `_workflow/PRODUCT-BUILDER.md`.
  - `decisions/0004-archive-convention.md`: `builds-on: [_workflow/PRODUCT-BUILDER.md]` — OK.
  - `support/samples/pile-counter.md`: three paths all verified OK.
- Found 3 new S2 issues (all in `_workflow/` docs using bare names instead of `_workflow/`-prefixed paths).

### 6. Deduplication against ALIGNMENT-DEBT.md

- Read existing open entries: 5 open (ad-20260501-022 through 026).
- ad-20260501-026 targets `features/engine/models/_test-fixture.md` — skip, already logged.
- All other findings are new.

### 7. Count and classify

- Genuine S1 broken hyperlinks: 4 unique targets (3 GAME-ARCHETYPES spike links + 1 wrong-depth relative link with 3 occurrences).
- Low-severity S1 (phantom paths in prose/examples): 4 paths in _workflow/ docs.
- S2 broken builds-on: 3 (REPORT-STYLE x2, EMOJI-LEGEND x1).
- Archive (A1–A4): 0.
- Already logged: 1 (ad-20260501-026).
- New debt entries proposed: 6 (ad-20260501-027 through 032).

---

## Files read

- `/home/user/Card-Framework/Product/ledgers/ALIGNMENT-DEBT.md`
- `/home/user/Card-Framework/Product/_workflow/PRODUCT-BUILDER.md`
- `/home/user/Card-Framework/Product/_workflow/REPORT-STYLE.md`
- `/home/user/Card-Framework/Product/_workflow/EMOJI-LEGEND.md`
- `/home/user/Card-Framework/.claude/skills/builder/lint/SKILL.md`
- `/home/user/Card-Framework/Product/features/engine/models/04-gamestate-slices.md` (line 83, 150, 162)
- `/home/user/Card-Framework/Product/general/GAME-ARCHETYPES.md` (lines 56–58)

## Commands run (key)

```
find Product/ -name '*.md' -not -path '*/_workflow/_validation/*' | sort | wc -l
find Product/features/*/archive -type d -exec ls {} ;
grep -rn '^superseded_by:' Product/features/*/archive/
grep -rn 'status: Superseded|status: Cancelled' Product/ --include='*.md' | grep -v '/archive/'
grep -rnoE 'features/[a-z]+/[a-z0-9/._-]+\.md' Product/ --include='*.md' (corrected regex)
grep -rHn -oP '(?<=\]\()(\.\./[^)]+)(?=\))' Product/ --include='*.md'
grep -rn '^builds-on:' Product/features/ --include='*.md' | grep -v '/archive/'
grep -rn '^builds-on:' Product/ --include='*.md'
```
