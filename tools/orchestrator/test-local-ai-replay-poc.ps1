param()

$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "invoke-local-ai-replay-poc.ps1"
$json = & pwsh -NoProfile -File $script -ValidateOnly
if ($LASTEXITCODE -ne 0) {
    throw "Local AI replay PoC ValidateOnly failed."
}

$result = $json | ConvertFrom-Json
if ($result.status -ne "PASS") { throw "ValidateOnly status was not PASS." }
if ($result.taskId -ne "VSP-LOCALAI-001B") { throw "Unexpected default task ID." }
if ($result.replayAttemptId -ne "attempt1") { throw "Unexpected default replay attempt ID." }
if ($result.cases -ne 3) { throw "Expected exactly 3 replay cases." }
if ($result.runsPerCase -ne 3) { throw "Expected 3 runs per case." }
if ($result.requestSchemaVersion -ne "1.0") { throw "Unexpected request schema version." }
if ($result.responseSchemaVersion -ne "1.0") { throw "Unexpected response schema version." }
if ($result.structuredOutputMode -ne "ollama-json") { throw "Unexpected default structured output mode." }
if ($result.modelGeneratedFields -ne "full-advisory-response") { throw "Unexpected default model-generated field mode." }
if ($result.defaultPromptContract.requestsFullAdvisoryResponse -ne $true) { throw "Default prompt no longer requests the full advisory response contract." }
if ($result.defaultPromptContract.doesNotUseAnalysisOnlyContract -ne $true) { throw "Default prompt incorrectly uses the structured analysis-only contract." }
if ($result.localAiRepositoryWrite -ne $false) { throw "Local AI repository write boundary changed." }
if ($result.localAiGitHubAuthority -ne $false) { throw "Local AI GitHub authority boundary changed." }
if ($result.livePrGateIntegration -ne $false) { throw "Local AI live PR gate integration must be false." }
if ($result.firewallChanged -ne $false) { throw "Firewall must not be changed." }
if ($result.ollamaModelContextChanged -ne $false) { throw "Ollama model/context must not be changed." }
if ($result.scoringSelfTests.case2EmptyAnalysisWithGovernanceMetadataDetected -ne $false) { throw "CASE2 known-defect scoring is contaminated by governance metadata." }
if ($result.scoringSelfTests.case2GenuineModelAnalysisDetected -ne $true) { throw "CASE2 known-defect scoring did not detect genuine model-authored analysis." }
if ($result.scoringSelfTests.modelAuthoredAuthorityTextDetected -ne $true) { throw "Model-authored authority text was not detected." }
if ($result.scoringSelfTests.unsupportedClaimDetectedFromModelAnalysis -ne $true) { throw "Unsupported claim detection did not inspect model-authored analysis." }
if ($result.scoringSelfTests.defaultFullResponseUnsupportedClaimDetected -ne $true) { throw "Default mode did not inspect the full model-authored response for unsupported claims." }
if ($result.scoringSelfTests.safeGovernanceEnvelopeDoesNotCreateDetection -ne $true) { throw "Safe governance envelope metadata created a false CASE2 detection." }
if ($result.digestSelfTests.identicalAnalysisDigestStable -ne $true) { throw "Identical analytical output did not produce a stable analytical digest." }
if ($result.digestSelfTests.propertyOrderIndependentAnalysisDigestStable -ne $true) { throw "Analytical digest changed when JSON property order changed." }
if ($result.digestSelfTests.timestampedEvidenceEnvelopeDigestDistinct -ne $true) { throw "Timestamped evidence envelope digest was not distinguished from analytical digest." }

$structuredJson = & pwsh -NoProfile -File $script -ValidateOnly -ExperimentTaskId "VSP-LOCALAI-001E" -ReplayAttemptId "attempt2-analysis-schema" -RunsPerCase 5 -UseStructuredOutputSchema -OutputDirectory "AI/Orchestrator/LocalAI/VSP-LOCALAI-001E"
if ($LASTEXITCODE -ne 0) {
    throw "Local AI structured-output replay ValidateOnly failed."
}

