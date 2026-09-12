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
            $approvedCodexBootstrapTasks = @("VSP-AI02-001T", "VSP-AI02-001TI-B1", "VSP-AI02-001TI-A1-DS1", "VSP-AI02-001TI-A1D-POLICY", "VSP-AI02-001TI-A1D-GEN", "VSP-AI02-001TI-A1D-VALIDATEF", "VSP-AI02-001TI-A-P2-AGV2", "VSP-AI02-001TI-A-P2-PM1", "VSP-AI02-001TI-A-P2-PM1-R1", "VSP-AI02-001TI-A2-R1I")
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

function Test-Pm1R1Authorization {
    param([Parameter(Mandatory = $true)] $Manifest)
    if ($Manifest.taskId -ne "VSP-AI02-001TI-A-P2-PM1-R1") { return }

    $expectedFiles = @(
        "tools/orchestrator/ai02-artifact-chain.ps1",
        "tools/orchestrator/test-ai02-artifact-chain.ps1",
        "AI/Orchestrator/Manifests/VSP-AI02-001TI-A-P2-PM1-R1.manifest.json",
        "AI/Orchestrator/State/VSP-AI02-001TI-A-P2-PM1-R1.state.json",
        "tools/orchestrator/task-manifest.ps1"
    )
    $actualFiles = @($Manifest.approvedFiles)
    if ($Manifest.authorizedSourceSha -ne "11375031aa3d4d98497bb48b60c39e37031983bb" -or $actualFiles.Count -ne $expectedFiles.Count) {
        throw "Manifest validation failed: PM1-R1 source or exact five-file allowlist mismatch."
    }
    foreach ($path in $expectedFiles) {
        if ($actualFiles -cnotcontains $path) { throw "Manifest validation failed: PM1-R1 exact five-file allowlist mismatch." }
    }

    $contract = $Manifest.failurePathContract
    if ($contract.function -ne "Assert-ExactSet" -or
        $contract.script -ne "tools/orchestrator/ai02-artifact-chain.ps1" -or
        $contract.allowEmptyActualCollection -ne $true -or
        $contract.emptyActualExpectedDisposition -ne "CHANGED_FILE_SET_MISMATCH_REJECT" -or
        $contract.emptyActualMayPass -ne $false -or
        $contract.packageCreatedOnMismatch -ne $false -or
        $contract.descriptorCreatedOnMismatch -ne $false -or
        $contract.checkpointCreatedOnMismatch -ne $false -or
        $contract.successfulPackagingSemanticsChanged -ne $false -or
        $contract.rawPowerShellExceptionNormative -ne $false) {
        throw "Manifest validation failed: PM1-R1 failure-path contract mismatch."
    }

    $validation = $Manifest.focusedValidation
    if ([int]$validation.preR1TestCount -ne 165 -or [int]$validation.newR1RegressionCount -ne 7 -or [int]$validation.expectedFinalTestCount -ne 172) {
        throw "Manifest validation failed: PM1-R1 focused-test accounting mismatch."
    }

    $ledger = $Manifest.permanentA2AttemptLedger
    if ($ledger.runId -ne "34699028049" -or
        $ledger.sourceSha -ne "11375031aa3d4d98497bb48b60c39e37031983bb" -or
        $ledger.claudeSessionId -ne "59f378db-aa23-4124-8d54-d18a3dfba2da" -or
        $ledger.disposition -ne "CLAUDE_ACTION_SUCCESS / REQUIRED_A2_OUTPUT_NOT_CREATED / ZERO_WORKING_TREE_CHANGES / ROOT_CAUSE_UNRESOLVED" -or
        [int]$ledger.initialSemanticAttemptsConsumed -ne 1 -or
        [int]$ledger.causeSpecificRemediationsConsumed -ne 0 -or
        [int]$ledger.remainingCauseSpecificRemediations -ne 1 -or
        $ledger.automaticRetry -ne $false -or $ledger.thirdAttemptAuthorized -ne $false -or
        $ledger.pm1R1ConsumesA2SemanticAttempt -ne $false -or
        $ledger.a2Readiness -ne "A2_R1_NOT_READY_ROOT_CAUSE_UNRESOLVED") {
        throw "Manifest validation failed: PM1-R1 permanent A2 attempt ledger mismatch."
    }

    $boundaries = $Manifest.credentialAndAuthorityBoundaries
    if ($boundaries.claudeRepositoryWriteCredential -ne $false -or
        $boundaries.repositoryMergeArtifactReadCredential -ne $false -or
        $boundaries.persistedCheckoutCredentials -ne $false -or
        $boundaries.directBranchAuthority -ne $false -or
        $boundaries.directPullRequestAuthority -ne $false -or
        $boundaries.mergeAuthority -ne $false -or
        $boundaries.repositoryTransportSoleAutomatedProductionWriteBoundary -ne $true -or
        $boundaries.productOwnerSoleMergeAuthority -ne $true) {
        throw "Manifest validation failed: PM1-R1 credential or authority boundary mismatch."
    }
}

