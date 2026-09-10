param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("ValidateDescriptor", "InitializeWorkspace", "ValidateChildAuthorization", "MaterializeLocal", "BuildAggregateState", "AppendDescriptor", "AcquireAndMaterialize", "ValidatePostClaude", "PackageChild", "NewDescriptor", "ValidateFinalAggregate")]
    [string] $Mode,

    [string] $Repository = "game2082001/VSP",
    [string] $RecoveryRepositorySha = "",
    [string] $WorkspacePath = "",
    [string] $BaselinePath = "",
    [string] $AggregateStatePath = "",
    [string] $DescriptorPath = "",
    [string] $ArtifactDirectory = "",
    [string] $BindingsJson = "[]",
    [string] $ManifestPath = "",
    [string] $OutputDirectory = "",
    [string] $ChildTaskId = "",
    [ValidateSet("", "A1", "A2", "A3")]
    [string] $ChildPhase = "",
    [int] $Sequence = 0,
    [string] $ParentAggregateStateDigest = "",
    [string] $WorkflowId = "",
    [string] $RunId = "",
    [int] $RunAttempt = 0,
    [string] $ArtifactId = "",
    [string] $ArtifactName = "",
    [string] $GitHubArtifactDigest = ""
    ,[string[]] $DescriptorPaths = @()
    ,[string[]] $ArtifactDirectories = @()
    ,[string] $ExpectedBaselineSha256 = ""
    ,[string] $ExpectedAggregateStateSha256 = ""
)

$ErrorActionPreference = "Stop"

$Script:MaxOuterArtifactCompressedBytes = 26214400
$Script:MaxInnerPublicationZipCompressedBytes = 20971520
$Script:MaxTotalInnerUncompressedBytes = 52428800
$Script:MaxChangedFiles = 200
$Script:MaxPerFileUncompressedBytes = 10485760
$Script:MaxManifestJsonBytes = 262144
$Script:MaxResultJsonBytes = 262144
$Script:MaxRepositoryRelativePathCharacters = 240
$Script:MaxPathSegmentCharacters = 100
$Script:MaxJsonNestingDepth = 16
$Script:MaxCompressionRatio = 100
$Script:RequiredOuterPublicationFileCount = 3

$Script:PhaseFiles = [ordered]@{
    A1 = @(
        "AI/Orchestrator/Templates/artifact-intake-decision.schema.json",
        "AI/Orchestrator/Templates/artifact-intake-decision.template.json",
        "AI/Orchestrator/Templates/artifact-intake-request.schema.json",
        "AI/Orchestrator/Templates/artifact-intake-request.template.json"
    )
    A2 = @("tools/orchestrator/artifact-intake-contract.ps1")
    A3 = @(
        "AI/Orchestrator/ARTIFACT_INTAKE_SCHEMA.md",
        "tools/orchestrator/test-artifact-intake-contract.ps1"
    )
}

$Script:FinalFiles = @(
    "AI/Orchestrator/ARTIFACT_INTAKE_SCHEMA.md",
    "AI/Orchestrator/Templates/artifact-intake-decision.schema.json",
    "AI/Orchestrator/Templates/artifact-intake-decision.template.json",
    "AI/Orchestrator/Templates/artifact-intake-request.schema.json",
    "AI/Orchestrator/Templates/artifact-intake-request.template.json",
    "tools/orchestrator/artifact-intake-contract.ps1",
    "tools/orchestrator/test-artifact-intake-contract.ps1"
)

function Stop-Chain {
    param([Parameter(Mandatory = $true)][string] $Message)
    throw "AI02 artifact chain validation failed: $Message"
}

function Assert-NonBlank {
    param([AllowNull()][object] $Value, [Parameter(Mandatory = $true)][string] $Name)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        Stop-Chain "$Name is required."
    }
}

function Assert-Matches {
    param([AllowNull()][object] $Value, [Parameter(Mandatory = $true)][string] $Pattern, [Parameter(Mandatory = $true)][string] $Name)
    Assert-NonBlank -Value $Value -Name $Name
    if ([string]$Value -cnotmatch $Pattern) {
        Stop-Chain "$Name has an invalid value."
    }
}

function Assert-ExactProperties {
    param([Parameter(Mandatory = $true)] $Object, [Parameter(Mandatory = $true)][string[]] $Names, [Parameter(Mandatory = $true)][string] $Name)
    Assert-ExactSet @($Object.PSObject.Properties.Name) $Names "$Name properties"
}

function Assert-AllowedProperties {
    param([Parameter(Mandatory = $true)] $Object, [Parameter(Mandatory = $true)][string[]] $Required, [Parameter(Mandatory = $true)][string[]] $Allowed, [Parameter(Mandatory = $true)][string] $Name)
    $actual = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($property in @($Object.PSObject.Properties.Name)) { if (-not $actual.Add([string]$property)) { Stop-Chain "$Name contains duplicate properties." } }
    $allowedSet = [Collections.Generic.HashSet[string]]::new($Allowed, [StringComparer]::Ordinal)
    foreach ($requiredName in $Required) { if (-not $actual.Contains($requiredName)) { Stop-Chain "$Name contains missing properties." } }
    foreach ($actualName in $actual) { if (-not $allowedSet.Contains($actualName)) { Stop-Chain "$Name contains unknown properties." } }
}

function Assert-ExactSet {
    param([Parameter(Mandatory = $true)][string[]] $Actual, [Parameter(Mandatory = $true)][string[]] $Expected, [Parameter(Mandatory = $true)][string] $Name)
    if ($Actual.Count -ne $Expected.Count) { Stop-Chain "$Name does not exactly match the approved set." }
    $actualSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($value in $Actual) { if (-not $actualSet.Add([string]$value)) { Stop-Chain "$Name contains a duplicate." } }
    foreach ($value in $Expected) { if (-not $actualSet.Contains([string]$value)) { Stop-Chain "$Name does not exactly match the approved set." } }
}

function Assert-RepoPath {
    param([Parameter(Mandatory = $true)][string] $Path, [string] $Name = "path")
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.Length -gt $Script:MaxRepositoryRelativePathCharacters) {
        Stop-Chain "$Name is blank or exceeds the path limit."
    }
    foreach ($character in $Path.ToCharArray()) {
        $code = [int][char]$character
        if ($code -lt 32 -or $code -gt 126) { Stop-Chain "$Name must be printable ASCII." }
    }
    if ($Path.Contains("\") -or $Path.StartsWith("/") -or $Path.StartsWith("//") -or $Path.Contains(":")) {
        Stop-Chain "$Name contains an absolute, backslash, drive, UNC, or colon form."
    }
    $segments = @($Path.Split('/'))
    if ($segments.Count -eq 0) { Stop-Chain "$Name is invalid." }
    foreach ($segment in $segments) {
        if ([string]::IsNullOrWhiteSpace($segment) -or $segment -in @(".", "..") -or $segment.Length -gt $Script:MaxPathSegmentCharacters) {
            Stop-Chain "$Name contains an empty, traversal, or oversized segment."
        }
    }
    return ($segments -join "/")
}

function Assert-UniquePaths {
    param([Parameter(Mandatory = $true)][string[]] $Paths, [Parameter(Mandatory = $true)][string] $Name)
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $Paths) {
        $normalized = Assert-RepoPath -Path $path -Name $Name
        if (-not $seen.Add($normalized)) { Stop-Chain "$Name contains a duplicate or case-colliding path: $normalized" }
    }
}

