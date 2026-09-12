param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("ValidateDescriptor", "InitializeWorkspace", "ValidateChildAuthorization", "MaterializeLocal", "BuildAggregateState", "AppendDescriptor", "ClassifyBindings", "AcquireAndMaterialize", "ValidatePostClaude", "PackageChild", "NewDescriptor", "ValidateFinalAggregate", "ValidateLegacyCheckpoint", "NewCheckpointV2", "ValidateCheckpointV2")]
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
    ,[string] $CheckpointPath = ""
    ,[string[]] $CheckpointEvidencePaths = @()
    ,[string[]] $DescriptorEvidencePaths = @()
    ,[string[]] $PackageResultEvidencePaths = @()
    ,[string[]] $PublicationEvidencePaths = @()
    ,[string] $ChildDescriptorPath = ""
    ,[string] $ChildPackageResultPath = ""
    ,[string] $EvidenceDirectory = ""
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
$Script:LegacyA1AggregateFileSha256 = "750334cd06ecf529303b14ba0431f5ca492c714e606baca6767aef5961106ddc"
$Script:LegacyA1AggregateDigest = "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1"
$Script:LegacyA1DescriptorSha256 = "c1595130a63ffb3eaba0facbb4c0f1e73c5dbf17db098ea5052ca23ff021a253"
$Script:LegacyA1ExecutionSha = "8a0a441c532295405b8133d96142844026ee4a93"
$Script:LegacyA1TaskId = "VSP-AI02-001TI-A1D-VALIDATE"

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
    param([Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]] $Actual, [Parameter(Mandatory = $true)][string[]] $Expected, [Parameter(Mandatory = $true)][string] $Name)
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
        if ($baseline.sourceType -eq "AUTHORITATIVE_REPOSITORY_MERGE") {
            if ($state.schemaVersion -ne $baseline.checkpointSchemaVersion -or $state.aggregateStateDigest -ne $baseline.aggregateStateDigest) { Stop-Chain "Repository checkpoint and baseline lineage disagree." }
        } else {
            $recomputed = New-AggregateState ([string]$state.recoveryRepositorySha) @($state.predecessors)
            if ($recomputed.aggregateStateDigest -ne $state.aggregateStateDigest) { Stop-Chain "Pre-Claude aggregate state digest is invalid." }
            if (@($state.predecessors).Count -eq 0) {
                if ($baseline.aggregateStateDigest -ne "GENESIS") { Stop-Chain "Genesis baseline lineage is invalid." }
            } elseif ($baseline.aggregateStateDigest -ne $state.aggregateStateDigest) { Stop-Chain "Baseline and aggregate lineage disagree." }
        }
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

function Read-PredecessorBindings {
    try { $bindings = @($BindingsJson | ConvertFrom-Json -Depth $Script:MaxJsonNestingDepth) } catch { Stop-Chain "BindingsJson is malformed." }
    if ($bindings.Count -lt 1 -or $bindings.Count -gt 2) { Stop-Chain "A2/A3 requires one or two predecessor bindings." }
    foreach ($binding in $bindings) {
        if (-not ($binding.PSObject.Properties.Name -ccontains "sourceType")) { Stop-Chain "Predecessor binding sourceType is required." }
        if ($binding.sourceType -eq "ACTIONS_ARTIFACT") {
            Assert-ExactProperties $binding @("sourceType","descriptorArtifactId","descriptorArtifactName","descriptorArtifactDigest","packageArtifactId","packageArtifactName","packageArtifactDigest","runId","runAttempt","recoveryRepositorySha") "artifact binding"
            foreach ($id in @("descriptorArtifactId","packageArtifactId","runId")) { Assert-Matches $binding.$id '^[1-9][0-9]{0,19}$' "binding.$id" }
            foreach ($name in @("descriptorArtifactName","packageArtifactName")) { Assert-Matches $binding.$name '^[A-Za-z0-9._-]{1,200}$' "binding.$name" }
            foreach ($digest in @("descriptorArtifactDigest","packageArtifactDigest")) { Assert-Matches $binding.$digest '^sha256:[0-9a-f]{64}$' "binding.$digest" }
            if ([int]$binding.runAttempt -lt 1 -or [int]$binding.runAttempt -gt 2) { Stop-Chain "Artifact binding runAttempt is invalid." }
            Assert-Matches $binding.recoveryRepositorySha '^[0-9a-f]{40}$' "binding.recoveryRepositorySha"
        } elseif ($binding.sourceType -eq "AUTHORITATIVE_REPOSITORY_MERGE") {
            $names=@("sourceType","repository","mergeCommit","orderedMergeParents","publishedProductionHead","predecessorTaskId","phase","sequence","executionRepositorySha","checkpointSchemaVersion","descriptorSha256","aggregateStateDigest","aggregateFileSha256","packageSha256","manifestSha256","resultSha256","checkpointEvidenceBase64","descriptorEvidenceBase64","packageResultEvidenceBase64","productionFiles")
            Assert-ExactProperties $binding $names "repository-merge binding"
            if ($binding.repository -ne $Repository) { Stop-Chain "Repository-merge binding repository mismatch." }
            foreach ($sha in @("mergeCommit","publishedProductionHead","executionRepositorySha")) { Assert-Matches $binding.$sha '^[0-9a-f]{40}$' "binding.$sha" }
            if (@($binding.orderedMergeParents).Count -ne 2) { Stop-Chain "Repository-merge binding parent count is invalid." }
            foreach ($parent in @($binding.orderedMergeParents)) { Assert-Matches $parent '^[0-9a-f]{40}$' "binding.orderedMergeParent" }
            Assert-Matches $binding.predecessorTaskId '^VSP-AI02-[A-Za-z0-9-]+$' "binding.predecessorTaskId"
            $expectedSequence=@{A1=1;A2=2}[[string]$binding.phase]
            if ([string]::IsNullOrWhiteSpace([string]$expectedSequence) -or [int]$binding.sequence -ne $expectedSequence) { Stop-Chain "Repository-merge binding phase/sequence mismatch." }
            if ($binding.checkpointSchemaVersion -notin @("1.0","2.0")) { Stop-Chain "Repository-merge checkpoint schema is unsupported." }
            foreach ($hash in @("descriptorSha256","aggregateFileSha256","packageSha256","manifestSha256","resultSha256")) { Assert-Matches $binding.$hash '^[0-9a-f]{64}$' "binding.$hash" }
            Assert-Matches $binding.aggregateStateDigest '^sha256:[0-9a-f]{64}$' "binding.aggregateStateDigest"
            foreach ($evidence in @("checkpointEvidenceBase64","descriptorEvidenceBase64","packageResultEvidenceBase64")) { Assert-Matches $binding.$evidence '^[A-Za-z0-9+/]+={0,2}$' "binding.$evidence" }
            Assert-CheckpointOwnedFiles @($binding.productionFiles) ([string]$binding.phase) -RequireGitBlob
        } else { Stop-Chain "Unknown predecessor binding sourceType." }
    }
    return $bindings
}

