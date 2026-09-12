param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Prepare", "ValidateCompletion")]
    [string] $Mode,

    [Parameter(Mandatory = $true)]
    [string] $ManifestPath,

    [Parameter(Mandatory = $true)]
    [string] $WorkspacePath,

    [Parameter(Mandatory = $true)]
    [string] $RuntimeDirectory,

    [Parameter(Mandatory = $true)]
    [string] $PredecessorBaselinePath,

    [string] $ExpectedBaselineSha256 = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ExpectedTaskId = "VSP-AI02-001TI-A2-R1"
$ExpectedOutputPath = "tools/orchestrator/artifact-intake-contract.ps1"
$Sentinel = "NOT_IMPLEMENTED"
$Utf8NoBom = [Text.UTF8Encoding]::new($false)

function Stop-Bootstrap {
    param([Parameter(Mandatory = $true)][string] $Message)
    throw "A2 remediation bootstrap validation failed: $Message"
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Stop-Bootstrap "Required file not found: $Path"
    }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-DeterministicText {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text
    )
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    [IO.File]::WriteAllText($Path, $normalized, $Utf8NoBom)
}

function Write-DeterministicJson {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)] $Value,
        [int] $Depth = 30
    )
    $json = ($Value | ConvertTo-Json -Depth $Depth) + "`n"
    Write-DeterministicText -Path $Path -Text $json
}

function Read-Json {
    param([Parameter(Mandatory = $true)][string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Stop-Bootstrap "Required JSON file not found: $Path"
    }
    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
    } catch {
        Stop-Bootstrap "Invalid JSON file: $Path"
    }
}

function Resolve-ContainedPath {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $RelativePath
    )
    if ([IO.Path]::IsPathRooted($RelativePath) -or $RelativePath -match '(^|[\\/])\.\.([\\/]|$)') {
        Stop-Bootstrap "Repository path is not a safe relative path: $RelativePath"
    }
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $candidate = [IO.Path]::GetFullPath((Join-Path $rootFull $RelativePath))
    if (-not $candidate.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-Bootstrap "Resolved path escapes the workspace: $RelativePath"
    }
    return $candidate
}

