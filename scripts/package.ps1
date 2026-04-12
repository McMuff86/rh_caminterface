# RhinoCNCExporter packaging script
# Usage: .\scripts\package.ps1 [-Config Release|Debug] [-SkipBuild]

param(
    [ValidateSet("Release", "Debug")]
    [string]$Config = "Release",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$PluginDir = Join-Path $RepoRoot "RhinoCNCExporter"
$BuildDir = Join-Path $PluginDir "bin\$Config\net7.0-windows"
$ArtifactsDir = Join-Path $RepoRoot "artifacts\package"
$ManifestPath = Join-Path $RepoRoot "manifest.yml"
$YakExe = "C:\Program Files\Rhino 8\System\yak.exe"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build.ps1") -Config $Config -PluginOnly
    if ($LASTEXITCODE -ne 0) { throw "Build step failed before packaging" }
}

if (-not (Test-Path $YakExe)) {
    throw "yak.exe not found at '$YakExe'. Install Rhino 8 or set up the self-hosted runner correctly."
}

if (-not (Test-Path $BuildDir)) {
    throw "Build output directory not found: $BuildDir"
}

New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
Copy-Item $ManifestPath $BuildDir -Force

Push-Location $BuildDir
try {
    Remove-Item *.yak -Force -ErrorAction SilentlyContinue

    & $YakExe build --platform win
    if ($LASTEXITCODE -ne 0) {
        throw "Yak build failed"
    }

    $YakFile = Get-ChildItem -Filter "*.yak" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $YakFile) {
        throw "No .yak file found after yak build"
    }

    Copy-Item $YakFile.FullName $ArtifactsDir -Force

    $RhpPath = Join-Path $BuildDir "RhinoCNCExporter.rhp"
    if (Test-Path $RhpPath) {
        Copy-Item $RhpPath $ArtifactsDir -Force
    }

    Copy-Item $ManifestPath $ArtifactsDir -Force
    Write-Host "[package] Package: $($YakFile.Name)" -ForegroundColor Green
}
finally {
    Pop-Location
}
