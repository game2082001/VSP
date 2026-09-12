param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "../..")).Path
$scriptUnderTest = Join-Path $repoRoot "tools/orchestrator/a2-remediation-bootstrap.ps1"
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("a2-remediation-bootstrap-test-" + [Guid]::NewGuid().ToString("N"))
$script:tests = 0

function Assert-True {
    param([Parameter(Mandatory = $true)][bool] $Condition, [Parameter(Mandatory = $true)][string] $Name)
    if (-not $Condition) { throw "FAILED: $Name" }
    $script:tests++
}

function Assert-Equal {
    param($Expected, $Actual, [Parameter(Mandatory = $true)][string] $Name)
    if ($Expected -cne $Actual) { throw "FAILED: $Name. Expected '$Expected', actual '$Actual'." }
    $script:tests++
}

function Assert-Fails {
    param([Parameter(Mandatory = $true)][string] $Name, [Parameter(Mandatory = $true)][string] $Pattern, [Parameter(Mandatory = $true)][scriptblock] $Action)
    try { & $Action; throw "FAILED: $Name did not fail." } catch {
        if ($_.Exception.Message -notmatch $Pattern) { throw "FAILED: $Name returned unexpected error: $($_.Exception.Message)" }
    }
    $script:tests++
}

function Write-Utf8NoBom {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Text)
    [IO.File]::WriteAllText($Path, $Text.Replace("`r`n", "`n").Replace("`r", "`n"), [Text.UTF8Encoding]::new($false))
}

function New-TestRepository {
    param([Parameter(Mandatory = $true)][string] $Root)
    New-Item -ItemType Directory -Force -Path $Root | Out-Null
    foreach ($relative in @(
        "AI/Orchestrator/Policy/artifact-intake-policy.json",
        "AI/Orchestrator/Policy/artifact-intake-policy.schema.json",
        "AI/Orchestrator/Templates/artifact-intake-request.schema.json",
        "AI/Orchestrator/Templates/artifact-intake-request.template.json",
        "AI/Orchestrator/Templates/artifact-intake-decision.schema.json",
        "AI/Orchestrator/Templates/artifact-intake-decision.template.json"
    )) {
        $destination = Join-Path $Root $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        Copy-Item -LiteralPath (Join-Path $repoRoot $relative) -Destination $destination
    }
    $manifestPath = Join-Path $Root "r1.manifest.json"
    $manifest = [ordered]@{
        taskId = "VSP-AI02-001TI-A2-R1"
        classification = "CRITICAL"
        repositoryTransport = [ordered]@{ approvedFiles = @("tools/orchestrator/artifact-intake-contract.ps1") }
        executionBaseHandling = [ordered]@{ actualExecutionSha = "0" * 40 }
        remediationInfrastructure = [ordered]@{
            architecture = "A2_RECOVERY_OPTION_C_MECHANICAL_SKELETON_PLUS_CLAUDE"
            outputPath = "tools/orchestrator/artifact-intake-contract.ps1"
            sentinel = "NOT_IMPLEMENTED"
            claudeAllowedTools = @("Read", "Write", "Edit")
            harness = [ordered]@{ minimumSemanticCaseCount = 52 }
        }
    }
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    $baselinePath = Join-Path $Root "baseline.json"
    Set-Content -LiteralPath $baselinePath -Value '{"baseline":"immutable"}' -Encoding utf8NoBOM
    Push-Location $Root
    try {
        git init | Out-Null
        git config user.email "ai02-test@example.invalid" | Out-Null
        git config user.name "AI02 Test" | Out-Null
        git add . | Out-Null
        git commit -m baseline | Out-Null
    } finally { Pop-Location }
    return [ordered]@{ manifest = $manifestPath; baseline = $baselinePath; runtime = (Join-Path (Split-Path -Parent $Root) ((Split-Path -Leaf $Root) + "-runtime")); output = (Join-Path $Root "tools/orchestrator/artifact-intake-contract.ps1") }
}

