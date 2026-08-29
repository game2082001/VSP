# AI01 Orchestrator

**Status:** Draft implementation
**Owner:** AI Development Kit
**Scope:** Reusable AI01 Autonomous Development Team lifecycle

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

## AI02 Task Intake

Before implementation begins, AI02 work must have a Product Owner-approved machine-readable task manifest. The preferred intake path is:

```text
GitHub Issue + AI02 Task Manifest
```

The manifest schema is defined in `TASK_MANIFEST_SCHEMA.md`, with a reusable template at `Templates/task-manifest.template.json`. The validation tool `tools/orchestrator/task-manifest.ps1` verifies classification, approved scope, role assignment, independent reviewer requirements, Claude Cross Review requirements, Stop Conditions, and Product Owner authorization evidence. It can create an initial structured state artifact, but it must not dispatch developers, dispatch reviewers, route remediation, trigger workflows, or merge PRs.

## Terminal State

The orchestrator never merges automatically. A successful run stops at:

```text
READY_FOR_MERGE
```

`READY_FOR_MERGE` must be followed by `PRODUCT OWNER DECISION REQUIRED` with a recommended merge decision, PR number, HEAD SHA, gate results, remediation count, and remaining known risks. The Product Owner performs the final merge manually.

## Protected Scope

Each AI01 task must define its approved scope from current GitHub truth. Protected PRs, paused investigations, and product-feature exclusions remain out of scope unless the Product Owner explicitly includes them in that task plan.

## Documents

- `AGENT_CONTRACTS.md` - role boundaries and allowed actions
- `ROUTER_POLICY.md` - routing decisions and stage transitions
- `POST_MERGE_MAIN_VALIDATION.md` - post-merge main-head validation rules
- `STATE_SCHEMA.md` - structured state artifact contract
- `TOKEN_BUDGET_POLICY.md` - budget gates and stop behavior
- `STOP_CONDITIONS.md` - conditions that require Product Owner input
- `RECOVERY.md` - crash and session recovery rules
- `ROLE_SEPARATION.md` - implementation/review isolation rules
- `REMEDIATION_POLICY.md` - bounded automatic remediation loop
- `TASK_MANIFEST_SCHEMA.md` - Product Owner-approved task intake manifest contract
- `DECISION_UX.md` - Product Owner decision format and pre-authorized execution
