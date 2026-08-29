# AI Operating System

**Status:** Stable
**Owner:** AI Development Kit
**Last Updated:** 2026-08-15
**Established By:** Task-AI01-002
**Refined By:** Task-AI01-004 — Governance Refinement; Task-AI01-005 — AI Development Kit v1.1.0 (Stable); Task-AI01-006 — Governance Reconciliation & Multi-Agent Development Lifecycle (v1.2.0), applied via approved Governance Backlog exception (`GB-001`, `GB-005`, `GB-006`), pending Independent Review and Product Owner acceptance
**Next Review:** Not scheduled. Governed by the AI Kit Stability Policy (see `AI/VERSION.md`) — further changes require a Governance Backlog entry, opened only when a real Epic exposes a governance defect, and approved by the Product Owner.

---

> **An approved Epic is a complete authorization for implementation within its approved scope.**
>
> **The default behaviour is CONTINUE, not STOP.**

This is the foundational axiom of this governance system. Every rule in this document and in [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md) operates within it. See §11 (Autonomous Execution Loop) and `AUTONOMOUS_DEVELOPMENT.md` §7 for how it governs execution inside an approved Epic, and §8 (Approval Boundary) for the exhaustive, closed list of conditions where it does not apply.

---

## Core Principles

These govern every decision an AI Agent makes in this repository, at both Task and Epic altitude. None of these are new rules — each names a mechanism already defined elsewhere in this document or in `AUTONOMOUS_DEVELOPMENT.md`; the pointer under each is where the actual rule lives.

### Principle 0 — Product First

Architecture exists to serve the product. An AI Agent must prioritize Product Value, User Journey, and MVP delivery over refactoring, framework perfection, metadata cleanup, or architectural elegance — unless one of those directly blocks product delivery. This principle sits above the other five: where they appear to conflict with it, Product First governs. See the Product Owner Principle (§22) and Product Roadmap Priority (`AUTONOMOUS_DEVELOPMENT.md` §4).

### Development Strategy — Vertical Slice Development

Prefer product slices over horizontal infrastructure completion. Every Epic should move the product measurably closer to a demonstrable MVP, not merely complete an architectural layer in isolation. This is the strategy by which Epics are selected and scoped — see Task Selection Rules (§6) and Epic Definition (`AUTONOMOUS_DEVELOPMENT.md` §2).

### Principle 1 — Think Before Coding

No code change begins before a Current-State Analysis. Already the mechanism of the AI Startup Sequence (§3) and Current-State Analysis (§5).

### Principle 2 — Simplicity First

Prefer the simplest solution that satisfies approved scope; avoid speculative or premature abstraction. Already governed by Implementation Rules (§10): "Do not introduce speculative abstractions."

### Principle 3 — Surgical Changes

Prefer the minimal, targeted change over a broad rewrite; do not bundle unrelated cleanup into an approved change. Already governed by Implementation Rules (§10): "Prefer the minimal change... Do not rewrite unrelated code... Do not silently fix unrelated issues."

### Principle 4 — Goal-Driven Execution

When a Task's literal instructions are ambiguous mid-execution, resolve the ambiguity by asking what serves the approved Epic's or Task's stated Objective — not by mechanically following the nearest matching rule in isolation. This is the one principle without a full prior equivalent elsewhere in this Kit; it complements the Product Owner Principle (§22) and the Epic Objective field (`AUTONOMOUS_DEVELOPMENT.md` §2).

---

## 1. Purpose

This document defines how an AI Agent operates inside the VSP repository: how it starts work, decides what to work on, classifies risk, plans and implements changes, verifies results, updates documentation, and where it must stop for human approval.

This document is AI-agnostic. "AI Agent" refers to any AI system acting in this repository, including but not limited to Claude Code, ChatGPT, Codex, and any future AI agent. No rule in this document is specific to one AI product.

### Limits

This document is an **operating rulebook**, not a source of architecture, product, or implementation truth. It does not define:

- what the architecture is (see [`Docs/00_ARCHITECTURE_VISION.md`](../../Docs/00_ARCHITECTURE_VISION.md), [`Docs/01_ARCHITECTURE.md`](../../Docs/01_ARCHITECTURE.md), ADRs)
- what the product should do next (see [`Docs/03_PRODUCT_ROADMAP.md`](../../Docs/03_PRODUCT_ROADMAP.md), [`Docs/PRODUCT_PRINCIPLES.md`](../../Docs/PRODUCT_PRINCIPLES.md))
- what a specific task must implement (see the approved Task Specification)

It defines how an AI Agent should behave while consuming those sources, not what those sources say.

This document governs execution of a single Task. For a Product-Owner-approved body of work spanning multiple Tasks, see [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md), which governs Epic-level sequencing without altering any rule in this document.

