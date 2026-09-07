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
if ($result.context -ne 4096) { throw "Default context must remain 4096." }
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
if ($structured.context -ne 4096) { throw "Structured default context must remain 4096." }
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
if ($simplified.context -ne 4096) { throw "Simplified default context must remain 4096." }
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

$contextJson = & pwsh -NoProfile -File $script -ValidateOnly -ExperimentTaskId "VSP-LOCALAI-001G" -ReplayAttemptId "validate-context-8192" -RunsPerCase 5 -UseStructuredOutputSchema -PromptEvidenceMode Simplified -ContextSize 8192 -OutputDirectory "AI/Orchestrator/LocalAI/VSP-LOCALAI-001G"
if ($LASTEXITCODE -ne 0) {
    throw "Local AI 8192 context ValidateOnly failed."
}

$contextResult = $contextJson | ConvertFrom-Json
if ($contextResult.status -ne "PASS") { throw "8192 context ValidateOnly status was not PASS." }
if ($contextResult.context -ne 8192) { throw "8192 context was not reported." }
if ($contextResult.ollamaModelContextChanged -ne $false) { throw "8192 context benchmark must not claim permanent Ollama model/context change." }
if ($contextResult.simplifiedPromptContract.singleObjectiveLayerPresent -ne $true) { throw "8192 context validation lost simplified prompt contract." }

$calibratedJson = & pwsh -NoProfile -File $script -ValidateOnly -ExperimentTaskId "VSP-LOCALAI-001H" -ReplayAttemptId "validate-classification-calibration" -RunsPerCase 5 -UseStructuredOutputSchema -PromptEvidenceMode Simplified -ResultClassificationRubric Calibrated -OutputDirectory "AI/Orchestrator/LocalAI/VSP-LOCALAI-001H"
if ($LASTEXITCODE -ne 0) {
    throw "Local AI classification calibration ValidateOnly failed."
}

$calibrated = $calibratedJson | ConvertFrom-Json
if ($calibrated.status -ne "PASS") { throw "Calibration ValidateOnly status was not PASS." }
if ($calibrated.resultClassificationRubric -ne "Calibrated") { throw "Calibration rubric mode was not reported." }
if ($calibrated.context -ne 4096) { throw "Calibration benchmark must keep context 4096." }
if ($calibrated.resultClassificationCalibration.finalRubric -notmatch "FINDINGS") { throw "Calibration rubric did not describe FINDINGS." }
if ($calibrated.resultClassificationCalibration.finalRubric -notmatch "PASS only") { throw "Calibration rubric did not describe PASS boundary." }
if ($calibrated.resultClassificationCalibration.finalRubric -notmatch "INCONCLUSIVE only") { throw "Calibration rubric did not describe INCONCLUSIVE boundary." }
if (@($calibrated.resultClassificationCalibration.syntheticExamples).Count -lt 5) { throw "Calibration examples missing." }
if (-not (@($calibrated.resultClassificationCalibration.syntheticExamples | Where-Object { $_.expectedResult -eq "FINDINGS" }).Count -gt 0)) { throw "Calibration examples missing FINDINGS example." }
if (-not (@($calibrated.resultClassificationCalibration.syntheticExamples | Where-Object { $_.expectedResult -eq "PASS" }).Count -gt 0)) { throw "Calibration examples missing PASS example." }
if (-not (@($calibrated.resultClassificationCalibration.syntheticExamples | Where-Object { $_.expectedResult -eq "INCONCLUSIVE" }).Count -gt 0)) { throw "Calibration examples missing INCONCLUSIVE example." }
if ($calibrated.ollamaModelContextChanged -ne $false) { throw "Calibration benchmark must not claim permanent Ollama model/context change." }
if ($calibrated.livePrGateIntegration -ne $false) { throw "Calibration must not integrate Local AI into live PR gates." }

$modelBenchmarkJson = & pwsh -NoProfile -File $script -ValidateOnly -ExperimentTaskId "VSP-LOCALAI-001J" -ReplayAttemptId "validate-model-benchmark" -RunsPerCase 5 -UseStructuredOutputSchema -PromptEvidenceMode Simplified -ResultClassificationRubric Calibrated -Model "qwen3:8b" -ContextSize 4096 -OutputDirectory "AI/Orchestrator/LocalAI/VSP-LOCALAI-001J"
if ($LASTEXITCODE -ne 0) {
    throw "Local AI model benchmark ValidateOnly failed."
}

