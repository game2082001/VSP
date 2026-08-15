# Autonomous Development — Epic Governance

**Status:** Stable
**Owner:** AI Development Kit
**Last Updated:** 2026-08-15
**Established By:** Task-AI01-003 — AI Development Kit v1.0 Finalization
**Refined By:** Task-AI01-004 — Governance Refinement; Task-AI01-005 — AI Development Kit v1.1.0 (Stable); Task-AI01-006 — Governance Reconciliation & Multi-Agent Development Lifecycle (v1.2.0), minor Commit Gate delegation clarification only, pending Independent Review and Product Owner acceptance
**Next Review:** Not scheduled. Governed by the AI Kit Stability Policy (see `AI/VERSION.md`) — further changes require a Governance Backlog entry, opened only when a real Epic exposes a governance defect, and approved by the Product Owner.

---

> **An approved Epic is a complete authorization for implementation within its approved scope.**
>
> **The default behaviour is CONTINUE, not STOP.**

Restated from the foundational axiom in [`AI_OPERATING_SYSTEM.md`](AI_OPERATING_SYSTEM.md) (the canonical source — this is not a separate rule). This document exists to define exactly what "approved scope" and "CONTINUE, not STOP" mean at Epic altitude: §2 defines what an approved Epic must specify, §7 defines the execution loop this axiom licenses, and §6 defines the boundaries it does not cross.

---

## 1. Purpose & Scope

[`AI_OPERATING_SYSTEM.md`](AI_OPERATING_SYSTEM.md) governs how an AI Agent executes a single **Task**. This document governs how an AI Agent executes an **Epic** — a Product-Owner-defined body of work spanning multiple Tasks — autonomously, without requiring a full stop-and-restart approval cycle between every constituent Task.

This document does not replace, loosen, or add exceptions to any rule in `AI_OPERATING_SYSTEM.md`. Every Task inside an Epic is still subject to that document's Startup Sequence (§3), Current-State Analysis (§5), Risk Classification (§7), Approval Boundary (§8), Implementation Rules (§10), Build/Test Rules (§12), Worktree Safety (§15), and Prohibited Actions (§16) in full. This document only adds the rules for *sequencing* and *governing* multiple Tasks under one approved umbrella.

Applies to: Claude Code, ChatGPT, Codex, and any future AI Agent, per the same AI-agnostic principle as `AI_OPERATING_SYSTEM.md` §1.

---

## 2. Epic Definition

An **Epic** is an external, Product-Owner-originated, business- or product-scoped unit of work that is expected to require multiple internal Tasks to complete.

This is not a new concept invented by this document — `Docs/03_ROADMAP.md` already uses an informal `EPIC-XX` grouping (for example, `EPIC-01 Device Management`, containing Task-101 through Task-116) to describe exactly this kind of grouping. This document formalizes how an AI Agent behaves once given an assignment at that altitude; it does not redefine, move, or replace that existing roadmap content.

An approved Epic must define:

- **Epic ID** — should follow the existing `EPIC-XX` convention where the Epic corresponds to product-roadmap work, to stay consistent with `Docs/03_ROADMAP.md`.
- **Objective** — the outcome the Epic exists to deliver.
- **Scope Boundary** — what is included and, explicitly, what is not.
- **Risk Ceiling** — the highest Risk Classification (per `AI_OPERATING_SYSTEM.md` §7) the AI Agent is pre-authorized to execute without stopping for a fresh approval. A Task that would exceed the Epic's Risk Ceiling always stops and escalates, regardless of Epic approval (§6).
- **Constituent Tasks** — either an explicit ordered list of Tasks, or a decomposition policy the AI Agent may use to derive that list, subject to Product Owner review before execution begins.
- **Definition of Done** — the condition under which the Epic itself, not just its Tasks, is considered complete.
- **Approval Record** — who approved the Epic, and when.

An AI Agent must not begin Epic-mode execution against an Epic that is missing any of the above fields. A missing field is treated the same as "no approved Specification exists" under `AI_OPERATING_SYSTEM.md` §8 — it stops work. Defining an Epic is a Product Decision (Approval Required, per `AI_OPERATING_SYSTEM.md` §22) and this rule is not relaxed by Implementation Authority.

This is distinct from a missing *supporting* implementation spec for a constituent Task once the Epic itself is already approved (for example, a short design spec for one hook inside an approved Epic) — that case is governed by Implementation Authority (`AI_OPERATING_SYSTEM.md` §22): the AI Agent may create it directly as an implementation artifact, without stopping to ask, provided it stays within the approved Epic's scope.

---

## 3. Internal Tasks vs. External Epics