function Write-ImmutableGitBlob {
    param([Parameter(Mandatory = $true)][string]$GitWorkspace,[Parameter(Mandatory = $true)][string]$BlobId,[Parameter(Mandatory = $true)][string]$Destination)
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName="git";$start.UseShellExecute=$false;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    foreach($argument in @("-C",$GitWorkspace,"cat-file","blob",$BlobId)){$start.ArgumentList.Add($argument)}
    try{
        $process=[Diagnostics.Process]::Start($start)
        $stream=[IO.File]::Open($Destination,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try{$process.StandardOutput.BaseStream.CopyTo($stream)}finally{$stream.Dispose()}
        $errorText=$process.StandardError.ReadToEnd();$process.WaitForExit()
        if($process.ExitCode-ne 0-or-not[string]::IsNullOrWhiteSpace($errorText)){Stop-Chain "Unable to materialize immutable repository predecessor blob."}
    }catch{
        if($_.Exception.Message-like"AI02 artifact chain validation failed:*"){throw}
        Stop-Chain "Unable to materialize immutable repository predecessor blob."
    }
}

function Get-PredecessorBindingRoute {
    param([Parameter(Mandatory = $true)][object[]] $Bindings)
    $types=@($Bindings.sourceType|Sort-Object -Unique)
    if ($types.Count -eq 1) { return [string]$types[0] }
    return "MIXED"
}

function Write-BoundEvidence {
    param([Parameter(Mandatory = $true)][string]$Base64,[Parameter(Mandatory = $true)][string]$Path,[Parameter(Mandatory = $true)][string]$ExpectedSha256)
    try { $bytes=[Convert]::FromBase64String($Base64) } catch { Stop-Chain "Repository-merge evidence encoding is invalid." }
    if ($bytes.Length -eq 0 -or $bytes.Length -gt $Script:MaxPerFileUncompressedBytes -or (Get-BytesSha256 $bytes) -ne $ExpectedSha256) { Stop-Chain "Repository-merge evidence digest mismatch." }
    [IO.File]::WriteAllBytes($Path,$bytes)
}

function Test-RepositoryBindingSummary {
    param([Parameter(Mandatory = $true)]$Binding,[Parameter(Mandatory = $true)]$Summary)
    if ($Binding.checkpointSchemaVersion -ne $Summary.checkpoint.schemaVersion -or $Binding.predecessorTaskId -ne $Summary.terminalTaskId -or $Binding.phase -ne $Summary.terminalPhase -or [int]$Binding.sequence -ne [int]$Summary.terminalSequence -or $Binding.executionRepositorySha -ne $Summary.terminalExecutionRepositorySha -or $Binding.descriptorSha256 -ne $Summary.descriptorSha256 -or $Binding.aggregateStateDigest -ne $Summary.aggregateStateDigest -or $Binding.aggregateFileSha256 -ne $Summary.checkpointFileSha256 -or $Binding.packageSha256 -ne $Summary.packageSha256 -or $Binding.manifestSha256 -ne $Summary.packageManifestSha256 -or $Binding.resultSha256 -ne $Summary.packageResultSha256) { Stop-Chain "Repository-merge checkpoint or package provenance mismatch." }
}

function Invoke-ActionsArtifactAcquisition {
    param([Parameter(Mandatory = $true)][object[]]$Bindings,[Parameter(Mandatory = $true)][string]$Token,[Parameter(Mandatory = $true)][string]$WorkRoot)
    Assert-NonBlank $Token "AI02_ARTIFACT_READ_TOKEN"
    $descriptors = @()
    foreach ($binding in $Bindings) {
        if ($binding.sourceType -ne "ACTIONS_ARTIFACT") { continue }
            if ([string]$binding.recoveryRepositorySha -ne $RecoveryRepositorySha) { Stop-Chain "Binding recovery SHA mismatch." }
            $descriptorBinding = [pscustomobject]@{ artifactId=$binding.descriptorArtifactId; artifactName=$binding.descriptorArtifactName; artifactDigest=$binding.descriptorArtifactDigest; runId=$binding.runId; recoveryRepositorySha=$binding.recoveryRepositorySha }
            $packageBinding = [pscustomobject]@{ artifactId=$binding.packageArtifactId; artifactName=$binding.packageArtifactName; artifactDigest=$binding.packageArtifactDigest; runId=$binding.runId; recoveryRepositorySha=$binding.recoveryRepositorySha }
            $itemRoot = Join-Path $WorkRoot ([string]$binding.runId)
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
    return $descriptors
}

function Invoke-RepositoryMergeAcquisition {
    param([Parameter(Mandatory = $true)][object[]]$Bindings,[Parameter(Mandatory = $true)][string]$WorkRoot)
    if (-not [string]::IsNullOrEmpty([string]$env:AI02_ARTIFACT_READ_TOKEN) -or -not [string]::IsNullOrEmpty([string]$env:GH_TOKEN) -or -not [string]::IsNullOrEmpty([string]$env:GITHUB_TOKEN)) { Stop-Chain "Repository-merge acquisition must be credentialless." }
    if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) { Stop-Chain "EvidenceDirectory is required for repository-merge acquisition." }
    $workspaceHead=@(Invoke-CheckpointGit @("rev-parse","HEAD") "CHILD_EXECUTION_BASE_MISMATCH")
    if($workspaceHead.Count-ne 1-or$workspaceHead[0]-ne$RecoveryRepositorySha){Stop-Chain "Repository workspace does not match the immutable recovery SHA."}
    New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
    $summary=$null;$previousPublicationPath="";$ownership=@();$lastCheckpointPath="";$repositoryIndex=0
    foreach($binding in $Bindings){
        if($binding.sourceType -ne "AUTHORITATIVE_REPOSITORY_MERGE"){continue}
        $prefix=Join-Path $EvidenceDirectory ([string]$repositoryIndex)
        $checkpointPath=$prefix+"-checkpoint.json";$descriptorPath=$prefix+"-descriptor.json";$resultPath=$prefix+"-result.json";$publicationPath=$prefix+"-publication.json"
        Write-BoundEvidence $binding.checkpointEvidenceBase64 $checkpointPath $binding.aggregateFileSha256
        Write-BoundEvidence $binding.descriptorEvidenceBase64 $descriptorPath $binding.descriptorSha256
        Write-BoundEvidence $binding.packageResultEvidenceBase64 $resultPath $binding.resultSha256
        if($binding.checkpointSchemaVersion -eq "1.0"){
            if($null-ne$summary){Stop-Chain "Legacy checkpoint cannot follow another checkpoint."}
            $summary=Test-LegacyCheckpointEvidence $checkpointPath $descriptorPath $resultPath
        }else{
            if($null-eq$summary-or[string]::IsNullOrWhiteSpace($previousPublicationPath)){Stop-Chain "Repository-merge lineage evidence is incomplete."}
            $summary=Test-CheckpointV2Evidence $checkpointPath $summary $previousPublicationPath $descriptorPath $resultPath
        }
        Test-RepositoryBindingSummary $binding $summary
        $publication=[pscustomobject][ordered]@{sourceType="AUTHORITATIVE_REPOSITORY_MERGE";repository=[string]$binding.repository;mergeCommit=[string]$binding.mergeCommit;orderedMergeParents=@($binding.orderedMergeParents);publishedProductionHead=[string]$binding.publishedProductionHead;checkpointAggregateStateDigest=[string]$binding.aggregateStateDigest;checkpointAggregateFileSha256=[string]$binding.aggregateFileSha256;productionFiles=@(Sort-OrdinalByPath @($binding.productionFiles)|ForEach-Object{ConvertTo-CanonicalCheckpointFile $_})}
        Write-Utf8CanonicalJson $publication $publicationPath
        $publication=Read-JsonBounded $publicationPath
        $canonicalPublication=Assert-ParentPublication $publication $summary
        Test-ParentPublicationGitBinding $canonicalPublication $summary $RecoveryRepositorySha
        foreach($file in @($binding.productionFiles)){
            $target=Join-Path $WorkspacePath ([string]$file.path).Replace('/',[IO.Path]::DirectorySeparatorChar)
            Assert-RegularMode100644 $target "Repository predecessor $($file.path)" $WorkspacePath ([string]$file.path)
            Write-ImmutableGitBlob $WorkspacePath ([string]$file.gitBlobId) $target
            if((Get-Item -LiteralPath $target).Length-ne[int64]$file.size-or(Get-Sha256 $target)-ne$file.sha256){Stop-Chain "Repository predecessor materialization mismatch."}
        }
        $ownership+=@($summary.descriptor.ownedFiles|ForEach-Object{[ordered]@{path=[string]$_.path;ownerTaskId=[string]$summary.terminalTaskId;phase=[string]$summary.terminalPhase;mode=[string]$_.mode;size=[int64]$_.size;sha256=[string]$_.sha256}})
        $previousPublicationPath=$publicationPath;$lastCheckpointPath=$checkpointPath;$repositoryIndex++
    }
    if($repositoryIndex-eq 0){return $null}
    Copy-Item -LiteralPath $lastCheckpointPath -Destination $AggregateStatePath
    $baseline=[ordered]@{schemaVersion="2.0";sourceType="AUTHORITATIVE_REPOSITORY_MERGE";recoveryRepositorySha=$RecoveryRepositorySha;checkpointSchemaVersion=[string]$summary.checkpoint.schemaVersion;aggregateStateDigest=[string]$summary.aggregateStateDigest;predecessorFiles=@(Sort-OrdinalByPath $ownership)}
    Write-Utf8CanonicalJson $baseline $BaselinePath
    return $summary
}

function Invoke-AcquireAndMaterialize {
    Assert-Matches $RecoveryRepositorySha '^[0-9a-f]{40}$' "RecoveryRepositorySha"
    $bindings=@(Read-PredecessorBindings)
    $route=Get-PredecessorBindingRoute $bindings
    $token=[string]$env:AI02_ARTIFACT_READ_TOKEN
    $workRoot = Join-Path ([IO.Path]::GetTempPath()) ("ai02-chain-acquire-" + [Guid]::NewGuid().ToString("N"))
    try {
        if($route-eq"AUTHORITATIVE_REPOSITORY_MERGE"){
            $summary=Invoke-RepositoryMergeAcquisition $bindings $workRoot
            return [pscustomobject]@{schemaVersion="2.0";sourceType=$route;recoveryRepositorySha=$RecoveryRepositorySha;aggregateStateDigest=$summary.aggregateStateDigest;predecessorCount=$bindings.Count;repositoryWriteCredentialAvailableToDeveloper=$false}
        }
        $descriptors=@(Invoke-ActionsArtifactAcquisition $bindings $token $workRoot)
        if($descriptors.Count-ne$bindings.Count){Stop-Chain "Mixed predecessor source acquisition is not supported by this phase."}
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

function Stop-Checkpoint {
    param([Parameter(Mandatory = $true)][string] $Code)
    Stop-Chain "checkpoint validation rejected [$Code]."
}

function Get-CanonicalJsonBytes {
    param([Parameter(Mandatory = $true)] $Value)
    return [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Compress -Depth 30))
}

function Test-ByteArrayEqual {
    param([Parameter(Mandatory = $true)][byte[]] $Left, [Parameter(Mandatory = $true)][byte[]] $Right)
    if ($Left.Length -ne $Right.Length) { return $false }
    for ($index = 0; $index -lt $Left.Length; $index++) {
        if ($Left[$index] -ne $Right[$index]) { return $false }
    }
    return $true
}

function Read-CanonicalJsonEvidence {
    param([Parameter(Mandatory = $true)][string] $Path, [Parameter(Mandatory = $true)][string] $FailureCode)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Stop-Checkpoint $FailureCode }
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).Path)
    if ($bytes.Length -eq 0 -or $bytes.Length -gt $Script:MaxPerFileUncompressedBytes) { Stop-Checkpoint $FailureCode }
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    try {
        $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
        $value = $text | ConvertFrom-Json -Depth 30
    } catch { Stop-Checkpoint $FailureCode }
    $canonical = Get-CanonicalJsonBytes $value
    if (-not (Test-ByteArrayEqual $bytes $canonical)) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    return [pscustomobject]@{ value=$value; bytes=$bytes; sha256=(Get-BytesSha256 $bytes) }
}