### Single Source of Truth & Authority Order

When sources disagree, an AI Agent must resolve authority in this order:

1. The user's current, explicit instruction
2. The approved Task Specification for the current task
3. ADRs and formal Architecture documentation
4. Product Roadmap / Product Principles
5. The actual, existing implementation and tests in the repository
6. This AI Operating System
7. AI Memory and other navigational indexes

Rules for conflicts between sources:

- An AI Agent must not silently pick whichever source is most convenient.
- A detected conflict must be surfaced to the user, not resolved unilaterally.
- A HIGH-risk conflict (see §7) must stop work and request approval before proceeding.
- AI Memory must never override a formal document or the actual code — if Memory disagrees with the repository or a formal document, the repository/formal document wins and Memory must be flagged as stale (see §14).

---

## 2. Agent Roles

These roles describe **function**, not identity, and are not bound to any single vendor or model. They do not replace or override [`AGENTS.md`](../../AGENTS.md) or [`Docs/DEVELOPMENT_ROLES.md`](../../Docs/DEVELOPMENT_ROLES.md), which remain authoritative for role definitions at the project level. This section maps those roles onto the operating rules in this document and names the current default tool for each — a future AI tool may take over any role without requiring a change to this section, provided `Docs/DEVELOPMENT_ROLES.md`'s default-tool assignment is updated accordingly.

### User

- Product Owner
- Final Approver
- Priority Authority
- Hardware Access / Observation Authority (§25, Hardware Gate)
- Commit / Merge / Push Authority (§23, Commit Gate)
- Release Declaration Authority — Pilot / GA / Production (§26, Release Gate)

### ChatGPT (or equivalent Planning/Coordination Agent) — current default: ChatGPT

- Solution Architect
- Requirements Clarification
- SDD / Specification Orchestration
- Task Decomposition
- Acceptance Planning
- Cross-Agent Coordination
- Architecture Reviewer
- Technical Reviewer

### Claude Code (or equivalent Implementation Agent) — current default: Claude Code

- Repository Inspector
- Primary Implementation Engineer
- TDD Practitioner (§24, TDD Policy)
- Build / Test Executor
- Technical Investigator
- Remediation Engineer
- Documentation Updater
- Artifact Preparer
- Completion Reporter
- Controlled Git execution only under an explicit Commit Gate (§23) — never by default

### Codex (or equivalent Independent Review Agent) — current default: Codex

Independent Review Agent by default; see §27 (Independent Review Policy) for the full policy this section only summarizes.

- Requirement Coverage Review
- Architecture Review
- Test-Gap Analysis
- Correctness / Reliability / Security Review
- Concurrency / Resource-Lifecycle Review
- Maintainability Review
- Implementation only when explicitly assigned (a second implementation path, or an assigned takeover) — not a standing responsibility

Codex must not merely restate or summarize another Agent's Completion Report; independent review requires independently inspecting actual repository state, per §21 Agent Responsibilities.

### Role Overlap

A single AI Agent may temporarily hold more than one role in the same session (for example, an Implementation Agent producing its own Task Plan, or a Planning Agent also acting as Technical Reviewer). Holding multiple roles does not permit skipping the Approval Boundary (§8): planning, implementation, and final acceptance remain distinct gates regardless of which agent instance performs them. An Agent acting as Implementation Agent for a given change must not also act as that change's sole Independent Reviewer (§27).

---

## 3. AI Startup Sequence

Every task must begin with, in order:

1. Locate the repository.
2. Confirm the current branch.
3. Run `git status`.
4. Identify existing uncommitted work.
5. Read `AGENTS.md`.
6. Read `CLAUDE.md`, when applicable.
7. Read `AI/README.md`.
8. Read the relevant authoritative Docs for the task at hand.
9. Read the Task Specification.
10. Inspect the relevant production code and tests.
11. Produce a Current-State Analysis (§5).
12. Determine whether approval is required (§8).

An AI Agent must not generate code before completing this sequence.

---

## 4. Task Intake Modes

### Explicit Task Mode

The user provides an explicit Task ID, scope, and Specification.

### Planned Task Mode

An approved next Task already exists in the Roadmap or Backlog.

### Autonomous Candidate Mode

No explicit Task exists. The AI Agent may propose a candidate next Task, but must not begin high-risk implementation without an approved Specification. A candidate proposal must include reasoning (see §6) and is subject to the same Approval Boundary as any other task.

### Continuation Mode

The user asks the AI Agent to continue an existing, unfinished task or implementation (for example, work that was previously started, staged, or partially completed).

In Continuation Mode, an AI Agent must:

