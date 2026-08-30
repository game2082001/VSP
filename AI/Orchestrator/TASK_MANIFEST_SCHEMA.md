# AI02 Task Manifest Schema

**Status:** Draft
**Established By:** VSP-AI02-001B
**Scope:** Machine-readable Product Owner task intake before PR creation.

---

## 1. Purpose

The AI02 Task Manifest is the fail-closed intake record for Product Owner-approved work before implementation begins.

The manifest records task identity, classification, approved scope, role assignment, reviewer separation requirements, Claude Cross Review requirements, Stop Conditions, and Product Owner authorization evidence in a machine-readable form.

Preferred source:

```text
GitHub Issue + AI02 Task Manifest
```

The GitHub Issue provides human-visible authorization context. The manifest provides the structured contract used to create initial orchestrator state.

This document does not dispatch Claude, dispatch Codex, request review, route remediation, or modify existing router behavior.

---

## 2. Recommended Path

```text
AI/Orchestrator/Manifests/<task-id>.manifest.json
```

Manifest artifacts may also be attached to, copied from, or referenced by the Product Owner-approved GitHub Issue. If a repository manifest and a GitHub Issue disagree, the task must stop until Product Owner authorization is reconciled.

---

## 3. Required Fields

```json
{
  "schemaVersion": "1.0",
  "taskId": "VSP-AI02-001B",
  "title": "Approved Task Intake / Machine-Readable Task Manifest",
  "classification": "SMALL|MEDIUM|MAJOR|CRITICAL",
  "repository": "game2082001/VSP",
  "baseBranch": "main",
  "approvedScope": [
    "Scope item approved by the Product Owner"
  ],
  "outOfScope": [
    "Explicitly excluded work"
  ],
  "primaryDeveloper": {
    "role": "Codex Development Agent|Claude Code Primary Developer",
    "adapter": "codex|claude|manual",
    "assignedBy": "Product Owner",
    "contextId": "",
    "runId": ""
  },
  "independentReviewer": {
    "required": true,
    "role": "Separate Codex Independent Reviewer",
    "adapter": "codex",
    "contextId": ""
  },
  "claudeCrossReview": {
    "required": true,
    "status": "PENDING",
    "runId": "",
    "reason": "Product Owner required"
  },
  "stopConditions": [
    "Stop condition approved by the Product Owner"
  ],
  "productOwnerAuthorization": {
    "authorized": true,
    "authorizedBy": "Product Owner",
    "authorizedAtUtc": "2026-08-29T00:00:00Z",
    "evidenceSource": "GitHub Issue",
    "evidenceUrl": "https://github.com/game2082001/VSP/issues/0",
    "approvalSummary": "Product Owner approved task classification and scope"
  },
  "executionAuthorization": {
    "implementation": true,
    "localValidation": true,
    "commit": true,
    "pushFeatureBranch": true,
    "openOrUpdatePr": true,
    "ciGate": true,
    "automatedReviewGate": true,
    "requiredIndependentReview": true,
    "inScopeRemediation": true,
    "remediationCommitPushAndGates": true
  },
  "repositoryTransport": {
    "required": false,
    "requestPath": "",
    "baseBinding": "EXACT",
    "infrastructureSmoke": false,
    "allowWorkflowChanges": false,
    "openPullRequest": true,
    "postMergeOperationalSmokeRequired": false,
    "approvedFiles": []
  }
}
```

`repositoryTransport.baseBinding` defaults to `EXACT`. `DISPATCH_MAIN` is reserved for Product Owner-approved AI02 infrastructure smoke fixtures and must not be used by ordinary Product, SEC, UI, PLAYER, release, or engineering tasks.

`repositoryTransport.infrastructureSmoke` must be `true` for the narrow `DISPATCH_MAIN` repository transport smoke path and must be omitted or `false` for ordinary tasks.

---

## 4. Classification Consistency Rules

The manifest must be consistent with `AI/OperatingSystem/TASK_CLASSIFICATION.md`.

### SMALL

- Primary Developer must be `Codex Development Agent`.
- Primary Developer adapter must be `codex`.
- Independent Reviewer must be required.
- Independent Reviewer adapter must be `codex`.
- Claude Cross Review may be `false` unless Product Owner requires it.

