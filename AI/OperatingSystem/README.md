# AI Operating System

**Status:** Stable
**Owner:** AI Development Kit
**Last Updated:** 2026-08-16
**Next Task:** Task-AI01-007 GB-007 Pipeline Governance Implementation — PR #7 (`task/ai01-007-pipeline-governance`) open; CI Gate and Automated Review Gate PASS; Codex Independent Review returned REMEDIATION REQUIRED (3 findings, 2026-08-16), remediated same day within original scope, awaiting Codex Independent Re-Review. Commit/Push/PR authorized for this Task, Autonomous Merge withheld (manual Merge bootstrap). See the Governance Backlog below and `AI/VERSION.md` (current version 1.3.0).

---

## Purpose

This directory holds the rules that govern how an AI agent operates autonomously in this repository: what it may decide on its own, what it must stop and ask about, and how it selects and sequences work — at both single-Task and multi-Task Epic scope.

## Contents

| Document | Status |
|---|---|
| [`AI_OPERATING_SYSTEM.md`](AI_OPERATING_SYSTEM.md) | **Established** (Task-AI01-002; refined by Task-AI01-004; stabilized by Task-AI01-005; amended by Task-AI01-006 — pending Independent Review/acceptance) — Foundational axiom, Core Principles (Product First, Vertical Slice Development, Think Before Coding, Simplicity First, Surgical Changes, Goal-Driven Execution), Purpose, Agent Roles (now four, incl. Codex/Independent Review Agent), AI Startup Sequence, Task Intake Modes (including Continuation Mode), Current-State Analysis, Task Selection Rules (including Task Dependency Check), Risk Classification, Approval Boundary (eight Stop Conditions), Implementation Planning, Implementation Rules, Autonomous Execution Loop, Build/Test Rules, Documentation Update Rules, AI Memory Rules, Worktree Safety, Prohibited Actions, Self-Review Checklist, Completion Report Standard, Failure/Escalation Rules, Relationship to Other Documents, Multi-Agent Collaboration, Decision Authority Model (Autonomous / Conditional / Approval Required, Implementation Authority, Implementation Ownership, Product Owner Principle), Commit Gate (§23), TDD Policy (§24), Hardware Gate (§25), Release Gate (§26), Independent Review Policy (§27), Standard Development Lifecycle (§28) |
| [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md) | **Established** (Task-AI01-003; refined by Task-AI01-004; stabilized by Task-AI01-005; minor Commit Gate delegation clarification by Task-AI01-006 — pending Independent Review/acceptance) — Epic Governance: foundational axiom, Epic Definition, Internal Tasks vs. External Epics, Product Owner Responsibilities (including Product Roadmap Priority), AI Agent Responsibilities, Governance, Epic Execution Model (Product Roadmap → Approval → Autonomous Execution → Epic Review → Commit → Next Epic), Long-Running Autonomous Execution, Autonomous Recovery, Continuous Validation, Epic Completion Reporting |
| [`PIPELINE_GOVERNANCE.md`](PIPELINE_GOVERNANCE.md) | **Established** (Task-AI01-007, resolving `GB-007` — pending Independent Review/acceptance) — Pipeline Governance: the seven-Gate Commit → Push → PR → CI → Automated Review → Independent Review → Merge sequence; formalizes Push/PR/CI/Automated Review/Merge Gates alongside the existing Commit Gate (§23) and Independent Review Policy (§27); corrects Independent Review Gate timing to run after CI/Automated Review and before Merge, against the final PR diff; defines Scoped Option B bootstrap rollout and a pipeline-scoped Autonomous Recovery Loop |

## Governance Backlog

Entries here are reserved, not scheduled. Per the AI Kit Stability Policy (`AI/VERSION.md`), a Governance Backlog entry is opened only when a real Epic exposes a governance defect, and is acted on only with explicit Product Owner approval. This is not a general to-do list for Kit completeness — items below are real, already-surfaced gaps deliberately deferred, not speculative additions.

`GB-001`, `GB-005`, and `GB-006` were triggered and approved for action by the Product Owner in Task-AI01-006 Phase 1 (2026-08-15), based on real evidence from Task-AI00B (RC1 Clean Baseline Commit Execution), and marked **Resolved** by the Task-AI01-006 Phase 2B amendment applied the same day. Phase 2C Independent Review (Codex) then found that GB-006's resolution was incomplete outside the two files Phase 2B touched — see GB-006 below — among 3 findings; Phase 2D (2026-08-15) remediated all three. "Resolved" here means the specific diff that closes each entry's stated gap has been applied to the working tree — it is **not yet committed, not yet independently re-reviewed by Codex, and not yet Product Owner-accepted**; see `AI/VERSION.md` v1.2.0 and the Task-AI01-006 Phase 2D remediation record.

