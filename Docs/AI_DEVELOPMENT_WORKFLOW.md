# AI Development Workflow

Workflow index for how AI agents execute a Task in VSP. This document describes the workflow only — governing rules live in the linked documents below, not here.

This is the canonical single-Task workflow diagram for VSP. `Docs/DEVELOPMENT_ROLES.md` §3 shows the same workflow at a more granular, VSP-specific step level and defers to this document where the two differ. For how multiple Tasks are sequenced inside an approved Epic — including when pausing between Tasks is, and isn't, required — see [`AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md`](../AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md).

For PR-based autonomous multi-agent work, the executable routing and gate artifacts live under [`AI/Orchestrator/`](../AI/Orchestrator/README.md). The orchestrator must use GitHub PR state, Git history, workflow/check results, and structured artifacts as recovery truth, and the first version stops at `READY_FOR_MERGE` for Product Owner manual merge.

## Workflow

```text
Task
  ↓
Analysis
  ↓
Architecture Review
  ↓
Spec
  ↓
Planning
  ↓
Approval
  ↓
Implementation
  ↓
Technical Review
  ↓
Architecture Review
  ↓
Definition of Done
  ↓
Commit
```

## Further Reading

- Architecture → [01_ARCHITECTURE.md](01_ARCHITECTURE.md), [00_ARCHITECTURE_VISION.md](00_ARCHITECTURE_VISION.md)
- Coding Rules → [02_CODING_RULES.md](02_CODING_RULES.md)
- Roles → [DEVELOPMENT_ROLES.md](DEVELOPMENT_ROLES.md)
- Task Plan Format → [WORKFLOW/IMPLEMENT_TASK.md](WORKFLOW/IMPLEMENT_TASK.md)
- Review → [WORKFLOW/REVIEW_TASK.md](WORKFLOW/REVIEW_TASK.md)
- Project Reference → [PROJECT.md](PROJECT.md)
- AI Operating System → [`AI/OperatingSystem/AI_OPERATING_SYSTEM.md`](../AI/OperatingSystem/AI_OPERATING_SYSTEM.md)
- Epic Governance → [`AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md`](../AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md)
- PR Orchestrator → [`AI/Orchestrator/README.md`](../AI/Orchestrator/README.md)
