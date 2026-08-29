param(
    [Parameter(Mandatory = $true)]
    [string] $ManifestPath,

    [string] $StatePath = "",

    [switch] $CreateState,

    [switch] $Force
)

$ErrorActionPreference = "Stop"

$ExecutionAuthorizationFields = @(
    "implementation",
    "localValidation",
    "commit",
    "pushFeatureBranch",
    "openOrUpdatePr",
    "ciGate",
    "automatedReviewGate",
    "requiredIndependentReview",
    "inScopeRemediation",
    "remediationCommitPushAndGates"
)

function Resolve-RequiredPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Manifest file not found: $Path"
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string] $Path)

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    } catch {
        throw "Invalid JSON in $Path. $($_.Exception.Message)"
    }
}

function Assert-NonBlankString {
    param(
        [Parameter(Mandatory = $true)] $Value,
        [Parameter(Mandatory = $true)][string] $Name
    )

    if ($null -eq $Value -or -not ($Value -is [string]) -or [string]::IsNullOrWhiteSpace($Value)) {
        throw "Manifest validation failed: $Name is required."
    }
}

function Assert-NonBlankText {
    param(
        [Parameter(Mandatory = $true)] $Value,
        [Parameter(Mandatory = $true)][string] $Name
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        throw "Manifest validation failed: $Name is required."
    }
}

function Assert-StringArray {
    param(
        [Parameter(Mandatory = $true)] $Value,
        [Parameter(Mandatory = $true)][string] $Name
    )

    if ($null -eq $Value) {
        throw "Manifest validation failed: $Name is required."
    }

    $items = @($Value)
    if ($items.Count -eq 0) {
        throw "Manifest validation failed: $Name must contain at least one item."
    }

    foreach ($item in $items) {
        if ($null -eq $item -or -not ($item -is [string]) -or [string]::IsNullOrWhiteSpace($item)) {
            throw "Manifest validation failed: $Name contains a blank item."
        }
    }
}

function Assert-BooleanField {
    param(
        [Parameter(Mandatory = $true)] $Object,
        [Parameter(Mandatory = $true)][string] $Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or -not ($property.Value -is [bool])) {
        throw "Manifest validation failed: $Name must be a boolean."
    }
}

function Get-ArrayText {
    param([Parameter(Mandatory = $true)] $Value)

    return (@($Value) -join [Environment]::NewLine)
}

function Test-ManifestClassification {
    param([Parameter(Mandatory = $true)] $Manifest)

    $classification = $Manifest.classification
    $developerRole = $Manifest.primaryDeveloper.role
    $developerAdapter = $Manifest.primaryDeveloper.adapter
    $reviewerAdapter = $Manifest.independentReviewer.adapter
    $claudeRequired = [bool]$Manifest.claudeCrossReview.required

    if ($Manifest.independentReviewer.required -ne $true) {
        throw "Manifest validation failed: independentReviewer.required must be true."
    }

    if ($Manifest.independentReviewer.role -ne "Separate Codex Independent Reviewer") {
        throw "Manifest validation failed: independentReviewer.role must be Separate Codex Independent Reviewer."
    }

    if ($reviewerAdapter -ne "codex") {
        throw "Manifest validation failed: independentReviewer.adapter must be codex."
    }

    switch ($classification) {
        "SMALL" {
            if ($developerRole -ne "Codex Development Agent" -or $developerAdapter -ne "codex") {
                throw "Manifest validation failed: SMALL tasks require Codex Development Agent."
            }
        }
        "MEDIUM" {
            if (($developerRole -eq "Codex Development Agent" -and $developerAdapter -eq "codex") -or
                ($developerRole -eq "Claude Code Primary Developer" -and $developerAdapter -eq "claude")) {
                return
            }
            throw "Manifest validation failed: MEDIUM tasks require Codex Development Agent or Claude Code Primary Developer."
        }
        "MAJOR" {
            if ($developerRole -ne "Claude Code Primary Developer" -or $developerAdapter -ne "claude") {
                throw "Manifest validation failed: MAJOR tasks require Claude Code Primary Developer."
            }
        }
        "CRITICAL" {
            if ($developerRole -ne "Claude Code Primary Developer" -or $developerAdapter -ne "claude") {
                throw "Manifest validation failed: CRITICAL tasks require Claude Code Primary Developer."
            }
            if (-not $claudeRequired) {
                throw "Manifest validation failed: CRITICAL tasks require Claude Cross Review."
            }
        }
        default {
            throw "Manifest validation failed: classification must be SMALL, MEDIUM, MAJOR, or CRITICAL."
        }
    }
}

