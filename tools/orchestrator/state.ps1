param(
    [Parameter(Mandatory = $true)]
    [string] $StatePath
)

$ErrorActionPreference = "Stop"

function Read-OrchestratorState {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "State file not found: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Write-OrchestratorState {
    param(
        [Parameter(Mandatory = $true)] $State,
        [Parameter(Mandatory = $true)][string] $Path
    )

    $State.updatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    $State | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding utf8
}

Read-OrchestratorState -Path $StatePath | ConvertTo-Json -Depth 10
