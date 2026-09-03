param()

$ErrorActionPreference = "Stop"

function Join-RepoPath {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string[]] $Segments
    )

    $path = $Root
    foreach ($segment in $Segments) {
        $path = Join-Path $path $segment
    }
    return $path
}

$repoRoot = (Resolve-Path -LiteralPath (Join-RepoPath -Root $PSScriptRoot -Segments @("..", ".."))).Path
$requestSchemaPath = Join-RepoPath -Root $repoRoot -Segments @("AI", "Orchestrator", "Templates", "local-ai-advisory-request.schema.json")
$responseSchemaPath = Join-RepoPath -Root $repoRoot -Segments @("AI", "Orchestrator", "Templates", "local-ai-advisory-response.schema.json")
$requestTemplatePath = Join-RepoPath -Root $repoRoot -Segments @("AI", "Orchestrator", "Templates", "local-ai-advisory-request.template.json")
$responseTemplatePath = Join-RepoPath -Root $repoRoot -Segments @("AI", "Orchestrator", "Templates", "local-ai-advisory-response.template.json")

function Read-Json {
    param([Parameter(Mandatory = $true)][string] $Path)
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)][bool] $Condition,
        [Parameter(Mandatory = $true)][string] $Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Fails {
    param(
        [Parameter(Mandatory = $true)][scriptblock] $Script,
        [Parameter(Mandatory = $true)][string] $Name
    )

    try {
        & $Script | Out-Null
    } catch {
        return
    }

    throw "Expected validation failure did not occur: $Name"
}

function Get-JsonByteCount {
    param([Parameter(Mandatory = $true)] $Object)
    $json = $Object | ConvertTo-Json -Depth 20
    return [Text.Encoding]::UTF8.GetByteCount($json)
}

function Assert-Sha {
    param(
        [Parameter(Mandatory = $true)][string] $Value,
        [Parameter(Mandatory = $true)][string] $Name
    )
    if ($Value -notmatch '^[0-9a-f]{40}$') {
        throw "$Name must be a lowercase 40-character SHA."
    }
}

function Assert-Digest {
    param(
        [Parameter(Mandatory = $true)][string] $Value,
        [Parameter(Mandatory = $true)][string] $Name
    )
    if ($Value -notmatch '^sha256:[0-9a-f]{64}$') {
        throw "$Name must be a sha256 digest."
    }
}

function Assert-NoUnknownProperties {
    param(
        [Parameter(Mandatory = $true)] $Object,
        [Parameter(Mandatory = $true)][string[]] $Allowed,
        [Parameter(Mandatory = $true)][string] $Name
    )

    foreach ($property in @($Object.PSObject.Properties.Name)) {
        if ($Allowed -notcontains $property) {
            throw "$Name contains unsupported property: $property"
        }
    }
}

function Test-LocalAiRequest {
    param(
        [Parameter(Mandatory = $true)] $Request,
        [Parameter(Mandatory = $true)] $Schema
    )

    Assert-NoUnknownProperties -Object $Request -Allowed @($Schema.allowedTopLevelProperties) -Name "request"
    foreach ($required in @($Schema.required)) {
        Assert-Condition -Condition ($null -ne $Request.PSObject.Properties[$required]) -Message "request missing required field: $required"
    }
    foreach ($forbidden in @($Schema.forbiddenTopLevelProperties)) {
        Assert-Condition -Condition ($null -eq $Request.PSObject.Properties[$forbidden]) -Message "request contains forbidden field: $forbidden"
    }
    Assert-Condition -Condition (@($Schema.allowedAnalysisTypes) -contains $Request.analysisType) -Message "request analysisType is not allowed."
    Assert-Sha -Value ([string]$Request.sourceSha) -Name "sourceSha"
    if ($null -ne $Request.PSObject.Properties["baseSha"]) { Assert-Sha -Value ([string]$Request.baseSha) -Name "baseSha" }
    if ($null -ne $Request.PSObject.Properties["headSha"]) { Assert-Sha -Value ([string]$Request.headSha) -Name "headSha" }
    Assert-Digest -Value ([string]$Request.inputDigest) -Name "inputDigest"
    Assert-Condition -Condition (@($Request.approvedScope).Count -le $Schema.bounds.maxApprovedScopeItems) -Message "approvedScope exceeds bound."
    Assert-Condition -Condition (@($Request.prohibitedScope).Count -le $Schema.bounds.maxProhibitedScopeItems) -Message "prohibitedScope exceeds bound."
    Assert-Condition -Condition (@($Request.acceptanceCriteria).Count -le $Schema.bounds.maxAcceptanceCriteriaItems) -Message "acceptanceCriteria exceeds bound."
    Assert-Condition -Condition (@($Request.changedFiles).Count -le $Schema.bounds.maxChangedFiles) -Message "changedFiles exceeds bound."
    if ($null -ne $Request.PSObject.Properties["boundedDiff"]) {
        Assert-Condition -Condition (([string]$Request.boundedDiff).Length -le $Schema.bounds.maxBoundedDiffChars) -Message "boundedDiff exceeds bound."
    }
    Assert-Condition -Condition ((Get-JsonByteCount -Object $Request) -le $Schema.bounds.maxRequestBytes) -Message "request exceeds byte bound."
    foreach ($path in @($Request.changedFiles)) {
        $text = [string]$path
        Assert-Condition -Condition (-not [IO.Path]::IsPathRooted($text)) -Message "changedFiles must be repository-relative."
        Assert-Condition -Condition ($text -notmatch '(^|/)\.\.(/|$)') -Message "changedFiles must not contain traversal."
    }
    return $true
}

