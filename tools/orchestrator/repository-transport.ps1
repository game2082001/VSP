param(
    [Parameter(Mandatory = $true)]
    [string] $RequestPath,

    [Parameter(Mandatory = $true)]
    [string] $Repository,

    [Parameter(Mandatory = $true)]
    [string] $WorkflowRunId,

    [Parameter(Mandatory = $true)]
    [string] $WorkflowRunAttempt,

    [string] $AppSlug = "",

    [switch] $ValidateOnly,

    [switch] $Publish
)

$ErrorActionPreference = "Stop"

function Stop-Transport {
    param([Parameter(Mandatory = $true)][string] $Reason)
    throw "AI02 repository transport rejected request: $Reason"
}

function Read-Json {
    param([Parameter(Mandatory = $true)][string] $Path)
    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    } catch {
        Stop-Transport "Invalid JSON at $Path. $($_.Exception.Message)"
    }
}

function Assert-RepoPath {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][string] $Name
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        Stop-Transport "$Name is required."
    }
    if ($Path.Contains("\")) {
        Stop-Transport "$Name must use repository-relative forward slashes: $Path"
    }
    if ($Path.StartsWith("/") -or $Path.StartsWith("../") -or $Path.Contains("/../") -or $Path -eq "..") {
        Stop-Transport "$Name must not escape the repository: $Path"
    }
    if ($Path -match "^[A-Za-z]:") {
        Stop-Transport "$Name must not be an absolute path: $Path"
    }
    if ($Path -notmatch "^[A-Za-z0-9._/-]+$" -or $Path.Contains("//")) {
        Stop-Transport "$Name contains unsupported repository path characters: $Path"
    }
}

function Assert-String {
    param($Value, [string] $Name)
    if ($null -eq $Value -or -not ($Value -is [string]) -or [string]::IsNullOrWhiteSpace($Value)) {
        Stop-Transport "$Name is required."
    }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][byte[]] $Bytes)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return (($sha.ComputeHash($Bytes) | ForEach-Object { $_.ToString("x2") }) -join "")
    } finally {
        $sha.Dispose()
    }
}

function ConvertFrom-Base64Bytes {
    param([Parameter(Mandatory = $true)][string] $Value, [Parameter(Mandatory = $true)][string] $Name)
    try {
        return [Convert]::FromBase64String($Value)
    } catch {
        Stop-Transport "$Name is not valid base64."
    }
}

function Assert-Branch {
    param([Parameter(Mandatory = $true)][string] $Branch)
    if ($Branch -eq "main") {
        Stop-Transport "Direct writes to main are prohibited."
    }
    if ($Branch.StartsWith("refs/") -or $Branch.StartsWith("tags/") -or $Branch.Contains("..") -or $Branch.Contains("~") -or $Branch.Contains("^") -or $Branch.Contains(":")) {
        Stop-Transport "Unsafe branch ref: $Branch"
    }
    if ($Branch -notmatch "^ai02/[a-z0-9][a-z0-9._-]*/[a-z0-9][a-z0-9._-]*$") {
        Stop-Transport "Branch must match ai02/<task>/<purpose>: $Branch"
    }
}

