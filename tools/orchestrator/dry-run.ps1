param(
    [ValidateSet("pass", "remediation", "gate-pending", "remediation-limit", "budget-exceeded", "repeat-finding", "stale-head", "recursive-trigger", "agent-failure", "scope-expansion", "security-decision", "architecture-decision", "restart")]
    [string] $Scenario = "pass",

    [string] $StatePath = "",

    [switch] $Initialize
)

$ErrorActionPreference = "Stop"

function New-State {
    param([string] $ScenarioName)

    [pscustomobject]@{
        schemaVersion = "1.0"
        taskId = "AI01-008-E2E"
        prNumber = 8
        repository = "game2082001/VSP"
        baseBranch = "main"
        headBranch = "ai01-008-e2e-validation"
        approvedScope = "AI01-008 deterministic orchestrator validation only"
        executionAuthorization = [pscustomobject]@{
            implementation = $true
            localValidation = $true
            commit = $true
            pushFeatureBranch = $true
            openOrUpdatePr = $true
            ciGate = $true
            automatedReviewGate = $true
            requiredIndependentReview = $true
            inScopeRemediation = $true
            remediationCommitPushAndGates = $true
        }
        riskCeiling = "LOW"
        currentStage = "PLANNED"
        assignedImplementationRole = ""
        implementationContextId = ""
        codexWorkerTouchedPr = $false
        independentReviewerRole = "Codex Independent Reviewer"
        independentReviewerModel = "gpt-5.6-luna medium"
        independentReviewerContextId = ""
        ciStatus = "UNKNOWN"
        claudeReviewStatus = "UNKNOWN"
        independentReviewStatus = "NOT_REQUESTED"
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
        stopCondition = ""
        productOwnerDecision = [pscustomobject]@{
            required = $false
            reason = ""
            recommended = ""
            why = ""
            ifApproved = ""
            alternatives = @()
        }
        lastKnownCommit = "HEAD-001"
        observedHeadCommit = "HEAD-001"
        lastWorkflowRunIds = @("ci-001", "claude-review-001")
        lastFindingFingerprint = ""
        currentFindingFingerprint = ""
        remainingKnownRisks = @()
        liveAgentSmoke = "NOT_REQUIRED_FOR_DETERMINISTIC_DRY_RUN"
        readyForMerge = $false
        updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
        e2eScenario = $ScenarioName
        e2eEvents = @()
    }
}

function Add-Event {
    param(
        [Parameter(Mandatory = $true)] $State,
        [Parameter(Mandatory = $true)][string] $Event
    )

    $events = @($State.e2eEvents)
    $State.e2eEvents = @($events + $Event)
}

function Set-ProductOwnerDecision {
    param(
        [Parameter(Mandatory = $true)] $State,
        [Parameter(Mandatory = $true)][string] $Reason,
        [Parameter(Mandatory = $true)][string] $Recommended,
        [Parameter(Mandatory = $true)][string] $Why,
        [Parameter(Mandatory = $true)][string] $IfApproved,
        [string[]] $Alternatives = @()
    )

    $finalAlternatives = @($Recommended)
    foreach ($alternative in $Alternatives) {
        if ($alternative -and $finalAlternatives -notcontains $alternative) {
            $finalAlternatives += $alternative
        }
    }
    while ($finalAlternatives.Count -lt 3) {
        $finalAlternatives += "Request revised Task Plan"
    }
    $finalAlternatives = @($finalAlternatives[0..2] + "Stop / Defer")

    $State.productOwnerDecision = [pscustomobject]@{
        required = $true
        reason = $Reason
        recommended = $Recommended
        why = $Why
        ifApproved = $IfApproved
        alternatives = $finalAlternatives
    }
}

function Stop-ForProductOwner {
    param(
        [Parameter(Mandatory = $true)] $State,
        [Parameter(Mandatory = $true)][string] $Reason,
        [string] $Recommended = "Approve recommended recovery path",
        [string] $Why = "This preserves governance while allowing the Orchestrator to continue only after explicit Product Owner direction.",
        [string] $IfApproved = "The Orchestrator resumes from structured state and continues to the next approved gate.",
        [string[]] $Alternatives = @("Revise scope", "Request manual investigation")
    )

    $State.currentStage = "STOPPED_FOR_PRODUCT_OWNER"
    $State.stopCondition = $Reason
    $State.readyForMerge = $false
    Set-ProductOwnerDecision -State $State -Reason $Reason -Recommended $Recommended -Why $Why -IfApproved $IfApproved -Alternatives $Alternatives
    Add-Event -State $State -Event "STOP:$Reason"
}

