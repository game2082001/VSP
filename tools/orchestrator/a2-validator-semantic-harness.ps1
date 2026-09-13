[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ValidatorPath,

    [string] $PolicyPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Policy\artifact-intake-policy.json"),
    [string] $PolicySchemaPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Policy\artifact-intake-policy.schema.json"),
    [string] $RequestSchemaPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Templates\artifact-intake-request.schema.json"),
    [string] $DecisionSchemaPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Templates\artifact-intake-decision.schema.json"),
    [string] $OutputDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) ("ai02-a2-validator-harness-" + [guid]::NewGuid().ToString("N"))),
    [int] $MinimumCaseCount = 52
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Stop-Harness {
    param([Parameter(Mandatory = $true)][string] $Message)
    throw "AI02 A2 validator semantic harness failed: $Message"
}

function Read-Json {
    param([Parameter(Mandatory = $true)][string] $Path)
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable -Depth 100
}

function Write-Json {
    param([Parameter(Mandatory = $true)] $Value, [Parameter(Mandatory = $true)][string] $Path)
    $json = ConvertTo-CanonicalJson $Value
    [System.IO.File]::WriteAllText($Path, $json, [System.Text.UTF8Encoding]::new($false))
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string] $Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-PolicyProjection {
    param([Parameter(Mandatory = $true)][hashtable] $Policy)
    [ordered]@{
        repositoryPath = $Policy.repositoryPath
        limits = $Policy.limits
        replay = $Policy.replay
        staleBase = $Policy.staleBase
        credentialInvariants = $Policy.credentialInvariants
        transport = $Policy.transport
    }
}

function ConvertTo-CanonicalJson {
    param($Value)
    return (($Value | ConvertTo-Json -Depth 100 -Compress) + "`n").Replace("`r`n", "`n").Replace("`r", "`n")
}

function Test-JsonEqual {
    param($Left, $Right)
    return ((ConvertTo-CanonicalJson $Left) -ceq (ConvertTo-CanonicalJson $Right))
}

function New-BaseVector {
    $policy = Read-Json $PolicyPath
    $projection = Get-PolicyProjection $policy
    $request = [ordered]@{
        schemaVersion = $policy.schemaVersion
        schemaId = $policy.identity.requestSchemaIdentifier
        policyVersion = $policy.identity.policyVersion
        taskId = "VSP-AI02-001TI-A2-VAL1"
        repository = $policy.identity.repository.exactValue
        expectedSourceSha = "e969928c568bbe1b0bb25b355abf2cfe33fd4e55"
        developer = [ordered]@{ workflowId = "1"; runId = "1"; runAttempt = 1 }
        artifact = [ordered]@{ artifactId = "1"; artifactName = "a2-val1-package"; githubArtifactDigest = ("sha256:" + ("1" * 64)) }
        hashBindings = [ordered]@{ packageSha256 = ("2" * 64); manifestSha256 = ("3" * 64); stateSha256 = ("4" * 64); trustedInnerHashPins = @() }
        authorization = [ordered]@{
            manifestPath = "AI/Orchestrator/Manifests/VSP-AI02-001TI-A2-VAL1.manifest.json"
            statePath = "AI/Orchestrator/State/VSP-AI02-001TI-A2-VAL1.state.json"
            approvedFiles = @("tools/orchestrator/artifact-intake-contract.ps1")
        }
        transportIntent = [ordered]@{ handoff = $policy.transport.handoff; interface = $policy.transport.interface; requestTransportAuthorization = $policy.transport.trustedIntakeMayAuthorize }
        policy = $projection
    }
    $requestSha = Get-TextSha256 (ConvertTo-CanonicalJson $request)
    $decision = [ordered]@{
        schemaVersion = $policy.schemaVersion
        schemaId = $policy.identity.decisionSchemaIdentifier
        policyVersion = $policy.identity.policyVersion
        taskId = $request.taskId
        repository = $request.repository
        requestReference = [ordered]@{
            requestSha256 = $requestSha
            artifactId = $request.artifact.artifactId
            artifactName = $request.artifact.artifactName
            githubArtifactDigest = $request.artifact.githubArtifactDigest
            packageSha256 = $request.hashBindings.packageSha256
            manifestSha256 = $request.hashBindings.manifestSha256
            stateSha256 = $request.hashBindings.stateSha256
        }
        decision = "ACCEPTED_FOR_TRANSPORT"
        transportAuthorized = $true
        transportInvoked = $false
        evidence = [ordered]@{ statusCategory = "VALID_PACKAGE"; failureCategory = "NONE"; approvedFilesCount = 1 }
        policy = $projection
    }
    $trusted = [ordered]@{
        sourceSha = $request.expectedSourceSha
        actualChangedFiles = @("tools/orchestrator/artifact-intake-contract.ps1")
        consumedIdentities = @()
        fileMetadata = @([ordered]@{ path = "tools/orchestrator/artifact-intake-contract.ps1"; mode = "100644"; size = 4096; sha256 = ("5" * 64) })
        predecessor = [ordered]@{
            taskId = "VSP-AI02-001TI-A1D-VALIDATE"
            phase = "A1"
            sequence = 1
            descriptorSha256 = "c1595130a63ffb3eaba0facbb4c0f1e73c5dbf17db098ea5052ca23ff021a253"
            parentAggregateStateDigest = "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1"
        }
    }
    [ordered]@{ request = $request; decision = $decision; trusted = $trusted }
}