function Read-PackageResultEvidence {
    param([Parameter(Mandatory = $true)][string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Stop-Checkpoint "CHILD_PACKAGE_BINDING_MISMATCH" }
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).Path)
    if ($bytes.Length -eq 0 -or $bytes.Length -gt $Script:MaxResultJsonBytes) { Stop-Checkpoint "CHILD_PACKAGE_BINDING_MISMATCH" }
    try {
        $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
        $value = $text | ConvertFrom-Json -Depth 30
    } catch { Stop-Checkpoint "CHILD_PACKAGE_BINDING_MISMATCH" }
    $canonical = Get-CanonicalJsonBytes $value
    $legacyLf = [byte[]]::new($canonical.Length + 1)
    [Array]::Copy($canonical, $legacyLf, $canonical.Length)
    $legacyLf[$legacyLf.Length - 1] = 0x0A
    $isCanonical = Test-ByteArrayEqual $bytes $canonical
    $isExactLegacyLf = Test-ByteArrayEqual $bytes $legacyLf
    if (-not $isCanonical -and -not $isExactLegacyLf) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    return [pscustomobject]@{ value=$value; bytes=$bytes; sha256=(Get-BytesSha256 $bytes); exactLegacyLf=$isExactLegacyLf }
}

function ConvertTo-CanonicalCheckpointFile {
    param([Parameter(Mandatory = $true)] $File)
    return [ordered]@{ path=[string]$File.path; mode=[string]$File.mode; gitBlobId=[string]$File.gitBlobId; size=[int64]$File.size; sha256=[string]$File.sha256 }
}

function ConvertTo-CanonicalCheckpointChildFile {
    param([Parameter(Mandatory = $true)] $File)
    return [ordered]@{ path=[string]$File.path; mode=[string]$File.mode; size=[int64]$File.size; sha256=[string]$File.sha256 }
}

