# AI02 Task Classification

**Status:** Stable
**Owner:** AI Development Kit
**Established By:** VSP-AI02-001A
**Scope:** Repository-persisted task classification, developer/reviewer role assignment, Claude Cross Review requirements, and READY_FOR_MERGE governance evidence.

---

## 1. Purpose

This document is the authoritative VSP-AI02 source for classifying Product and Engineering tasks before implementation begins.

AI02 extends the AI01 Orchestrator baseline. It does not grant autonomous merge authority, does not replace Product Owner approval, and does not allow a developer context to be the only independent reviewer of its own work.

Any normal `PD`, `SEC`, `PLAYER`, `UI`, `BUG`, or `RELEASE` task must not modify or weaken this document. Changes to task classification, developer/reviewer assignment, Claude Cross Review requirements, role separation, READY_FOR_MERGE evidence, or autonomous merge authority require a dedicated `VSP-AI02-*` Governance Task and explicit Product Owner approval.

---

## 2. Mandatory Classification Block

Every Product or Engineering task must record this block before implementation begins:

```text
TASK CLASSIFICATION

Task:

Risk Class:
SMALL | MEDIUM | MAJOR | CRITICAL

Primary Developer:

implementationContextId:

Independent Reviewer:

independentReviewerContextId:

Developer == Reviewer:
FALSE

Claude Cross Review:
REQUIRED | NOT_REQUIRED | RISK_BASED

Reason:

Approved Scope:

Stop Conditions:

Lifecycle:
```

If `Developer == Reviewer` is `TRUE`, blank, unknown, or not provably `FALSE`, the task may not reach `READY_FOR_MERGE`.

---

## 3. Risk Classes

### SMALL

Examples:

- localized bug fix
- small UX fix
- workflow fix
- CI remediation
- test hardening
- warning cleanup
- localized security remediation

Lifecycle:

```text
Codex Development Agent
-> tests
-> Windows CI
-> Separate Codex Independent Reviewer Agent
-> remediation by original Development Agent if required
-> Independent Re-review
-> READY_FOR_MERGE
-> Product Owner
```

Claude Cross Review is not required by default for SMALL tasks.

### MEDIUM

MEDIUM tasks have moderate scope or risk and require task-specific developer selection.

Lifecycle:

```text
Assigned Developer
-> tests
-> Windows CI
-> Separate Codex Independent Reviewer Agent
-> remediation by original Development Agent if required
-> Independent Re-review
-> Claude Cross Review when classification requires it
-> READY_FOR_MERGE
-> Product Owner
```

Claude Cross Review is risk-based for MEDIUM tasks.

### MAJOR

Examples:

- substantial product feature
- cross-module implementation
- significant UI plus backend integration
- substantial refactor
- recording architecture
- multi-view or timeline work
- vendor driver implementation

Primary developer:

```text
Claude Code Primary Developer
```

Lifecycle:

```text
Claude Code Primary Developer
-> tests
-> Windows CI
-> Separate Codex Independent Reviewer Agent
-> Claude remediation if findings exist
-> Codex Re-review
-> Claude Cross Review when classification requires it
-> READY_FOR_MERGE
-> Product Owner
```

### CRITICAL

Automatically includes:

- authentication architecture
- authorization boundary
- credential encryption or protection
- destructive or database schema migration
- Backup/Restore architecture
- filesystem destructive behavior
- release evidence infrastructure
- installer/update security
- irreversible data-format changes
- similarly high-risk security-sensitive changes

Primary developer:

```text
Claude Code Primary Developer
```

Mandatory lifecycle:

```text
Claude Code Primary Development
-> tests
-> Windows CI
-> Separate Codex Independent Review
-> remediation by original Developer
-> Codex Re-review
-> Claude Cross Review
-> READY_FOR_MERGE
-> Product Owner
```

Claude Cross Review is always REQUIRED for CRITICAL tasks.

---

## 4. Developer / Reviewer Separation

This rule is non-negotiable:

```text
Developer Agent != Independent Reviewer Agent
```

For Codex-developed work:

```text
implementationContextId != independentReviewerContextId
```

The Independent Reviewer is read-only by default.

The reviewer may inspect code, tests, diffs, CI, and evidence; report findings; and approve or reject.

The reviewer must not silently fix its own findings.

Finding lifecycle:

```text
Reviewer finding
-> original Development Agent remediation
-> CI/tests
-> same independent-review role re-review
```

If role separation cannot be proven, the result is:

```text
STOP / NOT READY_FOR_MERGE
```

---

## 5. Claude Usage Policy

Claude must not be required on every PR.

Claude is required when:

- the task is CRITICAL
- the task is MAJOR and classification requires Claude Cross Review
- the Product Owner explicitly requests Claude Cross Review
- the Codex Independent Reviewer escalates due risk or uncertainty

Claude is not required by default for SMALL tasks, documentation-only tasks, or localized tasks where the classification records `Claude Cross Review: NOT_REQUIRED`.

Claude Cross Review is evidence, not Product Owner acceptance, and never grants merge authority.

---

## 6. READY_FOR_MERGE Governance Evidence

Every task or PR must provide this final evidence block before it can be presented as `READY_FOR_MERGE`:

```text
AI GOVERNANCE EVIDENCE

Task:
Classification:

Primary Developer:

implementationContextId:

Independent Reviewer:

independentReviewerContextId:

Developer == Reviewer:
FALSE

Claude Cross Review Required:

Claude Cross Review Result:
N/A | PASS

Windows CI:
PASS

Independent Review:
APPROVED

Unresolved Findings:
0

Scope Drift:
NONE

READY_FOR_MERGE:
YES
```

Missing developer/reviewer separation evidence means:

```text
STOP / NOT READY_FOR_MERGE
```

`READY_FOR_MERGE` is merge eligibility evidence only. Product Owner remains the sole merge authority.

---

## 7. Bootstrap Exception

`VSP-AI02-001A` has a Product Owner-approved temporary bootstrap exception:

- Codex Development Agent may implement AI02 governance foundation.
- Separate Codex Independent Reviewer context is still required.
- `implementationContextId != independentReviewerContextId` must be proven.
- Claude Cross Review remains REQUIRED.
- Product Owner remains the only merge authority.

This exception applies only to AI02 governance bootstrap work. It must not be extended to normal Product, Security, Player, UI, Bug, Release, Pilot, or GA tasks.