function Set-ReadyForMergeDecision {
    param([Parameter(Mandatory = $true)] $State)

    $reason = "All authorized gates passed. Autonomous merge is forbidden in AI01-008 V1.0."
    $recommended = "Merge PR $($State.prNumber)"
    $risks = @($State.remainingKnownRisks) -join "; "
    if (-not $risks) {
        $risks = "none recorded"
    }
    $why = "$($State.observedHeadCommit) has CI=$($State.ciStatus), Automated Review=$($State.claudeReviewStatus), Independent Review=$($State.independentReviewStatus), remediation iterations=$($State.remediationCount), remaining risks=$risks."
    $ifApproved = "Product Owner manually merges PR $($State.prNumber). Orchestrator does not merge."
    Set-ProductOwnerDecision -State $State -Reason $reason -Recommended $recommended -Why $why -IfApproved $ifApproved -Alternatives @("Defer merge", "Request another independent review")
}

function Test-PreAuthorization {
    param(
        [Parameter(Mandatory = $true)] $State,
        [Parameter(Mandatory = $true)][string] $Capability
    )

    return [bool]$State.executionAuthorization.$Capability
}

function Invoke-OrchestratorStep {
    param(
        [Parameter(Mandatory = $true)] $State,
        [Parameter(Mandatory = $true)][string] $ScenarioName
    )

    if ($State.prNumber -eq 7) {
        Stop-ForProductOwner -State $State -Reason "PR_7_OUT_OF_SCOPE" -Recommended "Keep PR #7 excluded from AI01-008" -Why "PR #7 is explicitly protected and unrelated to this bootstrap validation." -IfApproved "The Orchestrator continues only on an authorized non-PR-7 task."
        return
    }

    if ($State.lastKnownCommit -ne $State.observedHeadCommit) {
        Stop-ForProductOwner -State $State -Reason "STALE_HEAD_EVIDENCE" -Recommended "Refresh current-head evidence before continuing" -Why "Review and gate evidence must match the actual PR HEAD." -IfApproved "The Orchestrator re-reads Git/GitHub state, updates structured state, and resumes at the correct gate."
        return
    }

    if ($ScenarioName -eq "recursive-trigger") {
        Stop-ForProductOwner -State $State -Reason "RECURSIVE_WORKFLOW_TRIGGER" -Recommended "Stop recursive workflow and require manual inspection" -Why "Recursive triggers can create uncontrolled automation loops." -IfApproved "The Orchestrator remains stopped until workflow trigger rules are corrected."
        return
    }

    if ($ScenarioName -eq "scope-expansion") {
        Stop-ForProductOwner -State $State -Reason "SCOPE_EXPANSION" -Recommended "Approve a revised Task Plan before continuing" -Why "The requested work exceeds the approved scope." -IfApproved "The Orchestrator records the revised authorization and restarts from Task Plan Gate."
        return
    }

    if ($ScenarioName -eq "security-decision") {
        Stop-ForProductOwner -State $State -Reason "SECURITY_OR_CREDENTIAL_DECISION" -Recommended "Escalate to Product Owner security decision" -Why "Credential and security decisions cannot be inferred by agents." -IfApproved "The Orchestrator applies only the approved security decision and resumes at validation."
        return
    }

    if ($ScenarioName -eq "architecture-decision") {
        Stop-ForProductOwner -State $State -Reason "ARCHITECTURE_DECISION_OUTSIDE_APPROVED_SCOPE" -Recommended "Request architecture approval before implementation" -Why "The finding changes architecture beyond the approved Task Plan." -IfApproved "The Orchestrator records the architecture decision and routes implementation within the new scope."
        return
    }

    if ($State.tokenBudget.total -gt 0 -and $State.tokenSpentEstimate -ge $State.tokenBudget.total) {
        Stop-ForProductOwner -State $State -Reason "TOKEN_BUDGET_EXCEEDED" -Recommended "Stop automation and review the remaining work manually" -Why "The configured token budget prevents an unbounded remediation loop." -IfApproved "The Orchestrator waits for a revised budget or revised Task Plan."
        return
    }

    if ($State.remediationCount -gt 0 -and
        $State.lastFindingFingerprint -and
        $State.lastFindingFingerprint -eq $State.currentFindingFingerprint) {
        Stop-ForProductOwner -State $State -Reason "REPEATED_IDENTICAL_FINDING" -Recommended "Stop and request Product Owner triage" -Why "The same finding repeated after remediation, so continuing automatically is unlikely to converge." -IfApproved "The Orchestrator routes a revised remediation only after Product Owner direction."
        return
    }

    switch ($State.currentStage) {
        "PLANNED" {
            if (-not (Test-PreAuthorization -State $State -Capability "implementation")) {
                Stop-ForProductOwner -State $State -Reason "IMPLEMENTATION_NOT_AUTHORIZED" -Recommended "Authorize implementation in the Task Plan" -Why "The Router cannot start implementation without Task Plan authorization." -IfApproved "The Orchestrator starts implementation and continues through authorized gates."
                return
            }
            $State.currentStage = "IMPLEMENTING"
            $State.assignedImplementationRole = "Codex Worker"
            $State.implementationContextId = "codex-worker-e2e-context"
            $State.codexWorkerTouchedPr = $true
            $State.tokenSpentEstimate += 100
            Add-Event -State $State -Event "ROUTED:Codex Worker"
        }
        "IMPLEMENTING" {
            foreach ($capability in @("localValidation", "commit", "pushFeatureBranch", "openOrUpdatePr", "ciGate", "automatedReviewGate")) {
                if (-not (Test-PreAuthorization -State $State -Capability $capability)) {
                    Stop-ForProductOwner -State $State -Reason "$($capability.ToUpperInvariant())_NOT_AUTHORIZED" -Recommended "Authorize $capability in the Task Plan" -Why "Pre-authorized execution must be explicit before the Router proceeds." -IfApproved "The Orchestrator continues the normal lifecycle to the next approved gate."
                    return
                }
            }
            $State.currentStage = "WAITING_PARALLEL_GATES"
            $State.ciStatus = "PENDING"
            $State.claudeReviewStatus = "PENDING"
            $State.tokenSpentEstimate += 50
            Add-Event -State $State -Event "IMPLEMENTATION_REQUEST_GENERATED"
            Add-Event -State $State -Event "LOCAL_VALIDATION_PASS"
            Add-Event -State $State -Event "COMMIT_CREATED"
            Add-Event -State $State -Event "FEATURE_BRANCH_PUSHED"
            Add-Event -State $State -Event "PR_OPENED_OR_UPDATED"
        }
        "WAITING_PARALLEL_GATES" {
            if ($ScenarioName -eq "agent-failure") {
                Stop-ForProductOwner -State $State -Reason "AGENT_FAILURE_OR_TIMEOUT" -Recommended "Stop automation and inspect agent authentication/environment" -Why "The Router cannot prove the agent completed its assigned work." -IfApproved "The Orchestrator retries only after the agent failure is resolved."
                return
            }

            if ($ScenarioName -eq "gate-pending") {
                $State.ciStatus = "PENDING"
                $State.claudeReviewStatus = "IN_PROGRESS"
                Add-Event -State $State -Event "GATES_PENDING:CONTINUE_POLLING"
                return
            }

            $State.ciStatus = "PASS"
            $State.claudeReviewStatus = "PASS"
            $State.currentStage = "WAITING_INDEPENDENT_REVIEW"
            $State.independentReviewerContextId = "codex-reviewer-clean-readonly-context"
            Add-Event -State $State -Event "PARALLEL_GATES_PASS"
            Add-Event -State $State -Event "REVIEW_REQUEST_GENERATED"
        }
        "WAITING_INDEPENDENT_REVIEW" {
            if (-not (Test-PreAuthorization -State $State -Capability "requiredIndependentReview")) {
                Stop-ForProductOwner -State $State -Reason "INDEPENDENT_REVIEW_NOT_AUTHORIZED" -Recommended "Authorize Required Independent Review" -Why "The Router cannot request Required Independent Review without Task Plan authorization." -IfApproved "The Orchestrator requests clean-context read-only review."
                return
            }

            if ($State.codexWorkerTouchedPr -and $State.independentReviewerContextId -eq $State.implementationContextId) {
                Stop-ForProductOwner -State $State -Reason "ROLE_SEPARATION_VIOLATION" -Recommended "Create a clean read-only reviewer context" -Why "Codex Worker cannot independently review a PR it modified using the same role/context." -IfApproved "The Orchestrator requests a new read-only Codex Independent Reviewer and rechecks evidence."
                return
            }

            $State.tokenSpentEstimate += 150

            if ($ScenarioName -eq "remediation") {
                $State.independentReviewStatus = "REMEDIATION_REQUIRED"
                $State.currentStage = "REMEDIATION_REQUIRED"
                $State.currentFindingFingerprint = "missing-e2e-evidence"
                Add-Event -State $State -Event "REMEDIATION_REQUIRED"
            } else {
                $State.independentReviewStatus = "APPROVED"
                $State.currentStage = "READY_FOR_MERGE"
                $State.readyForMerge = $true
                Add-Event -State $State -Event "READY_FOR_MERGE"
                Set-ReadyForMergeDecision -State $State
            }
        }
        "REMEDIATION_REQUIRED" {
            if (-not (Test-PreAuthorization -State $State -Capability "inScopeRemediation")) {
                Stop-ForProductOwner -State $State -Reason "REMEDIATION_NOT_AUTHORIZED" -Recommended "Authorize in-scope remediation loop" -Why "The Router cannot remediate automatically without Task Plan authorization." -IfApproved "The Orchestrator routes remediation and re-runs gates."
                return
            }

            if ($State.remediationCount -ge $State.remediationLimit) {
                Stop-ForProductOwner -State $State -Reason "REMEDIATION_LIMIT_EXCEEDED" -Recommended "Stop and review remediation strategy" -Why "The configured remediation loop limit has been reached." -IfApproved "The Orchestrator resumes only with a revised Task Plan or budget."
                return
            }

            $State.remediationCount += 1
            $State.lastFindingFingerprint = $State.currentFindingFingerprint
            $State.currentFindingFingerprint = ""
            $State.assignedImplementationRole = "Claude Code"
            $State.implementationContextId = "claude-code-remediation-context"
            $State.currentStage = "REMEDIATING"
            $State.tokenSpentEstimate += 200
            Add-Event -State $State -Event "REMEDIATION_REQUEST_GENERATED:Claude Code"
        }
        "REMEDIATING" {
            if (-not (Test-PreAuthorization -State $State -Capability "remediationCommitPushAndGates")) {
                Stop-ForProductOwner -State $State -Reason "REMEDIATION_COMMIT_PUSH_GATES_NOT_AUTHORIZED" -Recommended "Authorize remediation commit/push/gates" -Why "Remediation follow-up gates must be pre-authorized." -IfApproved "The Orchestrator commits, pushes, re-runs gates, and requests re-review."
                return
            }

            $State.lastKnownCommit = "HEAD-002"
            $State.observedHeadCommit = "HEAD-002"
            $State.ciStatus = "PASS"
            $State.claudeReviewStatus = "PASS"
            $State.independentReviewStatus = "APPROVED"
            $State.currentStage = "READY_FOR_MERGE"
            $State.readyForMerge = $true
            Add-Event -State $State -Event "REMEDIATION_COMMIT_VALIDATED"
            Add-Event -State $State -Event "READY_FOR_MERGE"
            Set-ReadyForMergeDecision -State $State
        }
        default {
            Add-Event -State $State -Event "NOOP:$($State.currentStage)"
        }
    }
}

