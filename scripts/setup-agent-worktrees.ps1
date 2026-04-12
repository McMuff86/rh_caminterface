# RhinoCNCExporter multi-agent worktree bootstrap
# Usage: .\scripts\setup-agent-worktrees.ps1 -Ticket cam-ui -BaseBranch main

param(
    [Parameter(Mandatory = $true)]
    [string]$Ticket,
    [string]$BaseBranch = "main",
    [string]$WorkspaceRoot = ".worktrees",
    [string[]]$Roles = @("ui", "tests", "ci", "docs", "review")
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$WorktreeRoot = Join-Path $RepoRoot $WorkspaceRoot

Push-Location $RepoRoot
try {
    git fetch origin | Out-Host

    $baseRef = $BaseBranch
    git rev-parse --verify $BaseBranch *> $null
    if ($LASTEXITCODE -ne 0) {
        $baseRef = "origin/$BaseBranch"
        git rev-parse --verify $baseRef *> $null
        if ($LASTEXITCODE -ne 0) {
            throw "Base branch '$BaseBranch' not found locally or on origin."
        }
    }

    New-Item -ItemType Directory -Force -Path $WorktreeRoot | Out-Null

    foreach ($role in $Roles) {
        $branchName = "swarm/$Ticket/$role"
        $targetPath = Join-Path $WorktreeRoot $role

        if (Test-Path $targetPath) {
            throw "Worktree path already exists: $targetPath"
        }

        Write-Host "[worktree] Creating $branchName -> $targetPath" -ForegroundColor Yellow
        git worktree add -b $branchName $targetPath $baseRef | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create worktree for role '$role'"
        }
    }
}
finally {
    Pop-Location
}

Write-Host "" 
Write-Host "Created worktrees:" -ForegroundColor Green
foreach ($role in $Roles) {
    Write-Host "  $role -> $WorkspaceRoot/$role" -ForegroundColor Green
}
Write-Host "" 
Write-Host "Suggested harness labels:" -ForegroundColor Cyan
foreach ($role in $Roles) {
    Write-Host "  codex-rhino-$role" -ForegroundColor Cyan
}
