# AI Operating System

**Status:** Stable
**Owner:** AI Development Kit
**Last Updated:** 2026-07-26
**Next Task:** None scheduled. See Governance Backlog below and the AI Kit Stability Policy in `AI/VERSION.md`.

---

## Purpose

This directory holds the rules that govern how an AI agent operates autonomously in this repository: what it may decide on its own, what it must stop and ask about, and how it selects and sequences work — at both single-Task and multi-Task Epic scope.

## Contents

| Document | Status |
|---|---|
| [`AI_OPERATING_SYSTEM.md`](AI_OPERATING_SYSTEM.md) | **Established** (Task-AI01-002; refined by Task-AI01-004; stabilized by Task-AI01-005) — Foundational axiom, Core Principles (Product First, Vertical Slice Development, Think Before Coding, Simplicity First, Surgical Changes, Goal-Driven Execution), Purpose, Agent Roles, AI Startup Sequence, Task Intake Modes (including Continuation Mode), Current-State Analysis, Task Selection Rules (including Task Dependency Check), Risk Classification, Approval Boundary (eight Stop Conditions), Implementation Planning, Implementation Rules, Autonomous Execution Loop, Build/Test Rules, Documentation Update Rules, AI Memory Rules, Worktree Safety, Prohibited Actions, Self-Review Checklist, Completion Report Standard, Failure/Escalation Rules, Relationship to Other Documents, Multi-Agent Collaboration, Decision Authority Model (Autonomous / Conditional / Approval Required, Implementation Authority, Implementation Ownership, Product Owner Principle) |
| [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md) | **Established** (Task-AI01-003; refined by Task-AI01-004; stabilized by Task-AI01-005) — Epic Governance: foundational axiom, Epic Definition, Internal Tasks vs. External Epics, Product Owner Responsibilities (including Product Roadmap Priority), AI Agent Responsibilities, Governance, Epic Execution Model (Product Roadmap → Approval → Autonomous Execution → Epic Review → Commit → Next Epic), Long-Running Autonomous Execution, Autonomous Recovery, Continuous Validation, Epic Completion Reporting |

## Governance Backlog

Entries here are reserved, not scheduled. Per the AI Kit Stability Policy (`AI/VERSION.md`), a Governance Backlog entry is opened only when a real Epic exposes a governance defect, and is acted on only with explicit Product Owner approval. This is not a general to-do list for Kit completeness — items below are real, already-surfaced gaps deliberately deferred, not speculative additions.

| ID | Item | Trigger to revisit |
|---|---|---|
| GB-001 | `CLAUDE.md` Integration (deferred from the retired "Next Review: AI01-007" mechanism) | A real Epic touching Claude Code-specific tooling behavior exposes a gap this Kit doesn't cover |
| GB-002 | `Docs/00_AI_CONTEXT.md` is stale and inconsistent with `AGENTS.md`/`CLAUDE.md`/`Docs/AI_DEVELOPMENT_WORKFLOW.md` (flagged, not resolved, in `AI/README.md`) | A real Epic touching onboarding/context docs surfaces actual confusion from this staleness |
| GB-003 | Decision Engine / Risk Matrix extraction into standalone documents | Risk Classification (`AI_OPERATING_SYSTEM.md` §7) or the Decision Authority Model (§22) grows complex enough that embedding them there stops working |
| GB-004 | `Docs/02_CODING_RULES.md` §18 "Coding Philosophy" priority order (Architecture first) vs. Principle 0 Product First — cross-referenced, not reconciled, by Task-AI01-005 | A real Epic surfaces an actual conflict between the two priority orders in practice |

## Future Contents (Not Yet Established)

- Decision Engine
- Risk Matrix

Neither exists yet as a standalone document; see GB-003. Risk classification and approval-boundary logic currently live inside `AI_OPERATING_SYSTEM.md` §7–§8.

## Related Source Documents

`AI_OPERATING_SYSTEM.md` references, and does not duplicate, the following (see its §20 for the full mapping):

- [`AGENTS.md`](../../AGENTS.md) — Approval Boundary section (no files may be modified before explicit approval)
- [`Docs/AI_DEVELOPMENT_WORKFLOW.md`](../../Docs/AI_DEVELOPMENT_WORKFLOW.md) — Task → Analysis → ... → Commit workflow
- [`Docs/WORKFLOW/IMPLEMENT_TASK.md`](../../Docs/WORKFLOW/IMPLEMENT_TASK.md) — Task Plan format and approval requirement
- [`Docs/DEVELOPMENT_ROLES.md`](../../Docs/DEVELOPMENT_ROLES.md) — roles and responsibilities

## Out of Scope

This README only indexes `AI_OPERATING_SYSTEM.md` and lists what remains unestablished in this directory. It does not itself define any operating rule — see `AI_OPERATING_SYSTEM.md` for those.
