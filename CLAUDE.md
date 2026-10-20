# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project map

Unity card-game framework. Unity version: `6000.6.6f1`.

```
Card Framework/
├── CLAUDE.md                  # This file — router + hard rules
├── docs/
│   ├── architecture.md        # How the current Framework works: codemap, data flow, invariants
│   ├── coding-standards.md    # C# / Unity conventions for code under Assets/
│   ├── feature-layout.md      # Feature structure, asmdefs, spike → framework promotion
│   └── testing.md             # Test tiers (unit / acceptance / integration); which to write
├── Product/spikes/framework-roadmap.md  # Ordered plan for the remaining framework work
├── Assets/
│   ├── Framework/             # Graduated features (vertical slices)
│   ├── Spike/                 # Exploratory / prototype work; deletable
│   └── Tools/Rulebook/        # Rule check kinds + rulebook tests (Unity side)
├── Tools/Rulebook/            # rulebook.json, gate.json, gate.ps1, local app
├── Packages/manifest.json     # UPM dependencies
└── Library/                   # Unity-generated; gitignored
    └── PackageCache/          # Imported UPM package source — read-only
```

## Hard rules

### Git

**Branch and PR.** Every change ships through a feature branch and PR. Never commit to `main` — even for mechanical changes.

**PR target follows the branch you cut from.** If you cut from `main`, PR into `main`. If you cut from another feature branch, PR back into that branch — not into `main`.

### Code

**Verify before claiming done.** Run the gate. It is the authority, not type-checking or "looks right":

```powershell
powershell -NoProfile -File Tools/Rulebook/gate.ps1
```

It compiles, runs the rulebook tests and the Framework EditMode tests in one Unity launch, prints a summary, and exits 1 on FAIL. Add `-With playmode` when you touch Wire, Multiplayer or NGO code. On FAIL, read `Logs/gate/report.json`: each failure names the rule, the file and line, and the fix. Apply each fix, then run the gate again. Options and stages: [Tools/Rulebook/README.md](Tools/Rulebook/README.md).

**Rules live in `Tools/Rulebook/rulebook.json`.** Do not weaken a rule or add an exception to make the gate pass. If a rule is wrong, say so and ask. A new architecture rule goes in the rulebook, not only in prose docs.

**Benchmarks are opt-in.** Tests tagged `[Category("Benchmark")]` (perf/allocation gates — currently in `Assets/Spike/ECS/VerticalSlice/QueryBench/`) are excluded by default via `-testCategory "!Benchmark"`. To run only them, swap to `-testCategory "Benchmark"`.

**Adding or modifying a feature.** Follow [docs/feature-layout.md](docs/feature-layout.md).

**Adding or modifying a test.** Follow [docs/testing.md](docs/testing.md) — it decides the tier (per-feature **unit** in `Framework.<Feature>.Tests`; global **acceptance**, intent-as-picture, in `Framework.Testing.Acceptance.Tests`; global **integration**, hand-written mechanism, in `Framework.Testing.Integration.Tests`) and the rules for keeping pictures canonical and reusing `Framework.Testing.Support`. For acceptance tests, the `author-acceptance-test` skill scaffolds the picture + binding.

**Before inventing a pattern.** Read existing code in the target feature (or relevant spike) first. If no pattern fits, ask — don't quietly introduce a new style alongside an existing one.

**Searching for an API.** If a type or method isn't in `Assets/`, grep `Library/PackageCache/` before assuming it doesn't exist — that's where UPM-imported packages live.

### Conversation

**On user concerns.** When the user says they have concerns, issues, or worries, walk through each one conversationally — current behavior, tradeoff, options — before changing anything. Wait for confirmation.