function Get-ChangedFiles {
    param([Parameter(Mandatory = $true)][string] $Root)
    $tracked = @(& git -C $Root diff --name-only HEAD --)
    if ($LASTEXITCODE -ne 0) { Stop-Bootstrap "Unable to inspect tracked working-tree changes." }
    $untracked = @(& git -C $Root ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { Stop-Bootstrap "Unable to inspect untracked working-tree changes." }
    return @($tracked + $untracked | ForEach-Object { ([string]$_).Replace('\', '/') } | Where-Object { $_ } | Sort-Object -Unique)
}

function Assert-ExactChangedFile {
    param([Parameter(Mandatory = $true)][string] $Root)
    $changed = @(Get-ChangedFiles -Root $Root)
    if ($changed.Count -ne 1 -or $changed[0] -cne $ExpectedOutputPath) {
        Stop-Bootstrap "Working-tree changed-file set must equal exactly $ExpectedOutputPath. Actual: $($changed -join ', ')"
    }
}

function Assert-R1Manifest {
    param([Parameter(Mandatory = $true)] $Manifest)
    if ($Manifest.taskId -ne $ExpectedTaskId -or $Manifest.classification -ne "CRITICAL") {
        Stop-Bootstrap "Infrastructure is gated to $ExpectedTaskId CRITICAL only."
    }
    $approved = @($Manifest.repositoryTransport.approvedFiles)
    if ($approved.Count -ne 1 -or $approved[0] -cne $ExpectedOutputPath) {
        Stop-Bootstrap "A2-R1 must authorize exactly the one A2 production path."
    }
    $contract = $Manifest.remediationInfrastructure
    if ($null -eq $contract -or $contract.architecture -ne "A2_RECOVERY_OPTION_C_MECHANICAL_SKELETON_PLUS_CLAUDE" -or
        $contract.outputPath -cne $ExpectedOutputPath -or $contract.sentinel -cne $Sentinel -or
        $contract.harness.minimumSemanticCaseCount -ne 52 -or $contract.claudeAllowedTools -join ',' -cne 'Read,Write,Edit') {
        Stop-Bootstrap "A2-R1 remediation infrastructure contract is missing or invalid."
    }
    if ($Manifest.executionBaseHandling.actualExecutionSha -notmatch '^[0-9a-f]{40}$') {
        Stop-Bootstrap "A2-R1 requires an immutable lowercase execution SHA."
    }
}

function Get-SkeletonText {
    return @'
# VSP AI02 A2 deterministic non-normative bootstrap.
# This file is deliberately nonfunctional and must be substantively implemented by Claude Code.

Set-StrictMode -Version Latest

# INPUT_AND_ENCODING_VALIDATION
# TRUSTED_CONTEXT_AND_PROVENANCE
# FILE_SET_PATH_REPLAY_AND_STALE_BASE
# DECISION_STATE_MACHINE_AND_SANITIZED_EVIDENCE

throw "NOT_IMPLEMENTED"
'@
}

function Get-HarnessText {
    return @'
param(
    [Parameter(Mandatory = $true)][string] $ValidatorPath,
    [Parameter(Mandatory = $true)][string] $PolicyPath,
    [Parameter(Mandatory = $true)][string] $PolicySchemaPath,
    [Parameter(Mandatory = $true)][string] $RequestSchemaPath,
    [Parameter(Mandatory = $true)][string] $DecisionSchemaPath,
    [Parameter(Mandatory = $true)][string] $TrustedContextFixtureRoot,
    [Parameter(Mandatory = $true)][string] $OutputEvidencePath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

foreach ($required in @($ValidatorPath, $PolicyPath, $PolicySchemaPath, $RequestSchemaPath, $DecisionSchemaPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "A2 harness required file missing." }
}
if (-not (Test-Path -LiteralPath $TrustedContextFixtureRoot -PathType Container)) { throw "A2 harness fixture root missing." }

$inputRoot = Join-Path $TrustedContextFixtureRoot "inputs"
$expectedRoot = Join-Path $TrustedContextFixtureRoot "expected"
$caseDirectories = @(Get-ChildItem -LiteralPath $inputRoot -Directory | Sort-Object Name)
if ($caseDirectories.Count -lt 52) { throw "A2 harness requires at least 52 semantic cases." }

$results = [Collections.Generic.List[object]]::new()
foreach ($caseDirectory in $caseDirectories) {
    $expectedPath = Join-Path $expectedRoot ($caseDirectory.Name + ".json")
    $expected = Get-Content -LiteralPath $expectedPath -Raw | ConvertFrom-Json
    $runEvidence = @()
    foreach ($repeat in 1..2) {
        $caseOutput = Join-Path ([IO.Path]::GetTempPath()) ("a2-evidence-" + [Guid]::NewGuid().ToString("N") + ".json")
        try {
            & $ValidatorPath `
                -PolicyPath $PolicyPath `
                -PolicySchemaPath $PolicySchemaPath `
                -RequestSchemaPath $RequestSchemaPath `
                -DecisionSchemaPath $DecisionSchemaPath `
                -TrustedContextFixtureRoot $caseDirectory.FullName `
                -OutputEvidencePath $caseOutput | Out-Null
            if (-not $?) { throw "Validator execution failed." }
            if (-not (Test-Path -LiteralPath $caseOutput -PathType Leaf)) { throw "Validator did not create evidence." }
            if ((Get-Item -LiteralPath $caseOutput).Length -gt 1048576) { throw "Validator evidence exceeds the approved sanitized evidence ceiling." }
            $evidenceBytes = [IO.File]::ReadAllBytes($caseOutput)
            if ($evidenceBytes.Length -ge 3 -and $evidenceBytes[0] -eq 0xef -and $evidenceBytes[1] -eq 0xbb -and $evidenceBytes[2] -eq 0xbf) {
                throw "Validator evidence must be UTF-8 without BOM."
            }
            $raw = Get-Content -LiteralPath $caseOutput -Raw
            if ($raw -match '(?i)rawExceptionText|authorizationHeader|credential|accessToken|machineLocalPath') {
                throw "Validator evidence contains a prohibited field."
            }
            $evidence = $raw | ConvertFrom-Json
            $requiredEvidenceFields = @(
                "validatorSchemaVersion", "policyVersion", "result", "canonicalFailureCategory", "findingCodes", "checkIds", "taskId", "repository",
                "computedRequestSha256", "approvedFileMetadata", "trustedSourceIdentity", "trustedArtifactIdentity", "predecessorTaskPhaseSequence",
                "predecessorDescriptorSha256", "parentAggregateStateDigest", "validatorVersion"
            )
            $actualEvidenceFields = @($evidence.PSObject.Properties.Name)
            if ($actualEvidenceFields.Count -ne $requiredEvidenceFields.Count) { throw "Validator evidence field set is not exact." }
            foreach ($field in $requiredEvidenceFields) {
                if ($actualEvidenceFields -cnotcontains $field) { throw "Validator evidence missing required field $field." }
            }
            if ($evidence.result -ne $expected.result -or $evidence.canonicalFailureCategory -ne $expected.canonicalFailureCategory) {
                throw "Unexpected semantic disposition for case $($caseDirectory.Name)."
            }
            if (-not [string]::IsNullOrWhiteSpace([string]$expected.requiredFindingCode) -and
                @($evidence.findingCodes) -cnotcontains [string]$expected.requiredFindingCode) {
                throw "Required finding code missing for case $($caseDirectory.Name)."
            }
            $runEvidence += (($evidence | ConvertTo-Json -Depth 30 -Compress))
        } finally {
            Remove-Item -LiteralPath $caseOutput -Force -ErrorAction SilentlyContinue
        }
    }
    if ($runEvidence[0] -cne $runEvidence[1]) { throw "Nondeterministic evidence for case $($caseDirectory.Name)." }
    $results.Add([pscustomobject]@{ caseId = $caseDirectory.Name; status = "PASS" })
}

$summary = [ordered]@{
    schemaVersion = "1.0"
    result = "PASS"
    semanticCaseCount = $results.Count
    repeatedExecutionCount = 2
    credentialAvailable = $false
    repositoryMutationAuthorized = $false
    networkRequired = $false
    cases = @($results)
}
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $OutputEvidencePath -Encoding utf8NoBOM
'@
}

function Copy-Hashtable {
    param([Parameter(Mandatory = $true)] $Value)
    return ($Value | ConvertTo-Json -Depth 30 | ConvertFrom-Json -AsHashtable)
}

function New-BaseTrustedContext {
    param([Parameter(Mandatory = $true)][string] $SourceSha)
    return [ordered]@{
        schemaVersion = "1.0"
        repositoryGovernance = [ordered]@{
            repository = "game2082001/VSP"
            taskId = "VSP-AI02-001TI-A2-R1"
            sourceSha = $SourceSha
            approvedFiles = @([ordered]@{ path = $ExpectedOutputPath; mode = "100644"; size = 128; sha256 = ("a" * 64) })
            policyVersion = "vsp-ai02-intake-v1"
            predecessorIdentity = [ordered]@{ taskId = "VSP-AI02-001TI-A1D-VALIDATE"; phase = "A1"; sequence = 1; descriptorSha256 = ("b" * 64); parentAggregateStateDigest = "sha256:" + ("c" * 64) }
        }
        githubTrustedCaller = [ordered]@{ workflowId = "345910582"; runId = "1"; runAttempt = 1; artifactId = "1"; artifactName = "a2-package"; artifactDigest = "sha256:" + ("d" * 64) }
        artifactObservations = [ordered]@{
            packageSha256 = "0" * 64
            manifestSha256 = "1" * 64
            stateSha256 = "2" * 64
            files = @([ordered]@{ path = $ExpectedOutputPath; mode = "100644"; size = 128; sha256 = ("a" * 64) })
        }
        intake = [ordered]@{ consumedIdentities = @(); trustedCurrentBaseObservation = $SourceSha; replayStateKnown = $true }
        testControl = [ordered]@{ injectInternalValidationException = $false; requireNoExternalDependency = $false }
    }
}

function New-ExpectedCase {
    param(
        [Parameter(Mandatory = $true)][string] $Result,
        [Parameter(Mandatory = $true)][string] $Category,
        [string] $Finding = ""
    )
    return [ordered]@{ result = $Result; canonicalFailureCategory = $Category; requiredFindingCode = $Finding }
}

function Initialize-CaseInputs {
    param(
        [Parameter(Mandatory = $true)][string] $CaseId,
        [Parameter(Mandatory = $true)][hashtable] $RequestTemplate,
        [Parameter(Mandatory = $true)][hashtable] $DecisionTemplate,
        [Parameter(Mandatory = $true)][hashtable] $TrustedTemplate,
        [Parameter(Mandatory = $true)][string] $InputRoot,
        [Parameter(Mandatory = $true)][string] $ExpectedRoot,
        [Parameter(Mandatory = $true)][hashtable] $Expected,
        [Parameter(Mandatory = $true)][scriptblock] $Mutation
    )
    $request = Copy-Hashtable $RequestTemplate
    $decision = Copy-Hashtable $DecisionTemplate
    $trusted = Copy-Hashtable $TrustedTemplate
    $rawOverride = $null
    & $Mutation ([ref]$request) ([ref]$decision) ([ref]$trusted) ([ref]$rawOverride)

    $caseRoot = Join-Path $InputRoot $CaseId
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    $requestPath = Join-Path $caseRoot "request.json"
    if ($null -ne $rawOverride) {
        [IO.File]::WriteAllBytes($requestPath, [byte[]]$rawOverride)
    } else {
        Write-DeterministicJson -Path $requestPath -Value $request
    }
    $requestSha = (Get-FileHash -LiteralPath $requestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $decision.requestReference.requestSha256 = $requestSha
    Write-DeterministicJson -Path (Join-Path $caseRoot "decision.json") -Value $decision
    Write-DeterministicJson -Path (Join-Path $caseRoot "trusted-context.json") -Value $trusted
    Write-DeterministicJson -Path (Join-Path $ExpectedRoot ($CaseId + ".json")) -Value $Expected
}

function New-SemanticVectors {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $Workspace,
        [Parameter(Mandatory = $true)][string] $SourceSha
    )
    $inputRoot = Join-Path $Root "inputs"
    $expectedRoot = Join-Path $Root "expected"
    New-Item -ItemType Directory -Force -Path $inputRoot | Out-Null
    New-Item -ItemType Directory -Force -Path $expectedRoot | Out-Null

    $request = Read-Json -Path (Join-Path $Workspace "AI/Orchestrator/Templates/artifact-intake-request.template.json")
    $decision = Read-Json -Path (Join-Path $Workspace "AI/Orchestrator/Templates/artifact-intake-decision.template.json")
    $policy = Read-Json -Path (Join-Path $Workspace "AI/Orchestrator/Policy/artifact-intake-policy.json")
    $request.taskId = $ExpectedTaskId
    $request.expectedSourceSha = $SourceSha
    $request.artifact.artifactName = "a2-package"
    $request.artifact.githubArtifactDigest = "sha256:" + ("d" * 64)
    $request.authorization.approvedFiles = @($ExpectedOutputPath)
    $request.policy = Copy-Hashtable $policy
    $decision.taskId = $ExpectedTaskId
    $decision.repository = "game2082001/VSP"
    $decision.requestReference.artifactName = "a2-package"
    $decision.requestReference.githubArtifactDigest = "sha256:" + ("d" * 64)
    $decision.policy = Copy-Hashtable $policy
    $trusted = New-BaseTrustedContext -SourceSha $SourceSha

    $cases = @(
        @("01-valid-request", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) }),
        @("02-valid-rejected-decision", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) $d.Value.decision="REJECTED"; $d.Value.transportAuthorized=$false; $d.Value.evidence.statusCategory="STALE_BASE"; $d.Value.evidence.failureCategory="STALE_BASE" }),
        @("03-valid-accepted-decision", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) }),
        @("04-replay-empty-first-use", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) }),
        @("05-replay-matching", (New-ExpectedCase "REJECT" "REPLAY_DETECTED" "REQUEST_REPLAY_DETECTED"), { param($r,$d,$t,$raw) $t.Value.intake.consumedIdentities=@((Copy-Hashtable $t.Value.githubTrustedCaller)) }),
        @("06-replay-nonmatching", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) $other=Copy-Hashtable $t.Value.githubTrustedCaller; $other.artifactId="2"; $t.Value.intake.consumedIdentities=@($other) }),
        @("07-replay-unknown", (New-ExpectedCase "REJECT" "REPLAY_DETECTED"), { param($r,$d,$t,$raw) $t.Value.intake.replayStateKnown=$false }),
        @("08-malformed-json", (New-ExpectedCase "REJECT" "STRUCTURE_MISMATCH"), { param($r,$d,$t,$raw) $raw.Value=[Text.Encoding]::UTF8.GetBytes('{') }),
        @("09-invalid-utf8", (New-ExpectedCase "REJECT" "STRUCTURE_MISMATCH"), { param($r,$d,$t,$raw) $raw.Value=[byte[]](0xff,0xfe,0xfd) }),
        @("10-utf8-bom", (New-ExpectedCase "REJECT" "STRUCTURE_MISMATCH"), { param($r,$d,$t,$raw) $body=[Text.Encoding]::UTF8.GetBytes(($r.Value|ConvertTo-Json -Depth 30)); $raw.Value=[byte[]](@(0xef,0xbb,0xbf)+@($body)) }),
        @("11-duplicate-property", (New-ExpectedCase "REJECT" "STRUCTURE_MISMATCH" "JSON_DUPLICATE_PROPERTY"), { param($r,$d,$t,$raw) $json=$r.Value|ConvertTo-Json -Depth 30 -Compress; $raw.Value=[Text.Encoding]::UTF8.GetBytes($json.Insert(1,'"schemaVersion":"1.0",')) }),
        @("12-excessive-depth", (New-ExpectedCase "REJECT" "JSON_DEPTH_EXCEEDED"), { param($r,$d,$t,$raw) $node=[ordered]@{}; $cursor=$node; foreach($i in 1..20){$next=[ordered]@{};$cursor["n"]=$next;$cursor=$next}; $r.Value["tooDeep"]=$node }),
        @("13-oversized-input", (New-ExpectedCase "REJECT" "OVERSIZED_FILE"), { param($r,$d,$t,$raw) $r.Value["oversized"]="x"*300000 }),
        @("14-unknown-field", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED" "REQUEST_SCHEMA_INVALID"), { param($r,$d,$t,$raw) $r.Value["unknown"]=$true }),
        @("15-missing-required", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED" "REQUEST_SCHEMA_INVALID"), { param($r,$d,$t,$raw) $r.Value.Remove("repository") }),
        @("16-wrong-type", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED" "REQUEST_SCHEMA_INVALID"), { param($r,$d,$t,$raw) $r.Value.developer.runAttempt="1" }),
        @("17-unsupported-version", (New-ExpectedCase "REJECT" "UNSUPPORTED_INPUT" "UNSUPPORTED_SCHEMA_VERSION"), { param($r,$d,$t,$raw) $r.Value.schemaVersion="2.0" }),
        @("18-wrong-repository", (New-ExpectedCase "REJECT" "MANIFEST_OR_STATE_INVALID"), { param($r,$d,$t,$raw) $r.Value.repository="other/repo" }),
        @("19-wrong-task", (New-ExpectedCase "REJECT" "MANIFEST_OR_STATE_INVALID"), { param($r,$d,$t,$raw) $r.Value.taskId="VSP-AI02-WRONG" }),
        @("20-wrong-source-sha", (New-ExpectedCase "REJECT" "STALE_BASE" "SOURCE_SHA_MISMATCH"), { param($r,$d,$t,$raw) $r.Value.expectedSourceSha="f"*40 }),
        @("21-wrong-package-sha", (New-ExpectedCase "REJECT" "DIGEST_MISMATCH"), { param($r,$d,$t,$raw) $r.Value.hashBindings.packageSha256="f"*64 }),
        @("22-wrong-manifest-sha", (New-ExpectedCase "REJECT" "DIGEST_MISMATCH"), { param($r,$d,$t,$raw) $r.Value.hashBindings.manifestSha256="f"*64 }),
        @("23-wrong-state-sha", (New-ExpectedCase "REJECT" "DIGEST_MISMATCH"), { param($r,$d,$t,$raw) $r.Value.hashBindings.stateSha256="f"*64 }),
        @("24-mismatched-provenance", (New-ExpectedCase "REJECT" "DIGEST_MISMATCH" "HASH_OR_PROVENANCE_MISMATCH"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.packageSha256="f"*64 }),
        @("25-exact-file-set", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) }),
        @("26-missing-approved-file", (New-ExpectedCase "REJECT" "APPROVED_FILES_MISMATCH"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.files=@() }),
        @("27-unexpected-file", (New-ExpectedCase "REJECT" "APPROVED_FILES_MISMATCH"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.files+=@([ordered]@{path="extra.txt";mode="100644";size=1;sha256="e"*64}) }),
        @("28-duplicate-path", (New-ExpectedCase "REJECT" "PATH_POLICY_VIOLATION"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.files+=@(Copy-Hashtable $t.Value.artifactObservations.files[0]) }),
        @("29-unsafe-path", (New-ExpectedCase "REJECT" "PATH_POLICY_VIOLATION" "DUPLICATE_OR_UNSAFE_PATH"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.files[0].path="../escape" }),
        @("30-wrong-mode", (New-ExpectedCase "REJECT" "MANIFEST_OR_STATE_INVALID"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.files[0].mode="100755" }),
        @("31-wrong-size", (New-ExpectedCase "REJECT" "DIGEST_MISMATCH"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.files[0].size=127 }),
        @("32-size-over-ceiling", (New-ExpectedCase "REJECT" "OVERSIZED_FILE"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.files[0].size=10485761 }),
        @("33-wrong-digest", (New-ExpectedCase "REJECT" "DIGEST_MISMATCH"), { param($r,$d,$t,$raw) $t.Value.artifactObservations.files[0].sha256="f"*64 }),
        @("34-exact-base", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) }),
        @("35-stale-base", (New-ExpectedCase "REJECT" "STALE_BASE" "SOURCE_SHA_MISMATCH"), { param($r,$d,$t,$raw) $t.Value.intake.trustedCurrentBaseObservation="f"*40 }),
        @("36-accepted-state-valid", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) }),
        @("37-accepted-authorization-false", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.transportAuthorized=$false }),
        @("38-accepted-invoked-true", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.transportInvoked=$true }),
        @("39-accepted-failure-not-none", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.evidence.failureCategory="STALE_BASE" }),
        @("40-accepted-status-not-valid", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.evidence.statusCategory="STALE_BASE" }),
        @("41-rejected-authorization-true", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.decision="REJECTED"; $d.Value.evidence.statusCategory="STALE_BASE"; $d.Value.evidence.failureCategory="STALE_BASE" }),
        @("42-rejected-invoked-true", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.decision="REJECTED"; $d.Value.transportAuthorized=$false; $d.Value.transportInvoked=$true; $d.Value.evidence.statusCategory="STALE_BASE"; $d.Value.evidence.failureCategory="STALE_BASE" }),
        @("43-rejected-failure-none", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.decision="REJECTED"; $d.Value.transportAuthorized=$false; $d.Value.evidence.statusCategory="STALE_BASE" }),
        @("44-rejected-status-valid", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.decision="REJECTED"; $d.Value.transportAuthorized=$false; $d.Value.evidence.failureCategory="STALE_BASE" }),
        @("45-deterministic-evidence", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) }),
        @("46-null-vs-missing", (New-ExpectedCase "REJECT" "UNSUPPORTED_INPUT"), { param($r,$d,$t,$raw) $t.Value.intake.consumedIdentities=$null }),
        @("47-valid-empty-arrays", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) $r.Value.hashBindings.trustedInnerHashPins=@(); $t.Value.intake.consumedIdentities=@() }),
        @("48-integer-boundaries", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) $r.Value.developer.runAttempt=2147483647 }),
        @("49-unknown-enum", (New-ExpectedCase "REJECT" "SCHEMA_VALIDATION_FAILED"), { param($r,$d,$t,$raw) $d.Value.decision="UNKNOWN" }),
        @("50-internal-exception-fail-closed", (New-ExpectedCase "REJECT" "UNSUPPORTED_INPUT" "INTERNAL_VALIDATION_ERROR"), { param($r,$d,$t,$raw) $t.Value.testControl.injectInternalValidationException=$true }),
        @("51-no-raw-exception-leak", (New-ExpectedCase "REJECT" "UNSUPPORTED_INPUT" "INTERNAL_VALIDATION_ERROR"), { param($r,$d,$t,$raw) $t.Value.testControl.injectInternalValidationException=$true }),
        @("52-no-external-dependency", (New-ExpectedCase "PASS" "NONE"), { param($r,$d,$t,$raw) $t.Value.testControl.requireNoExternalDependency=$true })
    )

    foreach ($case in $cases) {
        Initialize-CaseInputs -CaseId $case[0] -RequestTemplate $request -DecisionTemplate $decision -TrustedTemplate $trusted -InputRoot $inputRoot -ExpectedRoot $expectedRoot -Expected $case[1] -Mutation $case[2]
    }
    return $cases.Count
}

