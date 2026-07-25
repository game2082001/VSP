# AI Development Kit — Version

**Stable** · **Feature Freeze Enabled** · **Governance Freeze Enabled**

**Current Version:** 1.1.0
**Current Phase:** Stable
**Established By:** Task-AI01-003 — AI Development Kit v1.0 Finalization
**Refined By:** Task-AI01-004 — Governance Refinement; Task-AI01-005 — AI Development Kit v1.1.0 (Stable)
**Last Updated:** 2026-07-26

---

## AI Kit Stability Policy

**Stable is the default and expected state of the AI Development Kit.** The Kit does not require ongoing active development, and an AI Agent should not treat governance work as a standing task or proactively propose more of it. Product Roadmap work has default priority over any further Kit/governance work (see `AUTONOMOUS_DEVELOPMENT.md` §4, Product Roadmap Priority) — this is Principle 0, Product First, applied to the Kit's own maintenance.

**Feature Freeze:** no new governance mechanisms are added to this Kit by default.
**Governance Freeze:** no existing rule in `AI_OPERATING_SYSTEM.md` or `AUTONOMOUS_DEVELOPMENT.md` is changed by default.

Future changes are allowed **only** when a real Epic exposes a governance defect — not a hypothetical one — and the change is approved by the Product Owner. The mechanism for this is the Governance Backlog (`AI/OperatingSystem/README.md`): a defect surfaced during real Epic execution is logged there as a reserved entry, and is acted on only after explicit Product Owner approval. There is no standing "next review" task; the Kit does not schedule its own future work.

---

## What v1.0 Means

v1.0 signals that the **Operating System / Governance layer** of the AI Development Kit is feature-complete and stable: an AI Agent can operate under this kit for both single-Task work (`AI_OPERATING_SYSTEM.md`) and multi-Task, Product-Owner-approved Epic work (`AUTONOMOUS_DEVELOPMENT.md`), with defined roles, risk classification, approval boundaries, worktree safety, recovery, continuous validation, and completion reporting at both altitudes.

v1.0 does **not** mean the entire kit is fully populated. `Architecture/`, `Repository/`, `Standards/`, `Product/`, and `Memory/` remain placeholder-only (README describing future contents, no rules/backlogs/state files yet) — that content is intentionally deferred to future, separately approved tasks and does not block this v1.0 designation.

---

## Phase History

| Version | Date | Phase | Summary |
|---|---|---|---|
| 0.1.0 | 2026-07-24 | Foundation | Created `AI/` directory structure and index READMEs (`OperatingSystem`, `Architecture`, `Repository`, `Standards`, `Product`, `Memory`, `Templates`). No rules, checklists, backlogs, or state files created. Root `CLAUDE.md` and `AGENTS.md` remain the primary entry points. |
| 0.2.0 | 2026-07-24 | AI Operating System Foundation | Added `AI/OperatingSystem/AI_OPERATING_SYSTEM.md`: agent roles, AI startup sequence, task intake modes (including Continuation Mode), current-state analysis, task selection rules (including Task Dependency Check), risk classification, approval boundary, implementation planning/rules, autonomous execution loop, build/test rules, documentation update rules, AI memory rules, worktree safety, prohibited actions, self-review checklist, completion report standard, failure/escalation rules, relationship to other documents, and multi-agent collaboration. Updated `AI/OperatingSystem/README.md` index accordingly. |
| 1.0.0 | 2026-07-24 | Epic Autonomous Development | Added `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md`: Epic Definition, Internal Tasks vs. External Epics, Product Owner / AI Agent responsibilities at Epic scope, Governance, Long-Running Autonomous Execution, Autonomous Recovery, Continuous Validation, and Epic Completion Reporting. Added minimal cross-references from `AI_OPERATING_SYSTEM.md` (§1, §4, §11, §20) to the new document, with no existing Task-level rule changed. Updated `AI/OperatingSystem/README.md` and `AI/README.md` indexes accordingly. |
| 1.0.1 | 2026-07-25 | Governance Refinement | Refined governance based on lessons from the first real Epic execution (Discovery Foundation). Added `AI_OPERATING_SYSTEM.md` §22 Decision Authority Model (Autonomous / Conditional / Approval Required — renamed from the draft "Level 0/1/2" naming to avoid confusion with Risk Classification), Implementation Authority (renamed from "Good Faith Rule"), Implementation Ownership, and the Product Owner Principle ("The Product Owner approves product outcomes, not implementation details"). Clarified that a missing Epic definition (`AUTONOMOUS_DEVELOPMENT.md` §2) remains Approval Required, while a missing *supporting* implementation spec inside an already-approved Epic is governed by Implementation Authority. Mandated "Implementation Complete — Pending Product Owner Acceptance" as the standard completion terminology in both documents' completion-report sections, and forbade the AI Agent from ever declaring Product Acceptance. No existing rule was removed or redesigned. |
| 1.1.0 | 2026-07-26 | Stable (Task-AI01-005) | Final governance update; the Kit enters Stable state under Feature Freeze / Governance Freeze (see AI Kit Stability Policy above). Placed the foundational axiom ("An approved Epic is a complete authorization for implementation within its approved scope" / "The default behaviour is CONTINUE, not STOP") at the top of `AI_OPERATING_SYSTEM.md` and `AUTONOMOUS_DEVELOPMENT.md`. Added a Core Principles section to `AI_OPERATING_SYSTEM.md`: Principle 0 Product First, the Vertical Slice Development strategy, and Principles 1–4 (Think Before Coding, Simplicity First, Surgical Changes, Goal-Driven Execution) — each naming an existing mechanism rather than adding new rule text. Reduced Approval Boundary (§8) to exactly eight Stop Conditions (Product Decision, Scope Expansion, High Risk, Database Schema, Public API, Security, External Package, Unrecoverable Build/Test failure), folding in every prior trigger with none silently dropped, and separated Operational Pre-Flight Checks out as a distinct, non-approval-boundary category. Deduplicated §19 against §8. Added Product Roadmap Priority to `AUTONOMOUS_DEVELOPMENT.md` §4 and redrew the Epic Execution Model to start from and return to the Product Roadmap (`Product Roadmap → Epic Approval → Autonomous Execution → Epic Review → Commit → Next Epic`), with an explicit statement that "Next Epic" requires a fresh approval, not autonomous cross-Epic chaining. Added the Governance Backlog (`AI/OperatingSystem/README.md`) with four reserved entries carried forward from this review, none invented speculatively. Reconciled `Docs/WORKFLOW/IMPLEMENT_TASK.md` (per-Task stop language qualified for Epic Mode; mechanical 8-file threshold removed in favor of Risk Classification) and `Docs/DEVELOPMENT_ROLES.md` (Authority Principle aligned with `AI_OPERATING_SYSTEM.md` §1; rigid role-to-tool binding aligned with §2 Role Overlap; its own workflow diagram deferred to `Docs/AI_DEVELOPMENT_WORKFLOW.md`). Replaced duplicated rule text in `Docs/AI_PLAYBOOK.md` and `Docs/02_CODING_RULES.md` (§15, §17) with cross-references to `AI_OPERATING_SYSTEM.md`. No VSP-specific technical content (naming, MVVM, async policy, folder structure) was altered. |

## Next Phase

Governance work is closed by default (see AI Kit Stability Policy above). Per Product Roadmap Priority (`AUTONOMOUS_DEVELOPMENT.md` §4), work returns to the Product Roadmap. The next Epic is **Epic-005 — Camera Management Workspace**, to be proposed and approved through the normal Epic Execution Model, not through this Kit.
