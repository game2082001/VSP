param(
    [Parameter(Mandatory = $true)]
    [int] $PrNumber,

    [string] $Repository = "game2082001/VSP"
)

$ErrorActionPreference = "Stop"

gh pr view $PrNumber --repo $Repository --json number,state,headRefName,baseRefName,title,isDraft,mergeable,statusCheckRollup,reviewDecision,url,headRefOid