function Assert-Request {
    param($Request)

    Assert-String $Request.schemaVersion "schemaVersion"
    if ($Request.schemaVersion -ne "1.0") {
        Stop-Transport "Unsupported schemaVersion $($Request.schemaVersion)."
    }

    Assert-String $Request.taskId "taskId"
    Assert-String $Request.repository "repository"
    Assert-String $Request.baseSha "baseSha"
    Assert-String $Request.targetBranch "targetBranch"
    Assert-String $Request.commitMessage "commitMessage"
    Assert-String $Request.manifestPath "manifestPath"
    Assert-String $Request.statePath "statePath"
    Assert-String $Request.title "title"

    if ($Request.repository -ne $Repository -or $Request.repository -ne "game2082001/VSP") {
        Stop-Transport "Repository must be exactly game2082001/VSP."
    }
    if ($Request.baseSha -notmatch "^[0-9a-f]{40}$") {
        Stop-Transport "baseSha must be a 40-character lowercase SHA."
    }
    if ($Request.commitMessage -notmatch [regex]::Escape($Request.taskId)) {
        Stop-Transport "commitMessage must include taskId."
    }

    Assert-Branch $Request.targetBranch
    Assert-RepoPath $Request.manifestPath "manifestPath"
    Assert-RepoPath $Request.statePath "statePath"

    if ($Request.PSObject.Properties["allowWorkflowChanges"] -eq $null -or -not ($Request.allowWorkflowChanges -is [bool])) {
        Stop-Transport "allowWorkflowChanges must be boolean."
    }
    if ($Request.PSObject.Properties["openPullRequest"] -eq $null -or -not ($Request.openPullRequest -is [bool])) {
        Stop-Transport "openPullRequest must be boolean."
    }

    $approved = @($Request.approvedFiles)
    $files = @($Request.files)
    if ($approved.Count -eq 0 -or $files.Count -eq 0) {
        Stop-Transport "approvedFiles and files are required."
    }
    if ($approved.Count -ne $files.Count) {
        Stop-Transport "approvedFiles must exactly match files."
    }

    $seen = @{}
    foreach ($path in $approved) {
        Assert-RepoPath ([string]$path) "approvedFiles"
        if ($seen.ContainsKey([string]$path)) {
            Stop-Transport "approvedFiles contains duplicate path: $path"
        }
        $seen[[string]$path] = $true
    }

    $fileSeen = @{}

    foreach ($file in $files) {
        Assert-String $file.path "file.path"
        Assert-RepoPath $file.path "file.path"
        if ($fileSeen.ContainsKey([string]$file.path)) {
            Stop-Transport "files contains duplicate path: $($file.path)"
        }
        $fileSeen[[string]$file.path] = $true
        if (-not $seen.ContainsKey([string]$file.path)) {
            Stop-Transport "File not in approved allowlist: $($file.path)"
        }
        if ($file.path.StartsWith(".github/workflows/") -and $Request.allowWorkflowChanges -ne $true) {
            Stop-Transport "Workflow change is not authorized: $($file.path)"
        }
        if ($file.mode -ne "100644") {
            Stop-Transport "Only 100644 file mode is supported: $($file.path)"
        }
        Assert-String $file.contentBase64 "file.contentBase64"
        Assert-String $file.sha256 "file.sha256"
        if ($file.sha256 -notmatch "^[0-9a-f]{64}$") {
            Stop-Transport "file.sha256 must be lowercase SHA-256: $($file.path)"
        }
        $bytes = ConvertFrom-Base64Bytes $file.contentBase64 "contentBase64 for $($file.path)"
        $actual = Get-Sha256Hex $bytes
        if ($actual -ne $file.sha256) {
            Stop-Transport "Content hash mismatch for $($file.path)."
        }
    }
}

function Invoke-GhJson {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)
    $json = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        Stop-Transport "GitHub CLI command failed: gh $($Arguments -join ' ')"
    }
    if ([string]::IsNullOrWhiteSpace($json)) {
        return $null
    }
    return $json | ConvertFrom-Json
}

function Assert-AppScope {
    $repos = @(gh api /installation/repositories --jq ".repositories[].full_name")
    if ($LASTEXITCODE -ne 0) {
        Stop-Transport "Unable to validate GitHub App installation repositories."
    }
    if ($repos.Count -ne 1 -or $repos[0] -ne "game2082001/VSP") {
        Stop-Transport "Implementation token scope is not exactly game2082001/VSP."
    }
}

function ConvertTo-Utf8Text {
    param([Parameter(Mandatory = $true)][byte[]] $Bytes)
    return [System.Text.Encoding]::UTF8.GetString($Bytes)
}

$request = Read-Json $RequestPath
Assert-Request $request

if ($WorkflowRunAttempt -ne "1") {
    Stop-Transport "Publication workflow must run on attempt 1."
}

