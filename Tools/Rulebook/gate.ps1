<#
.SYNOPSIS
  Runs the project gate: compile, rulebook, tests. Stages come from Tools/Rulebook/gate.json.

.DESCRIPTION
  All EditMode stages share one Unity launch, and all PlayMode stages share another. Results are
  split back into stages by test assembly. Writes Logs/gate/report.json and Logs/gate/catalog.json,
  prints a summary, and exits 0 on PASS and 1 on FAIL.

.EXAMPLE
  powershell -NoProfile -File Tools/Rulebook/gate.ps1
  powershell -NoProfile -File Tools/Rulebook/gate.ps1 -With playmode
  powershell -NoProfile -File Tools/Rulebook/gate.ps1 -Only rules
  powershell -NoProfile -File Tools/Rulebook/gate.ps1 -Rule ARCH-05
#>
[CmdletBinding()]
param(
  [string[]] $Only = @(),
  [string[]] $Skip = @(),
  [string[]] $With = @(),
  [switch]   $All,
  [string]   $Rule,
  [string]   $Filter,
  [int]      $TimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
$Root    = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$OutDir  = Join-Path $Root 'Logs\gate'
$Utf8    = New-Object System.Text.UTF8Encoding $false
$Started = Get-Date

function Read-Json([string] $path) { [IO.File]::ReadAllText($path) | ConvertFrom-Json }
function Write-Json([string] $path, $value) { [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 12), $Utf8) }
function Test-Glob([string[]] $patterns, [string] $value) {
  foreach ($p in $patterns) { if ($value -like $p) { return $true } }
  return $false
}

# --- configuration ---------------------------------------------------------------------------

$gate     = Read-Json (Join-Path $PSScriptRoot 'gate.json')
$rulebook = Read-Json (Join-Path $PSScriptRoot 'rulebook.json')
$rules    = @{}
foreach ($r in $rulebook.rules) { $rules[$r.id] = $r }

