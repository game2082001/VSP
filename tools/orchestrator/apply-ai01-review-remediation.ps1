param()

$ErrorActionPreference = "Stop"

function Replace-TextIfPresent {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Old,
        [Parameter(Mandatory = $true)][string] $New
    )

    $content = Get-Content -LiteralPath $Path -Raw
    if ($content.Contains($Old)) {
        $content = $content.Replace($Old, $New)
        $content | Set-Content -LiteralPath $Path -NoNewline -Encoding utf8
    }
}

function Set-ClaudeAllowedBots {
    param(
        [Parameter(Mandatory = $true)][string] $Path
    )

    $tokenLine = '          claude_code_oauth_token: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}'
    $allowedLine = '          allowed_bots: vsp-ai-implementation'
    $content = Get-Content -LiteralPath $Path -Raw

    if ($content -match '(?m)^\s*allowed_bots:\s*[''"]?\*[''"]?\s*$') {
        throw "$Path allows all bots for Claude Automated Review."
    }
    if (-not $content.Contains($tokenLine)) {
        throw "Claude OAuth token line not found in $Path"
    }

    $content = [regex]::Replace($content, '(?m)^\s*allowed_bots:\s*vsp-ai-implementation\s*\r?\n', '')
    $content = $content.Replace($tokenLine, "$tokenLine`n$allowedLine")
    $content | Set-Content -LiteralPath $Path -NoNewline -Encoding utf8
}

$claudeOld = 'Do not run `git add`, `git commit`, or `git push` by default. Git operations are performed by the user (Product Owner), per [Docs/DEVELOPMENT_ROLES.md](Docs/DEVELOPMENT_ROLES.md). The Product Owner may explicitly authorize a task-scoped Commit Gate ([AI/OperatingSystem/AI_OPERATING_SYSTEM.md](AI/OperatingSystem/AI_OPERATING_SYSTEM.md) §23), permitting approved staging/commit execution for that task only. `git push` always requires its own separate, explicit authorization — a Commit Gate never implies it. No standing Git authority is granted to Claude Code by this note.'
$claudeNew = 'Do not run `git add`, `git commit`, or `git push` by default. Git operations are performed by the user (Product Owner), per [Docs/DEVELOPMENT_ROLES.md](Docs/DEVELOPMENT_ROLES.md). The Product Owner may explicitly authorize a task-scoped Commit Gate ([AI/OperatingSystem/AI_OPERATING_SYSTEM.md](AI/OperatingSystem/AI_OPERATING_SYSTEM.md) §23), permitting approved staging/commit execution for that task only. `git push` always requires its own separate, explicit authorization unless an approved orchestrated lifecycle explicitly pre-authorizes push for that task. A Commit Gate alone never implies push authority, and no standing Git authority is granted to Claude Code by this note.'
Replace-TextIfPresent -Path "CLAUDE.md" -Old $claudeOld -New $claudeNew

Replace-TextIfPresent `
    -Path "tools/orchestrator/router.ps1" `
    -Old '            & "$PSScriptRoot\request-review.ps1" -PrNumber $PrNumber -Repository $Repository' `
    -New '            & (Join-Path $PSScriptRoot "request-review.ps1") -PrNumber $PrNumber -Repository $Repository'

Replace-TextIfPresent `
    -Path "tools/orchestrator/dry-run.ps1" `
    -Old '    $StatePath = Join-Path $env:TEMP "ai01-008-$Scenario.state.json"' `
    -New '    $StatePath = Join-Path ([System.IO.Path]::GetTempPath()) "ai01-008-$Scenario.state.json"'

foreach ($reviewWorkflowPath in @(".github/workflows/claude-code-review.yml", "AI/Orchestrator/Templates/claude-code-review.yml")) {
    Set-ClaudeAllowedBots -Path $reviewWorkflowPath
}

$changelogPath = "Docs/CHANGELOG.md"
if (Test-Path -LiteralPath $changelogPath) {
    $changelog = Get-Content -LiteralPath $changelogPath -Raw
    $entryPattern = '(?ms)^# CHANGELOG\r?\n## 2026-08-18 \(AI01-008 - Autonomous Multi-Agent Development Pipeline\).*?^---\r?\n'
    $matches = [regex]::Matches($changelog, $entryPattern)
    if ($matches.Count -gt 1) {
        $first = $matches[0].Value
        $withoutAll = [regex]::Replace($changelog, $entryPattern, "")
        ($first + $withoutAll) | Set-Content -LiteralPath $changelogPath -NoNewline -Encoding utf8
    }
}