function ConvertTo-CanonicalPackageResult {
    param([Parameter(Mandatory = $true)] $Result)
    $files = @(Sort-OrdinalByPath @($Result.changedFiles) | ForEach-Object { ConvertTo-CanonicalCheckpointChildFile $_ })
    return [ordered]@{
        taskId=[string]$Result.taskId; approvedBaseSha=[string]$Result.approvedBaseSha
        parentAggregateStateDigest=[string]$Result.parentAggregateStateDigest; changedFiles=$files
        packageSha256=[string]$Result.packageSha256; manifestSha256=[string]$Result.manifestSha256
        repositoryWriteCredentialAvailableToDeveloper=[bool]$Result.repositoryWriteCredentialAvailableToDeveloper
        productOwnerManualTransport=[bool]$Result.productOwnerManualTransport
    }
}

function ConvertTo-CanonicalParentCheckpoint {
    param([Parameter(Mandatory = $true)] $Parent)
    return [ordered]@{
        schemaVersion=[string]$Parent.schemaVersion
        aggregateStateDigest=[string]$Parent.aggregateStateDigest
        aggregateFileSha256=[string]$Parent.aggregateFileSha256
        terminalDescriptorSha256=[string]$Parent.terminalDescriptorSha256
        terminalTaskId=[string]$Parent.terminalTaskId
        terminalPhase=[string]$Parent.terminalPhase
        terminalSequence=[int]$Parent.terminalSequence
        terminalExecutionRepositorySha=[string]$Parent.terminalExecutionRepositorySha
        terminalPackageSha256=[string]$Parent.terminalPackageSha256
        terminalPackageManifestSha256=[string]$Parent.terminalPackageManifestSha256
        terminalPackageResultSha256=[string]$Parent.terminalPackageResultSha256
    }
}

function ConvertTo-CanonicalParentPublication {
    param([Parameter(Mandatory = $true)] $Publication)
    $files = @(Sort-OrdinalByPath @($Publication.productionFiles) | ForEach-Object { ConvertTo-CanonicalCheckpointFile $_ })
    return [ordered]@{
        sourceType=[string]$Publication.sourceType
        repository=[string]$Publication.repository
        mergeCommit=[string]$Publication.mergeCommit
        orderedMergeParents=@($Publication.orderedMergeParents | ForEach-Object { [string]$_ })
        publishedProductionHead=[string]$Publication.publishedProductionHead
        checkpointAggregateStateDigest=[string]$Publication.checkpointAggregateStateDigest
        checkpointAggregateFileSha256=[string]$Publication.checkpointAggregateFileSha256
        productionFiles=$files
    }
}

function ConvertTo-CanonicalCheckpointChild {
    param([Parameter(Mandatory = $true)] $Child)
    $files = @(Sort-OrdinalByPath @($Child.ownedFiles) | ForEach-Object { ConvertTo-CanonicalCheckpointChildFile $_ })
    return [ordered]@{
        taskId=[string]$Child.taskId
        phase=[string]$Child.phase
        sequence=[int]$Child.sequence
        executionRepositorySha=[string]$Child.executionRepositorySha
        descriptorSha256=[string]$Child.descriptorSha256
        packageSha256=[string]$Child.packageSha256
        packageManifestSha256=[string]$Child.packageManifestSha256
        packageResultSha256=[string]$Child.packageResultSha256
        ownedFiles=$files
    }
}

function ConvertTo-CanonicalCheckpointV2 {
    param([Parameter(Mandatory = $true)] $Checkpoint, [switch] $ExcludeDigest)
    $canonical = [ordered]@{
        schemaVersion="2.0"
        checkpointType="PHASED_CHILD_CHECKPOINT"
        checkpointPolicyVersion="vsp-ai02-checkpoint-chain-v2"
        parentCheckpoint=(ConvertTo-CanonicalParentCheckpoint $Checkpoint.parentCheckpoint)
        parentPublication=(ConvertTo-CanonicalParentPublication $Checkpoint.parentPublication)
        child=(ConvertTo-CanonicalCheckpointChild $Checkpoint.child)
    }
    if (-not $ExcludeDigest) { $canonical.aggregateStateDigest=[string]$Checkpoint.aggregateStateDigest }
    return $canonical
}

