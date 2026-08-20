# Structured State Schema

**Task:** AI01-008

State artifacts are stored as JSON and treated as recoverable evidence, not as a replacement for GitHub or Git.

Recommended path:

```text
AI/Orchestrator/State/<pr-number>.state.json
```

## Required Fields

```json
{
  "schemaVersion": "1.0",
  "taskId": "AI01-008",
  "prNumber": 0,
  "repository": "game2082001/VSP",
  "baseBranch": "main",
  "headBranch": "",
  "approvedScope": "",
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
  "assignedImplementationRole": "",
  "implementationContextId": "",
  "codexWorkerTouchedPr": false,
  "independentReviewerRole": "Codex Independent Reviewer",
  "independentReviewerModel": "gpt-5.6-luna medium",
  "independentReviewerContextId": "",
  "ciStatus": "UNKNOWN",
  "claudeReviewStatus": "UNKNOWN",
  "independentReviewStatus": "NOT_REQUESTED",
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
  "remainingKnownRisks": [],
  "readyForMerge": false,
  "updatedAtUtc": ""
}
```

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