function Test-A2R1InfrastructureAuthorization {
    param([Parameter(Mandatory = $true)] $Manifest)
    if ($Manifest.taskId -ne "VSP-AI02-001TI-A2-R1I") { return }

    $expectedFiles = @(
        ".github/workflows/ai02-claude-artifact-developer.yml",
        "tools/orchestrator/claude-artifact-developer.ps1",
        "tools/orchestrator/test-claude-artifact-developer.ps1",
        "tools/orchestrator/a2-remediation-bootstrap.ps1",
        "tools/orchestrator/test-a2-remediation-bootstrap.ps1",
        "AI/Orchestrator/Manifests/VSP-AI02-001TI-A2-R1I.manifest.json",
        "AI/Orchestrator/State/VSP-AI02-001TI-A2-R1I.state.json",
        "tools/orchestrator/task-manifest.ps1"
    )
    $actualFiles = @($Manifest.approvedFiles)
    if ($Manifest.authorizedSourceSha -ne "0a57293238c0430bda75aedfbbd2ab05b1098b44" -or $actualFiles.Count -ne $expectedFiles.Count) {
        throw "Manifest validation failed: A2-R1I source or exact eight-file allowlist mismatch."
    }
    foreach ($path in $expectedFiles) {
        if ($actualFiles -cnotcontains $path) { throw "Manifest validation failed: A2-R1I exact eight-file allowlist mismatch." }
    }

    $architecture = $Manifest.recoveryArchitecture
    if ($architecture.selectedOption -ne "A2_RECOVERY_OPTION_C_MECHANICAL_SKELETON_PLUS_CLAUDE" -or
        $architecture.activationTaskId -ne "VSP-AI02-001TI-A2-R1" -or
        $architecture.unrelatedArtifactDeveloperBehaviorChanged -ne $false -or
        $architecture.claudeSubstantivePrimaryDeveloperPreserved -ne $true -or
        $architecture.r1iConsumesA2SemanticAttempt -ne $false) {
        throw "Manifest validation failed: A2-R1I recovery architecture mismatch."
    }

    $skeleton = $Manifest.mechanicalSkeleton
    if ($skeleton.outputPath -ne "tools/orchestrator/artifact-intake-contract.ps1" -or
        $skeleton.encoding -ne "UTF-8_NO_BOM" -or $skeleton.lineEndings -ne "LF" -or
        $skeleton.sentinel -ne "NOT_IMPLEMENTED" -or $skeleton.strictMode -ne "Latest" -or
        $skeleton.containsFunctionSignatures -ne $false -or $skeleton.containsPolicyConstants -ne $false -or
        $skeleton.containsValidatorSemantics -ne $false -or $skeleton.terminalDisposition -ne "FAIL_CLOSED_NOT_IMPLEMENTED") {
        throw "Manifest validation failed: A2-R1I mechanical skeleton contract mismatch."
    }

    $harness = $Manifest.temporaryHarness
    $expectedInterface = @("ValidatorPath", "PolicyPath", "PolicySchemaPath", "RequestSchemaPath", "DecisionSchemaPath", "TrustedContextFixtureRoot", "OutputEvidencePath")
    if ($harness.location -ne "RUNNER_TEMP_OUTSIDE_REPOSITORY_OWNERSHIP" -or $harness.committed -ne $false -or
        $harness.normative -ne $false -or $harness.credentialless -ne $true -or $harness.networkRequired -ne $false -or
        $harness.repositoryWriteAuthority -ne $false -or $harness.hashLockedBeforeClaude -ne $true -or
        $harness.rehashRequiredAfterClaude -ne $true -or [int]$harness.minimumSemanticCaseCount -ne 52 -or
        [int]$harness.repeatedExecutionCount -ne 2 -or (@($harness.invocationInterface) -join ',') -cne ($expectedInterface -join ',')) {
        throw "Manifest validation failed: A2-R1I temporary harness contract mismatch."
    }

    $requiredChecks = @("OUTPUT_EXISTS_AND_NONEMPTY", "FINAL_SHA_DIFFERS_FROM_SKELETON", "POWERSHELL_PARSE_PASS", "NO_SENTINEL_OR_TODO", "EXACT_ONE_FILE_CHANGED_SET", "PREDECESSOR_BASELINE_UNCHANGED", "HARNESS_SHA_UNCHANGED", "TEST_VECTOR_SHA_SET_UNCHANGED", "NO_PROHIBITED_NETWORK_OR_PROCESS_API", "NO_GIT_OR_GITHUB_WRITE", "NO_BRANCH_PR_OR_MERGE", "NO_REPOSITORY_TRANSPORT", "NO_CREDENTIAL_ACCESS", "FOCUSED_SEMANTIC_MATRIX_PASS", "DETERMINISTIC_REPEAT_PASS")
    if ($Manifest.completionGuard.requiredBeforePackageChild -ne $true -or $Manifest.completionGuard.staticAnalysisIsSoleSecurityProof -ne $false -or
        @($Manifest.completionGuard.checks).Count -ne $requiredChecks.Count) {
        throw "Manifest validation failed: A2-R1I completion guard contract mismatch."
    }
    foreach ($check in $requiredChecks) {
        if (@($Manifest.completionGuard.checks) -cnotcontains $check) { throw "Manifest validation failed: A2-R1I completion guard missing $check." }
    }

    if ((@($Manifest.claudeBoundary.allowedTools) -join ',') -cne "Read,Write,Edit" -or
        $Manifest.claudeBoundary.bashAuthority -ne $false -or $Manifest.claudeBoundary.processAuthority -ne $false -or
        $Manifest.claudeBoundary.networkAuthority -ne $false -or $Manifest.claudeBoundary.repositoryWriteCredential -ne $false -or
        $Manifest.claudeBoundary.branchOrPullRequestAuthority -ne $false -or $Manifest.claudeBoundary.repositoryTransportAuthority -ne $false) {
        throw "Manifest validation failed: A2-R1I Claude permission boundary mismatch."
    }

    $attempt = $Manifest.attemptConsumption
    if ([int]$attempt.initialSemanticAttemptsConsumed -ne 1 -or [int]$attempt.causeSpecificRemediationsConsumed -ne 0 -or
        [int]$attempt.remainingCauseSpecificRemediations -ne 1 -or $attempt.r1iConsumesA2SemanticAttempt -ne $false -or
        $attempt.consumptionPoint -ne "Run Claude Code Primary Developer for A2-R1 actually begins" -or
        $attempt.preClaudeFailureConsumesAttempt -ne $false -or $attempt.automaticRetry -ne $false -or $attempt.thirdAttemptAuthorized -ne $false) {
        throw "Manifest validation failed: A2-R1I attempt-consumption contract mismatch."
    }

    $ledger = $Manifest.permanentA2AttemptLedger
    if ($ledger.runId -ne "34699028049" -or $ledger.sourceSha -ne "11375031aa3d4d98497bb48b60c39e37031983bb" -or
        $ledger.claudeSessionId -ne "59f378db-aa23-4124-8d54-d18a3dfba2da" -or
        $ledger.disposition -ne "CLAUDE_ACTION_SUCCESS / REQUIRED_A2_OUTPUT_NOT_CREATED / ZERO_WORKING_TREE_CHANGES / ROOT_CAUSE_UNRESOLVED" -or
        $ledger.rootCauseResolved -ne $false) {
        throw "Manifest validation failed: A2-R1I permanent A2 ledger mismatch."
    }
}

