[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$PolicyPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Policy\artifact-intake-policy.json"),

    [string]$PolicySchemaPath = (Join-Path $PSScriptRoot "..\..\AI\Orchestrator\Policy\artifact-intake-policy.schema.json")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-NoDuplicateJsonProperties {
    param([System.Text.Json.JsonElement]$Element, [string]$Path = '$')

    if ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Object) {
        $names = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($property in $Element.EnumerateObject()) {
            if (-not $names.Add($property.Name)) {
                throw "Duplicate JSON property at ${Path}: $($property.Name)"
            }
            Assert-NoDuplicateJsonProperties -Element $property.Value -Path "$Path.$($property.Name)"
        }
    }
    elseif ($Element.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
        $index = 0
        foreach ($item in $Element.EnumerateArray()) {
            Assert-NoDuplicateJsonProperties -Element $item -Path "$Path[$index]"
            $index++
        }
    }
}

function Read-StrictJson {
    param([Parameter(Mandatory = $true)][string]$LiteralPath)

    $resolved = (Resolve-Path -LiteralPath $LiteralPath -ErrorAction Stop).Path
    $bytes = [System.IO.File]::ReadAllBytes($resolved)
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        throw "JSON must be UTF-8 without BOM: $resolved"
    }
    $text = [System.Text.UTF8Encoding]::new($false, $true).GetString($bytes)
    $options = [System.Text.Json.JsonDocumentOptions]::new()
    $options.AllowTrailingCommas = $false
    $options.CommentHandling = [System.Text.Json.JsonCommentHandling]::Disallow
    $document = [System.Text.Json.JsonDocument]::Parse($text, $options)
    try {
        Assert-NoDuplicateJsonProperties -Element $document.RootElement
    }
    finally {
        $document.Dispose()
    }
    return ($text | ConvertFrom-Json -AsHashtable -Depth 100)
}

function ConvertTo-CompactJson {
    param($Value)
    return ($Value | ConvertTo-Json -Depth 100 -Compress)
}

function Assert-PolicyMatchesClosedSchema {
    param([hashtable]$Policy, [hashtable]$Schema)

    if ($Schema['$schema'] -cne 'https://json-schema.org/draft/2020-12/schema') {
        throw "Unsupported policy schema dialect."
    }
    if ($Schema['$id'] -cne 'https://github.com/game2082001/VSP/AI/Orchestrator/Policy/artifact-intake-policy.schema.json') {
        throw "Unsupported policy schema identifier."
    }
    if ($Schema.type -cne 'object' -or $Schema.additionalProperties -ne $false) {
        throw "Policy schema must be a closed object."
    }
    foreach ($requiredName in $Schema.required) {
        if (-not $Policy.Contains($requiredName)) {
            throw "Policy is missing required section: $requiredName"
        }
    }
    foreach ($name in $Policy.Keys) {
        if (-not $Schema.properties.Contains($name)) {
            throw "Policy contains unknown top-level field: $name"
        }
    }
    foreach ($name in $Schema.properties.Keys) {
        $rule = $Schema.properties[$name]
        if (-not $rule.Contains('const')) {
            throw "Policy schema property is not mechanically closed by const: $name"
        }
        if ((ConvertTo-CompactJson $Policy[$name]) -cne (ConvertTo-CompactJson $rule.const)) {
            throw "Policy does not match the closed schema at: $name"
        }
    }
}

function Write-CanonicalJson {
    param([Parameter(Mandatory = $true)]$Value, [Parameter(Mandatory = $true)][string]$LiteralPath)

    $json = ($Value | ConvertTo-Json -Depth 100)
    $json = $json.Replace("`r`n", "`n").Replace("`r", "`n") + "`n"
    [System.IO.File]::WriteAllText($LiteralPath, $json, [System.Text.UTF8Encoding]::new($false))
}

$policy = Read-StrictJson -LiteralPath $PolicyPath
$policySchema = Read-StrictJson -LiteralPath $PolicySchemaPath
Assert-PolicyMatchesClosedSchema -Policy $policy -Schema $policySchema

$identity = $policy.identity
$provenance = $policy.provenance
$pathPolicy = $policy.repositoryPath
$limits = $policy.limits
$credentials = $policy.credentialInvariants
$transport = $policy.transport
$states = $policy.decisionStateMachine