function Get-Sha256 {
    param([Parameter(Mandatory = $true)][string] $Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-BytesSha256 {
    param([Parameter(Mandatory = $true)][byte[]] $Bytes)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return (($sha.ComputeHash($Bytes) | ForEach-Object { $_.ToString("x2") }) -join "") } finally { $sha.Dispose() }
}

function Sort-OrdinalByPath {
    param([object[]] $Items)
    $list = [Collections.Generic.List[object]]::new()
    foreach ($item in @($Items)) { $list.Add($item) }
    $list.Sort([Comparison[object]]{ param($left, $right) [StringComparer]::Ordinal.Compare([string]$left.path, [string]$right.path) })
    return @($list)
}

function Sort-DescriptorsCanonical {
    param([object[]] $Items)
    $list = [Collections.Generic.List[object]]::new()
    foreach ($item in @($Items)) { $list.Add($item) }
    $list.Sort([Comparison[object]]{
        param($left, $right)
        $sequenceComparison = [int]$left.sequence - [int]$right.sequence
        if ($sequenceComparison -ne 0) { return $sequenceComparison }
        return [StringComparer]::Ordinal.Compare([string]$left.childTaskId, [string]$right.childTaskId)
    })
    return @($list)
}

function Read-JsonBounded {
    param([Parameter(Mandatory = $true)][string] $Path, [int] $MaxBytes = 262144)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Stop-Chain "JSON file is missing: $Path" }
    if ((Get-Item -LiteralPath $Path).Length -gt $MaxBytes) { Stop-Chain "JSON file exceeds its size limit: $Path" }
    try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -Depth $Script:MaxJsonNestingDepth } catch { Stop-Chain "JSON file is malformed or too deeply nested: $Path" }
}

function Get-PhaseFiles {
    param([Parameter(Mandatory = $true)][string] $Phase)
    if (-not $Script:PhaseFiles.Contains($Phase)) { Stop-Chain "Unknown child phase." }
    return @($Script:PhaseFiles[$Phase])
}

function Test-RegularZipEntry {
    param([Parameter(Mandatory = $true)] $Entry)
    $unixMode = ([int64]$Entry.ExternalAttributes -shr 16) -band 0xF000
    if ($unixMode -ne 0 -and $unixMode -ne 0x8000) { Stop-Chain "Archive entry is not a regular file: $($Entry.FullName)" }
}

function Expand-SafeZip {
    param(
        [Parameter(Mandatory = $true)][string] $ZipPath,
        [Parameter(Mandatory = $true)][string] $Destination,
        [Parameter(Mandatory = $true)][string[]] $ExpectedPaths,
        [int64] $MaxCompressedBytes,
        [int64] $MaxTotalUncompressedBytes
    )
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) { Stop-Chain "Archive is missing." }
    if ((Get-Item -LiteralPath $ZipPath).Length -gt $MaxCompressedBytes) { Stop-Chain "Archive exceeds the compressed-size ceiling." }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    try { $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath) } catch { Stop-Chain "Archive is malformed." }
    try {
        if ($archive.Entries.Count -gt $Script:MaxChangedFiles) { Stop-Chain "Archive exceeds the file-count ceiling." }
        $paths = @()
        [int64]$total = 0
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrWhiteSpace($entry.Name)) { Stop-Chain "Directory archive entries are not allowed." }
            $path = Assert-RepoPath -Path ([string]$entry.FullName) -Name "archive entry"
            Test-RegularZipEntry -Entry $entry
            if ($entry.Length -le 0 -or $entry.Length -gt $Script:MaxPerFileUncompressedBytes) { Stop-Chain "Archive entry size is invalid: $path" }
            if ($entry.CompressedLength -eq 0 -and $entry.Length -gt 0) { Stop-Chain "Archive entry has an invalid compression ratio: $path" }
            if ($entry.CompressedLength -gt 0 -and ([double]$entry.Length / [double]$entry.CompressedLength) -gt $Script:MaxCompressionRatio) { Stop-Chain "Archive entry exceeds the compression-ratio ceiling: $path" }
            $total += $entry.Length
            if ($total -gt $MaxTotalUncompressedBytes) { Stop-Chain "Archive exceeds the total uncompressed-size ceiling." }
            $paths += $path
        }
        Assert-UniquePaths -Paths $paths -Name "archive entries"
        Assert-ExactSet -Actual $paths -Expected $ExpectedPaths -Name "archive entries"
        New-Item -ItemType Directory -Force -Path $Destination | Out-Null
        foreach ($entry in $archive.Entries) {
            $path = Assert-RepoPath -Path ([string]$entry.FullName) -Name "archive entry"
            $target = Join-Path $Destination ($path.Replace('/', [IO.Path]::DirectorySeparatorChar))
            $targetParent = Split-Path -Parent $target
            if (-not [string]::IsNullOrWhiteSpace($targetParent)) { New-Item -ItemType Directory -Force -Path $targetParent | Out-Null }
            $sourceStream = $entry.Open()
            $destinationStream = [IO.File]::Open($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $sourceStream.CopyTo($destinationStream) } finally { $destinationStream.Dispose(); $sourceStream.Dispose() }
        }
    } finally { $archive.Dispose() }
}

function ConvertTo-CanonicalDescriptor {
    param([Parameter(Mandatory = $true)] $Descriptor)
    $files = @(Sort-OrdinalByPath @($Descriptor.ownedFiles) | ForEach-Object {
        [ordered]@{ path = [string]$_.path; mode = [string]$_.mode; size = [int64]$_.size; sha256 = [string]$_.sha256 }
    })
    return [ordered]@{
        schemaVersion = [string]$Descriptor.schemaVersion
        childTaskId = [string]$Descriptor.childTaskId
        phase = [string]$Descriptor.phase
        sequence = [int]$Descriptor.sequence
        recoveryRepositorySha = [string]$Descriptor.recoveryRepositorySha
        workflowId = [string]$Descriptor.workflowId
        runId = [string]$Descriptor.runId
        runAttempt = [int]$Descriptor.runAttempt
        artifactId = [string]$Descriptor.artifactId
        artifactName = [string]$Descriptor.artifactName
        githubArtifactDigest = [string]$Descriptor.githubArtifactDigest
        packageSha256 = [string]$Descriptor.packageSha256
        manifestSha256 = [string]$Descriptor.manifestSha256
        ownedFiles = $files
        validationResult = [string]$Descriptor.validationResult
        parentAggregateStateDigest = [string]$Descriptor.parentAggregateStateDigest
    }
}

