# Lint Transcript — iteration-2 / eval-1-full-lint

**Date:** 2026-05-01  
**Mode:** --all  
**Project root:** /home/user/Card-Framework

---

## Step 1 — Surface collection

`find Product/ -name '*.md' -not -path '*/_workflow/_validation/*' | sort`

Result: 90 markdown files collected. Partitioned into:
- active (features/): 39 files
- archived: 0 files (no archive/ subdirectories exist)
- other (ledgers, sessions, decisions, general, spikes, support, _workflow): 51 files

---

## Step 2 — Archive integrity (A1–A4)

All four rules return zero findings:
- A1: `grep -rn '^superseded_by:' Product/features/*/archive/` — no output (no archive/ dirs)
- A2: `grep -rn 'archive/' Product/ --include='*.md'` — hits are only in legitimate prose references to the convention (PRODUCT-BUILDER.md, decisions/0004-archive-convention.md, BACKLOG.md) and template files. No stale inbound links.
- A3a: No active files carry `status: Superseded` or `status: Cancelled`. PRODUCT-BUILDER.md line 741 and _templates/archive.md line 3 mention these in definitions/examples, not as doc statuses.
- A3b: No files exist under any `archive/` path.

---

## Step 3 — Path resolution (S1 + S2)

### S1 Pass 1 — markdown link targets

Ran: `grep -rnoP '\]\(([^)]*\.md[^)]*)\)' Product/ --include='*.md' --exclude-dir='_workflow/_validation'`

All extracted paths verified. Broken links found:

1. **_workflow/PRODUCT-BUILDER.md** (lines 625, 647, 711): `[...](AI-FIRST-AUTOMATION.md)` — prose links. File does not exist at Product/_workflow/AI-FIRST-AUTOMATION.md. → NEW finding ad-20260501-028.

2. **general/GAME-ARCHETYPES.md** (lines 56–58): Three URL-encoded links:
   - `spikes/SabberStone-Architecture-Analysis.md%20(1)` → URL-decoded: `spikes/SabberStone-Architecture-Analysis.md (1)` — does not exist. Actual file: `spikes/sabberstone-architecture.md`. → ad-20260501-029
   - `spikes/Fireplace-Architecture-Analysis.md%20(1)` → URL-decoded: not on disk. → ad-20260501-030
   - `spikes/Brimstone-Architecture-Analysis.md%20(1)` → URL-decoded: not on disk. → ad-20260501-031

3. **support/prior-art/card-game-base-architecture-bb88f456.plan.md** (lines 10, 181, 193): Multiple links into `Docs/` tree — `Docs/SabberStone-Architecture-Analysis.md`, `Docs/Fireplace-Architecture-Analysis.md`, `Docs/Brimstone-Architecture-Analysis.md`, `Docs/Research/Card Game Architecture Research.md`, `Docs/Architecture/ReactiveNetworkQueue.md`, `Docs/Architecture/BlackBoxSimulation.md`. No `Docs/` directory exists. → ad-20260501-032 through 037.

4. **support/prior-art/card-game-base-architecture-117b41c9.plan.md** (lines 36, 137, 138): `Docs/Architecture/ReactiveNetworkQueue.md` and `Docs/SabberStone-Architecture-Analysis.md`. → ad-20260501-038 and 039.

**Excluded (code block / illustrative examples):**
- PRODUCT-BUILDER.md line 91: `features/actions/models/03-action-definition.md` and `features/actions/decisions/0002-async-layer-hierarchy.md` — appear inside inline backtick spans in the sentence "Filenames look like `...`". Purely illustrative.
- REPORT-STYLE.md lines 238, 244: `Product/features/engine/models/04-evaluation-loop.md` and `Product/decisions/0009-async-snapshot-pipeline.md` — inside triple-backtick code block examples. Excluded.

### S1 Pass 2 — bare path mentions

`grep -rnoE 'features/[...]+\.md|Product/[...]+\.md' Product/ --include='*.md' --exclude-dir='_workflow/_validation'`

All bare path mentions cross-checked. No additional broken paths found beyond those identified in Pass 1. All `features/` paths that appear as bare mentions refer to existing files.

### S2 — builds-on references

`grep -rn '^builds-on:' Product/ --include='*.md' | grep -v '/archive/'`

15 files carry builds-on declarations. All paths verified:
- All feature model builds-on chains resolve to existing files.
- _workflow/PRODUCT-BUILDER.md builds-on: [SESSION-CHECKPOINT.md, ledgers/OPEN-QUESTIONS.md] — both exist.
- _workflow/EMOJI-LEGEND.md builds-on: [PRODUCT-BUILDER.md] — exists (resolved relative to _workflow/).
- _workflow/REPORT-STYLE.md builds-on: [PRODUCT-BUILDER.md, EMOJI-LEGEND.md] — both exist.
- decisions/0004-archive-convention.md builds-on: [_workflow/PRODUCT-BUILDER.md] — exists.
- support/samples/pile-counter.md builds-on chains — all three paths exist.
- PRODUCT-BUILDER.md line 143: `builds-on: [features/actions/03, features/triggers/02]` — inside triple-backtick YAML template example. Excluded.

Zero new S2 findings.

---

## Step 4 — Deduplication

Checked against existing open entries (ad-20260501-022 through ad-20260501-027). No overlap with new S1 findings.

---

## Step 5 — Ledger update

Appended 12 new entries (ad-20260501-028 through ad-20260501-039) to `Product/ledgers/ALIGNMENT-DEBT.md` in a single Edit call.

---

## Step 6 — Totals

- New entries written: 12
- Skipped (already open): 2 (ad-20260501-026, ad-20260501-027 — S2 test fixtures)
- Total open after run: 18
