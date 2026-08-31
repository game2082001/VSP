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
$scriptUnderTest = Join-RepoPath -Root $repoRoot -Segments @("tools", "orchestrator", "claude-artifact-developer.ps1")
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("ai02-claude-artifact-test-" + [Guid]::NewGuid().ToString("N"))

function Invoke-CheckedGit {
    param(
        [Parameter(Mandatory = $true)][string] $WorkingDirectory,
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )

    $output = & git -C $WorkingDirectory @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed in $WorkingDirectory"
    }
    return $output
}

function Read-TextFileOrEmpty {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ""
    }

    $content = Get-Content -LiteralPath $Path -Raw
    if ($null -eq $content) {
        return ""
    }

    return [string]$content
}

function Write-Fixture {
    param([Parameter(Mandatory = $true)][string] $Root)

    New-Item -ItemType Directory -Force -Path (Join-RepoPath -Root $Root -Segments @("tools", "orchestrator")) | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-RepoPath -Root $Root -Segments @("AI", "Orchestrator", "Manifests")) | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-RepoPath -Root $Root -Segments @("AI", "Orchestrator", "State")) | Out-Null
    Copy-Item -LiteralPath $scriptUnderTest -Destination (Join-RepoPath -Root $Root -Segments @("tools", "orchestrator", "claude-artifact-developer.ps1"))

    $manifest = [ordered]@{
        schemaVersion = "1.0"
        taskId = "VSP-AI02-001TI-B1-SMOKE"
        title = "Claude Developer Smoke Fixture"
        classification = "MEDIUM"
        repository = "game2082001/VSP"
        baseBranch = "main"
        approvedScope = @("Create exactly one harmless AI02 smoke output file.")
        outOfScope = @("Any other repository modification.")
        primaryDeveloper = [ordered]@{
            role = "Claude Code Primary Developer"
            adapter = "claude"
        }
        independentReviewer = [ordered]@{
            required = $true
            adapter = "codex"
        }
        claudeCrossReview = [ordered]@{
            required = $false
        }
        productOwnerAuthorization = [ordered]@{
            authorized = $true
        }
        executionAuthorization = [ordered]@{
            implementation = $true
            pushFeatureBranch = $false
            openOrUpdatePr = $false
        }
        repositoryTransport = [ordered]@{
            required = $true
            approvedFiles = @("AI/Orchestrator/Smoke/VSP-AI02-001TI-B1.claude-developer-smoke.txt")
        }
        smokeFixture = [ordered]@{
            infrastructureSmoke = $true
            approvedOutputPath = "AI/Orchestrator/Smoke/VSP-AI02-001TI-B1.claude-developer-smoke.txt"
            expectedContentMarkers = @(
                "Task: VSP-AI02-001TI-B1-SMOKE",
                "AI02 Claude Artifact Developer smoke",
                "No product behavior change"
            )
        }
    }

    $state = [ordered]@{
        schemaVersion = "1.0"
        taskId = "VSP-AI02-001TI-B1-SMOKE"
        taskManifestStatus = "VALID"
        classification = "MEDIUM"
        classificationConsistencyStatus = "VALID"
        repository = "game2082001/VSP"
        primaryDeveloperRole = "Claude Code Primary Developer"
        primaryDeveloperAdapter = "claude"
        independentReviewerRole = "Separate Codex Independent Reviewer"
        developerEqualsReviewer = $false
        claudeCrossReviewRequired = $false
        implementationContextId = ""
        productOwnerAuthorizationEvidence = [ordered]@{
            authorized = $true
        }
        repositoryTransport = [ordered]@{
            approvedFiles = @("AI/Orchestrator/Smoke/VSP-AI02-001TI-B1.claude-developer-smoke.txt")
        }
    }

    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-RepoPath -Root $Root -Segments @("AI", "Orchestrator", "Manifests", "VSP-AI02-001TI-B1-SMOKE.manifest.json")) -Encoding utf8
    $state | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-RepoPath -Root $Root -Segments @("AI", "Orchestrator", "State", "VSP-AI02-001TI-B1-SMOKE.state.json")) -Encoding utf8

    Push-Location $Root
    try {
        git init | Out-Null
        git config user.email "ai02-test@example.invalid" | Out-Null
        git config user.name "AI02 Test" | Out-Null
        git add . | Out-Null
        git commit -m "baseline" | Out-Null
        return (git rev-parse HEAD).Trim()
    } finally {
        Pop-Location
    }
}