function Invoke-Bootstrap {
    param([Parameter(Mandatory = $true)] $Fixture, [Parameter(Mandatory = $true)][string] $Mode, [string] $ExpectedBaselineSha256 = "")
    $arguments = @{
        Mode = $Mode
        ManifestPath = $Fixture.manifest
        WorkspacePath = (Split-Path -Parent $Fixture.manifest)
        RuntimeDirectory = $Fixture.runtime
        PredecessorBaselinePath = $Fixture.baseline
    }
    if ($ExpectedBaselineSha256) { $arguments.ExpectedBaselineSha256 = $ExpectedBaselineSha256 }
    return (& $scriptUnderTest @arguments | Out-String).Trim()
}

function Set-OracleCopyingStub {
    param([Parameter(Mandatory = $true)][string] $Path)
    $stub = @'
param(
    [Parameter(Mandatory = $true)][string] $PolicyPath,
    [Parameter(Mandatory = $true)][string] $PolicySchemaPath,
    [Parameter(Mandatory = $true)][string] $RequestSchemaPath,
    [Parameter(Mandatory = $true)][string] $DecisionSchemaPath,
    [Parameter(Mandatory = $true)][string] $TrustedContextFixtureRoot,
    [Parameter(Mandatory = $true)][string] $OutputEvidencePath
)
Set-StrictMode -Version Latest
$caseId = Split-Path -Leaf $TrustedContextFixtureRoot
$fixtureRoot = Split-Path -Parent (Split-Path -Parent $TrustedContextFixtureRoot)
$expected = Get-Content -LiteralPath (Join-Path $fixtureRoot "expected/$caseId.json") -Raw | ConvertFrom-Json
[ordered]@{
    validatorSchemaVersion = "1.0"
    policyVersion = "vsp-ai02-intake-v1"
    result = $expected.result
    canonicalFailureCategory = $expected.canonicalFailureCategory
    findingCodes = if ([string]::IsNullOrWhiteSpace([string]$expected.requiredFindingCode)) { @() } else { @([string]$expected.requiredFindingCode) }
    checkIds = @($caseId)
    taskId = "VSP-AI02-001TI-A2-R1"
    repository = "game2082001/VSP"
    computedRequestSha256 = "0" * 64
    approvedFileMetadata = @()
    trustedSourceIdentity = [ordered]@{ sourceSha = "0" * 40 }
    trustedArtifactIdentity = [ordered]@{ artifactId = "1" }
    predecessorTaskPhaseSequence = [ordered]@{ taskId = "VSP-AI02-001TI-A1D-VALIDATE"; phase = "A1"; sequence = 1 }
    predecessorDescriptorSha256 = "1" * 64
    parentAggregateStateDigest = "sha256:" + ("2" * 64)
    validatorVersion = "test-stub"
} | ConvertTo-Json -Depth 8 | ForEach-Object { [IO.File]::WriteAllText($OutputEvidencePath, $_ + "`n", [Text.UTF8Encoding]::new($false)) }
'@
    Write-Utf8NoBom -Path $Path -Text $stub
}

function Set-ControlledPassingStub {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $FixtureRoot
    )
    $mapping = [ordered]@{}
    foreach ($caseDirectory in @(Get-ChildItem -LiteralPath (Join-Path $FixtureRoot "inputs") -Directory | Sort-Object Name)) {
        $fingerprintText = @(Get-ChildItem -LiteralPath $caseDirectory.FullName -Recurse -File | Sort-Object FullName | ForEach-Object {
            ([IO.Path]::GetRelativePath($caseDirectory.FullName, $_.FullName).Replace('\','/') + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())
        }) -join "`n"
        $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($fingerprintText))).ToLowerInvariant()
        $mapping[$fingerprint] = Get-Content -LiteralPath (Join-Path $FixtureRoot ("expected/" + $caseDirectory.Name + ".json")) -Raw | ConvertFrom-Json
    }
    $mappingBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($mapping | ConvertTo-Json -Depth 10 -Compress)))
    $stub = @'
