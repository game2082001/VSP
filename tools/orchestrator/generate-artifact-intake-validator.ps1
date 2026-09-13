[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $OutputRelativePath = "tools/orchestrator/artifact-intake-contract.ps1",

    [string] $PolicyPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Policy\artifact-intake-policy.json"),
    [string] $PolicySchemaPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Policy\artifact-intake-policy.schema.json"),
    [string] $RequestSchemaPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Templates\artifact-intake-request.schema.json"),
    [string] $RequestTemplatePath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Templates\artifact-intake-request.template.json"),
    [string] $DecisionSchemaPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Templates\artifact-intake-decision.schema.json"),
    [string] $DecisionTemplatePath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Templates\artifact-intake-decision.template.json")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$GeneratorVersion = "vsp-ai02-a2-gen1-validator-generator/1.0"
$RequiredPolicyProjectionSections = @("repositoryPath", "limits", "replay", "staleBase", "credentialInvariants", "transport")

function Stop-Generator {
    param([Parameter(Mandatory = $true)][string] $Message)
    throw "AI02 deterministic validator generation failed: $Message"
}

function Assert-NoDuplicateJsonProperties {
    param([System.Text.Json.JsonElement] $Element, [string] $Path = '$')
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $names.Add($property.Name)) { Stop-Generator "Duplicate JSON property at ${Path}: $($property.Name)" }
            Assert-NoDuplicateJsonProperties -Element $property.Value -Path "$Path.$($property.Name)"
        }
        return
    }
    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        $index = 0
        foreach ($item in $Element.EnumerateArray()) {
            Assert-NoDuplicateJsonProperties -Element $item -Path "$Path[$index]"
            $index++
        }
    }
}

function Read-StrictJson {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)
    $resolved = (Resolve-Path -LiteralPath $LiteralPath -ErrorAction Stop).Path
    $bytes = [System.IO.File]::ReadAllBytes($resolved)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xef -and $bytes[1] -eq 0xbb -and $bytes[2] -eq 0xbf) {
        Stop-Generator "UTF-8 BOM is not allowed: $LiteralPath"
    }
    try {
        $text = [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    } catch {
        Stop-Generator "Input is not strict UTF-8: $LiteralPath"
    }
    $options = [System.Text.Json.JsonDocumentOptions]::new()
    $options.AllowTrailingCommas = $false
    $options.CommentHandling = [System.Text.Json.JsonCommentHandling]::Disallow
    try {
        $document = [System.Text.Json.JsonDocument]::Parse($text, $options)
        try { Assert-NoDuplicateJsonProperties -Element $document.RootElement } finally { $document.Dispose() }
        $value = $text | ConvertFrom-Json -AsHashtable -Depth 100
    } catch {
        Stop-Generator "Malformed JSON: $LiteralPath"
    }
    return [pscustomobject]@{ path = $resolved; text = $text; value = $value; sha256 = (Get-BytesSha256 $bytes); bytes = $bytes.Length }
}

function Get-BytesSha256 {
    param([Parameter(Mandatory = $true)][byte[]] $Bytes)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($Bytes) | ForEach-Object { $_.ToString("x2") }) -join "") } finally { $sha.Dispose() }
}

function ConvertTo-CanonicalJson {
    param([Parameter(Mandatory = $true)] $Value)
    return (($Value | ConvertTo-Json -Depth 100 -Compress) + "`n").Replace("`r`n", "`n").Replace("`r", "`n")
}

function Test-JsonEqual {
    param($Left, $Right)
    return ((ConvertTo-CanonicalJson $Left) -ceq (ConvertTo-CanonicalJson $Right))
}

function Get-PolicyProjection {
    param([Parameter(Mandatory = $true)][hashtable] $Policy)
    $projection = [ordered]@{}
    foreach ($section in $RequiredPolicyProjectionSections) {
        if (-not $Policy.Contains($section)) { Stop-Generator "Policy is missing required projection section: $section" }
        $projection[$section] = $Policy[$section]
    }
    return $projection
}

