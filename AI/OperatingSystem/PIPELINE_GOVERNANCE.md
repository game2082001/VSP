# Pipeline Governance

**Status:** Stable
**Owner:** AI Development Kit
**Established By:** AI01-013 — PR #7 Governance Delta Reconciliation
**Last Updated:** 2026-08-21
**Next Review:** Not scheduled. Governed by the AI Kit Stability Policy in `AI/VERSION.md`.

---

## 1. Purpose

This document defines the repository-level Git/GitHub gate sequence between completed implementation work and a possible merge to `main`.

It preserves the durable governance value first explored in PR #7 while aligning the rule text with the current `main` truth established by AI01-008 and AI01-009:

- AI01-008 added the executable PR-based Orchestrator lifecycle.
- AI01-009 added post-merge `main` HEAD validation evidence.
- PR #7's old branch history and remediation narrative are not source of truth for this document.

This document is a higher-level governance summary. The executable routing rules live under `AI/Orchestrator/`.

---

## 2. Gate Sequence

```text
Implementation
  -> Local Validation
  -> Commit Gate
  -> Push Gate
  -> PR Gate
  -> Windows CI Gate
  -> Claude Automated Review / Cross Review Gate when required by AI02 classification
  -> Codex Independent Review Gate (when required)
  -> Merge Gate
```

AI02 task classification is governed by [`TASK_CLASSIFICATION.md`](TASK_CLASSIFICATION.md). Claude review is not mandatory for every PR: it is not required by default for SMALL tasks, risk-based for MEDIUM tasks, classification-driven for MAJOR tasks, and mandatory for CRITICAL tasks. When Claude review is required, current-head PASS evidence is part of merge eligibility.

## 3. Gate Rules

### Commit Gate

Governed by `AI_OPERATING_SYSTEM.md` §23. AI agents have no standing commit authority. Commit execution requires task-scoped Product Owner authorization.

### Push Gate

Push authority is separate from Commit Gate authority. A task-scoped commit authorization does not imply push authorization.

Allowed push scope must name the target feature branch. Force push is not included unless explicitly authorized by the Product Owner.

### PR Gate

Opening or updating a PR is a separate gate. The PR must be scoped to the approved task plan and must not silently include unrelated work.

### Windows CI Gate

The Windows CI gate uses `.github/workflows/vsp-windows-ci.yml`.

For PR lifecycle validation, the gate must be evaluated against the PR current head or GitHub's current PR merge ref evidence. A red CI gate blocks Independent Review and Merge until triaged.

### Claude Automated Review / Cross Review Gate

The Claude Automated Review gate uses `.github/workflows/claude-code-review.yml`.

Automated Review is evidence, not final approval. It never substitutes for any required Codex Independent Review. AI02 distinguishes optional/risk-based Claude review from mandatory Claude Cross Review; CRITICAL tasks always require Claude Cross Review evidence.

### Codex Independent Review Gate

Independent Review is separate from implementation and automated review.

Trigger criteria are governed by `AI_OPERATING_SYSTEM.md` §27. Independent Review is mandatory for the §27 significant-change categories and for any approved task or PR lifecycle that explicitly requires it. Proportional review applies where §27 allows it for LOW-risk or documentation-only work.

The reviewer must inspect:

- the current PR diff,
- current-head CI evidence,
- current-head Claude Automated Review evidence,
- unresolved review comments or findings.

A remediation commit invalidates the previous Independent Review result and requires re-review.

### Merge Gate

Merge eligibility requires:

1. Windows CI PASS.
2. Claude Automated Review / Cross Review PASS when required by the AI02 task classification.
3. Codex Independent Review APPROVED when required by §27, by `TASK_CLASSIFICATION.md`, or by the approved task/PR lifecycle.
4. Developer / Independent Reviewer separation evidence.
5. No unresolved in-scope findings.
6. No PR head drift after approval evidence.

Merge eligibility is not merge authorization. Merge remains a Product Owner decision unless a future task explicitly grants autonomous merge authority. Current AI01 Orchestrator policy stops at `READY_FOR_MERGE`.

---

## 4. Recovery And Remediation

If a gate fails, the Orchestrator follows `AI/Orchestrator/ROUTER_POLICY.md` and `AI/Orchestrator/REMEDIATION_POLICY.md`.

In-scope remediation may proceed only when the approved task plan pre-authorizes it. Scope expansion, security changes, public API changes, database schema changes, unrecoverable CI failures, or exhausted retry budgets remain Stop Conditions.

The normal retry path is:

```text
REMEDIATION_REQUIRED
  -> in-scope remediation
  -> Local Validation
  -> Commit / Push through approved path
  -> [Windows CI Gate || Claude Automated Review Gate]
  -> Codex Independent Re-Review when required
```

The retry path never grants merge authority.

---

## 5. Post-Merge Main Validation

Post-merge validation is not part of PR merge eligibility. It is the read-only evidence path that runs after `main` advances.

For post-merge rules, see `AI/Orchestrator/POST_MERGE_MAIN_VALIDATION.md`.

Post-merge validation must:

- target the exact `main` commit SHA,
- produce Windows CI evidence,
- avoid implementation remediation,
- avoid PR creation,
- avoid autonomous merge,
- avoid writing back to `main`.

---

## 6. Relationship To Current Documents

| Document | Relationship |
|---|---|
| `AI_OPERATING_SYSTEM.md` | Owns core role, risk, approval, Stop Condition, Commit Gate, and Independent Review policy |
| `AUTONOMOUS_DEVELOPMENT.md` | Owns Epic-level execution and recovery rules |
| `AI/Orchestrator/ROUTER_POLICY.md` | Owns executable AI01 routing order and gate transitions |
| `AI/Orchestrator/REMEDIATION_POLICY.md` | Owns bounded remediation loop behavior |
| `AI/Orchestrator/ROLE_SEPARATION.md` | Owns implementation/review identity separation |
| `AI/Orchestrator/POST_MERGE_MAIN_VALIDATION.md` | Owns post-merge main-head validation evidence rules |

---

## 7. Out Of Scope

This document does not:

- modify workflow configuration,
- grant autonomous merge,
- modify PR #7,
- close PR #7,
- change production code,
- change RTSP, decoder, guard, or diagnostic behavior,
- replace the AI01 Orchestrator documents.
