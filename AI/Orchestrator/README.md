# AI01-008 Orchestrator

**Status:** Draft implementation
**Owner:** AI Development Kit
**Task:** AI01-008 - Autonomous Multi-Agent Development Pipeline

This directory defines the PR-based orchestration layer for autonomous multi-agent development in VSP.

The orchestrator does not replace the AI Operating System. It provides executable routing, state, and gate rules for the existing governance model:

```text
Agent Router
    -> Codex Worker or Claude Code
    -> Windows CI and Claude Automated Review
    -> Codex Independent Review
    -> Remediation Loop or READY FOR MERGE
```

## Source of Truth

Crash and session recovery must be based on verifiable repository state:

1. GitHub pull request metadata
2. Git branch and commit history
3. GitHub checks and workflow runs
4. Structured orchestration state
5. PR comments and review comments
6. Chat history as a hint only

## Terminal State

The first orchestrator version never merges automatically. A successful run stops at:

```text
READY_FOR_MERGE
```

`READY_FOR_MERGE` must be followed by `PRODUCT OWNER DECISION REQUIRED` with a recommended merge decision, PR number, HEAD SHA, gate results, remediation count, and remaining known risks. The Product Owner performs the final merge manually.

## Protected Scope

AI01-008 does not modify PR #7, does not remediate PR #7 CI failures, and does not include the paused RTSP flaky investigation.

## Documents

- `AGENT_CONTRACTS.md` - role boundaries and allowed actions
- `ROUTER_POLICY.md` - routing decisions and stage transitions
- `STATE_SCHEMA.md` - structured state artifact contract
- `TOKEN_BUDGET_POLICY.md` - budget gates and stop behavior
- `STOP_CONDITIONS.md` - conditions that require Product Owner input
- `RECOVERY.md` - crash and session recovery rules
- `ROLE_SEPARATION.md` - implementation/review isolation rules
- `REMEDIATION_POLICY.md` - bounded automatic remediation loop
- `DECISION_UX.md` - Product Owner decision format and pre-authorized execution