$manifest = Read-Json $request.manifestPath
$state = Read-Json $request.statePath
if ($manifest.taskId -ne $request.taskId -or $state.taskId -ne $request.taskId) {
    Stop-Transport "Manifest/state taskId must match request taskId."
}
if ($manifest.repository -ne $request.repository -or $state.repository -ne $request.repository) {
    Stop-Transport "Manifest/state repository must match request repository."
}
if ($manifest.productOwnerAuthorization.authorized -ne $true) {
    Stop-Transport "Product Owner authorization is missing from manifest."
}
if ($state.developerEqualsReviewer -eq $true) {
    Stop-Transport "Developer/reviewer separation violation."
}
if ($manifest.repositoryTransport.required -ne $true) {
    Stop-Transport "Manifest does not authorize repository transport."
}
if ($state.repositoryTransport.required -ne $true) {
    Stop-Transport "State does not require repository transport."
}
if ($request.allowWorkflowChanges -eq $true -and $manifest.repositoryTransport.allowWorkflowChanges -ne $true) {
    Stop-Transport "Manifest does not authorize workflow file changes."
}
if ($request.openPullRequest -eq $true -and $manifest.repositoryTransport.openPullRequest -ne $true) {
    Stop-Transport "Manifest does not authorize pull request creation."
}
$manifestApprovedFiles = @($manifest.repositoryTransport.approvedFiles | ForEach-Object { [string]$_ } | Sort-Object)
$requestApprovedFiles = @($request.approvedFiles | ForEach-Object { [string]$_ } | Sort-Object)
if ($manifestApprovedFiles.Count -eq 0) {
    Stop-Transport "Manifest repository transport approvedFiles is required."
}
if ($manifestApprovedFiles.Count -ne $requestApprovedFiles.Count) {
    Stop-Transport "Request approvedFiles must match manifest approvedFiles."
}
for ($i = 0; $i -lt $manifestApprovedFiles.Count; $i++) {
    if ($manifestApprovedFiles[$i] -ne $requestApprovedFiles[$i]) {
        Stop-Transport "Request approvedFiles must match manifest approvedFiles."
    }
}

$localBase = (git rev-parse $request.baseSha).Trim()
if ($localBase -ne $request.baseSha) {
    Stop-Transport "Approved base SHA is not present in checkout."
}
$remoteMain = (gh api "repos/$Repository/branches/main" --jq ".commit.sha")
if ($LASTEXITCODE -ne 0) {
    Stop-Transport "Unable to read remote main."
}
if ($remoteMain -ne $request.baseSha) {
    Stop-Transport "Base drift: remote main is $remoteMain, request base is $($request.baseSha)."
}

if ($ValidateOnly) {
    [pscustomobject]@{
        taskId = $request.taskId
        repository = $request.repository
        targetBranch = $request.targetBranch
        baseSha = $request.baseSha
        fileCount = @($request.files).Count
        workflowChanges = [bool]($request.files | Where-Object { $_.path.StartsWith(".github/workflows/") } | Select-Object -First 1)
        validation = "VALID"
        publish = $false
    } | ConvertTo-Json -Depth 6
    exit 0
}

if (-not $Publish) {
    Stop-Transport "Specify -ValidateOnly or -Publish."
}

Assert-AppScope

$baseCommit = Invoke-GhJson @("api", "repos/$Repository/git/commits/$($request.baseSha)")
$baseTreeSha = [string]$baseCommit.tree.sha

$treeEntries = @()
foreach ($file in @($request.files)) {
    $bytes = ConvertFrom-Base64Bytes $file.contentBase64 "contentBase64 for $($file.path)"
    $treeEntries += [pscustomobject]@{
        path = [string]$file.path
        mode = "100644"
        type = "blob"
        content = ConvertTo-Utf8Text $bytes
    }
}

$treePayload = [pscustomobject]@{
    base_tree = $baseTreeSha
    tree = $treeEntries
} | ConvertTo-Json -Depth 8
$treeFile = Join-Path $env:RUNNER_TEMP "ai02-transport-tree.json"
$treePayload | Set-Content -LiteralPath $treeFile -Encoding utf8
$tree = Invoke-GhJson @("api", "repos/$Repository/git/trees", "--method", "POST", "--input", $treeFile)

$commitPayload = [pscustomobject]@{
    message = [string]$request.commitMessage
    tree = [string]$tree.sha
    parents = @([string]$request.baseSha)
} | ConvertTo-Json -Depth 5
$commitFile = Join-Path $env:RUNNER_TEMP "ai02-transport-commit.json"
$commitPayload | Set-Content -LiteralPath $commitFile -Encoding utf8
$commit = Invoke-GhJson @("api", "repos/$Repository/git/commits", "--method", "POST", "--input", $commitFile)

