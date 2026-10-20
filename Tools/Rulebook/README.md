# Rulebook and gate

The rulebook holds the project's enforceable rules as data. Each rule generates a test. The gate runs
the compile, the rule tests and the test suites in one command, and writes a report that people,
the app and agents read.

## Run the gate

```powershell
powershell -NoProfile -File Tools/Rulebook/gate.ps1                 # compile + rules + Framework EditMode
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -With playmode  # also the NGO PlayMode suites
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -All            # also the spike and package tests
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -Only rules     # only the rule tests
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -Rule ARCH-05   # one rule and its linked tests
```

Close the Unity Editor first: a batch run cannot open a project that the Editor has open.

The console prints one line per stage, then each failure with the rule, the file and line, and the
fix. The exit code is 0 on PASS and 1 on FAIL.

## Files

| File | Who edits it | What it is |
|---|---|---|
| `rulebook.json` | People, the app | Books and rules. Each rule has a check kind and its parameters. |
| `gate.json` | People, the app | Stages, in order. A test assembly belongs to the first stage of its kind whose patterns match it. |
| `schema/*.schema.json` | People | JSON Schemas for editors and the app. `MetaTests` keeps the check-kind list in sync. |
| `gate.ps1` | People | The gate. Runs Unity through the `unity` CLI. |
| `app/` | People | The local app. |
| `fixtures/` | People | On-disk fixtures for `CheckKindTests` (one deliberate violation per kind). |
| `Logs/gate/report.json` | Generated | Last run: stages, compile errors, failures with violations and fixes, rule results. |
| `Logs/gate/catalog.json` | Generated | Every test in the last run: assembly, stage, tier, linked rules, result. |

The Unity side lives in `Assets/Tools/Rulebook/`: `RuleAttribute` (Runtime), the check kinds
(Editor), the fixtures assembly, and the tests (`RulebookTests`, `CheckKindTests`, `MetaTests`).

## Rules

A rule has `id`, `book`, `title`, `rule`, `why`, `fix`, `status` (`enforced` or `planned`), `check`
(`kind` + `params`) and optional `exceptions`.

- **A rule of an existing kind needs no code.** Add it to `rulebook.json`. `RulebookTests` runs one
  test case per rule, named by its id.
- **A hand-written test links to a rule with `[Rule("ID")]`.** Use the kind `attached` for a rule
  that only hand-written tests check. The rule fails while no test carries its id.
- **An exception tolerates a known violation.** `target` is a glob on the violation target,
  `reason` says why, `until` names the work that removes it. An exception that no longer matches a
  violation fails the rule, so the list only shrinks.
- **A planned rule** is written down with no check yet. Its test case is ignored.

## Check kinds

| Kind | Checks | Main params |
|---|---|---|
| `asmdef.noCycles` | No cycles in asmdef references | `roots` |
| `asmdef.allowedReferences` | Each in-scope asmdef references only its listed assemblies; unlisted asmdefs fail | `roots`, `scope`, `exclude`, `allowed` |
| `asmdef.forbiddenReferences` | No reference matches a forbidden pattern | `roots`, `from`, `forbidden` |
| `ivt.allowlist` | `InternalsVisibleTo` grants match a table | `assemblies`, `exclude`, `allowed` |
| `il.callersOnly` | Only subclasses of a base type call matching methods (IL scan) | `assemblies`, `callees`, `allowedCallers` |
| `il.forbiddenCalls` | Subclasses of a base type do not call matching methods (IL scan) | `assemblies`, `callers`, `callees` |
| `type.propertyTypes` | A type's public properties have allowed types | `assembly`, `type`, `allowed` |
| `type.noSubtypesIn` | An assembly declares no subtypes of a base, except allowed ones | `assembly`, `baseType`, `allowed` |
| `feature.hasTests` | Each feature folder has a test assembly with tests | `root`, `runtimeFolder`, `testFolders` |
| `tests.requireAttribute` | Tests carry an attribute that names an existing file; optionally every file is named | `assemblies`, `attribute`, `fileFolder` |
| `attached` | At least one test carries `[Rule("ID")]` | none |

To add a kind: write an `IRuleCheck` in `Assets/Tools/Rulebook/Editor/Checks/`, register it in
`CheckRegistry`, add it to the schema enum, and add a `CheckKindTests` case with a fixture violation.

## The app

```powershell
python Tools/Rulebook/app/server.py
```

Open http://127.0.0.1:8765/. The app reads and writes `rulebook.json` and `gate.json`, starts the
gate, streams its output, and shows the last report and the test catalog. It binds to 127.0.0.1
only, rejects other hosts and origins, and needs a per-process token on every API call. It only
starts `gate.ps1`, with stage and rule names checked against the JSON files.
