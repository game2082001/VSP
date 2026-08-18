param(
    [Parameter(Mandatory = $true)]
    [string] $StatePath
)

$ErrorActionPreference = "Stop"

$state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json

if ($state.tokenBudget.total -le 0) {
    Write-Output "TOKEN_BUDGET_UNSET"
    exit 0
}

$percent = [math]::Round(($state.tokenSpentEstimate / $state.tokenBudget.total) * 100, 2)

if ($percent -ge $state.tokenBudget.hardStopPercent) {
    Write-Output "TOKEN_HARD_STOP"
    exit 2
}

if ($percent -ge $state.tokenBudget.softStopPercent) {
    Write-Output "TOKEN_SOFT_STOP"
    exit 1
}

Write-Output "TOKEN_OK"