function Test-TaskManifest {
    param([Parameter(Mandatory = $true)] $Manifest)

    Assert-NonBlankString -Value $Manifest.schemaVersion -Name "schemaVersion"
    if ($Manifest.schemaVersion -ne "1.0") {
        throw "Manifest validation failed: unsupported schemaVersion $($Manifest.schemaVersion)."
    }

    Assert-NonBlankString -Value $Manifest.taskId -Name "taskId"
    Assert-NonBlankString -Value $Manifest.title -Name "title"
    Assert-NonBlankString -Value $Manifest.classification -Name "classification"
    Assert-NonBlankString -Value $Manifest.repository -Name "repository"
    Assert-NonBlankString -Value $Manifest.baseBranch -Name "baseBranch"
    Assert-StringArray -Value $Manifest.approvedScope -Name "approvedScope"
    Assert-StringArray -Value $Manifest.stopConditions -Name "stopConditions"

    if ($null -eq $Manifest.primaryDeveloper) {
        throw "Manifest validation failed: primaryDeveloper is required."
    }
    Assert-NonBlankString -Value $Manifest.primaryDeveloper.role -Name "primaryDeveloper.role"
    Assert-NonBlankString -Value $Manifest.primaryDeveloper.adapter -Name "primaryDeveloper.adapter"

    if ($null -eq $Manifest.independentReviewer) {
        throw "Manifest validation failed: independentReviewer is required."
    }
    Assert-BooleanField -Object $Manifest.independentReviewer -Name "required"
    Assert-NonBlankString -Value $Manifest.independentReviewer.role -Name "independentReviewer.role"
    Assert-NonBlankString -Value $Manifest.independentReviewer.adapter -Name "independentReviewer.adapter"

    if ($null -eq $Manifest.claudeCrossReview) {
        throw "Manifest validation failed: claudeCrossReview is required."
    }
    Assert-BooleanField -Object $Manifest.claudeCrossReview -Name "required"

    if ($null -eq $Manifest.productOwnerAuthorization) {
        throw "Manifest validation failed: productOwnerAuthorization is required."
    }
    Assert-BooleanField -Object $Manifest.productOwnerAuthorization -Name "authorized"
    if ($Manifest.productOwnerAuthorization.authorized -ne $true) {
        throw "Manifest validation failed: Product Owner authorization must be true."
    }
    Assert-NonBlankString -Value $Manifest.productOwnerAuthorization.authorizedBy -Name "productOwnerAuthorization.authorizedBy"
    Assert-NonBlankText -Value $Manifest.productOwnerAuthorization.authorizedAtUtc -Name "productOwnerAuthorization.authorizedAtUtc"
    Assert-NonBlankString -Value $Manifest.productOwnerAuthorization.evidenceSource -Name "productOwnerAuthorization.evidenceSource"
    Assert-NonBlankString -Value $Manifest.productOwnerAuthorization.evidenceUrl -Name "productOwnerAuthorization.evidenceUrl"
    Assert-NonBlankString -Value $Manifest.productOwnerAuthorization.approvalSummary -Name "productOwnerAuthorization.approvalSummary"

    try {
        [datetime]::Parse([string]$Manifest.productOwnerAuthorization.authorizedAtUtc).ToUniversalTime() | Out-Null
    } catch {
        throw "Manifest validation failed: productOwnerAuthorization.authorizedAtUtc must be parseable as a UTC timestamp."
    }

    if ($null -eq $Manifest.executionAuthorization) {
        throw "Manifest validation failed: executionAuthorization is required."
    }
    foreach ($field in $ExecutionAuthorizationFields) {
        Assert-BooleanField -Object $Manifest.executionAuthorization -Name $field
    }

    $implementationContextId = [string]$Manifest.primaryDeveloper.contextId
    $reviewerContextId = [string]$Manifest.independentReviewer.contextId
    if (-not [string]::IsNullOrWhiteSpace($implementationContextId) -and
        -not [string]::IsNullOrWhiteSpace($reviewerContextId) -and
        $implementationContextId -eq $reviewerContextId) {
        throw "Manifest validation failed: implementationContextId must not equal independentReviewerContextId."
    }

    Test-ManifestClassification -Manifest $Manifest
}