function Assert-Descriptor {
    param([Parameter(Mandatory = $true)] $Descriptor)
    $names = @("schemaVersion", "childTaskId", "phase", "sequence", "recoveryRepositorySha", "workflowId", "runId", "runAttempt", "artifactId", "artifactName", "githubArtifactDigest", "packageSha256", "manifestSha256", "ownedFiles", "validationResult", "parentAggregateStateDigest")
    Assert-ExactProperties -Object $Descriptor -Names $names -Name "descriptor"
    if ($Descriptor.schemaVersion -ne "1.0") { Stop-Chain "Unknown descriptor schemaVersion." }
    Assert-Matches $Descriptor.childTaskId '^VSP-AI02-[A-Za-z0-9-]+$' "childTaskId"
    $phase = [string]$Descriptor.phase
    $expectedFiles = Get-PhaseFiles -Phase $phase
    $expectedSequence = @{ A1 = 1; A2 = 2; A3 = 3 }[$phase]
    if ([int]$Descriptor.sequence -ne $expectedSequence) { Stop-Chain "Descriptor phase/sequence mismatch." }
    Assert-Matches $Descriptor.recoveryRepositorySha '^[0-9a-f]{40}$' "recoveryRepositorySha"
    foreach ($field in @("workflowId", "runId", "artifactId")) { Assert-Matches $Descriptor.$field '^[1-9][0-9]{0,19}$' $field }
    if ([int]$Descriptor.runAttempt -lt 1 -or [int]$Descriptor.runAttempt -gt 2) { Stop-Chain "runAttempt is outside the child budget." }
    Assert-Matches $Descriptor.artifactName '^[A-Za-z0-9._-]{1,200}$' "artifactName"
    Assert-Matches $Descriptor.githubArtifactDigest '^sha256:[0-9a-f]{64}$' "githubArtifactDigest"
    Assert-Matches $Descriptor.packageSha256 '^[0-9a-f]{64}$' "packageSha256"
    Assert-Matches $Descriptor.manifestSha256 '^[0-9a-f]{64}$' "manifestSha256"
    if ($Descriptor.validationResult -ne "PASS") { Stop-Chain "Descriptor validationResult must be PASS." }
    if ($Descriptor.parentAggregateStateDigest -ne "GENESIS" -and [string]$Descriptor.parentAggregateStateDigest -cnotmatch '^sha256:[0-9a-f]{64}$') { Stop-Chain "parentAggregateStateDigest is invalid." }
    if ($phase -eq "A1" -and $Descriptor.parentAggregateStateDigest -ne "GENESIS") { Stop-Chain "A1 must bind GENESIS." }
    $files = @($Descriptor.ownedFiles)
    Assert-ExactSet -Actual @($files.path) -Expected $expectedFiles -Name "descriptor ownedFiles"
    Assert-UniquePaths -Paths @($files.path) -Name "descriptor ownedFiles"
    foreach ($file in $files) {
        Assert-ExactProperties -Object $file -Names @("path", "mode", "size", "sha256") -Name "ownedFiles item"
        Assert-RepoPath ([string]$file.path) "ownedFiles.path" | Out-Null
        if ($file.mode -ne "100644") { Stop-Chain "Only mode 100644 is allowed." }
        if ([int64]$file.size -le 0 -or [int64]$file.size -gt $Script:MaxPerFileUncompressedBytes) { Stop-Chain "Owned file size is invalid." }
        Assert-Matches $file.sha256 '^[0-9a-f]{64}$' "ownedFiles.sha256"
    }
}

function Test-DescriptorPackage {
    param([Parameter(Mandatory = $true)] $Descriptor, [Parameter(Mandatory = $true)][string] $Directory)
    Assert-Descriptor -Descriptor $Descriptor
    $zipPath = Join-Path $Directory "publication-package.zip"
    $manifestPath = Join-Path $Directory "publication-package.manifest.json"
    $resultPath = Join-Path $Directory "publication-package.result.json"
    foreach ($path in @($zipPath, $manifestPath, $resultPath)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Stop-Chain "Publication package component is missing." } }
    if ((Get-Item $manifestPath).Length -gt $Script:MaxManifestJsonBytes -or (Get-Item $resultPath).Length -gt $Script:MaxResultJsonBytes) { Stop-Chain "Publication metadata exceeds its size ceiling." }
    if ((Get-Item $zipPath).Length -gt $Script:MaxInnerPublicationZipCompressedBytes) { Stop-Chain "Publication ZIP exceeds its compressed-size ceiling." }
    if ((Get-Sha256 $zipPath) -ne $Descriptor.packageSha256) { Stop-Chain "Package digest mismatch." }
    if ((Get-Sha256 $manifestPath) -ne $Descriptor.manifestSha256) { Stop-Chain "Manifest digest mismatch." }
    $manifest = Read-JsonBounded $manifestPath $Script:MaxManifestJsonBytes
    $result = Read-JsonBounded $resultPath $Script:MaxResultJsonBytes
    $manifestProperties = @("schemaVersion", "taskId", "classification", "repository", "approvedBaseSha", "parentAggregateStateDigest", "files", "repositoryWriteCredentialAvailableToDeveloper")
    Assert-AllowedProperties $manifest $manifestProperties @($manifestProperties + "generatedAtUtc") "publication manifest"
    Assert-ExactProperties $result @("taskId", "approvedBaseSha", "parentAggregateStateDigest", "changedFiles", "packageSha256", "manifestSha256", "repositoryWriteCredentialAvailableToDeveloper", "productOwnerManualTransport") "publication result"
    if ($manifest.schemaVersion -ne "1.0" -or $manifest.classification -ne "CRITICAL" -or $manifest.repositoryWriteCredentialAvailableToDeveloper -ne $false -or $result.repositoryWriteCredentialAvailableToDeveloper -ne $false -or $result.productOwnerManualTransport -ne $false) { Stop-Chain "Publication metadata violates the credential or governance boundary." }
    if ($result.packageSha256 -ne $Descriptor.packageSha256 -or $result.manifestSha256 -ne $Descriptor.manifestSha256) { Stop-Chain "Publication result digest binding mismatch." }
    if ($manifest.parentAggregateStateDigest -ne $Descriptor.parentAggregateStateDigest -or $result.parentAggregateStateDigest -ne $Descriptor.parentAggregateStateDigest) { Stop-Chain "Package parent lineage mismatch." }
    if ($manifest.taskId -ne $Descriptor.childTaskId -or $result.taskId -ne $Descriptor.childTaskId) { Stop-Chain "Package task identity mismatch." }
    if ($manifest.approvedBaseSha -ne $Descriptor.recoveryRepositorySha -or $result.approvedBaseSha -ne $Descriptor.recoveryRepositorySha) { Stop-Chain "Package recovery SHA mismatch." }
    Assert-ExactSet -Actual @($manifest.files.path) -Expected @($Descriptor.ownedFiles.path) -Name "publication manifest files"
    Assert-ExactSet -Actual @($result.changedFiles.path) -Expected @($Descriptor.ownedFiles.path) -Name "publication result files"
    foreach ($file in @($Descriptor.ownedFiles)) {
        $manifestFile = @($manifest.files | Where-Object { $_.path -ceq $file.path })
        $resultFile = @($result.changedFiles | Where-Object { $_.path -ceq $file.path })
        if ($manifestFile.Count -ne 1 -or $resultFile.Count -ne 1) { Stop-Chain "Package file identity is ambiguous." }
        foreach ($candidate in @($manifestFile[0], $resultFile[0])) {
            if ($candidate.mode -ne $file.mode -or [int64]$candidate.size -ne [int64]$file.size -or $candidate.sha256 -ne $file.sha256) { Stop-Chain "Package file metadata mismatch: $($file.path)" }
        }
    }
    $extractRoot = Join-Path ([IO.Path]::GetTempPath()) ("ai02-chain-package-" + [Guid]::NewGuid().ToString("N"))
    try {
        Expand-SafeZip -ZipPath $zipPath -Destination $extractRoot -ExpectedPaths @($Descriptor.ownedFiles.path) -MaxCompressedBytes $Script:MaxInnerPublicationZipCompressedBytes -MaxTotalUncompressedBytes $Script:MaxTotalInnerUncompressedBytes
        foreach ($file in @($Descriptor.ownedFiles)) {
            $path = Join-Path $extractRoot ([string]$file.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
            if ((Get-Item $path).Length -ne [int64]$file.size -or (Get-Sha256 $path) -ne $file.sha256) { Stop-Chain "Extracted file hash or size mismatch: $($file.path)" }
        }
    } finally { if (Test-Path $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force } }
    return $true
}

