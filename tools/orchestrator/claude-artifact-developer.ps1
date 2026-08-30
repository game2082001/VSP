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

    [switch] $Package
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
    $approvedFiles = (Get-AllowedFiles -Manifest $Manifest) -join "`n- "

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

Allowed publication files:
- $approvedFiles

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

if ($ValidateOnly -and ($PreparePrompt -or $Package)) {
    Stop-Developer "ValidateOnly cannot be combined with PreparePrompt or Package."
}

if (-not ($ValidateOnly -or $PreparePrompt -or $Package)) {
    Stop-Developer "Specify ValidateOnly, PreparePrompt, or Package."
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

if ($Package) {
    New-PublicationPackage -Manifest $manifest -State $state -Destination $OutputDirectory | ConvertTo-Json -Depth 12
}
