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
    param([string]$Root, [string]$Phase, [string]$Parent, [string]$TaskId, [hashtable]$Contents = @{}, [string[]]$ExtraZipPaths = @(), [string]$ExecutionSha = $recoverySha)
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
    $manifest = [ordered]@{ schemaVersion="1.0"; taskId=$TaskId; classification="CRITICAL"; repository="game2082001/VSP"; approvedBaseSha=$ExecutionSha; parentAggregateStateDigest=$Parent; files=$files; repositoryWriteCredentialAvailableToDeveloper=$false }
    $manifestPath = Join-Path $Root "publication-package.manifest.json"; Write-Json $manifest $manifestPath
    $result = [ordered]@{ taskId=$TaskId; approvedBaseSha=$ExecutionSha; parentAggregateStateDigest=$Parent; changedFiles=$files; packageSha256=(Get-Hash $zip); manifestSha256=(Get-Hash $manifestPath); repositoryWriteCredentialAvailableToDeveloper=$false; productOwnerManualTransport=$false }
    Write-Json $result (Join-Path $Root "publication-package.result.json")
    $sequence = @{A1=1;A2=2;A3=3}[$Phase]
    $descriptor = [ordered]@{ schemaVersion="1.0"; childTaskId=$TaskId; phase=$Phase; sequence=$sequence; recoveryRepositorySha=$ExecutionSha; workflowId="345910582"; runId=[string](9000+$sequence); runAttempt=1; artifactId=[string](8000+$sequence); artifactName="synthetic-$TaskId"; githubArtifactDigest=("sha256:" + ("a" * 64)); packageSha256=$result.packageSha256; manifestSha256=$result.manifestSha256; ownedFiles=$files; validationResult="PASS"; parentAggregateStateDigest=$Parent }
    $descriptorPath = Join-Path $Root "predecessor-descriptor.json"; Write-Json $descriptor $descriptorPath
    [pscustomobject]@{ Root=$Root; DescriptorPath=$descriptorPath; Descriptor=$descriptor }
}

function Merge-Arguments([hashtable]$Base, [hashtable]$Overrides) {
    $merged = $Base.Clone()
    foreach ($key in $Overrides.Keys) { $merged[$key] = $Overrides[$key] }
    return $merged
}

function New-LegacyA1Evidence {
    param([string]$Root)
    New-Item -ItemType Directory -Force -Path $Root | Out-Null
    $files = @(
        [ordered]@{path="AI/Orchestrator/Templates/artifact-intake-decision.schema.json";mode="100644";size=8618;sha256="c21dd6856a20ca39df707ce28181e09056e272ed603962562c5a24dd554aa01b"},
        [ordered]@{path="AI/Orchestrator/Templates/artifact-intake-decision.template.json";mode="100644";size=3450;sha256="fc04cabe343326c87b68ed121165687f0eb528a51ab380db056ac72bebdc3558"},
        [ordered]@{path="AI/Orchestrator/Templates/artifact-intake-request.schema.json";mode="100644";size=7237;sha256="f1b4b8d8f6cc434a2d1900161ee191fea61be77ab60a85fa1cb83cbb267f4e95"},
        [ordered]@{path="AI/Orchestrator/Templates/artifact-intake-request.template.json";mode="100644";size=3733;sha256="48b167ecece4381a87fcc010c615581b06188f57c137843232068b3f3ff87db0"}
    )
    $descriptor = [ordered]@{
        schemaVersion="1.0";childTaskId="VSP-AI02-001TI-A1D-VALIDATE";phase="A1";sequence=1;recoveryRepositorySha="8a0a441c532295405b8133d96142844026ee4a93"
        workflowId="1";runId="1";runAttempt=1;artifactId="1";artifactName="a1d-validate-local-child-package"
        githubArtifactDigest="sha256:64fa19736ce6a2d15211fb8646ae1bafabda8918a7510b13dece18b414736fef"
        packageSha256="64fa19736ce6a2d15211fb8646ae1bafabda8918a7510b13dece18b414736fef"
        manifestSha256="4b2e05f25c45461a9cc754a7fc2bc0480da347379f5bd6070b51d387af94fd85"
        ownedFiles=$files;validationResult="PASS";parentAggregateStateDigest="GENESIS"
    }
    $descriptorPath=Join-Path $Root "a1-descriptor.json"; Write-Json $descriptor $descriptorPath
    $ownership=@($files|ForEach-Object{[ordered]@{path=$_.path;ownerTaskId="VSP-AI02-001TI-A1D-VALIDATE";phase="A1";mode=$_.mode;size=$_.size;sha256=$_.sha256}})
    $aggregate=[ordered]@{schemaVersion="1.0";recoveryRepositorySha="8a0a441c532295405b8133d96142844026ee4a93";predecessors=@($descriptor);fileOwnership=$ownership;aggregateStateDigest="sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1"}
    $aggregatePath=Join-Path $Root "a1-aggregate.json"; Write-Json $aggregate $aggregatePath
    $result=[ordered]@{taskId=$descriptor.childTaskId;approvedBaseSha=$descriptor.recoveryRepositorySha;parentAggregateStateDigest="GENESIS";changedFiles=$files;packageSha256=$descriptor.packageSha256;manifestSha256=$descriptor.manifestSha256;repositoryWriteCredentialAvailableToDeveloper=$false;productOwnerManualTransport=$false}
    $resultPath=Join-Path $Root "a1-result.json"; $resultBytes=[Text.UTF8Encoding]::new($false).GetBytes(($result|ConvertTo-Json -Compress -Depth 20)+"`n");[IO.File]::WriteAllBytes($resultPath,$resultBytes)
    $publishedFiles=@(
        [ordered]@{path=$files[0].path;mode="100644";gitBlobId="407a195e093f7a8a6136983fb2bf946d7eac64ef";size=$files[0].size;sha256=$files[0].sha256},
        [ordered]@{path=$files[1].path;mode="100644";gitBlobId="cc9ac0c9e490a75a7643ea7394a09fe338084f35";size=$files[1].size;sha256=$files[1].sha256},
        [ordered]@{path=$files[2].path;mode="100644";gitBlobId="7378d130eda351b85c17ff3564de1fa45e701af1";size=$files[2].size;sha256=$files[2].sha256},
        [ordered]@{path=$files[3].path;mode="100644";gitBlobId="ef68b6df094c7f117a33658a031cc93bc747aaac";size=$files[3].size;sha256=$files[3].sha256}
    )
    $publication=[ordered]@{sourceType="AUTHORITATIVE_REPOSITORY_MERGE";repository="game2082001/VSP";mergeCommit="aa53c00d5e4125a53f8f835220bf6d6b2e911b14";orderedMergeParents=@("8a0a441c532295405b8133d96142844026ee4a93","cb7dbf0cfbcb0f715001588cdd6da97264e19d06");publishedProductionHead="cb7dbf0cfbcb0f715001588cdd6da97264e19d06";checkpointAggregateStateDigest=$aggregate.aggregateStateDigest;checkpointAggregateFileSha256="750334cd06ecf529303b14ba0431f5ca492c714e606baca6767aef5961106ddc";productionFiles=$publishedFiles}
    $publicationPath=Join-Path $Root "a1-publication.json";Write-Json $publication $publicationPath
    [pscustomobject]@{AggregatePath=$aggregatePath;DescriptorPath=$descriptorPath;ResultPath=$resultPath;PublicationPath=$publicationPath;Descriptor=$descriptor;Publication=$publication}
}

function New-RepositoryMergeBinding {
    param([Parameter(Mandatory = $true)]$Legacy)
    return [ordered]@{
        sourceType="AUTHORITATIVE_REPOSITORY_MERGE"
        repository="game2082001/VSP"
        mergeCommit=[string]$Legacy.Publication.mergeCommit
        orderedMergeParents=@($Legacy.Publication.orderedMergeParents)
        publishedProductionHead=[string]$Legacy.Publication.publishedProductionHead
        predecessorTaskId=[string]$Legacy.Descriptor.childTaskId
        phase=[string]$Legacy.Descriptor.phase
        sequence=[int]$Legacy.Descriptor.sequence
        executionRepositorySha=[string]$Legacy.Descriptor.recoveryRepositorySha
        checkpointSchemaVersion="1.0"
        descriptorSha256=(Get-Hash $Legacy.DescriptorPath)
        aggregateStateDigest="sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1"
        aggregateFileSha256=(Get-Hash $Legacy.AggregatePath)
        packageSha256=[string]$Legacy.Descriptor.packageSha256
        manifestSha256=[string]$Legacy.Descriptor.manifestSha256
        resultSha256=(Get-Hash $Legacy.ResultPath)
        checkpointEvidenceBase64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($Legacy.AggregatePath))
        descriptorEvidenceBase64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($Legacy.DescriptorPath))
        packageResultEvidenceBase64=[Convert]::ToBase64String([IO.File]::ReadAllBytes($Legacy.ResultPath))
        productionFiles=@($Legacy.Publication.productionFiles)
    }
}

