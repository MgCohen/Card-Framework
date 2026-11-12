# Run it through the core:secrets wrapper, so that GITHUB_TOKEN is set for the VSA feed.
# dotnet reads global.json from the current folder, so the gate runs from Base/.
$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'FAIL  dotnet   dotnet is not on PATH.'
    exit 1
}
if (-not $env:GITHUB_TOKEN) {
    Write-Host 'FAIL  token    GITHUB_TOKEN is not set. Run the gate through the core:secrets wrapper.'
    exit 1
}

Push-Location $PSScriptRoot
try {
    # A project that is not in Base.slnx is never built or tested.
    $listed = (Get-Content 'Base.slnx' -Raw)
    $missing = Get-ChildItem -Recurse -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '[\/](bin|obj)~?[\/]' } |
        ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1).Replace('\', '/') } |
        Where-Object { $listed -notmatch [regex]::Escape($_) }
    if ($missing) {
        Write-Host ("FAIL  solution  not in Base.slnx: {0}" -f ($missing -join ', '))
        exit 1
    }
    Write-Host 'PASS  solution'

    $steps = @(
        @{ Name = 'restore'; Args = @('restore', 'Base.slnx') },
        @{ Name = 'build';   Args = @('build', 'Base.slnx', '-c', 'Release', '--no-restore', '-warnaserror') },
        @{ Name = 'test';    Args = @('test', '--solution', 'Base.slnx', '-c', 'Release', '--no-build') }
    )
    foreach ($step in $steps) {
        & dotnet @($step.Args)
        if ($LASTEXITCODE -ne 0) {
            Write-Host ("FAIL  {0}" -f $step.Name)
            exit 1
        }
        Write-Host ("PASS  {0}" -f $step.Name)
    }
}
finally {
    Pop-Location
}
exit 0
