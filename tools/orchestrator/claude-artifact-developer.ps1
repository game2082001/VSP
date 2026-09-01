param(
    [Parameter(Mandatory = $true)]
    [string] $ManifestPath,

    [Parameter(Mandatory = $true)]
    [string] $StatePath,

    [Parameter(Mandatory = $true)]
    [string] $ExpectedBaseSha,

    [string] $Repository = "game2082001/VSP",

    [string] $OutputDirectory = "",

    [switch] $ValidateOnly,

    [switch] $PreparePrompt,

    [switch] $Package,

    [switch] $DiagnosePostClaude,

    [string] $ClaudeConclusion = "",

    [string] $ClaudeSessionId = "",

    [string] $ClaudeExecutionFile = "",

    [string] $ClaudePermissionDenialCount = ""
)

$ErrorActionPreference = "Stop"

function Stop-Developer {
    param([Parameter(Mandatory = $true)][string] $Message)
    throw "AI02 Claude Artifact Developer validation failed: $Message"
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        Stop-Developer "Required file not found: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    } catch {
        Stop-Developer "Invalid JSON in $Path. $($_.Exception.Message)"
    }
}

function Assert-RepoRelativePath {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Name
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        Stop-Developer "$Name is required."
    }

    if ([IO.Path]::IsPathRooted($Path)) {
        Stop-Developer "$Name must be repository-relative."
    }

    $normalized = $Path.Replace('\', '/')
    if ($normalized -match '(^|/)\.\.(/|$)' -or $normalized -match '(^|/)\.(/|$)') {
        Stop-Developer "$Name must not contain traversal or current-directory segments."
    }

    return $normalized
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)][string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Invoke-Git {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)

    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        Stop-Developer "git $($Arguments -join ' ') failed."
    }
    return $output
}

function Assert-TrustedTask {
    param(
        [Parameter(Mandatory = $true)] $Manifest,
        [Parameter(Mandatory = $true)] $State
    )

    if ($Manifest.repository -ne $Repository -or $State.repository -ne $Repository) {
        Stop-Developer "manifest/state repository must both be $Repository."
    }

    if ($Manifest.taskId -ne $State.taskId) {
        Stop-Developer "manifest/state taskId mismatch."
    }

    if ($State.taskManifestStatus -ne "VALID" -or $State.classificationConsistencyStatus -ne "VALID") {
        Stop-Developer "state must contain VALID manifest and classification consistency."
    }

    if ($Manifest.productOwnerAuthorization.authorized -ne $true -or $State.productOwnerAuthorizationEvidence.authorized -ne $true) {
        Stop-Developer "Product Owner authorization evidence is required."
    }

    if ($Manifest.classification -ne $State.classification) {
        Stop-Developer "manifest/state classification mismatch."
    }

    if ($Manifest.primaryDeveloper.role -ne "Claude Code Primary Developer" -or $Manifest.primaryDeveloper.adapter -ne "claude") {
        Stop-Developer "primary developer must be Claude Code Primary Developer with claude adapter."
    }

    if ($State.primaryDeveloperRole -ne "Claude Code Primary Developer" -or $State.primaryDeveloperAdapter -ne "claude") {
        Stop-Developer "state primary developer must be Claude Code Primary Developer with claude adapter."
    }

    if ($Manifest.independentReviewer.required -ne $true -or $Manifest.independentReviewer.adapter -ne "codex") {
        Stop-Developer "Separate Codex Independent Reviewer is required."
    }

    if ($State.developerEqualsReviewer -eq $true) {
        Stop-Developer "developerEqualsReviewer must be false."
    }

    if ($Manifest.classification -eq "CRITICAL" -and $Manifest.claudeCrossReview.required -ne $true) {
        Stop-Developer "CRITICAL tasks require Claude Cross Review."
    }

    if ($State.claudeCrossReviewRequired -ne [bool]$Manifest.claudeCrossReview.required) {
        Stop-Developer "Claude Cross Review requirement mismatch."
    }

    if ($Manifest.executionAuthorization.implementation -ne $true) {
        Stop-Developer "implementation execution must be authorized."
    }

    if ($Manifest.executionAuthorization.pushFeatureBranch -eq $true -or
        $Manifest.executionAuthorization.openOrUpdatePr -eq $true) {
        Stop-Developer "Claude artifact developer workflow must not authorize direct push or PR creation."
    }

    if ($ExpectedBaseSha -notmatch '^[0-9a-fA-F]{40}$') {
        Stop-Developer "ExpectedBaseSha must be a 40-character Git SHA."
    }

    $head = (Invoke-Git -Arguments @("rev-parse", "HEAD")).Trim()
    if ($head -ne $ExpectedBaseSha) {
        Stop-Developer "checked-out HEAD $head does not match expected base $ExpectedBaseSha."
    }
}

