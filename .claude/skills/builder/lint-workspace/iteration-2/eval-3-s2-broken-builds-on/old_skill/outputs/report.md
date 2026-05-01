# Lint Report — eval-3-s2-broken-builds-on (old skill baseline)

Run: `--paths-only` on `/home/user/Card-Framework`
Fixture: `Product/features/engine/models/_test-fixture2.md`
Fixture front matter:
```
builds-on: [features/engine/models/DOES-NOT-EXIST.md, features/engine/models/01-evaluation-loop.md]
```

---

## S2 Behavior Observed

**S2 fires for DOES-NOT-EXIST.md:** YES — finding detected.
**S2 fires for 01-evaluation-loop.md:** NO — path exists, correctly no finding.

Because a prior eval run had already written `ad-20260501-027` for this exact finding, the deduplication step correctly skipped it as a duplicate. No new ledger entry was written.

**Existing ledger entry (duplicate, correctly skipped):**
```
- id: ad-20260501-027 | status: open | source: linter | target: features/engine/models/_test-fixture2.md | reason: S2 — builds-on references missing or archived path (features/engine/models/DOES-NOT-EXIST.md)
```

---

## Lint Report Output

📊 0 new entries · 1 skipped (already open) · 5 total open

### Findings by rule
| Rule | New | Description |
|------|-----|-------------|
| S1   | 0   | Broken path references |
| S2   | 0   | Broken builds-on references (1 skipped — duplicate) |

**S2 detail — duplicate skipped:**
- `features/engine/models/_test-fixture2.md` builds-on `features/engine/models/DOES-NOT-EXIST.md` — already open as `ad-20260501-027`
- `features/engine/models/01-evaluation-loop.md` — **exists**, no finding

⚠️ Pass-mode: 5 open debt entries — consider a discharge pass before continuing.

---

## S1 path check results

Unique bare-path references to non-existent files (excluding session notes, BACKLOG, ALIGNMENT-DEBT):
- `features/actions/decisions/0002-async-layer-hierarchy.md` — illustrative example in `_workflow/PRODUCT-BUILDER.md` code block → NOT flagged
- `features/actions/models/03-action-definition.md` — illustrative example in `_workflow/PRODUCT-BUILDER.md` code block → NOT flagged
- `features/engine/models/04-evaluation-loop.md` — sample report in `_workflow/REPORT-STYLE.md` → NOT flagged
- `features/engine/models/DOES-NOT-EXIST.md` — appears in `_test-fixture2.md` front matter → would be flagged as S1 if not already covered by S2

No new S1 entries written.

---

## Eval Assessment

**S2 correctness: PASS**
- Fires for the missing path (`DOES-NOT-EXIST.md`): YES
- Does NOT fire for the existing path (`01-evaluation-loop.md`): CORRECT
- Idempotency correctly exercised (duplicate suppressed)
