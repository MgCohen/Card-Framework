# Rulebook and gate

The rulebook holds the project's enforceable rules as data. Each rule generates a test. The gate runs
the compile, the rule tests and the test suites in one command, and writes a report that people,
the app and agents read.

## Run the gate

```powershell
powershell -NoProfile -File Tools/Rulebook/gate.ps1                 # compile + rules + Framework EditMode
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -With playmode  # also the NGO PlayMode suites
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -With review    # also the agent review (costs model tokens)
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -All            # every stage
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -Only rules     # only the rule tests
powershell -NoProfile -File Tools/Rulebook/gate.ps1 -Rule ARCH-05   # one rule and its linked tests
```

Close the Unity Editor first: a batch run cannot open a project that the Editor has open.

The console prints one line per stage, then each failure with the rule, the file and line, and the
fix. The exit code is 1 on FAIL and 0 on PASS or WARN. WARN means only advisory rules failed.
Each run is kept in `Logs/gate/history/<yyyyMMdd-HHmmss>/` (the newest 50).

## Files

| File | Who edits it | What it is |
|---|---|---|
| `rulebook.json` | People, the app | Books and rules. Each rule has a check kind and its parameters. |
| `gate.json` | People, the app | Stages, in order. A test assembly belongs to the first stage of its kind whose patterns match it. |
| `schema/*.schema.json` | People | JSON Schemas for editors and the app. `MetaTests` keeps the check-kind list in sync. |
| `schema/check-kinds.json` | People | One descriptor per check kind: its judge, description and typed params. The app builds its forms from it. |
| `review.py` | People | The agent judge. Runs `agent.review` rules through headless `claude -p`. |
| `gate.ps1` | People | The gate. Runs Unity through the `unity` CLI. |
| `app/` | People | The local app. |
| `fixtures/` | People | On-disk fixtures for `CheckKindTests` (one deliberate violation per kind). |
| `Logs/gate/report.json` | Generated | Last run: stages, compile errors, failures with violations and fixes, rule results. |
| `Logs/gate/catalog.json` | Generated | Every test in the last run: assembly, stage, tier, linked rules, result. |

The Unity side lives in `Assets/Tools/Rulebook/`: `RuleAttribute` (Runtime), the check kinds
(Editor), the fixtures assembly, and the tests (`RulebookTests`, `CheckKindTests`, `MetaTests`).

## Rules

A rule has `id`, `book`, `title`, `rule`, `why`, `fix`, `status` (`enforced` or `planned`),
`severity` (`blocking` or `advisory`), `check` (`kind` + `params`), optional `exceptions`, and
optional `extra` values for the custom fields defined in the top-level `fields` template.

- **A rule of an existing kind needs no code.** Add it to `rulebook.json`. `RulebookTests` runs one
  test case per rule, named by its id.
- **A hand-written test links to a rule with `[Rule("ID")]`.** Use the kind `attached` for a rule
  that only hand-written tests check. The rule fails while no test carries its id.
- **An exception tolerates a known violation.** `target` is a glob on the violation target,
  `reason` says why, `until` names the work that removes it. An exception that no longer matches a
  violation fails the rule, so the list only shrinks.
- **A planned rule** is written down with no check yet. Its test case is ignored.

## Judges

Every rule has one judge, set by its check kind:

| Judge | Deterministic | Runs in | Kinds |
|---|---|---|---|
| Static check | Yes | Unity EditMode, `rules` stage | the asmdef, ivt, il, type, feature and tests kinds |
| Linked tests | Yes | The stage that owns the test assembly | `attached` |
| Agent review | No | `review` stage, headless `claude -p` with only Read, Grep and Glob | `agent.review` |

An agent verdict can change between runs, so agent rules default to `advisory`. The `review` stage
is optional: it runs with `-With review`, `-All` or `-Rule <agent rule id>`. Each result records
the model, turns, duration, cost and the agent's summary.

Agent rules can judge code (`CODE-*`) and the suite itself (`REV-*`): whether rules are well
written, whether linked tests prove their rule, and whether acceptance bindings match their
pictures.

## Check kinds

| Kind | Checks | Main params |
|---|---|---|
| `asmdef.noCycles` | No cycles in asmdef references | `roots` |
| `asmdef.allowedReferences` | Each in-scope asmdef references only its listed assemblies; unlisted asmdefs fail | `roots`, `scope`, `exclude`, `allowed` |
| `asmdef.forbiddenReferences` | No reference matches a forbidden pattern | `roots`, `from`, `forbidden` |
| `ivt.allowlist` | `InternalsVisibleTo` grants match a table | `assemblies`, `exclude`, `allowed` |
| `il.callersOnly` | Only subclasses of a base type call matching methods (IL scan) | `assemblies`, `callees`, `allowedCallers` |
| `il.forbiddenCalls` | Subclasses of a base type do not call matching methods (IL scan) | `assemblies`, `callers`, `callees` |
| `type.memberTypes` | A type's public properties, fields and method return types have allowed types | `assembly`, `type`, `allowed` |
| `type.noSubtypesIn` | An assembly declares no subtypes of a base, except allowed ones | `assembly`, `baseType`, `allowed` |
| `feature.hasTests` | Each feature folder has a test assembly with tests | `root`, `runtimeFolder`, `testFolders` |
| `tests.requireAttribute` | Tests carry an attribute that names an existing file; optionally every file is named | `assemblies`, `attribute`, `fileFolder` |
| `attached` | At least one test carries `[Rule("ID")]` | none |
| `agent.review` | A model reviews a subject against a prompt | `subject` (files, diff, rules, linkedTests, pictures), `prompt`, `model` |

To add a kind: write an `IRuleCheck` in `Assets/Tools/Rulebook/Editor/Checks/`, register it in
`CheckRegistry`, add it to the schema enum and to `schema/check-kinds.json`, and add a
`CheckKindTests` case with a fixture violation.

## The app

```powershell
python Tools/Rulebook/app/server.py
```

Open http://127.0.0.1:8765/.

| Tab | What it does |
|---|---|
| Rules | Rules by book, with a status dot. The editor has Definition (form built from the kind descriptor, or raw JSON), Result (violations, agent verdict), Tests (linked tests) and History. |
| Template | Books and custom rule fields. |
| Gate | The stage flow, settings, and a drag-to-reorder stage list. Config only. |
| Tests | The test catalog as a foldable tree: tier, assembly, class. Rule ids link to their rule. |
| Runs | Run history with a duration chart, and the full report of each run. | It binds to 127.0.0.1
only, rejects other hosts and origins, and needs a per-process token on every API call. It only
starts `gate.ps1`, with stage and rule names checked against the JSON files.
