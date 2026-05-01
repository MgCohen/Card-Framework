# Eval Transcript — old_skill baseline run

**Date:** 2026-05-01  
**Skill:** builder:lint (old skill, skill-snapshot/SKILL.md)  
**Arguments:** --all (default)  
**Project:** /home/user/Card-Framework

---

## Step 1 — Collect working surface

```bash
find Product/ -name '*.md' -not -path '*/_workflow/_validation/*' | sort
```

**Result:** 87 files found.  
- Archived: 0 (no `archive/` subdirectories exist under `Product/features/`)  
- Active features: 34 files under `Product/features/`  
- Other: 53 files (ledgers, sessions, decisions, general, spikes, support, _workflow)

---

## Step 2 — Archive integrity checks (--all)

### A1 — Forward-pointer integrity
```bash
grep -rn '^superseded_by:' Product/features/*/archive/
```
Output: `NO ARCHIVE DIRS FOUND`  
**Findings: 0**

### A2 — No stale inbound links to archive
```bash
grep -rn 'archive/' Product/ --include='*.md' --exclude-dir='_workflow/_validation'
```
Output: Matches found but all in workflow convention docs (`PRODUCT-BUILDER.md`, `BACKLOG.md`, `decisions/0004-archive-convention.md`) describing the *concept* of `archive/` in prose and code blocks — not actual links to archived files.  
**Findings: 0**

### A3a — Active files with retired status
```bash
grep -rn 'status: Superseded\|status: Cancelled' Product/ --include='*.md' | grep -v '/archive/'
```
Output: Matches only in `_workflow/_templates/archive.md` (template doc, illustrative) and `_workflow/PRODUCT-BUILDER.md` (prose describing the convention).  
**Findings: 0**

### A3b — Archive files without retired status
```bash
find Product/features -path '*/archive/*.md' | xargs grep -L 'status: Superseded\|status: Cancelled'
```
Output: No archive dirs exist.  
**Findings: 0**

### A4 — Successor presence
No `superseded_by:` entries found (A1 returned none).  
**Findings: 0**

---

## Step 3 — Path-resolution checks (--all)

### S1 — Broken path references

**Pass 1 — markdown link targets:**
```bash
grep -rnoP '\]\(([^)]*\.md[^)]*)\)' Product/ --include='*.md' --exclude-dir='_workflow/_validation'
```
Extracted all markdown link targets. URL-decoded and verified on disk:

- `general/GAME-ARCHETYPES.md` links:
  - `spikes/SabberStone-Architecture-Analysis.md (1)` → MISSING (actual: `spikes/sabberstone-architecture.md`)
  - `spikes/Fireplace-Architecture-Analysis.md (1)` → MISSING (actual: `spikes/fireplace-architecture.md`)
  - `spikes/Brimstone-Architecture-Analysis.md (1)` → MISSING (actual: `spikes/brimstone-architecture.md`)
- `_workflow/PRODUCT-BUILDER.md` links:
  - `AI-FIRST-AUTOMATION.md` (×3, lines 625/647/711) → MISSING
- `support/prior-art/card-game-base-architecture-bb88f456.plan.md` links:
  - `Docs/SabberStone-Architecture-Analysis.md` → MISSING
  - `Docs/Fireplace-Architecture-Analysis.md` → MISSING
  - `Docs/Brimstone-Architecture-Analysis.md` → MISSING
  - `Docs/Research/Card Game Architecture Research.md` → MISSING
  - `Docs/Architecture/ReactiveNetworkQueue.md` → MISSING (×2 more occurrences)
  - `Docs/Architecture/BlackBoxSimulation.md` → MISSING
- `support/prior-art/card-game-base-architecture-117b41c9.plan.md` links:
  - `Docs/Architecture/ReactiveNetworkQueue.md` → MISSING (×2)
  - `Docs/SabberStone-Architecture-Analysis.md` → MISSING

All other markdown links verified as existing on disk.