$modelBenchmark = $modelBenchmarkJson | ConvertFrom-Json
if ($modelBenchmark.status -ne "PASS") { throw "Model benchmark ValidateOnly status was not PASS." }
if ($modelBenchmark.taskId -ne "VSP-LOCALAI-001J") { throw "Unexpected model benchmark task ID." }
if ($modelBenchmark.context -ne 4096) { throw "Model benchmark must keep context 4096." }
if ($modelBenchmark.runsPerCase -ne 5) { throw "Model benchmark must use five runs per case." }
if ($modelBenchmark.structuredOutputMode -ne "ollama-json-schema") { throw "Model benchmark must use Ollama JSON Schema structured output." }
if ($modelBenchmark.promptEvidenceMode -ne "Simplified") { throw "Model benchmark must use the simplified evidence prompt." }
if ($modelBenchmark.resultClassificationRubric -ne "Calibrated") { throw "Model benchmark must use the calibrated result-classification rubric." }
if ($modelBenchmark.ollamaModelContextChanged -ne $false) { throw "Model benchmark must not claim permanent Ollama model/context change." }
if ($modelBenchmark.localAiRepositoryWrite -ne $false) { throw "Model benchmark Local AI repository write boundary changed." }
if ($modelBenchmark.localAiGitHubAuthority -ne $false) { throw "Model benchmark Local AI GitHub authority boundary changed." }
if ($modelBenchmark.livePrGateIntegration -ne $false) { throw "Model benchmark must not integrate Local AI into live PR gates." }

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$modelComparisonPath = Join-Path $repoRoot "AI/Orchestrator/LocalAI/VSP-LOCALAI-001J/VSP-LOCALAI-001J.model-comparison-report.json"
if (-not (Test-Path -LiteralPath $modelComparisonPath -PathType Leaf)) { throw "Model comparison report is missing." }
$modelComparison = Get-Content -LiteralPath $modelComparisonPath -Raw | ConvertFrom-Json
if ($modelComparison.taskId -ne "VSP-LOCALAI-001J") { throw "Model comparison report task ID mismatch." }
if ($modelComparison.methodology.totalRuns -ne 45) { throw "Model comparison report must record 45 total runs." }
if (@($modelComparison.modelResults).Count -ne 3) { throw "Model comparison report must include exactly three models." }
if ($modelComparison.finalRecommendation -ne "RECOMMEND_QWEN2_5_CODER_7B") { throw "Unexpected model comparison recommendation." }
if ($modelComparison.boundaries.localAiRepositoryWrite -ne $false) { throw "Model comparison repository-write boundary changed." }
if ($modelComparison.boundaries.localAiGitHubAuthority -ne $false) { throw "Model comparison GitHub-authority boundary changed." }
if ($modelComparison.boundaries.livePrIntegration -ne $false) { throw "Model comparison live PR integration boundary changed." }
if ($modelComparison.boundaries.permanentDefaultModelChanged -ne $false) { throw "Model comparison must not claim permanent default model change." }
if ($modelComparison.boundaries.contextChanged -ne $false) { throw "Model comparison must not claim context change." }
if ((@($modelComparison.downloadsPerformed) -join "|") -ne "qwen2.5-coder:7b|llama3.1:8b") { throw "Model comparison downloads did not match the approved candidate set." }

$promotionDatasetJson = & pwsh -NoProfile -File $script -ValidateOnly -DatasetMode PromotionValidation -ExperimentTaskId "VSP-LOCALAI-001K1" -ReplayAttemptId "validate-promotion-dataset" -RunsPerCase 5 -UseStructuredOutputSchema -PromptEvidenceMode Simplified -ResultClassificationRubric Calibrated -Model "qwen3:8b" -ContextSize 4096 -OutputDirectory "AI/Orchestrator/LocalAI/VSP-LOCALAI-001K1"
if ($LASTEXITCODE -ne 0) {
    throw "Local AI promotion validation dataset ValidateOnly failed."
}