function New-PublicationEvidence {
    param([string]$Path,[string]$CheckpointPath,[string]$DescriptorPath,[string]$MergeCommit,[string[]]$Parents,[string]$PublishedHead,[string]$BlobSeed="b")
    $checkpoint=Get-Content $CheckpointPath -Raw|ConvertFrom-Json
    $descriptor=Get-Content $DescriptorPath -Raw|ConvertFrom-Json
    $files=@($descriptor.ownedFiles|Sort-Object path|ForEach-Object{[ordered]@{path=$_.path;mode=$_.mode;gitBlobId=($BlobSeed*40);size=$_.size;sha256=$_.sha256}})
    $publication=[ordered]@{sourceType="AUTHORITATIVE_REPOSITORY_MERGE";repository="game2082001/VSP";mergeCommit=$MergeCommit;orderedMergeParents=$Parents;publishedProductionHead=$PublishedHead;checkpointAggregateStateDigest=$checkpoint.aggregateStateDigest;checkpointAggregateFileSha256=(Get-Hash $CheckpointPath);productionFiles=$files}
    Write-Json $publication $Path
    return $publication
}

function Copy-CanonicalJsonWithMutation {
    param([string]$Source, [string]$Destination, [scriptblock]$Mutation)
    $value = Get-Content $Source -Raw | ConvertFrom-Json -Depth 30
    & $Mutation $value
    Write-Json $value $Destination
    return $Destination
}

function New-SyntheticA2PublicationRepository {
    param([string]$Path, [string]$BaseSha, [string]$A2Content)
    & git clone --quiet --shared --no-checkout (Get-Location).Path $Path
    if ($LASTEXITCODE -ne 0) { throw "synthetic repository clone failed" }
    & git -C $Path config user.name "Synthetic AGV2 Test"
    & git -C $Path config user.email "agv2@example.invalid"
    & git -C $Path checkout --quiet --detach $BaseSha
    & git -C $Path switch --quiet -c synthetic-a2-publication
    $outputPath = Join-Path $Path $phaseFiles.A2[0]
    New-Item -ItemType Directory -Force (Split-Path -Parent $outputPath) | Out-Null
    [IO.File]::WriteAllText($outputPath, $A2Content, [Text.UTF8Encoding]::new($false))
    & git -C $Path add -- $phaseFiles.A2[0]
    & git -C $Path commit --quiet -m "synthetic A2 production head"
    $productionHead = (& git -C $Path rev-parse HEAD).Trim()
    & git -C $Path checkout --quiet --detach $BaseSha
    & git -C $Path merge --quiet --no-ff synthetic-a2-publication -m "synthetic A2 publication merge"
    $mergeCommit = (& git -C $Path rev-parse HEAD).Trim()
    $parents = @((& git -C $Path show -s --format=%P HEAD).Trim().Split(' ', [StringSplitOptions]::RemoveEmptyEntries))
    & git -C $Path commit --quiet --allow-empty -m "synthetic A3 execution base"
    $childExecution = (& git -C $Path rev-parse HEAD).Trim()
    $treeLine = (& git -C $Path ls-tree $mergeCommit -- $phaseFiles.A2[0]).Trim()
    if ($treeLine -cnotmatch '^([0-9]{6}) blob ([0-9a-f]{40})\t(.+)$') { throw "synthetic publication tree mismatch" }
    $file = [ordered]@{
        path=$Matches[3]; mode=$Matches[1]; gitBlobId=$Matches[2]
        size=[int64](& git -C $Path cat-file -s $Matches[2]).Trim()
        sha256=(Get-Hash $outputPath)
    }
    return [pscustomobject]@{ Path=$Path; ProductionHead=$productionHead; MergeCommit=$mergeCommit; Parents=$parents; ChildExecution=$childExecution; File=$file }
}