**Pass 2 — bare path mentions:**
```bash
grep -rnoE 'features/[a-zA-Z0-9_-]+/[a-zA-Z0-9/_. %-]+\.md|Product/[a-zA-Z0-9/_. %-]+\.md' Product/ --include='*.md' --exclude-dir='_workflow/_validation' | grep -v '/archive/'
```
Unique bare paths checked:
- `features/engine/models/DOES-NOT-EXIST.md` — MISSING, appears in `ALIGNMENT-DEBT.md` (existing open entry)
- `features/engine/models/_test-fixture.md` — MISSING, appears in `ALIGNMENT-DEBT.md` (existing open entry)  
- `features/engine/models/_test-fixture2.md` — MISSING (file deleted), appears in `ALIGNMENT-DEBT.md` (existing open entry)
- `features/x/y.md` — MISSING, appears in `_workflow/PRODUCT-BUILDER.md` (prose example in code block, skipped)
- `features/actions/models/03-action-definition.md` — MISSING, appears in `_workflow/PRODUCT-BUILDER.md` (illustrative code block path, skipped)
- `features/actions/decisions/0002-async-layer-hierarchy.md` — MISSING, appears in `_workflow/PRODUCT-BUILDER.md` (illustrative code block path, skipped)
- `Product/decisions/0009-async-snapshot-pipeline.md` — MISSING, appears in `_workflow/REPORT-STYLE.md` (example output in code block, skipped)
- `Product/features/engine/models/04-evaluation-loop.md` — MISSING, appears in `_workflow/REPORT-STYLE.md` (example output in code block, skipped)
- `Product/_workflow/_validation-sandbox.md` — MISSING, appears only in `_workflow/_validation/debt-pass-runbook.md` (excluded dir, skipped)
- `sessions/session-NN.md` — template placeholder, appears in code blocks and README prose examples, skipped

**S1 Findings: 6 (grouped by source document)**

### S2 — builds-on references

```bash
grep -rn '^builds-on:' Product/ --include='*.md' | grep -v '/archive/'
```

All active `builds-on:` entries verified:
- Feature models: all paths resolve to existing active files
- `_workflow/PRODUCT-BUILDER.md` → `[SESSION-CHECKPOINT.md, ledgers/OPEN-QUESTIONS.md]` → both exist
- `_workflow/EMOJI-LEGEND.md` → `[PRODUCT-BUILDER.md]` → resolves relative to `_workflow/` → exists
- `_workflow/REPORT-STYLE.md` → `[PRODUCT-BUILDER.md, EMOJI-LEGEND.md]` → both exist
- `decisions/0004-archive-convention.md` → `[_workflow/PRODUCT-BUILDER.md]` → exists
- `_workflow/PRODUCT-BUILDER.md` line 143 → code block example (not front-matter), skipped
- `_workflow/_templates/archive.md` → `builds-on: []` (empty list), skipped

Note: Previous linter entries `ad-20260501-026/027` referenced `_test-fixture.md` and `_test-fixture2.md` which no longer exist on disk — those files have been deleted, making the existing ALIGNMENT-DEBT entries stale orphans. Not re-flagged (already open).

**S2 Findings: 0 new**

---

## Step 4 — Deduplication

Read `Product/ledgers/ALIGNMENT-DEBT.md`. Open entries:
- ad-20260501-027 (S2, target `_test-fixture2.md`) — stale, but not re-opened (same rule+target)
- ad-20260501-026 (S2, target `_test-fixture.md`) — stale, but not re-opened
- ad-20260501-022 through 025 (cascade entries, not linter)

None of the 6 new S1 findings match existing entries. All 6 are new.

---

## Step 5 — Write ledger entries

**READ-ONLY BASELINE RUN — no entries written to ALIGNMENT-DEBT.md.**

Would-be entries (ad-20260501-028 through 033):
```
- id: ad-20260501-028 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — broken path reference (spikes/SabberStone-Architecture-Analysis.md (1) missing)
- id: ad-20260501-029 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — broken path reference (spikes/Fireplace-Architecture-Analysis.md (1) missing)
- id: ad-20260501-030 | status: open | source: linter | target: general/GAME-ARCHETYPES.md | reason: S1 — broken path reference (spikes/Brimstone-Architecture-Analysis.md (1) missing)
- id: ad-20260501-031 | status: open | source: linter | target: _workflow/PRODUCT-BUILDER.md | reason: S1 — broken path reference (AI-FIRST-AUTOMATION.md missing, ×3 links)
- id: ad-20260501-032 | status: open | source: linter | target: support/prior-art/card-game-base-architecture-bb88f456.plan.md | reason: S1 — broken path references (Docs/... paths missing, ×6 links)
- id: ad-20260501-033 | status: open | source: linter | target: support/prior-art/card-game-base-architecture-117b41c9.plan.md | reason: S1 — broken path references (Docs/Architecture/ReactiveNetworkQueue.md, Docs/SabberStone-Architecture-Analysis.md missing)
```

---

## Observations for eval comparison

1. The old skill detects S1 correctly for prose markdown links but relies on judgment calls for code-block exclusion.
2. The `_test-fixture` stale entries in ALIGNMENT-DEBT (target files deleted) are not auto-closed or flagged by the skill — this is by design ("linter opens, discharge flow closes").
3. Prior-art `.plan.md` files with `Docs/` links are flagged — these are arguably noise since they're imported historical artifacts, not authored Product docs.
4. The skill groups multiple broken links in one source doc into a single entry per doc, rather than one entry per broken link. This is a judgment call in the current skill spec.
5. `AI-FIRST-AUTOMATION.md` is a legitimately missing planned doc (referenced 3× in PRODUCT-BUILDER.md) — a real gap.