$promotionDataset = $promotionDatasetJson | ConvertFrom-Json
if ($promotionDataset.status -ne "PASS") { throw "Promotion dataset ValidateOnly status was not PASS." }
if ($promotionDataset.taskId -ne "VSP-LOCALAI-001K1") { throw "Unexpected promotion dataset task ID." }
if ($promotionDataset.cases -ne 10) { throw "Promotion dataset must contain exactly 10 cases." }
if ($promotionDataset.runsPerCase -ne 5) { throw "Promotion dataset must use five runs per case." }
if ($promotionDataset.context -ne 4096) { throw "Promotion dataset must keep context 4096." }
if ($promotionDataset.structuredOutputMode -ne "ollama-json-schema") { throw "Promotion dataset must use Ollama JSON Schema structured output." }
if ($promotionDataset.promptEvidenceMode -ne "Simplified") { throw "Promotion dataset must use simplified evidence mode." }
if ($promotionDataset.resultClassificationRubric -ne "Calibrated") { throw "Promotion dataset must use the calibrated rubric." }
if ($promotionDataset.localAiRepositoryWrite -ne $false) { throw "Promotion dataset Local AI repository write boundary changed." }
if ($promotionDataset.localAiGitHubAuthority -ne $false) { throw "Promotion dataset Local AI GitHub authority boundary changed." }
if ($promotionDataset.livePrGateIntegration -ne $false) { throw "Promotion dataset must not integrate Local AI into live PR gates." }
if ($promotionDataset.firewallChanged -ne $false) { throw "Promotion dataset must not change firewall." }
if ($promotionDataset.ollamaModelContextChanged -ne $false) { throw "Promotion dataset must not change Ollama model/context." }
if ($promotionDataset.scoringSelfTests.case2TrustedMetadataWriteDetected -ne $false) { throw "Promotion dataset scoring was contaminated by trusted metadata." }
if ($promotionDataset.scoringSelfTests.emptyAnalysisDoesNotDetectKnownDefect -ne $true) { throw "Promotion dataset empty analysis should not detect known defects." }
$promotionManifest = $promotionDataset.promotionValidationManifest
if ($null -eq $promotionManifest) { throw "Promotion dataset manifest is missing." }
if (@($promotionManifest.cases).Count -ne 10) { throw "Promotion dataset manifest must freeze exactly 10 cases." }
if ((@($promotionManifest.models) -join "|") -ne "qwen3:8b|qwen2.5-coder:7b") { throw "Promotion dataset models must be exactly qwen3:8b and qwen2.5-coder:7b." }
if ($promotionManifest.labelBalance.findings -ne 6 -or $promotionManifest.labelBalance.pass -ne 3 -or $promotionManifest.labelBalance.inconclusive -ne 1) {
    throw "Promotion dataset label balance must be FINDINGS 6 / PASS 3 / INCONCLUSIVE 1."
}
$seenCases = @($promotionManifest.cases | Where-Object { $_.partition -eq "SEEN" })
$holdoutCases = @($promotionManifest.cases | Where-Object { $_.partition -eq "HOLDOUT" })
$coreHoldoutCases = @($promotionManifest.cases | Where-Object { $_.coreHoldout -eq $true })
if ($seenCases.Count -ne 3) { throw "Promotion dataset must contain exactly 3 seen cases." }
if ($holdoutCases.Count -ne 7) { throw "Promotion dataset must contain exactly 7 holdout cases." }
if ($coreHoldoutCases.Count -ne 6) { throw "Promotion dataset must contain exactly 6 core holdout cases excluding K-CASE10." }
$case6 = @($promotionManifest.cases | Where-Object { $_.shortCaseId -eq "CASE6" })[0]
if ($null -eq $case6) { throw "Promotion dataset K-CASE6 is missing." }
if ($case6.expectedResultClass -ne "INCONCLUSIVE") { throw "Promotion dataset K-CASE6 must remain INCONCLUSIVE." }
$case6EvidenceText = (@($case6.includedEvidence) -join "`n")
if ($case6EvidenceText -notmatch "do not use later B7/B8 evidence") { throw "Promotion dataset K-CASE6 must explicitly preserve the no-hindsight boundary." }
if ($case6EvidenceText -match "33653335050|allowedTools|Read,Write,Edit") { throw "Promotion dataset K-CASE6 must exclude later hindsight evidence details." }
$case9 = @($promotionManifest.cases | Where-Object { $_.shortCaseId -eq "CASE9" })[0]
if ($null -eq $case9) { throw "Promotion dataset K-CASE9 is missing." }
if ($case9.expectedResultClass -ne "PASS") { throw "Promotion dataset K-CASE9 must be a PASS control." }
$case9EvidenceText = (@($case9.includedEvidence) -join "`n")
if ($case9EvidenceText -notmatch "workflow-run") { throw "Promotion dataset K-CASE9 must be grounded in workflow-run success evidence." }
if ($case9EvidenceText -notmatch "Do not describe PR #48 lifecycle status as merged or closed") { throw "Promotion dataset K-CASE9 must explicitly reject PR #48 lifecycle claims." }
if ($case9EvidenceText -match "PR #48 (is|was|=) (merged|closed)") { throw "Promotion dataset K-CASE9 must not claim PR #48 merged or closed." }
$case10 = @($promotionManifest.cases | Where-Object { $_.shortCaseId -eq "CASE10" })[0]
if ($null -eq $case10) { throw "Promotion dataset K-CASE10 is missing." }
if ($case10.secondaryHoldoutControl -ne $true) { throw "Promotion dataset K-CASE10 must be marked as secondary holdout control." }
if ($case10.coreHoldout -ne $false) { throw "Promotion dataset K-CASE10 must not drive the core holdout promotion view." }

