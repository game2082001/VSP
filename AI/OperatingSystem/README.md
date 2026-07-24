# AI Operating System

**Status:** Draft
**Owner:** AI Development Kit
**Last Updated:** 2026-07-24
**Next Task:** TBD — Decision Engine and Risk Matrix remain candidates for future standalone documents, if a future task determines they should not stay embedded in `AI_OPERATING_SYSTEM.md`.

---

## Purpose

This directory holds the rules that govern how an AI agent operates autonomously in this repository: what it may decide on its own, what it must stop and ask about, and how it selects and sequences work.

## Contents

| Document | Status |
|---|---|
| [`AI_OPERATING_SYSTEM.md`](AI_OPERATING_SYSTEM.md) | **Established** (Task-AI01-002) — Purpose, Agent Roles, AI Startup Sequence, Task Intake Modes (including Continuation Mode), Current-State Analysis, Task Selection Rules (including Task Dependency Check), Risk Classification, Approval Boundary, Implementation Planning, Implementation Rules, Autonomous Execution Loop, Build/Test Rules, Documentation Update Rules, AI Memory Rules, Worktree Safety, Prohibited Actions, Self-Review Checklist, Completion Report Standard, Failure/Escalation Rules, Relationship to Other Documents, Multi-Agent Collaboration |

## Future Contents (Not Yet Established)

- Autonomous Development Workflow (as a dedicated document, if the Autonomous Execution Loop in `AI_OPERATING_SYSTEM.md` §11 is later judged insufficient)
- Decision Engine
- Risk Matrix

None of the above exist yet as standalone documents. Risk classification and approval-boundary logic currently live inside `AI_OPERATING_SYSTEM.md` §7–§8.

## Related Source Documents

`AI_OPERATING_SYSTEM.md` references, and does not duplicate, the following (see its §20 for the full mapping):

- [`AGENTS.md`](../../AGENTS.md) — Approval Boundary section (no files may be modified before explicit approval)
- [`Docs/AI_DEVELOPMENT_WORKFLOW.md`](../../Docs/AI_DEVELOPMENT_WORKFLOW.md) — Task → Analysis → ... → Commit workflow
- [`Docs/WORKFLOW/IMPLEMENT_TASK.md`](../../Docs/WORKFLOW/IMPLEMENT_TASK.md) — Task Plan format and approval requirement
- [`Docs/DEVELOPMENT_ROLES.md`](../../Docs/DEVELOPMENT_ROLES.md) — roles and responsibilities

## Out of Scope

This README only indexes `AI_OPERATING_SYSTEM.md` and lists what remains unestablished in this directory. It does not itself define any operating rule — see `AI_OPERATING_SYSTEM.md` for those.