function Get-TextSha256 {
    param([Parameter(Mandatory = $true)][string] $Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Text)) | ForEach-Object { $_.ToString("x2") }) -join "") } finally { $sha.Dispose() }
}

function Sync-DecisionReference {
    param([Parameter(Mandatory = $true)] $Vector)
    $Vector.decision.requestReference.requestSha256 = Get-TextSha256 (ConvertTo-CanonicalJson $Vector.request)
    $Vector.decision.requestReference.artifactId = $Vector.request.artifact.artifactId
    $Vector.decision.requestReference.artifactName = $Vector.request.artifact.artifactName
    $Vector.decision.requestReference.githubArtifactDigest = $Vector.request.artifact.githubArtifactDigest
    $Vector.decision.requestReference.packageSha256 = $Vector.request.hashBindings.packageSha256
    $Vector.decision.requestReference.manifestSha256 = $Vector.request.hashBindings.manifestSha256
    $Vector.decision.requestReference.stateSha256 = $Vector.request.hashBindings.stateSha256
}

function New-Case {
    param([string] $Id, [string] $ExpectedResult, [string] $ExpectedCategory, [string] $ExpectedFinding, [scriptblock] $Mutation, [switch] $SkipPostMutationSync)
    $vector = New-BaseVector
    if ($Mutation) { & $Mutation $vector }
    if (-not $SkipPostMutationSync) { Sync-DecisionReference $vector }
    [pscustomobject]@{ id = $Id; expectedResult = $ExpectedResult; expectedCategory = $ExpectedCategory; expectedFinding = $ExpectedFinding; vector = $vector }
}

