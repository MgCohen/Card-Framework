# Transcript — Eval-3 S2 Broken builds-on

## Steps and Tool Calls

### Step 1: Read SKILL.md
- Tool: Read `/home/user/Card-Framework/.claude/skills/builder/lint/SKILL.md`
- Purpose: Understand skill behavior, rules S1/S2, idempotency requirement

### Step 2: Create test fixture
- Tool: Write `/home/user/Card-Framework/Product/features/engine/models/_test-fixture.md`
- Content: front matter with `builds-on: [features/engine/models/DOES-NOT-EXIST.md, features/engine/models/01-evaluation-loop.md]`

### Step 3: Run 1 — /builder:lint --paths-only (fixture present)
- Tool: Skill `builder:lint` with args `--paths-only`
- Skill internals:
  - Bash: `find Product/ -name '*.md' -not -path '*/_workflow/_validation/*' | sort` — collected working surface
  - Bash: `grep -rnoE 'features/[a-z]+/...' ...` — extracted S1 path refs; all resolved
  - Bash: `grep -rn '^builds-on:' ...` — found fixture's broken reference to DOES-NOT-EXIST.md
  - Bash: checked existing ledger for duplicates — none found
  - Edit: appended `ad-20260501-026` to ALIGNMENT-DEBT.md under "Linter findings — 2026-05-01"
- Output: 1 new S2 entry, 0 skipped, 7 total open

### Step 4: Verify S2 discrimination
- DOES-NOT-EXIST.md: flagged (does not exist on disk)
- 01-evaluation-loop.md: NOT flagged (exists at `Product/features/engine/models/01-evaluation-loop.md`)

### Step 5: Delete fixture
- Tool: Bash `rm /home/user/Card-Framework/Product/features/engine/models/_test-fixture.md`
- Note: The file was already deleted during/after skill execution; rm returned "No such file or directory"
- Confirmed: `ls` shows fixture is gone

### Step 6: Run 2 — /builder:lint --paths-only (fixture absent)
- Tool: Skill `builder:lint` with args `--paths-only`
- Skill internals:
  - Bash: collected working surface — fixture no longer in file list
  - Bash: `grep -rn '^builds-on:'` — only valid paths found (fixture gone)
  - Read: checked ALIGNMENT-DEBT.md — ad-20260501-026 already open
  - No new entries to write
- Output: 0 new entries, 0 skipped, 7 total open — idempotency confirmed

### Step 7: Save outputs
- Bash: Wrote report.md and transcript.md to eval-3 output directory

## Key Findings

1. S2 correctly fires for broken `builds-on:` path (DOES-NOT-EXIST.md) — entry ad-20260501-026 opened
2. S2 correctly ignores valid `builds-on:` path (01-evaluation-loop.md) — no false positive
3. Run 2 after fixture deletion: no new entries created — idempotency holds
4. Ledger entry ad-20260501-026 remains open (orphaned target, but not duplicated)

## File modified
- `/home/user/Card-Framework/Product/ledgers/ALIGNMENT-DEBT.md` — one new entry added (ad-20260501-026)