$pathPattern = '^(?=.{1,' + $pathPolicy.maximumLength + '}$)(?!/)(?!.*[\\:])(?!.*//)(?!.*(?:^|/)\.{1,2}(?:/|$))(?!.*\/$)(?!.*(?:^|/)[^/]{' + ($pathPolicy.maximumSegmentLength + 1) + ',}(?:/|$))[\x20-\x7E]+$'

$policySnapshot = [ordered]@{
    repositoryPath = $pathPolicy
    limits = $limits
    replay = $policy.replay
    staleBase = $policy.staleBase
    credentialInvariants = $credentials
    transport = $transport
}

$commonDefs = [ordered]@{
    sha256Hex = [ordered]@{ type = 'string'; pattern = $provenance.sha256HexPattern }
    artifactDigest = [ordered]@{ type = 'string'; pattern = $provenance.githubArtifactDigestPattern }
    commitSha = [ordered]@{ type = 'string'; pattern = $identity.sourceSha.pattern }
    positiveDecimalId = [ordered]@{ type = 'string'; pattern = $identity.artifactId.pattern }
    repositoryPath = [ordered]@{ type = 'string'; minLength = $pathPolicy.minimumLength; maxLength = $pathPolicy.maximumLength; pattern = $pathPattern }
}

$requestSchema = [ordered]@{
    '$schema' = 'https://json-schema.org/draft/2020-12/schema'
    '$id' = 'https://github.com/game2082001/VSP/AI/Orchestrator/Templates/artifact-intake-request.schema.json'
    title = 'AI02 Trusted Artifact Intake request'
    type = 'object'
    additionalProperties = $false
    '$defs' = $commonDefs
    required = @('schemaVersion','schemaId','policyVersion','taskId','repository','expectedSourceSha','developer','artifact','hashBindings','authorization','transportIntent','policy')
    properties = [ordered]@{
        schemaVersion = [ordered]@{ const = $policy.schemaVersion }
        schemaId = [ordered]@{ const = $identity.requestSchemaIdentifier }
        policyVersion = [ordered]@{ const = $identity.policyVersion }
        taskId = [ordered]@{ type = 'string'; pattern = $identity.taskId.pattern; maxLength = $identity.taskId.maxLength }
        repository = [ordered]@{ const = $identity.repository.exactValue }
        expectedSourceSha = [ordered]@{ '$ref' = '#/$defs/commitSha' }
        developer = [ordered]@{
            type = 'object'; additionalProperties = $false
            required = @('workflowId','runId','runAttempt')
            properties = [ordered]@{
                workflowId = [ordered]@{ type = 'string'; pattern = $identity.workflowId.pattern }
                runId = [ordered]@{ type = 'string'; pattern = $identity.runId.pattern }
                runAttempt = [ordered]@{ type = 'integer'; minimum = $identity.runAttempt.minimum; maximum = $identity.runAttempt.maximum }
            }
        }
        artifact = [ordered]@{
            type = 'object'; additionalProperties = $false
            required = @('artifactId','artifactName','githubArtifactDigest')
            properties = [ordered]@{
                artifactId = [ordered]@{ '$ref' = '#/$defs/positiveDecimalId' }
                artifactName = [ordered]@{ type = 'string'; pattern = $identity.artifactName.pattern; maxLength = $identity.artifactName.maxLength }
                githubArtifactDigest = [ordered]@{ '$ref' = '#/$defs/artifactDigest' }
            }
        }
        hashBindings = [ordered]@{
            type = 'object'; additionalProperties = $false
            required = @('packageSha256','manifestSha256','stateSha256')
            properties = [ordered]@{
                packageSha256 = [ordered]@{ '$ref' = '#/$defs/sha256Hex' }
                manifestSha256 = [ordered]@{ '$ref' = '#/$defs/sha256Hex' }
                stateSha256 = [ordered]@{ '$ref' = '#/$defs/sha256Hex' }
                trustedInnerHashPins = [ordered]@{
                    type = 'array'; maxItems = $limits.maxChangedFiles; uniqueItems = $true
                    items = [ordered]@{
                        type = 'object'; additionalProperties = $false; required = @('path','sha256')
                        properties = [ordered]@{ path = [ordered]@{ '$ref' = '#/$defs/repositoryPath' }; sha256 = [ordered]@{ '$ref' = '#/$defs/sha256Hex' } }
                    }
                }
            }
        }
        authorization = [ordered]@{
            type = 'object'; additionalProperties = $false
            required = @('manifestPath','statePath','approvedFiles')
            properties = [ordered]@{
                manifestPath = [ordered]@{ '$ref' = '#/$defs/repositoryPath' }
                statePath = [ordered]@{ '$ref' = '#/$defs/repositoryPath' }
                approvedFiles = [ordered]@{ type = 'array'; minItems = 1; maxItems = $limits.maxChangedFiles; uniqueItems = $true; items = [ordered]@{ '$ref' = '#/$defs/repositoryPath' } }
            }
        }
        transportIntent = [ordered]@{
            const = [ordered]@{ handoff = $transport.handoff; interface = $transport.interface; requestTransportAuthorization = $transport.trustedIntakeMayAuthorize }
        }
        policy = [ordered]@{ const = $policySnapshot }
    }
}