param(
    [Parameter(Mandatory = $true)][string] $PolicyPath,
    [Parameter(Mandatory = $true)][string] $PolicySchemaPath,
    [Parameter(Mandatory = $true)][string] $RequestSchemaPath,
    [Parameter(Mandatory = $true)][string] $DecisionSchemaPath,
    [Parameter(Mandatory = $true)][string] $TrustedContextFixtureRoot,
    [Parameter(Mandatory = $true)][string] $OutputEvidencePath
)
Set-StrictMode -Version Latest
$mapping = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__MAPPING_BASE64__')) | ConvertFrom-Json -AsHashtable
$fingerprintText = @(Get-ChildItem -LiteralPath $TrustedContextFixtureRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
    ([IO.Path]::GetRelativePath($TrustedContextFixtureRoot, $_.FullName).Replace('\','/') + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())
}) -join "`n"
$fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($fingerprintText))).ToLowerInvariant()
$expected = $mapping[$fingerprint]
if ($null -eq $expected) { throw 'Unknown controlled test vector.' }
[ordered]@{
    validatorSchemaVersion = "1.0"
    policyVersion = "vsp-ai02-intake-v1"
    result = $expected.result
    canonicalFailureCategory = $expected.canonicalFailureCategory
    findingCodes = if ([string]::IsNullOrWhiteSpace([string]$expected.requiredFindingCode)) { @() } else { @([string]$expected.requiredFindingCode) }
    checkIds = @("controlled-harness-test-double")
    taskId = "VSP-AI02-001TI-A2-R1"
    repository = "game2082001/VSP"
    computedRequestSha256 = "0" * 64
    approvedFileMetadata = @()
    trustedSourceIdentity = [ordered]@{ sourceSha = "0" * 40 }
    trustedArtifactIdentity = [ordered]@{ artifactId = "1" }
    predecessorTaskPhaseSequence = [ordered]@{ taskId = "VSP-AI02-001TI-A1D-VALIDATE"; phase = "A1"; sequence = 1 }
    predecessorDescriptorSha256 = "1" * 64
    parentAggregateStateDigest = "sha256:" + ("2" * 64)
    validatorVersion = "controlled-harness-test-double"
} | ConvertTo-Json -Depth 8 | ForEach-Object { [IO.File]::WriteAllText($OutputEvidencePath, $_ + "`n", [Text.UTF8Encoding]::new($false)) }
'@
    Write-Utf8NoBom -Path $Path -Text $stub.Replace('__MAPPING_BASE64__', $mappingBase64)
}