# "-Only a,b" arrives as one string under powershell -File; accept both forms.
$Only = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$Skip = @($Skip | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$With = @($With | ForEach-Object { $_ -split ',' } | Where-Object { $_ })

$knownIds = @($gate.stages | ForEach-Object { $_.id })
foreach ($id in @($Only + $Skip + $With)) {
  if ($id -and $knownIds -notcontains $id) { throw "Unknown stage '$id'. Stages: $($knownIds -join ', ')" }
}
if ($Rule -and -not $rules.ContainsKey($Rule)) { throw "Unknown rule '$Rule'." }

$stages = foreach ($s in $gate.stages) {
  $enabled = -not $s.disabled -and (-not $s.optional -or $All -or $With -contains $s.id)
  if ($Only.Count -gt 0) { $enabled = $Only -contains $s.id }
  if ($Skip -contains $s.id) { $enabled = $false }
  [pscustomobject]@{
    id = $s.id; kind = $s.kind; title = $s.title; enabled = $enabled
    patterns = @($s.assemblies)
    result = 'SKIP'; total = 0; passed = 0; failed = 0; skipped = 0
  }
}

# A rule run selects the rule's own test case plus every test linked to it in the last catalog.
if ($Rule) {
  $names = @("Tools.Rulebook.Tests.RulebookTests.$Rule *")
  $catalogPath = Join-Path $OutDir 'catalog.json'
  if (Test-Path $catalogPath) {
    $names += @((Read-Json $catalogPath).tests | Where-Object { $_.rules -contains $Rule } | ForEach-Object { $_.fullname })
  }
  $Filter = ($names | ForEach-Object { '^' + [regex]::Escape($_).Replace('\ \*', '.*').Replace('\*', '.*') + '$' }) -join '|'
  foreach ($s in $stages) { $s.enabled = ($s.kind -ne 'playmode') -and ($s.id -ne 'others') }
}

# --- test assemblies -------------------------------------------------------------------------

# A test assembly is any asmdef that opts into the test framework. Editor-only ones run in
# EditMode, the rest in PlayMode. Each belongs to the first stage of its kind that matches it.
$testAsmdefs = foreach ($dir in @('Assets', 'Packages')) {
  $path = Join-Path $Root $dir
  if (-not (Test-Path $path)) { continue }
  Get-ChildItem -Path $path -Recurse -Filter '*.asmdef' -File | ForEach-Object {
    $j = Read-Json $_.FullName
    $isTest = (@($j.defineConstraints) -contains 'UNITY_INCLUDE_TESTS') -or
              (@($j.references) -contains 'UnityEngine.TestRunner') -or
              (@($j.optionalUnityReferences) -contains 'TestAssemblies')
    if ($isTest) {
      $platforms = @($j.includePlatforms)
      [pscustomobject]@{ name = $j.name; mode = $(if ($platforms.Count -eq 1 -and $platforms[0] -eq 'Editor') { 'editmode' } else { 'playmode' }) }
    }
  }
}

$owner = @{}
foreach ($a in $testAsmdefs) {
  $match = $gate.stages | Where-Object { $_.kind -eq $a.mode -and (Test-Glob @($_.assemblies) $a.name) } | Select-Object -First 1
  if ($match) { $owner[$a.name] = $match.id }
}

# --- run Unity -------------------------------------------------------------------------------

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Remove-Item -Recurse -Force (Join-Path $OutDir 'rules') -ErrorAction SilentlyContinue
$unity = Get-Command unity -ErrorAction SilentlyContinue
if (-not $unity) { throw "The Unity CLI ('unity') is not on PATH." }

# Runs the Unity CLI with its own output in a file, so the console shows only the gate summary.
# Waits for the CLI process only: Start-Process -Wait would also wait for helper processes the
# CLI leaves running (for example its background update check).
function Invoke-Unity([string[]] $arguments, [string] $outFile) {
  $quoted = $arguments | ForEach-Object { if ($_ -match '[\s";|]') { '"' + $_.Replace('"', '\"') + '"' } else { $_ } }
  $p = Start-Process -FilePath $unity.Source -ArgumentList ($quoted -join ' ') -NoNewWindow -PassThru `
         -RedirectStandardOutput $outFile -RedirectStandardError "$outFile.err"
  $null = $p.Handle   # caches the handle so ExitCode is readable after exit
  $p.WaitForExit()
  return $p.ExitCode
}

$runs = @()
foreach ($mode in @('editmode', 'playmode')) {
  $enabledIds = @($stages | Where-Object { $_.enabled -and $_.kind -eq $mode } | ForEach-Object { $_.id })
  $asms = @($owner.Keys | Where-Object { $enabledIds -contains $owner[$_] } | Sort-Object)
  if ($asms.Count -eq 0) { continue }

  $xml  = Join-Path $OutDir "$mode-results.xml"
  $log  = Join-Path $OutDir "$mode.log"
  Remove-Item $xml, $log -ErrorAction SilentlyContinue
  $mArg = $(if ($mode -eq 'editmode') { 'EditMode' } else { 'PlayMode' })
  $cli  = @('test', $Root, '--mode', $mArg, '--output', $xml, '--timeout', $TimeoutSeconds, '--no-banner', '--format', 'json')
  if ($Filter) { $cli += @('--filter', $Filter) }
  $pass = @('-assemblyNames', ($asms -join ';'), '-logFile', $log)
  if ($gate.testCategory) { $pass += @('-testCategory', $gate.testCategory) }

  Write-Host "Running $mArg ($($asms.Count) assemblies)..."
  $code = Invoke-Unity ($cli + @('--') + $pass) (Join-Path $OutDir "$mode-cli.json")
  $runs += [pscustomobject]@{ mode = $mode; xml = $xml; log = $log; exit = $code }
}

# Every test run compiles first, so the compile stage reports whenever Unity ran at all.
$compileStage = $stages | Where-Object { $_.kind -eq 'compile' } | Select-Object -First 1
if ($compileStage -and $runs.Count -gt 0) { $compileStage.enabled = $true }
if ($compileStage -and $compileStage.enabled -and $runs.Count -eq 0) {
  $log = Join-Path $OutDir 'compile.log'
  Write-Host 'Compiling...'
  $code = Invoke-Unity @('run', $Root, '--timeout', $TimeoutSeconds, '--no-banner', '--format', 'json', '--', '-logFile', $log) (Join-Path $OutDir 'compile-cli.json')
  $runs += [pscustomobject]@{ mode = 'compile'; xml = $null; log = $log; exit = $code }
}

# --- collect results -------------------------------------------------------------------------

$compileErrors = @()
$infraErrors   = @()
foreach ($run in $runs) {
  if (-not (Test-Path $run.log)) { $infraErrors += "$($run.mode): Unity wrote no log (exit $($run.exit))."; continue }
  $lines = [IO.File]::ReadAllLines($run.log)
  foreach ($l in $lines) {
    if ($l -match '^(?<file>[^()]+)\((?<line>\d+),(?<col>\d+)\): error (?<code>CS\d+): (?<msg>.*)$') {
      $compileErrors += [pscustomobject]@{ file = $Matches.file.Replace('\', '/'); line = [int]$Matches.line; code = $Matches.code; message = $Matches.msg.Trim() }
    }
  }
  if ($lines -match 'another Unity instance is running with this project open') {
    $infraErrors += 'The project is open in the Unity Editor. Close it, or run the tests from the Editor.'
  }
  elseif ($run.xml -and -not (Test-Path $run.xml) -and $compileErrors.Count -eq 0) {
    $infraErrors += "$($run.mode): Unity exited $($run.exit) without results. See $($run.log)."
  }
}
$compileErrors = @($compileErrors | Sort-Object file, line, code -Unique)
if ($compileStage) { $compileStage.result = $(if ($compileErrors.Count -gt 0) { 'FAIL' } elseif ($infraErrors.Count -gt 0) { 'ERROR' } else { 'PASS' }) }

$tests    = @()
$failures = @()
foreach ($run in $runs | Where-Object { $_.xml -and (Test-Path $_.xml) }) {
  [xml] $doc = [IO.File]::ReadAllText($run.xml)
  foreach ($suite in $doc.SelectNodes("//test-suite[@type='Assembly']")) {
    $asm = [IO.Path]::GetFileNameWithoutExtension($suite.name)
    $stageId = $owner[$asm]
    foreach ($case in $suite.SelectNodes('.//test-case')) {
      $ruleIds = @($case.SelectNodes("ancestor-or-self::*/properties/property[@name='Rule']") | ForEach-Object { $_.value } | Select-Object -Unique)
      $tier = switch -Wildcard ($asm) {
        '*.PlayTests'                 { 'playmode' }
        'Framework.Testing.Acceptance*' { 'acceptance' }
        'Framework.Testing.Integration*' { 'integration' }
        'Tools.Rulebook.Tests'        { 'rulebook' }
        default                       { 'unit' }
      }
      $entry = [pscustomobject]@{
        name = $case.name; fullname = $case.fullname; assembly = $asm; stage = $stageId; tier = $tier
        rules = $ruleIds; result = $case.result; duration = [double]$case.duration
      }
      $tests += $entry

      $stage = $stages | Where-Object { $_.id -eq $stageId } | Select-Object -First 1
      if (-not $stage) { continue }
      $stage.total++
      switch ($case.result) { 'Passed' { $stage.passed++ } 'Failed' { $stage.failed++ } default { $stage.skipped++ } }

      if ($case.result -eq 'Failed') {
        $message = $case.SelectSingleNode('failure/message')
        $failure = [pscustomobject]@{
          stage = $stageId; test = $case.fullname; assembly = $asm; rules = $ruleIds
          message = $(if ($message) { $message.InnerText.Trim() } else { '' })
          violations = @(); fix = $null
        }
        foreach ($id in $ruleIds) {
          if ($rules.ContainsKey($id)) { $failure.fix = $rules[$id].fix }
          $file = Join-Path $OutDir "rules\$id.json"
          if (Test-Path $file) { $failure.violations += @((Read-Json $file).violations) }
        }
        $failures += $failure
      }
    }
  }
}

foreach ($s in $stages | Where-Object { $_.kind -ne 'compile' -and $_.enabled }) {
  $s.result = $(if ($s.failed -gt 0) { 'FAIL' } elseif ($compileErrors.Count -gt 0 -or $infraErrors.Count -gt 0) { 'ERROR' } elseif ($s.total -eq 0) { 'EMPTY' } else { 'PASS' })
}

$bad    = @($stages | Where-Object { $_.enabled -and @('FAIL', 'ERROR') -contains $_.result })
$result = $(if ($bad.Count -gt 0) { 'FAIL' } else { 'PASS' })

$ruleResults = @()
$rulesDir = Join-Path $OutDir 'rules'
if (Test-Path $rulesDir) { $ruleResults = @(Get-ChildItem $rulesDir -Filter '*.json' | ForEach-Object { Read-Json $_.FullName }) }

$report = [pscustomobject]@{
  result        = $result
  startedAt     = $Started.ToString('o')
  durationSec   = [int]((Get-Date) - $Started).TotalSeconds
  selection     = [pscustomobject]@{ only = $Only; skip = $Skip; with = $With; all = [bool]$All; rule = $Rule; filter = $Filter }
  stages        = @($stages | Select-Object id, title, kind, enabled, result, total, passed, failed, skipped)
  compileErrors = $compileErrors
  infraErrors   = $infraErrors
  failures      = $failures
  rules         = $ruleResults
}
Write-Json (Join-Path $OutDir 'report.json') $report
# A filtered run (-Rule, -Filter, -Only) updates its tests in the catalog and keeps the rest.
$catalogFile = Join-Path $OutDir 'catalog.json'
$partial = $Rule -or $Filter -or $Only.Count -gt 0 -or $Skip.Count -gt 0
if ($partial -and (Test-Path $catalogFile) -and $tests.Count -gt 0) {
  $fresh = @{}
  foreach ($t in $tests) { $fresh[$t.fullname] = $true }
  $tests = @(@((Read-Json $catalogFile).tests | Where-Object { -not $fresh.ContainsKey($_.fullname) }) + $tests)
}
if ($tests.Count -gt 0) { Write-Json $catalogFile ([pscustomobject]@{ generatedAt = $Started.ToString('o'); tests = $tests }) }

# --- console summary (short enough for an agent to read in full) ------------------------------

Write-Host ''
Write-Host ('{0,-14} {1,-6} {2,10}' -f 'Stage', 'Result', 'Tests')
Write-Host ('{0,-14} {1,-6} {2,10}' -f '-----', '------', '-----')
foreach ($s in $stages) {
  $count = $(if ($s.total -gt 0) { "$($s.passed)/$($s.total)" } else { '-' })
  Write-Host ('{0,-14} {1,-6} {2,10}' -f $s.id, $s.result, $count)
}
Write-Host ('{0,-14} {1,-6} {2,10}' -f 'GATE', $result, "$($report.durationSec)s")

foreach ($e in $infraErrors) { Write-Host "`nERROR $e" }
foreach ($e in $compileErrors | Select-Object -First 20) { Write-Host "`nCOMPILE $($e.file):$($e.line) $($e.code) $($e.message)" }
foreach ($f in $failures) {
  Write-Host "`nFAIL $($f.stage) | $($f.test)"
  if ($f.violations.Count -gt 0) {
    foreach ($v in $f.violations) { Write-Host ("  - {0}: {1}{2}" -f $v.target, $v.detail, $(if ($v.location) { " ($($v.location))" } else { '' })) }
  } else {
    Write-Host ('  ' + (($f.message -split "`n" | Select-Object -First 6) -join "`n  "))
  }
  if ($f.fix) { Write-Host "  Fix: $($f.fix)" }
}
Write-Host "`nReport: Logs/gate/report.json"

exit $(if ($result -eq 'PASS') { 0 } else { 1 })
