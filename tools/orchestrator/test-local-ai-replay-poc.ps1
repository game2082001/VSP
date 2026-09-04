param()

$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "invoke-local-ai-replay-poc.ps1"
$json = & pwsh -NoProfile -File $script -ValidateOnly
if ($LASTEXITCODE -ne 0) {
    throw "Local AI replay PoC ValidateOnly failed."
}

$result = $json | ConvertFrom-Json
if ($result.status -ne "PASS") { throw "ValidateOnly status was not PASS." }
if ($result.cases -ne 3) { throw "Expected exactly 3 replay cases." }
if ($result.runsPerCase -ne 3) { throw "Expected 3 runs per case." }
if ($result.requestSchemaVersion -ne "1.0") { throw "Unexpected request schema version." }
if ($result.responseSchemaVersion -ne "1.0") { throw "Unexpected response schema version." }
if ($result.localAiRepositoryWrite -ne $false) { throw "Local AI repository write boundary changed." }
if ($result.localAiGitHubAuthority -ne $false) { throw "Local AI GitHub authority boundary changed." }
if ($result.livePrGateIntegration -ne $false) { throw "Local AI live PR gate integration must be false." }
if ($result.firewallChanged -ne $false) { throw "Firewall must not be changed." }
if ($result.ollamaModelContextChanged -ne $false) { throw "Ollama model/context must not be changed." }

[pscustomobject]@{
    status = "PASS"
    validateOnly = "PASS"
    replayCases = 3
    runsPerCase = 3
    localAiRepositoryWrite = $false
    localAiGitHubAuthority = $false
    livePrGateIntegration = $false
    firewallChanged = $false
    ollamaModelContextChanged = $false
} | ConvertTo-Json -Depth 4