function New-AggregateState {
    param([Parameter(Mandatory = $true)][string] $RecoverySha, [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]] $Descriptors)
    Assert-Matches $RecoverySha '^[0-9a-f]{40}$' "recoveryRepositorySha"
    $orderedDescriptors = @(Sort-DescriptorsCanonical $Descriptors)
    $accepted = @()
    $ownership = @()
    $currentDigest = "GENESIS"
    for ($descriptorIndex = 0; $descriptorIndex -lt $orderedDescriptors.Count; $descriptorIndex++) {
        $descriptor = $orderedDescriptors[$descriptorIndex]
        Assert-Descriptor $descriptor
        if ([int]$descriptor.sequence -ne ($descriptorIndex + 1)) { Stop-Chain "Aggregate phases are missing, duplicated, or non-contiguous." }
        if ($descriptor.recoveryRepositorySha -ne $RecoverySha) { Stop-Chain "Descriptor recovery SHA mismatch." }
        if ($descriptor.parentAggregateStateDigest -ne $currentDigest) { Stop-Chain "Aggregate lineage mismatch or stale predecessor." }
        $accepted += (ConvertTo-CanonicalDescriptor $descriptor)
        foreach ($file in @($descriptor.ownedFiles)) {
            $ownership += [ordered]@{ path = [string]$file.path; ownerTaskId = [string]$descriptor.childTaskId; phase = [string]$descriptor.phase; mode = [string]$file.mode; size = [int64]$file.size; sha256 = [string]$file.sha256 }
        }
        Assert-UniquePaths -Paths @($ownership.path) -Name "aggregate ownership"
        $digestInput = [ordered]@{ schemaVersion = "1.0"; recoveryRepositorySha = $RecoverySha; predecessors = @($accepted); fileOwnership = @(Sort-OrdinalByPath $ownership) }
        $canonical = $digestInput | ConvertTo-Json -Compress -Depth 20
        $currentDigest = "sha256:" + (Get-BytesSha256 ([Text.UTF8Encoding]::new($false).GetBytes($canonical)))
    }
    $finalInput = [ordered]@{ schemaVersion = "1.0"; recoveryRepositorySha = $RecoverySha; predecessors = @($accepted); fileOwnership = @(Sort-OrdinalByPath $ownership) }
    $finalCanonical = $finalInput | ConvertTo-Json -Compress -Depth 20
    $finalDigest = "sha256:" + (Get-BytesSha256 ([Text.UTF8Encoding]::new($false).GetBytes($finalCanonical)))
    return [ordered]@{ schemaVersion = "1.0"; recoveryRepositorySha = $RecoverySha; predecessors = @($accepted); fileOwnership = @(Sort-OrdinalByPath $ownership); aggregateStateDigest = $finalDigest }
}

function Write-Utf8CanonicalJson {
    param([Parameter(Mandatory = $true)] $Value, [Parameter(Mandatory = $true)][string] $Path)
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Compress -Depth 20), [Text.UTF8Encoding]::new($false))
}

function Write-Baseline {
    param([Parameter(Mandatory = $true)] $AggregateState, [Parameter(Mandatory = $true)][string] $Path)
    $baseline = [ordered]@{ schemaVersion = "1.0"; recoveryRepositorySha = $AggregateState.recoveryRepositorySha; aggregateStateDigest = $AggregateState.aggregateStateDigest; predecessorFiles = @($AggregateState.fileOwnership) }
    Write-Utf8CanonicalJson $baseline $Path
}

function Materialize-ValidatedPackage {
    param([Parameter(Mandatory = $true)] $Descriptor, [Parameter(Mandatory = $true)][string] $Directory, [Parameter(Mandatory = $true)][string] $Workspace)
    Test-DescriptorPackage $Descriptor $Directory | Out-Null
    $extractRoot = Join-Path ([IO.Path]::GetTempPath()) ("ai02-chain-materialize-" + [Guid]::NewGuid().ToString("N"))
    try {
        Expand-SafeZip -ZipPath (Join-Path $Directory "publication-package.zip") -Destination $extractRoot -ExpectedPaths @($Descriptor.ownedFiles.path) -MaxCompressedBytes $Script:MaxInnerPublicationZipCompressedBytes -MaxTotalUncompressedBytes $Script:MaxTotalInnerUncompressedBytes
        foreach ($file in @($Descriptor.ownedFiles)) {
            $relative = ([string]$file.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
            $source = Join-Path $extractRoot $relative
            $target = Join-Path $Workspace $relative
            $workspaceRoot = [IO.Path]::GetFullPath($Workspace).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            $targetFull = [IO.Path]::GetFullPath($target)
            if (-not $targetFull.StartsWith($workspaceRoot, [StringComparison]::OrdinalIgnoreCase)) { Stop-Chain "Materialization target escaped the aggregate workspace." }
            if (Test-Path -LiteralPath $target) { Stop-Chain "Predecessor would overwrite a repository-base file: $($file.path)" }
            $parent = Split-Path -Parent $target
            $cursor = $parent
            while ($cursor -and $cursor.StartsWith($workspaceRoot.TrimEnd([IO.Path]::DirectorySeparatorChar), [StringComparison]::OrdinalIgnoreCase)) {
                if (Test-Path -LiteralPath $cursor) {
                    $item = Get-Item -LiteralPath $cursor -Force
                    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $item.LinkType) { Stop-Chain "Materialization parent is a link or reparse point." }
                }
                if ($cursor -eq $workspaceRoot.TrimEnd([IO.Path]::DirectorySeparatorChar)) { break }
                $cursor = Split-Path -Parent $cursor
            }
            New-Item -ItemType Directory -Force -Path $parent | Out-Null
            Copy-Item -LiteralPath $source -Destination $target
            if ((Get-Sha256 $target) -ne $file.sha256) { Stop-Chain "Materialized predecessor hash mismatch." }
        }
    } finally { if (Test-Path $extractRoot) { Remove-Item -LiteralPath $extractRoot -Recurse -Force } }
}