try {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    $first = New-TestRepository -Root (Join-Path $tempRoot "first")
    $second = New-TestRepository -Root (Join-Path $tempRoot "second")
    $firstResult = Invoke-Bootstrap -Fixture $first -Mode Prepare | ConvertFrom-Json
    $secondResult = Invoke-Bootstrap -Fixture $second -Mode Prepare | ConvertFrom-Json

    Assert-Equal $firstResult.skeletonSha256 $secondResult.skeletonSha256 "skeleton SHA is deterministic"
    Assert-Equal $firstResult.skeletonByteSize $secondResult.skeletonByteSize "skeleton byte size is deterministic"
    Assert-Equal $firstResult.harnessSha256 $secondResult.harnessSha256 "harness SHA is deterministic"
    Assert-Equal 52 ([int]$firstResult.semanticCaseCount) "semantic matrix count"
    $skeleton = Get-Content -LiteralPath $first.output -Raw
    Assert-True ($skeleton -match 'NOT_IMPLEMENTED') "skeleton sentinel present"
    Assert-True ($skeleton -match 'Set-StrictMode -Version Latest') "skeleton strict mode present"
    Assert-True ($skeleton -notmatch 'maxChangedFiles|REPLAY_DETECTED|ACCEPTED_FOR_TRANSPORT') "skeleton contains no policy semantics"
    Assert-True ($skeleton -notmatch '(?im)^\s*(function|param)\b') "skeleton invents no function or script interface"
    $skeletonBytes = [IO.File]::ReadAllBytes($first.output)
    Assert-True (-not ($skeletonBytes.Length -ge 3 -and $skeletonBytes[0] -eq 0xef -and $skeletonBytes[1] -eq 0xbb -and $skeletonBytes[2] -eq 0xbf)) "skeleton is UTF-8 without BOM"
    Assert-True (-not $skeleton.Contains("`r")) "skeleton uses LF line endings"
    Assert-Fails "skeleton is nonfunctional" "NOT_IMPLEMENTED" { & $first.output }
    $firstRoot = Split-Path -Parent $first.manifest
    $changed = @(
        @(git -C $firstRoot diff --name-only HEAD --) + @(git -C $firstRoot ls-files --others --exclude-standard) |
            ForEach-Object { ([string]$_).Replace('\','/') } | Sort-Object -Unique
    )
    Assert-Equal 1 $changed.Count "prepare changed-file count"
    Assert-Equal "tools/orchestrator/artifact-intake-contract.ps1" $changed[0] "prepare exact changed file"

    $statePath = Join-Path $first.runtime "bootstrap-state.json"
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    $originalHarness = [IO.File]::ReadAllBytes((Join-Path $first.runtime "a2-focused-harness.ps1"))
    $firstVector = Get-ChildItem -LiteralPath (Join-Path $first.runtime "fixtures") -Recurse -File | Sort-Object FullName | Select-Object -First 1
    $originalVector = [IO.File]::ReadAllBytes($firstVector.FullName)
    $runtimeRequest = Join-Path $first.runtime "fixtures/inputs/01-valid-request/request.json"
    $originalRuntimeRequest = [IO.File]::ReadAllBytes($runtimeRequest)
    $originalBaseline = [IO.File]::ReadAllBytes($first.baseline)
    $originalSkeleton = [IO.File]::ReadAllBytes($first.output)
    $baselineHash = (Get-FileHash -LiteralPath $first.baseline -Algorithm SHA256).Hash.ToLowerInvariant()

    Assert-Fails "unchanged skeleton rejected" "did not change" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    [IO.File]::WriteAllBytes($first.output, [byte[]]@())
    Assert-Fails "empty file rejected" "missing or empty" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Remove-Item -LiteralPath $first.output -Force
    Assert-Fails "missing file rejected" "missing or empty" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    [IO.File]::WriteAllBytes($first.output, $originalSkeleton)
    Write-Utf8NoBom -Path $first.output -Text "Set-StrictMode -Version Latest`nthrow 'NOT_IMPLEMENTED'`n# changed"
    Assert-Fails "sentinel rejected" "unfinished skeleton sentinel" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Write-Utf8NoBom -Path $first.output -Text "Set-StrictMode -Version Latest`n# TODO finish validator"
    Assert-Fails "TODO marker rejected" "unfinished skeleton sentinel" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Write-Utf8NoBom -Path $first.output -Text "function Broken {`n"
    Assert-Fails "malformed PowerShell rejected" "not valid PowerShell" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Write-Utf8NoBom -Path $first.output -Text "Invoke-WebRequest 'https://example.invalid'"
    Assert-Fails "network call rejected" "Prohibited command" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Write-Utf8NoBom -Path $first.output -Text "git push origin main"
    Assert-Fails "Git write rejected" "Prohibited command" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Write-Utf8NoBom -Path $first.output -Text "Start-Process pwsh"
    Assert-Fails "process launch rejected" "Prohibited command" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Write-Utf8NoBom -Path $first.output -Text "& './tools/orchestrator/repository-transport.ps1'"
    Assert-Fails "Repository Transport rejected" "Transport" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Write-Utf8NoBom -Path $first.output -Text "`$value = `$env:GITHUB_TOKEN"
    Assert-Fails "credential access rejected" "environment or credential" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }

    Set-Content -LiteralPath (Join-Path (Split-Path -Parent $first.manifest) "extra.txt") -Value extra
    Set-OracleCopyingStub -Path $first.output
    Assert-Fails "extra changed file rejected" "changed-file set" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    Remove-Item -LiteralPath (Join-Path (Split-Path -Parent $first.manifest) "extra.txt") -Force

    Add-Content -LiteralPath (Join-Path $first.runtime "a2-focused-harness.ps1") -Value "# mutation"
    Assert-Fails "harness mutation rejected" "harness integrity" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    [IO.File]::WriteAllBytes((Join-Path $first.runtime "a2-focused-harness.ps1"), $originalHarness)
    Add-Content -LiteralPath $firstVector.FullName -Value " "
    Assert-Fails "test-vector mutation rejected" "test-vector integrity" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    [IO.File]::WriteAllBytes($firstVector.FullName, $originalVector)
    Add-Content -LiteralPath $first.baseline -Value " "
    Assert-Fails "predecessor mutation rejected" "baseline changed" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }
    [IO.File]::WriteAllBytes($first.baseline, $originalBaseline)

    Set-ControlledPassingStub -Path $first.output -FixtureRoot (Join-Path $first.runtime "fixtures")
    Add-Content -LiteralPath $first.output -Value "`nAdd-Content -LiteralPath (Join-Path `$TrustedContextFixtureRoot 'request.json') -Value ' '"
    $isolatedMutationCompletion = Invoke-Bootstrap $first ValidateCompletion $baselineHash | ConvertFrom-Json
    Assert-Equal "PASS" $isolatedMutationCompletion.result "validator receives only an isolated input copy"
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($runtimeRequest)) -ceq [Convert]::ToBase64String($originalRuntimeRequest)) "isolated validator cannot mutate original test vectors"
    Remove-Item -LiteralPath $first.output -Force
    Invoke-Bootstrap $first Prepare | Out-Null

    Set-ControlledPassingStub -Path $first.output -FixtureRoot (Join-Path $first.runtime "fixtures")
    Add-Content -LiteralPath $first.output -Value "`nAdd-Content -LiteralPath `$PSCommandPath -Value '# runtime mutation'"
    Assert-Fails "validator cannot mutate itself during harness" "changed during semantic execution" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }

    Set-OracleCopyingStub -Path $first.output
    Assert-Fails "validator cannot read sibling expected-result oracle" "Validator did not create evidence" { Invoke-Bootstrap $first ValidateCompletion $baselineHash }

    Set-ControlledPassingStub -Path $first.output -FixtureRoot (Join-Path $first.runtime "fixtures")
    $completion = Invoke-Bootstrap $first ValidateCompletion $baselineHash | ConvertFrom-Json
    Assert-Equal "PASS" $completion.result "completed file proceeds through harness"
    Assert-Equal 52 ([int]$completion.semanticCaseCount) "completed harness semantic count"
    Assert-True ([bool]$completion.deterministicRepeatedExecution) "completed harness deterministic repeat"
    Assert-True ($completion.finalSha256 -cne $completion.skeletonSha256) "completed file differs from skeleton"
    Assert-Equal $false ([bool]$completion.repositoryWriteCredentialAvailableToDeveloper) "completion remains credentialless"
    Assert-Equal "Run Claude Code Primary Developer begins" $completion.a2AttemptConsumptionBoundary "attempt boundary"
    $finalChanged = @(
        @(git -C $firstRoot diff --name-only HEAD --) + @(git -C $firstRoot ls-files --others --exclude-standard) |
            ForEach-Object { ([string]$_).Replace('\','/') } | Sort-Object -Unique
    )
    Assert-Equal 1 $finalChanged.Count "completion does not mutate repository outside target"
    Assert-Equal "tools/orchestrator/artifact-intake-contract.ps1" $finalChanged[0] "completion preserves exact one-file change"

    $harnessText = Get-Content -LiteralPath (Join-Path $first.runtime "a2-focused-harness.ps1") -Raw
    foreach ($parameter in @('ValidatorPath','PolicyPath','PolicySchemaPath','RequestSchemaPath','DecisionSchemaPath','TrustedContextFixtureRoot','OutputEvidencePath')) {
        Assert-True ($harnessText -match ("\$" + $parameter)) "harness interface $parameter"
    }
    Assert-True ($harnessText -notmatch '(?i)GITHUB_TOKEN|Authorization:|VSP_AI_APP_PRIVATE_KEY') "harness contains no credential"
    Assert-True ($harnessText -notmatch '(?i)Invoke-WebRequest|Invoke-RestMethod|git push|gh api') "harness requires no network or repository write"

    Write-Output ("A2 remediation bootstrap focused tests: {0}/{0} PASS; semantic matrix: 52/52 represented" -f $script:tests)
} finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