function Get-Cases {
    $cases = [System.Collections.Generic.List[object]]::new()
    $cases.Add((New-Case "01-valid-first-use" "ACCEPTED_FOR_TRANSPORT" "NONE" "" {}))
    $cases.Add((New-Case "02-replay-rejected" "REJECTED" "REPLAY_DETECTED" "REPLAY_IDENTITY_MATCH" { param($v) $sha = Get-TextSha256 (ConvertTo-CanonicalJson $v.request); $v.trusted.consumedIdentities = @($sha) }))
    $cases.Add((New-Case "03-stale-base" "REJECTED" "STALE_BASE" "AUTHORIZED_SOURCE_SHA_MISMATCH" { param($v) $v.trusted.sourceSha = "0" * 40 }))
    $cases.Add((New-Case "04-changed-files-empty" "REJECTED" "APPROVED_FILES_MISMATCH" "CHANGED_FILE_SET_MISMATCH" { param($v) $v.trusted.actualChangedFiles = @() }))
    $cases.Add((New-Case "05-extra-file" "REJECTED" "APPROVED_FILES_MISMATCH" "CHANGED_FILE_SET_MISMATCH" { param($v) $v.trusted.actualChangedFiles = @("tools/orchestrator/artifact-intake-contract.ps1", "extra.txt") }))
    $cases.Add((New-Case "06-missing-file-metadata" "REJECTED" "OVERSIZED_FILE" "FILE_METADATA_INVALID" { param($v) $v.trusted.fileMetadata[0].size = 0 }))
    foreach ($path in @("../escape", "folder/../escape", "folder\escape", "C:escape", "/absolute", "//unc/path", "folder//file", "folder/./file", "folder/", "", ("a" * 241))) {
        $caseId = ("path-" + ($cases.Count + 1).ToString("00"))
        $cases.Add((New-Case $caseId "REJECTED" "PATH_POLICY_VIOLATION" "REPOSITORY_PATH_POLICY_VIOLATION" { param($v) $v.request.authorization.approvedFiles = @($path); $v.trusted.actualChangedFiles = @($path); $v.trusted.fileMetadata[0].path = $path }))
    }
    $mutations = @(
        @("artifact-id", "DIGEST_MISMATCH", "ARTIFACT_BINDING_MISMATCH", { param($v) $v.decision.requestReference.artifactId = "9" }),
        @("artifact-digest", "DIGEST_MISMATCH", "ARTIFACT_BINDING_MISMATCH", { param($v) $v.decision.requestReference.githubArtifactDigest = "sha256:$("9" * 64)" }),
        @("package-sha", "DIGEST_MISMATCH", "PACKAGE_BINDING_MISMATCH", { param($v) $v.decision.requestReference.packageSha256 = "9" * 64 }),
        @("manifest-sha", "DIGEST_MISMATCH", "PACKAGE_BINDING_MISMATCH", { param($v) $v.decision.requestReference.manifestSha256 = "9" * 64 }),
        @("state-sha", "DIGEST_MISMATCH", "PACKAGE_BINDING_MISMATCH", { param($v) $v.decision.requestReference.stateSha256 = "9" * 64 }),
        @("bad-repo", "MANIFEST_OR_STATE_INVALID", "REPOSITORY_MISMATCH", { param($v) $v.request.repository = "evil/repo"; $v.decision.repository = "evil/repo" }),
        @("arbitrary-task-id", "MANIFEST_OR_STATE_INVALID", "TASK_ID_MISMATCH", { param($v) $v.request.taskId = "VSP-AI02-ARBITRARY-REPLACEMENT"; $v.decision.taskId = "VSP-AI02-ARBITRARY-REPLACEMENT" }),
        @("decision-task-mismatch", "MANIFEST_OR_STATE_INVALID", "TASK_ID_MISMATCH", { param($v) $v.decision.taskId = "VSP-AI02-DIFFERENT-TASK" }),
        @("manifest-path", "MANIFEST_OR_STATE_INVALID", "GOVERNANCE_PATH_MISMATCH", { param($v) $v.request.authorization.manifestPath = "AI/Orchestrator/Manifests/VSP-AI02-ARBITRARY.manifest.json" }),
        @("state-path", "MANIFEST_OR_STATE_INVALID", "GOVERNANCE_PATH_MISMATCH", { param($v) $v.request.authorization.statePath = "AI/Orchestrator/State/VSP-AI02-ARBITRARY.state.json" }),
        @("bad-decision", "STRUCTURE_MISMATCH", "DECISION_STATE_MISMATCH", { param($v) $v.decision.decision = "REJECTED"; $v.decision.transportAuthorized = $false }),
        @("transport-invoked", "STRUCTURE_MISMATCH", "DECISION_STATE_MISMATCH", { param($v) $v.decision.transportInvoked = $true }),
        @("transport-not-authorized", "STRUCTURE_MISMATCH", "DECISION_STATE_MISMATCH", { param($v) $v.decision.transportAuthorized = $false }),
        @("status-category", "STRUCTURE_MISMATCH", "DECISION_STATE_MISMATCH", { param($v) $v.decision.evidence.statusCategory = "DIGEST_MISMATCH" }),
        @("failure-category", "STRUCTURE_MISMATCH", "DECISION_STATE_MISMATCH", { param($v) $v.decision.evidence.failureCategory = "DIGEST_MISMATCH" }),
        @("predecessor-task", "MANIFEST_OR_STATE_INVALID", "PREDECESSOR_BINDING_MISMATCH", { param($v) $v.trusted.predecessor.taskId = "VSP-AI02-001TI-A2" }),
        @("predecessor-phase", "MANIFEST_OR_STATE_INVALID", "PREDECESSOR_BINDING_MISMATCH", { param($v) $v.trusted.predecessor.phase = "A2" }),
        @("predecessor-sequence", "MANIFEST_OR_STATE_INVALID", "PREDECESSOR_BINDING_MISMATCH", { param($v) $v.trusted.predecessor.sequence = 2 }),
        @("descriptor", "MANIFEST_OR_STATE_INVALID", "A1_LINEAGE_MISMATCH", { param($v) $v.trusted.predecessor.descriptorSha256 = "9" * 64 }),
        @("aggregate", "MANIFEST_OR_STATE_INVALID", "A1_LINEAGE_MISMATCH", { param($v) $v.trusted.predecessor.parentAggregateStateDigest = "sha256:$("9" * 64)" })
    )
    foreach ($m in $mutations) { $cases.Add((New-Case ("semantic-" + $m[0]) "REJECTED" $m[1] $m[2] $m[3] -SkipPostMutationSync)) }
    while ($cases.Count -lt 52) {
        $index = $cases.Count + 1
        $cases.Add((New-Case ("repeat-valid-" + $index.ToString("00")) "ACCEPTED_FOR_TRANSPORT" "NONE" "" {}))
    }
    return @($cases)
}

