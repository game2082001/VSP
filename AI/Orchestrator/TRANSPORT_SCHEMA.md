# AI02 Repository Transport Schema

**Status:** Draft
**Established By:** VSP-AI02-001T
**Scope:** Credentialless agent publication requests and GitHub App repository-write evidence.

---

## 1. Purpose

The AI02 Repository Transport is the only approved AI02 path for publishing agent-prepared repository changes without exposing reusable GitHub credentials to Codex or Claude execution contexts.

The trusted boundary is the `AI02 Repository Transport` GitHub Actions workflow. It validates the publication request with read-only workflow credentials, creates a short-lived request-scoped VSP AI Implementation GitHub App installation token inside GitHub Actions, creates one Git tree and one Git commit using Git Data semantics, updates a controlled task branch, opens or updates a pull request, verifies remote equality, and records structured evidence.

Agents must not receive:

- GitHub App private keys
- installation tokens
- PATs
- reusable repository credentials

---

## 2. Publication Request

Recommended path:

```text
AI/Orchestrator/Transport/<task-id>.publication-request.json
```

Required shape:

```json
{
  "schemaVersion": "1.0",
  "taskId": "VSP-AI02-001T",
  "title": "VSP-AI02-001T Repository Transport",
  "repository": "game2082001/VSP",
  "baseSha": "0000000000000000000000000000000000000000",
  "baseBinding": "EXACT",
  "targetBranch": "ai02/vsp-ai02-001t/repository-transport",
  "commitMessage": "VSP-AI02-001T: add repository transport",
  "manifestPath": "AI/Orchestrator/Manifests/VSP-AI02-001T.manifest.json",
  "statePath": "AI/Orchestrator/State/VSP-AI02-001T.state.json",
  "allowWorkflowChanges": true,
  "openPullRequest": true,
  "approvedFiles": [
    ".github/workflows/ai02-repository-transport.yml"
  ],
  "files": [
    {
      "path": ".github/workflows/ai02-repository-transport.yml",
      "mode": "100644",
      "sha256": "64 lowercase hex characters",
      "contentBase64": "base64 encoded UTF-8 file content"
    }
  ]
}
```

The file set in `files` must exactly match `approvedFiles`. Every file content hash is recomputed inside the workflow before any write credential is created.

The request `approvedFiles` list is not self-authorizing. It must also match the Product Owner-approved `repositoryTransport.approvedFiles` allowlist in the referenced AI02 Task Manifest. A request may set `allowWorkflowChanges=true` only when the manifest also sets `repositoryTransport.allowWorkflowChanges=true`.

`baseBinding` defaults to `EXACT`. Normal Product, SEC, UI, PLAYER, release, and engineering tasks must use `EXACT`, where `baseSha` must match the current remote `main` at execution time. The only approved non-default mode is `DISPATCH_MAIN`, restricted to Product Owner-approved AI02 infrastructure smoke fixtures. For `DISPATCH_MAIN`, the workflow resolves current remote `main` at dispatch time, records that value as `executionBaseSha`, uses it as the commit parent, and rechecks remote `main` immediately before branch ref publication. The request must not supply `executionBaseSha`.

## 2.1 Request-Driven GitHub App Permissions

The transport must request the minimum GitHub App token permissions required by the already-validated request.

For requests with no workflow file changes, the transport requests only:

- `contents: write` for Git Data blob/tree/commit/ref publication;
- `pull-requests: write` only when `openPullRequest=true`.

The non-workflow path must not request `workflows: write`, `issues: write`, or `actions: read`.

For requests that include `.github/workflows/*` changes, validation must first prove `allowWorkflowChanges=true` and manifest-level workflow authorization. Only then may the trusted workflow request `workflows: write` in addition to the publication permissions, and `pull-requests: write` remains conditional on `openPullRequest=true`. If the VSP AI Implementation GitHub App installation does not possess workflow-write permission, token creation must fail closed before any Git Data write, branch creation, or PR creation. The transport must not silently downgrade a workflow-changing request into a non-workflow publication.

---

## 3. Publication Result

Recommended path:

```text
AI/Orchestrator/Transport/publication-result.json
```

Required shape:

```json
{
  "schemaVersion": "1.0",
  "taskId": "VSP-AI02-001T",
  "repository": "game2082001/VSP",
  "approvedBaseSha": "0000000000000000000000000000000000000000",
  "baseBinding": "EXACT",
  "executionBaseSha": "0000000000000000000000000000000000000000",
  "targetBranch": "ai02/vsp-ai02-001t/repository-transport",
  "treeSha": "",
  "commitSha": "",
  "prNumber": 0,
  "appSlug": "vsp-ai-implementation",
  "workflowRunId": "",
  "workflowRunAttempt": "1",
  "remoteTreeMatchesRequest": true,
  "singleAtomicCommit": true,
  "productOwnerManualTransport": false,
  "agentCredentialExposure": false,
  "merged": false,
  "publishedAtUtc": ""
}
```

The result must not contain secrets, tokens, private keys, Authorization headers, or credential-bearing URLs.

---

## 4. Fail-Closed Rules

The transport must stop before publication when:

- repository is not exactly `game2082001/VSP`;
- task manifest or state is malformed, missing, or mismatched;
- Product Owner authorization is missing;
- `baseBinding` is absent or `EXACT` and `baseSha` does not match current remote `main`;
- `baseBinding` is `DISPATCH_MAIN` without explicit manifest and state authorization for an AI02 infrastructure smoke fixture;
- a request supplies `executionBaseSha`;
- `targetBranch` is `main`, a tag, or outside `ai02/<task>/<purpose>`;
- changed files differ from the approved file allowlist;
- a workflow file changes without `allowWorkflowChanges=true`;
- the request file allowlist does not exactly match the manifest `repositoryTransport.approvedFiles` allowlist;
- workflow changes are requested without manifest-level workflow change authorization;
- pull request creation is requested without manifest-level pull request authorization;
- content SHA-256 does not match submitted content;
- GitHub App installation scope is not exactly `game2082001/VSP`;
- the compare range contains more than one commit after publication;
- remote changed-file set differs from the request;
- any remote file hash differs from the request;
- PR creation/update fails;
- the workflow run attempt is not `1`;
- remote `main` drifts between dispatch-time base resolution and branch ref publication;
- merge would be required.

---

## 5. Scope Boundary

This transport publishes branches and PRs only. It never merges. Product Owner remains the only merge authority.

Workflow-write permission is not an agent credential and is not available to Codex or Claude contexts. It may be requested inside the trusted GitHub Actions transport boundary only for an explicit AI02 governance task whose manifest authorizes workflow file changes. Any request that changes `.github/workflows/*` without manifest-level approval must fail before publication.