if (-not $StatePath) {
    $StatePath = Join-Path ([System.IO.Path]::GetTempPath()) "ai01-008-$Scenario.state.json"
}

if ($Initialize -or -not (Test-Path -LiteralPath $StatePath)) {
    $state = New-State -ScenarioName $Scenario
} else {
    $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
}

switch ($Scenario) {
    "budget-exceeded" { $state.tokenSpentEstimate = $state.tokenBudget.total }
    "repeat-finding" {
        $state.currentStage = "WAITING_INDEPENDENT_REVIEW"
        $state.ciStatus = "PASS"
        $state.claudeReviewStatus = "PASS"
        $state.remediationCount = 1
        $state.lastFindingFingerprint = "same-finding"
        $state.currentFindingFingerprint = "same-finding"
    }
    "remediation-limit" {
        $state.currentStage = "REMEDIATION_REQUIRED"
        $state.remediationCount = $state.remediationLimit
        $state.currentFindingFingerprint = "limit-check"
    }
    "stale-head" { $state.observedHeadCommit = "HEAD-STALE" }
}

for ($i = 0; $i -lt 8; $i++) {
    if ($state.currentStage -in @("READY_FOR_MERGE", "STOPPED_FOR_PRODUCT_OWNER", "FAILED_UNRECOVERABLE")) {
        break
    }

    Invoke-OrchestratorStep -State $state -ScenarioName $Scenario
}

$state.updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
$state | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $StatePath -Encoding utf8
$state | ConvertTo-Json -Depth 12
