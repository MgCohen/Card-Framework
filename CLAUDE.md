# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project map

Unity card-game framework. Unity version: `6000.4.0f1`.

```
Card Framework/
├── CLAUDE.md                  # This file — router + hard rules
├── docs/
│   ├── architecture.md        # Cross-cutting invariants and rationale (stub)
│   ├── coding-standards.md    # C# / Unity conventions for code under Assets/
│   └── feature-layout.md      # Feature structure, asmdefs, spike → framework promotion
├── Assets/
│   ├── Framework/             # Graduated features (vertical slices)
│   └── Spike/                 # Exploratory / prototype work; deletable
├── Packages/manifest.json     # UPM dependencies
└── Library/                   # Unity-generated; gitignored
    └── PackageCache/          # Imported UPM package source — read-only
```

## Hard rules

### Git

**Branch and PR.** Every change ships through a feature branch and PR. Never commit to `main` — even for mechanical changes.

**PR target follows the branch you cut from.** If you cut from `main`, PR into `main`. If you cut from another feature branch, PR back into that branch — not into `main`.

### Code

**Verify before claiming done.** Run the affected tests in headless batch mode. The batch run is the authority, not type-checking or "looks right":

```
Unity.exe -batchmode -nographics -projectPath . -runTests \
  -testPlatform EditMode \
  [-assemblyNames Framework.<Feature>.Tests] \
  [-testFilter <Namespace>.<Class>.<Method>] \
  -testResults Logs/test-results.xml -logFile Logs/test.log
```

Read `Logs/test-results.xml` for pass/fail, `Logs/test.log` for compile errors and stack traces.

**Adding or modifying a feature.** Follow [docs/feature-layout.md](docs/feature-layout.md).

**Before inventing a pattern.** Read existing code in the target feature (or relevant spike) first. If no pattern fits, ask — don't quietly introduce a new style alongside an existing one.

**Searching for an API.** If a type or method isn't in `Assets/`, grep `Library/PackageCache/` before assuming it doesn't exist — that's where UPM-imported packages live.

### Conversation

**On user concerns.** When the user says they have concerns, issues, or worries, walk through each one conversationally — current behavior, tradeoff, options — before changing anything. Wait for confirmation.
