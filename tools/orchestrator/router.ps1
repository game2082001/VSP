param(
    [Parameter(Mandatory = $true)]
    [int] $PrNumber,

    [string] $Repository = "game2082001/VSP",
    [string] $StatePath = ""
)

$ErrorActionPreference = "Stop"

if ($PrNumber -eq 7) {
    Write-Output "STOPPED_FOR_PRODUCT_OWNER: PR #7 is protected out-of-scope for AI01-008."
    exit 2
}

$prJson = gh pr view $PrNumber --repo $Repository --json number,state,headRefName,baseRefName,headRefOid,statusCheckRollup,reviewDecision,isDraft
$pr = $prJson | ConvertFrom-Json

if ($pr.state -ne "OPEN") {
    Write-Output "STOPPED_FOR_PRODUCT_OWNER: PR is not open."
    exit 2
}

if ($pr.isDraft) {
    Write-Output "WAITING: PR is draft."
    exit 1
}

if ($StatePath -and (Test-Path -LiteralPath $StatePath)) {
    $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    if ($state.lastKnownCommit -and $state.lastKnownCommit -ne $pr.headRefOid) {
        Write-Output "RECOVERY_REQUIRED: state commit differs from PR head."
        exit 1
    }
}

& "$PSScriptRoot\check-gates.ps1" -PrNumber $PrNumber -Repository $Repository
