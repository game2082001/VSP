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
        [string] $Mode = "Package",
        [string] $ClaudeExecutionFile = "/tmp/claude-execution.json"
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
        if ($Mode -eq "DiagnosePostClaude") {
            $scriptArgs += @(
                "-DiagnosePostClaude",
                "-ClaudeConclusion",
                "success",
                "-ClaudeSessionId",
                "test-session",
                "-ClaudeExecutionFile",
                $ClaudeExecutionFile,
                "-ClaudePermissionDenialCount",
                "6"
            )
            $process = Start-Process -FilePath "pwsh" -ArgumentList $scriptArgs -NoNewWindow -Wait -PassThru -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
            $stdout = (Read-TextFileOrEmpty -Path $stdoutPath).Trim()
            $combined = ($stdout + "`n" + (Read-TextFileOrEmpty -Path $stderrPath)).Trim()
            if ($process.ExitCode -ne 0) {
                throw "claude-artifact-developer.ps1 DiagnosePostClaude failed. Output: $combined"
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

    $absentDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude | ConvertFrom-Json
    if ($absentDiagnostics.authorizedOutputFile.exists -ne $false) {
        throw "Diagnostics did not report the authorized file as absent."
    }
    if ($absentDiagnostics.secretBoundary.environmentDumped -ne $false -or
        $absentDiagnostics.secretBoundary.fileContentsLogged -ne $false -or
        $absentDiagnostics.secretBoundary.fullClaudeTranscriptLogged -ne $false) {
        throw "Diagnostics secret boundary changed unexpectedly."
    }
    if ($absentDiagnostics.approvedBasenameSearch.scope -ne "repository-root") {
        throw "Diagnostics search scope is not repository-root."
    }
    if ($absentDiagnostics.claude.sessionId -ne "test-session" -or $absentDiagnostics.claude.permissionDenialCount -ne "6") {
        throw "Diagnostics did not preserve Claude action identity metadata."
    }
    if ($absentDiagnostics.sanitizedClaudeExecution.rawExecutionOutputUploaded -ne $false) {
        throw "Sanitized Claude execution diagnostics unexpectedly marked raw output as uploaded."
    }

    $safeExecutionFile = Join-Path $tempRoot "claude-execution-safe.json"
    @(
        '{"type":"tool_use","name":"Write","status":"success"}',
        '{"type":"tool_result","tool_name":"Bash","status":"permission_denied","reason":"Permission denied by policy"}',
        '{"type":"result","subtype":"success","num_turns":3}'
    ) | Set-Content -LiteralPath $safeExecutionFile -Encoding utf8
    $safeExecutionDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $safeExecutionFile | ConvertFrom-Json
    if (($safeExecutionDiagnostics.sanitizedClaudeExecution.toolNames | Where-Object { $_ -eq "Write" }).Count -ne 1) {
        throw "Sanitized diagnostics did not capture safe Write tool name."
    }
    if (($safeExecutionDiagnostics.sanitizedClaudeExecution.deniedTools | Where-Object { $_ -eq "Bash" }).Count -ne 1) {
        throw "Sanitized diagnostics did not capture denied Bash tool."
    }
    if ($safeExecutionDiagnostics.sanitizedClaudeExecution.writeAttempted -ne $true -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.bashAttempted -ne $true -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.finalResultSubtype -ne "success" -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.claudeTurnCount -ne "3") {
        throw "Sanitized diagnostics did not capture expected execution summary fields."
    }
    if (($safeExecutionDiagnostics | ConvertTo-Json -Depth 20).Contains("Permission denied by policy") -ne $true) {
        throw "Sanitized diagnostics did not preserve safe denial reason."
    }
    Remove-Item -LiteralPath $safeExecutionFile -Force

    $sensitiveExecutionFile = Join-Path $tempRoot "claude-execution-sensitive.json"
    @(
        '{"type":"tool_use","name":"Edit","status":"success","input":"secret repository file contents"}',
        '{"type":"tool_result","tool_name":"Write","status":"permission_denied","reason":"token ghp_abcdefghijklmnopqrstuvwxyz1234567890 leaked"}'
    ) | Set-Content -LiteralPath $sensitiveExecutionFile -Encoding utf8
    $sensitiveExecutionDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $sensitiveExecutionFile | ConvertFrom-Json
    $sensitiveJson = $sensitiveExecutionDiagnostics | ConvertTo-Json -Depth 20
    if ($sensitiveJson.Contains("secret repository file contents") -or $sensitiveJson.Contains("ghp_abcdefghijklmnopqrstuvwxyz1234567890")) {
        throw "Sanitized diagnostics leaked sensitive tool input or denial reason."
    }
    if (($sensitiveExecutionDiagnostics.sanitizedClaudeExecution.sanitizedDenialReasons | Where-Object { $_ -eq "REDACTED" }).Count -lt 1) {
        throw "Sanitized diagnostics did not redact sensitive denial reason."
    }
    Remove-Item -LiteralPath $sensitiveExecutionFile -Force

    $unknownExecutionFile = Join-Path $tempRoot "claude-execution-unknown.json"
    Set-Content -LiteralPath $unknownExecutionFile -Value "{not-json" -Encoding utf8
    $unknownExecutionDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $unknownExecutionFile | ConvertFrom-Json
    if ($unknownExecutionDiagnostics.sanitizedClaudeExecution.finalResultSubtype -ne "UNKNOWN" -or
        $unknownExecutionDiagnostics.sanitizedClaudeExecution.toolNames.Count -ne 0) {
        throw "Unknown Claude execution schema did not fail safely."
    }

    $diagnosticOutputDirectory = Split-Path -Parent ([string]$unknownExecutionDiagnostics.claude.executionFile)
    if (Test-Path -LiteralPath (Join-Path $diagnosticOutputDirectory "claude-execution-unknown.json")) {
        # The source file can exist in temp during the run, but the diagnostic artifact must not be renamed to a raw execution output.
        if (Test-Path -LiteralPath (Join-Path $diagnosticOutputDirectory "post-claude-diagnostics.sanitized.json") -PathType Leaf) {
            $sanitizedArtifactJson = Get-Content -LiteralPath (Join-Path $diagnosticOutputDirectory "post-claude-diagnostics.sanitized.json") -Raw
            if ($sanitizedArtifactJson.Contains("{not-json")) {
                throw "Sanitized diagnostic artifact contains raw execution output."
            }
        }
    }
    Remove-Item -LiteralPath $unknownExecutionFile -Force

    New-Item -ItemType Directory -Force -Path (Join-RepoPath -Root $tempRoot -Segments @("AI", "Orchestrator", "Wrong")) | Out-Null
    $wrongPath = Join-RepoPath -Root $tempRoot -Segments @("AI", "Orchestrator", "Wrong", "VSP-AI02-001TI-B1.claude-developer-smoke.txt")
    Set-Content -LiteralPath $wrongPath -Value "Task: VSP-AI02-001TI-B1-SMOKE`nAI02 Claude Artifact Developer smoke`nNo product behavior change`n" -Encoding utf8
    $wrongPathDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude | ConvertFrom-Json
    if ($wrongPathDiagnostics.authorizedOutputFile.exists -ne $false) {
        throw "Diagnostics incorrectly treated wrong-path smoke file as authorized output."
    }
    if (($wrongPathDiagnostics.approvedBasenameSearch.matches | Where-Object { $_.path -eq "AI/Orchestrator/Wrong/VSP-AI02-001TI-B1.claude-developer-smoke.txt" }).Count -ne 1) {
        throw "Diagnostics did not find wrong-path approved basename under repository root."
    }
    if (($wrongPathDiagnostics.gitUntrackedFiles | Where-Object { $_ -eq "AI/Orchestrator/Wrong/VSP-AI02-001TI-B1.claude-developer-smoke.txt" }).Count -ne 1) {
        throw "Diagnostics did not report wrong-path untracked file."
    }
    Remove-Item -LiteralPath $wrongPath -Force

    Set-Content -LiteralPath (Join-Path $tempRoot ".gitignore") -Value "ignored-smoke.txt`n" -Encoding utf8
    Set-Content -LiteralPath (Join-Path $tempRoot "ignored-smoke.txt") -Value "ignored" -Encoding utf8
    $ignoredDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude | ConvertFrom-Json
    if (($ignoredDiagnostics.gitIgnoredFiles | Where-Object { $_ -eq "ignored-smoke.txt" }).Count -ne 1) {
        throw "Diagnostics did not distinguish ignored files."
    }
    Remove-Item -LiteralPath (Join-Path $tempRoot ".gitignore") -Force
    Remove-Item -LiteralPath (Join-Path $tempRoot "ignored-smoke.txt") -Force

    Assert-Fails -Name "zero changed files after Claude" -ExpectedMessage "changed files do not exactly match approved publication files" -Script { Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base }

    New-Item -ItemType Directory -Force -Path (Join-RepoPath -Root $tempRoot -Segments @("AI", "Orchestrator", "Smoke")) | Out-Null
    $smokeOutput = Join-RepoPath -Root $tempRoot -Segments @("AI", "Orchestrator", "Smoke", "VSP-AI02-001TI-B1.claude-developer-smoke.txt")
    Set-Content -LiteralPath $smokeOutput -Value "Task: VSP-AI02-001TI-B1-SMOKE`nAI02 Claude Artifact Developer smoke`nNo product behavior change`nB3 exact output validation`n" -Encoding utf8
    $presentDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude | ConvertFrom-Json
    if ($presentDiagnostics.authorizedOutputFile.exists -ne $true -or [string]::IsNullOrWhiteSpace($presentDiagnostics.authorizedOutputFile.sha256)) {
        throw "Diagnostics did not report authorized file existence and hash."
    }
    if (($presentDiagnostics.gitUntrackedFiles | Where-Object { $_ -eq "AI/Orchestrator/Smoke/VSP-AI02-001TI-B1.claude-developer-smoke.txt" }).Count -ne 1) {
        throw "Diagnostics did not report authorized untracked file."
    }

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
        postClaudeDiagnosticsAbsent = "PASS"
        postClaudeDiagnosticsPresent = "PASS"
        postClaudeDiagnosticsWrongPath = "PASS"
        postClaudeDiagnosticsUntracked = "PASS"
        postClaudeDiagnosticsIgnored = "PASS"
        postClaudeDiagnosticsSecretBoundary = "PASS"
        sanitizedClaudeExecutionSafeFixture = "PASS"
        sanitizedClaudeExecutionUnknownSchema = "PASS"
        sanitizedClaudeExecutionRedaction = "PASS"
        rawExecutionFileArtifactRetention = "FALSE"
        exactRequiredFileOnly = "PASS"
        repositoryWriteCredentialBoundary = "UNCHANGED"
        artifactPipeline = "UNCHANGED"
    } | ConvertTo-Json -Depth 4
} finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