function Test-A2R1FinalAuthorization {
    param([Parameter(Mandatory = $true)] $Manifest)
    if ($Manifest.taskId -ne "VSP-AI02-001TI-A2-R1") { return }

    $sourceSha = "122284717b64259ffcaee934aed1537a84c0bc34"
    $ownedPath = "tools/orchestrator/artifact-intake-contract.ps1"
    $fixtureFiles = @(
        "AI/Orchestrator/Manifests/VSP-AI02-001TI-A2-R1.manifest.json",
        "AI/Orchestrator/State/VSP-AI02-001TI-A2-R1.state.json",
        "tools/orchestrator/task-manifest.ps1"
    )
    if ($Manifest.authorizedSourceSha -ne $sourceSha -or
        @($Manifest.approvedFiles).Count -ne 1 -or $Manifest.approvedFiles[0] -cne $ownedPath -or
        $Manifest.authorizationFixture.taskId -ne "VSP-AI02-001TI-A2-R1F" -or
        $Manifest.authorizationFixture.classification -ne "CRITICAL" -or
        $Manifest.authorizationFixture.developerRole -ne "Codex Development Agent" -or
        $Manifest.authorizationFixture.bootstrapException -ne $true -or
        $Manifest.authorizationFixture.consumesA2SemanticAttempt -ne $false -or
        @($Manifest.authorizationFixture.approvedFiles).Count -ne $fixtureFiles.Count) {
        throw "Manifest validation failed: A2-R1 identity, production ownership, or R1F fixture scope mismatch."
    }
    foreach ($path in $fixtureFiles) {
        if (@($Manifest.authorizationFixture.approvedFiles) -cnotcontains $path) {
            throw "Manifest validation failed: A2-R1F exact three-file fixture allowlist mismatch."
        }
    }

    if ($Manifest.primaryDeveloper.role -ne "Claude Code Primary Developer" -or
        $Manifest.primaryDeveloper.adapter -ne "claude" -or
        $Manifest.executionPlane -ne "AI02 Claude Artifact Developer" -or
        $Manifest.executionAuthorization.implementation -ne $true -or
        $Manifest.executionAuthorization.pushFeatureBranch -ne $false -or
        $Manifest.executionAuthorization.openOrUpdatePr -ne $false) {
        throw "Manifest validation failed: A2-R1 developer or direct repository authority mismatch."
    }
    $executable = $Manifest.executableA2
    if ($executable.taskId -ne "VSP-AI02-001TI-A2-R1" -or $executable.finalRemediation -ne $true -or
        $executable.primaryDeveloper -ne "Claude Code Primary Developer" -or
        $executable.executionPlane -ne "AI02 Claude Artifact Developer" -or
        @($executable.productionOwnership).Count -ne 1 -or $executable.productionOwnership[0] -cne $ownedPath -or
        $executable.networkAuthority -ne $false -or $executable.repositoryWriteAuthority -ne $false -or
        $executable.artifactReadCredentialAvailableToClaude -ne $false -or
        $executable.branchOrPullRequestAuthority -ne $false -or $executable.repositoryTransportAuthority -ne $false -or
        $executable.directMergeAuthority -ne $false -or $executable.persistedCheckoutCredentials -ne $false -or
        $executable.localAiAuthority -ne $false -or $executable.productOwnerSoleMergeAuthority -ne $true) {
        throw "Manifest validation failed: A2-R1 executable or credential boundary mismatch."
    }

    if ($Manifest.phasedChild.enabled -ne $true -or $Manifest.phasedChild.phase -ne "A2" -or
        [int]$Manifest.phasedChild.sequence -ne 2 -or [int]$Manifest.phasedChild.predecessorCount -ne 1 -or
        $Manifest.phasedChild.predecessorSourceType -ne "AUTHORITATIVE_REPOSITORY_MERGE" -or
        $Manifest.phasedChild.checkpointSchemaVersion -ne "2.0" -or
        $Manifest.phasedChild.parentAggregateStateDigest -ne "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1") {
        throw "Manifest validation failed: A2-R1 phased-child contract mismatch."
    }

    $attempt = $Manifest.attemptBudget
    if ([int]$attempt.initialAttempts -ne 1 -or $attempt.initialAttemptStatus -ne "CONSUMED" -or
        [int]$attempt.initialSemanticAttemptsConsumed -ne 1 -or [int]$attempt.maximumCauseSpecificRemediations -ne 1 -or
        [int]$attempt.causeSpecificRemediationsConsumed -ne 0 -or [int]$attempt.remainingCauseSpecificRemediations -ne 1 -or
        [int]$attempt.maximumTotalAttempts -ne 2 -or [int]$attempt.nextAttempt -ne 2 -or $attempt.finalAttempt -ne $true -or
        $attempt.automaticRetry -ne $false -or $attempt.thirdAttemptAuthorized -ne $false -or
        $attempt.consumptionPoint -ne "Run Claude Code Primary Developer for VSP-AI02-001TI-A2-R1 actually begins" -or
        $attempt.preClaudeFailureConsumesAttempt -ne $false) {
        throw "Manifest validation failed: A2-R1 final remediation attempt accounting mismatch."
    }

    $executionBase = $Manifest.executionBaseHandling
    if ($executionBase.fixtureImplementationBaseSha -ne $sourceSha -or $executionBase.actualExecutionSha -ne $sourceSha -or
        $executionBase.fixtureSourceIsHistoricalAfterMerge -ne $true -or $executionBase.shaChasingFixtureRequired -ne $false -or
        $executionBase.actualExecutionShaSource -ne "PRODUCT_OWNER_AUTHORIZED_WORKFLOW_INPUT" -or
        $executionBase.postMergeExecutionBaseAuthorizationRequired -ne $true) {
        throw "Manifest validation failed: A2-R1 execution-base handling mismatch."
    }

    $dependency = $Manifest.r1iDependency
    if ($dependency.required -ne $true -or $dependency.status -ne "COMPLETE" -or
        $dependency.mergeCommit -ne $sourceSha -or $dependency.reviewedHead -ne "8a7c11f514375db7a905d450d70f39d6dbbcd1b7" -or
        [int]$dependency.pullRequest -ne 86 -or $dependency.windowsCiRunId -ne "34704493531" -or
        $dependency.claudeSecurityCrossReviewRunId -ne "34704494013" -or
        $dependency.independentReviewerContext -ne "/root/publishf_independent_review" -or
        [int]$dependency.unresolvedFindings -ne 0 -or [int]$dependency.unresolvedReviewThreads -ne 0 -or
        $dependency.scopeDrift -ne "NONE") {
        throw "Manifest validation failed: A2-R1 merged R1I dependency mismatch."
    }

    $infrastructure = $Manifest.remediationInfrastructure
    $expectedInterface = @("ValidatorPath","PolicyPath","PolicySchemaPath","RequestSchemaPath","DecisionSchemaPath","TrustedContextFixtureRoot","OutputEvidencePath")
    if ($infrastructure.architecture -ne "A2_RECOVERY_OPTION_C_MECHANICAL_SKELETON_PLUS_CLAUDE" -or
        $infrastructure.r1iRequired -ne $true -or $infrastructure.r1iTaskId -ne "VSP-AI02-001TI-A2-R1I" -or
        $infrastructure.r1iMergeCommit -ne $sourceSha -or $infrastructure.outputPath -cne $ownedPath -or
        $infrastructure.sentinel -ne "NOT_IMPLEMENTED" -or (@($infrastructure.claudeAllowedTools) -join ',') -cne "Read,Write,Edit" -or
        (@($infrastructure.validatorInvocationInterface.parameters) -join ',') -cne ($expectedInterface -join ',') -or
        $infrastructure.packageChildOnlyAfterGuardPass -ne $true) {
        throw "Manifest validation failed: A2-R1 Option C or invocation-interface binding mismatch."
    }
    $skeleton = $infrastructure.skeleton
    if ($skeleton.deterministic -ne $true -or $skeleton.encoding -ne "UTF-8_NO_BOM" -or $skeleton.lineEndings -ne "LF" -or
        $skeleton.nonfunctional -ne $true -or $skeleton.containsSubstantiveSemantics -ne $false -or
        $skeleton.recordByteSize -ne $true -or $skeleton.recordSha256 -ne $true -or
        $skeleton.finalShaMustDiffer -ne $true -or [int]$skeleton.finalSentinelCount -ne 0) {
        throw "Manifest validation failed: A2-R1 skeleton contract mismatch."
    }
    $harness = $infrastructure.harness
    if ($harness.location -ne "RUNNER_TEMP_OUTSIDE_REPOSITORY_OWNERSHIP" -or $harness.committed -ne $false -or
        $harness.normative -ne $false -or $harness.credentialless -ne $true -or $harness.hashLocked -ne $true -or
        [int]$harness.minimumSemanticCaseCount -ne 52 -or [int]$harness.repeatedExecutionCount -ne 2 -or
        $harness.expectedOutcomeOracleExposed -ne $false -or $harness.isolatedValidatorExecution -ne $true) {
        throw "Manifest validation failed: A2-R1 temporary harness contract mismatch."
    }
    $requiredChecks = @("OUTPUT_EXISTS_AND_NONEMPTY","FINAL_SHA_DIFFERS_FROM_SKELETON","POWERSHELL_PARSE_PASS","NO_SENTINEL_OR_TODO","EXACT_ONE_FILE_CHANGED_SET","PREDECESSOR_BASELINE_UNCHANGED","HARNESS_SHA_UNCHANGED","TEST_VECTOR_SHA_SET_UNCHANGED","NO_PROHIBITED_NETWORK_OR_PROCESS_API","NO_GIT_OR_GITHUB_WRITE","NO_BRANCH_PR_OR_MERGE","NO_REPOSITORY_TRANSPORT","NO_CREDENTIAL_ACCESS","FOCUSED_SEMANTIC_MATRIX_PASS","DETERMINISTIC_REPEAT_PASS")
    if (@($infrastructure.completionGuard).Count -ne $requiredChecks.Count) { throw "Manifest validation failed: A2-R1 completion-guard count mismatch." }
    foreach ($check in $requiredChecks) {
        if (@($infrastructure.completionGuard) -cnotcontains $check) { throw "Manifest validation failed: A2-R1 completion guard missing $check." }
    }

    foreach ($contractName in @("trustedContext","pathResponsibility","failureMapping","replayAndStaleBase","decisionStateMachine","evidenceContract")) {
        if ($null -eq $Manifest.$contractName) { throw "Manifest validation failed: A2-R1 prompt contract missing $contractName." }
    }
    if ($Manifest.trustedContext.producerClaimsAuthoritative -ne $false -or
        $Manifest.replayAndStaleBase.staleBasePolicy -ne "EXACT_BASE_ONLY" -or
        $Manifest.replayAndStaleBase.emptyConsumedIdentities -ne "FIRST_USE_NOT_YET_CONSUMED" -or
        $Manifest.replayAndStaleBase.matchingConsumedIdentity -ne "REJECT_REPLAY" -or
        $Manifest.decisionStateMachine.transportInvokedGlobally -ne $false) {
        throw "Manifest validation failed: A2-R1 rendered semantic contract mismatch."
    }

    $ledger = $Manifest.permanentA2AttemptLedger
    if ($ledger.runId -ne "34699028049" -or $ledger.sourceSha -ne "11375031aa3d4d98497bb48b60c39e37031983bb" -or
        $ledger.claudeSessionId -ne "59f378db-aa23-4124-8d54-d18a3dfba2da" -or
        $ledger.disposition -ne "CLAUDE_ACTION_SUCCESS / REQUIRED_A2_OUTPUT_NOT_CREATED / ZERO_WORKING_TREE_CHANGES / ROOT_CAUSE_UNRESOLVED" -or
        $ledger.rootCauseResolved -ne $false -or $ledger.pm1R1Role -ne "SECONDARY_PACKAGER_FAILURE_PATH_REMEDIATION_ONLY") {
        throw "Manifest validation failed: A2-R1 permanent historical ledger mismatch."
    }

    $bindings = @($Manifest.predecessorBindings)
    if ($bindings.Count -ne 1) { throw "Manifest validation failed: A2-R1 requires exactly one predecessor binding." }
    $binding = $bindings[0]
    if ($binding.sourceType -ne "AUTHORITATIVE_REPOSITORY_MERGE" -or $binding.repository -ne "game2082001/VSP" -or
        $binding.mergeCommit -ne "aa53c00d5e4125a53f8f835220bf6d6b2e911b14" -or
        $binding.predecessorTaskId -ne "VSP-AI02-001TI-A1D-VALIDATE" -or $binding.phase -ne "A1" -or [int]$binding.sequence -ne 1 -or
        $binding.checkpointSchemaVersion -ne "1.0" -or
        $binding.aggregateStateDigest -ne "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1" -or
        $binding.descriptorSha256 -ne "c1595130a63ffb3eaba0facbb4c0f1e73c5dbf17db098ea5052ca23ff021a253" -or
        $binding.aggregateFileSha256 -ne "750334cd06ecf529303b14ba0431f5ca492c714e606baca6767aef5961106ddc" -or
        $binding.packageSha256 -ne "64fa19736ce6a2d15211fb8646ae1bafabda8918a7510b13dece18b414736fef" -or
        $binding.manifestSha256 -ne "4b2e05f25c45461a9cc754a7fc2bc0480da347379f5bd6070b51d387af94fd85" -or
        $binding.resultSha256 -ne "fc78ce57a27bed3cf757074cf1c7769ae4d6d2e75c5354ac0c9abd3eac85c67e") {
        throw "Manifest validation failed: A2-R1 A1 predecessor or PM1 binding mismatch."
    }
    if ($Manifest.repositoryTransport.required -ne $true -or $Manifest.repositoryTransport.status -ne "REQUIRED_LATER_NOT_AUTHORIZED_NOW" -or
        @($Manifest.repositoryTransport.approvedFiles).Count -ne 1 -or $Manifest.repositoryTransport.approvedFiles[0] -cne $ownedPath -or
        $Manifest.repositoryTransport.invokedDuringA2Execution -ne $false -or
        $Manifest.repositoryTransport.separateProductOwnerAuthorizationRequired -ne $true) {
        throw "Manifest validation failed: A2-R1 later Repository Transport boundary mismatch."
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
    Test-A2ExecutableAuthorization -Manifest $Manifest
    Test-Pm1R1Authorization -Manifest $Manifest
    Test-A2R1InfrastructureAuthorization -Manifest $Manifest
    Test-A2R1FinalAuthorization -Manifest $Manifest
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