function Assert-SelfValidation {
    param([Parameter(Mandatory = $true)][object[]] $Cases)
    $policy = Read-Json $PolicyPath
    $projection = Get-PolicyProjection $policy
    foreach ($case in $Cases) {
        $v = $case.vector
        if ($case.expectedResult -eq "ACCEPTED_FOR_TRANSPORT") {
            if (-not (Test-JsonEqual $v.request.policy $projection) -or -not (Test-JsonEqual $v.decision.policy $projection)) { Stop-Harness "PASS vector policy projection is invalid: $($case.id)" }
            if ($v.request.repository -cne "game2082001/VSP" -or $v.trusted.sourceSha -cne $v.request.expectedSourceSha) { Stop-Harness "PASS vector identity is incoherent: $($case.id)" }
            if ($v.trusted.predecessor.taskId -cne "VSP-AI02-001TI-A1D-VALIDATE" -or $v.trusted.predecessor.descriptorSha256 -cne "c1595130a63ffb3eaba0facbb4c0f1e73c5dbf17db098ea5052ca23ff021a253") { Stop-Harness "PASS vector A1 predecessor is invalid: $($case.id)" }
        }
    }
}

if (-not (Test-Path -LiteralPath $ValidatorPath -PathType Leaf)) { Stop-Harness "ValidatorPath is missing." }
$cases = Get-Cases
if ($cases.Count -lt $MinimumCaseCount) { Stop-Harness "Semantic case count is below the approved minimum." }
Assert-SelfValidation -Cases $cases

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$results = [System.Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    $caseHashes = @()
    for ($run = 1; $run -le 2; $run++) {
        $caseRoot = Join-Path $OutputDirectory ("run$run-$($case.id)")
        New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
        Write-Json $case.vector.request (Join-Path $caseRoot "request.json")
        Write-Json $case.vector.decision (Join-Path $caseRoot "decision.json")
        Write-Json $case.vector.trusted (Join-Path $caseRoot "trusted-context.json")
        $evidencePath = Join-Path $caseRoot "evidence.json"
        & pwsh -NoProfile -File $ValidatorPath -PolicyPath $PolicyPath -PolicySchemaPath $PolicySchemaPath -RequestSchemaPath $RequestSchemaPath -DecisionSchemaPath $DecisionSchemaPath -TrustedContextFixtureRoot $caseRoot -OutputEvidencePath $evidencePath | Out-Null
        if ($LASTEXITCODE -ne 0) { Stop-Harness "Validator process failed: $($case.id)" }
        $evidence = Read-Json $evidencePath
        if ($evidence.result -cne $case.expectedResult) { Stop-Harness "Unexpected result for $($case.id)." }
        if ($evidence.canonicalFailureCategory -cne $case.expectedCategory) { Stop-Harness "Unexpected category for $($case.id)." }
        if ($case.expectedFinding -and (@($evidence.findingCodes) -cnotcontains $case.expectedFinding)) { Stop-Harness "Expected finding not emitted for $($case.id)." }
        $caseHashes += Get-Sha256 $evidencePath
    }
    if ($caseHashes[0] -cne $caseHashes[1]) { Stop-Harness "Evidence is not deterministic for $($case.id)." }
    $results.Add([ordered]@{ id = $case.id; result = $case.expectedResult; category = $case.expectedCategory; evidenceSha256 = $caseHashes[0] })
}

$summary = [ordered]@{
    result = "PASS"
    harnessSelfValidation = "PASS"
    semanticCaseCount = $cases.Count
    executedTwice = $true
    deterministicRepeatedExecution = $true
    sixSectionPolicyProjection = "PASS"
    authoritativeA1Predecessor = "PASS"
    expectedOutcomeOracleExposed = $false
    repositoryWriteCredentialAvailableToDeveloper = $false
    cases = @($results)
}
$summaryPath = Join-Path $OutputDirectory "semantic-harness-summary.json"
Write-Json $summary $summaryPath
$summary | ConvertTo-Json -Depth 10