$refName = "heads/$($request.targetBranch)"
$existingRef = gh api "repos/$Repository/git/ref/$refName" 2>$null
if ($LASTEXITCODE -eq 0) {
    $patchPayload = [pscustomobject]@{
        sha = [string]$commit.sha
        force = $false
    } | ConvertTo-Json
    $patchFile = Join-Path $env:RUNNER_TEMP "ai02-transport-ref.json"
    $patchPayload | Set-Content -LiteralPath $patchFile -Encoding utf8
    Invoke-GhJson @("api", "repos/$Repository/git/refs/$refName", "--method", "PATCH", "--input", $patchFile) | Out-Null
} else {
    $refPayload = [pscustomobject]@{
        ref = "refs/$refName"
        sha = [string]$commit.sha
    } | ConvertTo-Json
    $refFile = Join-Path $env:RUNNER_TEMP "ai02-transport-ref.json"
    $refPayload | Set-Content -LiteralPath $refFile -Encoding utf8
    Invoke-GhJson @("api", "repos/$Repository/git/refs", "--method", "POST", "--input", $refFile) | Out-Null
}

$compare = Invoke-GhJson @("api", "repos/$Repository/compare/$($request.baseSha)...$($request.targetBranch)")
$remoteFiles = @($compare.files | ForEach-Object { $_.filename })
$expectedFiles = @($request.files | ForEach-Object { $_.path })
$unexpected = @($remoteFiles | Where-Object { $expectedFiles -notcontains $_ })
$missing = @($expectedFiles | Where-Object { $remoteFiles -notcontains $_ })
if ($unexpected.Count -gt 0 -or $missing.Count -gt 0) {
    Stop-Transport "Remote changed-file set mismatch. Unexpected=$($unexpected -join ',') Missing=$($missing -join ',')"
}
if ($compare.commits.Count -ne 1) {
    Stop-Transport "Publication must produce exactly one commit in compare range."
}
if ($compare.commits[0].sha -ne [string]$commit.sha) {
    Stop-Transport "Compare commit does not match created commit."
}

foreach ($file in @($request.files)) {
    $remote = Invoke-GhJson @("api", "repos/$Repository/contents/$($file.path)?ref=$($request.targetBranch)")
    $remoteBytes = [Convert]::FromBase64String(([string]$remote.content).Replace("`n", ""))
    $actualHash = Get-Sha256Hex $remoteBytes
    if ($actualHash -ne $file.sha256) {
        Stop-Transport "Remote content hash mismatch for $($file.path)."
    }
}

$prNumber = 0
if ($request.openPullRequest -eq $true) {
    $existingPr = gh pr view $request.targetBranch --repo $Repository --json number,state 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($existingPr)) {
        $pr = $existingPr | ConvertFrom-Json
        $prNumber = [int]$pr.number
    } else {
        $body = @"
AI02 Repository Transport publication.

Task: $($request.taskId)
Base: $($request.baseSha)
Branch: $($request.targetBranch)
Commit: $($commit.sha)
Merged: false
"@
        $created = gh pr create --repo $Repository --base main --head $request.targetBranch --title $request.title --body $body --json number
        if ($LASTEXITCODE -ne 0) {
            Stop-Transport "Pull request creation failed."
        }
        $prNumber = [int](($created | ConvertFrom-Json).number)
    }
}

$result = [pscustomobject]@{
    schemaVersion = "1.0"
    taskId = $request.taskId
    repository = $request.repository
    approvedBaseSha = $request.baseSha
    targetBranch = $request.targetBranch
    treeSha = [string]$tree.sha
    commitSha = [string]$commit.sha
    prNumber = $prNumber
    appSlug = $AppSlug
    workflowRunId = $WorkflowRunId
    workflowRunAttempt = $WorkflowRunAttempt
    remoteTreeMatchesRequest = $true
    singleAtomicCommit = $true
    productOwnerManualTransport = $false
    agentCredentialExposure = $false
    merged = $false
    publishedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
}

$resultPath = "AI/Orchestrator/Transport/publication-result.json"
$resultDir = Split-Path -Parent $resultPath
if (-not (Test-Path -LiteralPath $resultDir)) {
    New-Item -ItemType Directory -Path $resultDir | Out-Null
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
$result | ConvertTo-Json -Depth 8
