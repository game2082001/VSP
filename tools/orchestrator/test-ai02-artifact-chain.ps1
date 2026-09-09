$ErrorActionPreference = "Stop"

$tool = Join-Path $PSScriptRoot "ai02-artifact-chain.ps1"
$recoverySha = "6d05b0ff3a995039c3e50ee7712c45c23047d575"
$phaseFiles = @{
    A1 = @(
        "AI/Orchestrator/Templates/artifact-intake-decision.schema.json",
        "AI/Orchestrator/Templates/artifact-intake-decision.template.json",
        "AI/Orchestrator/Templates/artifact-intake-request.schema.json",
        "AI/Orchestrator/Templates/artifact-intake-request.template.json"
    )
    A2 = @("tools/orchestrator/artifact-intake-contract.ps1")
    A3 = @("AI/Orchestrator/ARTIFACT_INTAKE_SCHEMA.md", "tools/orchestrator/test-artifact-intake-contract.ps1")
}
$script:passed = 0
$script:failed = 0

function Write-Json([object]$Value, [string]$Path) {
    $parent = Split-Path -Parent $Path
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Compress -Depth 20), [Text.UTF8Encoding]::new($false))
}

function Get-Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Invoke-Chain([hashtable]$Arguments) {
    try { return (& $tool @Arguments 2>&1 | Out-String) }
    catch { throw "chain command failed`n$($_.Exception.Message)" }
}

function Expect-Pass([string]$Name, [scriptblock]$Action) {
    try { & $Action | Out-Null; $script:passed++; "PASS: $Name" }
    catch { $script:failed++; "FAIL: $Name -- $($_.Exception.Message)" }
}

function Expect-Fail([string]$Name, [scriptblock]$Action) {
    try { & $Action | Out-Null; $script:failed++; "FAIL: $Name -- expected rejection" }
    catch { $script:passed++; "PASS: $Name" }
}

function New-Package {
    param([string]$Root, [string]$Phase, [string]$Parent, [string]$TaskId, [hashtable]$Contents = @{}, [string[]]$ExtraZipPaths = @())
    New-Item -ItemType Directory -Force -Path $Root | Out-Null
    $files = @()
    foreach ($path in $phaseFiles[$Phase]) {
        $defaultContent = if ($Phase -eq "A1") { 'vsp.ai02.artifact-intake-request/1.0 vsp.ai02.artifact-intake-decision/1.0 vsp-ai02-intake-v1 EXACT_BASE_ONLY FIRST_USE NOT_YET_CONSUMED REJECT_REPLAY' } elseif ($Phase -eq "A2") { '26214400 20971520 52428800 200 10485760 262144 240 100 16 1048576 3 vsp-ai02-intake-v1 EXACT_BASE_ONLY FIRST_USE NOT_YET_CONSUMED REJECT_REPLAY' } else { "synthetic-$Phase-$path" }
        $content = if ($Contents.ContainsKey($path)) { [string]$Contents[$path] } else { $defaultContent }
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($content)
        $hash = [Security.Cryptography.SHA256]::HashData($bytes)
        $files += [ordered]@{ path=$path; mode="100644"; size=[int64]$bytes.Length; sha256=(($hash | ForEach-Object { $_.ToString("x2") }) -join "") }
    }
    $zip = Join-Path $Root "publication-package.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $entry = $archive.CreateEntry($file.path)
            $entry.ExternalAttributes = (0x81A4 -shl 16)
            $writer = [IO.StreamWriter]::new($entry.Open(), [Text.UTF8Encoding]::new($false))
            try {
                if ($Contents.ContainsKey($file.path)) { $writer.Write([string]$Contents[$file.path]) }
                elseif ($Phase -eq "A1") { $writer.Write('vsp.ai02.artifact-intake-request/1.0 vsp.ai02.artifact-intake-decision/1.0 vsp-ai02-intake-v1 EXACT_BASE_ONLY FIRST_USE NOT_YET_CONSUMED REJECT_REPLAY') }
                elseif ($Phase -eq "A2") { $writer.Write('26214400 20971520 52428800 200 10485760 262144 240 100 16 1048576 3 vsp-ai02-intake-v1 EXACT_BASE_ONLY FIRST_USE NOT_YET_CONSUMED REJECT_REPLAY') }
                else { $writer.Write("synthetic-$Phase-$($file.path)") }
            } finally { $writer.Dispose() }
        }
        foreach ($path in $ExtraZipPaths) {
            $entry = $archive.CreateEntry($path)
            $entry.ExternalAttributes = (0x81A4 -shl 16)
            $writer = [IO.StreamWriter]::new($entry.Open(), [Text.UTF8Encoding]::new($false)); try { $writer.Write("extra") } finally { $writer.Dispose() }
        }
    } finally { $archive.Dispose() }
    $manifest = [ordered]@{ schemaVersion="1.0"; taskId=$TaskId; classification="CRITICAL"; repository="game2082001/VSP"; approvedBaseSha=$recoverySha; parentAggregateStateDigest=$Parent; files=$files; repositoryWriteCredentialAvailableToDeveloper=$false }
    $manifestPath = Join-Path $Root "publication-package.manifest.json"; Write-Json $manifest $manifestPath
    $result = [ordered]@{ taskId=$TaskId; approvedBaseSha=$recoverySha; parentAggregateStateDigest=$Parent; changedFiles=$files; packageSha256=(Get-Hash $zip); manifestSha256=(Get-Hash $manifestPath); repositoryWriteCredentialAvailableToDeveloper=$false; productOwnerManualTransport=$false }
    Write-Json $result (Join-Path $Root "publication-package.result.json")
    $sequence = @{A1=1;A2=2;A3=3}[$Phase]
    $descriptor = [ordered]@{ schemaVersion="1.0"; childTaskId=$TaskId; phase=$Phase; sequence=$sequence; recoveryRepositorySha=$recoverySha; workflowId="345910582"; runId=[string](9000+$sequence); runAttempt=1; artifactId=[string](8000+$sequence); artifactName="synthetic-$TaskId"; githubArtifactDigest=("sha256:" + ("a" * 64)); packageSha256=$result.packageSha256; manifestSha256=$result.manifestSha256; ownedFiles=$files; validationResult="PASS"; parentAggregateStateDigest=$Parent }
    $descriptorPath = Join-Path $Root "predecessor-descriptor.json"; Write-Json $descriptor $descriptorPath
    [pscustomobject]@{ Root=$Root; DescriptorPath=$descriptorPath; Descriptor=$descriptor }
}