function Assert-CheckpointOwnedFiles {
    param([Parameter(Mandatory = $true)][object[]] $Files, [Parameter(Mandatory = $true)][string] $Phase, [switch] $RequireGitBlob)
    $properties = if ($RequireGitBlob) { @("path","mode","gitBlobId","size","sha256") } else { @("path","mode","size","sha256") }
    Assert-ExactSet @($Files.path) (Get-PhaseFiles $Phase) "checkpoint files"
    Assert-UniquePaths @($Files.path) "checkpoint files"
    $sorted = @(Sort-OrdinalByPath $Files)
    for ($index=0; $index -lt $Files.Count; $index++) {
        if ([string]$Files[$index].path -cne [string]$sorted[$index].path) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    }
    foreach ($file in $Files) {
        Assert-ExactProperties $file $properties "checkpoint file"
        Assert-RepoPath ([string]$file.path) "checkpoint file path" | Out-Null
        if ($file.mode -ne "100644") { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
        if ($RequireGitBlob) { Assert-Matches $file.gitBlobId '^[0-9a-f]{40}$' "gitBlobId" }
        if ([int64]$file.size -le 0 -or [int64]$file.size -gt $Script:MaxPerFileUncompressedBytes) { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
        Assert-Matches $file.sha256 '^[0-9a-f]{64}$' "sha256"
    }
}

function Test-PackageResultEvidence {
    param([Parameter(Mandatory = $true)] $Descriptor, [Parameter(Mandatory = $true)][string] $ResultPath)
    $evidence = Read-PackageResultEvidence $ResultPath
    $result = $evidence.value
    Assert-ExactProperties $result @("taskId","approvedBaseSha","parentAggregateStateDigest","changedFiles","packageSha256","manifestSha256","repositoryWriteCredentialAvailableToDeveloper","productOwnerManualTransport") "package result"
    $canonicalBytes = Get-CanonicalJsonBytes (ConvertTo-CanonicalPackageResult $result)
    $expectedBytes = if ($evidence.exactLegacyLf) { $withLf=[byte[]]::new($canonicalBytes.Length+1);[Array]::Copy($canonicalBytes,$withLf,$canonicalBytes.Length);$withLf[-1]=0x0A;$withLf } else { $canonicalBytes }
    if (-not (Test-ByteArrayEqual $evidence.bytes $expectedBytes)) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    if ($result.taskId -ne $Descriptor.childTaskId -or $result.approvedBaseSha -ne $Descriptor.recoveryRepositorySha -or $result.parentAggregateStateDigest -ne $Descriptor.parentAggregateStateDigest -or $result.packageSha256 -ne $Descriptor.packageSha256 -or $result.manifestSha256 -ne $Descriptor.manifestSha256 -or $result.repositoryWriteCredentialAvailableToDeveloper -ne $false -or $result.productOwnerManualTransport -ne $false) { Stop-Checkpoint "CHILD_PACKAGE_BINDING_MISMATCH" }
    Assert-ExactSet @($result.changedFiles.path) @($Descriptor.ownedFiles.path) "package result files"
    foreach ($file in @($Descriptor.ownedFiles)) {
        $candidate = @($result.changedFiles | Where-Object { $_.path -ceq $file.path })
        if ($candidate.Count -ne 1 -or $candidate[0].mode -ne $file.mode -or [int64]$candidate[0].size -ne [int64]$file.size -or $candidate[0].sha256 -ne $file.sha256) { Stop-Checkpoint "CHILD_PACKAGE_BINDING_MISMATCH" }
    }
    return $evidence.sha256
}

function Test-LegacyCheckpointEvidence {
    param([Parameter(Mandatory = $true)][string] $LegacyPath, [Parameter(Mandatory = $true)][string] $TerminalDescriptorPath, [Parameter(Mandatory = $true)][string] $ResultPath)
    $legacyEvidence = Read-CanonicalJsonEvidence $LegacyPath "LEGACY_IMPORT_MISMATCH"
    $legacy = $legacyEvidence.value
    Assert-ExactProperties $legacy @("schemaVersion","recoveryRepositorySha","predecessors","fileOwnership","aggregateStateDigest") "legacy aggregate"
    if ($legacy.schemaVersion -ne "1.0" -or @($legacy.predecessors).Count -lt 1) { Stop-Checkpoint "LEGACY_IMPORT_MISMATCH" }
    if ($legacyEvidence.sha256 -ne $Script:LegacyA1AggregateFileSha256 -or $legacy.aggregateStateDigest -ne $Script:LegacyA1AggregateDigest -or $legacy.recoveryRepositorySha -ne $Script:LegacyA1ExecutionSha) { Stop-Checkpoint "LEGACY_IMPORT_MISMATCH" }
    $recomputed = New-AggregateState ([string]$legacy.recoveryRepositorySha) @($legacy.predecessors)
    if ((Get-BytesSha256 (Get-CanonicalJsonBytes $recomputed)) -ne $legacyEvidence.sha256 -or $recomputed.aggregateStateDigest -ne $legacy.aggregateStateDigest) { Stop-Checkpoint "LEGACY_IMPORT_MISMATCH" }
    $descriptorEvidence = Read-CanonicalJsonEvidence $TerminalDescriptorPath "PARENT_TERMINAL_DESCRIPTOR_MISMATCH"
    $descriptor = $descriptorEvidence.value
    Assert-Descriptor $descriptor
    if ($descriptorEvidence.sha256 -ne $Script:LegacyA1DescriptorSha256 -or $descriptor.childTaskId -ne $Script:LegacyA1TaskId -or $descriptor.phase -ne "A1" -or [int]$descriptor.sequence -ne 1 -or $descriptor.recoveryRepositorySha -ne $Script:LegacyA1ExecutionSha -or $descriptor.parentAggregateStateDigest -ne "GENESIS") { Stop-Checkpoint "LEGACY_IMPORT_MISMATCH" }
    $terminal = @($legacy.predecessors)[-1]
    if (-not (Test-ByteArrayEqual (Get-CanonicalJsonBytes (ConvertTo-CanonicalDescriptor $terminal)) $descriptorEvidence.bytes)) { Stop-Checkpoint "PARENT_TERMINAL_DESCRIPTOR_MISMATCH" }
    $resultHash = Test-PackageResultEvidence $descriptor $ResultPath
    return [pscustomobject]@{
        checkpoint=$legacy; checkpointBytes=$legacyEvidence.bytes; checkpointFileSha256=$legacyEvidence.sha256
        descriptor=$descriptor; descriptorSha256=$descriptorEvidence.sha256; packageResultSha256=$resultHash
        terminalTaskId=[string]$descriptor.childTaskId; terminalPhase=[string]$descriptor.phase; terminalSequence=[int]$descriptor.sequence
        terminalExecutionRepositorySha=[string]$descriptor.recoveryRepositorySha; aggregateStateDigest=[string]$legacy.aggregateStateDigest
        packageSha256=[string]$descriptor.packageSha256; packageManifestSha256=[string]$descriptor.manifestSha256
    }
}

function Assert-ParentPublication {
    param([Parameter(Mandatory = $true)] $Publication, [Parameter(Mandatory = $true)] $ParentSummary)
    Assert-ExactProperties $Publication @("sourceType","repository","mergeCommit","orderedMergeParents","publishedProductionHead","checkpointAggregateStateDigest","checkpointAggregateFileSha256","productionFiles") "parent publication"
    if ($Publication.sourceType -ne "AUTHORITATIVE_REPOSITORY_MERGE" -or $Publication.repository -ne $Repository) { Stop-Checkpoint "PUBLICATION_MERGE_MISMATCH" }
    Assert-Matches $Publication.mergeCommit '^[0-9a-f]{40}$' "mergeCommit"
    if (@($Publication.orderedMergeParents).Count -ne 2) { Stop-Checkpoint "PUBLICATION_PARENT_MISMATCH" }
    foreach ($parent in @($Publication.orderedMergeParents)) { Assert-Matches $parent '^[0-9a-f]{40}$' "orderedMergeParent" }
    Assert-Matches $Publication.publishedProductionHead '^[0-9a-f]{40}$' "publishedProductionHead"
    if ([string]$Publication.orderedMergeParents[1] -ne [string]$Publication.publishedProductionHead) { Stop-Checkpoint "PUBLICATION_PARENT_MISMATCH" }
    if ([string]$Publication.orderedMergeParents[0] -ne [string]$ParentSummary.terminalExecutionRepositorySha) { Stop-Checkpoint "PUBLICATION_PARENT_MISMATCH" }
    if ($Publication.checkpointAggregateStateDigest -ne $ParentSummary.aggregateStateDigest -or $Publication.checkpointAggregateFileSha256 -ne $ParentSummary.checkpointFileSha256) { Stop-Checkpoint "PUBLICATION_MERGE_MISMATCH" }
    $files = @($Publication.productionFiles)
    Assert-CheckpointOwnedFiles $files $ParentSummary.terminalPhase -RequireGitBlob
    Assert-ExactSet @($files.path) @($ParentSummary.descriptor.ownedFiles.path) "publication descriptor files"
    foreach ($owned in @($ParentSummary.descriptor.ownedFiles)) {
        $file = @($files | Where-Object { $_.path -ceq $owned.path })
        if ($file.Count -ne 1 -or $file[0].mode -ne $owned.mode -or [int64]$file[0].size -ne [int64]$owned.size -or $file[0].sha256 -ne $owned.sha256) { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
    }
    return (ConvertTo-CanonicalParentPublication $Publication)
}

function Invoke-CheckpointGit {
    param([Parameter(Mandatory = $true)][string[]] $Arguments, [Parameter(Mandatory = $true)][string] $FailureCode)
    if ([string]::IsNullOrWhiteSpace($WorkspacePath) -or -not (Test-Path -LiteralPath $WorkspacePath -PathType Container)) { Stop-Checkpoint $FailureCode }
    $output = @(& git -C $WorkspacePath @Arguments 2>$null | ForEach-Object { [string]$_ })
    if ($LASTEXITCODE -ne 0) { Stop-Checkpoint $FailureCode }
    return $output
}

function Get-CheckpointGitBlobSha256 {
    param([Parameter(Mandatory = $true)][string] $BlobId)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = "git"
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @("-C",$WorkspacePath,"cat-file","blob",$BlobId)) { $start.ArgumentList.Add($argument) }
    try {
        $process = [Diagnostics.Process]::Start($start)
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = $sha.ComputeHash($process.StandardOutput.BaseStream) } finally { $sha.Dispose() }
        $errorText = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0 -or -not [string]::IsNullOrWhiteSpace($errorText)) { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
        return (($hash | ForEach-Object { $_.ToString("x2") }) -join "")
    } catch {
        if ($_.Exception.Message -like "AI02 artifact chain validation failed:*") { throw }
        Stop-Checkpoint "INTERNAL_VALIDATION_ERROR"
    }
}

function Test-ParentPublicationGitBinding {
    param([Parameter(Mandatory = $true)] $Publication, [Parameter(Mandatory = $true)] $ParentSummary, [Parameter(Mandatory = $true)][string] $ChildExecutionSha)
    Assert-Matches $ChildExecutionSha '^[0-9a-f]{40}$' "child execution SHA"
    $mergeType = @(Invoke-CheckpointGit @("cat-file","-t",[string]$Publication.mergeCommit) "PUBLICATION_MERGE_MISMATCH")
    if ($mergeType.Count -ne 1 -or $mergeType[0] -ne "commit") { Stop-Checkpoint "PUBLICATION_MERGE_MISMATCH" }
    $childType = @(Invoke-CheckpointGit @("cat-file","-t",$ChildExecutionSha) "CHILD_EXECUTION_BASE_MISMATCH")
    if ($childType.Count -ne 1 -or $childType[0] -ne "commit") { Stop-Checkpoint "CHILD_EXECUTION_BASE_MISMATCH" }
    $parentLines = @(Invoke-CheckpointGit @("show","-s","--format=%P",[string]$Publication.mergeCommit) "PUBLICATION_PARENT_MISMATCH")
    if ($parentLines.Count -ne 1) { Stop-Checkpoint "PUBLICATION_PARENT_MISMATCH" }
    $parents = @(([string]$parentLines[0]).Split(' ',[StringSplitOptions]::RemoveEmptyEntries))
    if ($parents.Count -ne 2 -or $parents[0] -ne [string]$Publication.orderedMergeParents[0] -or $parents[1] -ne [string]$Publication.orderedMergeParents[1]) { Stop-Checkpoint "PUBLICATION_PARENT_MISMATCH" }
    & git -C $WorkspacePath merge-base --is-ancestor ([string]$Publication.mergeCommit) $ChildExecutionSha 2>$null
    if ($LASTEXITCODE -ne 0) { Stop-Checkpoint "EXECUTION_BASE_ANCESTRY_FAILURE" }
    $changed = @(Invoke-CheckpointGit @("diff","--name-only",$parents[0],[string]$Publication.mergeCommit,"--") "PUBLICATION_CONTENT_MISMATCH")
    Assert-ExactSet $changed @($Publication.productionFiles.path) "publication merge changed files"
    foreach ($file in @($Publication.productionFiles)) {
        $mergeTree = @(Invoke-CheckpointGit @("ls-tree",[string]$Publication.mergeCommit,"--",[string]$file.path) "PUBLICATION_CONTENT_MISMATCH")
        $publishedTree = @(Invoke-CheckpointGit @("ls-tree",[string]$Publication.publishedProductionHead,"--",[string]$file.path) "PUBLICATION_CONTENT_MISMATCH")
        $childTree = @(Invoke-CheckpointGit @("ls-tree",$ChildExecutionSha,"--",[string]$file.path) "PUBLICATION_CONTENT_MISMATCH")
        if ($mergeTree.Count -ne 1 -or $publishedTree.Count -ne 1 -or $childTree.Count -ne 1 -or $mergeTree[0] -cnotmatch '^([0-9]{6}) blob ([0-9a-f]{40})\t(.+)$') { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
        $mode=$Matches[1];$blob=$Matches[2];$path=$Matches[3]
        if ($mode -ne $file.mode -or $blob -ne $file.gitBlobId -or $path -cne $file.path) { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
        if ($publishedTree[0] -cnotmatch '^([0-9]{6}) blob ([0-9a-f]{40})\t(.+)$' -or $Matches[1] -ne $file.mode -or $Matches[2] -ne $file.gitBlobId -or $Matches[3] -cne $file.path) { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
        if ($childTree[0] -cnotmatch '^([0-9]{6}) blob ([0-9a-f]{40})\t(.+)$' -or $Matches[1] -ne $file.mode -or $Matches[2] -ne $file.gitBlobId -or $Matches[3] -cne $file.path) { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
        $sizeLines = @(Invoke-CheckpointGit @("cat-file","-s",$blob) "PUBLICATION_CONTENT_MISMATCH")
        if ($sizeLines.Count -ne 1) { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
        $size = [int64]$sizeLines[0]
        if ($size -ne [int64]$file.size -or (Get-CheckpointGitBlobSha256 $blob) -ne $file.sha256) { Stop-Checkpoint "PUBLICATION_CONTENT_MISMATCH" }
    }
}

function Get-CheckpointChildFromEvidence {
    param([Parameter(Mandatory = $true)] $DescriptorEvidence, [Parameter(Mandatory = $true)][string] $ResultPath)
    $descriptor = $DescriptorEvidence.value
    Assert-Descriptor $descriptor
    if (-not (Test-ByteArrayEqual $DescriptorEvidence.bytes (Get-CanonicalJsonBytes (ConvertTo-CanonicalDescriptor $descriptor)))) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    $expectedTask = @{ A2="VSP-AI02-001TI-A2"; A3="VSP-AI02-001TI-A3" }[[string]$descriptor.phase]
    if ([string]::IsNullOrWhiteSpace($expectedTask) -or $descriptor.childTaskId -ne $expectedTask) { Stop-Checkpoint "CHILD_DESCRIPTOR_MISMATCH" }
    $resultHash = Test-PackageResultEvidence $descriptor $ResultPath
    return [ordered]@{
        taskId=[string]$descriptor.childTaskId; phase=[string]$descriptor.phase; sequence=[int]$descriptor.sequence
        executionRepositorySha=[string]$descriptor.recoveryRepositorySha; descriptorSha256=[string]$DescriptorEvidence.sha256
        packageSha256=[string]$descriptor.packageSha256; packageManifestSha256=[string]$descriptor.manifestSha256
        packageResultSha256=$resultHash; ownedFiles=@(Sort-OrdinalByPath @($descriptor.ownedFiles) | ForEach-Object { ConvertTo-CanonicalCheckpointChildFile $_ })
    }
}

function Test-CheckpointV2Evidence {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)] $ParentSummary,
        [Parameter(Mandatory = $true)][string] $PublicationPath,
        [Parameter(Mandatory = $true)][string] $TerminalDescriptorPath,
        [Parameter(Mandatory = $true)][string] $ResultPath
    )
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Stop-Checkpoint "PARENT_CHECKPOINT_MISSING" }
    $evidence = Read-CanonicalJsonEvidence $Path "CHECKPOINT_CANONICALIZATION_MISMATCH"
    $checkpoint = $evidence.value
    Assert-ExactProperties $checkpoint @("schemaVersion","checkpointType","checkpointPolicyVersion","parentCheckpoint","parentPublication","child","aggregateStateDigest") "v2 checkpoint"
    if ($checkpoint.schemaVersion -ne "2.0") { Stop-Checkpoint "CHECKPOINT_VERSION_UNSUPPORTED" }
    if ($checkpoint.checkpointType -ne "PHASED_CHILD_CHECKPOINT" -or $checkpoint.checkpointPolicyVersion -ne "vsp-ai02-checkpoint-chain-v2") { Stop-Checkpoint "CHECKPOINT_TYPE_UNSUPPORTED" }
    if (-not (Test-ByteArrayEqual $evidence.bytes (Get-CanonicalJsonBytes (ConvertTo-CanonicalCheckpointV2 $checkpoint)))) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    $parent = $checkpoint.parentCheckpoint
    Assert-ExactProperties $parent @("schemaVersion","aggregateStateDigest","aggregateFileSha256","terminalDescriptorSha256","terminalTaskId","terminalPhase","terminalSequence","terminalExecutionRepositorySha","terminalPackageSha256","terminalPackageManifestSha256","terminalPackageResultSha256") "parent checkpoint"
    if ($parent.schemaVersion -ne [string]$ParentSummary.checkpoint.schemaVersion) { Stop-Checkpoint "CHECKPOINT_MODEL_AMBIGUOUS" }
    if ($parent.aggregateStateDigest -ne $ParentSummary.aggregateStateDigest) { Stop-Checkpoint "PARENT_CHECKPOINT_DIGEST_MISMATCH" }
    if ($parent.aggregateFileSha256 -ne $ParentSummary.checkpointFileSha256) { Stop-Checkpoint "PARENT_CHECKPOINT_FILE_HASH_MISMATCH" }
    if ($parent.terminalDescriptorSha256 -ne $ParentSummary.descriptorSha256) { Stop-Checkpoint "PARENT_TERMINAL_DESCRIPTOR_MISMATCH" }
    if ($parent.terminalTaskId -ne $ParentSummary.terminalTaskId) { Stop-Checkpoint "PARENT_TERMINAL_TASK_MISMATCH" }
    if ([int]$parent.terminalSequence -ne [int]$ParentSummary.terminalSequence -or $parent.terminalPhase -ne $ParentSummary.terminalPhase) { Stop-Checkpoint "PARENT_TERMINAL_SEQUENCE_MISMATCH" }
    if ($parent.terminalExecutionRepositorySha -ne $ParentSummary.terminalExecutionRepositorySha) { Stop-Checkpoint "PARENT_EXECUTION_BASE_MISMATCH" }
    if ($parent.terminalPackageSha256 -ne $ParentSummary.packageSha256 -or $parent.terminalPackageManifestSha256 -ne $ParentSummary.packageManifestSha256 -or $parent.terminalPackageResultSha256 -ne $ParentSummary.packageResultSha256) { Stop-Checkpoint "CHILD_PACKAGE_BINDING_MISMATCH" }
    $publicationEvidence = Read-CanonicalJsonEvidence $PublicationPath "PUBLICATION_MERGE_MISMATCH"
    $canonicalPublication = Assert-ParentPublication $publicationEvidence.value $ParentSummary
    if (-not (Test-ByteArrayEqual $publicationEvidence.bytes (Get-CanonicalJsonBytes $canonicalPublication))) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    if (-not (Test-ByteArrayEqual (Get-CanonicalJsonBytes $canonicalPublication) (Get-CanonicalJsonBytes $checkpoint.parentPublication))) { Stop-Checkpoint "PUBLICATION_MERGE_MISMATCH" }
    $descriptorEvidence = Read-CanonicalJsonEvidence $TerminalDescriptorPath "CHILD_DESCRIPTOR_MISMATCH"
    $child = Get-CheckpointChildFromEvidence $descriptorEvidence $ResultPath
    Test-ParentPublicationGitBinding $checkpoint.parentPublication $ParentSummary ([string]$child.executionRepositorySha)
    Assert-ExactProperties $checkpoint.child @("taskId","phase","sequence","executionRepositorySha","descriptorSha256","packageSha256","packageManifestSha256","packageResultSha256","ownedFiles") "checkpoint child"
    Assert-CheckpointOwnedFiles @($checkpoint.child.ownedFiles) ([string]$checkpoint.child.phase)
    if (-not (Test-ByteArrayEqual (Get-CanonicalJsonBytes $child) (Get-CanonicalJsonBytes (ConvertTo-CanonicalCheckpointChild $checkpoint.child)))) { Stop-Checkpoint "CHILD_DESCRIPTOR_MISMATCH" }
    if ([int]$checkpoint.child.sequence -ne ([int]$ParentSummary.terminalSequence + 1)) { Stop-Checkpoint "CHILD_SEQUENCE_MISMATCH" }
    $expectedPhase = @{2="A2";3="A3"}[[int]$checkpoint.child.sequence]
    if ([string]::IsNullOrWhiteSpace($expectedPhase) -or $checkpoint.child.phase -ne $expectedPhase) { Stop-Checkpoint "LINEAGE_SEQUENCE_GAP" }
    $digest = "sha256:" + (Get-BytesSha256 (Get-CanonicalJsonBytes (ConvertTo-CanonicalCheckpointV2 $checkpoint -ExcludeDigest)))
    if ($checkpoint.aggregateStateDigest -ne $digest) { Stop-Checkpoint "CHECKPOINT_DIGEST_MISMATCH" }
    return [pscustomobject]@{
        checkpoint=$checkpoint; checkpointBytes=$evidence.bytes; checkpointFileSha256=$evidence.sha256
        descriptor=$descriptorEvidence.value; descriptorSha256=$descriptorEvidence.sha256; packageResultSha256=[string]$checkpoint.child.packageResultSha256
        terminalTaskId=[string]$checkpoint.child.taskId; terminalPhase=[string]$checkpoint.child.phase; terminalSequence=[int]$checkpoint.child.sequence
        terminalExecutionRepositorySha=[string]$checkpoint.child.executionRepositorySha; aggregateStateDigest=[string]$checkpoint.aggregateStateDigest
        packageSha256=[string]$checkpoint.child.packageSha256; packageManifestSha256=[string]$checkpoint.child.packageManifestSha256
    }
}

function Test-CheckpointEvidenceBundle {
    param([switch] $ForExtension)
    if ($CheckpointEvidencePaths.Count -lt 1 -or $CheckpointEvidencePaths.Count -gt 3 -or $DescriptorEvidencePaths.Count -ne $CheckpointEvidencePaths.Count -or $PackageResultEvidencePaths.Count -ne $CheckpointEvidencePaths.Count) { Stop-Checkpoint "LINEAGE_EVIDENCE_INCOMPLETE" }
    $requiredPublications = if ($ForExtension) { $CheckpointEvidencePaths.Count } else { $CheckpointEvidencePaths.Count - 1 }
    if ($PublicationEvidencePaths.Count -ne $requiredPublications) { Stop-Checkpoint "LINEAGE_EVIDENCE_INCOMPLETE" }
    $summary = Test-LegacyCheckpointEvidence $CheckpointEvidencePaths[0] $DescriptorEvidencePaths[0] $PackageResultEvidencePaths[0]
    for ($index=1; $index -lt $CheckpointEvidencePaths.Count; $index++) {
        $summary = Test-CheckpointV2Evidence $CheckpointEvidencePaths[$index] $summary $PublicationEvidencePaths[$index-1] $DescriptorEvidencePaths[$index] $PackageResultEvidencePaths[$index]
    }
    return $summary
}

function New-CheckpointV2FromEvidence {
    $parentSummary = Test-CheckpointEvidenceBundle -ForExtension
    $publicationEvidence = Read-CanonicalJsonEvidence $PublicationEvidencePaths[-1] "PUBLICATION_MERGE_MISMATCH"
    $publication = Assert-ParentPublication $publicationEvidence.value $parentSummary
    if (-not (Test-ByteArrayEqual $publicationEvidence.bytes (Get-CanonicalJsonBytes $publication))) { Stop-Checkpoint "CHECKPOINT_CANONICALIZATION_MISMATCH" }
    $descriptorEvidence = Read-CanonicalJsonEvidence $ChildDescriptorPath "CHILD_DESCRIPTOR_MISMATCH"
    $child = Get-CheckpointChildFromEvidence $descriptorEvidence $ChildPackageResultPath
    Test-ParentPublicationGitBinding $publication $parentSummary ([string]$child.executionRepositorySha)
    if ([int]$child.sequence -ne ([int]$parentSummary.terminalSequence + 1)) { Stop-Checkpoint "CHILD_SEQUENCE_MISMATCH" }
    if ($child.phase -ne @{2="A2";3="A3"}[[int]$child.sequence]) { Stop-Checkpoint "LINEAGE_SEQUENCE_GAP" }
    if ($descriptorEvidence.value.parentAggregateStateDigest -ne $parentSummary.aggregateStateDigest) { Stop-Checkpoint "PARENT_CHECKPOINT_DIGEST_MISMATCH" }
    $parent = [ordered]@{
        schemaVersion=[string]$parentSummary.checkpoint.schemaVersion; aggregateStateDigest=[string]$parentSummary.aggregateStateDigest
        aggregateFileSha256=[string]$parentSummary.checkpointFileSha256; terminalDescriptorSha256=[string]$parentSummary.descriptorSha256
        terminalTaskId=[string]$parentSummary.terminalTaskId; terminalPhase=[string]$parentSummary.terminalPhase; terminalSequence=[int]$parentSummary.terminalSequence
        terminalExecutionRepositorySha=[string]$parentSummary.terminalExecutionRepositorySha; terminalPackageSha256=[string]$parentSummary.packageSha256
        terminalPackageManifestSha256=[string]$parentSummary.packageManifestSha256; terminalPackageResultSha256=[string]$parentSummary.packageResultSha256
    }
    $checkpoint = [ordered]@{ schemaVersion="2.0"; checkpointType="PHASED_CHILD_CHECKPOINT"; checkpointPolicyVersion="vsp-ai02-checkpoint-chain-v2"; parentCheckpoint=$parent; parentPublication=$publication; child=$child }
    $checkpoint.aggregateStateDigest = "sha256:" + (Get-BytesSha256 (Get-CanonicalJsonBytes $checkpoint))
    Write-Utf8CanonicalJson $checkpoint $CheckpointPath
    return Test-CheckpointV2Evidence $CheckpointPath $parentSummary $PublicationEvidencePaths[-1] $ChildDescriptorPath $ChildPackageResultPath
}

function Invoke-SanitizedCheckpointOperation {
    param([Parameter(Mandatory = $true)][scriptblock] $Operation)
    try { return & $Operation }
    catch {
        if ($_.Exception.Message.StartsWith("AI02 artifact chain validation failed: checkpoint validation rejected [", [StringComparison]::Ordinal)) { throw }
        Stop-Checkpoint "INTERNAL_VALIDATION_ERROR"
    }
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
    "ClassifyBindings" {
        $bindings=@(Read-PredecessorBindings)
        $route=Get-PredecessorBindingRoute $bindings
        [pscustomobject]@{status="PASS";route=$route;bindingCount=$bindings.Count;requiresArtifactReadCredential=($route-ne"AUTHORITATIVE_REPOSITORY_MERGE");repositoryWriteCredentialAvailableToDeveloper=$false} | ConvertTo-Json -Depth 5
    }
    "AcquireAndMaterialize" { Invoke-AcquireAndMaterialize | ConvertTo-Json -Depth 20 }
    "ValidatePostClaude" {
        $baseline = Assert-BaselineUnchanged $BaselinePath $WorkspacePath $ExpectedBaselineSha256 $AggregateStatePath $ExpectedAggregateStateSha256
        [pscustomobject]@{ status = "PASS"; aggregateStateDigest = $baseline.aggregateStateDigest; predecessorFilesUnchanged = $true } | ConvertTo-Json
    }
    "PackageChild" { New-ChildPackage $WorkspacePath $BaselinePath $ManifestPath $OutputDirectory | ConvertTo-Json -Depth 10 }
    "NewDescriptor" { New-PredecessorDescriptor | ConvertTo-Json -Depth 10 }
    "ValidateFinalAggregate" { Test-FinalAggregate | ConvertTo-Json -Depth 5 }
    "ValidateLegacyCheckpoint" {
        $summary = Invoke-SanitizedCheckpointOperation { Test-LegacyCheckpointEvidence $CheckpointPath $DescriptorPath $ChildPackageResultPath }
        [pscustomobject]@{ status="LEGACY_V1_IMMUTABLE_CHECKPOINT"; aggregateStateDigest=$summary.aggregateStateDigest; aggregateFileSha256=$summary.checkpointFileSha256; terminalDescriptorSha256=$summary.descriptorSha256; terminalTaskId=$summary.terminalTaskId; terminalSequence=$summary.terminalSequence } | ConvertTo-Json -Depth 5
    }
    "NewCheckpointV2" {
        $summary = Invoke-SanitizedCheckpointOperation { New-CheckpointV2FromEvidence }
        [pscustomobject]@{ status="PASS"; schemaVersion="2.0"; checkpointType="PHASED_CHILD_CHECKPOINT"; aggregateStateDigest=$summary.aggregateStateDigest; aggregateFileSha256=$summary.checkpointFileSha256; terminalTaskId=$summary.terminalTaskId; terminalSequence=$summary.terminalSequence } | ConvertTo-Json -Depth 5
    }
    "ValidateCheckpointV2" {
        $summary = Invoke-SanitizedCheckpointOperation { Test-CheckpointEvidenceBundle }
        if ($summary.checkpoint.schemaVersion -ne "2.0") { Stop-Checkpoint "LINEAGE_DOWNGRADE_REJECTED" }
        [pscustomobject]@{ status="PASS"; schemaVersion="2.0"; aggregateStateDigest=$summary.aggregateStateDigest; aggregateFileSha256=$summary.checkpointFileSha256; terminalTaskId=$summary.terminalTaskId; terminalSequence=$summary.terminalSequence; completeLineageEvidence=$true } | ConvertTo-Json -Depth 5
    }
}
