# AI02 Repository Transport Schema

**Status:** Draft
**Established By:** VSP-AI02-001T
**Scope:** Credentialless agent publication requests and GitHub App repository-write evidence.

---

## 1. Purpose

The AI02 Repository Transport is the only approved AI02 path for publishing agent-prepared repository changes without exposing reusable GitHub credentials to Codex or Claude execution contexts.

The trusted boundary is the `AI02 Repository Transport` GitHub Actions workflow. It creates a short-lived VSP AI Implementation GitHub App installation token inside GitHub Actions, validates the publication request, creates one Git tree and one Git commit using Git Data semantics, updates a controlled task branch, opens or updates a pull request, verifies remote equality, and records structured evidence.

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
- `baseSha` does not match current remote `main`;
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
- merge would be required.

---

## 5. Scope Boundary

This transport publishes branches and PRs only. It never merges. Product Owner remains the only merge authority.

Workflow-write permission is not an agent credential and is not available to Codex or Claude contexts. It may be requested inside the trusted GitHub Actions transport boundary only for an explicit AI02 governance task whose manifest authorizes workflow file changes. Any request that changes `.github/workflows/*` without manifest-level approval must fail before publication.