function Test-LocalAiResponse {
    param(
        [Parameter(Mandatory = $true)] $Response,
        [Parameter(Mandatory = $true)] $Schema,
        [Parameter(Mandatory = $true)] $Request
    )

    Assert-NoUnknownProperties -Object $Response -Allowed @($Schema.allowedTopLevelProperties) -Name "response"
    foreach ($required in @($Schema.required)) {
        Assert-Condition -Condition ($null -ne $Response.PSObject.Properties[$required]) -Message "response missing required field: $required"
    }
    Assert-Condition -Condition (@($Schema.allowedResults) -contains $Response.result) -Message "response result is not advisory-only."
    Assert-Condition -Condition ($Response.taskId -eq $Request.taskId) -Message "response taskId does not bind to request."
    Assert-Condition -Condition ($Response.sourceSha -eq $Request.sourceSha) -Message "response sourceSha does not bind to request."
    Assert-Condition -Condition ($Response.analysisType -eq $Request.analysisType) -Message "response analysisType does not bind to request."
    Assert-Condition -Condition ($Response.requestInputDigest -eq $Request.inputDigest) -Message "response digest does not bind to request."
    Assert-Condition -Condition (@($Response.findings).Count -le $Schema.bounds.maxFindings) -Message "findings exceed bound."
    Assert-Condition -Condition ($Response.governance.repositoryWrite -eq $Schema.governanceRequiredValues.repositoryWrite) -Message "repositoryWrite escalated."
    Assert-Condition -Condition ($Response.governance.githubWrite -eq $Schema.governanceRequiredValues.githubWrite) -Message "githubWrite escalated."
    Assert-Condition -Condition ($Response.governance.productionCredentialAccess -eq $Schema.governanceRequiredValues.productionCredentialAccess) -Message "productionCredentialAccess escalated."
    Assert-Condition -Condition ($Response.governance.mergeAuthorization -eq $Schema.governanceRequiredValues.mergeAuthorization) -Message "mergeAuthorization escalated."
    foreach ($forbidden in @($Schema.forbiddenAuthorityValues)) {
        Assert-Condition -Condition ($Response.result -ne $forbidden) -Message "response result contains forbidden authority value."
        Assert-Condition -Condition ($Response.governance.mergeAuthorization -ne $forbidden) -Message "response governance contains forbidden authority value."
    }
    $forbiddenAuthorityPattern = (@($Schema.forbiddenAuthorityValues) | ForEach-Object { [regex]::Escape([string]$_) }) -join "|"
    foreach ($finding in @($Response.findings)) {
        Assert-Condition -Condition (@($Schema.allowedFindingSeverities) -contains $finding.severity) -Message "finding severity is invalid."
        Assert-Condition -Condition (@($Schema.allowedConfidence) -contains $finding.confidence) -Message "finding confidence is invalid."
        foreach ($fieldName in @("reason", "suggestedVerification")) {
            if ($null -ne $finding.PSObject.Properties[$fieldName]) {
                $text = [string]$finding.$fieldName
                Assert-Condition -Condition ($text -notmatch "(?i)\b($forbiddenAuthorityPattern)\b") -Message "finding $fieldName contains forbidden authority value."
                Assert-Condition -Condition ($text -notmatch '(?i)\b(merge|release|remediation)\s+authori[sz]ed\b') -Message "finding $fieldName contains forbidden authority language."
                Assert-Condition -Condition ($text -notmatch '(?i)\b(merge|release|remediation)\s+authorization\b') -Message "finding $fieldName contains forbidden authority language."
            }
        }
        if ($null -ne $finding.PSObject.Properties["file"] -and -not [string]::IsNullOrWhiteSpace([string]$finding.file)) {
            Assert-Condition -Condition (-not [IO.Path]::IsPathRooted([string]$finding.file)) -Message "finding file must be repository-relative."
            Assert-Condition -Condition ((@($Request.changedFiles) -contains $finding.file) -or $Response.result -eq "INCONCLUSIVE") -Message "finding file is outside request context."
        }
        if ($null -ne $finding.PSObject.Properties["startLine"]) {
            Assert-Condition -Condition ([int]$finding.startLine -ge 1) -Message "finding startLine must be positive."
        }
        if ($null -ne $finding.PSObject.Properties["endLine"]) {
            Assert-Condition -Condition ([int]$finding.endLine -ge 1) -Message "finding endLine must be positive."
        }
    }
    Assert-Condition -Condition ((Get-JsonByteCount -Object $Response) -le $Schema.bounds.maxResponseBytes) -Message "response exceeds byte bound."
    return $true
}

