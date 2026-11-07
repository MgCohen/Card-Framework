# Base/CLAUDE.md

`Base/` is a .NET stack in the VSA shape. It runs with no Unity. These rules apply to all work under `Base/`. The root `CLAUDE.md` git rules also apply.

Plan page: https://claude.ai/artifact/69oMeCjF3AU12Pc8ZneiXC. It is the source of truth for the layout, the decisions and the phases.

## Scope

- Work only in `Base/`. Do not change `Assets/`, `Packages/`, `ProjectSettings/`, `Tools/` or `docs/`.
- If you need current code from `Assets/`, copy it into `Base/`. Never change the source.

## Base gate

Run the gate before you claim done. It is the authority.

| Machine | Command |
|---|---|
| Windows | `& <core:secrets>/scripts/with-secrets.ps1 powershell -NoProfile -File Base/gate.ps1` |
| Linux, cloud | `<core:secrets>/scripts/with-secrets.sh pwsh Base/gate.ps1` |

```text
gate.ps1 (runs from Base/, so dotnet reads Base/global.json)
  1. solution  each *.csproj under Base/ is in Base.slnx
  2. restore   dotnet restore Base.slnx                          needs GITHUB_TOKEN for the VSA feed
  3. build     dotnet build -c Release --no-restore -warnaserror
  4. test      dotnet test --solution Base.slnx -c Release --no-build
→ PASS / FAIL for each step, exit 0 or 1
```

- `<core:secrets>` is the folder of the `core:secrets` skill. Load the skill to find it.
- `GITHUB_TOKEN` comes from the 1Password vault `agent` through the `core:secrets` wrapper. It needs the scope `read:packages`.
- Do not weaken a step or edit `gate.ps1` to make it pass.
- Do not set these in a project, props or config file: `NoWarn`, `TreatWarningsAsErrors` false, `WarningsNotAsErrors`, `MSBuildWarningsNotAsErrors`, `LangVersion`, `severity = none`.
- Do not add a deeper `Directory.Build.props` or `Directory.Build.targets`.
- Do not add `[Ignore]` or `[Explicit]` to a test.

## Tools

`dotnet-tools.json` pins `vsa.cli`. Run `dotnet tool restore` one time, then `dotnet vsa add module <Name>`.

## Packages

| Package | Version | Feed |
|---|---|---|
| VSA.Sdk and `VSA.*` | `0.1.0-preview.6` | GitHub Packages, owner `MgCohen` |
| Friflo.Engine.ECS | `3.6.0` | nuget.org |
| MemoryPack | `1.21.4` | nuget.org |
| NUnit, NUnit3TestAdapter | `4.6.1`, `6.3.1` | nuget.org |
| Microsoft.Extensions.DependencyInjection | `10.0.14` | nuget.org (`Host.csproj` only) |

- `nuget.config` maps `VSA.*` to GitHub Packages and all other packages to nuget.org.
- Do not add a package source in a project file.
- If you need a package that is not in this table, ask the orchestrator.

## Phase loop

```text
1 Brief → 2 Spec → Approval 1 → 3 Tests (red) → Approval 2 → 4 Code → 5 Gate → 6 Review → 7 PR + note
```

- Spec: API first. Give the entry points, the method shapes, the call sites and the bypass routes.
- Approval 1 and approval 2: stop, and send the spec or the tests to the orchestrator.
- Review: an independent reviewer agent examines the VSA rules, the code standards, the spec, the plan goal and the alignment with the other phases.
- If the review finds a conflict with another phase, send it to the orchestrator. Do not fix the other phase.

## Stacked PRs

| Phase | Cut from | PR into |
|---|---|---|
| P0 | `base` | `base` |
| P1 and later | The previous phase branch | The previous phase branch |

If a review changes a parent branch, rebase the child branch on it.

On Windows, the branch `base` and the folder `Base/` have the same name. Write `git diff base --`, not `git diff base`.

## VSA shape

```text
Base/
├─ Shared/<Name>/     Shared modules: the base systems (Host, Ids, Ecs, Pipeline, Wire, Network, Testing)
├─ Modules/<Name>/    modules: the Cards sample
└─ Tests/             dotnet test projects
```

A Shared module and a module have the layout of [slice-layout.md](https://github.com/MgCohen/vsa-template/blob/main/docs/slice-layout.md): a root project, `Projects/` with one project for each layer, and `Slices/<Feature>/`.

## Friflo boundary

- Only `Shared/Ecs` references Friflo.Engine.ECS.
- Other code reads through the `Entity` wrapper and `IWorldReader`.
- Only a mutation handler holds `IWorldWriter`.

## C# 11

- `Directory.Build.targets` sets `LangVersion` 11, because Unity compiles C# 11. It is in the targets file because VSA.Sdk sets `LangVersion` after the props file.
- C# 11 syntax is permitted.
- Features that need runtime support are forbidden in the layers that Unity uses. An example is `static abstract` interface members.
