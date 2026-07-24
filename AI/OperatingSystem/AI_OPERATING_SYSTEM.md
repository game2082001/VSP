# AI Operating System

**Status:** Draft
**Owner:** AI Development Kit
**Last Updated:** 2026-07-24
**Established By:** Task-AI01-002
**Next Review:** AI01-007 CLAUDE.md Integration

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

These roles describe **function**, not identity. They do not replace or override [`AGENTS.md`](../../AGENTS.md) or [`Docs/DEVELOPMENT_ROLES.md`](../../Docs/DEVELOPMENT_ROLES.md), which remain authoritative for role definitions at the project level. This section maps those roles onto the operating rules in this document.

### User

- Product Owner
- Final Approver
- Priority Authority
- Commit / Merge Authority

### ChatGPT (or equivalent Planning/Review Agent)

- Solution Architect
- Task Planner
- Architecture Reviewer
- Technical Reviewer

### Claude Code / Implementation Agent

- Repository Inspector
- Implementation Engineer
- Test Executor
- Documentation Updater
- Completion Reporter

### Role Overlap

A single AI Agent may temporarily hold more than one role in the same session (for example, an Implementation Agent producing its own Task Plan, or a Planning Agent also acting as Technical Reviewer). Holding multiple roles does not permit skipping the Approval Boundary (§8): planning, implementation, and final acceptance remain distinct gates regardless of which agent instance performs them.

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

### Must Wait For Approval

- No approved Specification exists
- Scope is unclear
- Risk is classified HIGH
- Source documents conflict
- Requires deleting or moving a large number of files
- Requires a Database Schema change
- Requires introducing a new Framework or Package
- Requires changing a Public Contract
- Requires addressing a pre-existing issue outside the current Task
- Discovery of possible data loss or compatibility risk

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

This is a required list of content categories. It does not replace the Task Plan document format defined in [`Docs/WORKFLOW/IMPLEMENT_TASK.md`](../../Docs/WORKFLOW/IMPLEMENT_TASK.md); that document remains the authoritative template.

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

- An AI Agent must not automatically repeat this loop for a next task.
- Only one approved task is handled per execution.
- The AI Agent must stop and report on completion.
- The AI Agent must not commit, push, or merge on its own, unless explicitly authorized by the user for that specific action.

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

- Committing, pushing, or merging on its own initiative
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

---

## 19. Failure and Escalation Rules

Stop when:

- The repository is inaccessible
- Required documents are missing and it is unsafe to proceed
- The Specification and the code seriously conflict
- The user's actual intent cannot be determined
- Build or Test exposes a significant architectural problem
- Completing the task would require expanding its Scope
- Data loss or a security risk is discovered

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
| [`Docs/03_PRODUCT_ROADMAP.md`](../../Docs/03_PRODUCT_ROADMAP.md) | Authoritative product roadmap; referenced in Task Selection Rules (§6), not restated |
| Task Specifications (`Docs/SPECS/`) | Authoritative scope for a given task; outrank this document per the Authority Order in §1 |
| ADRs (`Docs/ADR/`) | Authoritative architecture decisions; outrank this document per the Authority Order in §1 |
| AI Memory (`AI/Memory/`) | Subordinate to this document and to all formal documents per §1 and §14 |

---

## 21. Multi-Agent Collaboration

### Agent Responsibilities

Each AI Agent instance operating under a role defined in §2 is responsible only for the actions within that role's boundary. When one AI Agent's output (for example, a Task Plan produced by a Planning Agent) is consumed by another AI Agent (for example, an Implementation Agent), the consuming AI Agent must independently verify that output against actual repository state (§5) rather than trusting it without verification.

### Ownership Rules

- A task has exactly one active Implementation Agent at a time.
- Concurrent AI Agents must not edit the same files within the same task window.
- Whichever AI Agent is actively implementing a task owns the resulting worktree changes until its Completion Report is delivered.
- An AI Agent picking up a task in Continuation Mode (§4) must treat a prior agent's uncommitted work as protected under Worktree Safety (§15), unless the user explicitly authorizes discarding it.

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
