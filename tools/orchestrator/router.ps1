param(
    [Parameter(Mandatory = $true)]
    [int] $PrNumber,

    [string] $Repository = "game2082001/VSP",
    [string] $StatePath = "",
    [string] $ExpectedHeadSha = "",
    [int] $PollSeconds = 30,
    [int] $TimeoutMinutes = 30,
    [switch] $RequestIndependentReview
)

$ErrorActionPreference = "Stop"

function New-Decision {
    param(
        [string] $Reason,
        [string] $Recommended,
        [string] $Why,
        [string] $IfApproved
    )

    [pscustomobject]@{
        required = $true
        reason = $Reason
        recommended = $Recommended
        why = $Why
        ifApproved = $IfApproved
        alternatives = @($Recommended, "Refresh evidence and retry", "Revise Task Plan", "Stop / Defer")
    }
}

function Stop-ForProductOwner {
    param(
        [string] $Reason,
        [string] $Recommended,
        [string] $Why,
        [string] $IfApproved,
        [int] $ExitCode = 2
    )

    $decision = New-Decision -Reason $Reason -Recommended $Recommended -Why $Why -IfApproved $IfApproved
    $decision | ConvertTo-Json -Depth 6
    exit $ExitCode
}

function Write-StateIfRequested {
    param(
        $State,
        [string] $Path
    )

    if (-not $Path) {
        return
    }

    $State.updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    $State | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Path -Encoding utf8
}

function New-OrchestratorState {
    param(
        [int] $Number,
        [string] $Repo,
        [string] $HeadSha
    )

    [pscustomobject]@{
        schemaVersion = "1.0"
        taskId = "AI01-008"
        prNumber = $Number
        repository = $Repo
        currentStage = "WAITING_PARALLEL_GATES"
        implementationContextId = ""
        codexWorkerTouchedPr = $false
        independentReviewerContextId = ""
        remediationCount = 0
        remediationLimit = 2
        tokenBudget = [pscustomobject]@{
            total = 1000
            implementation = 500
            review = 300
            remediation = 200
            softStopPercent = 80
            hardStopPercent = 100
        }
        tokenSpentEstimate = 0
        lastKnownCommit = $HeadSha
        updatedAtUtc = ""
    }
}

if ($RequestIndependentReview -and -not $StatePath) {
    Stop-ForProductOwner -Reason "STRUCTURED_STATE_UNAVAILABLE" -Recommended "Provide a structured state path and rerun the Orchestrator" -Why "Required Independent Review routing must enforce persisted state, token budget, and role separation." -IfApproved "The Orchestrator recreates state from GitHub/Git evidence and resumes gate evaluation."
}

if ($PrNumber -eq 7) {
    Stop-ForProductOwner -Reason "PR_7_OUT_OF_SCOPE" -Recommended "Keep PR #7 excluded from AI01-008" -Why "PR #7 is explicitly protected and unrelated to AI01-008 bootstrap." -IfApproved "The Orchestrator continues only on an authorized non-PR-7 task."
}

$prJson = gh pr view $PrNumber --repo $Repository --json number,state,headRefName,baseRefName,headRefOid,statusCheckRollup,reviewDecision,isDraft
$pr = $prJson | ConvertFrom-Json

if ($pr.state -ne "OPEN") {
    Stop-ForProductOwner -Reason "PR_NOT_OPEN" -Recommended "Stop routing this PR" -Why "Only open PRs can enter the AI01-008 gate lifecycle." -IfApproved "The Orchestrator waits for a new open PR or revised Task Plan."
}

if ($pr.isDraft) {
    Write-Output "WAITING: PR is draft."
    exit 1
}

