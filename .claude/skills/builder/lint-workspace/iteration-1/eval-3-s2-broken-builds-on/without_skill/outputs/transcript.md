# Transcript: eval-3-s2-broken-builds-on (without_skill)

**Date:** 2026-05-01  
**Task:** Lint builds-on references in Product/features/, using a temporary broken fixture

---

## Step 1 — Pre-flight

Checked that `Product/features/engine/models/` exists and listed its contents:
- `01-evaluation-loop.md`
- `02-gamerules-contract.md`
- `03-snapshot-pipeline.md`
- `04-gamestate-slices.md`

Confirmed output directory existed at:
`.claude/skills/builder/lint-workspace/iteration-1/eval-3-s2-broken-builds-on/without_skill/outputs/`

---

## Step 2 — Create Test Fixture

Created file at:
`Product/features/engine/models/_test-fixture.md`

Content:
```
---
type: model
status: draft
builds-on: [features/engine/models/DOES-NOT-EXIST.md, features/engine/models/01-evaluation-loop.md]
---

# Test Fixture

This is a temporary fixture for linting tests.
```

---

## Step 3 — Lint builds-on References

Used `grep -r "builds-on:"` across `Product/features/` to collect all front-matter references.

For each non-`root` reference, resolved the path relative to `Product/` and tested for file existence using `[[ -f "$target" ]]`.

**Files with builds-on (15 total):**
- 5 files use `builds-on: root` (skipped as sentinel values)
- 10 files use array-form references pointing to other model files

**Results:**
- 16 references resolved
- 1 MISSING: `features/engine/models/DOES-NOT-EXIST.md` (referenced by `_test-fixture.md`)
- 15 OK, including `features/engine/models/01-evaluation-loop.md` (also referenced by `_test-fixture.md`)

No archived files exist (`find` on `*/archive/*` returned empty).

---

## Step 4 — Delete Fixture

Deleted `Product/features/engine/models/_test-fixture.md` using `rm`.

Verified removal with `ls` — file no longer present.

---

## Observations

- The linter (manual shell script) correctly flags only the broken reference.
- The valid sibling reference (`01-evaluation-loop.md`) in the same `builds-on` array is not flagged.
- No archived files exist in `features/`, so there are no archive-integrity issues.
- All other `builds-on` chains across the codebase are intact.