function Invoke-Developer {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $ExpectedBaseSha,
        [string] $Mode = "Package"
    )

    Push-Location $Root
    $previousGitConfigGlobal = $env:GIT_CONFIG_GLOBAL
    $previousGitConfigCount = $env:GIT_CONFIG_COUNT
    $previousGitConfigKey0 = $env:GIT_CONFIG_KEY_0
    $previousGitConfigValue0 = $env:GIT_CONFIG_VALUE_0
    try {
        $output = Join-Path ([IO.Path]::GetTempPath()) ("ai02-claude-artifact-output-" + [Guid]::NewGuid().ToString("N"))
        $gitConfigGlobal = Join-Path $output "empty-gitconfig"
        $gitExcludeFile = Join-Path $output "empty-gitignore"
        New-Item -ItemType Directory -Force -Path $output | Out-Null
        Set-Content -LiteralPath $gitConfigGlobal -Value "" -Encoding utf8
        Set-Content -LiteralPath $gitExcludeFile -Value "" -Encoding utf8
        $env:GIT_CONFIG_GLOBAL = $gitConfigGlobal
        $env:GIT_CONFIG_COUNT = "1"
        $env:GIT_CONFIG_KEY_0 = "core.excludesFile"
        $env:GIT_CONFIG_VALUE_0 = $gitExcludeFile
        $stdoutPath = Join-Path $output "stdout.txt"
        $stderrPath = Join-Path $output "stderr.txt"
        $scriptArgs = @(
            "-NoProfile",
            "-File",
            "tools/orchestrator/claude-artifact-developer.ps1",
            "-ManifestPath",
            "AI/Orchestrator/Manifests/VSP-AI02-001TI-B1-SMOKE.manifest.json",
            "-StatePath",
            "AI/Orchestrator/State/VSP-AI02-001TI-B1-SMOKE.state.json",
            "-ExpectedBaseSha",
            $ExpectedBaseSha,
            "-OutputDirectory",
            $output
        )
        if ($Mode -eq "PreparePrompt") {
            $scriptArgs += "-PreparePrompt"
            $process = Start-Process -FilePath "pwsh" -ArgumentList $scriptArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
            $stdout = (Read-TextFileOrEmpty -Path $stdoutPath).Trim()
            $combined = ($stdout + "`n" + (Read-TextFileOrEmpty -Path $stderrPath)).Trim()
            if ($process.ExitCode -ne 0) {
                throw "claude-artifact-developer.ps1 PreparePrompt failed. Output: $combined"
            }
            return $stdout
        }

        $scriptArgs += "-Package"
        $process = Start-Process -FilePath "pwsh" -ArgumentList $scriptArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
        $stdout = (Read-TextFileOrEmpty -Path $stdoutPath).Trim()
        $combined = ($stdout + "`n" + (Read-TextFileOrEmpty -Path $stderrPath)).Trim()
        if ($process.ExitCode -ne 0) {
            throw "claude-artifact-developer.ps1 Package failed. Output: $combined"
        }
        return $stdout
    } finally {
        $env:GIT_CONFIG_GLOBAL = $previousGitConfigGlobal
        $env:GIT_CONFIG_COUNT = $previousGitConfigCount
        $env:GIT_CONFIG_KEY_0 = $previousGitConfigKey0
        $env:GIT_CONFIG_VALUE_0 = $previousGitConfigValue0
        Pop-Location
    }
}

function Assert-Fails {
    param(
        [Parameter(Mandatory = $true)][scriptblock] $Script,
        [Parameter(Mandatory = $true)][string] $Name,
        [string] $ExpectedMessage
    )

    try {
        & $Script | Out-Null
    } catch {
        if (-not [string]::IsNullOrWhiteSpace($ExpectedMessage) -and -not $_.Exception.Message.Contains($ExpectedMessage)) {
            throw "Failure '$Name' did not contain expected message '$ExpectedMessage'. Actual: $($_.Exception.Message)"
        }
        return
    }

    throw "Expected failure did not occur: $Name"
}