function Get-TreeDigestRecords {
    param([Parameter(Mandatory = $true)][string] $Root)
    return @(Get-ChildItem -LiteralPath $Root -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            path = [IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/')
            size = $_.Length
            sha256 = Get-Sha256 -Path $_.FullName
        }
    })
}

function Assert-TreeDigestRecords {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)] $Expected
    )
    $actual = @(Get-TreeDigestRecords -Root $Root)
    $expectedItems = @($Expected)
    if ($actual.Count -ne $expectedItems.Count) { Stop-Bootstrap "Temporary test-vector file set changed." }
    for ($i = 0; $i -lt $actual.Count; $i++) {
        if ($actual[$i].path -cne $expectedItems[$i].path -or $actual[$i].size -ne $expectedItems[$i].size -or $actual[$i].sha256 -cne $expectedItems[$i].sha256) {
            Stop-Bootstrap "Temporary test-vector integrity mismatch."
        }
    }
}

function Assert-PowerShellAndProhibitions {
    param([Parameter(Mandatory = $true)][string] $Path)
    $tokens = $null
    $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$errors)
    if (@($errors).Count -ne 0) { Stop-Bootstrap "A2 production file is not valid PowerShell." }

    $prohibitedCommands = @('git','gh','Invoke-WebRequest','Invoke-RestMethod','Test-NetConnection','Resolve-DnsName','Invoke-Command','Enter-PSSession','New-PSSession','Start-Process','Start-Job','Invoke-Expression','curl','wget','ssh','Get-Secret','pwsh','powershell','cmd','bash','sh','python','python3','node')
    foreach ($command in @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] }, $true))) {
        $name = $command.GetCommandName()
        if ([string]::IsNullOrWhiteSpace($name)) {
            Stop-Bootstrap "Dynamic command invocation is prohibited in the A2 production file."
        }
        if ($prohibitedCommands -contains $name) {
            Stop-Bootstrap "Prohibited command detected in A2 production file: $name"
        }
        if ($command.Extent.Text -match '(?i)repository.?transport|git\s+(push|commit|branch)|gh\s+|api\.github\.com|System\.Net\.|HttpClient|WebClient|GetEnvironmentVariable|(^|\W)env:') {
            Stop-Bootstrap "Prohibited repository, Transport, or GitHub operation detected."
        }
    }
    foreach ($typeNode in @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.TypeExpressionAst] }, $true))) {
        if ($typeNode.TypeName.FullName -match '(?i)^System\.Net\.|HttpClient|WebClient|^System\.Diagnostics\.Process|^Diagnostics\.Process') {
            Stop-Bootstrap "Prohibited network or process type detected in A2 production file."
        }
    }
    foreach ($variable in @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.VariableExpressionAst] }, $true))) {
        if ($variable.VariablePath.UserPath -match '(?i)^env:|secret|credential|token|private.?key') {
            Stop-Bootstrap "Prohibited environment or credential access detected in A2 production file."
        }
    }
}