function New-MaliciousPublishedHeadScenario {
    param([string]$RepositoryPath, [string]$BaseSha, [string]$ExpectedMerge, [string]$Kind)
    & git -C $RepositoryPath checkout --quiet --detach $BaseSha
    & git -C $RepositoryPath switch --quiet -c ("malicious-"+$Kind)
    if ($Kind -eq "changed") {
        $path=Join-Path $RepositoryPath $phaseFiles.A2[0]
        New-Item -ItemType Directory -Force (Split-Path -Parent $path)|Out-Null
        [IO.File]::WriteAllText($path,"different predecessor bytes",[Text.UTF8Encoding]::new($false))
        & git -C $RepositoryPath add -- $phaseFiles.A2[0]
        & git -C $RepositoryPath commit --quiet -m "changed published head"
    } else {
        & git -C $RepositoryPath commit --quiet --allow-empty -m "published head missing predecessor"
    }
    $publishedHead=(& git -C $RepositoryPath rev-parse HEAD).Trim()
    $expectedTree=(& git -C $RepositoryPath rev-parse ($ExpectedMerge+"^{tree}")).Trim()
    $merge=(& git -C $RepositoryPath commit-tree $expectedTree -p $BaseSha -p $publishedHead -m ("malicious "+$Kind+" production merge")).Trim()
    $child=(& git -C $RepositoryPath commit-tree $expectedTree -p $merge -m ("child after malicious "+$Kind+" merge")).Trim()
    return [pscustomobject]@{PublishedHead=$publishedHead;MergeCommit=$merge;Parents=@($BaseSha,$publishedHead);ChildExecution=$child}
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
    $descriptorProperties = @("schemaVersion", "childTaskId", "phase", "sequence", "recoveryRepositorySha", "workflowId", "runId", "runAttempt", "artifactId", "artifactName", "githubArtifactDigest", "packageSha256", "manifestSha256", "ownedFiles", "validationResult", "parentAggregateStateDigest")
    $productionOne = Join-Path $root "production-descriptor-one"; $productionTwo = Join-Path $root "production-descriptor-two"
    New-Item -ItemType Directory -Force $productionOne,$productionTwo | Out-Null
    foreach ($name in @("publication-package.zip", "publication-package.manifest.json", "publication-package.result.json")) { Copy-Item -LiteralPath (Join-Path $a1.Root $name) -Destination (Join-Path $productionOne $name); Copy-Item -LiteralPath (Join-Path $a1.Root $name) -Destination (Join-Path $productionTwo $name) }
    $newDescriptorArguments = @{ Mode="NewDescriptor"; ChildTaskId="VSP-AI02-001TI-A1"; ChildPhase="A1"; Sequence=1; RecoveryRepositorySha=$recoverySha; WorkflowId="345910582"; RunId="9101"; RunAttempt=1; ArtifactId="8101"; ArtifactName="synthetic-production-a1"; GitHubArtifactDigest=("sha256:"+("a"*64)) }
    $productionDescriptorJson = Invoke-Chain ($newDescriptorArguments + @{ OutputDirectory=$productionOne })
    $productionDescriptor = $productionDescriptorJson | ConvertFrom-Json
    Expect-Pass "production NewDescriptor constructs a PSCustomObject before strict validation" { $source=Get-Content $tool -Raw; if($source -notmatch '\$descriptor\s*=\s*\[pscustomobject\]\[ordered\]@\{' ){throw "production descriptor is not a PSCustomObject"} }
    Expect-Pass "production NewDescriptor in-memory result has exactly the approved property set" { if(Compare-Object $descriptorProperties @($productionDescriptor.PSObject.Properties.Name)){throw "descriptor property set differs"} }
    Expect-Pass "valid A1 GENESIS package produces predecessor-descriptor.json" { if(-not(Test-Path -LiteralPath (Join-Path $productionOne "predecessor-descriptor.json") -PathType Leaf)){throw "descriptor was not created"} }
    Expect-Pass "descriptor validation occurs immediately before serialization" { $source=Get-Content $tool -Raw; $start=$source.IndexOf('function New-PredecessorDescriptor'); $validate=$source.IndexOf('Assert-Descriptor $descriptor',$start); $write=$source.IndexOf('Write-Utf8CanonicalJson (ConvertTo-CanonicalDescriptor $descriptor)',$start); if($validate-lt 0-or$write-lt 0-or$validate-gt$write){throw "validation does not precede serialization"} }
    Expect-Pass "serialized production descriptor validates after re-read" { $descriptorPath=Join-Path $productionOne "predecessor-descriptor.json"; Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=$descriptorPath;ArtifactDirectory=$productionOne;RecoveryRepositorySha=$recoverySha;RunId="9101";ArtifactId="8101";GitHubArtifactDigest=("sha256:"+("a"*64))}|Out-Null; if(-not(Get-Content $descriptorPath -Raw|Test-Json -SchemaFile "AI/Orchestrator/Templates/ai02-predecessor-descriptor.schema.json")){throw "descriptor schema validation failed"} }
    Invoke-Chain ($newDescriptorArguments + @{ OutputDirectory=$productionTwo }) | Out-Null
    Expect-Pass "canonical production descriptor serialization is deterministic" { if((Get-Hash (Join-Path $productionOne "predecessor-descriptor.json"))-ne(Get-Hash (Join-Path $productionTwo "predecessor-descriptor.json"))){throw "descriptor bytes differ"} }
    $extraDescriptor=Get-Content (Join-Path $productionOne "predecessor-descriptor.json") -Raw|ConvertFrom-Json; $extraDescriptor|Add-Member extraProperty "forbidden"; $extraDescriptorPath=Join-Path $root "descriptor-extra.json"; Write-Json $extraDescriptor $extraDescriptorPath
    Expect-Fail "additional production descriptor property is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=$extraDescriptorPath;ArtifactDirectory=$productionOne} }
    $missingDescriptor=Get-Content (Join-Path $productionOne "predecessor-descriptor.json") -Raw|ConvertFrom-Json; $missingDescriptor.PSObject.Properties.Remove("artifactName"); $missingDescriptorPath=Join-Path $root "descriptor-missing.json"; Write-Json $missingDescriptor $missingDescriptorPath
    Expect-Fail "missing production descriptor property is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=$missingDescriptorPath;ArtifactDirectory=$productionOne} }
    Expect-Fail "wrong production descriptor artifact ID is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=(Join-Path $productionOne "predecessor-descriptor.json");ArtifactDirectory=$productionOne;ArtifactId="9999"} }
    $wrongIdentity=Get-Content (Join-Path $productionOne "predecessor-descriptor.json") -Raw|ConvertFrom-Json; $wrongIdentity.workflowId="0"; $wrongIdentity.runId="0"; $wrongIdentityPath=Join-Path $root "descriptor-wrong-identity.json"; Write-Json $wrongIdentity $wrongIdentityPath
    Expect-Fail "wrong production descriptor workflow and run identity is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=$wrongIdentityPath;ArtifactDirectory=$productionOne} }
    Expect-Fail "wrong production descriptor GitHub artifact digest is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=(Join-Path $productionOne "predecessor-descriptor.json");ArtifactDirectory=$productionOne;GitHubArtifactDigest=("sha256:"+("b"*64))} }
    $wrongPackage=Get-Content (Join-Path $productionOne "predecessor-descriptor.json") -Raw|ConvertFrom-Json; $wrongPackage.packageSha256="b"*64; $wrongPackagePath=Join-Path $root "descriptor-wrong-package.json"; Write-Json $wrongPackage $wrongPackagePath
    Expect-Fail "wrong production descriptor package SHA is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=$wrongPackagePath;ArtifactDirectory=$productionOne} }
    $wrongManifest=Get-Content (Join-Path $productionOne "predecessor-descriptor.json") -Raw|ConvertFrom-Json; $wrongManifest.manifestSha256="b"*64; $wrongManifestPath=Join-Path $root "descriptor-wrong-manifest.json"; Write-Json $wrongManifest $wrongManifestPath
    Expect-Fail "wrong production descriptor manifest SHA is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=$wrongManifestPath;ArtifactDirectory=$productionOne} }
    $wrongParent=Get-Content (Join-Path $productionOne "predecessor-descriptor.json") -Raw|ConvertFrom-Json; $wrongParent.parentAggregateStateDigest="sha256:"+("b"*64); $wrongParentPath=Join-Path $root "descriptor-wrong-parent.json"; Write-Json $wrongParent $wrongParentPath
    Expect-Fail "wrong A1 production descriptor parent lineage is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=$wrongParentPath;ArtifactDirectory=$productionOne} }
    $wrongOwned=Get-Content (Join-Path $productionOne "predecessor-descriptor.json") -Raw|ConvertFrom-Json; $wrongOwned.ownedFiles=@($wrongOwned.ownedFiles|Select-Object -Skip 1); $wrongOwnedPath=Join-Path $root "descriptor-wrong-owned.json"; Write-Json $wrongOwned $wrongOwnedPath
    Expect-Fail "wrong production descriptor owned-file set is rejected" { Invoke-Chain @{Mode="ValidateDescriptor";DescriptorPath=$wrongOwnedPath;ArtifactDirectory=$productionOne} }
    $productionAggregate=Join-Path $root "production-aggregate.json"; Copy-Item -LiteralPath $genesisOneState -Destination $productionAggregate
    Expect-Pass "valid production descriptor appends to GENESIS aggregate" { Invoke-Chain @{Mode="AppendDescriptor";AggregateStatePath=$productionAggregate;DescriptorPath=(Join-Path $productionOne "predecessor-descriptor.json")} }
    Expect-Pass "aggregate produced from production descriptor contains A1 at sequence one" { $aggregate=Get-Content $productionAggregate -Raw|ConvertFrom-Json; if(@($aggregate.predecessors).Count-ne 1-or$aggregate.predecessors[0].phase-ne'A1'-or$aggregate.predecessors[0].sequence-ne 1){throw "aggregate A1 sequence is invalid"} }
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

    $legacy=New-LegacyA1Evidence (Join-Path $root "legacy-a1")
    Expect-Pass "exact immutable A1 legacy bytes reproduce approved hashes" { if((Get-Hash $legacy.DescriptorPath)-ne"c1595130a63ffb3eaba0facbb4c0f1e73c5dbf17db098ea5052ca23ff021a253"-or(Get-Hash $legacy.AggregatePath)-ne"750334cd06ecf529303b14ba0431f5ca492c714e606baca6767aef5961106ddc"-or(Get-Hash $legacy.ResultPath)-ne"fc78ce57a27bed3cf757074cf1c7769ae4d6d2e75c5354ac0c9abd3eac85c67e"){throw "immutable A1 evidence hash mismatch"} }
    Expect-Pass "exact A1 v1 checkpoint imports without rewriting bytes" { Invoke-Chain @{Mode="ValidateLegacyCheckpoint";CheckpointPath=$legacy.AggregatePath;DescriptorPath=$legacy.DescriptorPath;ChildPackageResultPath=$legacy.ResultPath} }
    Expect-Pass "legacy v1 checkpoint still validates against aggregate schema" { if(-not(Get-Content $legacy.AggregatePath -Raw|Test-Json -SchemaFile "AI/Orchestrator/Templates/ai02-aggregate-state.schema.json")){throw "legacy schema rejected"} }

    $repositoryBinding=New-RepositoryMergeBinding $legacy
    $repositoryBindingJson=@($repositoryBinding)|ConvertTo-Json -Compress -Depth 30
    $actionsBinding=[ordered]@{sourceType="ACTIONS_ARTIFACT";descriptorArtifactId="1";descriptorArtifactName="descriptor";descriptorArtifactDigest="sha256:"+("a"*64);packageArtifactId="2";packageArtifactName="package";packageArtifactDigest="sha256:"+("b"*64);runId="3";runAttempt=1;recoveryRepositorySha=("c"*40)}
    Expect-Pass "explicit Actions artifact binding validates against strict source schema" { if(-not(($actionsBinding|ConvertTo-Json -Compress -Depth 30)|Test-Json -SchemaFile "AI/Orchestrator/Templates/ai02-predecessor-binding.schema.json")){throw "Actions binding schema rejected"} }
    Expect-Pass "explicit authoritative repository merge binding validates against strict source schema" { if(-not(($repositoryBinding|ConvertTo-Json -Compress -Depth 30)|Test-Json -SchemaFile "AI/Orchestrator/Templates/ai02-predecessor-binding.schema.json")){throw "repository binding schema rejected"} }
    Expect-Fail "missing predecessor source discriminator rejects" { $bad=$actionsBinding.Clone();$bad.Remove("sourceType");Invoke-Chain @{Mode="ClassifyBindings";Repository="game2082001/VSP";BindingsJson=($bad|ConvertTo-Json -Compress -Depth 30)} }
    Expect-Fail "unknown predecessor source discriminator rejects" { $bad=$actionsBinding.Clone();$bad.sourceType="UNKNOWN";Invoke-Chain @{Mode="ClassifyBindings";Repository="game2082001/VSP";BindingsJson=($bad|ConvertTo-Json -Compress -Depth 30)} }
    Expect-Fail "mixed source-type fields reject" { $bad=$actionsBinding.Clone();$bad.mergeCommit="0"*40;Invoke-Chain @{Mode="ClassifyBindings";Repository="game2082001/VSP";BindingsJson=($bad|ConvertTo-Json -Compress -Depth 30)} }
    Expect-Pass "binding classifier preserves explicit Actions acquisition route" { $route=Invoke-Chain @{Mode="ClassifyBindings";Repository="game2082001/VSP";BindingsJson=(@($actionsBinding)|ConvertTo-Json -Compress -Depth 30)};if(($route|ConvertFrom-Json).route-ne"ACTIONS_ARTIFACT"-or-not($route|ConvertFrom-Json).requiresArtifactReadCredential){throw "Actions route changed"} }
    Expect-Pass "binding classifier selects credentialless repository merge route" { $route=Invoke-Chain @{Mode="ClassifyBindings";Repository="game2082001/VSP";BindingsJson=$repositoryBindingJson};if(($route|ConvertFrom-Json).route-ne"AUTHORITATIVE_REPOSITORY_MERGE"-or($route|ConvertFrom-Json).requiresArtifactReadCredential){throw "repository route changed"} }

    $pm1RecoverySha="2110f7ca4c851b183c6d07c3391c962ef33a5e98"
    $pm1Workspace=Join-Path $root "pm1-workspace"
    & git clone --quiet --shared --no-checkout (Get-Location).Path $pm1Workspace
    if($LASTEXITCODE-ne 0){throw "PM1 workspace clone failed"}
    & git -C $pm1Workspace checkout --quiet --detach $pm1RecoverySha
    if($LASTEXITCODE-ne 0){throw "PM1 recovery checkout failed"}
    $pm1State=Join-Path $root "pm1-checkpoint.json";$pm1Baseline=Join-Path $root "pm1-baseline.json";$pm1Evidence=Join-Path $root "pm1-evidence"
    $savedArtifactToken=$env:AI02_ARTIFACT_READ_TOKEN;$savedGhToken=$env:GH_TOKEN;$savedGithubToken=$env:GITHUB_TOKEN
    Remove-Item Env:AI02_ARTIFACT_READ_TOKEN,Env:GH_TOKEN,Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
    try {
        Expect-Pass "real A1 repository merge materializes from immutable local Git and bound evidence" { $result=Invoke-Chain @{Mode="AcquireAndMaterialize";Repository="game2082001/VSP";RecoveryRepositorySha=$pm1RecoverySha;WorkspacePath=$pm1Workspace;AggregateStatePath=$pm1State;BaselinePath=$pm1Baseline;EvidenceDirectory=$pm1Evidence;BindingsJson=$repositoryBindingJson};if(($result|ConvertFrom-Json).sourceType-ne"AUTHORITATIVE_REPOSITORY_MERGE"){throw "wrong acquisition result"} }
        Expect-Pass "repository merge acquisition preserves exact legacy checkpoint bytes" { if((Get-Hash $pm1State)-ne$repositoryBinding.aggregateFileSha256){throw "checkpoint bytes changed"} }
        Expect-Pass "repository merge acquisition baseline is deterministic" { $first=Get-Hash $pm1Baseline;Remove-Item $pm1State,$pm1Baseline -Force;Remove-Item $pm1Evidence -Recurse -Force;Invoke-Chain @{Mode="AcquireAndMaterialize";Repository="game2082001/VSP";RecoveryRepositorySha=$pm1RecoverySha;WorkspacePath=$pm1Workspace;AggregateStatePath=$pm1State;BaselinePath=$pm1Baseline;EvidenceDirectory=$pm1Evidence;BindingsJson=$repositoryBindingJson}|Out-Null;if((Get-Hash $pm1Baseline)-ne$first){throw "baseline changed"} }
        Expect-Pass "repository merge predecessor survives immutable post-Claude verification" { Invoke-Chain @{Mode="ValidatePostClaude";WorkspacePath=$pm1Workspace;BaselinePath=$pm1Baseline;AggregateStatePath=$pm1State;ExpectedBaselineSha256=(Get-Hash $pm1Baseline);ExpectedAggregateStateSha256=(Get-Hash $pm1State)} }
        $mutationPath=Join-Path $pm1Workspace $phaseFiles.A1[0];$originalBytes=[IO.File]::ReadAllBytes($mutationPath);[IO.File]::AppendAllText($mutationPath,"mutation",[Text.UTF8Encoding]::new($false))
        Expect-Fail "post-materialization repository predecessor mutation rejects" { Invoke-Chain @{Mode="ValidatePostClaude";WorkspacePath=$pm1Workspace;BaselinePath=$pm1Baseline;AggregateStatePath=$pm1State;ExpectedBaselineSha256=(Get-Hash $pm1Baseline);ExpectedAggregateStateSha256=(Get-Hash $pm1State)} }
        [IO.File]::WriteAllBytes($mutationPath,$originalBytes)
        $env:AI02_ARTIFACT_READ_TOKEN="not-a-real-token"
        Expect-Fail "repository merge acquisition rejects artifact credential presence" { Invoke-Chain @{Mode="AcquireAndMaterialize";Repository="game2082001/VSP";RecoveryRepositorySha=$pm1RecoverySha;WorkspacePath=$pm1Workspace;AggregateStatePath=$pm1State;BaselinePath=$pm1Baseline;EvidenceDirectory=$pm1Evidence;BindingsJson=$repositoryBindingJson} }
        Remove-Item Env:AI02_ARTIFACT_READ_TOKEN -ErrorAction SilentlyContinue
    } finally {
        if($null-ne$savedArtifactToken){$env:AI02_ARTIFACT_READ_TOKEN=$savedArtifactToken}else{Remove-Item Env:AI02_ARTIFACT_READ_TOKEN -ErrorAction SilentlyContinue}
        if($null-ne$savedGhToken){$env:GH_TOKEN=$savedGhToken}else{Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue}
        if($null-ne$savedGithubToken){$env:GITHUB_TOKEN=$savedGithubToken}else{Remove-Item Env:GITHUB_TOKEN -ErrorAction SilentlyContinue}
    }

    foreach($case in @(
        @{name="wrong repository";mutate={param($v)$v.repository="other/repository"}},
        @{name="wrong merge commit";mutate={param($v)$v.mergeCommit=$v.orderedMergeParents[0]}},
        @{name="wrong parent count";mutate={param($v)$v.orderedMergeParents=@($v.orderedMergeParents[0])}},
        @{name="wrong parent SHA";mutate={param($v)$v.orderedMergeParents[0]="0"*40}},
        @{name="wrong ordered merge parents";mutate={param($v)$x=$v.orderedMergeParents[0];$v.orderedMergeParents[0]=$v.orderedMergeParents[1];$v.orderedMergeParents[1]=$x}},
        @{name="wrong published production head";mutate={param($v)$v.publishedProductionHead=$v.orderedMergeParents[0]}},
        @{name="wrong descriptor digest";mutate={param($v)$v.descriptorSha256="0"*64}},
        @{name="wrong aggregate digest";mutate={param($v)$v.aggregateStateDigest="sha256:"+("0"*64)}},
        @{name="wrong aggregate file digest";mutate={param($v)$v.aggregateFileSha256="0"*64}},
        @{name="wrong package digest";mutate={param($v)$v.packageSha256="0"*64}},
        @{name="wrong manifest digest";mutate={param($v)$v.manifestSha256="0"*64}},
        @{name="wrong result digest";mutate={param($v)$v.resultSha256="0"*64}},
        @{name="missing production file";mutate={param($v)$v.productionFiles=@($v.productionFiles|Select-Object -Skip 1)}},
        @{name="additional production file";mutate={param($v)$v.productionFiles+=@($v.productionFiles[0])}},
        @{name="wrong production mode";mutate={param($v)$v.productionFiles[0].mode="100755"}},
        @{name="wrong production blob";mutate={param($v)$v.productionFiles[0].gitBlobId="0"*40}},
        @{name="wrong production size";mutate={param($v)$v.productionFiles[0].size=[int64]$v.productionFiles[0].size+1}},
        @{name="wrong production hash";mutate={param($v)$v.productionFiles[0].sha256="0"*64}}
    )){
        $bad=$repositoryBindingJson|ConvertFrom-Json -Depth 30;& $case.mutate $bad
        Expect-Fail ("repository merge "+$case.name+" rejects") { Invoke-Chain @{Mode="AcquireAndMaterialize";Repository="game2082001/VSP";RecoveryRepositorySha=$pm1RecoverySha;WorkspacePath=$pm1Workspace;AggregateStatePath=(Join-Path $root "bad-state.json");BaselinePath=(Join-Path $root "bad-baseline.json");EvidenceDirectory=(Join-Path $root "bad-evidence");BindingsJson=($bad|ConvertTo-Json -Compress -Depth 30)} }
    }
    $missingObject=$repositoryBindingJson|ConvertFrom-Json -Depth 30;$missingObject.mergeCommit="0"*40
    Expect-Fail "repository merge missing Git object rejects without fetch" { Invoke-Chain @{Mode="AcquireAndMaterialize";Repository="game2082001/VSP";RecoveryRepositorySha=$pm1RecoverySha;WorkspacePath=$pm1Workspace;AggregateStatePath=(Join-Path $root "missing-object-state.json");BaselinePath=(Join-Path $root "missing-object-baseline.json");EvidenceDirectory=(Join-Path $root "missing-object-evidence");BindingsJson=($missingObject|ConvertTo-Json -Compress -Depth 30)} }
    $notDescendantWorkspace=Join-Path $root "pm1-not-descendant";& git clone --quiet --shared --no-checkout (Get-Location).Path $notDescendantWorkspace;& git -C $notDescendantWorkspace checkout --quiet --detach $repositoryBinding.executionRepositorySha
    Expect-Fail "repository merge execution base not descendant rejects" { Invoke-Chain @{Mode="AcquireAndMaterialize";Repository="game2082001/VSP";RecoveryRepositorySha=$repositoryBinding.executionRepositorySha;WorkspacePath=$notDescendantWorkspace;AggregateStatePath=(Join-Path $root "not-descendant-state.json");BaselinePath=(Join-Path $root "not-descendant-baseline.json");EvidenceDirectory=(Join-Path $root "not-descendant-evidence");BindingsJson=$repositoryBindingJson} }
    $driftWorkspace=Join-Path $root "pm1-child-drift";& git clone --quiet --shared --no-checkout (Get-Location).Path $driftWorkspace;& git -C $driftWorkspace config user.name "Synthetic PM1 Test";& git -C $driftWorkspace config user.email "pm1@example.invalid";& git -C $driftWorkspace checkout --quiet --detach $repositoryBinding.mergeCommit
    $driftPath=Join-Path $driftWorkspace $phaseFiles.A1[0];[IO.File]::AppendAllText($driftPath,"drift",[Text.UTF8Encoding]::new($false));& git -C $driftWorkspace add -- $phaseFiles.A1[0];& git -C $driftWorkspace commit --quiet -m "synthetic predecessor drift";$driftSha=(& git -C $driftWorkspace rev-parse HEAD).Trim()
    Expect-Fail "repository merge child execution tree predecessor drift rejects" { Invoke-Chain @{Mode="AcquireAndMaterialize";Repository="game2082001/VSP";RecoveryRepositorySha=$driftSha;WorkspacePath=$driftWorkspace;AggregateStatePath=(Join-Path $root "drift-state.json");BaselinePath=(Join-Path $root "drift-baseline.json");EvidenceDirectory=(Join-Path $root "drift-evidence");BindingsJson=$repositoryBindingJson} }
    Expect-Pass "repository merge acquisition source contains no automatic fetch" { if((Get-Content $tool -Raw)-match'git\s+fetch|Invoke-CheckpointGit\s+@\("fetch"'){throw "automatic fetch found"} }

    $a2ExecutionSha="5c98c704a3a09aeebe58b46648dd203245e730cf"
    $crossBaseA2=New-Package (Join-Path $root "cross-base-a2") A2 "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1" "VSP-AI02-001TI-A2" @{} @() $a2ExecutionSha
    $a2ResultPath=Join-Path $crossBaseA2.Root "publication-package.result.json"
    $checkpointA2One=Join-Path $root "checkpoint-a2-one.json";$checkpointA2Two=Join-Path $root "checkpoint-a2-two.json"
    $a2CheckpointArgs=@{Mode="NewCheckpointV2";Repository="game2082001/VSP";WorkspacePath=(Get-Location).Path;CheckpointEvidencePaths=@($legacy.AggregatePath);DescriptorEvidencePaths=@($legacy.DescriptorPath);PackageResultEvidencePaths=@($legacy.ResultPath);PublicationEvidencePaths=@($legacy.PublicationPath);ChildDescriptorPath=$crossBaseA2.DescriptorPath;ChildPackageResultPath=$a2ResultPath}
    Expect-Pass "valid A1 v1 to A2 cross-base checkpoint extension" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{CheckpointPath=$checkpointA2One}) }
    Expect-Pass "repeated A2 checkpoint construction is byte-identical" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{CheckpointPath=$checkpointA2Two})|Out-Null;if((Get-Hash $checkpointA2One)-ne(Get-Hash $checkpointA2Two)){throw "v2 checkpoint bytes differ"} }
    Expect-Pass "v2 A2 checkpoint validates with complete ordered evidence" { Invoke-Chain @{Mode="ValidateCheckpointV2";Repository="game2082001/VSP";WorkspacePath=(Get-Location).Path;CheckpointEvidencePaths=@($legacy.AggregatePath,$checkpointA2One);DescriptorEvidencePaths=@($legacy.DescriptorPath,$crossBaseA2.DescriptorPath);PackageResultEvidencePaths=@($legacy.ResultPath,$a2ResultPath);PublicationEvidencePaths=@($legacy.PublicationPath)} }
    Expect-Pass "A2 checkpoint preserves historical A1 base and truthful child base" { $checkpoint=Get-Content $checkpointA2One -Raw|ConvertFrom-Json;if($checkpoint.parentCheckpoint.terminalExecutionRepositorySha-ne"8a0a441c532295405b8133d96142844026ee4a93"-or$checkpoint.child.executionRepositorySha-ne$a2ExecutionSha-or$checkpoint.parentCheckpoint.aggregateStateDigest-ne"sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1"){throw "cross-base authorities collapsed"} }
    Expect-Pass "v2 checkpoint validates against aggregate schema" { if(-not(Get-Content $checkpointA2One -Raw|Test-Json -SchemaFile "AI/Orchestrator/Templates/ai02-aggregate-state.schema.json")){throw "v2 schema rejected"} }

    $a2Content='26214400 20971520 52428800 200 10485760 262144 240 100 16 1048576 3 vsp-ai02-intake-v1 EXACT_BASE_ONLY FIRST_USE NOT_YET_CONSUMED REJECT_REPLAY'
    $syntheticRepo=New-SyntheticA2PublicationRepository (Join-Path $root "synthetic-publication-repo") $a2ExecutionSha $a2Content
    $publicationA2Path=Join-Path $root "a2-publication.json"
    $checkpointA2=Get-Content $checkpointA2One -Raw|ConvertFrom-Json
    $publicationA2=[ordered]@{sourceType="AUTHORITATIVE_REPOSITORY_MERGE";repository="game2082001/VSP";mergeCommit=$syntheticRepo.MergeCommit;orderedMergeParents=$syntheticRepo.Parents;publishedProductionHead=$syntheticRepo.ProductionHead;checkpointAggregateStateDigest=$checkpointA2.aggregateStateDigest;checkpointAggregateFileSha256=(Get-Hash $checkpointA2One);productionFiles=@($syntheticRepo.File)}
    Write-Json $publicationA2 $publicationA2Path
    $crossBaseA3=New-Package (Join-Path $root "cross-base-a3") A3 $checkpointA2.aggregateStateDigest "VSP-AI02-001TI-A3" @{} @() $syntheticRepo.ChildExecution
    $a3ResultPath=Join-Path $crossBaseA3.Root "publication-package.result.json"
    $checkpointA3=Join-Path $root "checkpoint-a3.json"
    $a3CheckpointArgs=@{Mode="NewCheckpointV2";Repository="game2082001/VSP";WorkspacePath=$syntheticRepo.Path;CheckpointEvidencePaths=@($legacy.AggregatePath,$checkpointA2One);DescriptorEvidencePaths=@($legacy.DescriptorPath,$crossBaseA2.DescriptorPath);PackageResultEvidencePaths=@($legacy.ResultPath,$a2ResultPath);PublicationEvidencePaths=@($legacy.PublicationPath,$publicationA2Path);ChildDescriptorPath=$crossBaseA3.DescriptorPath;ChildPackageResultPath=$a3ResultPath;CheckpointPath=$checkpointA3}
    Expect-Pass "synthetic A2 publication supports A2 to A3 checkpoint extension" { Invoke-Chain $a3CheckpointArgs }
    $validateA3Args=@{Mode="ValidateCheckpointV2";Repository="game2082001/VSP";WorkspacePath=$syntheticRepo.Path;CheckpointEvidencePaths=@($legacy.AggregatePath,$checkpointA2One,$checkpointA3);DescriptorEvidencePaths=@($legacy.DescriptorPath,$crossBaseA2.DescriptorPath,$crossBaseA3.DescriptorPath);PackageResultEvidencePaths=@($legacy.ResultPath,$a2ResultPath,$a3ResultPath);PublicationEvidencePaths=@($legacy.PublicationPath,$publicationA2Path)}
    Expect-Pass "complete D1 M1 D2 M2 D3 lineage validates" { Invoke-Chain $validateA3Args }
    Expect-Pass "A2 publication exact merge parents blobs modes sizes and hashes validate" { $validated=Invoke-Chain $validateA3Args;if($validated-notmatch'completeLineageEvidence'){throw "complete lineage result missing"} }
    foreach($kind in @("missing","changed")){
        $scenario=New-MaliciousPublishedHeadScenario $syntheticRepo.Path $a2ExecutionSha $syntheticRepo.MergeCommit $kind
        $badPublishedHeadPath=Join-Path $root ("published-head-"+$kind+".json")
        Write-Json ([ordered]@{sourceType="AUTHORITATIVE_REPOSITORY_MERGE";repository="game2082001/VSP";mergeCommit=$scenario.MergeCommit;orderedMergeParents=$scenario.Parents;publishedProductionHead=$scenario.PublishedHead;checkpointAggregateStateDigest=$checkpointA2.aggregateStateDigest;checkpointAggregateFileSha256=(Get-Hash $checkpointA2One);productionFiles=@($syntheticRepo.File)}) $badPublishedHeadPath
        $badHeadChild=New-Package (Join-Path $root ("bad-head-child-"+$kind)) A3 $checkpointA2.aggregateStateDigest "VSP-AI02-001TI-A3" @{} @() $scenario.ChildExecution
        $badHeadArguments=Merge-Arguments $a3CheckpointArgs @{PublicationEvidencePaths=@($legacy.PublicationPath,$badPublishedHeadPath);ChildDescriptorPath=$badHeadChild.DescriptorPath;ChildPackageResultPath=(Join-Path $badHeadChild.Root "publication-package.result.json");CheckpointPath=(Join-Path $root ("bad-published-head-checkpoint-"+$kind+".json"))}
        Expect-Fail ("published production HEAD with "+$kind+" predecessor blob rejects") { Invoke-Chain $badHeadArguments }
    }

    $modifiedLegacy=Join-Path $root "modified-legacy.json";Copy-Item $legacy.AggregatePath $modifiedLegacy;[IO.File]::AppendAllText($modifiedLegacy," ",[Text.UTF8Encoding]::new($false))
    Expect-Fail "modified legacy v1 bytes reject" { Invoke-Chain @{Mode="ValidateLegacyCheckpoint";CheckpointPath=$modifiedLegacy;DescriptorPath=$legacy.DescriptorPath;ChildPackageResultPath=$legacy.ResultPath} }
    $wrongLegacyDigest=Copy-CanonicalJsonWithMutation $legacy.AggregatePath (Join-Path $root "wrong-legacy-digest.json") { param($v) $v.aggregateStateDigest="sha256:"+("0"*64) }
    Expect-Fail "wrong legacy v1 digest rejects" { Invoke-Chain @{Mode="ValidateLegacyCheckpoint";CheckpointPath=$wrongLegacyDigest;DescriptorPath=$legacy.DescriptorPath;ChildPackageResultPath=$legacy.ResultPath} }
    $wrongLegacyDescriptor=Copy-CanonicalJsonWithMutation $legacy.DescriptorPath (Join-Path $root "wrong-legacy-descriptor.json") { param($v) $v.artifactName="changed" }
    Expect-Fail "wrong legacy terminal descriptor SHA rejects" { Invoke-Chain @{Mode="ValidateLegacyCheckpoint";CheckpointPath=$legacy.AggregatePath;DescriptorPath=$wrongLegacyDescriptor;ChildPackageResultPath=$legacy.ResultPath} }
    Expect-Fail "missing lineage evidence rejects" { Invoke-Chain @{Mode="ValidateCheckpointV2";CheckpointEvidencePaths=@();DescriptorEvidencePaths=@();PackageResultEvidencePaths=@();PublicationEvidencePaths=@()} }
    Expect-Fail "missing referenced parent checkpoint rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,(Join-Path $root "missing-v2.json"),$checkpointA3)}) }
    Expect-Fail "partial checkpoint chain rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$checkpointA3);DescriptorEvidencePaths=@($legacy.DescriptorPath,$crossBaseA3.DescriptorPath);PackageResultEvidencePaths=@($legacy.ResultPath,$a3ResultPath);PublicationEvidencePaths=@($legacy.PublicationPath)}) }

    $badParentFile=Copy-CanonicalJsonWithMutation $checkpointA2One (Join-Path $root "bad-parent-file.json") { param($v) $v.parentCheckpoint.aggregateFileSha256="0"*64 }
    Expect-Fail "wrong parent aggregate file SHA rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$badParentFile,$checkpointA3)}) }
    $badParentTask=Copy-CanonicalJsonWithMutation $checkpointA2One (Join-Path $root "bad-parent-task.json") { param($v) $v.parentCheckpoint.terminalTaskId="VSP-AI02-SUBSTITUTE" }
    Expect-Fail "wrong parent terminal task rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$badParentTask,$checkpointA3)}) }
    $badParentPhase=Copy-CanonicalJsonWithMutation $checkpointA2One (Join-Path $root "bad-parent-phase.json") { param($v) $v.parentCheckpoint.terminalPhase="A2" }
    Expect-Fail "wrong parent phase and sequence binding rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$badParentPhase,$checkpointA3)}) }
    $badParentDigest=Copy-CanonicalJsonWithMutation $checkpointA2One (Join-Path $root "bad-parent-digest.json") { param($v) $v.parentCheckpoint.aggregateStateDigest="sha256:"+("1"*64) }
    Expect-Fail "parent checkpoint substitution rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$badParentDigest,$checkpointA3)}) }

    $badPublicationMerge=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root "bad-publication-merge.json") { param($v) $v.mergeCommit=$v.orderedMergeParents[0] }
    Expect-Fail "wrong production merge rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{PublicationEvidencePaths=@($badPublicationMerge);CheckpointPath=(Join-Path $root "bad-merge-checkpoint.json")}) }
    $badPublicationOrder=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root "bad-publication-order.json") { param($v) $first=$v.orderedMergeParents[0];$v.orderedMergeParents[0]=$v.orderedMergeParents[1];$v.orderedMergeParents[1]=$first }
    Expect-Fail "wrong ordered merge parents reject" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{PublicationEvidencePaths=@($badPublicationOrder);CheckpointPath=(Join-Path $root "bad-order-checkpoint.json")}) }
    $badPublicationCount=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root "bad-publication-count.json") { param($v) $v.orderedMergeParents=@($v.orderedMergeParents[0]) }
    Expect-Fail "wrong merge parent count rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{PublicationEvidencePaths=@($badPublicationCount);CheckpointPath=(Join-Path $root "bad-count-checkpoint.json")}) }
    $mutablePublication=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root "mutable-publication.json") { param($v) $v.mergeCommit="main" }
    Expect-Fail "mutable ref substitution rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{PublicationEvidencePaths=@($mutablePublication);CheckpointPath=(Join-Path $root "mutable-checkpoint.json")}) }
    $badPublicationHead=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root "bad-publication-head.json") { param($v) $v.publishedProductionHead=$v.orderedMergeParents[0] }
    Expect-Fail "published production head mismatch rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{PublicationEvidencePaths=@($badPublicationHead);CheckpointPath=(Join-Path $root "bad-head-checkpoint.json")}) }
    foreach($case in @(
        @{name="missing predecessor";mutate={param($v)$v.productionFiles=@($v.productionFiles|Select-Object -Skip 1)}},
        @{name="additional predecessor";mutate={param($v)$v.productionFiles+=@($v.productionFiles[0])}},
        @{name="wrong predecessor mode";mutate={param($v)$v.productionFiles[0].mode="100755"}},
        @{name="wrong Git blob";mutate={param($v)$v.productionFiles[0].gitBlobId="0"*40}},
        @{name="wrong byte size";mutate={param($v)$v.productionFiles[0].size=[int64]$v.productionFiles[0].size+1}},
        @{name="wrong predecessor SHA-256";mutate={param($v)$v.productionFiles[0].sha256="0"*64}}
    )){
        $path=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root ("publication-"+$case.name.Replace(' ','-')+".json")) $case.mutate
        Expect-Fail ($case.name+" rejects") { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{PublicationEvidencePaths=@($path);CheckpointPath=(Join-Path $root "bad-publication-checkpoint.json")}) }
    }

    $notDescendant=New-Package (Join-Path $root "not-descendant-a2") A2 "sha256:a29ea66e53f3645ca38c0b2b6e2880cb472a3f37bc1fea15b01980bea2a2caa1" "VSP-AI02-001TI-A2" @{} @() "8a0a441c532295405b8133d96142844026ee4a93"
    Expect-Fail "child execution base not descendant rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{ChildDescriptorPath=$notDescendant.DescriptorPath;ChildPackageResultPath=(Join-Path $notDescendant.Root "publication-package.result.json");CheckpointPath=(Join-Path $root "not-descendant.json")}) }
    $wrongChildTask=Copy-CanonicalJsonWithMutation $crossBaseA2.DescriptorPath (Join-Path $root "wrong-child-task.json") { param($v) $v.childTaskId="VSP-AI02-001TI-A2-SUBSTITUTE" }
    $wrongChildTaskResult=Copy-CanonicalJsonWithMutation $a2ResultPath (Join-Path $root "wrong-child-task-result.json") { param($v) $v.taskId="VSP-AI02-001TI-A2-SUBSTITUTE" }
    Expect-Fail "cross-task child substitution rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{ChildDescriptorPath=$wrongChildTask;ChildPackageResultPath=$wrongChildTaskResult;CheckpointPath=(Join-Path $root "wrong-task.json")}) }
    $wrongSequence=Copy-CanonicalJsonWithMutation $crossBaseA2.DescriptorPath (Join-Path $root "wrong-sequence.json") { param($v) $v.sequence=3 }
    Expect-Fail "child sequence skip rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{ChildDescriptorPath=$wrongSequence;CheckpointPath=(Join-Path $root "wrong-sequence-checkpoint.json")}) }
    $rollbackSequence=Copy-CanonicalJsonWithMutation $crossBaseA3.DescriptorPath (Join-Path $root "rollback-sequence.json") { param($v) $v.sequence=1 }
    Expect-Fail "child sequence rollback rejects" { Invoke-Chain (Merge-Arguments $a3CheckpointArgs @{ChildDescriptorPath=$rollbackSequence;CheckpointPath=(Join-Path $root "rollback-checkpoint.json")}) }
    $duplicateSequence=Copy-CanonicalJsonWithMutation $crossBaseA3.DescriptorPath (Join-Path $root "duplicate-sequence.json") { param($v) $v.sequence=2 }
    Expect-Fail "duplicate child sequence rejects" { Invoke-Chain (Merge-Arguments $a3CheckpointArgs @{ChildDescriptorPath=$duplicateSequence;CheckpointPath=(Join-Path $root "duplicate-checkpoint.json")}) }
    Expect-Fail "descriptor replay rejects" { Invoke-Chain (Merge-Arguments $a3CheckpointArgs @{ChildDescriptorPath=$crossBaseA2.DescriptorPath;ChildPackageResultPath=$a2ResultPath;CheckpointPath=(Join-Path $root "replay-checkpoint.json")}) }
    $changedChildSha=Copy-CanonicalJsonWithMutation $crossBaseA2.DescriptorPath (Join-Path $root "changed-child-sha.json") { param($v) $v.recoveryRepositorySha="0"*40 }
    Expect-Fail "changed child execution SHA rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{ChildDescriptorPath=$changedChildSha;CheckpointPath=(Join-Path $root "changed-child-sha-checkpoint.json")}) }
    $alteredChild=Copy-CanonicalJsonWithMutation $crossBaseA2.DescriptorPath (Join-Path $root "altered-child.json") { param($v) $v.ownedFiles[0].sha256="0"*64 }
    Expect-Fail "altered child descriptor rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{ChildDescriptorPath=$alteredChild;CheckpointPath=(Join-Path $root "altered-child-checkpoint.json")}) }
    foreach($property in @("packageSha256","manifestSha256")){
        $badResult=Copy-CanonicalJsonWithMutation $a2ResultPath (Join-Path $root ("bad-result-"+$property+".json")) { param($v) $v.$property="0"*64 }
        Expect-Fail ($property+" disagreement rejects") { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{ChildPackageResultPath=$badResult;CheckpointPath=(Join-Path $root "bad-result-checkpoint.json")}) }
    }
    $badChangedFiles=Copy-CanonicalJsonWithMutation $a2ResultPath (Join-Path $root "bad-result-files.json") { param($v) $v.changedFiles[0].sha256="0"*64 }
    Expect-Fail "package result changed-file disagreement rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{ChildPackageResultPath=$badChangedFiles;CheckpointPath=(Join-Path $root "bad-result-files-checkpoint.json")}) }
    $badParentExecution=Copy-CanonicalJsonWithMutation $checkpointA2One (Join-Path $root "bad-parent-execution.json") { param($v) $v.parentCheckpoint.terminalExecutionRepositorySha="0"*40 }
    Expect-Fail "historical execution-base rewrite rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$badParentExecution,$checkpointA3)}) }
    Expect-Fail "stale parent publication substitution rejects" { Invoke-Chain (Merge-Arguments $a3CheckpointArgs @{PublicationEvidencePaths=@($legacy.PublicationPath,$legacy.PublicationPath);CheckpointPath=(Join-Path $root "stale-publication-checkpoint.json")}) }

    $reorderedCheckpoint=Join-Path $root "reordered-checkpoint.json";$value=Get-Content $checkpointA2One -Raw|ConvertFrom-Json;$reordered=[ordered]@{checkpointType=$value.checkpointType;schemaVersion=$value.schemaVersion;checkpointPolicyVersion=$value.checkpointPolicyVersion;parentCheckpoint=$value.parentCheckpoint;parentPublication=$value.parentPublication;child=$value.child;aggregateStateDigest=$value.aggregateStateDigest};Write-Json $reordered $reorderedCheckpoint
    Expect-Fail "reordered authoritative JSON properties reject" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$reorderedCheckpoint,$checkpointA3)}) }
    $nonCanonical=Join-Path $root "noncanonical-checkpoint.json";[IO.File]::WriteAllText($nonCanonical,(Get-Content $checkpointA2One -Raw)+"`n",[Text.UTF8Encoding]::new($false))
    Expect-Fail "noncanonical trailing newline rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$nonCanonical,$checkpointA3)}) }
    $unknownVersion=Copy-CanonicalJsonWithMutation $checkpointA2One (Join-Path $root "unknown-version.json") { param($v) $v.schemaVersion="9.0" }
    Expect-Fail "unknown checkpoint schema rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$unknownVersion,$checkpointA3)}) }
    $unknownType=Copy-CanonicalJsonWithMutation $checkpointA2One (Join-Path $root "unknown-type.json") { param($v) $v.checkpointType="UNKNOWN" }
    Expect-Fail "unknown checkpoint type rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$unknownType,$checkpointA3)}) }
    $ambiguous=Copy-CanonicalJsonWithMutation $checkpointA2One (Join-Path $root "ambiguous.json") { param($v) $v|Add-Member recoveryRepositorySha ("0"*40) }
    Expect-Fail "ambiguous v1 v2 structure rejects" { Invoke-Chain (Merge-Arguments $validateA3Args @{CheckpointEvidencePaths=@($legacy.AggregatePath,$ambiguous,$checkpointA3)}) }
    Expect-Fail "v2 to v1 downgrade rejects" { Invoke-Chain @{Mode="ValidateCheckpointV2";Repository="game2082001/VSP";WorkspacePath=(Get-Location).Path;CheckpointEvidencePaths=@($legacy.AggregatePath);DescriptorEvidencePaths=@($legacy.DescriptorPath);PackageResultEvidencePaths=@($legacy.ResultPath);PublicationEvidencePaths=@()} }
    $missingCommitPublication=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root "missing-commit.json") { param($v) $v.mergeCommit="0"*40 }
    Expect-Fail "missing immutable commit rejects without fetch" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{PublicationEvidencePaths=@($missingCommitPublication);CheckpointPath=(Join-Path $root "missing-commit-checkpoint.json")}) }
    $missingBlobPublication=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root "missing-blob.json") { param($v) $v.productionFiles[0].gitBlobId="0"*40 }
    Expect-Fail "missing immutable blob rejects" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{PublicationEvidencePaths=@($missingBlobPublication);CheckpointPath=(Join-Path $root "missing-blob-checkpoint.json")}) }
    $rewrittenAfterChild=Copy-CanonicalJsonWithMutation $publicationA2Path (Join-Path $root "rewritten-after-child.json") { param($v) $v.mergeCommit=$syntheticRepo.ChildExecution;$v.orderedMergeParents=@($syntheticRepo.MergeCommit,$syntheticRepo.ProductionHead) }
    Expect-Fail "publication rewriting after child execution rejects" { Invoke-Chain (Merge-Arguments $a3CheckpointArgs @{PublicationEvidencePaths=@($legacy.PublicationPath,$rewrittenAfterChild);CheckpointPath=(Join-Path $root "rewritten-after-child-checkpoint.json")}) }
    $legacyTree=(& git -C $syntheticRepo.Path rev-parse "aa53c00d5e4125a53f8f835220bf6d6b2e911b14^{tree}").Trim()
    $unauthorizedMerge=(& git -C $syntheticRepo.Path commit-tree $legacyTree -p "8a0a441c532295405b8133d96142844026ee4a93" -p "cb7dbf0cfbcb0f715001588cdd6da97264e19d06" -m "synthetic unauthorized same-byte merge").Trim()
    $unauthorizedPublication=Copy-CanonicalJsonWithMutation $legacy.PublicationPath (Join-Path $root "unauthorized-publication.json") { param($v) $v.mergeCommit=$unauthorizedMerge }
    Expect-Fail "same predecessor bytes from unauthorized commit reject" { Invoke-Chain (Merge-Arguments $a2CheckpointArgs @{WorkspacePath=$syntheticRepo.Path;PublicationEvidencePaths=@($unauthorizedPublication);CheckpointPath=(Join-Path $root "unauthorized-commit-checkpoint.json")}) }
    Expect-Pass "checkpoint validation source contains no automatic fetch" { if((Get-Content $tool -Raw)-match'git\s+fetch|Invoke-CheckpointGit\s+@\("fetch"'){throw "automatic fetch found"} }
    Expect-Pass "unexpected checkpoint exceptions are sanitized" { try{Invoke-Chain @{Mode="ValidateCheckpointV2";CheckpointEvidencePaths=@($null);DescriptorEvidencePaths=@($null);PackageResultEvidencePaths=@($null);PublicationEvidencePaths=@()}|Out-Null;throw "unexpected pass"}catch{if($_.Exception.Message-notmatch'INTERNAL_VALIDATION_ERROR|LINEAGE_EVIDENCE_INCOMPLETE'){throw "unsanitized failure"}} }

    Expect-Pass "predecessor descriptor schema parses and remains strict" { $schema=Get-Content "AI/Orchestrator/Templates/ai02-predecessor-descriptor.schema.json" -Raw|ConvertFrom-Json;if($schema.additionalProperties-ne$false){throw "descriptor schema is not strict"} }
    Expect-Pass "aggregate schema parses as a strict v1/v2 union" { $schema=Get-Content "AI/Orchestrator/Templates/ai02-aggregate-state.schema.json" -Raw|ConvertFrom-Json;if(@($schema.oneOf).Count-ne 2-or$schema.'$defs'.legacyV1.additionalProperties-ne$false-or$schema.'$defs'.checkpointV2.additionalProperties-ne$false){throw "aggregate schema union is not strict"} }
    $workflow = Get-Content ".github/workflows/ai02-claude-artifact-developer.yml" -Raw
    Expect-Pass "artifact credential is scoped outside Claude and explicitly checked absent" { if($workflow -notmatch 'AI02_ARTIFACT_READ_TOKEN' -or $workflow -notmatch 'Verify Claude receives no artifact or repository credential'){throw "credential lifecycle guard missing"} }
    Expect-Pass "workflow routes Actions artifact credential only to Actions acquisition" { if(([regex]::Matches($workflow,'AI02_ARTIFACT_READ_TOKEN:\s*\$\{\{ github\.token \}\}')).Count-ne 1-or$workflow-notmatch"steps\.predecessor_route\.outputs\.route == 'ACTIONS_ARTIFACT'"){throw "Actions credential route changed"} }
    Expect-Pass "workflow repository merge acquisition step declares no token" { $match=[regex]::Match($workflow,'(?s)- name: Materialize authoritative repository predecessors(?<body>.*?)(?=\r?\n\s+- name:)');if(-not$match.Success-or$match.Groups['body'].Value-match'github\.token|APP_TOKEN'){throw "repository route credential exposure"} }
    Expect-Pass "workflow repository merge route consumes AGV2 checkpoint construction" { if($workflow-notmatch'AI02_PREDECESSOR_ROUTE -eq "AUTHORITATIVE_REPOSITORY_MERGE"'-or$workflow-notmatch'-Mode NewCheckpointV2'){throw "AGV2 integration missing"} }
    Expect-Pass "workflow retains minimum Claude file tools exactly" { if($workflow-notmatch'--allowedTools "Read,Write,Edit"'){throw "Claude allowed tools changed"} }
    Expect-Pass "closed predecessor binding schema exposes exactly two source branches" { $schema=Get-Content "AI/Orchestrator/Templates/ai02-predecessor-binding.schema.json" -Raw|ConvertFrom-Json -Depth 30;if(@($schema.oneOf).Count-ne2-or$schema.'$defs'.actionsArtifact.additionalProperties-ne$false-or$schema.'$defs'.authoritativeRepositoryMerge.additionalProperties-ne$false){throw "binding schema is not closed"} }
    Expect-Pass "materializer and Claude retain no repository-write authority" { if($workflow -notmatch 'contents: read' -or $workflow -match 'contents: write'){throw "repository credential boundary changed"} }
    Expect-Pass "P2 does not invoke or alter Repository Transport" { if((Get-Content $tool -Raw) -match 'repository-transport' -or $workflow -match 'repository-transport'){throw "Repository Transport referenced"} }
    Expect-Pass "P2 performs no publication side effect" { if((Get-Content $tool -Raw) -match 'git push|gh pr|Repository Transport'){throw "publication side effect found"} }
}
finally { if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force } }

"AI02 artifact-chain focused tests: $script:passed passed, $script:failed failed"
if ($script:failed -ne 0) { exit 1 }
