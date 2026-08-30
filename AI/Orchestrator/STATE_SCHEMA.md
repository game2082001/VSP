# Structured State Schema

**Scope:** Reusable AI01 task lifecycle

State artifacts are stored as JSON and treated as recoverable evidence, not as a replacement for GitHub or Git.

Recommended path:

```text
AI/Orchestrator/State/<pr-number>.state.json
```

Initial AI02 state may also be created before a PR exists from a Product Owner-approved task manifest:

```text
AI/Orchestrator/State/<task-id>.state.json
```

## Required Fields

```json
{
  "schemaVersion": "1.0",
  "taskId": "AI01-XXX",
  "taskManifestPath": "",
  "taskManifestStatus": "MISSING|VALID|INVALID",
  "classification": "SMALL|MEDIUM|MAJOR|CRITICAL",
  "classificationConsistencyStatus": "UNKNOWN|VALID|INVALID",
  "prNumber": 0,
  "repository": "game2082001/VSP",
  "baseBranch": "main",
  "headBranch": "",
  "approvedScope": "",
  "outOfScope": "",
  "stopConditions": [],
  "productOwnerAuthorizationEvidence": {
    "authorized": false,
    "authorizedBy": "",
    "authorizedAtUtc": "",
    "evidenceSource": "",
    "evidenceUrl": "",
    "approvalSummary": ""
  },
  "executionAuthorization": {
    "implementation": false,
    "localValidation": false,
    "commit": false,
    "pushFeatureBranch": false,
    "openOrUpdatePr": false,
    "ciGate": false,
    "automatedReviewGate": false,
    "requiredIndependentReview": false,
    "inScopeRemediation": false,
    "remediationCommitPushAndGates": false
  },
  "riskCeiling": "HIGH",
  "currentStage": "PLANNED",
  "primaryDeveloperRole": "",
  "primaryDeveloperAdapter": "claude|codex|manual",
  "assignedImplementationRole": "",
  "implementationContextId": "",
  "implementationRunId": "",
  "codexWorkerTouchedPr": false,
  "independentReviewerRole": "Codex Independent Reviewer",
  "independentReviewerModel": "gpt-5.6-luna medium",
  "independentReviewerContextId": "",
  "developerEqualsReviewer": false,
  "ciStatus": "UNKNOWN",
  "claudeReviewStatus": "UNKNOWN",
  "environmentAuthority": {
    "sourceAuthority": "GitHub game2082001/VSP",
    "windowsCiAuthority": "VSP-Server-01 on DESKTOP-COVI6R2",
    "interactiveGuiAuthority": "VSP-GUI-01 on YOUSIN",
    "releaseEvidenceAuthority": "workflow-defined exact source SHA and runner evidence",
    "agentSandboxAuthority": "NON_AUTHORITATIVE_DIAGNOSTIC"
  },
  "sandboxDiagnostics": [],
  "sandboxAnomalyDisposition": "NONE|RECORDED_AND_RECONCILED|STOP_REQUIRED",
  "claudeCrossReviewRequired": false,
  "claudeCrossReviewRunId": "",
  "claudeCrossReviewStatus": "N/A",
  "independentReviewStatus": "NOT_REQUESTED",
  "findings": [],
  "remediationCount": 0,
  "remediationLimit": 2,
  "tokenBudget": {
    "total": 0,
    "implementation": 0,
    "review": 0,
    "remediation": 0,
    "softStopPercent": 80,
    "hardStopPercent": 100
  },
  "tokenSpentEstimate": 0,
  "stopCondition": "",
  "productOwnerDecision": {
    "required": false,
    "reason": "",
    "recommended": "",
    "why": "",
    "ifApproved": "",
    "alternatives": []
  },
  "lastKnownCommit": "",
  "observedHeadCommit": "",
  "lastWorkflowRunIds": [],
  "repositoryTransport": {
    "required": false,
    "status": "NOT_REQUESTED|PENDING|PUBLISHED|FAILED",
    "requestPath": "",
    "workflowRunId": "",
    "workflowRunAttempt": "",
    "appSlug": "",
    "baseSha": "",
    "targetBranch": "",
    "treeSha": "",
    "commitSha": "",
    "prNumber": 0,
    "remoteTreeMatchesRequest": false,
    "singleAtomicCommit": false,
    "productOwnerManualTransport": false,
    "agentCredentialExposure": false,
    "merged": false
  },
  "remainingKnownRisks": [],
  "scopeDrift": "NONE",
  "readyForMerge": false,
  "updatedAtUtc": ""
}
```

AI02 fields are authoritative for Product and Engineering PR lifecycle evidence. AI01-compatible consumers may ignore unknown fields, but must not claim `READY_FOR_MERGE` if `developerEqualsReviewer` is true, if required context IDs are missing, or if a sandbox diagnostic anomaly remains unreconciled.

`taskManifestPath`, `taskManifestStatus`, `classificationConsistencyStatus`, and `productOwnerAuthorizationEvidence` record the machine-readable intake contract introduced by VSP-AI02-001B. Missing or invalid manifest authorization must stop implementation before PR creation. A state artifact created before implementation may have blank implementation and reviewer context IDs; those IDs must be populated and proven distinct before `READY_FOR_MERGE`.

`environmentAuthority` records the Product Owner-approved authority matrix used to interpret evidence. Agent sandbox diagnostics are not automatically authoritative gates, but `sandboxDiagnostics` must preserve anomalies and the causal reconciliation. A sandbox anomaly must use `STOP_REQUIRED` if it plausibly indicates current-PR regression, secret exposure, data-loss risk, destructive behavior, or a gap not exercised by the authoritative gate.

`repositoryTransport` records AI02 credentialless publication evidence. A task that uses the AI02 Repository Transport must record the publication request path, trusted workflow run, VSP AI Implementation App slug, approved base SHA, controlled branch, resulting tree/commit/PR, and explicit `remoteTreeMatchesRequest=true`, `singleAtomicCommit=true`, `productOwnerManualTransport=false`, `agentCredentialExposure=false`, and `merged=false` evidence. Missing or false transport evidence is `STOP / NOT READY_FOR_MERGE` for tasks that depend on the transport.

## Stages

- `PLANNED`
- `IMPLEMENTING`
- `PUSHED`
- `WAITING_PARALLEL_GATES`
- `WAITING_INDEPENDENT_REVIEW`
- `REMEDIATION_REQUIRED`
- `REMEDIATING`
- `READY_FOR_MERGE`
- `STOPPED_FOR_PRODUCT_OWNER`
- `FAILED_UNRECOVERABLE`

## Recovery Rule

If structured state disagrees with GitHub PR metadata, Git branch state, or check results, GitHub/Git wins and the state must be reconciled before continuing.