$requestTemplate = [ordered]@{
    schemaVersion = $policy.schemaVersion
    schemaId = $identity.requestSchemaIdentifier
    policyVersion = $identity.policyVersion
    taskId = 'VSP-AI02-EXAMPLE'
    repository = $identity.repository.exactValue
    expectedSourceSha = ('0' * 40)
    developer = [ordered]@{ workflowId = '1'; runId = '1'; runAttempt = 1 }
    artifact = [ordered]@{ artifactId = '1'; artifactName = 'example-artifact'; githubArtifactDigest = ('sha256:' + ('0' * 64)) }
    hashBindings = [ordered]@{ packageSha256 = ('0' * 64); manifestSha256 = ('1' * 64); stateSha256 = ('2' * 64); trustedInnerHashPins = @() }
    authorization = [ordered]@{
        manifestPath = 'AI/Orchestrator/Manifests/example.manifest.json'
        statePath = 'AI/Orchestrator/State/example.state.json'
        approvedFiles = @('AI/Orchestrator/file.json')
    }
    transportIntent = [ordered]@{ handoff = $transport.handoff; interface = $transport.interface; requestTransportAuthorization = $transport.trustedIntakeMayAuthorize }
    policy = $policySnapshot
}

$decisionSchema = [ordered]@{
    '$schema' = 'https://json-schema.org/draft/2020-12/schema'
    '$id' = 'https://github.com/game2082001/VSP/AI/Orchestrator/Templates/artifact-intake-decision.schema.json'
    title = 'AI02 Trusted Artifact Intake sanitized decision'
    type = 'object'
    additionalProperties = $false
    '$defs' = $commonDefs
    required = @('schemaVersion','schemaId','policyVersion','taskId','repository','requestReference','decision','transportAuthorized','transportInvoked','evidence','policy')
    properties = [ordered]@{
        schemaVersion = [ordered]@{ const = $policy.schemaVersion }
        schemaId = [ordered]@{ const = $identity.decisionSchemaIdentifier }
        policyVersion = [ordered]@{ const = $identity.policyVersion }
        taskId = [ordered]@{ type = 'string'; pattern = $identity.taskId.pattern; maxLength = $identity.taskId.maxLength }
        repository = [ordered]@{ const = $identity.repository.exactValue }
        requestReference = [ordered]@{
            type = 'object'; additionalProperties = $false
            required = @('requestSha256','artifactId','artifactName','githubArtifactDigest','packageSha256','manifestSha256','stateSha256')
            properties = [ordered]@{
                requestSha256 = [ordered]@{ '$ref' = '#/$defs/sha256Hex' }
                artifactId = [ordered]@{ '$ref' = '#/$defs/positiveDecimalId' }
                artifactName = [ordered]@{ type = 'string'; pattern = $identity.artifactName.pattern; maxLength = $identity.artifactName.maxLength }
                githubArtifactDigest = [ordered]@{ '$ref' = '#/$defs/artifactDigest' }
                packageSha256 = [ordered]@{ '$ref' = '#/$defs/sha256Hex' }
                manifestSha256 = [ordered]@{ '$ref' = '#/$defs/sha256Hex' }
                stateSha256 = [ordered]@{ '$ref' = '#/$defs/sha256Hex' }
            }
        }
        decision = [ordered]@{ enum = @($states.allowedDecisions) }
        transportAuthorized = [ordered]@{ type = 'boolean' }
        transportInvoked = [ordered]@{ const = $states.global.transportInvoked }
        evidence = [ordered]@{
            type = 'object'; additionalProperties = $false
            required = @('statusCategory','failureCategory','approvedFilesCount')
            properties = [ordered]@{
                statusCategory = [ordered]@{ enum = @($policy.failureCategories) }
                failureCategory = [ordered]@{ enum = @($policy.failureCategories) }
                approvedFilesCount = [ordered]@{ type = 'integer'; minimum = 0; maximum = $limits.maxChangedFiles }
            }
        }
        policy = [ordered]@{ const = $policySnapshot }
    }
    allOf = @(
        [ordered]@{
            if = [ordered]@{ required = @('decision'); properties = [ordered]@{ decision = [ordered]@{ const = $states.rejected.decision } } }
            then = [ordered]@{
                properties = [ordered]@{
                    transportAuthorized = [ordered]@{ const = $states.rejected.transportAuthorized }
                    transportInvoked = [ordered]@{ const = $states.rejected.transportInvoked }
                    evidence = [ordered]@{ properties = [ordered]@{ failureCategory = [ordered]@{ not = [ordered]@{ const = $states.acceptedForTransport.failureCategory } }; statusCategory = [ordered]@{ not = [ordered]@{ const = $states.acceptedForTransport.validationStatus } } } }
                }
            }
        },
        [ordered]@{
            if = [ordered]@{ required = @('decision'); properties = [ordered]@{ decision = [ordered]@{ const = $states.acceptedForTransport.decision } } }
            then = [ordered]@{
                properties = [ordered]@{
                    transportAuthorized = [ordered]@{ const = $states.acceptedForTransport.transportAuthorized }
                    transportInvoked = [ordered]@{ const = $states.acceptedForTransport.transportInvoked }
                    evidence = [ordered]@{ properties = [ordered]@{ statusCategory = [ordered]@{ const = $states.acceptedForTransport.validationStatus }; failureCategory = [ordered]@{ const = $states.acceptedForTransport.failureCategory } } }
                }
            }
        }
    )
}