| ID | Item | Trigger to revisit | Status |
|---|---|---|---|
| GB-001 | `CLAUDE.md` Integration (deferred from the retired "Next Review: AI01-007" mechanism) | A real Epic touching Claude Code-specific tooling behavior exposes a gap this Kit doesn't cover | **Resolved** (pending re-review/acceptance) — `CLAUDE.md`'s Git-prohibition bullet now cross-references `AI_OPERATING_SYSTEM.md` §23 Commit Gate instead of stating only a bare prohibition; default preserved, no standing authority granted. Task-AI01-006 Phase 2B, 2026-08-15. |
| GB-002 | `Docs/00_AI_CONTEXT.md` is stale and inconsistent with `AGENTS.md`/`CLAUDE.md`/`Docs/AI_DEVELOPMENT_WORKFLOW.md` (flagged, not resolved, in `AI/README.md`); it also independently retains a live "Codex = Developer/Coding/Git-prohibited-role" section (§7–§12) of the same shape Phase 2D reconciled in `Docs/04_DEVELOPMENT_GUIDE.md`, confirmed by the Phase 2D repository-wide search | A real Epic touching onboarding/context docs surfaces actual confusion from this staleness | Reserved |
| GB-003 | Decision Engine / Risk Matrix extraction into standalone documents | Risk Classification (`AI_OPERATING_SYSTEM.md` §7) or the Decision Authority Model (§22) grows complex enough that embedding them there stops working | Reserved |
| GB-004 | `Docs/02_CODING_RULES.md` §18 "Coding Philosophy" priority order (Architecture first) vs. Principle 0 Product First — cross-referenced, not reconciled, by Task-AI01-005 | A real Epic surfaces an actual conflict between the two priority orders in practice | Reserved |
| GB-005 | Codex has no distinct role or responsibility in `AI_OPERATING_SYSTEM.md` §2 Agent Roles — named only generically as "any future AI agent," with no anchor for an independent-review or assigned-second-implementation function | Surfaced by Task-AI01 Phase 0 analysis (2026-08-15) against the actual Product Owner → ChatGPT → Claude Code → Codex model now in use | **Resolved** (pending re-review/acceptance) — `AI_OPERATING_SYSTEM.md` §2 now names Codex as a distinct Agent Role (Independent Review Agent) with explicit responsibilities, and new §27 Independent Review Policy defines its mandatory-trigger conditions. Task-AI01-006 Phase 2B, 2026-08-15. |
| GB-006 | `Docs/DEVELOPMENT_ROLES.md` binds "Developer" to Codex while `AI_OPERATING_SYSTEM.md` §2 binds "Implementation Agent" to Claude Code — conflicting tool-to-role assignment; actual practice (Task-AI00B) has Claude Code performing all Developer/Implementation-Agent work | Surfaced by Task-AI01 Phase 0 analysis (2026-08-15); `DEVELOPMENT_ROLES.md`'s stated binding diverges from actual Claude Code usage | **Resolved** (pending re-review/acceptance) — `Docs/DEVELOPMENT_ROLES.md` §二's Developer role's "目前預設工具" (current default tool) is now explicitly Claude Code, matching `AI_OPERATING_SYSTEM.md` §2's Implementation Agent=Claude Code; Codex moved to its own new Independent Review Agent role in both documents. Task-AI01-006 Phase 2B, 2026-08-15. Phase 2C Independent Review found this resolution was scoped only to those two documents — `Docs/04_DEVELOPMENT_GUIDE.md` still lived with the identical Codex=Developer conflict (F-1, MAJOR). Phase 2D (2026-08-15) reconciled `Docs/04_DEVELOPMENT_GUIDE.md` §8/§9/§14/§16 to the same Developer(Implementation Agent)/Codex(Independent Review Agent) split. A Phase 2D repository-wide search found no remaining live contradiction except `Docs/00_AI_CONTEXT.md` (see `GB-002` — separately tracked, already disclaimed as non-authoritative, out of this task's scope). GB-006 is now Resolved for the project's live, actively-referenced governance and process documents; `GB-002`'s pre-existing residual is deliberately deferred, not silently missed. |
| GB-007 | No named, gated pipeline existed between Commit (§23) and Merge — Push, PR creation, CI, Automated Review, and Merge already carried real authority/evidence consequences (CI and Automated Review already run in production via `.github/workflows/vsp-windows-ci.yml` and `claude-code-review.yml`) but had no formal Gate, default, or evidence requirement; separately, the originally proposed Task Plan sequenced Independent Review *before* Commit/Push/PR/CI, which would detach its evidence from the actual final merge candidate once remediation changed the diff | Surfaced during Task-AI01-007 Task Plan review (Product Owner correction, 2026-08-16) | **Resolved** — `PIPELINE_GOVERNANCE.md` (new) defines the seven-Gate Commit → Push → PR → CI → Automated Review → Independent Review → Merge sequence, cross-referencing §23/§27 rather than duplicating them, and fixes Independent Review Gate timing to run after CI/Automated Review Gate against the final PR diff, before Merge Gate. Task-AI01-007, 2026-08-16. Autonomous Merge is explicitly not granted by this resolution — see `PIPELINE_GOVERNANCE.md` §5 Scoped Option B. Pending Independent Review and Product Owner acceptance. |

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
