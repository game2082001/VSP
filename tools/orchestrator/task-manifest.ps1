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
            $approvedCodexBootstrapTasks = @("VSP-AI02-001T", "VSP-AI02-001TI-B1", "VSP-AI02-001TI-A1-DS1", "VSP-AI02-001TI-A1D-POLICY", "VSP-AI02-001TI-A1D-GEN", "VSP-AI02-001TI-A1D-VALIDATEF", "VSP-AI02-001TI-A-P2-AGV2", "VSP-AI02-001TI-A-P2-PM1")
            if ($Manifest.taskId -in $approvedCodexBootstrapTasks -and
                $Manifest.bootstrapException.authorized -eq $true -and
                $Manifest.bootstrapException.taskId -eq $Manifest.taskId -and
                $Manifest.normalRequiredPrimaryDeveloper -eq "Claude Code Primary Developer" -and
                $developerRole -eq "Codex Development Agent" -and
                $developerAdapter -eq "codex" -and
                $claudeRequired) {
                return
            }
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

function Test-A2ExecutableAuthorization {
    param([Parameter(Mandatory = $true)] $Manifest)
    if ($Manifest.taskId -ne "VSP-AI02-001TI-A2") { return }

    $ownedPath = "tools/orchestrator/artifact-intake-contract.ps1"
    if ($Manifest.authorizedSourceSha -ne "b222ef7cb391f8f3eb7f4cd98b45dda2a01e9fe3" -or
        @($Manifest.approvedFiles).Count -ne 1 -or $Manifest.approvedFiles[0] -ne $ownedPath -or
        @($Manifest.executableA2.productionOwnership).Count -ne 1 -or $Manifest.executableA2.productionOwnership[0] -ne $ownedPath) {
        throw "Manifest validation failed: executable A2 identity, source, or production ownership mismatch."
    }
    if ($Manifest.executableA2.primaryDeveloper -ne "Claude Code Primary Developer" -or
        $Manifest.executableA2.executionPlane -ne "AI02 Claude Artifact Developer" -or
        $Manifest.executionAuthorization.implementation -ne $true -or
        $Manifest.executionAuthorization.pushFeatureBranch -ne $false -or
        $Manifest.executionAuthorization.openOrUpdatePr -ne $false) {
        throw "Manifest validation failed: executable A2 developer or authority boundary mismatch."
    }
    if ($Manifest.repositoryTransport.required -ne $true -or
        $Manifest.repositoryTransport.baseBinding -ne "EXACT" -or
        $Manifest.repositoryTransport.openPullRequest -ne $true -or
        $Manifest.repositoryTransport.targetBranch -ne "ai02/vsp-ai02-001ti-a2/trusted-intake-contract-validator" -or
        $Manifest.repositoryTransport.targetBranch -eq "main" -or
        @($Manifest.repositoryTransport.approvedFiles).Count -ne 1 -or
        $Manifest.repositoryTransport.approvedFiles[0] -ne $ownedPath -or
        $Manifest.repositoryTransport.invokedDuringA2Execution -ne $false -or
        $Manifest.repositoryTransport.separateProductOwnerAuthorizationRequired -ne $true) {
        throw "Manifest validation failed: executable A2 Repository Transport contract mismatch."
    }
    if ($Manifest.phasedChild.enabled -ne $true -or $Manifest.phasedChild.phase -ne "A2" -or
        [int]$Manifest.phasedChild.sequence -ne 2 -or [int]$Manifest.phasedChild.predecessorCount -ne 1 -or
        $Manifest.phasedChild.predecessorSourceType -ne "AUTHORITATIVE_REPOSITORY_MERGE" -or
        $Manifest.phasedChild.checkpointSchemaVersion -ne "2.0" -or
        $Manifest.phasedChild.parentAggregateStateDigest -ne "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1") {
        throw "Manifest validation failed: executable A2 phased-child contract mismatch."
    }
    if ([int]$Manifest.attemptBudget.initialAttempts -ne 1 -or
        [int]$Manifest.attemptBudget.maximumCauseSpecificRemediations -ne 1 -or
        [int]$Manifest.attemptBudget.maximumTotalAttempts -ne 2 -or
        [int]$Manifest.attemptBudget.attemptsConsumed -ne 0 -or
        $Manifest.attemptBudget.automaticSecondAttempt -ne $false -or
        $Manifest.attemptBudget.thirdAttemptAuthorized -ne $false) {
        throw "Manifest validation failed: executable A2 attempt budget mismatch."
    }
    if ($Manifest.executableA2.repositoryWriteAuthority -ne $false -or
        $Manifest.executableA2.networkAuthority -ne $false -or
        $Manifest.executableA2.artifactReadCredentialAvailableToClaude -ne $false -or
        $Manifest.executableA2.branchOrPullRequestAuthority -ne $false -or
        $Manifest.executableA2.repositoryTransportAuthority -ne $false -or
        $Manifest.executableA2.directMergeAuthority -ne $false -or
        $Manifest.executableA2.persistedCheckoutCredentials -ne $false -or
        $Manifest.executableA2.productOwnerSoleMergeAuthority -ne $true -or
        $Manifest.executableA2.localAiAuthority -ne $false) {
        throw "Manifest validation failed: executable A2 credential or side-effect boundary mismatch."
    }

    $bindings = @($Manifest.predecessorBindings)
    if ($bindings.Count -ne 1) { throw "Manifest validation failed: executable A2 requires exactly one predecessor binding." }
    $binding = $bindings[0]
    if ($binding.sourceType -ne "AUTHORITATIVE_REPOSITORY_MERGE" -or $binding.repository -ne "game2082001/VSP" -or
        $binding.mergeCommit -ne "aa53c00d5e4125a53f8f835220bf6d6b2e911b14" -or
        @($binding.orderedMergeParents).Count -ne 2 -or
        $binding.orderedMergeParents[0] -ne "8a0a441c532295405b8133d96142844026ee4a93" -or
        $binding.orderedMergeParents[1] -ne "cb7dbf0cfbcb0f715001588cdd6da97264e19d06" -or
        $binding.publishedProductionHead -ne "cb7dbf0cfbcb0f715001588cdd6da97264e19d06" -or
        $binding.predecessorTaskId -ne "VSP-AI02-001TI-A1D-VALIDATE" -or $binding.phase -ne "A1" -or [int]$binding.sequence -ne 1 -or
        $binding.executionRepositorySha -ne "8a0a441c532295405b8133d96142844026ee4a93" -or $binding.checkpointSchemaVersion -ne "1.0") {
        throw "Manifest validation failed: executable A2 predecessor repository identity mismatch."
    }
    if ($binding.aggregateStateDigest -ne "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1") {
        throw "Manifest validation failed: executable A2 predecessor aggregate digest mismatch."
    }
    $expectedHashes = @{
        descriptorSha256="c1595130a63ffb3eaba0facbb4c0f1e73c5dbf17db098ea5052ca23ff021a253"
        aggregateFileSha256="750334cd06ecf529303b14ba0431f5ca492c714e606baca6767aef5961106ddc"
        packageSha256="64fa19736ce6a2d15211fb8646ae1bafabda8918a7510b13dece18b414736fef"
        manifestSha256="4b2e05f25c45461a9cc754a7fc2bc0480da347379f5bd6070b51d387af94fd85"
        resultSha256="fc78ce57a27bed3cf757074cf1c7769ae4d6d2e75c5354ac0c9abd3eac85c67e"
    }
    foreach ($name in $expectedHashes.Keys) { if ($binding.$name -ne $expectedHashes[$name]) { throw "Manifest validation failed: executable A2 predecessor $name mismatch." } }
    foreach ($evidence in @(@("checkpointEvidenceBase64","aggregateFileSha256"),@("descriptorEvidenceBase64","descriptorSha256"),@("packageResultEvidenceBase64","resultSha256"))) {
        try { $bytes = [Convert]::FromBase64String([string]$binding.($evidence[0])) } catch { throw "Manifest validation failed: executable A2 predecessor evidence encoding mismatch." }
        $actual = ([Security.Cryptography.SHA256]::HashData($bytes) | ForEach-Object { $_.ToString("x2") }) -join ""
        if ($actual -ne [string]$binding.($evidence[1])) { throw "Manifest validation failed: executable A2 predecessor evidence digest mismatch." }
    }
    $expectedFiles = @{
        "AI/Orchestrator/Templates/artifact-intake-decision.schema.json"=@("407a195e093f7a8a6136983fb2bf946d7eac64ef",8618,"c21dd6856a20ca39df707ce28181e09056e272ed603962562c5a24dd554aa01b")
        "AI/Orchestrator/Templates/artifact-intake-decision.template.json"=@("cc9ac0c9e490a75a7643ea7394a09fe338084f35",3450,"fc04cabe343326c87b68ed121165687f0eb528a51ab380db056ac72bebdc3558")
        "AI/Orchestrator/Templates/artifact-intake-request.schema.json"=@("7378d130eda351b85c17ff3564de1fa45e701af1",7237,"f1b4b8d8f6cc434a2d1900161ee191fea61be77ab60a85fa1cb83cbb267f4e95")
        "AI/Orchestrator/Templates/artifact-intake-request.template.json"=@("ef68b6df094c7f117a33658a031cc93bc747aaac",3733,"48b167ecece4381a87fcc010c615581b06188f57c137843232068b3f3ff87db0")
    }
    if (@($binding.productionFiles).Count -ne 4) { throw "Manifest validation failed: executable A2 predecessor file count mismatch." }
    foreach ($file in @($binding.productionFiles)) {
        $expected = $expectedFiles[[string]$file.path]
        if ($null -eq $expected -or $file.mode -ne "100644" -or $file.gitBlobId -ne $expected[0] -or [int64]$file.size -ne [int64]$expected[1] -or $file.sha256 -ne $expected[2]) {
            throw "Manifest validation failed: executable A2 predecessor file binding mismatch."
        }
        $expectedFiles.Remove([string]$file.path)
    }
    if ($expectedFiles.Count -ne 0) { throw "Manifest validation failed: executable A2 predecessor file set mismatch." }
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
    Test-A2ExecutableAuthorization -Manifest $Manifest
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
        repositoryTransport = [pscustomobject]@{
            required = [bool]$Manifest.repositoryTransport.required
            status = if ($Manifest.repositoryTransport.required -eq $true) { "PENDING" } else { "NOT_REQUIRED" }
            requestPath = [string]$Manifest.repositoryTransport.requestPath
            approvedFiles = @($Manifest.repositoryTransport.approvedFiles)
            workflowRunId = ""
            workflowRunAttempt = ""
            appSlug = ""
            approvedBaseSha = ""
            targetBranch = ""
            treeSha = ""
            commitSha = ""
            prNumber = 0
            remoteTreeMatchesRequest = $false
            singleAtomicCommit = $false
            productOwnerManualTransport = $false
            agentCredentialExposure = $false
            merged = $false
        }
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
