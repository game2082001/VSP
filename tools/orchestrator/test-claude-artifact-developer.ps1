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
$workflowUnderTest = Join-RepoPath -Root $repoRoot -Segments @(".github", "workflows", "ai02-claude-artifact-developer.yml")
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
    $workflowText = Get-Content -LiteralPath $workflowUnderTest -Raw
    if (-not $workflowText.Contains('persist-credentials: false')) {
        throw "Claude artifact developer workflow no longer disables checkout credential persistence."
    }
    if (-not $workflowText.Contains('permissions:') -or -not $workflowText.Contains('contents: read')) {
        throw "Claude artifact developer workflow no longer records contents: read permission."
    }
    if (-not $workflowText.Contains('--allowedTools "Read,Write,Edit"')) {
        throw "Claude artifact developer workflow does not explicitly allow minimum repository-local file tools."
    }
    if ($workflowText -match '--allowedTools\s+"[^"]*Bash') {
        throw "Claude artifact developer workflow must not broadly allow Bash for the smoke file operation."
    }
    foreach ($forbiddenBoundary in @(
        "VSP_AI_APP_PRIVATE_KEY",
        "create-github-app-token",
        "git push",
        "gh pr create",
        "gh pr merge"
    )) {
        if ($workflowText.Contains($forbiddenBoundary) -and
            $forbiddenBoundary -notin @("git push", "gh pr create", "gh pr merge")) {
            throw "Claude artifact developer workflow unexpectedly references repository-write credential boundary: $forbiddenBoundary"
        }
    }
    foreach ($requiredDisallowed in @(
        'Bash(git push:*)',
        'Bash(gh pr create:*)',
        'Bash(gh pr merge:*)',
        'Bash(gh api repos/*/git/refs:*)',
        'Bash(gh api repos/*/pulls:*)'
    )) {
        if (-not $workflowText.Contains($requiredDisallowed)) {
            throw "Claude artifact developer workflow is missing disallowed repository-write command guard: $requiredDisallowed"
        }
    }

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

    # Synthetic representatives of Run 34150617322 successful Write metadata and
    # Run 33653335050 permission-style metadata, without production transcript data.
    $safeExecutionFile = Join-Path $tempRoot "claude-execution-safe.json"
    @(
        '{"type":"assistant","message":{"content":[{"type":"tool_use","name":"Read","input":{"file_path":"must-not-leak.txt"}},{"type":"tool_use","name":"Write","input":{"content":"must-not-leak"}},{"type":"tool_use","name":"Edit","input":{"old_string":"must-not-leak"}},{"type":"tool_use","name":"Bash","input":{"command":"must-not-leak"}}]}}',
        '{"type":"result","subtype":"success","num_turns":3,"permission_denials":[{"tool_name":"Bash","status":"permission_denied","reason":"Approval required by policy"},{"tool_name":"FutureTool","status":"permission_denied","reason":"Tool not allowed"}]}'
    ) | Set-Content -LiteralPath $safeExecutionFile -Encoding utf8
    $safeExecutionDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $safeExecutionFile | ConvertFrom-Json
    foreach ($toolName in @("Read", "Write", "Edit", "Bash")) {
        if (($safeExecutionDiagnostics.sanitizedClaudeExecution.toolNames | Where-Object { $_ -eq $toolName }).Count -ne 1) {
            throw "Sanitized diagnostics did not capture approved tool name: $toolName"
        }
    }
    if (($safeExecutionDiagnostics.sanitizedClaudeExecution.deniedTools | Where-Object { $_ -eq "Bash" }).Count -ne 1) {
        throw "Sanitized diagnostics did not capture denied Bash tool."
    }
    if (($safeExecutionDiagnostics.sanitizedClaudeExecution.deniedTools | Where-Object { $_ -eq "UNKNOWN" }).Count -ne 1) {
        throw "Sanitized diagnostics did not map an unapproved denied tool to UNKNOWN."
    }
    if (($safeExecutionDiagnostics.sanitizedClaudeExecution.denialCategories | Where-Object { $_ -eq "APPROVAL_REQUIRED" }).Count -ne 1 -or
        ($safeExecutionDiagnostics.sanitizedClaudeExecution.denialCategories | Where-Object { $_ -eq "TOOL_NOT_ALLOWED" }).Count -ne 1) {
        throw "Sanitized diagnostics did not map denial reasons to fixed categories."
    }
    if ($safeExecutionDiagnostics.sanitizedClaudeExecution.readAttempted -ne "TRUE" -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.writeAttempted -ne "TRUE" -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.editAttempted -ne "TRUE" -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.bashAttempted -ne "TRUE" -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.finalResultSubtype -ne "success" -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.completionCategory -ne "COMPLETED" -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "COMPLETE" -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.permissionDenialEventsObserved -ne 2 -or
        $safeExecutionDiagnostics.sanitizedClaudeExecution.claudeTurnCount -ne "3") {
        throw "Sanitized diagnostics did not capture expected execution summary fields."
    }
    $safeJson = $safeExecutionDiagnostics | ConvertTo-Json -Depth 20
    foreach ($forbiddenText in @("must-not-leak", "Approval required by policy", "Tool not allowed")) {
        if ($safeJson.Contains($forbiddenText)) {
            throw "Sanitized diagnostics leaked tool payload or raw denial text."
        }
    }
    Remove-Item -LiteralPath $safeExecutionFile -Force

    $noToolExecutionFile = Join-Path $tempRoot "claude-execution-no-tool.json"
    @(
        '{"type":"system","subtype":"init"}',
        '{"type":"result","subtype":"success","num_turns":1}'
    ) | Set-Content -LiteralPath $noToolExecutionFile -Encoding utf8
    $noToolDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $noToolExecutionFile | ConvertFrom-Json
    if ($noToolDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "COMPLETE" -or
        $noToolDiagnostics.sanitizedClaudeExecution.readAttempted -ne "FALSE" -or
        $noToolDiagnostics.sanitizedClaudeExecution.writeAttempted -ne "FALSE" -or
        $noToolDiagnostics.sanitizedClaudeExecution.editAttempted -ne "FALSE" -or
        $noToolDiagnostics.sanitizedClaudeExecution.bashAttempted -ne "FALSE") {
        throw "Complete no-tool evidence did not produce authoritative FALSE attribution."
    }
    Remove-Item -LiteralPath $noToolExecutionFile -Force

    $sensitiveExecutionFile = Join-Path $tempRoot "claude-execution-sensitive.json"
    @(
        '{"type":"tool_use","name":"Edit","status":"success","input":"secret repository file contents","prompt":"must-not-leak-prompt","content":"must-not-leak-content","text":"must-not-leak-text","transcript":"must-not-leak-transcript","stdout":"must-not-leak-stdout","stderr":"must-not-leak-stderr","environment":{"TOKEN":"must-not-leak-token"}}',
        '{"type":"tool_result","tool_name":"Write","status":"permission_denied","reason":"token ghp_abcdefghijklmnopqrstuvwxyz1234567890 leaked","output":{"command":"must-not-leak-command","contents":"must-not-leak-output"}}'
    ) | Set-Content -LiteralPath $sensitiveExecutionFile -Encoding utf8
    $sensitiveExecutionDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $sensitiveExecutionFile | ConvertFrom-Json
    $sensitiveJson = $sensitiveExecutionDiagnostics | ConvertTo-Json -Depth 20
    foreach ($forbiddenText in @("secret repository file contents", "ghp_abcdefghijklmnopqrstuvwxyz1234567890", "must-not-leak")) {
        if ($sensitiveJson.Contains($forbiddenText)) {
            throw "Sanitized diagnostics leaked a prohibited execution field or denial reason."
        }
    }
    if (($sensitiveExecutionDiagnostics.sanitizedClaudeExecution.denialCategories | Where-Object { $_ -eq "PERMISSION_DENIED" }).Count -ne 1) {
        throw "Sanitized diagnostics did not classify a sensitive permission denial safely."
    }
    Remove-Item -LiteralPath $sensitiveExecutionFile -Force

    $unknownExecutionFile = Join-Path $tempRoot "claude-execution-unknown.json"
    Set-Content -LiteralPath $unknownExecutionFile -Value "{not-json" -Encoding utf8
    $unknownExecutionDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $unknownExecutionFile | ConvertFrom-Json
    if ($unknownExecutionDiagnostics.sanitizedClaudeExecution.finalResultSubtype -ne "UNKNOWN" -or
        $unknownExecutionDiagnostics.sanitizedClaudeExecution.toolNames.Count -ne 0 -or
        $unknownExecutionDiagnostics.sanitizedClaudeExecution.writeAttempted -ne "UNKNOWN" -or
        $unknownExecutionDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "UNKNOWN_SCHEMA") {
        throw "Unknown Claude execution schema did not fail safely."
    }
    if ($unknownExecutionDiagnostics.sanitizedClaudeExecution.parseStatus -ne "UNKNOWN_SCHEMA") {
        throw "Unknown Claude execution schema should report UNKNOWN_SCHEMA."
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

    $futureSchemaExecutionFile = Join-Path $tempRoot "claude-execution-future-schema.json"
    '{"type":"future_event_v9","payload":{"tool":{"name":"Write","input":"must-not-leak-future-input"},"free_form_result":"must-not-leak-future-result"}}' | Set-Content -LiteralPath $futureSchemaExecutionFile -Encoding utf8
    $futureSchemaDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $futureSchemaExecutionFile | ConvertFrom-Json
    $futureSchemaJson = $futureSchemaDiagnostics | ConvertTo-Json -Depth 20
    if ($futureSchemaDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "UNKNOWN_SCHEMA" -or
        $futureSchemaDiagnostics.sanitizedClaudeExecution.writeAttempted -ne "UNKNOWN" -or
        $futureSchemaJson.Contains("must-not-leak")) {
        throw "Arbitrary future Claude execution schema did not return UNKNOWN safely."
    }
    Remove-Item -LiteralPath $futureSchemaExecutionFile -Force

    $mixedSchemaExecutionFile = Join-Path $tempRoot "claude-execution-mixed-schema.json"
    '{"type":"result","subtype":"success","future_payload":{"operation":{"toolName":"Write","input":"must-not-leak-mixed-input"}}}' | Set-Content -LiteralPath $mixedSchemaExecutionFile -Encoding utf8
    $mixedSchemaDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $mixedSchemaExecutionFile | ConvertFrom-Json
    $mixedSchemaJson = $mixedSchemaDiagnostics | ConvertTo-Json -Depth 20
    if ($mixedSchemaDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "UNKNOWN_SCHEMA" -or
        $mixedSchemaDiagnostics.sanitizedClaudeExecution.writeAttempted -ne "UNKNOWN" -or
        $mixedSchemaJson.Contains("must-not-leak")) {
        throw "Mixed known/future Claude schema produced an unsafe authoritative negative attribution."
    }
    Remove-Item -LiteralPath $mixedSchemaExecutionFile -Force

    $fastPathNodeLimitExecutionFile = Join-Path $tempRoot "claude-execution-fast-path-node-limit.json"
    $fastPathLines = @()
    for ($outer = 0; $outer -lt 20; $outer++) {
        $blocks = @()
        for ($inner = 0; $inner -lt 200; $inner++) {
            $blocks += [ordered]@{ type = "tool_use"; name = "Read"; input = "must-not-leak-fast-path" }
        }
        $fastPathLines += ([ordered]@{ type = "assistant"; message = [ordered]@{ content = $blocks } } | ConvertTo-Json -Compress -Depth 8)
    }
    $fastPathLines | Set-Content -LiteralPath $fastPathNodeLimitExecutionFile -Encoding utf8
    $fastPathNodeLimitDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $fastPathNodeLimitExecutionFile | ConvertFrom-Json
    if ($fastPathNodeLimitDiagnostics.sanitizedClaudeExecution.maxNodesReached -ne $true -or
        $fastPathNodeLimitDiagnostics.sanitizedClaudeExecution.inspectedNodeCount -gt 2000 -or
        $fastPathNodeLimitDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "PARTIAL_BOUNDS_REACHED" -or
        $fastPathNodeLimitDiagnostics.sanitizedClaudeExecution.writeAttempted -ne "UNKNOWN") {
        throw "Schema-aware fast path did not enforce the shared MaxNodes=2000 ceiling."
    }
    if (($fastPathNodeLimitDiagnostics | ConvertTo-Json -Depth 20).Contains("must-not-leak")) {
        throw "Schema-aware fast path leaked bounded tool input."
    }
    Remove-Item -LiteralPath $fastPathNodeLimitExecutionFile -Force

    # Synthetic representative of Run 34238805895: denials are attributable, while
    # bounded unknown metadata prevents authoritative negative tool attribution.
    $noOutputDenialExecutionFile = Join-Path $tempRoot "claude-execution-no-output-denial.json"
    $noOutputDenialNode = [ordered]@{
        type = "result"
        subtype = "permission_denied"
        permission_denials = @(
            [ordered]@{ tool_name = "Write"; status = "permission_denied"; reason = "Approval required" },
            [ordered]@{ tool_name = "FutureTool"; status = "permission_denied"; reason = "Denied" }
        )
        metadata = $null
    }
    $cursor = $null
    for ($i = 0; $i -lt 20; $i++) {
        $next = [ordered]@{ child = $null }
        if ($null -eq $cursor) {
            $noOutputDenialNode.metadata = $next
        } else {
            $cursor.child = $next
        }
        $cursor = $next
    }
    $noOutputDenialNode | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $noOutputDenialExecutionFile -Encoding utf8
    $noOutputDenialDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $noOutputDenialExecutionFile | ConvertFrom-Json
    if ($noOutputDenialDiagnostics.sanitizedClaudeExecution.permissionDenialEventsObserved -ne 2 -or
        ($noOutputDenialDiagnostics.sanitizedClaudeExecution.deniedTools | Where-Object { $_ -eq "Write" }).Count -ne 1 -or
        $noOutputDenialDiagnostics.sanitizedClaudeExecution.completionCategory -ne "PERMISSION_BLOCKED" -or
        $noOutputDenialDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "PARTIAL_BOUNDS_REACHED" -or
        $noOutputDenialDiagnostics.sanitizedClaudeExecution.editAttempted -ne "UNKNOWN") {
        throw "No-output/denial-style evidence was not safely attributable under incomplete bounds: $($noOutputDenialDiagnostics.sanitizedClaudeExecution | ConvertTo-Json -Compress -Depth 10)"
    }
    Remove-Item -LiteralPath $noOutputDenialExecutionFile -Force

    # Synthetic representative of the Run 33416979294 recursion-style shape.
    $deepExecutionFile = Join-Path $tempRoot "claude-execution-deep.json"
    $deepNode = [ordered]@{
        type = "tool_use"
        name = "Write"
        status = "success"
        child = $null
    }
    $cursor = $deepNode
    for ($i = 0; $i -lt 40; $i++) {
        $next = [ordered]@{
            nested = $i
            child = $null
        }
        $cursor.child = $next
        $cursor = $next
    }
    $deepNode | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $deepExecutionFile -Encoding utf8
    $deepDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $deepExecutionFile | ConvertFrom-Json
    if ($deepDiagnostics.sanitizedClaudeExecution.maxDepthReached -ne $true) {
        throw "Deep Claude execution diagnostics did not report the max-depth boundary."
    }
    if (($deepDiagnostics.sanitizedClaudeExecution.toolNames | Where-Object { $_ -eq "Write" }).Count -ne 1) {
        throw "Deep Claude execution diagnostics failed to extract known root tool metadata."
    }
    if ($deepDiagnostics.sanitizedClaudeExecution.writeAttempted -ne "TRUE" -or
        $deepDiagnostics.sanitizedClaudeExecution.editAttempted -ne "UNKNOWN" -or
        $deepDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "PARTIAL_BOUNDS_REACHED") {
        throw "Depth-bounded evidence did not preserve observed TRUE and unavailable UNKNOWN attribution."
    }
    Remove-Item -LiteralPath $deepExecutionFile -Force

    $largeArrayExecutionFile = Join-Path $tempRoot "claude-execution-large-array.json"
    $largeItems = @()
    for ($i = 0; $i -lt 450; $i++) {
        $largeItems += [ordered]@{
            type = "tool_result"
            tool_name = "Bash"
            status = "permission_denied"
            reason = "Permission denied by policy"
        }
    }
    $largeItems | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $largeArrayExecutionFile -Encoding utf8
    $largeArrayDiagnostics = Invoke-Developer -Root $tempRoot -ExpectedBaseSha $base -Mode DiagnosePostClaude -ClaudeExecutionFile $largeArrayExecutionFile | ConvertFrom-Json
    if ($largeArrayDiagnostics.sanitizedClaudeExecution.maxArrayItemsReached -ne $true -or
        $largeArrayDiagnostics.sanitizedClaudeExecution.inspectedNodeCount -gt 2000) {
        throw "Large Claude execution diagnostics did not enforce bounded inspection."
    }
    if (($largeArrayDiagnostics.sanitizedClaudeExecution.deniedTools | Where-Object { $_ -eq "Bash" }).Count -ne 1) {
        throw "Large Claude execution diagnostics failed to extract denied tool metadata before bounding."
    }
    if ($largeArrayDiagnostics.sanitizedClaudeExecution.evidenceCompleteness -ne "PARTIAL_BOUNDS_REACHED" -or
        $largeArrayDiagnostics.sanitizedClaudeExecution.writeAttempted -ne "UNKNOWN") {
        throw "Array-bounded evidence did not return partial completeness and UNKNOWN absent-tool attribution."
    }
    if ($largeArrayDiagnostics.sanitizedClaudeExecution.inspectionLimits.maxDepth -ne 8 -or
        $largeArrayDiagnostics.sanitizedClaudeExecution.inspectionLimits.maxNodes -ne 2000 -or
        $largeArrayDiagnostics.sanitizedClaudeExecution.inspectionLimits.maxArrayItems -ne 200) {
        throw "Sanitized diagnostic safety ceilings changed unexpectedly."
    }
    Remove-Item -LiteralPath $largeArrayExecutionFile -Force

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

    foreach ($requiredWorkflowText in @(
        "Prepare deterministic A2-R1 skeleton and credentialless harness",
        "inputs.task_id == 'VSP-AI02-001TI-A2-R1'",
        "-Mode Prepare",
        "-Mode ValidateCompletion",
        "A2-R1 completion guard did not pass"
    )) {
        if (-not $workflowText.Contains($requiredWorkflowText)) {
            throw "Workflow is missing gated A2-R1 infrastructure text: $requiredWorkflowText"
        }
    }

    $a2Root = Join-Path ([IO.Path]::GetTempPath()) ("ai02-a2-r1-prompt-test-" + [Guid]::NewGuid().ToString("N"))
    try {
        New-Item -ItemType Directory -Force -Path (Join-RepoPath $a2Root @("tools", "orchestrator")) | Out-Null
        New-Item -ItemType Directory -Force -Path (Join-RepoPath $a2Root @("AI", "Orchestrator", "Manifests")) | Out-Null
        New-Item -ItemType Directory -Force -Path (Join-RepoPath $a2Root @("AI", "Orchestrator", "State")) | Out-Null
        Copy-Item -LiteralPath $scriptUnderTest -Destination (Join-RepoPath $a2Root @("tools", "orchestrator", "claude-artifact-developer.ps1"))
        $a2Manifest = [ordered]@{
            taskId = "VSP-AI02-001TI-A2-R1"; classification = "CRITICAL"; repository = "game2082001/VSP"; approvedScope = @("Implement the final A2 remediation."); outOfScope = @("Everything outside A2."); stopConditions = @("Stop on scope drift.")
            primaryDeveloper = [ordered]@{ role="Claude Code Primary Developer"; adapter="claude" }
            independentReviewer = [ordered]@{ required=$true; adapter="codex" }
            claudeCrossReview = [ordered]@{ required=$true }
            productOwnerAuthorization = [ordered]@{ authorized=$true }
            executionAuthorization = [ordered]@{ implementation=$true; pushFeatureBranch=$false; openOrUpdatePr=$false }
            repositoryTransport = [ordered]@{ required=$true; approvedFiles=@("tools/orchestrator/artifact-intake-contract.ps1") }
            remediationInfrastructure = [ordered]@{
                architecture="A2_RECOVERY_OPTION_C_MECHANICAL_SKELETON_PLUS_CLAUDE"; outputPath="tools/orchestrator/artifact-intake-contract.ps1"; sentinel="NOT_IMPLEMENTED"; claudeAllowedTools=@("Read","Write","Edit")
                harness=[ordered]@{minimumSemanticCaseCount=52}
                validatorInvocationInterface=[ordered]@{parameters=@("PolicyPath","PolicySchemaPath","RequestSchemaPath","DecisionSchemaPath","TrustedContextFixtureRoot","OutputEvidencePath")}
                completionGuard=@("FINAL_SHA_DIFFERS_FROM_SKELETON","SEMANTIC_MATRIX_PASS")
            }
            trustedContext=[ordered]@{producerClaimsAuthoritative=$false;repositoryGovernanceAuthority=@("repository","taskId","sourceSha")}
            pathResponsibility=[ordered]@{a2=@("DOT_DOT_SEGMENT","BACKSLASH","COLON")}
            failureMapping=@([ordered]@{findingCode="REQUEST_REPLAY_DETECTED";canonicalCategory="REPLAY_DETECTED"})
            replayAndStaleBase=[ordered]@{emptyConsumedIdentities="FIRST_USE_NOT_YET_CONSUMED";matchingConsumedIdentity="REJECT_REPLAY";staleBasePolicy="EXACT_BASE_ONLY"}
            decisionStateMachine=[ordered]@{transportInvokedGlobally=$false;acceptedForTransport=[ordered]@{transportAuthorized=$true;transportInvoked=$false}}
            evidenceContract=[ordered]@{resultValues=@("PASS","REJECT");excluded=@("credentials","rawExceptionText")}
        }
        $a2State = [ordered]@{
            taskId="VSP-AI02-001TI-A2-R1"; taskManifestStatus="VALID"; classification="CRITICAL"; classificationConsistencyStatus="VALID"; repository="game2082001/VSP"
            primaryDeveloperRole="Claude Code Primary Developer"; primaryDeveloperAdapter="claude"; developerEqualsReviewer=$false; claudeCrossReviewRequired=$true
            productOwnerAuthorizationEvidence=[ordered]@{authorized=$true}
        }
        $a2ManifestPath = Join-RepoPath $a2Root @("AI","Orchestrator","Manifests","VSP-AI02-001TI-A2-R1.manifest.json")
        $a2StatePath = Join-RepoPath $a2Root @("AI","Orchestrator","State","VSP-AI02-001TI-A2-R1.state.json")
        $a2Manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $a2ManifestPath -Encoding utf8NoBOM
        $a2State | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $a2StatePath -Encoding utf8NoBOM
        Push-Location $a2Root
        try {
            git init | Out-Null; git config user.email "ai02-test@example.invalid"; git config user.name "AI02 Test"; git add .; git commit -m baseline | Out-Null
            $a2Base = (git rev-parse HEAD).Trim()
            $skeletonPath = Join-RepoPath $a2Root @("tools","orchestrator","artifact-intake-contract.ps1")
            Set-Content -LiteralPath $skeletonPath -Value "Set-StrictMode -Version Latest`nthrow 'NOT_IMPLEMENTED'" -Encoding utf8NoBOM
            $a2Output = Join-Path $a2Root "prompt-output"
            $a2PromptJson = & pwsh -NoProfile -File "tools/orchestrator/claude-artifact-developer.ps1" -ManifestPath "AI/Orchestrator/Manifests/VSP-AI02-001TI-A2-R1.manifest.json" -StatePath "AI/Orchestrator/State/VSP-AI02-001TI-A2-R1.state.json" -ExpectedBaseSha $a2Base -OutputDirectory $a2Output -PreparePrompt
            if ($LASTEXITCODE -ne 0) { throw "A2-R1 prompt preparation failed." }
            $a2PromptPath = ($a2PromptJson | ConvertFrom-Json).promptPath
            $a2PromptText = Get-Content -LiteralPath $a2PromptPath -Raw
            foreach ($requiredA2PromptText in @(
                "A2 FINAL REMEDIATION EXECUTION CONTRACT:", "Skeleton SHA-256:", "Replace the skeleton with a substantive implementation", "VALIDATOR SCRIPT INVOCATION INTERFACE:",
                "TRUSTED-CONTEXT AUTHORITY MODEL", "PATH-POLICY RESPONSIBILITY:", "CANONICAL FAILURE MAPPING:", "REPLAY AND EXACT-BASE-ONLY CONTRACT:",
                "DECISION STATE MACHINE:", "SANITIZED DETERMINISTIC EVIDENCE CONTRACT:", "POST-CLAUDE COMPLETION GUARD:", "Read, Write, and Edit are the only allowed", "52-case-or-greater"
            )) {
                if (-not $a2PromptText.Contains($requiredA2PromptText)) { throw "A2-R1 prompt missing: $requiredA2PromptText" }
            }
            $privateKeyMarkerMatches = [regex]::Matches($a2PromptText, [regex]::Escape("VSP_AI_APP_PRIVATE_KEY"))
            if ($privateKeyMarkerMatches.Count -ne 1 -or
                -not $a2PromptText.Contains("Do not access VSP_AI_APP_PRIVATE_KEY, App installation tokens, PATs, or reusable GitHub credentials.")) {
                throw "A2-R1 prompt must mention the private-key marker exactly once and only as an explicit access prohibition."
            }
            foreach ($forbiddenA2PromptText in @("ghp_", "C:\\Users\\")) {
                if ($a2PromptText.Contains($forbiddenA2PromptText)) { throw "A2-R1 prompt leaked prohibited text." }
            }
        } finally { Pop-Location }
    } finally {
        if (Test-Path -LiteralPath $a2Root) { Remove-Item -LiteralPath $a2Root -Recurse -Force }
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
        sanitizedClaudeExecutionMaxDepth = "PASS"
        sanitizedClaudeExecutionMaxNodes = "PASS"
        rawExecutionFileArtifactRetention = "FALSE"
        claudeToolPermissionConfiguration = "MINIMUM_FILE_TOOLS_ONLY"
        broadBashAllowed = "FALSE"
        exactRequiredFileOnly = "PASS"
        repositoryWriteCredentialBoundary = "UNCHANGED"
        artifactPipeline = "UNCHANGED"
        a2R1WorkflowGating = "PASS"
        a2R1SemanticPromptRendering = "PASS"
        a2R1ClaudeAllowedTools = "Read,Write,Edit"
    } | ConvertTo-Json -Depth 4
} finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
