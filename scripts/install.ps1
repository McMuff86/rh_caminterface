# RhinoCNCExporter install script
# Usage: .\scripts\install.ps1 [-Config Release|Debug] [-YakPath path-to-yak]

param(
    [ValidateSet("Release", "Debug")]
    [string]$Config = "Release",
    [string]$YakPath
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$BuildDir = Join-Path $RepoRoot "RhinoCNCExporter\bin\$Config\net7.0-windows"
$ArtifactsDir = Join-Path $RepoRoot "artifacts\package"
$YakExe = "C:\Program Files\Rhino 8\System\yak.exe"

if (-not (Test-Path $YakExe)) {
    throw "yak.exe not found at '$YakExe'."
}

if (-not $YakPath) {
    $Candidates = @()
    if (Test-Path $ArtifactsDir) {
        $Candidates += Get-ChildItem $ArtifactsDir -Filter "*.yak" -File -ErrorAction SilentlyContinue
    }
    if (Test-Path $BuildDir) {
        $Candidates += Get-ChildItem $BuildDir -Filter "*.yak" -File -ErrorAction SilentlyContinue
    }

    $YakPath = $Candidates | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}

if (-not $YakPath -or -not (Test-Path $YakPath)) {
    throw "No .yak package found. Run .\scripts\package.ps1 first or pass -YakPath."
}

$PkgDir = Join-Path $env:APPDATA "McNeel\Rhinoceros\packages\8.0\rhinocncexporter"
if (Test-Path $PkgDir) {
    Get-ChildItem $PkgDir -Directory | ForEach-Object {
        Write-Host "[install] Removing old version: $($_.Name)" -ForegroundColor DarkYellow
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

& $YakExe install $YakPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "[install] yak install returned error, retrying after uninstall..." -ForegroundColor DarkYellow
    & $YakExe uninstall rhinocncexporter 2>$null
    & $YakExe install $YakPath
    if ($LASTEXITCODE -ne 0) {
        throw "Yak install failed"
    }
}

Write-Host "[install] OK" -ForegroundColor Green