function Get-AllowedFiles {
    param([Parameter(Mandatory = $true)] $Manifest)

    if ($null -eq $Manifest.repositoryTransport -or $Manifest.repositoryTransport.required -ne $true) {
        return @()
    }

    return @($Manifest.repositoryTransport.approvedFiles | ForEach-Object {
        Assert-RepoRelativePath -Path ([string]$_) -Name "approvedFiles item"
    })
}

function Write-DeveloperPrompt {
    param(
        [Parameter(Mandatory = $true)] $Manifest,
        [Parameter(Mandatory = $true)] $State,
        [Parameter(Mandatory = $true)][string] $PromptPath
    )

    $approvedScope = @($Manifest.approvedScope) -join "`n- "
    $stopConditions = @($Manifest.stopConditions) -join "`n- "
    $outOfScope = @($Manifest.outOfScope) -join "`n- "
    $allowedFiles = @(Get-AllowedFiles -Manifest $Manifest)
    $approvedFiles = $allowedFiles -join "`n- "
    $expectedMarkers = @()
    if ($null -ne $Manifest.smokeFixture -and $null -ne $Manifest.smokeFixture.expectedContentMarkers) {
        $expectedMarkers = @($Manifest.smokeFixture.expectedContentMarkers | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    $expectedMarkerText = if ($expectedMarkers.Count -gt 0) { $expectedMarkers -join "`n- " } else { "No additional content markers supplied by the validated manifest." }

    $text = @"
You are Claude Code acting as the AI02 Primary Developer.

Task ID: $($Manifest.taskId)
Classification: $($Manifest.classification)
Repository: $Repository
Base SHA: $ExpectedBaseSha

Approved Scope:
- $approvedScope

Out of Scope:
- $outOfScope

Stop Conditions:
- $stopConditions

TASK:
- Create or modify exactly the files authorized by the validated publication allowlist.
- For this task, the complete required changed-file set is:
- $approvedFiles

MANDATORY OUTPUT:
- Every path listed above must exist in the repository working tree before you finish.
- The final git changed-file set must exactly equal the allowlist above.
- The required output content must include these validated markers:
- $expectedMarkerText

FORBIDDEN:
- Do not modify any repository file outside the allowlist above.
- Do not make product code, product test, workflow, transport, intake, governance, credential, branch, pull request, merge, tag, release, or deployment changes unless those exact paths are in the validated allowlist.

COMPLETION CONDITION:
- Do not declare completion until the required file exists.
- Do not declare completion until `git diff --name-only HEAD --` plus untracked files exactly equals the allowlist.
- Do not declare completion until the required output content markers are present.

NO SUBSTITUTE:
- Analysis, explanation, recommendation, or a textual response without the required working-tree modification does not satisfy this task.
- If you cannot make the required working-tree change exactly, stop and report the blocking reason without modifying other files.

Rules:
- Do not push branches, create pull requests, merge, tag, or request repository-write credentials.
- Do not access VSP_AI_APP_PRIVATE_KEY, App installation tokens, PATs, or reusable GitHub credentials.
- Make changes only in the GitHub Actions runner working tree.
- Preserve Product Owner scope and stop instead of expanding architecture.
- When done, leave the working tree ready for packaging by the AI02 artifact developer workflow.
"@

    $parent = Split-Path -Parent $PromptPath
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    Set-Content -LiteralPath $PromptPath -Value $text -Encoding utf8
}

function New-PublicationPackage {
    param(
        [Parameter(Mandatory = $true)] $Manifest,
        [Parameter(Mandatory = $true)] $State,
        [Parameter(Mandatory = $true)][string] $Destination
    )

    if ([string]::IsNullOrWhiteSpace($Destination)) {
        Stop-Developer "OutputDirectory is required when Package is set."
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $packageRoot = Join-Path $Destination "package-root"
    if (Test-Path -LiteralPath $packageRoot) {
        Remove-Item -LiteralPath $packageRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

    $changed = @()
    $changed += Invoke-Git -Arguments @("diff", "--name-only", "HEAD", "--")
    $changed += Invoke-Git -Arguments @("ls-files", "--others", "--exclude-standard")
    $changed = @($changed | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)

    $allowed = Get-AllowedFiles -Manifest $Manifest
    if ($allowed.Count -gt 0) {
        $changedSorted = @($changed | ForEach-Object { $_.Replace('\', '/') } | Sort-Object)
        $allowedSorted = @($allowed | Sort-Object)
        if (($changedSorted -join "`n") -ne ($allowedSorted -join "`n")) {
            Stop-Developer "changed files do not exactly match approved publication files."
        }
    }

    $files = @()
    foreach ($path in $changed) {
        $repoPath = Assert-RepoRelativePath -Path $path -Name "changed file path"
        if ($repoPath -like ".git/*" -or $repoPath -eq ".git") {
            Stop-Developer "changed file path may not target .git."
        }
        if (-not (Test-Path -LiteralPath $repoPath -PathType Leaf)) {
            Stop-Developer "changed path is not a regular file: $repoPath"
        }
        $item = Get-Item -LiteralPath $repoPath -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Stop-Developer "reparse-point files are not allowed: $repoPath"
        }

        $target = Join-Path $packageRoot $repoPath
        $targetParent = Split-Path -Parent $target
        if (-not [string]::IsNullOrWhiteSpace($targetParent)) {
            New-Item -ItemType Directory -Force -Path $targetParent | Out-Null
        }
        Copy-Item -LiteralPath $repoPath -Destination $target

        $files += [pscustomobject]@{
            path = $repoPath
            mode = "100644"
            size = $item.Length
            sha256 = Get-FileSha256 -Path $repoPath
        }
    }

    if ($null -ne $Manifest.smokeFixture -and
        $Manifest.smokeFixture.infrastructureSmoke -eq $true -and
        -not [string]::IsNullOrWhiteSpace([string]$Manifest.smokeFixture.approvedOutputPath)) {
        $approvedOutputPath = Assert-RepoRelativePath -Path ([string]$Manifest.smokeFixture.approvedOutputPath) -Name "smokeFixture.approvedOutputPath"
        if ($changed.Count -ne 1 -or (($changed | ForEach-Object { $_.Replace('\', '/') }) -notcontains $approvedOutputPath)) {
            Stop-Developer "infrastructure smoke changed-file set must contain exactly the approved output path."
        }

        if (-not (Test-Path -LiteralPath $approvedOutputPath -PathType Leaf)) {
            Stop-Developer "infrastructure smoke approved output file is missing."
        }

        $content = Get-Content -LiteralPath $approvedOutputPath -Raw
        if ([string]::IsNullOrWhiteSpace($content)) {
            Stop-Developer "infrastructure smoke approved output file is empty."
        }

        foreach ($marker in @($Manifest.smokeFixture.expectedContentMarkers)) {
            $markerText = [string]$marker
            if (-not [string]::IsNullOrWhiteSpace($markerText) -and -not $content.Contains($markerText)) {
                Stop-Developer "infrastructure smoke approved output file is missing an expected content marker."
            }
        }
    }

    $manifestOut = [pscustomobject]@{
        schemaVersion = "1.0"
        taskId = $Manifest.taskId
        classification = $Manifest.classification
        repository = $Repository
        approvedBaseSha = $ExpectedBaseSha
        developerWorkflowRunId = [string]$env:GITHUB_RUN_ID
        developerWorkflowRunAttempt = [string]$env:GITHUB_RUN_ATTEMPT
        developerContextId = [string]$State.implementationContextId
        targetBranch = [string]$Manifest.repositoryTransport.targetBranch
        files = @($files)
        productOwnerManualTransport = $false
        repositoryWriteCredentialAvailableToDeveloper = $false
        generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    }

    $manifestPath = Join-Path $Destination "publication-package.manifest.json"
    $manifestOut | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding utf8

    $zipPath = Join-Path $Destination "publication-package.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -Force

    $result = [pscustomobject]@{
        taskId = $Manifest.taskId
        approvedBaseSha = $ExpectedBaseSha
        changedFiles = @($files)
        packagePath = $zipPath
        packageSha256 = Get-FileSha256 -Path $zipPath
        manifestPath = $manifestPath
        manifestSha256 = Get-FileSha256 -Path $manifestPath
        repositoryWriteCredentialAvailableToDeveloper = $false
        productOwnerManualTransport = $false
    }

    $resultPath = Join-Path $Destination "publication-package.result.json"
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultPath -Encoding utf8
    return $result
}

function Get-ChangedFiles {
    $changed = @()
    $changed += Invoke-Git -Arguments @("diff", "--name-only", "HEAD", "--")
    $changed += Invoke-Git -Arguments @("ls-files", "--others", "--exclude-standard")
    return @($changed | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -Unique)
}

function ConvertTo-SafeDiagnosticString {
    param([AllowNull()][object] $Value)

    if ($null -eq $Value) {
        return "UNKNOWN"
    }

    $text = ([string]$Value).Trim()
    if ([string]::IsNullOrWhiteSpace($text)) {
        return "UNKNOWN"
    }

    if ($text.Length -gt 120) {
        return "REDACTED"
    }

    if ($text -match '(?i)(token|secret|password|private[-_ ]?key|authorization|bearer|oauth|pat|api[-_ ]?key)' -or
        $text -match '://.*@' -or
        $text -match '[A-Za-z0-9_=-]{32,}') {
        return "REDACTED"
    }

    if ($text -notmatch '^[A-Za-z0-9 ._:/()@,+-]+$') {
        return "REDACTED"
    }

    return $text
}

function Add-SafeUniqueValue {
    param(
        [AllowNull()][object] $Values,
        [AllowNull()][object] $Value
    )

    if ($null -eq $Values) {
        return
    }

    $safe = ConvertTo-SafeDiagnosticString -Value $Value
    if ($safe -ne "UNKNOWN" -and -not $Values.Contains($safe)) {
        $Values.Add($safe) | Out-Null
    }
}

function Get-JsonObjectsFromExecutionFile {
    param([Parameter(Mandatory = $true)][string] $Path)

    $objects = @()
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return @()
    }

    try {
        $raw = Get-Content -LiteralPath $Path -Raw
        if (-not [string]::IsNullOrWhiteSpace($raw)) {
            $parsed = $raw | ConvertFrom-Json
            foreach ($item in @($parsed)) {
                $objects += $item
            }
            return @($objects)
        }
    } catch {
        $objects = @()
    }

    try {
        foreach ($line in @(Get-Content -LiteralPath $Path)) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }
            try {
                $objects += ($line | ConvertFrom-Json)
            } catch {
                return @()
            }
        }
    } catch {
        return @()
    }

    return @($objects)
}

function Get-PropertyValue {
    param(
        [AllowNull()][object] $Object,
        [Parameter(Mandatory = $true)][string[]] $Names
    )

    if ($null -eq $Object) {
        return $null
    }

    foreach ($name in $Names) {
        $property = $Object.PSObject.Properties[$name]
        if ($null -ne $property) {
            return $property.Value
        }
    }

    return $null
}

function New-ClaudeExecutionAccumulator {
    [pscustomobject]@{
        toolNames = New-Object System.Collections.ArrayList
        toolStatuses = New-Object System.Collections.ArrayList
        deniedTools = New-Object System.Collections.ArrayList
        denialCategories = New-Object System.Collections.ArrayList
        sanitizedDenialReasons = New-Object System.Collections.ArrayList
        writeAttempted = $false
        editAttempted = $false
        bashAttempted = $false
        claudeTurnCount = "UNKNOWN"
        finalResultSubtype = "UNKNOWN"
        parseStatus = "PARSED"
        maxDepthReached = $false
        maxNodesReached = $false
        inspectedNodeCount = 0
    }
}

function Visit-ClaudeExecutionKnownFields {
    param(
        [AllowNull()][object] $Node,
        [Parameter(Mandatory = $true)] $Accumulator
    )

    $type = Get-PropertyValue -Object $Node -Names @("type", "event", "kind")
    $name = Get-PropertyValue -Object $Node -Names @("name", "tool_name", "toolName", "tool")
    $status = Get-PropertyValue -Object $Node -Names @("status", "result", "outcome", "subtype")
    $error = Get-PropertyValue -Object $Node -Names @("error", "reason", "message", "category")
    $turnCount = Get-PropertyValue -Object $Node -Names @("num_turns", "numTurns", "turn_count", "turnCount")

    $safeType = ConvertTo-SafeDiagnosticString -Value $type
    $safeName = ConvertTo-SafeDiagnosticString -Value $name
    $safeStatus = ConvertTo-SafeDiagnosticString -Value $status
    $safeError = ConvertTo-SafeDiagnosticString -Value $error

    if ($safeName -ne "UNKNOWN") {
        Add-SafeUniqueValue -Values $Accumulator.toolNames -Value $safeName
        if ($safeName -match '(?i)^write$|^strreplaceeditor$|^multi_edit$|^edit$') {
            $Accumulator.writeAttempted = $true
        }
        if ($safeName -match '(?i)^edit$|^multi_edit$|^strreplaceeditor$') {
            $Accumulator.editAttempted = $true
        }
        if ($safeName -match '(?i)^bash$') {
            $Accumulator.bashAttempted = $true
        }
    }

    if ($safeStatus -ne "UNKNOWN") {
        Add-SafeUniqueValue -Values $Accumulator.toolStatuses -Value $safeStatus
    }

    if ($safeType -eq "result" -and $safeStatus -ne "UNKNOWN") {
        $Accumulator.finalResultSubtype = $safeStatus
    }

    if ($null -ne $turnCount -and ([string]$turnCount) -match '^\d+$') {
        $Accumulator.claudeTurnCount = [string]$turnCount
    }

    if (($safeType -match '(?i)denied|permission') -or
        ($safeStatus -match '(?i)denied|permission|rejected') -or
        ($safeError -match '(?i)denied|permission|rejected')) {
        if ($safeName -ne "UNKNOWN") {
            Add-SafeUniqueValue -Values $Accumulator.deniedTools -Value $safeName
        }
        if ($safeStatus -ne "UNKNOWN") {
            Add-SafeUniqueValue -Values $Accumulator.denialCategories -Value $safeStatus
        } elseif ($safeType -ne "UNKNOWN") {
            Add-SafeUniqueValue -Values $Accumulator.denialCategories -Value $safeType
        }
        Add-SafeUniqueValue -Values $Accumulator.sanitizedDenialReasons -Value $safeError
    }
}

function Visit-ClaudeExecutionNode {
    param(
        [AllowNull()][object] $Node,
        [Parameter(Mandatory = $true)] $Accumulator,
        [int] $Depth = 0,
        [int] $MaxDepth = 8,
        [int] $MaxNodes = 2000,
        [int] $MaxArrayItems = 200
    )

    if ($null -eq $Node) {
        return
    }

    if ($Accumulator.inspectedNodeCount -ge $MaxNodes) {
        $Accumulator.maxNodesReached = $true
        return
    }
    $Accumulator.inspectedNodeCount++

    if ($Depth -gt $MaxDepth) {
        $Accumulator.maxDepthReached = $true
        return
    }

    if ($Node -is [string] -or $Node.GetType().IsPrimitive) {
        return
    }

    if ($Node -is [System.Collections.IEnumerable] -and -not ($Node -is [System.Collections.IDictionary]) -and -not ($Node -is [pscustomobject])) {
        $visited = 0
        foreach ($item in $Node) {
            if ($visited -ge $MaxArrayItems) {
                $Accumulator.maxNodesReached = $true
                break
            }
            Visit-ClaudeExecutionNode -Node $item -Accumulator $Accumulator -Depth ($Depth + 1) -MaxDepth $MaxDepth -MaxNodes $MaxNodes -MaxArrayItems $MaxArrayItems
            $visited++
        }
        return
    }

    Visit-ClaudeExecutionKnownFields -Node $Node -Accumulator $Accumulator

    foreach ($property in @($Node.PSObject.Properties)) {
        if ($Accumulator.inspectedNodeCount -ge $MaxNodes) {
            $Accumulator.maxNodesReached = $true
            break
        }
        $propertyName = [string]$property.Name
        if ($propertyName -match '(?i)prompt|content|text|input|output|stdout|stderr|transcript|environment|env') {
            continue
        }
        Visit-ClaudeExecutionNode -Node $property.Value -Accumulator $Accumulator -Depth ($Depth + 1) -MaxDepth $MaxDepth -MaxNodes $MaxNodes -MaxArrayItems $MaxArrayItems
    }
}

function Get-SanitizedClaudeExecutionDiagnostics {
    param(
        [Parameter(Mandatory = $true)] $Manifest,
        [Parameter(Mandatory = $true)][string] $ExecutionFile
    )

    $objects = @(Get-JsonObjectsFromExecutionFile -Path $ExecutionFile)
    $accumulator = New-ClaudeExecutionAccumulator

    foreach ($object in $objects) {
        try {
            Visit-ClaudeExecutionNode -Node $object -Accumulator $accumulator
        } catch {
            $accumulator.parseStatus = "UNKNOWN"
            break
        }
    }

    [pscustomobject]@{
        schemaVersion = "1.0"
        taskId = [string]$Manifest.taskId
        workflowRunId = [string]$env:GITHUB_RUN_ID
        runAttempt = [string]$env:GITHUB_RUN_ATTEMPT
        claudeSessionId = ConvertTo-SafeDiagnosticString -Value $ClaudeSessionId
        claudeConclusion = ConvertTo-SafeDiagnosticString -Value $ClaudeConclusion
        claudeTurnCount = $accumulator.claudeTurnCount
        toolNames = @($accumulator.toolNames)
        toolStatuses = @($accumulator.toolStatuses)
        writeAttempted = $accumulator.writeAttempted
        editAttempted = $accumulator.editAttempted
        bashAttempted = $accumulator.bashAttempted
        permissionDenialCount = ConvertTo-SafeDiagnosticString -Value $ClaudePermissionDenialCount
        deniedTools = @($accumulator.deniedTools)
        denialCategories = @($accumulator.denialCategories)
        sanitizedDenialReasons = @($accumulator.sanitizedDenialReasons)
        finalResultSubtype = $accumulator.finalResultSubtype
        parseStatus = $accumulator.parseStatus
        maxDepthReached = $accumulator.maxDepthReached
        maxNodesReached = $accumulator.maxNodesReached
        inspectedNodeCount = $accumulator.inspectedNodeCount
        rawExecutionOutputUploaded = $false
    }
}

function Get-PostClaudeDiagnostics {
    param(
        [Parameter(Mandatory = $true)] $Manifest,
        [Parameter(Mandatory = $true)] $State
    )

    $repoRoot = (Invoke-Git -Arguments @("rev-parse", "--show-toplevel")).Trim()
    $allowedFiles = @(Get-AllowedFiles -Manifest $Manifest)
    $approvedOutputPath = ""
    if ($null -ne $Manifest.smokeFixture -and -not [string]::IsNullOrWhiteSpace([string]$Manifest.smokeFixture.approvedOutputPath)) {
        $approvedOutputPath = Assert-RepoRelativePath -Path ([string]$Manifest.smokeFixture.approvedOutputPath) -Name "smokeFixture.approvedOutputPath"
    } elseif ($allowedFiles.Count -eq 1) {
        $approvedOutputPath = $allowedFiles[0]
    }

    $authorizedFile = $null
    $approvedBasenameMatches = @()
    if (-not [string]::IsNullOrWhiteSpace($approvedOutputPath)) {
        $authorizedFullPath = Join-Path $repoRoot $approvedOutputPath
        $exists = Test-Path -LiteralPath $authorizedFullPath -PathType Leaf
        $item = $null
        $sha256 = ""
        $length = $null
        if ($exists) {
            $item = Get-Item -LiteralPath $authorizedFullPath -Force
            $length = $item.Length
            $sha256 = Get-FileSha256 -Path $authorizedFullPath
        }

        $authorizedFile = [pscustomobject]@{
            path = $approvedOutputPath
            exists = $exists
            isFile = $exists
            size = $length
            sha256 = $sha256
        }

        $basename = Split-Path -Leaf $approvedOutputPath
        if (-not [string]::IsNullOrWhiteSpace($basename)) {
            $approvedBasenameMatches = @(
                Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Force -Filter $basename |
                    Where-Object { $_.FullName -notlike (Join-Path $repoRoot ".git*") } |
                    ForEach-Object {
                        $relative = [IO.Path]::GetRelativePath($repoRoot, $_.FullName).Replace('\', '/')
                        [pscustomobject]@{
                            path = $relative
                            size = $_.Length
                            sha256 = Get-FileSha256 -Path $_.FullName
                        }
                    }
            )
        }
    }

    $sanitizedClaudeExecution = Get-SanitizedClaudeExecutionDiagnostics -Manifest $Manifest -ExecutionFile $ClaudeExecutionFile

    $diagnostics = [pscustomobject]@{
        schemaVersion = "1.0"
        taskId = $Manifest.taskId
        repository = $Repository
        expectedBaseSha = $ExpectedBaseSha
        currentDirectory = (Get-Location).Path
        repositoryRoot = $repoRoot
        gitStatusShort = @(Invoke-Git -Arguments @("status", "--short", "--untracked-files=all"))
        gitDiffNameOnly = @(Invoke-Git -Arguments @("diff", "--name-only", "HEAD", "--"))
        gitUntrackedFiles = @(Invoke-Git -Arguments @("ls-files", "--others", "--exclude-standard"))
        gitIgnoredFiles = @(Invoke-Git -Arguments @("ls-files", "--others", "--ignored", "--exclude-standard"))
        changedFilesDetectedByPackager = @(Get-ChangedFiles)
        approvedFiles = @($allowedFiles)
        authorizedOutputFile = $authorizedFile
        approvedBasenameSearch = [pscustomobject]@{
            scope = "repository-root"
            excludes = @(".git")
            matches = @($approvedBasenameMatches)
        }
        claude = [pscustomobject]@{
            conclusion = $ClaudeConclusion
            sessionId = $ClaudeSessionId
            executionFile = $ClaudeExecutionFile
            permissionDenialCount = $ClaudePermissionDenialCount
        }
        secretBoundary = [pscustomobject]@{
            environmentDumped = $false
            fileContentsLogged = $false
            fullClaudeTranscriptLogged = $false
            rawClaudeExecutionOutputUploaded = $false
        }
        sanitizedClaudeExecution = $sanitizedClaudeExecution
    }

    if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
        New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
        $diagnosticPath = Join-Path $OutputDirectory "post-claude-diagnostics.sanitized.json"
        $diagnostics | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $diagnosticPath -Encoding utf8
    }

    return $diagnostics
}

if (($ValidateOnly.IsPresent + $PreparePrompt.IsPresent + $Package.IsPresent + $DiagnosePostClaude.IsPresent) -gt 1) {
    Stop-Developer "Specify only one mode: ValidateOnly, PreparePrompt, Package, or DiagnosePostClaude."
}

if (-not ($ValidateOnly -or $PreparePrompt -or $Package -or $DiagnosePostClaude)) {
    Stop-Developer "Specify ValidateOnly, PreparePrompt, Package, or DiagnosePostClaude."
}

$manifestPathNormalized = Assert-RepoRelativePath -Path $ManifestPath -Name "ManifestPath"
$statePathNormalized = Assert-RepoRelativePath -Path $StatePath -Name "StatePath"
$manifest = Read-JsonFile -Path $manifestPathNormalized
$state = Read-JsonFile -Path $statePathNormalized
Assert-TrustedTask -Manifest $manifest -State $state

if ($ValidateOnly) {
    [pscustomobject]@{
        taskId = $manifest.taskId
        classification = $manifest.classification
        repository = $Repository
        expectedBaseSha = $ExpectedBaseSha
        claudePrimaryDeveloper = $true
        repositoryWriteCredentialAvailableToDeveloper = $false
        readyToInvokeClaude = $true
    } | ConvertTo-Json -Depth 8
    return
}

if ($PreparePrompt) {
    if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
        Stop-Developer "OutputDirectory is required when PreparePrompt is set."
    }
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $promptPath = Join-Path $OutputDirectory "claude-developer-prompt.md"
    Write-DeveloperPrompt -Manifest $manifest -State $state -PromptPath $promptPath
    [pscustomobject]@{
        taskId = $manifest.taskId
        promptPath = $promptPath
        repositoryWriteCredentialAvailableToDeveloper = $false
    } | ConvertTo-Json -Depth 8
    return
}

if ($DiagnosePostClaude) {
    Get-PostClaudeDiagnostics -Manifest $manifest -State $state | ConvertTo-Json -Depth 12
    return
}

if ($Package) {
    New-PublicationPackage -Manifest $manifest -State $state -Destination $OutputDirectory | ConvertTo-Json -Depth 12
}