function New-OrchestratorStateFromManifest {
    param(
        [Parameter(Mandatory = $true)] $Manifest,
        [Parameter(Mandatory = $true)][string] $ResolvedManifestPath
    )

    $implementationContextId = [string]$Manifest.primaryDeveloper.contextId
    $reviewerContextId = [string]$Manifest.independentReviewer.contextId

    [pscustomobject]@{
        schemaVersion = "1.0"
        taskId = $Manifest.taskId
        taskManifestPath = $ResolvedManifestPath
        taskManifestStatus = "VALID"
        classification = $Manifest.classification
        classificationConsistencyStatus = "VALID"
        prNumber = 0
        repository = $Manifest.repository
        baseBranch = $Manifest.baseBranch
        headBranch = ""
        approvedScope = (Get-ArrayText -Value $Manifest.approvedScope)
        outOfScope = (Get-ArrayText -Value $Manifest.outOfScope)
        stopConditions = @($Manifest.stopConditions)
        productOwnerAuthorizationEvidence = $Manifest.productOwnerAuthorization
        executionAuthorization = $Manifest.executionAuthorization
        riskCeiling = if ($Manifest.classification -in @("MAJOR", "CRITICAL")) { "HIGH" } else { "MEDIUM" }
        currentStage = "PLANNED"
        primaryDeveloperRole = $Manifest.primaryDeveloper.role
        primaryDeveloperAdapter = $Manifest.primaryDeveloper.adapter
        assignedImplementationRole = $Manifest.primaryDeveloper.role
        implementationContextId = $implementationContextId
        implementationRunId = [string]$Manifest.primaryDeveloper.runId
        codexWorkerTouchedPr = $false
        independentReviewerRole = $Manifest.independentReviewer.role
        independentReviewerModel = "gpt-5.6-luna medium"
        independentReviewerContextId = $reviewerContextId
        developerEqualsReviewer = $false
        ciStatus = "UNKNOWN"
        claudeReviewStatus = "UNKNOWN"
        environmentAuthority = [pscustomobject]@{
            sourceAuthority = "GitHub game2082001/VSP"
            windowsCiAuthority = "VSP-Server-01 on DESKTOP-COVI6R2"
            interactiveGuiAuthority = "VSP-GUI-01 on YOUSIN"
            releaseEvidenceAuthority = "workflow-defined exact source SHA and runner evidence"
            agentSandboxAuthority = "NON_AUTHORITATIVE_DIAGNOSTIC"
        }
        sandboxDiagnostics = @()
        sandboxAnomalyDisposition = "NONE"
        claudeCrossReviewRequired = [bool]$Manifest.claudeCrossReview.required
        claudeCrossReviewRunId = [string]$Manifest.claudeCrossReview.runId
        claudeCrossReviewStatus = [string]$Manifest.claudeCrossReview.status
        independentReviewStatus = "NOT_REQUESTED"
        findings = @()
        remediationCount = 0
        remediationLimit = 2
        tokenBudget = [pscustomobject]@{
            total = 0
            implementation = 0
            review = 0
            remediation = 0
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
        lastKnownCommit = ""
        observedHeadCommit = ""
        lastWorkflowRunIds = @()
        remainingKnownRisks = @()
        scopeDrift = "NONE"
        readyForMerge = $false
        updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }
}

$resolvedManifestPath = Resolve-RequiredPath -Path $ManifestPath
$manifest = Read-JsonFile -Path $resolvedManifestPath
Test-TaskManifest -Manifest $manifest

$result = [pscustomobject]@{
    taskId = $manifest.taskId
    classification = $manifest.classification
    manifestPath = $resolvedManifestPath
    manifestStatus = "VALID"
    classificationConsistencyStatus = "VALID"
    developerEqualsReviewer = $false
    createState = [bool]$CreateState
    statePath = ""
}

if ($CreateState) {
    if ([string]::IsNullOrWhiteSpace($StatePath)) {
        throw "Manifest validation failed: StatePath is required when CreateState is set."
    }

    if ((Test-Path -LiteralPath $StatePath) -and -not $Force) {
        throw "State file already exists: $StatePath"
    }

    $state = New-OrchestratorStateFromManifest -Manifest $manifest -ResolvedManifestPath $resolvedManifestPath
    $state | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $StatePath -Encoding utf8
    $result.statePath = (Resolve-Path -LiteralPath $StatePath).Path
}

$result | ConvertTo-Json -Depth 8
