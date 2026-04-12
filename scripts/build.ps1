# RhinoCNCExporter build script
# Usage: .\scripts\build.ps1 [-Config Release|Debug] [-PluginOnly] [-SkipRestore]

param(
    [ValidateSet("Release", "Debug")]
    [string]$Config = "Release",
    [switch]$PluginOnly,
    [switch]$SkipRestore
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$SolutionPath = Join-Path $RepoRoot "RH_caminterface.sln"
$PluginProject = Join-Path $RepoRoot "RhinoCNCExporter\RhinoCNCExporter.csproj"
$Target = if ($PluginOnly) { $PluginProject } else { $SolutionPath }

Write-Host "[build] Target: $Target" -ForegroundColor Yellow
Write-Host "[build] Config: $Config" -ForegroundColor Yellow

Push-Location $RepoRoot
try {
    $arguments = @("build", $Target, "-c", $Config)
    if ($SkipRestore) { $arguments += "--no-restore" }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
}
finally {
    Pop-Location
}

Write-Host "[build] OK" -ForegroundColor Green