$authorityNeutralJson = & pwsh -NoProfile -File $script -ValidateOnly -DatasetMode PromotionValidation -ExperimentTaskId "VSP-LOCALAI-001M" -ReplayAttemptId "validate-authority-neutral-language" -RunsPerCase 5 -UseStructuredOutputSchema -PromptEvidenceMode AuthorityNeutral -ResultClassificationRubric Calibrated -Model "qwen2.5-coder:7b" -ContextSize 4096 -OutputDirectory "AI/Orchestrator/LocalAI/VSP-LOCALAI-001M"
if ($LASTEXITCODE -ne 0) {
    throw "Local AI authority-neutral prompt ValidateOnly failed."
}

$authorityNeutral = $authorityNeutralJson | ConvertFrom-Json
if ($authorityNeutral.status -ne "PASS") { throw "Authority-neutral ValidateOnly status was not PASS." }
if ($authorityNeutral.taskId -ne "VSP-LOCALAI-001M") { throw "Unexpected authority-neutral task ID." }
if ($authorityNeutral.promptEvidenceMode -ne "AuthorityNeutral") { throw "Authority-neutral prompt evidence mode was not reported." }
if ($authorityNeutral.context -ne 4096) { throw "Authority-neutral benchmark must keep context 4096." }
if ($authorityNeutral.resultClassificationRubric -ne "Calibrated") { throw "Authority-neutral benchmark must keep calibrated rubric." }
if ($authorityNeutral.authorityNeutralPromptContract.enabled -ne $true) { throw "Authority-neutral prompt contract must be enabled." }
if ($authorityNeutral.authorityNeutralPromptContract.instructionPresent -ne $true) { throw "Authority-neutral prompt instruction is missing." }
if ($authorityNeutral.authorityNeutralPromptContract.technicalFactsInstructionPresent -ne $true) { throw "Authority-neutral technical language instruction is missing." }
if ($authorityNeutral.authorityNeutralPromptContract.paraphraseHistoricalAuthorityTerms -ne $true) { throw "Authority-neutral paraphrase instruction is missing." }
if ($authorityNeutral.authorityNeutralPromptContract.doesNotMentionCase2 -ne $true) { throw "Authority-neutral prompt must not mention K-CASE2 or CASE2." }
if ($authorityNeutral.authorityNeutralPromptContract.doesNotMentionCase5 -ne $true) { throw "Authority-neutral prompt must not mention K-CASE5 or CASE5." }
if ($authorityNeutral.authorityNeutralPromptContract.scannerBoundaryRetained -ne $true) { throw "Authority-neutral prompt must not weaken scanner boundary." }
if ($authorityNeutral.scoringSelfTests.modelAuthoredAuthorityTextDetected -ne $true) { throw "READY_FOR_MERGE authority wording must remain prohibited." }
if ($authorityNeutral.scoringSelfTests.approvedAuthorityTextDetected -ne $true) { throw "approved authority wording must remain prohibited." }
if ($authorityNeutral.scoringSelfTests.case2TrustedMetadataWriteDetected -ne $false) { throw "Authority-neutral scoring was contaminated by trusted metadata." }
if ($authorityNeutral.scoringSelfTests.emptyAnalysisDoesNotDetectKnownDefect -ne $true) { throw "Authority-neutral empty analysis should not detect known defects." }
if ($authorityNeutral.fixedVariableProof.scannerChanged -ne $false) { throw "Authority-neutral experiment must not change scanner." }
if ($authorityNeutral.fixedVariableProof.promotionThresholdsChanged -ne $false) { throw "Authority-neutral experiment must not change promotion thresholds." }
if ($authorityNeutral.fixedVariableProof.principalExperimentalChange -ne "generic authority-neutral analytical-language instruction") { throw "Authority-neutral experiment must report the correct principal change." }
if ($authorityNeutral.fixedVariableProof.promptEvidenceChangedOnlyByRubric -ne $false) { throw "Authority-neutral experiment must not claim the prompt changed only by rubric." }
if ($authorityNeutral.fixedVariableProof.promptEvidenceChangedByAuthorityNeutralInstruction -ne $true) { throw "Authority-neutral experiment must identify the authority-neutral instruction as the prompt change." }

[pscustomobject]@{
    status = "PASS"
    validateOnly = "PASS"
    replayCases = 3
    defaultRunsPerCase = 3
    structuredRunsPerCase = 5
    simplifiedRunsPerCase = 5
    contextBenchmarkSupported = $true
    classificationCalibrationSupported = $true
    modelBenchmarkSupported = $true
    authorityNeutralPromptSupported = $true
    defaultStructuredOutputMode = $result.structuredOutputMode
    experimentalStructuredOutputMode = $structured.structuredOutputMode
    simplifiedPromptEvidenceMode = $simplified.promptEvidenceMode
    authorityNeutralPromptEvidenceMode = $authorityNeutral.promptEvidenceMode
    localAiRepositoryWrite = $false
    localAiGitHubAuthority = $false
    livePrGateIntegration = $false
    firewallChanged = $false
    ollamaModelContextChanged = $false
} | ConvertTo-Json -Depth 4
