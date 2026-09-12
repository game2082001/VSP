[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$generator = Join-Path $PSScriptRoot 'generate-artifact-intake-contract.ps1'
$policyPath = Join-Path $PSScriptRoot '..\..\AI\Orchestrator\Policy\artifact-intake-policy.json'
$policySchemaPath = Join-Path $PSScriptRoot '..\..\AI\Orchestrator\Policy\artifact-intake-policy.schema.json'
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('ai02-a1d-gen-' + [guid]::NewGuid().ToString('N'))
$script:passed = 0
$script:failed = 0

function Pass([string]$Name) { $script:passed++; Write-Output "PASS: $Name" }
function Fail([string]$Name, [string]$Message) { $script:failed++; Write-Output "FAIL: $Name -- $Message" }
function Assert-True([bool]$Condition, [string]$Name) { if ($Condition) { Pass $Name } else { Fail $Name 'assertion was false' } }
function Json-Equal($Left, $Right) { return (($Left | ConvertTo-Json -Depth 100 -Compress) -ceq ($Right | ConvertTo-Json -Depth 100 -Compress)) }

function Read-Json([string]$Path) { return (Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -AsHashtable -Depth 100) }
function Write-Json($Value, [string]$Path) {
    $json = ($Value | ConvertTo-Json -Depth 100).Replace("`r`n", "`n").Replace("`r", "`n") + "`n"
    [System.IO.File]::WriteAllText($Path, $json, [System.Text.UTF8Encoding]::new($false))
}

function Test-JsonType($Value, [string]$Type) {
    switch ($Type) {
        'object' { return $Value -is [System.Collections.IDictionary] }
        'array' { return ($Value -is [System.Collections.IList]) -and -not ($Value -is [string]) }
        'string' { return $Value -is [string] }
        'boolean' { return $Value -is [bool] }
        'integer' { return ($Value -is [byte]) -or ($Value -is [int16]) -or ($Value -is [int32]) -or ($Value -is [int64]) -or ($Value -is [uint16]) -or ($Value -is [uint32]) -or ($Value -is [uint64]) }
        'number' { return ($Value -is [System.ValueType]) -and -not ($Value -is [bool]) }
        default { throw "Unsupported test schema type: $Type" }
    }
}

function Assert-SchemaValue($Value, [hashtable]$Rule, [hashtable]$Root, [string]$Path = '$') {
    if ($Rule.Contains('$ref')) {
        $prefix = '#/$defs/'
        if (-not $Rule['$ref'].StartsWith($prefix, [StringComparison]::Ordinal)) { throw "Unsupported ref at ${Path}: $($Rule['$ref'])" }
        $name = $Rule['$ref'].Substring($prefix.Length)
        Assert-SchemaValue -Value $Value -Rule $Root['$defs'][$name] -Root $Root -Path $Path
        return
    }
    if ($Rule.Contains('const') -and -not (Json-Equal $Value $Rule.const)) { throw "const mismatch at $Path" }
    if ($Rule.Contains('enum')) {
        $matches = @($Rule.enum | Where-Object { Json-Equal $Value $_ }).Count -gt 0
        if (-not $matches) { throw "enum mismatch at $Path" }
    }
    if ($Rule.Contains('type') -and -not (Test-JsonType $Value $Rule.type)) { throw "type mismatch at $Path" }
    if ($Rule.Contains('not')) {
        $matched = $true
        try { Assert-SchemaValue -Value $Value -Rule $Rule.not -Root $Root -Path $Path } catch { $matched = $false }
        if ($matched) { throw "not matched at $Path" }
    }
    if ($Value -is [string]) {
        if ($Rule.Contains('minLength') -and $Value.Length -lt $Rule.minLength) { throw "minLength at $Path" }
        if ($Rule.Contains('maxLength') -and $Value.Length -gt $Rule.maxLength) { throw "maxLength at $Path" }
        if ($Rule.Contains('pattern') -and $Value -cnotmatch $Rule.pattern) { throw "pattern at $Path" }
    }
    if (Test-JsonType $Value 'number') {
        if ($Rule.Contains('minimum') -and $Value -lt $Rule.minimum) { throw "minimum at $Path" }
        if ($Rule.Contains('maximum') -and $Value -gt $Rule.maximum) { throw "maximum at $Path" }
    }
    if ($Value -is [System.Collections.IDictionary]) {
        if ($Rule.Contains('required')) { foreach ($name in $Rule.required) { if (-not $Value.Contains($name)) { throw "missing $name at $Path" } } }
        if ($Rule.Contains('additionalProperties') -and $Rule.additionalProperties -eq $false -and $Rule.Contains('properties')) {
            foreach ($name in $Value.Keys) { if (-not $Rule.properties.Contains($name)) { throw "unknown $name at $Path" } }
        }
        if ($Rule.Contains('properties')) {
            foreach ($name in $Rule.properties.Keys) { if ($Value.Contains($name)) { Assert-SchemaValue -Value $Value[$name] -Rule $Rule.properties[$name] -Root $Root -Path "$Path.$name" } }
        }
    }
    if (($Value -is [System.Collections.IList]) -and -not ($Value -is [string])) {
        if ($Rule.Contains('minItems') -and $Value.Count -lt $Rule.minItems) { throw "minItems at $Path" }
        if ($Rule.Contains('maxItems') -and $Value.Count -gt $Rule.maxItems) { throw "maxItems at $Path" }
        if ($Rule.Contains('uniqueItems') -and $Rule.uniqueItems) {
            $serialized = @($Value | ForEach-Object { $_ | ConvertTo-Json -Depth 100 -Compress })
            if (@($serialized | Sort-Object -Unique).Count -ne $serialized.Count) { throw "uniqueItems at $Path" }
        }
        if ($Rule.Contains('items')) { for ($i = 0; $i -lt $Value.Count; $i++) { Assert-SchemaValue -Value $Value[$i] -Rule $Rule.items -Root $Root -Path "$Path[$i]" } }
    }
    if ($Rule.Contains('allOf')) { foreach ($child in $Rule.allOf) { Assert-SchemaValue -Value $Value -Rule $child -Root $Root -Path $Path } }
    if ($Rule.Contains('if')) {
        $matchesIf = $true
        try { Assert-SchemaValue -Value $Value -Rule $Rule.if -Root $Root -Path $Path } catch { $matchesIf = $false }
        if ($matchesIf -and $Rule.Contains('then')) { Assert-SchemaValue -Value $Value -Rule $Rule.then -Root $Root -Path $Path }
    }
}

function Test-Valid($Value, [hashtable]$Schema) {
    try { Assert-SchemaValue -Value $Value -Rule $Schema -Root $Schema; return $true } catch { return $false }
}

function Assert-StandardsSchemaAndInstance([string]$SchemaPath, [string]$InstancePath, [string]$Name) {
    $schemaText = [IO.File]::ReadAllText($SchemaPath, [Text.UTF8Encoding]::new($false, $true))
    $instanceText = [IO.File]::ReadAllText($InstancePath, [Text.UTF8Encoding]::new($false, $true))
    $schemaNode = [Text.Json.Nodes.JsonNode]::Parse($schemaText)
    $metaResult = [Json.Schema.MetaSchemas]::Draft202012.Evaluate($schemaNode)
    Assert-True $metaResult.IsValid "$Name schema passes Draft 2020-12 meta-schema"
    $compiled = [Json.Schema.JsonSchema]::FromText($schemaText)
    $result = $compiled.Evaluate([Text.Json.Nodes.JsonNode]::Parse($instanceText))
    Assert-True $result.IsValid "$Name template passes standards-compliant schema evaluation"
}

function Invoke-ExpectedPolicyFailure([string]$Name, [scriptblock]$Mutation, [bool]$MutateSchema = $false) {
    $case = Join-Path $temporaryRoot ('invalid-' + $Name.Replace(' ', '-'))
    [System.IO.Directory]::CreateDirectory($case) | Out-Null
    $candidatePolicy = Read-Json $policyPath
    $candidateSchema = Read-Json $policySchemaPath
    if ($MutateSchema) { & $Mutation $candidateSchema } else { & $Mutation $candidatePolicy }
    $candidatePolicyPath = Join-Path $case 'policy.json'
    $candidateSchemaPath = Join-Path $case 'policy.schema.json'
    Write-Json $candidatePolicy $candidatePolicyPath
    Write-Json $candidateSchema $candidateSchemaPath
    $output = Join-Path $case 'output'
    $failed = $false
    try { & $generator -OutputDirectory $output -PolicyPath $candidatePolicyPath -PolicySchemaPath $candidateSchemaPath *> $null } catch { $failed = $true }
    Assert-True $failed "invalid policy rejected: $Name"
    Assert-True (-not (Test-Path -LiteralPath $output)) "invalid policy creates no output: $Name"
}

try {
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    $run1 = Join-Path $temporaryRoot 'run1'
    $run2 = Join-Path $temporaryRoot 'run2'
    & $generator -OutputDirectory $run1 *> $null
    & $generator -OutputDirectory $run2 *> $null

    $names = @('artifact-intake-request.schema.json','artifact-intake-request.template.json','artifact-intake-decision.schema.json','artifact-intake-decision.template.json')
    Assert-True (@(Get-ChildItem -File $run1).Count -eq 4) 'exactly four outputs generated'
    Assert-True (@(Compare-Object (@(Get-ChildItem -File $run1).Name) $names).Count -eq 0) 'generated output names are exact'
    foreach ($name in $names) {
        $one = Join-Path $run1 $name; $two = Join-Path $run2 $name
        Assert-True ([System.Collections.StructuralComparisons]::StructuralEqualityComparer.Equals([System.IO.File]::ReadAllBytes($one), [System.IO.File]::ReadAllBytes($two))) "byte repeatability: $name"
        Assert-True ((Get-FileHash -Algorithm SHA256 $one).Hash -ceq (Get-FileHash -Algorithm SHA256 $two).Hash) "SHA-256 repeatability: $name"
        $bytes = [System.IO.File]::ReadAllBytes($one)
        Assert-True (-not ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) "UTF-8 without BOM: $name"
        Assert-True (-not ([System.Text.Encoding]::UTF8.GetString($bytes).Contains("`r"))) "LF-only: $name"
        try { Read-Json $one | Out-Null; Pass "JSON parses: $name" } catch { Fail "JSON parses: $name" $_.Exception.Message }
    }

    $policy = Read-Json $policyPath
    $requestSchema = Read-Json (Join-Path $run1 $names[0])
    $request = Read-Json (Join-Path $run1 $names[1])
    $decisionSchema = Read-Json (Join-Path $run1 $names[2])
    $decision = Read-Json (Join-Path $run1 $names[3])
    Assert-True ($requestSchema['$schema'] -ceq 'https://json-schema.org/draft/2020-12/schema') 'request schema declares Draft 2020-12'
    Assert-True ($decisionSchema['$schema'] -ceq 'https://json-schema.org/draft/2020-12/schema') 'decision schema declares Draft 2020-12'
    Assert-StandardsSchemaAndInstance (Join-Path $run1 $names[0]) (Join-Path $run1 $names[1]) 'request'
    Assert-StandardsSchemaAndInstance (Join-Path $run1 $names[2]) (Join-Path $run1 $names[3]) 'decision'
    Assert-True (Test-Valid $request $requestSchema) 'request template validates against request schema'
    Assert-True (Test-Valid $decision $decisionSchema) 'decision template validates against decision schema'
    Assert-True (Json-Equal $request.policy ([ordered]@{repositoryPath=$policy.repositoryPath;limits=$policy.limits;replay=$policy.replay;staleBase=$policy.staleBase;credentialInvariants=$policy.credentialInvariants;transport=$policy.transport})) 'request normative policy snapshot structurally equals canonical policy'
    Assert-True (Json-Equal $decision.policy $request.policy) 'decision and request policy snapshots are identical'
    Assert-True (Json-Equal $decisionSchema.properties.evidence.properties.failureCategory.enum $policy.failureCategories) 'decision failure vocabulary comes exactly from policy'
    Assert-True ($requestSchema.properties.hashBindings.required -contains 'packageSha256') 'request requires package SHA'
    Assert-True ($requestSchema.properties.hashBindings.required -contains 'manifestSha256') 'request requires manifest SHA'
    Assert-True ($requestSchema.properties.hashBindings.required -contains 'stateSha256') 'request requires state SHA'
    Assert-True ($decisionSchema.properties.requestReference.required -contains 'requestSha256') 'decision requires request SHA'
    foreach ($binding in @('artifactId','artifactName','githubArtifactDigest','packageSha256','manifestSha256','stateSha256')) { Assert-True ($decisionSchema.properties.requestReference.required -contains $binding) "decision requires provenance: $binding" }
    Assert-True ($decisionSchema.properties.transportInvoked.const -eq $false) 'transportInvoked is globally false'

    foreach ($safe in @('AI/Orchestrator/file.json','tools/orchestrator/script.ps1','a/b/c.txt')) {
        $candidate = Read-Json (Join-Path $run1 $names[1]); $candidate.authorization.approvedFiles = @($safe)
        Assert-True (Test-Valid $candidate $requestSchema) "safe path accepted: $safe"
    }
    $unsafe = @('../escape','folder/../escape','./file','folder/./file','/absolute','//server/path','folder\file','C:escape','C:/escape','folder//file','','é/file',(([string]([char]1)) + 'file'),(('a' * 241)),(('a' * 101) + '/file'))
    foreach ($path in $unsafe) {
        $candidate = Read-Json (Join-Path $run1 $names[1]); $candidate.authorization.approvedFiles = @($path)
        Assert-True (-not (Test-Valid $candidate $requestSchema)) "unsafe path rejected: $([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($path)))"
    }

    $acceptedBad = Read-Json (Join-Path $run1 $names[3]); $acceptedBad.transportAuthorized = $false
    Assert-True (-not (Test-Valid $acceptedBad $decisionSchema)) 'accepted decision requires transportAuthorized true'
    $acceptedFailure = Read-Json (Join-Path $run1 $names[3]); $acceptedFailure.evidence.failureCategory = 'DIGEST_MISMATCH'
    Assert-True (-not (Test-Valid $acceptedFailure $decisionSchema)) 'accepted decision requires failure NONE'
    $rejected = Read-Json (Join-Path $run1 $names[3]); $rejected.decision = 'REJECTED'; $rejected.transportAuthorized = $false; $rejected.evidence.statusCategory = 'DIGEST_MISMATCH'; $rejected.evidence.failureCategory = 'DIGEST_MISMATCH'
    Assert-True (Test-Valid $rejected $decisionSchema) 'valid rejected decision accepted'
    $rejected.transportAuthorized = $true
    Assert-True (-not (Test-Valid $rejected $decisionSchema)) 'rejected decision requires transportAuthorized false'
    $rejected.transportAuthorized = $false; $rejected.evidence.failureCategory = 'NONE'
    Assert-True (-not (Test-Valid $rejected $decisionSchema)) 'rejected decision forbids failure NONE'

    Invoke-ExpectedPolicyFailure 'numeric ceiling' { param($p) $p.limits.maxChangedFiles = 201 }
    Invoke-ExpectedPolicyFailure 'path max length' { param($p) $p.repositoryPath.maximumLength = 241 }
    Invoke-ExpectedPolicyFailure 'credential invariant' { param($p) $p.credentialInvariants.claudeRepositoryWriteAuthority = $true }
    Invoke-ExpectedPolicyFailure 'decision state' { param($p) $p.decisionStateMachine.rejected.transportAuthorized = $true }
    Invoke-ExpectedPolicyFailure 'replay disposition' { param($p) $p.replay.matchingConsumedIdentity = 'ALLOW' }
    Invoke-ExpectedPolicyFailure 'exact base policy' { param($p) $p.staleBase.policy = 'LATEST_MAIN' }
    Invoke-ExpectedPolicyFailure 'transport boundary' { param($p) $p.transport.trustedIntakeMayInvoke = $true }
    Invoke-ExpectedPolicyFailure 'unknown top level' { param($p) $p.unknown = $true }
    Invoke-ExpectedPolicyFailure 'unknown nested field' { param($p) $p.replay.unknown = $true }
    Invoke-ExpectedPolicyFailure 'missing normative section' { param($p) $p.Remove('provenance') }
    Invoke-ExpectedPolicyFailure 'unsupported policy version' { param($p) $p.identity.policyVersion = 'vsp-ai02-intake-v2' }
    Invoke-ExpectedPolicyFailure 'unsupported schema dialect' { param($s) $s['$schema'] = 'https://json-schema.org/draft/2019-09/schema' } $true
    Invoke-ExpectedPolicyFailure 'unsupported schema identifier' { param($s) $s['$id'] = 'https://example.invalid/policy.schema.json' } $true
    Invoke-ExpectedPolicyFailure 'adversarial valid schema keyword' { param($s) $s['not'] = @{} } $true

    $duplicateCase = Join-Path $temporaryRoot 'duplicate-policy.json'
    [IO.File]::WriteAllText($duplicateCase, '{"schemaVersion":"1.0","schemaVersion":"1.0"}', [Text.UTF8Encoding]::new($false))
    $duplicateFailed = $false
    try { & $generator -OutputDirectory (Join-Path $temporaryRoot 'duplicate-output') -PolicyPath $duplicateCase *> $null } catch { $duplicateFailed = $true }
    Assert-True $duplicateFailed 'duplicate JSON properties rejected'

    $generatorSource = Get-Content -Raw -LiteralPath $generator
    Assert-True (-not $generatorSource.Contains('34480651384') -and -not $generatorSource.Contains('34490250854')) 'historical artifacts are not generator templates'
    Assert-True (-not $generatorSource.Contains('Get-Date') -and -not $generatorSource.Contains('New-Guid') -and -not $generatorSource.Contains('Invoke-WebRequest')) 'generator has no time, GUID, or network dependency'
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}

Write-Output "A1D-GEN focused tests: $script:passed passed, $script:failed failed"
if ($script:failed -ne 0) { exit 1 }