$decisionTemplate = [ordered]@{
    schemaVersion = $policy.schemaVersion
    schemaId = $identity.decisionSchemaIdentifier
    policyVersion = $identity.policyVersion
    taskId = 'VSP-AI02-EXAMPLE'
    repository = $identity.repository.exactValue
    requestReference = [ordered]@{
        requestSha256 = ('3' * 64); artifactId = '1'; artifactName = 'example-artifact'; githubArtifactDigest = ('sha256:' + ('0' * 64))
        packageSha256 = ('0' * 64); manifestSha256 = ('1' * 64); stateSha256 = ('2' * 64)
    }
    decision = $states.acceptedForTransport.decision
    transportAuthorized = $states.acceptedForTransport.transportAuthorized
    transportInvoked = $states.acceptedForTransport.transportInvoked
    evidence = [ordered]@{ statusCategory = $states.acceptedForTransport.validationStatus; failureCategory = $states.acceptedForTransport.failureCategory; approvedFilesCount = 1 }
    policy = $policySnapshot
}

$outputs = [ordered]@{
    'artifact-intake-request.schema.json' = $requestSchema
    'artifact-intake-request.template.json' = $requestTemplate
    'artifact-intake-decision.schema.json' = $decisionSchema
    'artifact-intake-decision.template.json' = $decisionTemplate
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
foreach ($name in $outputs.Keys) {
    Write-CanonicalJson -Value $outputs[$name] -LiteralPath (Join-Path $resolvedOutput $name)
}

[pscustomobject]@{
    status = 'GENERATED'
    policyPath = (Resolve-Path -LiteralPath $PolicyPath).Path
    policySchemaPath = (Resolve-Path -LiteralPath $PolicySchemaPath).Path
    outputDirectory = $resolvedOutput
    files = @($outputs.Keys)
} | ConvertTo-Json -Depth 10
