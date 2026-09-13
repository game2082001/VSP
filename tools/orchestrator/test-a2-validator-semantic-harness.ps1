[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$generator = Join-Path $PSScriptRoot "generate-artifact-intake-validator.ps1"
$harness = Join-Path $PSScriptRoot "a2-validator-semantic-harness.ps1"
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("ai02-a2-gen1-harness-test-" + [Guid]::NewGuid().ToString("N"))
$script:passed = 0
$script:failed = 0

function Pass([string] $Name) { $script:passed++; Write-Output "PASS: $Name" }
function Fail([string] $Name, [string] $Message) { $script:failed++; Write-Output "FAIL: $Name -- $Message" }
function Assert-True([bool] $Condition, [string] $Name) { if ($Condition) { Pass $Name } else { Fail $Name "assertion was false" } }

try {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    $stage1 = Join-Path $tempRoot "stage1"
    $stage2 = Join-Path $tempRoot "stage2"
    $gen1 = (& $generator -OutputDirectory $stage1 | Out-String).Trim() | ConvertFrom-Json
    $gen2 = (& $generator -OutputDirectory $stage2 | Out-String).Trim() | ConvertFrom-Json
    $validator = Join-Path $stage1 "tools/orchestrator/artifact-intake-contract.ps1"

    Assert-True ($gen1.generatedSha256 -ceq $gen2.generatedSha256) "generator repeat SHA matches before harness execution"
    Assert-True ($gen1.generatedByteSize -gt 0) "generated validator is non-empty"

    $harnessOut1 = Join-Path $tempRoot "harness1"
    $harnessOut2 = Join-Path $tempRoot "harness2"
    $summary1 = (& $harness -ValidatorPath $validator -OutputDirectory $harnessOut1 | Out-String).Trim() | ConvertFrom-Json
    $summary2 = (& $harness -ValidatorPath $validator -OutputDirectory $harnessOut2 | Out-String).Trim() | ConvertFrom-Json

    Assert-True ($summary1.result -ceq "PASS") "harness result PASS"
    Assert-True ($summary1.harnessSelfValidation -ceq "PASS") "harness self-validation PASS"
    Assert-True ([int]$summary1.semanticCaseCount -ge 52) "minimum 52 semantic cases represented"
    Assert-True ([bool]$summary1.executedTwice) "each semantic case executed twice"
    Assert-True ([bool]$summary1.deterministicRepeatedExecution) "semantic evidence deterministic on repeat"
    Assert-True ($summary1.sixSectionPolicyProjection -ceq "PASS") "six-section projection self-validation PASS"
    Assert-True ($summary1.authoritativeA1Predecessor -ceq "PASS") "authoritative A1 predecessor self-validation PASS"
    Assert-True ($summary1.expectedOutcomeOracleExposed -eq $false) "oracle expected outcome is not exposed to validator"
    Assert-True ($summary1.repositoryWriteCredentialAvailableToDeveloper -eq $false) "harness remains credentialless"
    Assert-True ([int]$summary2.semanticCaseCount -eq [int]$summary1.semanticCaseCount) "harness repeat case count stable"

    $caseIds = @($summary1.cases | ForEach-Object { $_.id })
    foreach ($required in @("01-valid-first-use", "02-replay-rejected", "03-stale-base", "04-changed-files-empty", "path-07", "semantic-predecessor-task", "semantic-descriptor", "semantic-aggregate")) {
        Assert-True ($caseIds -contains $required) "required semantic case present: $required"
    }
    Assert-True (@($summary1.cases | Where-Object { $_.result -eq "ACCEPTED_FOR_TRANSPORT" }).Count -gt 0) "PASS vectors are represented"
    Assert-True (@($summary1.cases | Where-Object { $_.category -eq "REPLAY_DETECTED" }).Count -eq 1) "replay rejection represented"
    Assert-True (@($summary1.cases | Where-Object { $_.category -eq "STALE_BASE" }).Count -eq 1) "stale base rejection represented"
    Assert-True (@($summary1.cases | Where-Object { $_.category -eq "PATH_POLICY_VIOLATION" }).Count -ge 4) "path policy rejection represented"
    Assert-True (@($summary1.cases | Where-Object { $_.category -eq "APPROVED_FILES_MISMATCH" }).Count -ge 2) "changed-file mismatch rejection represented"

    $summaryPath = Join-Path $harnessOut1 "semantic-harness-summary.json"
    Assert-True (Test-Path -LiteralPath $summaryPath -PathType Leaf) "summary evidence file is written"
    $summaryHash1 = (Get-FileHash -LiteralPath $summaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $summaryHash2 = (Get-FileHash -LiteralPath (Join-Path $harnessOut2 "semantic-harness-summary.json") -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-True ($summaryHash1 -ceq $summaryHash2) "summary evidence is deterministic across harness runs"
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output "A2-GEN1 harness focused tests: $script:passed passed, $script:failed failed"
if ($script:failed -ne 0) { exit 1 }
