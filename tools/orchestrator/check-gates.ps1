param(
    [Parameter(Mandatory = $true)]
    [int] $PrNumber,

    [string] $Repository = "game2082001/VSP"
)

$ErrorActionPreference = "Stop"

$pr = gh pr view $PrNumber --repo $Repository --json statusCheckRollup | ConvertFrom-Json
$checks = @($pr.statusCheckRollup)

$windowsCi = $checks | Where-Object { $_.name -eq "Build and Test (Windows self-hosted)" } | Select-Object -First 1
$claudeReview = $checks | Where-Object { $_.name -eq "claude-review" } | Select-Object -First 1

if (-not $windowsCi -or -not $claudeReview) {
    Write-Output "PARALLEL_GATES_MISSING"
    exit 2
}

if ($windowsCi.conclusion -eq "SUCCESS" -and $claudeReview.conclusion -eq "SUCCESS") {
    Write-Output "PARALLEL_GATES_PASS"
    exit 0
}

if ($windowsCi.status -ne "COMPLETED" -or $claudeReview.status -ne "COMPLETED") {
    Write-Output "PARALLEL_GATES_PENDING"
    exit 1
}

Write-Output "PARALLEL_GATES_FAILED"
exit 3
