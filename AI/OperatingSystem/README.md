# AI Operating System

**Status:** Stable
**Owner:** AI Development Kit
**Last Updated:** 2026-08-21
**Next Task:** AI01-013 — PR #7 Governance Delta Reconciliation. Rebuild the durable Pipeline Governance delta from current `main` truth without modifying, rebasing, merging, or closing PR #7. See `AI/VERSION.md` (current version 1.3.0).

---

## Purpose

This directory holds the rules that govern how an AI agent operates autonomously in this repository: what it may decide on its own, what it must stop and ask about, and how it selects and sequences work — at both single-Task and multi-Task Epic scope.

## Contents

| Document | Status |
|---|---|
| [`AI_OPERATING_SYSTEM.md`](AI_OPERATING_SYSTEM.md) | **Established** (Task-AI01-002; refined by Task-AI01-004; stabilized by Task-AI01-005; amended by Task-AI01-006 and later AI01 lifecycle execution) — Foundational axiom, Core Principles (Product First, Vertical Slice Development, Think Before Coding, Simplicity First, Surgical Changes, Goal-Driven Execution), Purpose, Agent Roles (now four, incl. Codex/Independent Review Agent), AI Startup Sequence, Task Intake Modes (including Continuation Mode), Current-State Analysis, Task Selection Rules (including Task Dependency Check), Risk Classification, Approval Boundary (eight Stop Conditions), Implementation Planning, Implementation Rules, Autonomous Execution Loop, Build/Test Rules, Documentation Update Rules, AI Memory Rules, Worktree Safety, Prohibited Actions, Self-Review Checklist, Completion Report Standard, Failure/Escalation Rules, Relationship to Other Documents, Multi-Agent Collaboration, Decision Authority Model (Autonomous / Conditional / Approval Required, Implementation Authority, Implementation Ownership, Product Owner Principle), Commit Gate (§23), TDD Policy (§24), Hardware Gate (§25), Release Gate (§26), Independent Review Policy (§27), Standard Development Lifecycle (§28) |
| [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md) | **Established** (Task-AI01-003; refined by Task-AI01-004; stabilized by Task-AI01-005; minor Commit Gate delegation clarification by Task-AI01-006 and later AI01 lifecycle execution) — Epic Governance: foundational axiom, Epic Definition, Internal Tasks vs. External Epics, Product Owner Responsibilities (including Product Roadmap Priority), AI Agent Responsibilities, Governance, Epic Execution Model (Product Roadmap → Approval → Autonomous Execution → Epic Review → Commit → Next Epic), Long-Running Autonomous Execution, Autonomous Recovery, Continuous Validation, Epic Completion Reporting |
| [`PIPELINE_GOVERNANCE.md`](PIPELINE_GOVERNANCE.md) | **Established** (AI01-013, resolving `GB-007` from current `main` truth) — Repository-level Commit → Push → PR → [Windows CI \|\| Claude Automated Review] → Codex Independent Review when required → Merge Gate governance summary; aligns with AI01-008 Orchestrator and AI01-009 post-merge validation; does not modify PR #7 |

## Governance Backlog

Entries here are reserved, not scheduled. Per the AI Kit Stability Policy (`AI/VERSION.md`), a Governance Backlog entry is opened only when a real Epic exposes a governance defect, and is acted on only with explicit Product Owner approval. This is not a general to-do list for Kit completeness — items below are real, already-surfaced gaps deliberately deferred, not speculative additions.

`GB-001`, `GB-005`, and `GB-006` were triggered and approved for action by the Product Owner in Task-AI01-006 Phase 1 (2026-08-15), based on real evidence from Task-AI00B (RC1 Clean Baseline Commit Execution), and marked **Resolved** by the Task-AI01-006 Phase 2B amendment applied the same day. Phase 2C Independent Review (Codex) then found that GB-006's resolution was incomplete outside the two files Phase 2B touched — see GB-006 below — among 3 findings; Phase 2D (2026-08-15) remediated all three. Later AI01 lifecycle execution on `main` supersedes the old branch-local "pending acceptance" status; the historical Task-AI01-006 record remains in `AI/VERSION.md` for provenance.