function Invoke-Prepare {
    param([Parameter(Mandatory = $true)] $Manifest)
    $workspaceFull = [IO.Path]::GetFullPath($WorkspacePath).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $runtimeFull = [IO.Path]::GetFullPath($RuntimeDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ($runtimeFull -eq [IO.Path]::GetPathRoot($runtimeFull) -or
        $runtimeFull.StartsWith($workspaceFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-Bootstrap "RuntimeDirectory must be a dedicated temporary directory outside repository ownership."
    }
    if (Test-Path -LiteralPath $RuntimeDirectory) {
        Remove-Item -LiteralPath $RuntimeDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $RuntimeDirectory | Out-Null
    $outputPath = Resolve-ContainedPath -Root $WorkspacePath -RelativePath $ExpectedOutputPath
    if (Test-Path -LiteralPath $outputPath) { Stop-Bootstrap "A2 production path must be absent before deterministic skeleton creation." }

    Write-DeterministicText -Path $outputPath -Text (Get-SkeletonText)
    $skeletonHash = Get-Sha256 -Path $outputPath
    $harnessPath = Join-Path $RuntimeDirectory "a2-focused-harness.ps1"
    Write-DeterministicText -Path $harnessPath -Text (Get-HarnessText)
    $fixtureRoot = Join-Path $RuntimeDirectory "fixtures"
    $caseCount = New-SemanticVectors -Root $fixtureRoot -Workspace $WorkspacePath -SourceSha ([string]$Manifest.executionBaseHandling.actualExecutionSha)
    if ($caseCount -lt 52) { Stop-Bootstrap "Focused semantic matrix contains fewer than 52 cases." }
    Assert-ExactChangedFile -Root $WorkspacePath

    $state = [ordered]@{
        schemaVersion = "1.0"
        taskId = $ExpectedTaskId
        outputPath = $ExpectedOutputPath
        sentinel = $Sentinel
        skeletonByteSize = (Get-Item -LiteralPath $outputPath).Length
        skeletonSha256 = $skeletonHash
        harnessFile = "a2-focused-harness.ps1"
        harnessSha256 = Get-Sha256 -Path $harnessPath
        semanticCaseCount = $caseCount
        fixtureRecords = @(Get-TreeDigestRecords -Root $fixtureRoot)
        predecessorBaselineSha256 = Get-Sha256 -Path $PredecessorBaselinePath
        a2AttemptConsumed = $false
    }
    Write-DeterministicJson -Path (Join-Path $RuntimeDirectory "bootstrap-state.json") -Value $state
    return $state
}

function Invoke-ValidateCompletion {
    param([Parameter(Mandatory = $true)] $Manifest)
    $state = Read-Json -Path (Join-Path $RuntimeDirectory "bootstrap-state.json")
    if ($state.taskId -ne $ExpectedTaskId -or $state.outputPath -cne $ExpectedOutputPath -or $state.sentinel -cne $Sentinel) {
        Stop-Bootstrap "Bootstrap state identity mismatch."
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedBaselineSha256) -and $state.predecessorBaselineSha256 -cne $ExpectedBaselineSha256.ToLowerInvariant()) {
        Stop-Bootstrap "Recorded predecessor baseline hash does not match the workflow binding."
    }
    if ((Get-Sha256 -Path $PredecessorBaselinePath) -cne $state.predecessorBaselineSha256) {
        Stop-Bootstrap "Predecessor baseline changed after bootstrap."
    }

    $harnessPath = Join-Path $RuntimeDirectory ([string]$state.harnessFile)
    if ((Get-Sha256 -Path $harnessPath) -cne $state.harnessSha256) { Stop-Bootstrap "Temporary harness integrity mismatch." }
    $fixtureRoot = Join-Path $RuntimeDirectory "fixtures"
    Assert-TreeDigestRecords -Root $fixtureRoot -Expected $state.fixtureRecords

    $outputPath = Resolve-ContainedPath -Root $WorkspacePath -RelativePath $ExpectedOutputPath
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf) -or (Get-Item -LiteralPath $outputPath).Length -le 0) {
        Stop-Bootstrap "A2 production file is missing or empty."
    }
    $finalHash = Get-Sha256 -Path $outputPath
    if ($finalHash -ceq $state.skeletonSha256) { Stop-Bootstrap "Claude did not change the deterministic skeleton." }
    $text = Get-Content -LiteralPath $outputPath -Raw
    if ($text -match '(?i)NOT_IMPLEMENTED|\bTODO\b') { Stop-Bootstrap "A2 production file contains an unfinished skeleton sentinel." }
    Assert-PowerShellAndProhibitions -Path $outputPath
    Assert-ExactChangedFile -Root $WorkspacePath

    $summaryPath = Join-Path $RuntimeDirectory "semantic-harness-result.json"
    & $harnessPath `
        -ValidatorPath $outputPath `
        -PolicyPath (Join-Path $WorkspacePath "AI/Orchestrator/Policy/artifact-intake-policy.json") `
        -PolicySchemaPath (Join-Path $WorkspacePath "AI/Orchestrator/Policy/artifact-intake-policy.schema.json") `
        -RequestSchemaPath (Join-Path $WorkspacePath "AI/Orchestrator/Templates/artifact-intake-request.schema.json") `
        -DecisionSchemaPath (Join-Path $WorkspacePath "AI/Orchestrator/Templates/artifact-intake-decision.schema.json") `
        -TrustedContextFixtureRoot $fixtureRoot `
        -OutputEvidencePath $summaryPath
    if ($LASTEXITCODE -ne 0) { Stop-Bootstrap "Focused semantic harness returned a nonzero exit code." }
    $summary = Read-Json -Path $summaryPath
    if ($summary.result -ne "PASS" -or [int]$summary.semanticCaseCount -lt 52 -or [int]$summary.repeatedExecutionCount -ne 2) {
        Stop-Bootstrap "Focused semantic harness did not satisfy the completion contract."
    }
    if ((Get-Sha256 -Path $harnessPath) -cne $state.harnessSha256) { Stop-Bootstrap "Temporary harness changed during semantic execution." }
    Assert-TreeDigestRecords -Root $fixtureRoot -Expected $state.fixtureRecords
    if ((Get-Sha256 -Path $PredecessorBaselinePath) -cne $state.predecessorBaselineSha256) { Stop-Bootstrap "Predecessor baseline changed during semantic execution." }
    if ((Get-Sha256 -Path $outputPath) -cne $finalHash) { Stop-Bootstrap "A2 production file changed during semantic execution." }
    Assert-ExactChangedFile -Root $WorkspacePath
    return [ordered]@{
        taskId = $ExpectedTaskId
        result = "PASS"
        outputPath = $ExpectedOutputPath
        skeletonSha256 = $state.skeletonSha256
        finalSha256 = $finalHash
        harnessSha256 = $state.harnessSha256
        semanticCaseCount = [int]$summary.semanticCaseCount
        deterministicRepeatedExecution = $true
        repositoryWriteCredentialAvailableToDeveloper = $false
        a2AttemptConsumptionBoundary = "Run Claude Code Primary Developer begins"
    }
}

$manifest = Read-Json -Path $ManifestPath
Assert-R1Manifest -Manifest $manifest

switch ($Mode) {
    "Prepare" { Invoke-Prepare -Manifest $manifest | ConvertTo-Json -Depth 30 }
    "ValidateCompletion" { Invoke-ValidateCompletion -Manifest $manifest | ConvertTo-Json -Depth 20 }
}