function Assert-BaselineUnchanged {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][string] $Workspace, [string] $ExpectedBaselineHash = "", [string] $StatePath = "", [string] $ExpectedStateHash = "")
    if ($ExpectedBaselineHash) {
        Assert-Matches $ExpectedBaselineHash '^[0-9a-f]{64}$' "ExpectedBaselineSha256"
        if ((Get-Sha256 $Path) -ne $ExpectedBaselineHash) { Stop-Chain "Pre-Claude baseline metadata was modified." }
    }
    $baseline = Read-JsonBounded $Path
    if ($ExpectedStateHash) {
        Assert-Matches $ExpectedStateHash '^[0-9a-f]{64}$' "ExpectedAggregateStateSha256"
        if ([string]::IsNullOrWhiteSpace($StatePath) -or (Get-Sha256 $StatePath) -ne $ExpectedStateHash) { Stop-Chain "Pre-Claude aggregate state was modified." }
        $state = Read-JsonBounded $StatePath
        $recomputed = New-AggregateState ([string]$state.recoveryRepositorySha) @($state.predecessors)
        if ($recomputed.aggregateStateDigest -ne $state.aggregateStateDigest) { Stop-Chain "Pre-Claude aggregate state digest is invalid." }
        if (@($state.predecessors).Count -eq 0) {
            if ($baseline.aggregateStateDigest -ne "GENESIS") { Stop-Chain "Genesis baseline lineage is invalid." }
        } elseif ($baseline.aggregateStateDigest -ne $state.aggregateStateDigest) { Stop-Chain "Baseline and aggregate lineage disagree." }
    }
    foreach ($file in @($baseline.predecessorFiles)) {
        $fullPath = Join-Path $Workspace ([string]$file.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { Stop-Chain "Predecessor file is missing after Claude: $($file.path)" }
        if ((Get-Item $fullPath).Length -ne [int64]$file.size -or (Get-Sha256 $fullPath) -ne $file.sha256) { Stop-Chain "Predecessor mutation detected after Claude: $($file.path)" }
    }
    return $baseline
}

function Assert-RegularMode100644 {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][string] $Name, [string] $GitWorkspace = "", [string] $RepositoryPath = "")
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Stop-Chain "$Name is missing or is not a file." }
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or $item.LinkType) { Stop-Chain "$Name is a link or reparse point." }
    if ($GitWorkspace -and $RepositoryPath) {
        $stage = @(& git -C $GitWorkspace ls-files --stage -- $RepositoryPath)
        if ($LASTEXITCODE -ne 0) { Stop-Chain "Unable to determine Git mode for $Name." }
        if ($stage.Count -gt 1) { Stop-Chain "$Name has ambiguous Git index entries." }
        if ($stage.Count -eq 1 -and [string]$stage[0] -cnotmatch '^100644 ') { Stop-Chain "$Name must have Git mode 100644." }
    }
    if (-not $IsWindows) {
        $mode = [int][IO.File]::GetUnixFileMode($Path)
        if (($mode -band 0x1FF) -ne 0x1A4) { Stop-Chain "$Name must have actual filesystem mode 100644." }
    }
}

function Get-GitChangesExcludingBaseline {
    param([Parameter(Mandatory = $true)][string] $Workspace, [Parameter(Mandatory = $true)] $Baseline)
    $tracked = @(& git -C $Workspace diff --name-only HEAD -- | ForEach-Object { [string]$_ })
    if ($LASTEXITCODE -ne 0) { Stop-Chain "git diff failed." }
    $untracked = @(& git -C $Workspace ls-files --others --exclude-standard | ForEach-Object { [string]$_ })
    if ($LASTEXITCODE -ne 0) { Stop-Chain "git untracked-file query failed." }
    $baselinePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($file in @($Baseline.predecessorFiles)) { $baselinePaths.Add([string]$file.path) | Out-Null }
    return @(@($tracked + $untracked | Sort-Object -Unique) | Where-Object { -not $baselinePaths.Contains($_) })
}

function Get-ManifestApprovedFiles {
    param([Parameter(Mandatory = $true)][string] $Path)
    $manifest = Read-JsonBounded $Path
    $files = @($manifest.repositoryTransport.approvedFiles | ForEach-Object { Assert-RepoPath ([string]$_) "manifest approvedFiles" })
    Assert-UniquePaths $files "manifest approvedFiles"
    return [pscustomobject]@{ manifest = $manifest; files = $files }
}

function Test-ChildAuthorization {
    $expectedSequence = @{ A1=1; A2=2; A3=3 }[$ChildPhase]
    if ($Sequence -ne $expectedSequence) { Stop-Chain "Child phase and sequence do not match." }
    $manifestInfo = Get-ManifestApprovedFiles $ManifestPath
    if ([string]$manifestInfo.manifest.taskId -ne $ChildTaskId) { Stop-Chain "Child task identity does not match its manifest." }
    Assert-ExactSet @($manifestInfo.files) (Get-PhaseFiles $ChildPhase) "child authorization allowlist"
    $baseline = Read-JsonBounded $BaselinePath
    if ([string]$baseline.recoveryRepositorySha -ne $RecoveryRepositorySha) { Stop-Chain "Child baseline recovery SHA mismatch." }
    $expectedPredecessorCount = $Sequence - 1
    $actualPredecessorCount = @($baseline.predecessorFiles | Select-Object -ExpandProperty phase -Unique).Count
    if ($actualPredecessorCount -ne $expectedPredecessorCount) { Stop-Chain "Child predecessor phase count is invalid." }
    return [pscustomobject]@{ status="PASS"; childTaskId=$ChildTaskId; phase=$ChildPhase; sequence=$Sequence; approvedFiles=@($manifestInfo.files); predecessorPhaseCount=$actualPredecessorCount; repositoryWriteCredentialAvailableToDeveloper=$false }
}

function New-ChildPackage {
    param([Parameter(Mandatory = $true)][string] $Workspace, [Parameter(Mandatory = $true)][string] $Baseline, [Parameter(Mandatory = $true)][string] $Manifest, [Parameter(Mandatory = $true)][string] $Destination)
    $baselineObject = Assert-BaselineUnchanged $Baseline $Workspace $ExpectedBaselineSha256 $AggregateStatePath $ExpectedAggregateStateSha256
    $manifestInfo = Get-ManifestApprovedFiles $Manifest
    $expected = @($manifestInfo.files)
    $phase = if ($expected.Count -eq 4) { "A1" } elseif ($expected.Count -eq 1) { "A2" } elseif ($expected.Count -eq 2) { "A3" } else { Stop-Chain "Child manifest allowlist does not match a phase." }
    Assert-ExactSet $expected (Get-PhaseFiles $phase) "child manifest allowlist"
    $changes = @(Get-GitChangesExcludingBaseline $Workspace $baselineObject)
    Assert-ExactSet $changes $expected "child changed files"
    $metadata = @()
    foreach ($path in $expected) {
        $fullPath = Join-Path $Workspace $path.Replace('/', [IO.Path]::DirectorySeparatorChar)
        Assert-RegularMode100644 $fullPath "Child output $path" $Workspace $path
        $size = (Get-Item $fullPath).Length
        if ($size -le 0 -or $size -gt $Script:MaxPerFileUncompressedBytes) { Stop-Chain "Child output size is invalid: $path" }
        $metadata += [ordered]@{ path = $path; mode = "100644"; size = [int64]$size; sha256 = Get-Sha256 $fullPath }
    }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $zipPath = Join-Path $Destination "publication-package.zip"
    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $metadata) {
            $entry = $archive.CreateEntry([string]$file.path, [IO.Compression.CompressionLevel]::Optimal)
            $entry.ExternalAttributes = (0x81A4 -shl 16)
            $input = [IO.File]::OpenRead((Join-Path $Workspace ([string]$file.path).Replace('/', [IO.Path]::DirectorySeparatorChar)))
            $output = $entry.Open()
            try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
        }
    } finally { $archive.Dispose() }
    $packageManifest = [ordered]@{ schemaVersion = "1.0"; taskId = [string]$manifestInfo.manifest.taskId; classification = [string]$manifestInfo.manifest.classification; repository = [string]$manifestInfo.manifest.repository; approvedBaseSha = [string]$baselineObject.recoveryRepositorySha; parentAggregateStateDigest = [string]$baselineObject.aggregateStateDigest; files = $metadata; repositoryWriteCredentialAvailableToDeveloper = $false; generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o") }
    $manifestPathOut = Join-Path $Destination "publication-package.manifest.json"
    Write-Utf8CanonicalJson $packageManifest $manifestPathOut
    $result = [ordered]@{ taskId = [string]$manifestInfo.manifest.taskId; approvedBaseSha = [string]$baselineObject.recoveryRepositorySha; parentAggregateStateDigest = [string]$baselineObject.aggregateStateDigest; changedFiles = $metadata; packageSha256 = Get-Sha256 $zipPath; manifestSha256 = Get-Sha256 $manifestPathOut; repositoryWriteCredentialAvailableToDeveloper = $false; productOwnerManualTransport = $false }
    Write-Utf8CanonicalJson $result (Join-Path $Destination "publication-package.result.json")
    return $result
}