$state = $null
if ($StatePath -and -not (Test-Path -LiteralPath $StatePath)) {
    $directory = Split-Path -Parent $StatePath
    if ($directory -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $state = New-OrchestratorState -Number $PrNumber -Repo $Repository -HeadSha $pr.headRefOid
    Write-StateIfRequested -State $state -Path $StatePath
}

if ($StatePath -and (Test-Path -LiteralPath $StatePath)) {
    $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    if ($state.lastKnownCommit -and $state.lastKnownCommit -ne $pr.headRefOid) {
        Stop-ForProductOwner -Reason "STALE_HEAD_EVIDENCE" -Recommended "Refresh current-head evidence before continuing" -Why "Review and gate evidence must bind to the actual PR HEAD." -IfApproved "The Orchestrator re-reads GitHub PR state and resumes from the matching gate." -ExitCode 3
    }

    if ($state.tokenBudget.total -gt 0 -and $state.tokenSpentEstimate -ge $state.tokenBudget.total) {
        Stop-ForProductOwner -Reason "TOKEN_BUDGET_EXCEEDED" -Recommended "Stop automation and review the remaining work manually" -Why "The configured token budget prevents unbounded remediation or polling loops." -IfApproved "The Orchestrator waits for a revised budget or revised Task Plan."
    }

    if ($state.codexWorkerTouchedPr -and $state.independentReviewerContextId -and $state.independentReviewerContextId -eq $state.implementationContextId) {
        Stop-ForProductOwner -Reason "ROLE_SEPARATION_VIOLATION" -Recommended "Create a clean read-only reviewer context" -Why "Codex Worker cannot be the Required Independent Reviewer for a PR it modified using the same context." -IfApproved "The Orchestrator requests a new read-only reviewer and rechecks current-head evidence."
    }
}

if ($ExpectedHeadSha -and $ExpectedHeadSha -ne $pr.headRefOid) {
    Stop-ForProductOwner -Reason "STALE_HEAD_EVIDENCE" -Recommended "Refresh current-head evidence before continuing" -Why "Workflow HEAD input no longer matches the actual PR HEAD." -IfApproved "The Orchestrator restarts from current PR HEAD $($pr.headRefOid)." -ExitCode 3
}

$deadline = (Get-Date).AddMinutes($TimeoutMinutes)

while ($true) {
    $prJson = gh pr view $PrNumber --repo $Repository --json number,headRefOid,statusCheckRollup,reviewDecision
    $pr = $prJson | ConvertFrom-Json

    if ($ExpectedHeadSha -and $ExpectedHeadSha -ne $pr.headRefOid) {
        Stop-ForProductOwner -Reason "STALE_HEAD_EVIDENCE" -Recommended "Refresh current-head evidence before continuing" -Why "PR HEAD changed while gates were being evaluated." -IfApproved "The Orchestrator binds evidence to the new HEAD and resumes."
    }

    $checks = @($pr.statusCheckRollup)
    $windowsCi = $checks | Where-Object { $_.name -eq "Build and Test (Windows self-hosted)" } | Select-Object -First 1
    $claudeReview = $checks | Where-Object { $_.name -eq "claude-review" } | Select-Object -First 1

    if (-not $windowsCi -or -not $claudeReview) {
        Stop-ForProductOwner -Reason "GATE_EVIDENCE_UNAVAILABLE" -Recommended "Refresh workflow evidence and retry" -Why "Required CI or Automated Review check evidence is missing." -IfApproved "The Orchestrator re-reads GitHub checks and resumes when evidence exists." -ExitCode 3
    }

    if ($windowsCi.conclusion -eq "SUCCESS" -and $claudeReview.conclusion -eq "SUCCESS") {
        $nextState = [pscustomobject]@{
            currentStage = if ($RequestIndependentReview) { "WAITING_INDEPENDENT_REVIEW" } else { "PARALLEL_GATES_PASS" }
            prNumber = $PrNumber
            repository = $Repository
            lastKnownCommit = $pr.headRefOid
            ciStatus = "PASS"
            claudeReviewStatus = "PASS"
            independentReviewStatus = "NOT_REQUESTED"
            remediationCount = if ($state -and $null -ne $state.remediationCount) { $state.remediationCount } else { 0 }
            remediationLimit = if ($state -and $null -ne $state.remediationLimit) { $state.remediationLimit } else { 2 }
            tokenBudget = if ($state -and $state.tokenBudget) { $state.tokenBudget } else { $null }
            tokenSpentEstimate = if ($state -and $null -ne $state.tokenSpentEstimate) { $state.tokenSpentEstimate } else { 0 }
            updatedAtUtc = ""
        }
        Write-StateIfRequested -State $nextState -Path $StatePath

        if ($RequestIndependentReview) {
            & (Join-Path $PSScriptRoot "request-review.ps1") -PrNumber $PrNumber -Repository $Repository
            Write-Output "WAITING_INDEPENDENT_REVIEW"
        } else {
            Write-Output "PARALLEL_GATES_PASS"
        }
        exit 0
    }

    if (($windowsCi.status -eq "COMPLETED" -and $windowsCi.conclusion -ne "SUCCESS") -or
        ($claudeReview.status -eq "COMPLETED" -and $claudeReview.conclusion -ne "SUCCESS")) {
        Write-Output "PARALLEL_GATES_FAILED"
        exit 4
    }

    if ((Get-Date) -ge $deadline) {
        Stop-ForProductOwner -Reason "GATE_TIMEOUT_EXCEEDED" -Recommended "Stop automation and inspect the pending gate" -Why "A queued, pending, or in-progress gate exceeded the configured tolerance." -IfApproved "The Orchestrator retries only after the gate timeout is explained or the tolerance is revised." -ExitCode 3
    }

    Write-Output "PARALLEL_GATES_PENDING: polling continues without Product Owner decision"
    Start-Sleep -Seconds $PollSeconds
}
