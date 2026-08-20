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
    protectedPrBlockedByRequestReview = $false
    protectedPrBlockedByRequestRemediation = $false
    claudeReviewAllowsImplementationBot = $false
    claudeReviewAvoidsWildcardBotAllow = $false
    secretsPersisted = $false
    status = "UNKNOWN"
}

if ($result.gitAvailable) {
    $null = git rev-parse --is-inside-work-tree
}

if ($ProtectedPrNumber -eq 7) {
    $statePath = Join-Path ([System.IO.Path]::GetTempPath()) "ai01-008-pr7-protection.state.json"
    $routerOutput = & pwsh -NoProfile -File (Join-Path $PSScriptRoot "router.ps1") -PrNumber $ProtectedPrNumber -Repository "game2082001/VSP" -StatePath $statePath 2>&1
    if ($LASTEXITCODE -eq 2 -and ($routerOutput | Out-String) -match "PR_7_OUT_OF_SCOPE") {
        $result.protectedPrBlockedByRouter = $true
    }

    try {
        & (Join-Path $PSScriptRoot "request-review.ps1") -PrNumber $ProtectedPrNumber -Repository "game2082001/VSP" 2>&1 | Out-Null
    } catch {
        if ($_.Exception.Message -match "PR #7 is protected") {
            $result.protectedPrBlockedByRequestReview = $true
        }
    }

    try {
        & (Join-Path $PSScriptRoot "request-remediation.ps1") -PrNumber $ProtectedPrNumber -Repository "game2082001/VSP" 2>&1 | Out-Null
    } catch {
        if ($_.Exception.Message -match "PR #7 is protected") {
            $result.protectedPrBlockedByRequestRemediation = $true
        }
    }
}

$secretPatterns = @(
    "ghp_[A-Za-z0-9_]{20,}",
    "github_pat_[A-Za-z0-9_]{20,}",
    "sk-[A-Za-z0-9]{20,}",
    "sk-ant-[A-Za-z0-9_-]{20,}"
)
$reviewWorkflowPaths = @(
    ".github/workflows/claude-code-review.yml",
    "AI/Orchestrator/Templates/claude-code-review.yml"
)
$reviewWorkflowContents = @()
foreach ($workflowPath in $reviewWorkflowPaths) {
    if (Test-Path -LiteralPath $workflowPath) {
        $reviewWorkflowContents += Get-Content -LiteralPath $workflowPath -Raw
    }
}
$result.claudeReviewAllowsImplementationBot = ($reviewWorkflowContents.Count -eq $reviewWorkflowPaths.Count) -and -not ($reviewWorkflowContents | Where-Object { $_ -notmatch "(?m)^\s*allowed_bots:\s*vsp-ai-implementation\s*$" })
$result.claudeReviewAvoidsWildcardBotAllow = -not ($reviewWorkflowContents | Where-Object { $_ -match "(?m)^\s*allowed_bots:\s*[""]?\*[""]?\s*$" })

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
    $result.status = "FAIL: PR #7 router protection unavailable"
} elseif (-not $result.protectedPrBlockedByRequestReview) {
    $result.status = "FAIL: PR #7 review request protection unavailable"
} elseif (-not $result.protectedPrBlockedByRequestRemediation) {
    $result.status = "FAIL: PR #7 remediation request protection unavailable"
} elseif (-not $result.claudeReviewAllowsImplementationBot) {
    $result.status = "FAIL: Claude Automated Review does not allow the trusted implementation bot"
} elseif (-not $result.claudeReviewAvoidsWildcardBotAllow) {
    $result.status = "FAIL: Claude Automated Review allows arbitrary bots"
} elseif ($result.secretsPersisted) {
    $result.status = "FAIL: secret marker found in orchestrator-controlled files"
} else {
    $result.status = "PASS"
}

[pscustomobject]$result | ConvertTo-Json -Depth 5