$root = Join-Path ([IO.Path]::GetTempPath()) ("ai02-chain-tests-" + [Guid]::NewGuid().ToString("N"))
try {
    $genesisOneState=Join-Path $root "genesis-one-state.json"; $genesisOneBaseline=Join-Path $root "genesis-one-baseline.json"
    Expect-Pass "New-AggregateState accepts the legitimate empty GENESIS descriptor collection" { Invoke-Chain @{Mode="InitializeWorkspace";RecoveryRepositorySha=$recoverySha;AggregateStatePath=$genesisOneState;BaselinePath=$genesisOneBaseline} }
    $genesisTwoState=Join-Path $root "genesis-two-state.json"; $genesisTwoBaseline=Join-Path $root "genesis-two-baseline.json"
    Expect-Pass "New-GenesisWorkspace completes with zero predecessors" { Invoke-Chain @{Mode="InitializeWorkspace";RecoveryRepositorySha=$recoverySha;AggregateStatePath=$genesisTwoState;BaselinePath=$genesisTwoBaseline} }
    Expect-Pass "GENESIS aggregate and baseline output is deterministic" { if((Get-Hash $genesisOneState)-ne(Get-Hash $genesisTwoState)-or(Get-Hash $genesisOneBaseline)-ne(Get-Hash $genesisTwoBaseline)){throw "GENESIS bytes differ"} }
    Expect-Pass "GENESIS state and baseline dual representation is internally consistent" { $state=Get-Content $genesisOneState -Raw|ConvertFrom-Json; $baseline=Get-Content $genesisOneBaseline -Raw|ConvertFrom-Json; if($state.recoveryRepositorySha-ne$recoverySha-or@($state.predecessors).Count-ne 0-or@($state.fileOwnership).Count-ne 0-or$state.aggregateStateDigest-notmatch'^sha256:[0-9a-f]{64}$'-or$baseline.recoveryRepositorySha-ne$recoverySha-or@($baseline.predecessorFiles).Count-ne 0-or$baseline.aggregateStateDigest-ne'GENESIS'){throw "GENESIS representation mismatch"} }
    $a1ManifestPath=Join-Path $root "a1-manifest.json"; Write-Json ([ordered]@{taskId="VSP-AI02-001TI-A1";repositoryTransport=[ordered]@{approvedFiles=$phaseFiles.A1}}) $a1ManifestPath
    Expect-Pass "A1 child authorization accepts zero predecessors" { Invoke-Chain @{Mode="ValidateChildAuthorization";ChildTaskId="VSP-AI02-001TI-A1";ChildPhase="A1";Sequence=1;RecoveryRepositorySha=$recoverySha;ManifestPath=$a1ManifestPath;BaselinePath=$genesisOneBaseline} }
    $a2GenesisManifestPath=Join-Path $root "a2-genesis-manifest.json"; Write-Json ([ordered]@{taskId="VSP-AI02-001TI-A2";repositoryTransport=[ordered]@{approvedFiles=$phaseFiles.A2}}) $a2GenesisManifestPath
    Expect-Fail "A2 child authorization still rejects zero predecessors" { Invoke-Chain @{Mode="ValidateChildAuthorization";ChildTaskId="VSP-AI02-001TI-A2";ChildPhase="A2";Sequence=2;RecoveryRepositorySha=$recoverySha;ManifestPath=$a2GenesisManifestPath;BaselinePath=$genesisOneBaseline} }

    $a1 = New-Package (Join-Path $root "a1") A1 GENESIS "VSP-AI02-001TI-A1"
    Expect-Pass "valid predecessor descriptor and package" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$a1.DescriptorPath; ArtifactDirectory=$a1.Root; RecoveryRepositorySha=$recoverySha; RunId="9001"; ArtifactId="8001"; GitHubArtifactDigest=("sha256:"+("a"*64)) } }
    Expect-Fail "wrong run identity rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$a1.DescriptorPath; ArtifactDirectory=$a1.Root; RunId="9999" } }
    Expect-Fail "wrong artifact identity rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$a1.DescriptorPath; ArtifactDirectory=$a1.Root; ArtifactId="9999" } }
    Expect-Fail "wrong GitHub artifact digest rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$a1.DescriptorPath; ArtifactDirectory=$a1.Root; GitHubArtifactDigest=("sha256:"+("b"*64)) } }
    Expect-Fail "wrong recovery SHA rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$a1.DescriptorPath; ArtifactDirectory=$a1.Root; RecoveryRepositorySha=("f"*40) } }
    $unknownSchema=Get-Content $a1.DescriptorPath -Raw|ConvertFrom-Json; $unknownSchema.schemaVersion="2.0"; $unknownSchemaPath=Join-Path $root "unknown-schema.json"; Write-Json $unknownSchema $unknownSchemaPath
    Expect-Fail "unknown descriptor schema rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$unknownSchemaPath; ArtifactDirectory=$a1.Root } }
    $thirdAttempt=Get-Content $a1.DescriptorPath -Raw|ConvertFrom-Json; $thirdAttempt.runAttempt=3; $thirdAttemptPath=Join-Path $root "third-attempt.json"; Write-Json $thirdAttempt $thirdAttemptPath
    Expect-Fail "third child attempt rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$thirdAttemptPath; ArtifactDirectory=$a1.Root } }

    $tampered = New-Package (Join-Path $root "tampered") A1 GENESIS "VSP-AI02-001TI-A1"
    Add-Content -LiteralPath (Join-Path $tampered.Root "publication-package.manifest.json") -Value " "
    Expect-Fail "metadata digest tampering rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$tampered.DescriptorPath; ArtifactDirectory=$tampered.Root } }
    $unsafe = New-Package (Join-Path $root "unsafe") A1 GENESIS "VSP-AI02-001TI-A1" @{} @("../escape.txt")
    Expect-Fail "unsafe ZIP path rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$unsafe.DescriptorPath; ArtifactDirectory=$unsafe.Root } }
    $collision = New-Package (Join-Path $root "collision") A1 GENESIS "VSP-AI02-001TI-A1" @{} @("ai/orchestrator/templates/artifact-intake-decision.schema.json")
    Expect-Fail "case-colliding ZIP path rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$collision.DescriptorPath; ArtifactDirectory=$collision.Root } }
    foreach ($unsafePath in @("C:/escape.txt", "//server/share.txt", "folder\escape.txt", "folder:name.txt")) {
        $unsafeForm = New-Package (Join-Path $root ("unsafe-" + [Guid]::NewGuid().ToString("N"))) A1 GENESIS "VSP-AI02-001TI-A1" @{} @($unsafePath)
        Expect-Fail "unsafe drive/UNC/backslash/colon ZIP path rejected: $unsafePath" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$unsafeForm.DescriptorPath; ArtifactDirectory=$unsafeForm.Root } }
    }
    $oversizedContent = "x" * 10485761
    $oversized = New-Package (Join-Path $root "oversized") A2 GENESIS "VSP-AI02-001TI-A2" @{ "tools/orchestrator/artifact-intake-contract.ps1"=$oversizedContent }
    Expect-Fail "oversized predecessor file rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$oversized.DescriptorPath; ArtifactDirectory=$oversized.Root } }
    $invalidMode = New-Package (Join-Path $root "invalid-mode") A1 GENESIS "VSP-AI02-001TI-A1"
    $invalidModeObject = Get-Content $invalidMode.DescriptorPath -Raw | ConvertFrom-Json; $invalidModeObject.ownedFiles[0].mode="100755"; Write-Json $invalidModeObject $invalidMode.DescriptorPath
    Expect-Fail "invalid predecessor mode rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$invalidMode.DescriptorPath; ArtifactDirectory=$invalidMode.Root } }
    $missing = New-Package (Join-Path $root "missing") A1 GENESIS "VSP-AI02-001TI-A1"
    $missingZip = Join-Path $missing.Root "publication-package.zip"
    $missingArchive=[IO.Compression.ZipFile]::Open($missingZip,[IO.Compression.ZipArchiveMode]::Update); try{$missingArchive.Entries[0].Delete()}finally{$missingArchive.Dispose()}
    $missingDescriptor=Get-Content $missing.DescriptorPath -Raw|ConvertFrom-Json; $missingDescriptor.packageSha256=Get-Hash $missingZip
    $missingResultPath=Join-Path $missing.Root "publication-package.result.json"; $missingResult=Get-Content $missingResultPath -Raw|ConvertFrom-Json; $missingResult.packageSha256=$missingDescriptor.packageSha256; Write-Json $missingResult $missingResultPath; Write-Json $missingDescriptor $missing.DescriptorPath
    Expect-Fail "missing predecessor file rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$missing.DescriptorPath; ArtifactDirectory=$missing.Root } }
    $unauthorized = New-Package (Join-Path $root "unauthorized") A1 GENESIS "VSP-AI02-001TI-A1" @{} @("unauthorized.txt")
    Expect-Fail "unauthorized predecessor file rejected" { Invoke-Chain @{ Mode="ValidateDescriptor"; DescriptorPath=$unauthorized.DescriptorPath; ArtifactDirectory=$unauthorized.Root } }

    $state1 = Join-Path $root "state1.json"
    Invoke-Chain @{ Mode="BuildAggregateState"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath); AggregateStatePath=$state1 } | Out-Null
    $digest1 = (Get-Content $state1 -Raw | ConvertFrom-Json).aggregateStateDigest
    $a2 = New-Package (Join-Path $root "a2") A2 $digest1 "VSP-AI02-001TI-A2"
    $state2a = Join-Path $root "state2a.json"; $state2b = Join-Path $root "state2b.json"
    Invoke-Chain @{ Mode="BuildAggregateState"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath,$a2.DescriptorPath); AggregateStatePath=$state2a } | Out-Null
    Invoke-Chain @{ Mode="BuildAggregateState"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a2.DescriptorPath,$a1.DescriptorPath); AggregateStatePath=$state2b } | Out-Null
    Expect-Pass "aggregate lineage is deterministic" { if ((Get-Hash $state2a) -ne (Get-Hash $state2b)) { throw "aggregate bytes differ" } }
    $stale = Get-Content $a2.DescriptorPath -Raw | ConvertFrom-Json; $stale.parentAggregateStateDigest = "GENESIS"; $stalePath=Join-Path $root "stale.json"; Write-Json $stale $stalePath
    Expect-Fail "stale lineage rejected" { Invoke-Chain @{ Mode="BuildAggregateState"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath,$stalePath); AggregateStatePath=(Join-Path $root "stale-state.json") } }
    $gapA3=Get-Content $a2.DescriptorPath -Raw|ConvertFrom-Json; $gapA3.phase="A3"; $gapA3.sequence=3; $gapA3.ownedFiles=@((New-Package (Join-Path $root "gap-a3-package") A3 $digest1 "VSP-AI02-001TI-A3").Descriptor.ownedFiles); $gapA3Path=Join-Path $root "gap-a3.json"; Write-Json $gapA3 $gapA3Path
    Expect-Fail "descriptor sequence gap still rejected" { Invoke-Chain @{Mode="BuildAggregateState";RecoveryRepositorySha=$recoverySha;DescriptorPaths=@($a1.DescriptorPath,$gapA3Path);AggregateStatePath=(Join-Path $root "gap-state.json")} }
    $duplicateA1=Get-Content $a1.DescriptorPath -Raw|ConvertFrom-Json; $duplicateA1.childTaskId="VSP-AI02-001TI-A1-DUPLICATE"; $duplicateA1Path=Join-Path $root "duplicate-a1.json"; Write-Json $duplicateA1 $duplicateA1Path
    Expect-Fail "duplicate descriptor sequence still rejected" { Invoke-Chain @{Mode="BuildAggregateState";RecoveryRepositorySha=$recoverySha;DescriptorPaths=@($a1.DescriptorPath,$duplicateA1Path);AggregateStatePath=(Join-Path $root "duplicate-state.json")} }
    $changedA1=Get-Content $a1.DescriptorPath -Raw|ConvertFrom-Json; $changedA1.ownedFiles[0].sha256=("c"*64); $changedA1Path=Join-Path $root "changed-a1.json"; Write-Json $changedA1 $changedA1Path
    Expect-Fail "changed A1 digest invalidates prior A2 lineage" { Invoke-Chain @{ Mode="BuildAggregateState"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($changedA1Path,$a2.DescriptorPath); AggregateStatePath=(Join-Path $root "invalid-a2-state.json") } }

    $workspace1=Join-Path $root "workspace1"; $workspace2=Join-Path $root "workspace2"; New-Item -ItemType Directory $workspace1,$workspace2 | Out-Null
    Invoke-Chain @{ Mode="MaterializeLocal"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath,$a2.DescriptorPath); ArtifactDirectories=@($a1.Root,$a2.Root); WorkspacePath=$workspace1; AggregateStatePath=(Join-Path $root "m1state.json"); BaselinePath=(Join-Path $root "m1base.json") } | Out-Null
    Invoke-Chain @{ Mode="MaterializeLocal"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath,$a2.DescriptorPath); ArtifactDirectories=@($a1.Root,$a2.Root); WorkspacePath=$workspace2; AggregateStatePath=(Join-Path $root "m2state.json"); BaselinePath=(Join-Path $root "m2base.json") } | Out-Null
    Expect-Pass "materialization is deterministic" { foreach($file in @($phaseFiles.A1+$phaseFiles.A2)){ if((Get-Hash (Join-Path $workspace1 $file)) -ne (Get-Hash (Join-Path $workspace2 $file))){throw "materialized hash differs"} } }
    Add-Content -LiteralPath (Join-Path $workspace1 $phaseFiles.A1[0]) -Value "mutation"
    Expect-Fail "predecessor mutation rejected" { Invoke-Chain @{ Mode="ValidatePostClaude"; WorkspacePath=$workspace1; BaselinePath=(Join-Path $root "m1base.json") } }
    $m2BaselinePath=Join-Path $root "m2base.json"; $m2StatePath=Join-Path $root "m2state.json"; $boundBaselineHash=Get-Hash $m2BaselinePath; $boundStateHash=Get-Hash $m2StatePath
    $m2ChangedPath=Join-Path $workspace2 $phaseFiles.A1[0]; Add-Content -LiteralPath $m2ChangedPath -Value "coordinated mutation"
    $forgedBaseline=Get-Content $m2BaselinePath -Raw|ConvertFrom-Json; $forgedFile=$forgedBaseline.predecessorFiles|Where-Object path -eq $phaseFiles.A1[0]; $forgedFile.size=(Get-Item $m2ChangedPath).Length; $forgedFile.sha256=Get-Hash $m2ChangedPath; Write-Json $forgedBaseline $m2BaselinePath
    Expect-Fail "coordinated predecessor and baseline tampering rejected by immutable binding" { Invoke-Chain @{Mode="ValidatePostClaude";WorkspacePath=$workspace2;BaselinePath=$m2BaselinePath;AggregateStatePath=$m2StatePath;ExpectedBaselineSha256=$boundBaselineHash;ExpectedAggregateStateSha256=$boundStateHash} }

    $childWorkspace=Join-Path $root "child-workspace"; New-Item -ItemType Directory $childWorkspace | Out-Null
    & git -C $childWorkspace init --quiet; & git -C $childWorkspace config user.name "Synthetic Test"; & git -C $childWorkspace config user.email "synthetic@example.invalid"; & git -C $childWorkspace commit --allow-empty -m base --quiet
    $childState=Join-Path $root "child-state.json"; $childBaseline=Join-Path $root "child-base.json"
    Invoke-Chain @{ Mode="MaterializeLocal"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath); ArtifactDirectories=@($a1.Root); WorkspacePath=$childWorkspace; AggregateStatePath=$childState; BaselinePath=$childBaseline } | Out-Null
    $childOutput=Join-Path $childWorkspace $phaseFiles.A2[0]; New-Item -ItemType Directory -Force (Split-Path -Parent $childOutput)|Out-Null; [IO.File]::WriteAllText($childOutput,"child-owned",[Text.UTF8Encoding]::new($false))
    $childManifestPath=Join-Path $root "child-manifest.json"; Write-Json ([ordered]@{taskId="VSP-AI02-001TI-A2";classification="CRITICAL";repository="game2082001/VSP";repositoryTransport=[ordered]@{approvedFiles=$phaseFiles.A2}}) $childManifestPath
    Expect-Pass "child changes exclude materialized predecessor files" { Invoke-Chain @{ Mode="PackageChild"; WorkspacePath=$childWorkspace; BaselinePath=$childBaseline; ManifestPath=$childManifestPath; OutputDirectory=(Join-Path $root "child-output") } }
    & git -C $childWorkspace add -- $phaseFiles.A2[0]; & git -C $childWorkspace update-index --chmod=+x -- $phaseFiles.A2[0]
    Expect-Fail "executable Git mode child output rejected" { Invoke-Chain @{ Mode="PackageChild"; WorkspacePath=$childWorkspace; BaselinePath=$childBaseline; ManifestPath=$childManifestPath; OutputDirectory=(Join-Path $root "executable-output") } }
    Expect-Pass "A1 lineage authorizes A2 child before Claude" { Invoke-Chain @{Mode="ValidateChildAuthorization";ChildTaskId="VSP-AI02-001TI-A2";ChildPhase="A2";Sequence=2;RecoveryRepositorySha=$recoverySha;ManifestPath=$childManifestPath;BaselinePath=$childBaseline} }
    Expect-Fail "missing A2 lineage cannot authorize A3 child" { Invoke-Chain @{Mode="ValidateChildAuthorization";ChildTaskId="VSP-AI02-001TI-A2";ChildPhase="A3";Sequence=3;RecoveryRepositorySha=$recoverySha;ManifestPath=$childManifestPath;BaselinePath=$childBaseline} }

    $digest2 = (Get-Content $state2a -Raw | ConvertFrom-Json).aggregateStateDigest
    $focusedEvidence = [ordered]@{
        schemaVersion="vsp.ai02.artifact-intake-focused-suite/1.0";suite="VSP-AI02-001TI-A";status="PASS";testCount=17;failedCount=0
        requiredChecks=@("schema-validation","template-schema-conformance","ascii-path-rejection","traversal-rejection","absolute-path-rejection","backslash-rejection","drive-unc-colon-rejection","duplicate-case-collision-rejection","mode-restriction","size-count-ceilings","hash-verification","malformed-unknown-rejection","exact-base-rejection","replay-first-use","replay-rejection","credential-invariants","deterministic-output")
        policy=[ordered]@{schemaVersion="1.0";policyVersion="vsp-ai02-intake-v1";requestSchemaIdentifier="vsp.ai02.artifact-intake-request/1.0";decisionSchemaIdentifier="vsp.ai02.artifact-intake-decision/1.0";maxOuterArtifactCompressedBytes=26214400;maxInnerPublicationZipCompressedBytes=20971520;maxTotalInnerUncompressedBytes=52428800;maxChangedFiles=200;maxPerFileUncompressedBytes=10485760;maxManifestJsonBytes=262144;maxResultJsonBytes=262144;maxRepositoryRelativePathCharacters=240;maxPathSegmentCharacters=100;maxJsonNestingDepth=16;maxSanitizedEvidenceArtifactBytes=1048576;maxCompressionRatio=100;requiredOuterPublicationFileCount=3;staleBasePolicy="EXACT_BASE_ONLY";emptyConsumedIdentitiesMeaning="FIRST_USE_NOT_YET_CONSUMED";matchingConsumedIdentityDisposition="REJECT_REPLAY";repositoryWriteCredentialAvailableToDeveloper=$false}
    } | ConvertTo-Json -Compress -Depth 10
    $a3Contents = @{ "tools/orchestrator/test-artifact-intake-contract.ps1" = "Write-Output '$focusedEvidence'; exit 0"; "AI/Orchestrator/ARTIFACT_INTAKE_SCHEMA.md" = "# Synthetic contract`nvsp-ai02-intake-v1 EXACT_BASE_ONLY FIRST_USE NOT_YET_CONSUMED REJECT_REPLAY" }
    $a3 = New-Package (Join-Path $root "a3") A3 $digest2 "VSP-AI02-001TI-A3" $a3Contents
    $changedA2=Get-Content $a2.DescriptorPath -Raw|ConvertFrom-Json; $changedA2.ownedFiles[0].sha256=("d"*64); $changedA2Path=Join-Path $root "changed-a2.json"; Write-Json $changedA2 $changedA2Path
    Expect-Fail "changed A2 invalidates prior A3 lineage" { Invoke-Chain @{ Mode="BuildAggregateState"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath,$changedA2Path,$a3.DescriptorPath); AggregateStatePath=(Join-Path $root "invalid-a3-state.json") } }
    $finalWorkspace=Join-Path $root "final"; New-Item -ItemType Directory $finalWorkspace | Out-Null
    $finalState=Join-Path $root "final-state.json"
    Invoke-Chain @{ Mode="MaterializeLocal"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath,$a2.DescriptorPath,$a3.DescriptorPath); ArtifactDirectories=@($a1.Root,$a2.Root,$a3.Root); WorkspacePath=$finalWorkspace; AggregateStatePath=$finalState; BaselinePath=(Join-Path $root "final-base.json") } | Out-Null
    Expect-Pass "exact seven-file final aggregate passes focused validation" { Invoke-Chain @{ Mode="ValidateFinalAggregate"; WorkspacePath=$finalWorkspace; AggregateStatePath=$finalState } }
    Remove-Item -LiteralPath (Join-Path $finalWorkspace $phaseFiles.A3[0])
    Expect-Fail "missing final aggregate file rejected" { Invoke-Chain @{ Mode="ValidateFinalAggregate"; WorkspacePath=$finalWorkspace; AggregateStatePath=$finalState } }
    [IO.File]::WriteAllText((Join-Path $finalWorkspace $phaseFiles.A3[0]),$a3Contents[$phaseFiles.A3[0]],[Text.UTF8Encoding]::new($false))
    $extraState=Get-Content $finalState -Raw|ConvertFrom-Json; $extraState.fileOwnership += [pscustomobject]@{path="unauthorized.txt";ownerTaskId="VSP-AI02-001TI-A3";phase="A3";mode="100644";size=1;sha256=("e"*64)}; $extraStatePath=Join-Path $root "extra-state.json"; Write-Json $extraState $extraStatePath
    Expect-Fail "extra final aggregate file rejected" { Invoke-Chain @{ Mode="ValidateFinalAggregate"; WorkspacePath=$finalWorkspace; AggregateStatePath=$extraStatePath } }

    $badContents = @{ "tools/orchestrator/test-artifact-intake-contract.ps1" = 'exit 1'; "AI/Orchestrator/ARTIFACT_INTAKE_SCHEMA.md" = "# Synthetic contract" }
    $badA3 = New-Package (Join-Path $root "bad-a3") A3 $digest2 "VSP-AI02-001TI-A3" $badContents
    $badWorkspace=Join-Path $root "bad-final"; New-Item -ItemType Directory $badWorkspace | Out-Null
    $badState=Join-Path $root "bad-state.json"
    Invoke-Chain @{ Mode="MaterializeLocal"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath,$a2.DescriptorPath,$badA3.DescriptorPath); ArtifactDirectories=@($a1.Root,$a2.Root,$badA3.Root); WorkspacePath=$badWorkspace; AggregateStatePath=$badState; BaselinePath=(Join-Path $root "bad-base.json") } | Out-Null
    Expect-Fail "focused aggregate validation failure is fail-closed" { Invoke-Chain @{ Mode="ValidateFinalAggregate"; WorkspacePath=$badWorkspace; AggregateStatePath=$badState } }
    $noopContents = @{ "tools/orchestrator/test-artifact-intake-contract.ps1" = 'exit 0'; "AI/Orchestrator/ARTIFACT_INTAKE_SCHEMA.md" = "vsp-ai02-intake-v1 EXACT_BASE_ONLY FIRST_USE NOT_YET_CONSUMED REJECT_REPLAY" }
    $noopA3 = New-Package (Join-Path $root "noop-a3") A3 $digest2 "VSP-AI02-001TI-A3" $noopContents
    $noopWorkspace=Join-Path $root "noop-final"; New-Item -ItemType Directory $noopWorkspace | Out-Null; $noopState=Join-Path $root "noop-state.json"
    Invoke-Chain @{ Mode="MaterializeLocal"; RecoveryRepositorySha=$recoverySha; DescriptorPaths=@($a1.DescriptorPath,$a2.DescriptorPath,$noopA3.DescriptorPath); ArtifactDirectories=@($a1.Root,$a2.Root,$noopA3.Root); WorkspacePath=$noopWorkspace; AggregateStatePath=$noopState; BaselinePath=(Join-Path $root "noop-base.json") } | Out-Null
    Expect-Fail "no-op focused script cannot certify final aggregate" { Invoke-Chain @{ Mode="ValidateFinalAggregate"; WorkspacePath=$noopWorkspace; AggregateStatePath=$noopState } }
    $alteredA2 = New-Package (Join-Path $root "altered-a2") A2 $digest1 "VSP-AI02-001TI-A2" @{"tools/orchestrator/artifact-intake-contract.ps1"='26214400 20971520 52428800 200 10485760 262144 240 100 16 3 vsp-ai02-intake-v1 EXACT_BASE_ONLY FIRST_USE NOT_YET_CONSUMED REJECT_REPLAY'}
    $alteredState2=Join-Path $root "altered-state2.json"; Invoke-Chain @{Mode="BuildAggregateState";RecoveryRepositorySha=$recoverySha;DescriptorPaths=@($a1.DescriptorPath,$alteredA2.DescriptorPath);AggregateStatePath=$alteredState2}|Out-Null; $alteredDigest2=(Get-Content $alteredState2 -Raw|ConvertFrom-Json).aggregateStateDigest
    $alteredA3=New-Package (Join-Path $root "altered-a3") A3 $alteredDigest2 "VSP-AI02-001TI-A3" $a3Contents
    $alteredWorkspace=Join-Path $root "altered-final";New-Item -ItemType Directory $alteredWorkspace|Out-Null;$alteredFinalState=Join-Path $root "altered-final-state.json"
    Invoke-Chain @{Mode="MaterializeLocal";RecoveryRepositorySha=$recoverySha;DescriptorPaths=@($a1.DescriptorPath,$alteredA2.DescriptorPath,$alteredA3.DescriptorPath);ArtifactDirectories=@($a1.Root,$alteredA2.Root,$alteredA3.Root);WorkspacePath=$alteredWorkspace;AggregateStatePath=$alteredFinalState;BaselinePath=(Join-Path $root "altered-base.json")}|Out-Null
    Expect-Fail "altered P0 numeric ceiling rejects final aggregate" { Invoke-Chain @{Mode="ValidateFinalAggregate";WorkspacePath=$alteredWorkspace;AggregateStatePath=$alteredFinalState} }

    foreach ($schemaPath in @("AI/Orchestrator/Templates/ai02-predecessor-descriptor.schema.json", "AI/Orchestrator/Templates/ai02-aggregate-state.schema.json")) {
        Expect-Pass "schema parses and is strict: $schemaPath" { $schema=Get-Content $schemaPath -Raw | ConvertFrom-Json; if($schema.additionalProperties -ne $false){throw "schema is not strict"} }
    }
    $workflow = Get-Content ".github/workflows/ai02-claude-artifact-developer.yml" -Raw
    Expect-Pass "artifact credential is scoped outside Claude and explicitly checked absent" { if($workflow -notmatch 'AI02_ARTIFACT_READ_TOKEN' -or $workflow -notmatch 'Verify Claude receives no artifact or repository credential'){throw "credential lifecycle guard missing"} }
    Expect-Pass "materializer and Claude retain no repository-write authority" { if($workflow -notmatch 'contents: read' -or $workflow -match 'contents: write'){throw "repository credential boundary changed"} }
    Expect-Pass "P2 does not invoke or alter Repository Transport" { if((Get-Content $tool -Raw) -match 'repository-transport' -or $workflow -match 'repository-transport'){throw "Repository Transport referenced"} }
    Expect-Pass "P2 performs no publication side effect" { if((Get-Content $tool -Raw) -match 'git push|gh pr|Repository Transport'){throw "publication side effect found"} }
}
finally { if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force } }

"AI02 artifact-chain focused tests: $script:passed passed, $script:failed failed"
if ($script:failed -ne 0) { exit 1 }