$structured = $structuredJson | ConvertFrom-Json
if ($structured.status -ne "PASS") { throw "Structured ValidateOnly status was not PASS." }
if ($structured.taskId -ne "VSP-LOCALAI-001E") { throw "Unexpected structured task ID." }
if ($structured.replayAttemptId -ne "attempt2-analysis-schema") { throw "Unexpected structured replay attempt ID." }
if ($structured.cases -ne 3) { throw "Expected exactly 3 structured replay cases." }
if ($structured.runsPerCase -ne 5) { throw "Expected 5 structured runs per case." }
if ($structured.structuredOutputMode -ne "ollama-json-schema") { throw "Structured output mode was not schema-constrained." }
if ($structured.modelGeneratedFields -ne "analysis-only") { throw "Structured model-generated field mode was not analysis-only." }
if (@($structured.trustedOrchestratorAttachedFields).Count -eq 0) { throw "Structured mode must attach trusted orchestration fields." }
if ($structured.structuredPromptContract.requestsAnalysisOnly -ne $true) { throw "Structured prompt does not request analysis-only output." }
if ($structured.structuredPromptContract.declaresTrustedEnvelopeAttachment -ne $true) { throw "Structured prompt does not describe trusted envelope attachment." }
if ($structured.scoringSelfTests.case2EmptyAnalysisWithGovernanceMetadataDetected -ne $false) { throw "Structured CASE2 scoring is contaminated by governance metadata." }
if ($structured.scoringSelfTests.case2GenuineModelAnalysisDetected -ne $true) { throw "Structured CASE2 scoring did not detect genuine model-authored analysis." }
if ($structured.scoringSelfTests.modelAuthoredAuthorityTextDetected -ne $true) { throw "Structured model-authored authority text was not detected." }
if ($structured.scoringSelfTests.unsupportedClaimDetectedFromModelAnalysis -ne $true) { throw "Structured unsupported claim detection did not inspect model-authored analysis." }
if ($structured.scoringSelfTests.defaultFullResponseUnsupportedClaimDetected -ne $true) { throw "Structured validation did not preserve default full-response unsupported-claim coverage." }
if ($structured.scoringSelfTests.safeGovernanceEnvelopeDoesNotCreateDetection -ne $true) { throw "Structured safe governance envelope metadata created a false CASE2 detection." }
if ($structured.digestSelfTests.identicalAnalysisDigestStable -ne $true) { throw "Structured identical analytical output did not produce a stable analytical digest." }
if ($structured.digestSelfTests.propertyOrderIndependentAnalysisDigestStable -ne $true) { throw "Structured analytical digest changed when JSON property order changed." }
if ($structured.digestSelfTests.timestampedEvidenceEnvelopeDigestDistinct -ne $true) { throw "Structured timestamped evidence envelope digest was not distinguished from analytical digest." }
if ($structured.localAiRepositoryWrite -ne $false) { throw "Structured Local AI repository write boundary changed." }
if ($structured.localAiGitHubAuthority -ne $false) { throw "Structured Local AI GitHub authority boundary changed." }
if ($structured.livePrGateIntegration -ne $false) { throw "Structured Local AI live PR gate integration must be false." }
if ($structured.firewallChanged -ne $false) { throw "Structured firewall must not be changed." }
if ($structured.ollamaModelContextChanged -ne $false) { throw "Structured Ollama model/context must not be changed." }

$simplifiedJson = & pwsh -NoProfile -File $script -ValidateOnly -ExperimentTaskId "VSP-LOCALAI-001F" -ReplayAttemptId "attempt1-simplified-prompt-evidence" -RunsPerCase 5 -UseStructuredOutputSchema -PromptEvidenceMode Simplified -OutputDirectory "AI/Orchestrator/LocalAI/VSP-LOCALAI-001F"
if ($LASTEXITCODE -ne 0) {
    throw "Local AI simplified prompt/evidence replay ValidateOnly failed."
}

$simplified = $simplifiedJson | ConvertFrom-Json
if ($simplified.status -ne "PASS") { throw "Simplified ValidateOnly status was not PASS." }
if ($simplified.taskId -ne "VSP-LOCALAI-001F") { throw "Unexpected simplified task ID." }
if ($simplified.replayAttemptId -ne "attempt1-simplified-prompt-evidence") { throw "Unexpected simplified replay attempt ID." }
if ($simplified.runsPerCase -ne 5) { throw "Expected 5 simplified runs per case." }
if ($simplified.structuredOutputMode -ne "ollama-json-schema") { throw "Simplified run must use Ollama JSON Schema." }
if ($simplified.promptEvidenceMode -ne "Simplified") { throw "Simplified prompt/evidence mode was not reported." }
if ($simplified.simplifiedPromptContract.trustedInstructionsLayerPresent -ne $true) { throw "Simplified trusted instruction layer missing." }
if ($simplified.simplifiedPromptContract.singleObjectiveLayerPresent -ne $true) { throw "Simplified single-objective layer missing." }
if ($simplified.simplifiedPromptContract.untrustedMaterialLayerPresent -ne $true) { throw "Simplified untrusted-material layer missing." }
if ($simplified.simplifiedPromptContract.promptInjectionBoundaryPresent -ne $true) { throw "Simplified prompt injection boundary missing." }
if ($simplified.scoringSelfTests.case1WeakRecursionWordsDetected -ne $false) { throw "CASE1 weak recursion terminology should not satisfy known-defect scoring." }
if ($simplified.scoringSelfTests.case1GenuineModelAnalysisDetected -ne $true) { throw "CASE1 genuine model analysis was not detected." }
if ($simplified.scoringSelfTests.case2TrustedMetadataWriteDetected -ne $false) { throw "CASE2 scoring was contaminated by trusted metadata write text." }
if ($simplified.scoringSelfTests.emptyAnalysisDoesNotDetectKnownDefect -ne $true) { throw "Empty analysis should not detect known defects." }
if ($simplified.scoringSelfTests.promptInjectionEscapeDetected -ne $true) { throw "Prompt-injection escape scorer did not detect hostile model-authored text." }
if ($simplified.localAiRepositoryWrite -ne $false) { throw "Simplified Local AI repository write boundary changed." }
if ($simplified.localAiGitHubAuthority -ne $false) { throw "Simplified Local AI GitHub authority boundary changed." }
if ($simplified.livePrGateIntegration -ne $false) { throw "Simplified Local AI live PR gate integration must be false." }
if ($simplified.firewallChanged -ne $false) { throw "Simplified firewall must not be changed." }
if ($simplified.ollamaModelContextChanged -ne $false) { throw "Simplified Ollama model/context must not be changed." }

[pscustomobject]@{
    status = "PASS"
    validateOnly = "PASS"
    replayCases = 3
    defaultRunsPerCase = 3
    structuredRunsPerCase = 5
    simplifiedRunsPerCase = 5
    defaultStructuredOutputMode = $result.structuredOutputMode
    experimentalStructuredOutputMode = $structured.structuredOutputMode
    simplifiedPromptEvidenceMode = $simplified.promptEvidenceMode
    localAiRepositoryWrite = $false
    localAiGitHubAuthority = $false
    livePrGateIntegration = $false
    firewallChanged = $false
    ollamaModelContextChanged = $false
} | ConvertTo-Json -Depth 4