| | Task | Epic |
|---|---|---|
| Origin | May be proposed by the AI Agent (Autonomous Candidate Mode) or given explicitly | Always originated and approved by the Product Owner |
| Altitude | Internal, engineering-scoped | External, product/business-scoped |
| Governed by | `AI_OPERATING_SYSTEM.md` | This document, layered on top of `AI_OPERATING_SYSTEM.md` |
| Risk handling | Classified individually (LOW/MEDIUM/HIGH) per §7 | Bounded by an overall Risk Ceiling; each constituent Task is still individually classified |
| Completion unit | One Completion Report (`AI_OPERATING_SYSTEM.md` §18) | One Epic Completion Report (§10 below), aggregating constituent Task reports |
| Typical duration | One execution session | May span multiple sessions, requiring Autonomous Recovery (§8) |

---

## 4. Product Owner Responsibilities

- Define and approve an Epic's Objective, Scope Boundary, Risk Ceiling, and Definition of Done.
- Approve the Epic's constituent Task list, or its decomposition policy, before execution begins.
- Retain override authority at any point during Epic execution (per `AI_OPERATING_SYSTEM.md` §21 Human Override) — an override always takes precedence over the Epic's pre-authorized Risk Ceiling.
- Approve Epic completion; an AI Agent's Epic Completion Report is a submission for approval, not a self-certifying "done" state. Per the Product Owner Principle (`AI_OPERATING_SYSTEM.md` §22): the Product Owner approves product outcomes, not implementation details — but the outcome approval itself is exclusively the Product Owner's to give.

### Product Roadmap Priority

Once the AI Kit is Stable (see `AI/VERSION.md`, AI Kit Stability Policy), Product Roadmap work has default priority over further Kit/governance work. Epic proposals originate from the Product Roadmap (§7 Epic Execution Model), not from the AI Kit itself — the Kit does not generate its own work. Governance work resumes only via a Governance Backlog entry, opened when a real Epic exposes a governance defect and approved by the Product Owner (`AI/OperatingSystem/README.md`). This operationalizes Principle 0 — Product First (`AI_OPERATING_SYSTEM.md`, Core Principles).

---

## 5. AI Agent Responsibilities

- Decompose an approved Epic into an ordered Task sequence, or accept a Product-Owner-supplied sequence.
- Execute each Task under the unmodified rules of `AI_OPERATING_SYSTEM.md`.
- Perform Continuous Validation (§9) between Tasks, not only at Epic end.
- Checkpoint state after every Task so execution can be safely resumed (§8).
- Stop immediately and escalate if a Task would exceed the Epic's Risk Ceiling, or would require expanding the Epic's Scope Boundary.
- Produce an Epic Completion Report (§10) at the end, or at the point execution stops.

---

## 6. Governance

- An Epic's Risk Ceiling can never authorize skipping the HIGH-risk Approval Boundary defined in `AI_OPERATING_SYSTEM.md` §8. Epic-level pre-approval covers sequencing convenience, not risk exceptions.
- Epic Scope Boundary must not silently expand. A candidate Task that falls outside the approved scope requires either rejecting that Task or requesting a Product-Owner scope amendment — an AI Agent must not fold it in silently under the theory that it is "related."
- If implementation reveals that an already-approved feature cannot be completed within the approved Epic scope, the AI Agent must stop and submit a **Scope Expansion request** — it must not silently expand the Epic to cover the gap. A Scope Expansion request states: what was found, why the current scope is insufficient to complete the feature, and the options available to the Product Owner (reject the feature, amend scope, or approve expansion). This is the named mechanism for the scope-amendment path in the bullet above.
- Every constituent Task still independently goes through `AI_OPERATING_SYSTEM.md`'s Startup Sequence (§3), Current-State Analysis (§5), Risk Classification (§7), and Implementation Rules (§10). Epic mode removes the need to pause for a full re-approval conversation between Tasks that are already inside approved scope at or below the Risk Ceiling — it does not remove any single-Task rule.
- Conflicts between the Epic's stated scope and a formal document (ADR, Architecture, Roadmap) follow the same Single Source of Truth authority order defined in `AI_OPERATING_SYSTEM.md` §1 — the Epic's own description does not outrank ADRs, Architecture, or Roadmap.

---

## 7. Long-Running Autonomous Execution

### Epic Execution Model

```text
Product Roadmap
    v
One Approval Per Epic   (Product Owner; per Epic Definition, §2)
    v
Autonomous Execution    (AI Agent; internal Tasks remain private — see §3, §5)
    v
Epic Review             (Epic Completion Report, §10)
    v
Commit                  (Product Owner normally executes; may delegate task-scoped
                          staging/commit execution via the Commit Gate, see below)
    v
Next Epic  ->  back to Product Roadmap
```