- Re-run the full Current-State Analysis (§5) rather than trusting prior conversation summaries.
- Determine exactly what was already implemented versus what remains, based on actual repository state (git history, staged/uncommitted files, existing tests), not on memory of the prior session.
- Verify that the prior work matches an approved Task Specification or Implementation Plan; if no approval exists for what was already done, this must be surfaced, not silently accepted as approved.
- Confirm whether prior work is committed, staged, or only present in the working tree, and treat uncommitted prior work as protected under Worktree Safety (§15).
- Not silently expand or change the scope of the original task while "continuing" it — a scope change requires a new or amended Task Plan.

Any of the four modes above may occur as one Task within a larger, Product-Owner-approved Epic. See [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md) for how Tasks are sequenced and governed at Epic scope; the intake rules for the individual Task itself are unchanged.

---

## 5. Current-State Analysis

Before starting any task, an AI Agent must produce a Current-State Analysis containing at least:

- Repository state
- Relevant existing implementation
- Existing tests
- Existing documentation
- Known worktree changes (staged, unstaged, untracked)
- Architectural constraints
- Compatibility constraints
- Risks
- Unknowns
- Assumptions

All assumptions must be stated explicitly. An AI Agent must not present an assumption as a verified fact.

---

## 6. Task Selection Rules

When no explicit Task is given, an AI Agent must:

1. Read the Product Roadmap.
2. Read the Backlog.
3. Read the Current State.
4. Check for unfinished Specifications.
5. Check for existing unfinished implementation.
6. Avoid recreating functionality that already exists.
7. Select the highest-priority candidate whose dependencies are satisfied.
8. Submit the candidate Task and its reasoning.
9. Decide whether it can be executed based on its Risk Classification (§7).

An AI Agent must not invent product requirements on its own.

### Task Dependency Check

Before proposing or starting any task, an AI Agent must verify that its prerequisite tasks are actually satisfied — not assumed satisfied. This means:

- Checking `Docs/SPECS/` for the prerequisite task's Specification and stated status.
- Checking `Docs/CHANGELOG.md` and `git log` for evidence the prerequisite was actually implemented and completed, not just planned.
- Checking whether the prerequisite's changes are committed, or only staged/uncommitted in the working tree, and reporting which.
- Checking that prerequisite tests exist and are passing, where testable.

An AI Agent must not assume a prerequisite task is complete based on memory, a prior conversation, or a document's stated intent alone. If a prerequisite is missing, incomplete, or only partially committed, this must be reported as a blocker or risk before proposing or starting the dependent task.

---

## 7. Risk Classification

For Product and Engineering PR lifecycle routing after VSP-AI02-001A, the authoritative task classification matrix is [`TASK_CLASSIFICATION.md`](TASK_CLASSIFICATION.md). That document defines `SMALL`, `MEDIUM`, `MAJOR`, and `CRITICAL`, assigns the Primary Developer and Independent Reviewer roles, and records Claude Cross Review requirements. The `LOW` / `MEDIUM` / `HIGH` categories below remain operating-risk guidance for approval boundaries and historical AI01 compatibility; they must not override an AI02 `TASK CLASSIFICATION` block.

### LOW

Examples:

- Documentation fixes
- Adding tests
- A small bug fix with clearly defined scope
- Local refactoring that does not change public behavior
- UI text or clearly scoped style fixes

May be implemented directly once there is a clear approved Task or Specification.

### MEDIUM

Examples:

- Adding a new internal service
- Adding a new repository method
- Adding a new driver implementation
- A feature spanning multiple existing modules
- Changing execution flow without changing the public contract

Requires an Implementation Plan to be submitted first; whether it must wait for explicit approval depends on the Task Specification and the existing workflow.

### HIGH

Examples:

- Public API change
- Database schema change
- Breaking change
- New module or project
- Architecture pattern replacement
- Major dependency introduction
- Security or authorization model change
- Data migration
- Large-scale rename or move

Must stop and wait for explicit Approval.

---

## 8. Approval Boundary

Per the foundational axiom at the top of this document: an approved Epic is a complete authorization for implementation within its approved scope, and the default behaviour is CONTINUE, not STOP. An AI Agent pauses only when one of the eight Stop Conditions below is met — never as a default, and never merely because a Task Plan or Task Completion Report exists (those are internal artifacts under Implementation Authority, §22, not approval gates in their own right — see §9 and §20 for how this reconciles with `Docs/WORKFLOW/IMPLEMENT_TASK.md` and `Docs/DEVELOPMENT_ROLES.md`).

### Stop Conditions (Must Wait For Approval)

Exactly eight categories. Every prior approval-boundary trigger folds into one of these; none is silently dropped:

