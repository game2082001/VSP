param(
    [Parameter(Mandatory = $true)]
    [int] $PrNumber,

    [string] $Repository = "game2082001/VSP"
)

$ErrorActionPreference = "Stop"

if ($PrNumber -eq 7) {
    throw "PR #7 is protected out-of-scope for AI01-008."
}

gh pr comment $PrNumber --repo $Repository --body "@codex Required Independent Review requested. Use read-only reviewer credentials and inspect actual PR state. Return APPROVED, REMEDIATION REQUIRED, or STOPPED FOR PRODUCT OWNER."