function Get-ArtifactMetadata {
    param([Parameter(Mandatory = $true)][string] $Repo, [Parameter(Mandatory = $true)][string] $Id, [Parameter(Mandatory = $true)][string] $Token)
    $headers = @{ Authorization = "Bearer $Token"; Accept = "application/vnd.github+json"; "X-GitHub-Api-Version" = "2022-11-28" }
    try { return Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/artifacts/$Id" -Headers $headers -Method Get } catch { Stop-Chain "Artifact metadata acquisition failed." }
}

function Get-WorkflowRunMetadata {
    param([Parameter(Mandatory = $true)][string] $Repo, [Parameter(Mandatory = $true)][string] $Id, [Parameter(Mandatory = $true)][string] $Token)
    $headers = @{ Authorization = "Bearer $Token"; Accept = "application/vnd.github+json"; "X-GitHub-Api-Version" = "2022-11-28" }
    try { return Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs/$Id" -Headers $headers -Method Get } catch { Stop-Chain "Workflow-run provenance acquisition failed." }
}

function Receive-Artifact {
    param([Parameter(Mandatory = $true)][string] $Repo, [Parameter(Mandatory = $true)] $Binding, [Parameter(Mandatory = $true)][string] $Token, [Parameter(Mandatory = $true)][string] $Destination)
    Assert-Matches $Binding.artifactId '^[1-9][0-9]{0,19}$' "binding.artifactId"
    Assert-Matches $Binding.artifactDigest '^sha256:[0-9a-f]{64}$' "binding.artifactDigest"
    $metadata = Get-ArtifactMetadata $Repo ([string]$Binding.artifactId) $Token
    if ([string]$metadata.id -ne [string]$Binding.artifactId -or [string]$metadata.name -ne [string]$Binding.artifactName -or [string]$metadata.digest -ne [string]$Binding.artifactDigest -or $metadata.expired -eq $true) { Stop-Chain "Artifact immutable identity mismatch." }
    if ([string]$metadata.workflow_run.id -ne [string]$Binding.runId -or [string]$metadata.workflow_run.head_sha -ne [string]$Binding.recoveryRepositorySha) { Stop-Chain "Artifact workflow identity or source SHA mismatch." }
    $headers = @{ Authorization = "Bearer $Token"; Accept = "application/vnd.github+json"; "X-GitHub-Api-Version" = "2022-11-28" }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $zipPath = Join-Path $Destination "artifact.zip"
    try { Invoke-WebRequest -Uri "https://api.github.com/repos/$Repo/actions/artifacts/$($Binding.artifactId)/zip" -Headers $headers -OutFile $zipPath } catch { Stop-Chain "Artifact download failed." }
    if ((Get-Item $zipPath).Length -gt $Script:MaxOuterArtifactCompressedBytes) { Stop-Chain "Downloaded artifact exceeds the outer size ceiling." }
    if (("sha256:" + (Get-Sha256 $zipPath)) -ne [string]$Binding.artifactDigest) { Stop-Chain "Downloaded artifact digest mismatch." }
    return $zipPath
}

function Invoke-AcquireAndMaterialize {
    $token = [string]$env:AI02_ARTIFACT_READ_TOKEN
    Assert-NonBlank $token "AI02_ARTIFACT_READ_TOKEN"
    Assert-Matches $RecoveryRepositorySha '^[0-9a-f]{40}$' "RecoveryRepositorySha"
    try { $bindings = @($BindingsJson | ConvertFrom-Json -Depth $Script:MaxJsonNestingDepth) } catch { Stop-Chain "BindingsJson is malformed." }
    if ($bindings.Count -lt 1 -or $bindings.Count -gt 2) { Stop-Chain "A2/A3 requires one or two predecessor bindings." }
    $descriptors = @()
    $workRoot = Join-Path ([IO.Path]::GetTempPath()) ("ai02-chain-acquire-" + [Guid]::NewGuid().ToString("N"))
    try {
        foreach ($binding in $bindings) {
            Assert-ExactProperties $binding @("descriptorArtifactId", "descriptorArtifactName", "descriptorArtifactDigest", "packageArtifactId", "packageArtifactName", "packageArtifactDigest", "runId", "runAttempt", "recoveryRepositorySha") "artifact binding"
            if ([string]$binding.recoveryRepositorySha -ne $RecoveryRepositorySha) { Stop-Chain "Binding recovery SHA mismatch." }
            $descriptorBinding = [pscustomobject]@{ artifactId=$binding.descriptorArtifactId; artifactName=$binding.descriptorArtifactName; artifactDigest=$binding.descriptorArtifactDigest; runId=$binding.runId; recoveryRepositorySha=$binding.recoveryRepositorySha }
            $packageBinding = [pscustomobject]@{ artifactId=$binding.packageArtifactId; artifactName=$binding.packageArtifactName; artifactDigest=$binding.packageArtifactDigest; runId=$binding.runId; recoveryRepositorySha=$binding.recoveryRepositorySha }
            $itemRoot = Join-Path $workRoot ([string]$binding.runId)
            $descriptorZip = Receive-Artifact $Repository $descriptorBinding $token (Join-Path $itemRoot "descriptor-download")
            $descriptorExtract = Join-Path $itemRoot "descriptor"
            Expand-SafeZip $descriptorZip $descriptorExtract @("predecessor-descriptor.json") $Script:MaxOuterArtifactCompressedBytes $Script:MaxManifestJsonBytes
            $descriptor = Read-JsonBounded (Join-Path $descriptorExtract "predecessor-descriptor.json")
            Assert-Descriptor $descriptor
            if ([string]$descriptor.artifactId -ne [string]$binding.packageArtifactId -or [string]$descriptor.githubArtifactDigest -ne [string]$binding.packageArtifactDigest -or [string]$descriptor.runId -ne [string]$binding.runId -or [int]$descriptor.runAttempt -ne [int]$binding.runAttempt) { Stop-Chain "Descriptor does not bind the supplied package artifact." }
            $run = Get-WorkflowRunMetadata $Repository ([string]$binding.runId) $token
            if ([string]$run.id -ne [string]$descriptor.runId -or [string]$run.workflow_id -ne [string]$descriptor.workflowId -or [int]$run.run_attempt -ne [int]$descriptor.runAttempt -or [string]$run.head_sha -ne $RecoveryRepositorySha -or [string]$run.repository.full_name -ne $Repository -or [string]$run.conclusion -ne "success") { Stop-Chain "Workflow-run provenance mismatch or unsuccessful predecessor run." }
            $packageZipOuter = Receive-Artifact $Repository $packageBinding $token (Join-Path $itemRoot "package-download")
            $packageExtract = Join-Path $itemRoot "package"
            Expand-SafeZip $packageZipOuter $packageExtract @("publication-package.manifest.json", "publication-package.result.json", "publication-package.zip") $Script:MaxOuterArtifactCompressedBytes $Script:MaxTotalInnerUncompressedBytes
            Test-DescriptorPackage $descriptor $packageExtract | Out-Null
            $descriptors += $descriptor
        }
        $aggregate = New-AggregateState $RecoveryRepositorySha $descriptors
        foreach ($index in 0..($descriptors.Count - 1)) { Materialize-ValidatedPackage $descriptors[$index] (Join-Path (Join-Path $workRoot ([string]$bindings[$index].runId)) "package") $WorkspacePath }
        Write-Utf8CanonicalJson $aggregate $AggregateStatePath
        Write-Baseline $aggregate $BaselinePath
        return $aggregate
    } finally {
        Remove-Item Env:AI02_ARTIFACT_READ_TOKEN -ErrorAction SilentlyContinue
        if (Test-Path $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
    }
}

function New-GenesisWorkspace {
    Assert-Matches $RecoveryRepositorySha '^[0-9a-f]{40}$' "RecoveryRepositorySha"
    $aggregate = New-AggregateState $RecoveryRepositorySha @()
    Write-Utf8CanonicalJson $aggregate $AggregateStatePath
    $baseline = [ordered]@{ schemaVersion = "1.0"; recoveryRepositorySha = $aggregate.recoveryRepositorySha; aggregateStateDigest = "GENESIS"; predecessorFiles = @() }
    Write-Utf8CanonicalJson $baseline $BaselinePath
    return $aggregate
}

function Get-LocalDescriptors {
    if ($DescriptorPaths.Count -lt 1 -or $DescriptorPaths.Count -gt 3) { Stop-Chain "One to three local descriptors are required." }
    if ($ArtifactDirectories.Count -ne 0 -and $ArtifactDirectories.Count -ne $DescriptorPaths.Count) { Stop-Chain "Local descriptor and artifact directory counts differ." }
    $items = @()
    foreach ($path in $DescriptorPaths) {
        $descriptor = Read-JsonBounded $path
        Assert-Descriptor $descriptor
        $items += $descriptor
    }
    return @($items)
}

function Invoke-LocalMaterialization {
    $descriptors = @(Get-LocalDescriptors)
    if ($ArtifactDirectories.Count -ne $descriptors.Count) { Stop-Chain "Artifact directories are required for local materialization." }
    $aggregate = New-AggregateState $RecoveryRepositorySha $descriptors
    for ($index = 0; $index -lt $descriptors.Count; $index++) {
        Materialize-ValidatedPackage $descriptors[$index] $ArtifactDirectories[$index] $WorkspacePath
    }
    Write-Utf8CanonicalJson $aggregate $AggregateStatePath
    Write-Baseline $aggregate $BaselinePath
    return $aggregate
}

function New-PredecessorDescriptor {
    $resultPath = Join-Path $OutputDirectory "publication-package.result.json"
    $manifestOut = Join-Path $OutputDirectory "publication-package.manifest.json"
    $result = Read-JsonBounded $resultPath $Script:MaxResultJsonBytes
    $manifest = Read-JsonBounded $manifestOut $Script:MaxManifestJsonBytes
    Assert-ExactSet @($manifest.files.path) (Get-PhaseFiles $ChildPhase) "descriptor package files"
    $normalizedArtifactDigest = if ($GitHubArtifactDigest -cmatch '^[0-9a-f]{64}$') { "sha256:$GitHubArtifactDigest" } else { $GitHubArtifactDigest }
    $descriptor = [pscustomobject][ordered]@{
        schemaVersion = "1.0"; childTaskId = $ChildTaskId; phase = $ChildPhase; sequence = $Sequence; recoveryRepositorySha = $RecoveryRepositorySha
        workflowId = $WorkflowId; runId = $RunId; runAttempt = $RunAttempt; artifactId = $ArtifactId; artifactName = $ArtifactName
        githubArtifactDigest = $normalizedArtifactDigest; packageSha256 = [string]$result.packageSha256; manifestSha256 = [string]$result.manifestSha256
        ownedFiles = @($manifest.files); validationResult = "PASS"; parentAggregateStateDigest = [string]$result.parentAggregateStateDigest
    }
    Assert-Descriptor $descriptor
    Test-DescriptorPackage $descriptor $OutputDirectory | Out-Null
    $path = Join-Path $OutputDirectory "predecessor-descriptor.json"
    Write-Utf8CanonicalJson (ConvertTo-CanonicalDescriptor $descriptor) $path
    return $descriptor
}

function Assert-FinalP0Contract {
    param([Parameter(Mandatory = $true)][string] $Workspace)
    $a1Text = (($Script:PhaseFiles.A1 | ForEach-Object { Get-Content -LiteralPath (Join-Path $Workspace $_) -Raw }) -join "`n")
    foreach ($marker in @("vsp.ai02.artifact-intake-request/1.0", "vsp.ai02.artifact-intake-decision/1.0")) {
        if (-not $a1Text.Contains($marker, [StringComparison]::Ordinal)) { Stop-Chain "Final A1 schemas/templates omit required P0 identity: $marker" }
    }
    $contractText = Get-Content -LiteralPath (Join-Path $Workspace "tools/orchestrator/artifact-intake-contract.ps1") -Raw
    foreach ($limit in @(26214400,20971520,52428800,200,10485760,262144,240,100,16,1048576,3)) {
        if ($contractText -cnotmatch "(?<![0-9])$limit(?![0-9])") { Stop-Chain "Final A2 contract omits required P0 numeric ceiling: $limit" }
    }
    $allText = (($Script:FinalFiles | ForEach-Object { Get-Content -LiteralPath (Join-Path $Workspace $_) -Raw }) -join "`n")
    foreach ($marker in @("vsp-ai02-intake-v1", "EXACT_BASE_ONLY", "FIRST_USE", "NOT_YET_CONSUMED", "REJECT_REPLAY")) {
        if (-not $allText.Contains($marker, [StringComparison]::Ordinal)) { Stop-Chain "Final aggregate omits required P0 policy marker: $marker" }
    }
}

function Assert-FocusedSuiteEvidence {
    param([object[]] $OutputLines)
    $line = @($OutputLines | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })[-1]
    try { $evidence = $line | ConvertFrom-Json -Depth 10 } catch { Stop-Chain "Focused suite did not emit valid final JSON evidence." }
    Assert-ExactProperties $evidence @("schemaVersion","suite","status","testCount","failedCount","requiredChecks","policy") "focused suite evidence"
    if ($evidence.schemaVersion -ne "vsp.ai02.artifact-intake-focused-suite/1.0" -or $evidence.suite -ne "VSP-AI02-001TI-A" -or $evidence.status -ne "PASS" -or [int]$evidence.failedCount -ne 0 -or [int]$evidence.testCount -lt 17) { Stop-Chain "Focused suite completion evidence is incomplete or failed." }
    $requiredChecks = @("schema-validation","template-schema-conformance","ascii-path-rejection","traversal-rejection","absolute-path-rejection","backslash-rejection","drive-unc-colon-rejection","duplicate-case-collision-rejection","mode-restriction","size-count-ceilings","hash-verification","malformed-unknown-rejection","exact-base-rejection","replay-first-use","replay-rejection","credential-invariants","deterministic-output")
    Assert-ExactSet @($evidence.requiredChecks) $requiredChecks "focused suite required checks"
    $policyNames = @("schemaVersion","policyVersion","requestSchemaIdentifier","decisionSchemaIdentifier","maxOuterArtifactCompressedBytes","maxInnerPublicationZipCompressedBytes","maxTotalInnerUncompressedBytes","maxChangedFiles","maxPerFileUncompressedBytes","maxManifestJsonBytes","maxResultJsonBytes","maxRepositoryRelativePathCharacters","maxPathSegmentCharacters","maxJsonNestingDepth","maxSanitizedEvidenceArtifactBytes","maxCompressionRatio","requiredOuterPublicationFileCount","staleBasePolicy","emptyConsumedIdentitiesMeaning","matchingConsumedIdentityDisposition","repositoryWriteCredentialAvailableToDeveloper")
    Assert-ExactProperties $evidence.policy $policyNames "focused suite P0 policy evidence"
    $expected = [ordered]@{ schemaVersion="1.0";policyVersion="vsp-ai02-intake-v1";requestSchemaIdentifier="vsp.ai02.artifact-intake-request/1.0";decisionSchemaIdentifier="vsp.ai02.artifact-intake-decision/1.0";maxOuterArtifactCompressedBytes=26214400;maxInnerPublicationZipCompressedBytes=20971520;maxTotalInnerUncompressedBytes=52428800;maxChangedFiles=200;maxPerFileUncompressedBytes=10485760;maxManifestJsonBytes=262144;maxResultJsonBytes=262144;maxRepositoryRelativePathCharacters=240;maxPathSegmentCharacters=100;maxJsonNestingDepth=16;maxSanitizedEvidenceArtifactBytes=1048576;maxCompressionRatio=100;requiredOuterPublicationFileCount=3;staleBasePolicy="EXACT_BASE_ONLY";emptyConsumedIdentitiesMeaning="FIRST_USE_NOT_YET_CONSUMED";matchingConsumedIdentityDisposition="REJECT_REPLAY";repositoryWriteCredentialAvailableToDeveloper=$false }
    foreach ($name in $policyNames) { if ([string]$evidence.policy.$name -cne [string]$expected.$name) { Stop-Chain "Focused suite P0 policy evidence mismatch: $name" } }
    return $evidence
}

function Test-FinalAggregate {
    $state = Read-JsonBounded $AggregateStatePath
    $expectedProperties = @("schemaVersion", "recoveryRepositorySha", "predecessors", "fileOwnership", "aggregateStateDigest")
    Assert-ExactProperties $state $expectedProperties "aggregate state"
    $recomputed = New-AggregateState ([string]$state.recoveryRepositorySha) @($state.predecessors)
    if ($recomputed.aggregateStateDigest -ne $state.aggregateStateDigest) { Stop-Chain "Aggregate state digest mismatch." }
    Assert-ExactSet @($state.fileOwnership.path) $Script:FinalFiles "final aggregate files"
    foreach ($file in @($state.fileOwnership)) {
        $path = Join-Path $WorkspacePath ([string]$file.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
        Assert-RegularMode100644 $path "Final aggregate file $($file.path)"
        if ($file.mode -ne "100644" -or (Get-Item $path).Length -ne [int64]$file.size -or (Get-Sha256 $path) -ne $file.sha256) { Stop-Chain "Final aggregate file metadata mismatch: $($file.path)" }
    }
    Assert-FinalP0Contract $WorkspacePath
    $testPath = Join-Path $WorkspacePath "tools/orchestrator/test-artifact-intake-contract.ps1"
    $focusedOutput = @(& pwsh -NoProfile -File $testPath)
    if ($LASTEXITCODE -ne 0) { Stop-Chain "Focused aggregate validation failed." }
    $evidence = Assert-FocusedSuiteEvidence $focusedOutput
    return [pscustomobject]@{ status = "FULLY_VALIDATED_FINAL_AGGREGATE"; aggregateStateDigest = $state.aggregateStateDigest; fileCount = 7; focusedValidation = "PASS"; focusedTestCount = [int]$evidence.testCount; p0PolicyValidation = "PASS"; repositoryWriteAuthority = $false; publicationPerformed = $false }
}

function Add-DescriptorToAggregate {
    $state = Read-JsonBounded $AggregateStatePath
    $descriptor = Read-JsonBounded $DescriptorPath
    $aggregate = New-AggregateState ([string]$state.recoveryRepositorySha) @(@($state.predecessors) + @($descriptor))
    Write-Utf8CanonicalJson $aggregate $AggregateStatePath
    return $aggregate
}

switch ($Mode) {
    "ValidateDescriptor" {
        $descriptor = Read-JsonBounded $DescriptorPath
        Test-DescriptorPackage $descriptor $ArtifactDirectory | Out-Null
        if ($RunId -and [string]$descriptor.runId -ne $RunId) { Stop-Chain "Expected run identity mismatch." }
        if ($ArtifactId -and [string]$descriptor.artifactId -ne $ArtifactId) { Stop-Chain "Expected artifact identity mismatch." }
        if ($GitHubArtifactDigest -and [string]$descriptor.githubArtifactDigest -ne $GitHubArtifactDigest) { Stop-Chain "Expected GitHub artifact digest mismatch." }
        if ($RecoveryRepositorySha -and [string]$descriptor.recoveryRepositorySha -ne $RecoveryRepositorySha) { Stop-Chain "Expected recovery SHA mismatch." }
        [pscustomobject]@{ status = "PASS"; taskId = $descriptor.childTaskId; phase = $descriptor.phase; fileCount = @($descriptor.ownedFiles).Count } | ConvertTo-Json -Depth 5
    }
    "InitializeWorkspace" { New-GenesisWorkspace | ConvertTo-Json -Depth 20 }
    "ValidateChildAuthorization" { Test-ChildAuthorization | ConvertTo-Json -Depth 10 }
    "MaterializeLocal" { Invoke-LocalMaterialization | ConvertTo-Json -Depth 20 }
    "BuildAggregateState" {
        $aggregate = New-AggregateState $RecoveryRepositorySha @(Get-LocalDescriptors)
        Write-Utf8CanonicalJson $aggregate $AggregateStatePath
        $aggregate | ConvertTo-Json -Depth 20
    }
    "AppendDescriptor" { Add-DescriptorToAggregate | ConvertTo-Json -Depth 20 }
    "AcquireAndMaterialize" { Invoke-AcquireAndMaterialize | ConvertTo-Json -Depth 20 }
    "ValidatePostClaude" {
        $baseline = Assert-BaselineUnchanged $BaselinePath $WorkspacePath $ExpectedBaselineSha256 $AggregateStatePath $ExpectedAggregateStateSha256
        [pscustomobject]@{ status = "PASS"; aggregateStateDigest = $baseline.aggregateStateDigest; predecessorFilesUnchanged = $true } | ConvertTo-Json
    }
    "PackageChild" { New-ChildPackage $WorkspacePath $BaselinePath $ManifestPath $OutputDirectory | ConvertTo-Json -Depth 10 }
    "NewDescriptor" { New-PredecessorDescriptor | ConvertTo-Json -Depth 10 }
    "ValidateFinalAggregate" { Test-FinalAggregate | ConvertTo-Json -Depth 5 }
}