| ID | Item | Trigger to revisit | Status |
|---|---|---|---|
| GB-001 | `CLAUDE.md` Integration (deferred from the retired "Next Review: AI01-007" mechanism) | A real Epic touching Claude Code-specific tooling behavior exposes a gap this Kit doesn't cover | **Resolved** — `CLAUDE.md`'s Git-prohibition bullet now cross-references `AI_OPERATING_SYSTEM.md` §23 Commit Gate instead of stating only a bare prohibition; default preserved, no standing authority granted. Task-AI01-006 Phase 2B, 2026-08-15. |
| GB-002 | `Docs/00_AI_CONTEXT.md` is stale and inconsistent with `AGENTS.md`/`CLAUDE.md`/`Docs/AI_DEVELOPMENT_WORKFLOW.md` (flagged, not resolved, in `AI/README.md`); it also independently retains a live "Codex = Developer/Coding/Git-prohibited-role" section (§7–§12) of the same shape Phase 2D reconciled in `Docs/04_DEVELOPMENT_GUIDE.md`, confirmed by the Phase 2D repository-wide search | A real Epic touching onboarding/context docs surfaces actual confusion from this staleness | Reserved |
| GB-003 | Decision Engine / Risk Matrix extraction into standalone documents | Risk Classification (`AI_OPERATING_SYSTEM.md` §7) or the Decision Authority Model (§22) grows complex enough that embedding them there stops working | Reserved |
| GB-004 | `Docs/02_CODING_RULES.md` §18 "Coding Philosophy" priority order (Architecture first) vs. Principle 0 Product First — cross-referenced, not reconciled, by Task-AI01-005 | A real Epic surfaces an actual conflict between the two priority orders in practice | Reserved |
| GB-005 | Codex has no distinct role or responsibility in `AI_OPERATING_SYSTEM.md` §2 Agent Roles — named only generically as "any future AI agent," with no anchor for an independent-review or assigned-second-implementation function | Surfaced by Task-AI01 Phase 0 analysis (2026-08-15) against the actual Product Owner → ChatGPT → Claude Code → Codex model now in use | **Resolved** — `AI_OPERATING_SYSTEM.md` §2 now names Codex as a distinct Agent Role (Independent Review Agent) with explicit responsibilities, and new §27 Independent Review Policy defines its mandatory-trigger conditions. Task-AI01-006 Phase 2B, 2026-08-15. |
| GB-006 | `Docs/DEVELOPMENT_ROLES.md` binds "Developer" to Codex while `AI_OPERATING_SYSTEM.md` §2 binds "Implementation Agent" to Claude Code — conflicting tool-to-role assignment; actual practice (Task-AI00B) has Claude Code performing all Developer/Implementation-Agent work | Surfaced by Task-AI01 Phase 0 analysis (2026-08-15); `DEVELOPMENT_ROLES.md`'s stated binding diverges from actual Claude Code usage | **Resolved** — `Docs/DEVELOPMENT_ROLES.md` §二's Developer role's "目前預設工具" (current default tool) is now explicitly Claude Code, matching `AI_OPERATING_SYSTEM.md` §2's Implementation Agent=Claude Code; Codex moved to its own new Independent Review Agent role in both documents. Task-AI01-006 Phase 2B, 2026-08-15. Phase 2C Independent Review found this resolution was scoped only to those two documents — `Docs/04_DEVELOPMENT_GUIDE.md` still lived with the identical Codex=Developer conflict (F-1, MAJOR). Phase 2D (2026-08-15) reconciled `Docs/04_DEVELOPMENT_GUIDE.md` §8/§9/§14/§16 to the same Developer(Implementation Agent)/Codex(Independent Review Agent) split. A Phase 2D repository-wide search found no remaining live contradiction except `Docs/00_AI_CONTEXT.md` (see `GB-002` — separately tracked, already disclaimed as non-authoritative, out of this task's scope). GB-006 is now Resolved for the project's live, actively-referenced governance and process documents; `GB-002`'s pre-existing residual is deliberately deferred, not silently missed. |
| GB-007 | PR pipeline gates existed operationally but not as an Operating System level governance summary: Push, PR, Windows CI, Claude Automated Review, Codex Independent Review requirements, and Merge each carry authority/evidence consequences; PR #7 explored this but became stale after AI01-008/009 | Surfaced by PR #7 review and later AI01-008/009 lifecycle execution | **Resolved by AI01-013** — `PIPELINE_GOVERNANCE.md` preserves the durable higher-level gate model from current `main` truth, cross-references the AI01 Orchestrator as executable policy, and excludes PR #7 stale remediation narrative. PR #7 remains untouched and requires separate evidence-based reconciliation after AI01-013. |

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