try {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    $base = Write-Fixture -Root $tempRoot

    $promptJson = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode PreparePrompt
    $promptPath = ($promptJson | ConvertFrom-Json).promptPath
    $promptText = Get-Content -LiteralPath $promptPath -Raw
    foreach ($required in @(
        "MANDATORY OUTPUT:",
        "NO SUBSTITUTE:",
        "Do not declare completion until",
        "AI/Orchestrator/Smoke/VSP-AI02-001TI-B1.claude-developer-smoke.txt",
        "Task: VSP-AI02-001TI-B1-SMOKE",
        "Any other repository modification."
    )) {
        if (-not $promptText.Contains($required)) {
            throw "Prompt contract missing required text: $required"
        }
    }

    Assert-Fails -Name "zero changed files after Claude" -ExpectedMessage "changed files do not exactly match approved publication files" -Script { Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base }

    New-Item -ItemType Directory -Force -Path (Join-RepoPath -Root $tempRoot -Segments @("AI", "Orchestrator", "Smoke")) | Out-Null
    $smokeOutput = Join-RepoPath -Root $tempRoot -Segments @("AI", "Orchestrator", "Smoke", "VSP-AI02-001TI-B1.claude-developer-smoke.txt")
    Set-Content -LiteralPath $smokeOutput -Value "Task: VSP-AI02-001TI-B1-SMOKE`nAI02 Claude Artifact Developer smoke`nNo product behavior change`nB3 exact output validation`n" -Encoding utf8
    Set-Content -LiteralPath (Join-Path $tempRoot "extra.txt") -Value "extra" -Encoding utf8
    Assert-Fails -Name "extra changed file" -ExpectedMessage "changed files do not exactly match approved publication files" -Script { Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base }
    Remove-Item -LiteralPath (Join-Path $tempRoot "extra.txt") -Force

    git -C $tempRoot add "AI/Orchestrator/Smoke/VSP-AI02-001TI-B1.claude-developer-smoke.txt" | Out-Null
    git -C $tempRoot commit -m "track approved smoke output" | Out-Null
    $baseWithTrackedOutput = (git -C $tempRoot rev-parse HEAD).Trim()
    Remove-Item -LiteralPath $smokeOutput -Force
    Assert-Fails -Name "missing required file" -ExpectedMessage "changed path is not a regular file" -Script { Invoke-Developer -Root $tempRoot -ExpectedBaseSha $baseWithTrackedOutput }

    Set-Content -LiteralPath $smokeOutput -Value "" -Encoding utf8
    Assert-Fails -Name "empty required file" -ExpectedMessage "approved output file is empty" -Script { Invoke-Developer -Root $tempRoot -ExpectedBaseSha $baseWithTrackedOutput }

    Set-Content -LiteralPath $smokeOutput -Value "Task: VSP-AI02-001TI-B1-SMOKE`nAI02 Claude Artifact Developer smoke`n" -Encoding utf8
    Assert-Fails -Name "missing expected content marker" -ExpectedMessage "expected content marker" -Script { Invoke-Developer -Root $tempRoot -ExpectedBaseSha $baseWithTrackedOutput }

    Set-Content -LiteralPath $smokeOutput -Value "Task: VSP-AI02-001TI-B1-SMOKE`nAI02 Claude Artifact Developer smoke`nNo product behavior change`n" -Encoding utf8
    $result = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $baseWithTrackedOutput | ConvertFrom-Json
    if ($result.changedFiles.Count -ne 1 -or $result.changedFiles[0].path -ne "AI/Orchestrator/Smoke/VSP-AI02-001TI-B1.claude-developer-smoke.txt") {
        throw "Exact required file only case did not produce expected package result."
    }
    if ($result.repositoryWriteCredentialAvailableToDeveloper -ne $false -or $result.productOwnerManualTransport -ne $false) {
        throw "Credential/manual transport boundary changed unexpectedly."
    }
    if (-not (Test-Path -LiteralPath $result.packagePath) -or -not (Test-Path -LiteralPath $result.manifestPath)) {
        throw "Package artifacts were not created for exact required file only case."
    }

    [pscustomobject]@{
        status = "PASS"
        promptContract = "PASS"
        zeroChangedFilesFailClosed = "PASS"
        extraChangedFileFailClosed = "PASS"
        missingRequiredFileFailClosed = "PASS"
        emptyRequiredFileFailClosed = "PASS"
        missingContentMarkerFailClosed = "PASS"
        exactRequiredFileOnly = "PASS"
        repositoryWriteCredentialBoundary = "UNCHANGED"
        artifactPipeline = "UNCHANGED"
    } | ConvertTo-Json -Depth 4
} finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