$requestSchema = Read-Json -Path $requestSchemaPath
$responseSchema = Read-Json -Path $responseSchemaPath
$request = Read-Json -Path $requestTemplatePath
$response = Read-Json -Path $responseTemplatePath

Test-LocalAiRequest -Request $request -Schema $requestSchema | Out-Null
Test-LocalAiResponse -Response $response -Schema $responseSchema -Request $request | Out-Null

$badAuthorityResponse = $response | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$badAuthorityResponse.result = "READY_FOR_MERGE"
Assert-Fails -Name "response cannot express READY_FOR_MERGE" -Script { Test-LocalAiResponse -Response $badAuthorityResponse -Schema $responseSchema -Request $request }

$badGovernanceResponse = $response | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$badGovernanceResponse.governance.githubWrite = $true
Assert-Fails -Name "response cannot escalate GitHub authority" -Script { Test-LocalAiResponse -Response $badGovernanceResponse -Schema $responseSchema -Request $request }

$staleResponse = $response | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$staleResponse.sourceSha = "1111111111111111111111111111111111111111"
Assert-Fails -Name "stale source SHA fails binding" -Script { Test-LocalAiResponse -Response $staleResponse -Schema $responseSchema -Request $request }

$badRequest = $request | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$badRequest | Add-Member -NotePropertyName "githubToken" -NotePropertyValue "secret"
Assert-Fails -Name "request rejects sensitive token field" -Script { Test-LocalAiRequest -Request $badRequest -Schema $requestSchema }

$unknownAuthorityRequest = $request | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$unknownAuthorityRequest | Add-Member -NotePropertyName "mergeAuthorization" -NotePropertyValue "APPROVED"
Assert-Fails -Name "request rejects unknown authority-bearing property" -Script { Test-LocalAiRequest -Request $unknownAuthorityRequest -Schema $requestSchema }

$oversizedRequest = $request | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$oversizedRequest.changedFiles = @(1..101 | ForEach-Object { "Docs/file$_.md" })
Assert-Fails -Name "request changed-file count bound" -Script { Test-LocalAiRequest -Request $oversizedRequest -Schema $requestSchema }

$malformedFinding = $response | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$malformedFinding.findings[0].severity = "P9"
Assert-Fails -Name "malformed finding severity" -Script { Test-LocalAiResponse -Response $malformedFinding -Schema $responseSchema -Request $request }

$authorityFinding = $response | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$authorityFinding.findings[0].reason = "This advisory finding says READY_FOR_MERGE."
Assert-Fails -Name "finding reason cannot express READY_FOR_MERGE" -Script { Test-LocalAiResponse -Response $authorityFinding -Schema $responseSchema -Request $request }

$authorityVerification = $response | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$authorityVerification.findings[0].suggestedVerification = "Merge authorized after this advisory check."
Assert-Fails -Name "finding suggestedVerification cannot express merge authorization" -Script { Test-LocalAiResponse -Response $authorityVerification -Schema $responseSchema -Request $request }

$authorityNounForm = $response | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$authorityNounForm.findings[0].reason = "This finding implies release authorization."
Assert-Fails -Name "finding reason cannot express release authorization noun form" -Script { Test-LocalAiResponse -Response $authorityNounForm -Schema $responseSchema -Request $request }

$hallucinatedPath = $response | ConvertTo-Json -Depth 20 | ConvertFrom-Json
$hallucinatedPath.result = "FINDINGS"
$hallucinatedPath.findings[0].file = "Unknown/NotInRequest.cs"
Assert-Fails -Name "hallucinated file path outside request context" -Script { Test-LocalAiResponse -Response $hallucinatedPath -Schema $responseSchema -Request $request }

[pscustomobject]@{
    status = "PASS"
    requestSchema = "PASS"
    responseSchema = "PASS"
    advisoryResultsOnly = "PASS"
    staleShaBindingRejected = "PASS"
    authorityEscalationRejected = "PASS"
    authorityTextInFindingsRejected = "PASS"
    sensitiveRequestFieldsExcluded = "PASS"
    oversizedRequestRejected = "PASS"
    malformedFindingsRejected = "PASS"
    hallucinatedPathRejected = "PASS"
    localAiCalled = $false
    repositoryWriteAuthorityGranted = $false
    githubAuthorityGranted = $false
    existingVspGatesChanged = $false
} | ConvertTo-Json -Depth 4
