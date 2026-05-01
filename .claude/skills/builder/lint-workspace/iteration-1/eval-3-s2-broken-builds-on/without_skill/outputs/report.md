# Lint Report: builds-on Reference Check

**Date:** 2026-05-01  
**Scope:** `Product/features/` — all `.md` files with `builds-on:` front-matter  
**Task:** Verify all `builds-on` references resolve to existing, non-archived files

---

## Summary

- **Total files scanned:** 15 (with `builds-on:` front-matter)
- **Total references checked:** 16 (excluding `root` sentinels)
- **Missing references found:** 1
- **Archived references found:** 0 (no archive/ files exist)

---

## Findings

### BROKEN — Missing Target

| File | Reference | Status |
|------|-----------|--------|
| `Product/features/engine/models/_test-fixture.md` | `features/engine/models/DOES-NOT-EXIST.md` | FILE NOT FOUND |

**Details:** The file `Product/features/engine/models/DOES-NOT-EXIST.md` does not exist on disk. This reference is broken and should be corrected or removed.

---

### Valid References in Same File

| File | Reference | Status |
|------|-----------|--------|
| `Product/features/engine/models/_test-fixture.md` | `features/engine/models/01-evaluation-loop.md` | OK |

**Note:** `01-evaluation-loop.md` exists and resolves correctly. It is **not** flagged.

---

### All Other builds-on References (OK)

| File | Reference | Status |
|------|-----------|--------|
| `features/effects/models/02-replacement-prevention.md` | `features/effects/models/01-async-dispatch.md` | OK |
| `features/effects/models/03-ongoing-effects.md` | `features/effects/models/01-async-dispatch.md` | OK |
| `features/effects/models/04-effect-flow.md` | `features/effects/models/01-async-dispatch.md` | OK |
| `features/effects/models/04-effect-flow.md` | `features/engine/models/03-snapshot-pipeline.md` | OK |
| `features/engine/models/02-gamerules-contract.md` | `features/engine/models/01-evaluation-loop.md` | OK |
| `features/engine/models/03-snapshot-pipeline.md` | `features/engine/models/02-gamerules-contract.md` | OK |
| `features/engine/models/03-snapshot-pipeline.md` | `features/actions/models/02-availability-snapshot.md` | OK |
| `features/engine/models/04-gamestate-slices.md` | `features/effects/models/04-effect-flow.md` | OK |
| `features/engine/models/04-gamestate-slices.md` | `features/engine/models/02-gamerules-contract.md` | OK |
| `features/actions/models/02-availability-snapshot.md` | `features/actions/models/01-command-pipeline.md` | OK |
| `features/actions/models/02-availability-snapshot.md` | `features/engine/models/03-snapshot-pipeline.md` | OK |
| `features/presentation/models/01-presentation-loop.md` | `features/actions/models/02-availability-snapshot.md` | OK |
| `features/presentation/models/01-presentation-loop.md` | `features/engine/models/03-snapshot-pipeline.md` | OK |
| `features/triggers/models/01-trigger-fold.md` | `features/engine/models/01-evaluation-loop.md` | OK |

---

## Lint Verdict

- `DOES-NOT-EXIST.md` — **FLAGGED** (file does not exist)
- `01-evaluation-loop.md` — **NOT flagged** (file exists and resolves correctly)

The linter correctly distinguishes between broken and valid references within the same `builds-on` array.
