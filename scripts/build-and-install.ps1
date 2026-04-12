# RhinoCNCExporter Build & Install Script
# Usage: .\scripts\build-and-install.ps1
# Optional: .\scripts\build-and-install.ps1 -SkipPull -Config Debug -RunTests

param(
    [switch]$SkipPull,
    [switch]$RunTests,
    [ValidateSet("Release", "Debug")]
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$ScriptsDir = Join-Path $RepoRoot "scripts"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RhinoCNCExporter Build & Install" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $SkipPull) {
    Write-Host "[1/4] Git pull..." -ForegroundColor Yellow
    Push-Location $RepoRoot
    try {
        git pull
        if ($LASTEXITCODE -ne 0) { throw "Git pull failed" }
    }
    finally {
        Pop-Location
    }
    Write-Host "  OK" -ForegroundColor Green
} else {
    Write-Host "[1/4] Git pull skipped" -ForegroundColor DarkGray
}

Write-Host "[2/4] Build..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "build.ps1") -Config $Config
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "  OK" -ForegroundColor Green

if ($RunTests) {
    Write-Host "[3/4] Tests..." -ForegroundColor Yellow
    & (Join-Path $ScriptsDir "test.ps1") -Config $Config
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
    Write-Host "  OK" -ForegroundColor Green
} else {
    Write-Host "[3/4] Tests skipped" -ForegroundColor DarkGray
}

Write-Host "[4/4] Package + Install..." -ForegroundColor Yellow
& (Join-Path $ScriptsDir "package.ps1") -Config $Config -SkipBuild
if ($LASTEXITCODE -ne 0) { throw "Package failed" }
& (Join-Path $ScriptsDir "install.ps1") -Config $Config
if ($LASTEXITCODE -ne 0) { throw "Install failed" }
Write-Host "  OK" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Done! Restart Rhino to load plugin." -ForegroundColor Green
Write-Host "  Then type: CNCPanel or ExportXilog" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
