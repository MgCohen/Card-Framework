# Transcript — eval-3-s2-broken-builds-on (old skill, iteration 2)

Date: 2026-05-01
Skill: builder:lint (old skill / skill-snapshot)
Arguments: --paths-only

---

## Step 1 — Fixture created

File: `Product/features/engine/models/_test-fixture2.md`
Content:
```
---
type: model
status: draft
builds-on: [features/engine/models/DOES-NOT-EXIST.md, features/engine/models/01-evaluation-loop.md]
---

# Test Fixture 2

This is a temporary fixture for linting tests.
```

---

## Step 2 — Collect working surface

`find Product/ -name '*.md' -not -path '*/_workflow/_validation/*' | sort`

Result: 84 files. Partitioned into:
- active (features/): 26 files + _test-fixture2.md
- archived: 0 files
- other: ledgers, sessions, decisions, general, spikes, support, _workflow

---

## Step 3 — S1: bare path reference check

Command:
```bash
grep -rnoE 'features/[a-zA-Z0-9_-]+/[a-zA-Z0-9/_. %-]+\.md|Product/[a-zA-Z0-9/_. %-]+\.md' \
  Product/ --include='*.md' --exclude-dir='_workflow/_validation' \
  | grep -v '/archive/'
```

Unique missing paths (excluding sessions, BACKLOG, ALIGNMENT-DEBT):
1. `features/actions/decisions/0002-async-layer-hierarchy.md` — found only in PRODUCT-BUILDER.md line 91 inside a prose example sentence (illustrative) → SKIP
2. `features/actions/models/03-action-definition.md` — found only in PRODUCT-BUILDER.md line 91 inside same illustrative sentence → SKIP
3. `features/engine/models/04-evaluation-loop.md` — found only in REPORT-STYLE.md line 238 inside a sample report section (illustrative) → SKIP
4. `features/engine/models/DOES-NOT-EXIST.md` — found in `_test-fixture2.md` line 4 (front matter, not illustrative)

`DOES-NOT-EXIST.md` would be an S1 finding, but it is also covered by S2. Checking dedup...

---

## Step 4 — S2: builds-on check

Command:
```bash
grep -rn '^builds-on:' Product/ --include='*.md' | grep -v '/archive/'
```

Fixture line: `builds-on: [features/engine/models/DOES-NOT-EXIST.md, features/engine/models/01-evaluation-loop.md]`

Path checks:
- `Product/features/engine/models/DOES-NOT-EXIST.md` → MISSING → S2 finding
- `Product/features/engine/models/01-evaluation-loop.md` → EXISTS → no finding

All other builds-on entries checked: all paths exist, no other findings.

---

## Step 5 — Deduplication

Read `Product/ledgers/ALIGNMENT-DEBT.md`.

Existing open entries relevant to this run:
```
- id: ad-20260501-027 | status: open | source: linter | target: features/engine/models/_test-fixture2.md | reason: S2 — builds-on references missing or archived path (features/engine/models/DOES-NOT-EXIST.md)
```

S2 finding for `_test-fixture2.md` / `DOES-NOT-EXIST.md` matches `ad-20260501-027` exactly → DUPLICATE, skip.

No new ledger entries written.

---

## Step 6 — Fixture deleted

`rm Product/features/engine/models/_test-fixture2.md` → confirmed deleted.

---

## Final State

- ALIGNMENT-DEBT.md: unchanged (no new entries)
- Open entries: 5 (ad-20260501-022 through ad-20260501-027, minus resolved)
- _test-fixture2.md: deleted

---

## Eval Result

| Check | Expected | Observed | Pass? |
|-------|----------|----------|-------|
| S2 fires for DOES-NOT-EXIST.md | YES | YES (ad-20260501-027, already open) | PASS |
| S2 does NOT fire for 01-evaluation-loop.md | NO | NO | PASS |
| Idempotency (duplicate suppressed) | YES | YES | PASS |
| Fixture deleted | YES | YES | PASS |

**Overall: PASS**