function Assert-ClosedConstPolicySchema {
    param([Parameter(Mandatory = $true)][hashtable] $Policy, [Parameter(Mandatory = $true)][hashtable] $Schema)
    if ($Schema['$schema'] -cne "https://json-schema.org/draft/2020-12/schema") { Stop-Generator "Unsupported policy schema dialect." }
    if ($Schema['$id'] -cne "https://github.com/game2082001/VSP/AI/Orchestrator/Policy/artifact-intake-policy.schema.json") { Stop-Generator "Unsupported policy schema identifier." }
    if ($Schema.type -cne "object" -or $Schema.additionalProperties -ne $false) { Stop-Generator "Policy schema must be a closed object." }
    foreach ($name in @($Schema.required)) {
        if (-not $Policy.Contains($name)) { Stop-Generator "Policy is missing required section: $name" }
    }
    foreach ($name in @($Policy.Keys)) {
        if (-not $Schema.properties.Contains($name)) { Stop-Generator "Policy contains unknown top-level field: $name" }
    }
    foreach ($name in @($Schema.properties.Keys)) {
        $rule = $Schema.properties[$name]
        if (-not $rule.Contains("const")) { Stop-Generator "Policy schema field is not const-closed: $name" }
        if (-not (Test-JsonEqual $Policy[$name] $rule.const)) { Stop-Generator "Policy does not match closed schema const: $name" }
    }
}

function Assert-SchemaAndTemplateConsistency {
    param(
        [Parameter(Mandatory = $true)][hashtable] $Policy,
        [Parameter(Mandatory = $true)][hashtable] $Projection,
        [Parameter(Mandatory = $true)][hashtable] $RequestSchema,
        [Parameter(Mandatory = $true)][hashtable] $RequestTemplate,
        [Parameter(Mandatory = $true)][hashtable] $DecisionSchema,
        [Parameter(Mandatory = $true)][hashtable] $DecisionTemplate
    )
    foreach ($schema in @($RequestSchema, $DecisionSchema)) {
        if ($schema['$schema'] -cne "https://json-schema.org/draft/2020-12/schema") { Stop-Generator "Unsupported schema dialect." }
        if ($schema.type -cne "object" -or $schema.additionalProperties -ne $false) { Stop-Generator "Request/decision schemas must be closed objects." }
    }
    if ($RequestSchema['$id'] -cne "https://github.com/game2082001/VSP/AI/Orchestrator/Templates/artifact-intake-request.schema.json") { Stop-Generator "Unsupported request schema identifier." }
    if ($DecisionSchema['$id'] -cne "https://github.com/game2082001/VSP/AI/Orchestrator/Templates/artifact-intake-decision.schema.json") { Stop-Generator "Unsupported decision schema identifier." }
    foreach ($candidate in @(
        @{ name = "Request schema"; value = $RequestSchema.properties.policy.const },
        @{ name = "Decision schema"; value = $DecisionSchema.properties.policy.const },
        @{ name = "Request template"; value = $RequestTemplate.policy },
        @{ name = "Decision template"; value = $DecisionTemplate.policy }
    )) {
        $actualKeys = @($candidate.value.Keys)
        if ($actualKeys.Count -ne $RequiredPolicyProjectionSections.Count) { Stop-Generator "$($candidate.name) policy projection does not have exactly six sections." }
        foreach ($section in $RequiredPolicyProjectionSections) {
            if ($actualKeys -cnotcontains $section) { Stop-Generator "$($candidate.name) policy projection is missing section: $section" }
            if (-not (Test-JsonEqual $candidate.value[$section] $Projection[$section])) { Stop-Generator "$($candidate.name) policy projection section is not canonical: $section" }
        }
    }
    if ($RequestSchema.properties.schemaVersion.const -cne $Policy.schemaVersion -or $DecisionSchema.properties.schemaVersion.const -cne $Policy.schemaVersion) { Stop-Generator "Schema version mismatch." }
    if ($RequestSchema.properties.policyVersion.const -cne $Policy.identity.policyVersion -or $DecisionSchema.properties.policyVersion.const -cne $Policy.identity.policyVersion) { Stop-Generator "Policy version mismatch." }
    if (-not (Test-JsonEqual $DecisionSchema.properties.evidence.properties.failureCategory.enum $Policy.failureCategories)) { Stop-Generator "Decision failure categories do not come from policy." }
}

function ConvertTo-PowerShellLiteral {
    param([Parameter(Mandatory = $true)][string] $Text)
    return "@'`n$Text`n'@"
}

function Write-Utf8NoBomLf {
    param([Parameter(Mandatory = $true)][string] $LiteralPath, [Parameter(Mandatory = $true)][string] $Text)
    $normalized = $Text.Replace("`r`n", "`n").Replace("`r", "`n")
    [System.IO.File]::WriteAllText($LiteralPath, $normalized, [System.Text.UTF8Encoding]::new($false))
}

