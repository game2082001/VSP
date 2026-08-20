# CLAUDE.md

Claude Code-specific guidance. Project-wide AI agent rules live in [AGENTS.md](AGENTS.md) — read that first, then [Docs/AI_DEVELOPMENT_WORKFLOW.md](Docs/AI_DEVELOPMENT_WORKFLOW.md), before starting any task.

## Claude Code Specific Notes

- Run build and test commands via the Bash/PowerShell tool as documented in [Docs/04_DEVELOPMENT_GUIDE.md](Docs/04_DEVELOPMENT_GUIDE.md).
- Do not run `git add`, `git commit`, or `git push` by default. Git operations are performed by the user (Product Owner), per [Docs/DEVELOPMENT_ROLES.md](Docs/DEVELOPMENT_ROLES.md). The Product Owner may explicitly authorize a task-scoped Commit Gate ([AI/OperatingSystem/AI_OPERATING_SYSTEM.md](AI/OperatingSystem/AI_OPERATING_SYSTEM.md) §23), permitting approved staging/commit execution for that task only. `git push` always requires its own separate, explicit authorization unless an approved orchestrated lifecycle explicitly pre-authorizes push for that task. A Commit Gate alone never implies push authority, and no standing Git authority is granted to Claude Code by this note.
- Follow the Task Plan → Approval → Implementation flow described in [Docs/AI_DEVELOPMENT_WORKFLOW.md](Docs/AI_DEVELOPMENT_WORKFLOW.md). Do not edit files before Approval.
- For orchestrated PR remediation, follow [AI/Orchestrator/AGENT_CONTRACTS.md](AI/Orchestrator/AGENT_CONTRACTS.md) and [AI/Orchestrator/REMEDIATION_POLICY.md](AI/Orchestrator/REMEDIATION_POLICY.md). Handoff must happen through PR state, Git history, workflow/check results, and structured artifacts, not manual chat copy/paste.
