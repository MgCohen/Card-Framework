# Eval-3 S2 Broken builds-on — Full Lint Output

## Run 1 (fixture present)

**Command:** `/builder:lint --paths-only`
**Fixture:** `Product/features/engine/models/_test-fixture.md` present with:
```yaml
builds-on: [features/engine/models/DOES-NOT-EXIST.md, features/engine/models/01-evaluation-loop.md]
```

### Lint Report

📊 1 new entry · 0 skipped (already open) · 7 total open

### Findings by rule
| Rule | New | Description |
|------|-----|-------------|
| S1   | 0   | Broken path references |
| S2   | 1   | Broken builds-on references |

⚠️ Pass-mode: 7 open debt entries — consider a discharge pass before continuing.

**S2 finding detail:**
- Target: `features/engine/models/_test-fixture.md`
- Missing path: `features/engine/models/DOES-NOT-EXIST.md` — DOES NOT EXIST — flagged
- Valid path: `features/engine/models/01-evaluation-loop.md` — EXISTS — not flagged

**New ledger entry written:**
```
- id: ad-20260501-026 | status: open | source: linter | target: features/engine/models/_test-fixture.md | reason: S2 — builds-on references missing or archived path (features/engine/models/DOES-NOT-EXIST.md)
```

---

## Run 2 (fixture deleted — idempotency check)

**Command:** `/builder:lint --paths-only`
**Fixture:** `Product/features/engine/models/_test-fixture.md` — DELETED before this run

### Lint Report

📊 0 new entries · 0 skipped (already open) · 7 total open

### Findings by rule
| Rule | New | Description |
|------|-----|-------------|
| S1   | 0   | Broken path references |
| S2   | 0   | Broken builds-on references |

⚠️ Pass-mode: 7 open debt entries — consider a discharge pass before continuing.

**Idempotency result:** No new entries written. The fixture's removal meant no builds-on reference to DOES-NOT-EXIST.md exists anymore, so no new S2 finding was generated.

---

## Verification Summary

| Check | Result |
|-------|--------|
| S2 fires for DOES-NOT-EXIST.md | YES — flagged in Run 1 |
| S2 does NOT fire for 01-evaluation-loop.md | CORRECT — valid path, not flagged |
| Fixture deleted before Run 2 | CONFIRMED |
| Run 2 produces no new entries | CONFIRMED — idempotency holds |
| No duplicate entries after two runs | CONFIRMED |
