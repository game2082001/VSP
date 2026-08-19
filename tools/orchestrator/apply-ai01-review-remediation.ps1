param()

$ErrorActionPreference = "Stop"

function Replace-RequiredText {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Old,
        [Parameter(Mandatory = $true)][string] $New
    )

    $content = Get-Content -LiteralPath $Path -Raw
    if (-not $content.Contains($Old)) {
        if ($content.Contains($New)) {
            return
        }

        throw "Required text not found in $Path"
    }

    $content.Replace($Old, $New) | Set-Content -LiteralPath $Path -NoNewline -Encoding utf8
}

$claudeOld = 'Do not run `git add`, `git commit`, or `git push` by default. Git operations are performed by the user (Product Owner), per [Docs/DEVELOPMENT_ROLES.md](Docs/DEVELOPMENT_ROLES.md). The Product Owner may explicitly authorize a task-scoped Commit Gate ([AI/OperatingSystem/AI_OPERATING_SYSTEM.md](AI/OperatingSystem/AI_OPERATING_SYSTEM.md) §23), permitting approved staging/commit execution for that task only. `git push` always requires its own separate, explicit authorization — a Commit Gate never implies it. No standing Git authority is granted to Claude Code by this note.'
$claudeNew = 'Do not run `git add`, `git commit`, or `git push` by default. Git operations are performed by the user (Product Owner), per [Docs/DEVELOPMENT_ROLES.md](Docs/DEVELOPMENT_ROLES.md). The Product Owner may explicitly authorize a task-scoped Commit Gate ([AI/OperatingSystem/AI_OPERATING_SYSTEM.md](AI/OperatingSystem/AI_OPERATING_SYSTEM.md) §23), permitting approved staging/commit execution for that task only. `git push` always requires its own separate, explicit authorization unless an approved orchestrated lifecycle explicitly pre-authorizes push for that task. A Commit Gate alone never implies push authority, and no standing Git authority is granted to Claude Code by this note.'
Replace-RequiredText -Path "CLAUDE.md" -Old $claudeOld -New $claudeNew

Replace-RequiredText `
    -Path "tools/orchestrator/router.ps1" `
    -Old '            & "$PSScriptRoot\request-review.ps1" -PrNumber $PrNumber -Repository $Repository' `
    -New '            & (Join-Path $PSScriptRoot "request-review.ps1") -PrNumber $PrNumber -Repository $Repository'

Replace-RequiredText `
    -Path "tools/orchestrator/dry-run.ps1" `
    -Old '    $StatePath = Join-Path $env:TEMP "ai01-008-$Scenario.state.json"' `
    -New '    $StatePath = Join-Path ([System.IO.Path]::GetTempPath()) "ai01-008-$Scenario.state.json"'

$smoke = Get-Content -LiteralPath "tools/orchestrator/live-agent-smoke.ps1" -Raw
if ($smoke -notmatch 'protectedPrBlockedByRequestReview') {
    $smoke = $smoke.Replace(
        '    protectedPrBlockedByRouter = $false
    secretsPersisted = $false',
        '    protectedPrBlockedByRouter = $false
    protectedPrBlockedByRequestReview = $false
    protectedPrBlockedByRequestRemediation = $false
    secretsPersisted = $false'
    )

    $smoke = $smoke.Replace(
        'if ($ProtectedPrNumber -eq 7) {
    $result.protectedPrBlockedByRouter = $true
}',
        'if ($ProtectedPrNumber -eq 7) {
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
}'
    )

    $smoke = $smoke.Replace(
        '} elseif (-not $result.protectedPrBlockedByRouter) {
    $result.status = "FAIL: PR #7 protection unavailable"
} elseif ($result.secretsPersisted) {',
        '} elseif (-not $result.protectedPrBlockedByRouter) {
    $result.status = "FAIL: PR #7 router protection unavailable"
} elseif (-not $result.protectedPrBlockedByRequestReview) {
    $result.status = "FAIL: PR #7 review request protection unavailable"
} elseif (-not $result.protectedPrBlockedByRequestRemediation) {
    $result.status = "FAIL: PR #7 remediation request protection unavailable"
} elseif ($result.secretsPersisted) {'
    )

    $smoke | Set-Content -LiteralPath "tools/orchestrator/live-agent-smoke.ps1" -NoNewline -Encoding utf8
}

$changelogPath = "Docs/CHANGELOG.md"
$changelog = Get-Content -LiteralPath $changelogPath -Raw
$entryPattern = '(?ms)^# CHANGELOG\r?\n## 2026-08-18 \(AI01-008 - Autonomous Multi-Agent Development Pipeline\).*?^---\r?\n'
$matches = [regex]::Matches($changelog, $entryPattern)
if ($matches.Count -lt 1) {
    throw "AI01-008 changelog entry not found."
}

$first = $matches[0].Value
$withoutAll = [regex]::Replace($changelog, $entryPattern, "")
$changelog = $first + $withoutAll
$changelog = [regex]::Replace($changelog, "(?m)^-----\r?\n\r?\n## 2026-06-28", "-----`n## 2026-06-28")
$changelog = [regex]::Replace($changelog, "(?m)^----\r?\n\r?\n## \[Unreleased\]", "----`n## [Unreleased]")
$changelog = [regex]::Replace($changelog, "(?m)^\r?\n## \[Unreleased\]", "## [Unreleased]")
$changelog | Set-Content -LiteralPath $changelogPath -NoNewline -Encoding utf8