function Get-GitBlobId {
    param([Parameter(Mandatory = $true)][string] $Path)
    try {
        $relative = [System.IO.Path]::GetRelativePath((Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path, $Path).Replace("\", "/")
        $line = @(git -C (Join-Path $PSScriptRoot "..\..") ls-files -s -- $relative 2>$null)
        if ($LASTEXITCODE -ne 0 -or $line.Count -ne 1) { return "" }
        if ($line[0] -match '^[0-9]{6}\s+([0-9a-f]{40})\s+') { return $Matches[1] }
        return ""
    } catch { return "" }
}

$inputs = [ordered]@{
    policy = Read-StrictJson $PolicyPath
    policySchema = Read-StrictJson $PolicySchemaPath
    requestSchema = Read-StrictJson $RequestSchemaPath
    requestTemplate = Read-StrictJson $RequestTemplatePath
    decisionSchema = Read-StrictJson $DecisionSchemaPath
    decisionTemplate = Read-StrictJson $DecisionTemplatePath
}

Assert-ClosedConstPolicySchema -Policy $inputs.policy.value -Schema $inputs.policySchema.value
$projection = Get-PolicyProjection -Policy $inputs.policy.value
Assert-SchemaAndTemplateConsistency -Policy $inputs.policy.value -Projection $projection -RequestSchema $inputs.requestSchema.value -RequestTemplate $inputs.requestTemplate.value -DecisionSchema $inputs.decisionSchema.value -DecisionTemplate $inputs.decisionTemplate.value

if ($OutputRelativePath -cne "tools/orchestrator/artifact-intake-contract.ps1") {
    Stop-Generator "The only authorized generated production path is tools/orchestrator/artifact-intake-contract.ps1."
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputPath = Join-Path $outputRoot ($OutputRelativePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
$outputParent = Split-Path -Parent $outputPath
$tempPath = Join-Path $outputParent ("." + [System.IO.Path]::GetFileName($outputPath) + ".tmp")
if (Test-Path -LiteralPath $tempPath) { Remove-Item -LiteralPath $tempPath -Force }
New-Item -ItemType Directory -Force -Path $outputParent | Out-Null

$projectionJson = ConvertTo-CanonicalJson $projection
$policyJson = ConvertTo-CanonicalJson $inputs.policy.value
$runtime = @"
[CmdletBinding()]
param(
    [Parameter(Mandatory = `$true)][string] `$PolicyPath,
    [Parameter(Mandatory = `$true)][string] `$PolicySchemaPath,
    [Parameter(Mandatory = `$true)][string] `$RequestSchemaPath,
    [Parameter(Mandatory = `$true)][string] `$DecisionSchemaPath,
    [Parameter(Mandatory = `$true)][string] `$TrustedContextFixtureRoot,
    [Parameter(Mandatory = `$true)][string] `$OutputEvidencePath
)

Set-StrictMode -Version Latest
`$ErrorActionPreference = "Stop"

`$Script:ValidatorVersion = "$GeneratorVersion"
`$Script:PolicyJson = $(ConvertTo-PowerShellLiteral $policyJson)
`$Script:PolicyProjectionJson = $(ConvertTo-PowerShellLiteral $projectionJson)

function Stop-Validation {
    param([string] `$Category, [string] `$Finding)
    [pscustomobject]@{ result = "REJECTED"; canonicalFailureCategory = `$Category; findingCodes = @(`$Finding) }
}

function Get-BytesSha256 {
    param([byte[]] `$Bytes)
    `$sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ((`$sha.ComputeHash(`$Bytes) | ForEach-Object { `$_.ToString("x2") }) -join "") } finally { `$sha.Dispose() }
}

function Assert-NoDuplicateJsonProperties {
    param([System.Text.Json.JsonElement] `$Element, [string] `$Path = '$')
    if (`$Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        `$names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach (`$property in `$Element.EnumerateObject()) {
            if (-not `$names.Add(`$property.Name)) { throw "Duplicate JSON property at `$Path" }
            Assert-NoDuplicateJsonProperties -Element `$property.Value -Path "`$Path.`$(`$property.Name)"
        }
    } elseif (`$Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        `$index = 0
        foreach (`$item in `$Element.EnumerateArray()) {
            Assert-NoDuplicateJsonProperties -Element `$item -Path "`$Path[`$index]"
            `$index++
        }
    }
}

function Read-StrictJson {
    param([string] `$Path)
    if (-not (Test-Path -LiteralPath `$Path -PathType Leaf)) { throw "JSON file missing." }
    `$bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath `$Path).Path)
    if (`$bytes.Length -ge 3 -and `$bytes[0] -eq 0xef -and `$bytes[1] -eq 0xbb -and `$bytes[2] -eq 0xbf) { throw "BOM rejected." }
    `$text = [Text.UTF8Encoding]::new(`$false, `$true).GetString(`$bytes)
    `$options = [Text.Json.JsonDocumentOptions]::new()
    `$options.AllowTrailingCommas = `$false
    `$options.CommentHandling = [Text.Json.JsonCommentHandling]::Disallow
    `$document = [Text.Json.JsonDocument]::Parse(`$text, `$options)
    try { Assert-NoDuplicateJsonProperties -Element `$document.RootElement } finally { `$document.Dispose() }
    return [pscustomobject]@{ text = `$text; value = (`$text | ConvertFrom-Json -AsHashtable -Depth 100); sha256 = Get-BytesSha256 `$bytes }
}

function ConvertTo-CanonicalJson {
    param(`$Value)
    return ((`$Value | ConvertTo-Json -Depth 100 -Compress) + "`n").Replace("`r`n", "`n").Replace("`r", "`n")
}

function Test-JsonEqual { param(`$Left, `$Right) return ((ConvertTo-CanonicalJson `$Left) -ceq (ConvertTo-CanonicalJson `$Right)) }

function Test-RepoPath {
    param([AllowNull()][object] `$Path)
    if (`$null -eq `$Path -or -not (`$Path -is [string]) -or [string]::IsNullOrWhiteSpace(`$Path)) { return `$false }
    `$policy = (ConvertFrom-Json `$Script:PolicyJson -AsHashtable -Depth 100).repositoryPath
    if (`$Path.Length -lt [int]`$policy.minimumLength -or `$Path.Length -gt [int]`$policy.maximumLength) { return `$false }
    foreach (`$ch in `$Path.ToCharArray()) { `$code = [int][char]`$ch; if (`$code -lt 32 -or `$code -gt 126) { return `$false } }
    if (`$Path.StartsWith("/") -or `$Path.Contains("\") -or `$Path.Contains(":") -or `$Path.Contains("//") -or `$Path.EndsWith("/")) { return `$false }
    foreach (`$segment in `$Path.Split('/')) {
        if ([string]::IsNullOrWhiteSpace(`$segment) -or `$segment -eq "." -or `$segment -eq ".." -or `$segment.Length -gt [int]`$policy.maximumSegmentLength) { return `$false }
    }
    return `$true
}

function Get-FailureEvidence {
    param([string] `$Category, [string] `$Finding, `$Request, `$Decision, `$Trusted)
    return [ordered]@{
        validatorSchemaVersion = "1.0"
        policyVersion = "vsp-ai02-intake-v1"
        result = "REJECTED"
        canonicalFailureCategory = `$Category
        findingCodes = @(`$Finding)
        checkIds = @("fail-closed")
        taskId = if (`$Request -and `$Request.Contains("taskId")) { [string]`$Request.taskId } else { "" }
        repository = if (`$Request -and `$Request.Contains("repository")) { [string]`$Request.repository } else { "" }
        computedRequestSha256 = ""
        approvedFileMetadata = @()
        trustedSourceIdentity = [ordered]@{ sourceSha = if (`$Trusted -and `$Trusted.Contains("sourceSha")) { [string]`$Trusted.sourceSha } else { "" } }
        trustedArtifactIdentity = [ordered]@{ artifactId = "" }
        predecessorTaskPhaseSequence = [ordered]@{ taskId = ""; phase = ""; sequence = 0 }
        predecessorDescriptorSha256 = ""
        parentAggregateStateDigest = ""
        validatorVersion = `$Script:ValidatorVersion
    }
}

try {
    `$policy = ConvertFrom-Json `$Script:PolicyJson -AsHashtable -Depth 100
    `$projection = ConvertFrom-Json `$Script:PolicyProjectionJson -AsHashtable -Depth 100
    `$requestDoc = Read-StrictJson (Join-Path `$TrustedContextFixtureRoot "request.json")
    `$decisionDoc = Read-StrictJson (Join-Path `$TrustedContextFixtureRoot "decision.json")
    `$trustedDoc = Read-StrictJson (Join-Path `$TrustedContextFixtureRoot "trusted-context.json")
    `$request = `$requestDoc.value
    `$decision = `$decisionDoc.value
    `$trusted = `$trustedDoc.value
    `$result = `$null
    if (-not (Test-JsonEqual `$request.policy `$projection) -or -not (Test-JsonEqual `$decision.policy `$projection)) { `$result = Stop-Validation "SCHEMA_VALIDATION_FAILED" "POLICY_PROJECTION_MISMATCH" }
    elseif (`$request.taskId -cne "VSP-AI02-001TI-A2-VAL1" -or `$decision.taskId -cne `$request.taskId) { `$result = Stop-Validation "MANIFEST_OR_STATE_INVALID" "TASK_ID_MISMATCH" }
    elseif (`$request.authorization.manifestPath -cne "AI/Orchestrator/Manifests/VSP-AI02-001TI-A2-VAL1.manifest.json" -or `$request.authorization.statePath -cne "AI/Orchestrator/State/VSP-AI02-001TI-A2-VAL1.state.json") { `$result = Stop-Validation "MANIFEST_OR_STATE_INVALID" "GOVERNANCE_PATH_MISMATCH" }
    elseif (`$request.repository -cne `$policy.identity.repository.exactValue -or `$decision.repository -cne `$policy.identity.repository.exactValue) { `$result = Stop-Validation "MANIFEST_OR_STATE_INVALID" "REPOSITORY_MISMATCH" }
    elseif (`$trusted.sourceSha -cne `$request.expectedSourceSha) { `$result = Stop-Validation "STALE_BASE" "AUTHORIZED_SOURCE_SHA_MISMATCH" }
    elseif (`$requestDoc.sha256 -cne `$decision.requestReference.requestSha256) { `$result = Stop-Validation "DIGEST_MISMATCH" "REQUEST_REFERENCE_SHA_MISMATCH" }
    elseif (`$request.artifact.artifactId -cne `$decision.requestReference.artifactId -or `$request.artifact.githubArtifactDigest -cne `$decision.requestReference.githubArtifactDigest) { `$result = Stop-Validation "DIGEST_MISMATCH" "ARTIFACT_BINDING_MISMATCH" }
    elseif (`$request.hashBindings.packageSha256 -cne `$decision.requestReference.packageSha256 -or `$request.hashBindings.manifestSha256 -cne `$decision.requestReference.manifestSha256 -or `$request.hashBindings.stateSha256 -cne `$decision.requestReference.stateSha256) { `$result = Stop-Validation "DIGEST_MISMATCH" "PACKAGE_BINDING_MISMATCH" }
    elseif (@(`$trusted.consumedIdentities | Where-Object { `$_ -ceq `$requestDoc.sha256 }).Count -gt 0) { `$result = Stop-Validation "REPLAY_DETECTED" "REPLAY_IDENTITY_MATCH" }
    elseif (@(`$request.authorization.approvedFiles).Count -gt [int]`$policy.limits.maxChangedFiles) { `$result = Stop-Validation "TOO_MANY_CHANGED_FILES" "APPROVED_FILE_LIMIT_EXCEEDED" }
    elseif (@(`$request.authorization.approvedFiles | Where-Object { -not (Test-RepoPath `$_) }).Count -gt 0 -or -not (Test-RepoPath `$request.authorization.manifestPath) -or -not (Test-RepoPath `$request.authorization.statePath)) { `$result = Stop-Validation "PATH_POLICY_VIOLATION" "REPOSITORY_PATH_POLICY_VIOLATION" }
    elseif (-not (Test-JsonEqual @(`$request.authorization.approvedFiles | Sort-Object) @(`$trusted.actualChangedFiles | Sort-Object))) { `$result = Stop-Validation "APPROVED_FILES_MISMATCH" "CHANGED_FILE_SET_MISMATCH" }
    elseif (@(`$trusted.fileMetadata | Where-Object { `$_.mode -cne "100644" -or [int64]`$_.size -le 0 -or [int64]`$_.size -gt [int64]`$policy.limits.maxPerFileUncompressedBytes }).Count -gt 0) { `$result = Stop-Validation "OVERSIZED_FILE" "FILE_METADATA_INVALID" }
    elseif (`$trusted.predecessor.taskId -cne "VSP-AI02-001TI-A1D-VALIDATE" -or `$trusted.predecessor.phase -cne "A1" -or [int]`$trusted.predecessor.sequence -ne 1) { `$result = Stop-Validation "MANIFEST_OR_STATE_INVALID" "PREDECESSOR_BINDING_MISMATCH" }
    elseif (`$trusted.predecessor.descriptorSha256 -cne "c1595130a63ffb3eaba0facbb4c0f1e73c5dbf17db098ea5052ca23ff021a253" -or `$trusted.predecessor.parentAggregateStateDigest -cne "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1") { `$result = Stop-Validation "MANIFEST_OR_STATE_INVALID" "A1_LINEAGE_MISMATCH" }
    elseif (`$decision.decision -cne "ACCEPTED_FOR_TRANSPORT" -or `$decision.transportAuthorized -ne `$true -or `$decision.transportInvoked -ne `$false -or `$decision.evidence.statusCategory -cne "VALID_PACKAGE" -or `$decision.evidence.failureCategory -cne "NONE") { `$result = Stop-Validation "STRUCTURE_MISMATCH" "DECISION_STATE_MISMATCH" }
    else {
        `$result = [ordered]@{
            validatorSchemaVersion = "1.0"
            policyVersion = "vsp-ai02-intake-v1"
            result = "ACCEPTED_FOR_TRANSPORT"
            canonicalFailureCategory = "NONE"
            findingCodes = @()
            checkIds = @("policy-projection", "exact-base", "replay", "changed-files", "a1-lineage", "decision-state")
            taskId = [string]`$request.taskId
            repository = [string]`$request.repository
            computedRequestSha256 = `$requestDoc.sha256
            approvedFileMetadata = @(`$trusted.fileMetadata)
            trustedSourceIdentity = [ordered]@{ sourceSha = [string]`$trusted.sourceSha }
            trustedArtifactIdentity = [ordered]@{ artifactId = [string]`$request.artifact.artifactId; githubArtifactDigest = [string]`$request.artifact.githubArtifactDigest }
            predecessorTaskPhaseSequence = [ordered]@{ taskId = [string]`$trusted.predecessor.taskId; phase = [string]`$trusted.predecessor.phase; sequence = [int]`$trusted.predecessor.sequence }
            predecessorDescriptorSha256 = [string]`$trusted.predecessor.descriptorSha256
            parentAggregateStateDigest = [string]`$trusted.predecessor.parentAggregateStateDigest
            validatorVersion = `$Script:ValidatorVersion
        }
    }
} catch {
    `$result = Get-FailureEvidence "UNSUPPORTED_INPUT" "FAIL_CLOSED_EXCEPTION" `$request `$decision `$trusted
}

`$json = (`$result | ConvertTo-Json -Depth 20).Replace("`r`n", "`n").Replace("`r", "`n") + "`n"
[IO.File]::WriteAllText(`$OutputEvidencePath, `$json, [Text.UTF8Encoding]::new(`$false))
"@

try {
    Write-Utf8NoBomLf -LiteralPath $tempPath -Text $runtime
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($tempPath, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -ne 0) { Stop-Generator "Generated PowerShell does not parse." }
    Move-Item -LiteralPath $tempPath -Destination $outputPath -Force
} catch {
    Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $outputPath -Force -ErrorAction SilentlyContinue
    throw
}

$outputBytes = [System.IO.File]::ReadAllBytes($outputPath)
$evidenceInputs = @()
foreach ($key in $inputs.Keys) {
    $input = $inputs[$key]
    $evidenceInputs += [ordered]@{
        role = $key
        path = [System.IO.Path]::GetFileName($input.path)
        sha256 = $input.sha256
        byteSize = $input.bytes
        gitBlobId = Get-GitBlobId $input.path
    }
}

[pscustomobject]@{
    status = "GENERATED"
    generatorVersion = $GeneratorVersion
    outputRelativePath = $OutputRelativePath
    outputPath = $outputPath
    generatedSha256 = Get-BytesSha256 $outputBytes
    generatedByteSize = $outputBytes.Length
    deterministicInputs = $evidenceInputs
    policyProjectionSections = $RequiredPolicyProjectionSections
    repositoryWriteCredentialAvailableToDeveloper = $false
    repositoryTransportInvoked = $false
} | ConvertTo-Json -Depth 10