Added by Task-AI01-006 (resolving `GB-001`, no redesign of this model): "Commit" here defaults to Product Owner execution, unchanged from this Kit's baseline Git authority model. The Product Owner may explicitly delegate task-scoped staging/commit execution to an AI Agent through the Commit Gate (`AI_OPERATING_SYSTEM.md` §23) — this delegation is scoped to one specific Task's approved change set only, and does not imply push, merge, tag/release, or authorization for any future Task or Epic.

"Next Epic" means execution returns to the Product Roadmap and awaits a **fresh** Epic Approval — it is a roadmap-continuity marker, not a grant of cross-Epic autonomy. Only Task-chaining *inside* an already-approved Epic is autonomous, per `AI_OPERATING_SYSTEM.md` §11; an AI Agent must not start a new Epic on its own initiative, per `AI_OPERATING_SYSTEM.md` §11 and §16.

**Default Behaviour:** per the foundational axiom at the top of this document, once an Epic is approved the default behaviour for every constituent Task is CONTINUE, not STOP. An AI Agent pauses mid-Epic only when a Task would exceed the Epic's Risk Ceiling, or when execution hits one of the eight Stop Conditions (`AI_OPERATING_SYSTEM.md` §8) — never by default, and never merely because a Task completed.

### Task Sequencing Within an Epic

```text
For each Task in the approved Epic sequence:

    Run AI_OPERATING_SYSTEM.md §11 Autonomous Execution Loop to completion
    (Repository Inspection -> ... -> Completion Report)
        v
    Checkpoint (git status baseline, Task Completion Report recorded)
        v
    Is the next Task within Epic Scope and at or below the Risk Ceiling?
        v
    Yes -> proceed to next Task
    No  -> stop, escalate, produce Epic Completion Report for what is done so far
```

A Task is never started before the previous Task has fully completed its own Completion Report. There is no partial overlap between constituent Tasks.

---

## 8. Autonomous Recovery

If Epic execution is interrupted (session end, crash, context loss) partway through:

- On resume, the AI Agent must re-enter through Continuation Mode (`AI_OPERATING_SYSTEM.md` §4), not by assuming the interruption point recorded anywhere is accurate.
- The AI Agent must re-run Current-State Analysis (`AI_OPERATING_SYSTEM.md` §5) to determine, from actual repository state (git history, staged/uncommitted files, existing tests), exactly which constituent Tasks are genuinely complete and committed versus merely believed complete from a prior session's memory.
- Recorded Epic progress (in AI Memory or elsewhere) must be treated as a hint, never as verified fact — per `AI_OPERATING_SYSTEM.md` §14, Memory cannot override the repository.
- Any uncommitted work found from before the interruption is protected under Worktree Safety (`AI_OPERATING_SYSTEM.md` §15) and must not be discarded or overwritten while resuming.

---

## 9. Continuous Validation

- Build and the relevant test set must be run after every constituent Task completes — not deferred until the whole Epic finishes.
- A failure at any checkpoint pauses forward progress on further Tasks until it is triaged, per `AI_OPERATING_SYSTEM.md` §12, as a new failure, a pre-existing failure, or an environment failure.
- A new failure must be resolved, or explicitly deferred by the Product Owner, before the next Task in the Epic begins.
- Continuous Validation does not replace each Task's own Build/Test Rules; it is the same rules applied at Epic cadence rather than only once at the end.

---

## 10. Epic Completion Reporting

Fixed format, produced when the Epic finishes or when execution stops before completion:

1. Epic Summary (Objective, Scope Boundary, Risk Ceiling as approved)
2. Tasks Completed (list, each referencing its own Task Completion Report)
3. Tasks Deferred or Rejected (with reason)
4. Aggregate Build Results
5. Aggregate Test Results
6. Aggregate Documentation Updates
7. Aggregate Architecture / Compatibility Impact
8. Existing Worktree Changes Preserved
9. Known Limitations
10. Out-of-Scope Confirmation
11. Suggested Commit Message(s)
12. Recommended Next Epic or Task

Recommended Next Epic or Task is a suggestion only. An AI Agent must not automatically start it, consistent with `AI_OPERATING_SYSTEM.md` §18.

Epic Summary status must be phrased as **"Implementation Complete — Pending Product Owner Acceptance,"** never as final product completion or "done." An AI Agent must never declare Product Acceptance under any wording, consistent with `AI_OPERATING_SYSTEM.md` §18 and §22.

---

## 11. Relationship to AI_OPERATING_SYSTEM.md

This document sits one altitude above `AI_OPERATING_SYSTEM.md`. It assumes and requires every rule in that document to remain in force unchanged. Where this document uses a term also defined there (Risk Classification, Approval Boundary, Worktree Safety, Continuation Mode, Human Override, Completion Report), the definition in `AI_OPERATING_SYSTEM.md` is authoritative; this document only describes how that same rule applies when it is repeated across multiple Tasks inside one approved Epic.
