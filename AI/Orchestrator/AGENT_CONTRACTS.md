# Agent Contracts

**Scope:** Reusable AI01 task lifecycle

## Agent Router / Orchestrator

The Router owns task routing, gate evaluation, structured state, and recovery.

Allowed:

- Read PR metadata, branch state, checks, reviews, and structured state.
- Route work to Codex Worker or Claude Code.
- Wait for Windows CI and Claude Automated Review as parallel gates.
- Request Codex Independent Review after both parallel gates pass.
- Route remediation within the approved scope and remediation limit.
- Stop for Product Owner input when a Stop Condition is reached.

Not allowed:

- Expand approved scope.
- Make product, architecture, or security decisions.
- Hide or downgrade CI/review failures.
- Merge a PR.
- Allow the same role/work context to implement and complete Required Independent Review for the same PR.

## Codex Worker

Codex Worker handles low-risk work.

Allowed:

- Low-risk analysis.
- CI triage.
- Small documentation/configuration fixes.
- Small implementation tasks inside approved scope.
- Structured evidence extraction.

Not allowed:

- High-risk implementation.
- Architecture, product, or security decisions.
- Required Independent Review for a PR it modified.

## Claude Code

Claude Code is the primary implementation/remediation agent.

Allowed:

- General implementation inside approved scope.
- High-risk implementation only after explicit approval.
- Remediation requested by the Router.
- Build/test execution and evidence reporting.
- Branch updates when authorized by the orchestrated workflow.

Not allowed:

- Final Product Owner acceptance.
- Required Independent Review of its own work.
- Autonomous merge.

## Codex Independent Reviewer

Default model: `gpt-5.6-luna medium`.

Allowed:

- Read-only PR and repository inspection.
- Requirement coverage review.
- Architecture review.
- Correctness, reliability, security, and maintainability review.
- Test-gap and regression-risk review.
- Concurrency and resource-lifecycle review.

Not allowed:

- Repository writes.
- Push, commit, merge, or workflow mutation.
- Reviewing a PR from the same role/work context that modified it.
- Trusting implementation reports without inspecting actual PR state.
