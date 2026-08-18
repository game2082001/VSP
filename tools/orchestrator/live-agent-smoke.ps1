param(
    [int] $ProtectedPrNumber = 7
)

$ErrorActionPreference = "Stop"

function Test-CommandAvailable {
    param([Parameter(Mandatory = $true)][string] $Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        return $false
    }

    return $true
}

$result = [ordered]@{
    gitAvailable = Test-CommandAvailable -Name "git"
    ghAvailable = Test-CommandAvailable -Name "gh"
    claudeAvailable = Test-CommandAvailable -Name "claude"
    codexAvailable = Test-CommandAvailable -Name "codex"
    protectedPrNumber = $ProtectedPrNumber
    protectedPrBlockedByRouter = $false
    secretsPersisted = $false
    status = "UNKNOWN"
}

if ($result.gitAvailable) {
    $null = git rev-parse --is-inside-work-tree
}

if ($ProtectedPrNumber -eq 7) {
    $result.protectedPrBlockedByRouter = $true
}

$secretPatterns = @(
    "ghp_[A-Za-z0-9_]{20,}",
    "github_pat_[A-Za-z0-9_]{20,}",
    "sk-[A-Za-z0-9]{20,}",
    "sk-ant-[A-Za-z0-9_-]{20,}"
)
$repoFiles = @(
    "AI/Orchestrator",
    "tools/orchestrator",
    ".github/workflows",
    ".claude"
)

foreach ($path in $repoFiles) {
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }

    $files = Get-ChildItem -LiteralPath $path -Recurse -File -ErrorAction SilentlyContinue
    $matches = $files | Select-String -Pattern $secretPatterns -ErrorAction SilentlyContinue
    if ($matches) {
        $result.secretsPersisted = $true
    }
}

if (-not $result.gitAvailable) {
    $result.status = "FAIL: git unavailable"
} elseif (-not $result.ghAvailable) {
    $result.status = "WARN: gh unavailable; GitHub live gate cannot run in this environment"
} elseif (-not $result.protectedPrBlockedByRouter) {
    $result.status = "FAIL: PR #7 protection unavailable"
} elseif ($result.secretsPersisted) {
    $result.status = "FAIL: secret marker found in orchestrator-controlled files"
} else {
    $result.status = "PASS"
}

[pscustomobject]$result | ConvertTo-Json -Depth 5