### MEDIUM

- Primary Developer may be `Codex Development Agent` or `Claude Code Primary Developer` as assigned by Product Owner classification.
- Primary Developer adapter must match the assigned role.
- Independent Reviewer must be required.
- Independent Reviewer adapter must be `codex`.
- Claude Cross Review is risk-based or Product Owner-required.

### MAJOR

- Primary Developer must be `Claude Code Primary Developer`.
- Primary Developer adapter must be `claude`.
- Independent Reviewer must be required.
- Independent Reviewer adapter must be `codex`.
- Claude Cross Review follows Product Owner classification.

### CRITICAL

- Primary Developer must be `Claude Code Primary Developer`.
- Primary Developer adapter must be `claude`.
- Independent Reviewer must be required.
- Independent Reviewer adapter must be `codex`.
- Claude Cross Review must be required.

---

## 5. Fail-Closed Authorization Rules

The manifest is invalid when:

- Product Owner authorization is missing or `authorized` is not `true`.
- Product Owner authorization evidence source, URL, timestamp, or summary is missing.
- `taskId`, `classification`, `repository`, `baseBranch`, `approvedScope`, or `stopConditions` is missing.
- Classification is not one of `SMALL`, `MEDIUM`, `MAJOR`, or `CRITICAL`.
- Primary Developer role and adapter are inconsistent.
- Required Independent Reviewer evidence is missing.
- Independent Reviewer adapter is not `codex`.
- Developer and reviewer contexts are both present and equal.
- `developerEqualsReviewer` would be true in generated state.
- CRITICAL tasks do not require Claude Cross Review.
- Any execution authorization value is missing or not boolean.
- Repository transport is required but its request path, workflow-change authorization, pull request authorization, or approved file allowlist is missing or inconsistent with the publication request.

Missing implementation or reviewer context IDs are allowed at intake time, before implementation/review contexts exist. They must be filled before `READY_FOR_MERGE`; missing separation evidence at merge eligibility remains:

```text
STOP / NOT READY_FOR_MERGE
```

---

## 6. Initial State Creation

The manifest parser may create an initial state artifact from an approved manifest.

Recommended path:

```text
AI/Orchestrator/State/<task-id>.state.json
```

Initial state must:

- preserve the task manifest path and authorization evidence;
- copy classification, approved scope, role assignment, Claude Cross Review requirement, and Stop Conditions;
- set `currentStage` to `PLANNED`;
- set `readyForMerge` to `false`;
- set `taskManifestStatus` and `classificationConsistencyStatus` to `VALID`;
- set `developerEqualsReviewer` to `false`;
- keep missing context IDs blank until real execution/review contexts are known.

The parser must not overwrite an existing state file unless the caller explicitly requests overwrite for a Product Owner-approved reconciliation.

---

## 7. Non-Dispatch Boundary

AI02-001B intake tooling is intentionally inert. It must not:

- dispatch Claude Developer;
- dispatch Codex Developer;
- dispatch Codex Independent Reviewer;
- trigger remediation routing;
- trigger GitHub Actions;
- open or merge PRs;
- modify product code or product tests.

---

## 8. Temporary Bootstrap Exception: VSP-AI02-001T Only

`VSP-AI02-001T` is a CRITICAL AI02 infrastructure task. Its normal required Primary Developer remains `Claude Code Primary Developer`.

Because the Claude autonomous developer adapter and credentialless repository transport do not yet exist, Product Owner explicitly authorized a one-task bootstrap exception:

```text
Task: VSP-AI02-001T
Primary Developer: Codex Development Agent
Required Independent Reviewer: Separate Codex Independent Reviewer
Claude Cross Review: REQUIRED
BOOTSTRAP_PUBLICATION_IDENTITY_EXCEPTION = VSP-AI02-001T_ONLY
```

This exception must not be reused for C1/C2, later AI02 phases, product tasks, security tasks, Pilot, GA, or version changes. After merge, `VSP-AI02-001T` still is not complete until the post-merge real GitHub App transport smoke proves operational publication without Product Owner manual transport.
