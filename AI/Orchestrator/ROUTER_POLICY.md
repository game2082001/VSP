# Router Policy

**Scope:** Reusable AI01 task lifecycle

## Routing Order

1. Read GitHub PR state.
2. Read Git branch/head SHA.
3. Read structured state.
4. Read AI02 Task Classification and Task Plan authorization.
5. Read workflow/check status.
6. Detect Stop Conditions.
7. Apply token budget gates.
8. Route to the next role.

## Pre-Authorized Lifecycle

When Task Plan Approval includes execution authorization, the Router automatically continues through:

```text
Implementation
-> Local Validation
-> Commit
-> Push feature branch
-> Open / update PR
-> CI Gate and Automated Review Gate
-> Required Independent Review
-> In-scope remediation loop
-> Remediation commit / push / gates
-> READY_FOR_MERGE
```

The Router must not ask the Product Owner again for commit, push, PR creation/update, CI, automated review, independent review, or in-scope remediation while the work remains inside approved scope and within configured budgets.

## Gate Polling

Queued, pending, and in-progress CI or Automated Review gates are normal lifecycle states. The Router must continue polling until a gate reaches a terminal result or the configured timeout/tolerance is exceeded.

The Router must not emit `PRODUCT OWNER DECISION REQUIRED` for normal waiting. It stops only when gate evidence becomes unavailable, an agent/authentication/infrastructure failure is detected, retry/remediation budget is exhausted, or another Stop Condition is reached.

## Role Selection

AI02 task classification is authoritative; see `AI/OperatingSystem/TASK_CLASSIFICATION.md`.

Use Codex Worker for:

- SMALL task implementation.
- MEDIUM task implementation when assigned by classification.
- Low-risk analysis.
- CI triage.
- Small documentation/configuration changes.
- Small implementation changes with clear scope and low blast radius.

Use Claude Code for:

- MAJOR task implementation.
- CRITICAL task implementation.
- MEDIUM task implementation when assigned by classification.
- Remediation that touches production behavior.
- Changes requiring build/test execution evidence.

Use Codex Independent Reviewer for:

- Required Independent Review after Windows CI and Claude Automated Review both pass.
- Re-review after remediation.

## Gate Rules

Windows CI is always required for PR merge eligibility. Claude review is required only when the AI02 task classification says `REQUIRED` or when risk-based escalation requires it.

```text
Windows CI PASS
Claude Automated Review / Cross Review PASS when required
        -> Codex Independent Review
```

If either parallel gate fails, the Router classifies the failure:

- recoverable within approved scope -> remediation loop
- unrecoverable or scope-expanding -> Product Owner stop

## Terminal Rules

`APPROVED` independent review sets:

```text
READY_FOR_MERGE
```

The Router must not merge the PR.

`READY_FOR_MERGE` is a Product Owner decision gate. The Router recommends merge and provides PR, HEAD SHA, gate results, remediation iterations, and remaining known risks, but performs no merge.