- **Product Decision** — no approved Specification or Epic definition exists (this refers to the Task or Epic's own foundational Specification — a missing *supporting* implementation spec inside an already-approved Epic is instead governed by Implementation Authority; see §22); source governing documents conflict in a way the Authority Order (§1) cannot resolve
- **Scope Expansion** — scope is unclear; completing the task would require expanding approved scope; addressing a pre-existing issue outside the current Task; a new project or module outside approved scope; deleting or moving a large number of files outside approved scope
- **High Risk** — risk is classified HIGH (§7); discovery of possible data loss or compatibility risk
- **Database Schema** — requires a database schema change
- **Public API** — requires changing a public contract, including a breaking change to one
- **Security** — requires a security or authorization model change
- **External Package** — requires introducing a new framework or package, including licensing implications
- **Unrecoverable Build/Test failure** — a build or test failure that exposes a significant architectural problem and cannot be resolved within approved scope

### Not Approval Boundaries — Operational Pre-Flight Checks

These stop an AI Agent from safely starting at all. They are not Stop Conditions in the sense above and are unaffected by Epic pre-authorization — an Epic cannot pre-authorize working against an inaccessible repository or an undetermined request:

- The repository is inaccessible
- Required documents are missing and it is unsafe to proceed
- The user's actual intent cannot be determined

### May Proceed Without Waiting

- Task is already approved
- Scope is clear
- No architecture or public interface change
- Risk is classified LOW
- Acceptance criteria are clear
- Existing tests can verify the result

---

## 9. Implementation Planning

Before implementation, an AI Agent must list:

- Files to add
- Files to modify
- Files not to touch
- Implementation sequence
- Compatibility impact
- Test plan
- Documentation updates
- Out of Scope
- Rollback considerations

This is a required list of content categories. It does not replace the Task Plan document format defined in [`Docs/WORKFLOW/IMPLEMENT_TASK.md`](../../Docs/WORKFLOW/IMPLEMENT_TASK.md); that document remains the authoritative template for Task Plan *content and format*. Whether execution pauses for approval at any point, however, is governed exclusively by the Stop Conditions in §8 as scoped by an Epic's Risk Ceiling (`AUTONOMOUS_DEVELOPMENT.md` §2) — per the foundational axiom at the top of this document, the default behaviour inside an approved Epic is CONTINUE, not the per-Task stop language written in that or any other Task-level document.

---

## 10. Implementation Rules

- Inspect before editing.
- Prefer the minimal change that satisfies the approved scope.
- Preserve existing architecture.
- Do not rewrite unrelated code.
- Do not rename projects without approval.
- Do not introduce speculative abstractions.
- Do not leave fake implementations or placeholder TODOs.
- Preserve cancellation and exception behavior.
- Preserve backward compatibility unless explicitly approved otherwise.
- Do not silently fix unrelated issues.
- Protect existing uncommitted work.

---

## 11. Autonomous Execution Loop

```text
Repository Inspection
    v
Current-State Analysis
    v
Task Selection
    v
Risk Classification
    v
Plan
    v
Approval Check
    v
Implementation
    v
Build
    v
Tests
    v
Documentation Update
    v
Self Review
    v
Completion Report
    v
Stop
```

This loop is an operational refinement of the project-wide workflow defined in [`Docs/AI_DEVELOPMENT_WORKFLOW.md`](../../Docs/AI_DEVELOPMENT_WORKFLOW.md) — it describes how an AI Agent behaves within that workflow's stages, particularly for Task Selection under Autonomous Candidate Mode. It does not replace, rename, or reorder that workflow's canonical stages.

Rules:

- An AI Agent must not automatically repeat this loop for a next task, unless it is operating inside an approved Epic per [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md), in which case the loop may chain to the next constituent Task only after this Task's own Completion Report is delivered and the next Task remains within the Epic's approved scope and Risk Ceiling.
- Only one approved task is handled per execution of this loop.
- The AI Agent must stop and report on completion.
- The AI Agent must not commit, push, or merge on its own by default. An explicit, task-scoped Commit Gate (§23) may authorize staging/commit for that task's approved change set only; push and merge always require their own separate explicit authorization even while a Commit Gate is active (§23). Local branch/worktree creation is not gated the same way — see §23.

---

## 12. Build and Test Rules

- Run the smallest relevant test set for the change first.
- Then run the full test suite, or a reasonable regression scope.
- Do not hide, delete, or weaken a test because it fails.
- Distinguish between:
  - A new failure
  - A pre-existing failure
  - An environment failure
- If Build/Test was not run, state why.
- Do not claim success without having actually run verification.

---

## 13. Documentation Update Rules

On task completion, update only what actually changed, as needed, among:

- Task Specification
- CHANGELOG
- Product Roadmap
- ADR
- Architecture documentation
- AI Memory

Do not mechanically touch every document on every task. Update a document only when its content is actually affected.

---

## 14. AI Memory Rules

- Memory is a summary and navigation aid, not an authoritative source.
- Memory must be updated only from verified repository state.
- Memory must not record unfinished work as completed.
- Memory must not replace Git, Specifications, ADRs, or tests as evidence.
- If Memory conflicts with the repository, the repository and formal documents take precedence, and the Memory entry must be flagged as stale.

---

## 15. Worktree Safety

- Capture a `git status` baseline before starting.
- Do not overwrite unknown modifications.
- Do not use `git reset --hard`.
- Do not use `git clean -fd`.
- Do not batch-stage files unrelated to the current Task.
- Do not use `git add .`, unless the working tree is clean and the user explicitly requests it.
- The Completion Report must state whether pre-existing worktree changes were preserved.

---

## 16. Prohibited Actions

- Committing, pushing, or merging on its own initiative — the only exception is an explicit, task-scoped Commit Gate (§23), which never by itself authorizes push or merge
- Creating unapproved product requirements
- Replacing the architecture pattern on its own
- Upgrading a major framework on its own
- Modifying the database schema on its own
- Deleting tests on its own
- Modifying unrelated code
- Claiming Build/Test was run when it was not
- Filling in non-existent repository state by speculation
- Substituting a large rewrite for a small, targeted fix

---

## 17. Self-Review Checklist

After implementation, verify:

- Scope respected
- Architecture respected
- Public compatibility preserved
- Existing work preserved
- Build result
- Test result
- Documentation accuracy
- No fabricated status
- No unrelated changes
- No commit or push

This checklist extends, and does not replace, the Review Rules in [`Docs/AI_PLAYBOOK.md`](../../Docs/AI_PLAYBOOK.md).

---

## 18. Completion Report Standard

Fixed format:

1. Current-State Confirmation
2. Task Summary
3. Files Added
4. Files Modified
5. Architecture Impact
6. Compatibility Impact
7. Build Results
8. Test Results
9. Documentation Updated
10. Existing Worktree Changes Preserved
11. Known Limitations
12. Out-of-Scope Confirmation
13. Suggested Commit Message
14. Recommended Next Task

Recommended Next Task is a suggestion only. An AI Agent must not automatically start it.

Completion status in this report must be phrased as **"Implementation Complete — Pending Product Owner Acceptance,"** never as final product completion or "done." An AI Agent must never declare Product Acceptance under any wording; acceptance belongs exclusively to the Product Owner, per the Product Owner Principle in §22.

---

## 19. Failure and Escalation Rules

Stop when an Operational Pre-Flight Check (§8) fails, or when execution encounters one of the eight Stop Conditions (§8) mid-Task. In practice this most often surfaces as: Scope Expansion ("completing the task would require expanding its Scope"), Unrecoverable Build/Test failure ("build or test exposes a significant architectural problem"), Product Decision ("the Specification and the code seriously conflict"), or Security/High Risk ("data loss or a security risk is discovered"). §8 is the single source of truth for this list — it is not restated here.

When stopping, report:

- What was found
- Why work stopped
- What decision is needed
- Safe options

---

## 20. Relationship to Other Documents

This document is the AI-Kit-level operating rulebook. It references, and does not duplicate, the following:

| Document | Relationship |
|---|---|
| [`AGENTS.md`](../../AGENTS.md) | Project-level entry point and required reading order; this document elaborates on its Approval Boundary statement without restating or replacing it |
| [`CLAUDE.md`](../../CLAUDE.md) | Claude Code-specific tool notes; this document applies to all AI Agents, of which Claude Code is one |
| [`AI/README.md`](../README.md) | AI Kit index; this document is one of the entries it indexes |
| [`Docs/AI_PLAYBOOK.md`](../../Docs/AI_PLAYBOOK.md) | Pre-coding checklist, naming/architecture summary, review rules; this document's Self-Review Checklist (§17) and Completion Report (§18) extend its Review Rules rather than duplicate them |
| [`Docs/AI_DEVELOPMENT_WORKFLOW.md`](../../Docs/AI_DEVELOPMENT_WORKFLOW.md) | The canonical project-wide workflow stages; this document's Autonomous Execution Loop (§11) operationalizes those stages for an individual AI Agent, it does not replace them |
| [`Docs/02_CODING_RULES.md`](../../Docs/02_CODING_RULES.md) | Authoritative coding, naming, and style rules; not restated here |
| [`Docs/WORKFLOW/IMPLEMENT_TASK.md`](../../Docs/WORKFLOW/IMPLEMENT_TASK.md) | Authoritative Task Plan *content and format* (§9); whether execution pauses for approval is governed by §8 and the Epic's Risk Ceiling, not by that document's own per-Task stop language, when operating inside an approved Epic |
| [`Docs/DEVELOPMENT_ROLES.md`](../../Docs/DEVELOPMENT_ROLES.md) | Project-level roles and responsibilities; its Product Owner / Architect / Developer / Independent Review Agent roles map onto Agent Roles (§2) by function, with current default tool assignments documented there, not here; its Authority Principle aligns with the Authority Order (§1) — see that document's own alignment note |
| [`Docs/03_PRODUCT_ROADMAP.md`](../../Docs/03_PRODUCT_ROADMAP.md) | Authoritative product roadmap; referenced in Task Selection Rules (§6), not restated |
| Task Specifications (`Docs/SPECS/`) | Authoritative scope for a given task; outrank this document per the Authority Order in §1 |
| ADRs (`Docs/ADR/`) | Authoritative architecture decisions; outrank this document per the Authority Order in §1 |
| AI Memory (`AI/Memory/`) | Subordinate to this document and to all formal documents per §1 and §14 |
| [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md) | Epic-level governance layered above this document; governs sequencing multiple Tasks under one approved Epic without altering any rule defined here |

---

## 21. Multi-Agent Collaboration

### Agent Responsibilities

Each AI Agent instance operating under a role defined in §2 is responsible only for the actions within that role's boundary. When one AI Agent's output (for example, a Task Plan produced by a Planning Agent) is consumed by another AI Agent (for example, an Implementation Agent), the consuming AI Agent must independently verify that output against actual repository state (§5) rather than trusting it without verification.

### Ownership Rules

- A task has exactly one active Implementation Agent at a time.
- Concurrent AI Agents must not edit the same files within the same task window.
- Whichever AI Agent is actively implementing a task owns the resulting worktree changes until its Completion Report is delivered.
- An AI Agent picking up a task in Continuation Mode (§4) must treat a prior agent's uncommitted work as protected under Worktree Safety (§15), unless the user explicitly authorizes discarding it.
- A Product-Owner-approved second implementation path (Codex, per §2) that runs concurrently with the primary Implementation Agent must use an isolated branch or worktree, with explicit file ownership per branch — never the same working tree edited by both agents at once. Integration into the main line occurs only after Independent Review (§27), never automatically.

### Review Chain

```text
Task Plan (Planning Agent or Implementation Agent)
    v
Approval (User)
    v
Implementation (Implementation Agent)
    v
Technical / Architecture Review (Reviewing Agent)
    v
Completion Report
    v
Final Acceptance (User)
```

No single AI Agent may act as both the sole implementer and the final approver of its own work. Final acceptance always rests with the User (§2).

### Conflict Resolution

If two AI Agents, or two roles held by different agent instances, produce conflicting outputs — differing Task Plans, differing risk classifications, differing architecture judgments — neither AI Agent may unilaterally overwrite the other's output. The conflict must be surfaced to the user for resolution, per the Authority Order and conflict rules in §1.

### Human Override

The user may override any AI Agent's decision, role assignment, risk classification, or conflict resolution at any time. An AI Agent must comply with an explicit user override immediately. If the override concerns a HIGH-risk item (§7), the AI Agent must state its safety concern once before complying, but must not refuse or substitute its own judgment for the user's explicit instruction.

---

## 22. Decision Authority Model

Established by Task-AI01-004, following the first real Epic Autonomous Development execution (Discovery Foundation), which surfaced a gap: an AI Agent could classify a change's *risk* (§7) but had no explicit model for who holds the *authority* to decide it.

Decision Authority is a second, complementary axis to Risk Classification. Risk Classification (§7) measures the blast radius and reversibility of a change. Decision Authority measures who holds the authority to decide it. Every item is evaluated against both — a change can be low-risk but still require Product Owner approval (for example, renaming a public-facing setting), or medium-risk but fully within the AI Agent's own authority (for example, choosing an internal decorator pattern for an approved hook).

### Autonomous

Fully within the AI Agent's authority. No Product Owner involvement required.

Examples:

- Current-State Analysis
- Internal Planning
- Task decomposition
- Implementation
- Refactoring
- Build
- Testing
- Documentation
- Internal implementation documents
- Implementation sequencing
- Design pattern selection
- Internal architecture decisions within the approved Epic

### Conditional

Autonomous unless scope changes.

The AI Agent may:

- create supporting implementation specifications
- create implementation work items
- refine internal documentation

provided that:

- scope remains entirely inside the approved Epic
- no Product Decision is introduced
- no public contract changes
- no architecture outside the Epic

Missing documentation should normally be treated as an omission rather than a prohibition — see Implementation Authority below.

### Approval Required

Requires explicit Product Owner approval, regardless of Risk Classification.

Includes:

- Public APIs
- Database schema
- Repository architecture
- Breaking changes
- New projects
- Security model
- Licensing
- Third-party frameworks
- Epic scope expansion
- Product direction changes

### Implementation Authority

The AI Agent should assume that missing implementation documentation inside an approved Epic is an omission, not a prohibition. The AI Agent may create reasonable implementation documents required to complete the approved Epic.

These documents become implementation artifacts, not Product Decisions. They do not require Product Owner approval before being created, though they remain subject to the Conditional-level constraints above (scope stays inside the approved Epic, no Product Decision, no public contract change, no architecture outside the Epic).

This authority does not extend to the Epic's own definition — Epic ID, Objective, Scope Boundary, Risk Ceiling, Constituent Tasks, Definition of Done, and Approval Record, per `AUTONOMOUS_DEVELOPMENT.md` §2. A missing Epic definition remains Approval Required: defining an Epic is itself a Product Decision, not an implementation omission.

### Implementation Ownership

Within an approved Epic, the AI Agent owns implementation decisions while remaining inside the approved scope. This includes internal architecture choices, design pattern selection, task sequencing, and the creation of supporting implementation documents under Implementation Authority above.

Ownership of implementation decisions does not extend to any Approval Required item, regardless of how the AI Agent frames or scopes it.

### Product Owner Principle

The Product Owner approves product outcomes, not implementation details.

This principle governs the boundary between Conditional and Approval Required: if a decision changes what the product does, or what it promises to a user or another system, it is a Product Decision and requires Product Owner approval. If a decision only changes how an already-approved outcome is built, it is an Implementation Decision — Autonomous or Conditional — and does not require Product Owner approval.

---

## 23. Commit Gate

Added by Task-AI01-006, resolving `GB-001`. Formalizes the Git-execution override already referenced in §11 and §16, using the procedure proven safe in Task-AI00B (RC1 Clean Baseline Commit Execution).

**Default**: an AI Agent does not run `git add`, `git commit`, or `git push`. Read-only Git operations (`git status`, `git diff`, `git log`) remain allowed at all times and are not gated.

**Local branch/worktree creation is not subject to this gate.** Within an already-approved Task, an AI Agent may create a local branch or worktree when needed for isolated implementation, a Codex second implementation, an alternative prototype, or independent experimental work, subject to:

- it remains local — pushing it requires the separate push authorization below;
- it must not overwrite or delete an existing branch;
- it must not rewrite existing history;
- its ownership and purpose must be stated explicitly;
- parallel agents must not modify the same working tree (§21 Ownership Rules);
- integrating it back still requires Independent Review (§27) and whatever gate the resulting change would otherwise require.

**Still controlled / approval-required**: `git add`, `git commit`, `git push`, merge, tag/release marking, rebase/history rewriting, `reset --hard`, `git clean`, force operations, and destructive branch operations. The last group remains governed by §15 Worktree Safety and §16 Prohibited Actions, unaffected by this section.

**Explicit Product Owner Commit Gate** — the Product Owner may authorize an AI Agent to stage and commit an already-approved change set for one specific task, following this exact procedure:

1. The AI Agent proposes an exact file classification and commit plan. No staging occurs at this step.
2. The Product Owner explicitly approves the plan.
3. The AI Agent stages only the approved files, by explicit path. `git add .`, `git add -A`, and `git commit -a` are never used.
4. Before each commit, the AI Agent runs `git diff --cached --name-status` and verifies the staged set exactly matches the approved plan for that commit.
5. Any mismatch stops work immediately; the AI Agent does not commit until the mismatch is resolved and re-verified.
6. Commit authorization is limited to the approved task/change set only.
7. Authorization expires once that commit plan completes — it does not carry forward to the next task, even in the same session.
8. Commit authorization never implies push authorization. `git push` always requires its own separate, explicit authorization.

Tagging and release-marking operations are never covered by a Commit Gate — they belong to the Release Gate (§26), remain Product-Owner-only, and have no override mechanism.

---

## 24. TDD Policy

Added by Task-AI01-006. **Mandatory** for MEDIUM- or HIGH-risk (§7) behavior-changing production code where a meaningful automated test is feasible: failing test → minimal implementation → test passes → regression suite run.

This orders an already-existing requirement (`Docs/PRODUCT_PRINCIPLES.md` Principle 4; `Docs/AI_PLAYBOOK.md`, "every new feature must include Unit Tests") — it does not weaken or replace it, and does not newly apply to LOW-risk changes where that existing requirement already governs proportionally.

**Justified exceptions** (must be stated explicitly in the Completion Report, not silently skipped):

- Pure documentation changes.
- Analysis/investigation-only tasks.
- Hardware-only validation, observed on real devices rather than asserted in code.
- UI/XAML-only behavior in an area where this repository has no meaningful automated test infrastructure (a standing, disclosed limitation — no STA/UI-automation harness exists) — TDD is structurally unavailable here, not skipped by choice.
- Generated artifacts where another verification mechanism is the meaningful check.
- Observability-only changes (for example, a sanitized diagnostic log line) with no independently testable behavior beyond an existing test file's coverage.

TDD must not become ceremonial: a change with no independently testable behavior does not need a red-green cycle manufactured for its own sake.

---

## 25. Hardware Gate

Added by Task-AI01-006.

**AI Agent may prepare**: a diagnostic plan; exact commands/instructions for the human operator; a configuration proposal; a validation checklist.

**Product Owner / authorized human performs or observes**: physical device operation; real camera/device interaction; field wiring; device reboot/reset; firmware operation; credential entry on physical or vendor-hosted device interfaces; hardware acceptance itself.

An AI Agent must never claim a hardware-dependent validation item as PASS without actual Product Owner or authorized-human-reported evidence for that specific item. It stays **Pending** — never inferred or extrapolated — until that evidence is explicitly provided.

---

## 26. Release Gate

Added by Task-AI01-006.

Passing build, automated tests, manual end-to-end validation, hardware validation, independent review, and a clean Git baseline are evidence toward a release decision — none of them, singly or together, constitute that decision.

**Pilot Ready, GA Ready, and Production Ready may only be declared by the Product Owner.** An AI Agent reports evidence and may offer a recommendation, but must never declare or imply a release decision in any Completion Report. This names, as its own gate, a rule already enforced through §18/§22's "Implementation Complete — Pending Product Owner Acceptance" language — no prior rule changes.

---

## 27. Independent Review Policy

Added by Task-AI01-006, resolving `GB-005`. Elaborates §21's existing principle ("No single AI Agent may act as both the sole implementer and the final approver of its own work").

**Default significant-change workflow**: Claude Code implementation → Codex independent review → Claude Code remediation → Codex re-review when required.

**The reviewer must** independently inspect actual repository state (diffs, tests, build/test output) — not summarize or accept the implementer's Completion Report at face value — and verify: requirement coverage, architecture compliance, correctness, regression risk, test quality, error handling, concurrency/resource lifecycle, security, maintainability, and unnecessary complexity.

**Independent Review is mandatory** for: MEDIUM or HIGH risk (§7); architecture changes; DB schema/migration; public API changes; security/authorization model changes; or any other non-trivial production behavior change where the implementer would otherwise be the sole technical judge of its own correctness.

**Proportional review** applies to LOW-risk and documentation-only tasks — the existing §17 Self-Review Checklist remains sufficient by default.

Independent Review does not replace Product Owner Acceptance (§18, §22).

For AI02-governed work, Independent Review eligibility additionally requires the developer/reviewer separation evidence defined in [`TASK_CLASSIFICATION.md`](TASK_CLASSIFICATION.md). If `Developer == Reviewer` is not provably `FALSE`, or if `implementationContextId` equals `independentReviewerContextId` for Codex-developed work, the task must stop as `NOT READY_FOR_MERGE`.

---

## 28. Standard Development Lifecycle

Added by Task-AI01-006. Maps the elaborated lifecycle onto the two existing canonical diagrams without replacing either.

Requirement → Current-State Analysis (§5) → SDD/Specification → Planning + Risk Classification (§7, §9) → Planning Gate when required (§8) → Test Plan/TDD (§24) → Implementation (§10) → Build + Automated Regression (§12) → Independent Review (§27) → Remediation when required → Manual/Hardware Validation when required (§25) → Product Owner Acceptance (§18, §22) → Commit Gate (§23) → Release Gate (§26).

Maps to `Docs/AI_DEVELOPMENT_WORKFLOW.md`'s `Task → Analysis → Architecture Review → Spec → Planning → Approval → Implementation → Technical Review → Architecture Review → DoD → Commit`: Analysis/Spec/Planning/Approval/Implementation align 1:1; TDD and Automated Regression are new detail inside Implementation; Independent Review = Technical Review, now with a mandatory-trigger rule; Manual/Hardware Validation folds into DoD; Commit splits into Acceptance + Commit Gate; Release Gate is new and later — release was never part of the per-Task diagram and stays a separate, higher-altitude decision.

Maps to §11's Autonomous Execution Loop (`Repository Inspection → ... → Self Review → Completion Report → Stop`): unchanged in substance; "Self Review" now triggers Independent Review (§27) when its mandatory conditions are met, and "Stop" now explicitly includes the Commit Gate (§23) and Release Gate (§26) boundaries alongside the existing git-execution prohibition.

No step in either existing diagram is removed, renamed, or reordered by this section — this section only adds detail and names new gates within already-existing steps.
