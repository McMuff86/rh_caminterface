# RhinoCNCExporter test script
# Usage: .\scripts\test.ps1 [-Config Release|Debug] [-SkipRestore]

param(
    [ValidateSet("Release", "Debug")]
    [string]$Config = "Release",
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$TestsProject = Join-Path $RepoRoot "RhinoCNCExporter.Tests\RhinoCNCExporter.Tests.csproj"
$ResultsDir = Join-Path $RepoRoot "artifacts\test-results"

New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

Write-Host "[test] Project: $TestsProject" -ForegroundColor Yellow
Write-Host "[test] Config: $Config" -ForegroundColor Yellow

Push-Location $RepoRoot
try {
    $arguments = @(
        "test", $TestsProject,
        "-c", $Config,
        "--logger", "trx;LogFileName=rhino-tests.trx",
        "--results-directory", $ResultsDir
    )

    if ($SkipRestore) { $arguments += "--no-restore" }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed"
    }
}
finally {
    Pop-Location
}

Write-Host "[test] OK" -ForegroundColor Green
