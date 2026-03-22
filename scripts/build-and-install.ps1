# RhinoCNCExporter Build & Install Script
# Usage: .\scripts\build-and-install.ps1
# Optional: .\scripts\build-and-install.ps1 -SkipPull -Config Debug

param(
    [switch]$SkipPull,
    [ValidateSet("Release", "Debug")]
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$PluginDir = Join-Path $RepoRoot "RhinoCNCExporter"
$BuildDir = Join-Path $PluginDir "bin\$Config\net7.0-windows"
$YakExe = "C:\Program Files\Rhino 8\System\yak.exe"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RhinoCNCExporter Build & Install" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- 1. Git Pull ---
if (-not $SkipPull) {
    Write-Host "[1/5] Git pull..." -ForegroundColor Yellow
    Push-Location $RepoRoot
    git pull
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Git pull failed" }
    Pop-Location
    Write-Host "  OK" -ForegroundColor Green
} else {
    Write-Host "[1/5] Git pull skipped" -ForegroundColor DarkGray
}

# --- 2. Build ---
Write-Host "[2/5] Building ($Config)..." -ForegroundColor Yellow
Push-Location $RepoRoot
dotnet build -c $Config
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Build failed" }
Pop-Location
Write-Host "  OK" -ForegroundColor Green

# --- 3. Copy manifest ---
Write-Host "[3/5] Copying manifest.yml..." -ForegroundColor Yellow
Copy-Item (Join-Path $RepoRoot "manifest.yml") $BuildDir -Force
Write-Host "  OK" -ForegroundColor Green

# --- 4. Build Yak package ---
Write-Host "[4/5] Building Yak package..." -ForegroundColor Yellow
Push-Location $BuildDir

# Remove old .yak files
Remove-Item *.yak -Force -ErrorAction SilentlyContinue

& $YakExe build --platform win
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Yak build failed" }

# Find the generated .yak file
$YakFile = Get-ChildItem -Filter "*.yak" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $YakFile) { Pop-Location; throw "No .yak file found after build" }

Write-Host "  Package: $($YakFile.Name)" -ForegroundColor Green
Pop-Location

# --- 5. Clean old versions & Install ---
Write-Host "[5/5] Installing package..." -ForegroundColor Yellow

# Remove old package versions to prevent "ID already in use" conflicts
$PkgDir = Join-Path $env:APPDATA "McNeel\Rhinoceros\packages\8.0\rhinocncexporter"
if (Test-Path $PkgDir) {
    Get-ChildItem $PkgDir -Directory | ForEach-Object {
        Write-Host "  Removing old version: $($_.Name)" -ForegroundColor DarkYellow
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$YakPath = Join-Path $BuildDir $YakFile.Name
& $YakExe install $YakPath
if ($LASTEXITCODE -ne 0) { 
    Write-Host "  Yak install returned error - retrying..." -ForegroundColor DarkYellow
    & $YakExe uninstall rhinocncexporter 2>$null
    & $YakExe install $YakPath
    if ($LASTEXITCODE -ne 0) { throw "Yak install failed" }
}
Write-Host "  OK" -ForegroundColor Green

# --- Done ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Done! Restart Rhino to load plugin." -ForegroundColor Green
Write-Host "  Then type: ExportXilog" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
