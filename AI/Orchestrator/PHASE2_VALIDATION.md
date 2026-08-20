# AI01-008 Phase 2 Validation

## Scope

Phase 2 validates the AI01-008 Role Model V1.0 and Orchestrator MVP without touching PR #7, RTSP flaky investigation, decoder investigation, or product implementation code.

## Role Model V1.0

- ChatGPT is Product / Architecture Lead.
- Work is ChatGPT Engineering Workspace / Control Room, not an Orchestrator development agent.
- Claude is a model/provider, not a modeled team member.
- Claude Code is the Senior Implementation Agent for normal and high-risk implementation and remediation.
- Codex Worker is Investigator / Software Engineer for analysis, CI triage, documentation, low-risk work, and test evidence.
- Codex Independent Reviewer is a clean-context, read-only Senior Reviewer and returns `APPROVED`, `REMEDIATION REQUIRED`, or `STOPPED FOR PRODUCT OWNER`.
- GitHub Claude Automated Review is an automated review gate, not an implementation agent and not the required independent review.
- Orchestrator is Dispatcher / AI PM for routing, state, gate sequencing, budgets, stop conditions, recovery, and remediation routing.

## Deterministic E2E Coverage

`tools/orchestrator/dry-run.ps1` validates the orchestration state machine with fake evidence:

- PASS path: task routing, implementation request, parallel CI/review gates, independent review request, `READY_FOR_MERGE`.
- Remediation path: independent review remediation finding, Router remediation assignment, current-head revalidation, `READY_FOR_MERGE`.
- Token budget hard stop: `STOPPED_FOR_PRODUCT_OWNER`.
- Remediation limit stop: `STOPPED_FOR_PRODUCT_OWNER`.
- Repeated finding stop: `STOPPED_FOR_PRODUCT_OWNER`.
- Agent failure / timeout stop: `STOPPED_FOR_PRODUCT_OWNER`.
- Recursive workflow trigger stop: `STOPPED_FOR_PRODUCT_OWNER`.
- Stale HEAD evidence stop: `STOPPED_FOR_PRODUCT_OWNER`.
- Scope expansion stop: `STOPPED_FOR_PRODUCT_OWNER` with fixed Product Owner Decision UX.
- Security / credential decision stop: `STOPPED_FOR_PRODUCT_OWNER` with fixed Product Owner Decision UX.
- Architecture decision stop: `STOPPED_FOR_PRODUCT_OWNER` with fixed Product Owner Decision UX.
- Pre-authorized PASS path: Task Plan authorization carries implementation, validation, commit, push, PR update, CI, automated review, and independent review through to `READY_FOR_MERGE` without intermediate Product Owner prompts.
- Pre-authorized remediation path: in-scope remediation automatically routes to Claude Code, commits/pushes through the authorized path, re-runs gates, and reaches `READY_FOR_MERGE`.
- Gate waiting path: queued, pending, and in-progress CI or Automated Review gates stay in lifecycle polling and do not become Product Owner decisions before timeout/tolerance.
- Trusted implementation bot review trigger: VSP-AI-Implementation-authored PR branch updates must trigger Claude Automated Review through `allowed_bots: vsp-ai-implementation`; wildcard bot allowance is prohibited.
- Merge Gate: `READY_FOR_MERGE` includes a `PRODUCT OWNER DECISION REQUIRED` payload recommending merge, but no autonomous merge occurs.
- Restart recovery: state is persisted as JSON and re-read from `StatePath`; Git/GitHub/current-head evidence remains authoritative when state disagrees.

## Product Owner Decision UX

Every `STOPPED_FOR_PRODUCT_OWNER` and `READY_FOR_MERGE` state must include:

- Reason
- Recommended
- Why
- If approved
- Alternatives, including `Stop / Defer`

The Product Owner can approve the recommendation or choose an option number. The Orchestrator must not emit only `Awaiting Approval`, `What would you like to do?`, `Please advise next step`, or any open-ended question without a recommendation.

## Security Coverage

- PR #7 is explicitly blocked by Router and request scripts.
- No credentials or secret values are required in repo files, JSON state, scripts, logs, or templates.
- Reviewer context is modeled separately from Codex Worker context.
- Codex Independent Reviewer remains read-only and cannot commit, push, mutate workflows, or merge.
- Claude Automated Review remains read-only and may be triggered by the trusted VSP implementation bot, but it must not receive the Implementation GitHub App write credential.
- First version stops at `READY_FOR_MERGE`; Product Owner performs manual merge.

## Live-Agent Smoke

`tools/orchestrator/live-agent-smoke.ps1` performs the minimum live environment smoke:

- Confirms `git`, `gh`, `claude`, and `codex` commands are discoverable.
- Confirms PR #7 remains protected.
- Scans orchestrator-controlled files for actual secret-looking values.
- Confirms Claude Automated Review narrowly allows `vsp-ai-implementation` and does not allow arbitrary bots.

It does not post comments, request reviews, push, open PRs, or invoke production workflows.

## Excluded Pre-existing Work

The following working-tree areas are excluded from AI01-008 and must not be staged or committed with this task:

- `VSP.Player/Decoder/*`
- `VSP.Tests/Player/*PacketGuardTests.cs`
- `VSP.Tests/Player/*FrameGuardTests.cs`
- `VSP.Tests/Player/MediaControllerReconnectTests.cs`
- `VSP.Infrastructure/VSP.Infrastructure.csproj`

RTSP flaky investigation remains paused, open, and root-cause unresolved.
