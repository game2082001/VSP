param(
    [Parameter(Mandatory = $true)]
    [int] $PrNumber,

    [string] $Repository = "game2082001/VSP",
    [string] $AssignedRole = "Claude Code"
)

$ErrorActionPreference = "Stop"

if ($PrNumber -eq 7) {
    throw "PR #7 is protected out-of-scope for AI01-008."
}

$mention = if ($AssignedRole -eq "Codex Worker") { "@codex" } else { "@claude" }
$body = "$mention Remediation requested by AI01-008 Router. Stay inside approved scope, respect remediation limits, and stop for Product Owner on scope, architecture, product, security, or unrecoverable CI issues."

gh pr comment $PrNumber --repo $Repository --body $body
