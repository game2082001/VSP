# AI Development Kit

**Status:** Stable
**Owner:** AI Development Kit
**Last Updated:** 2026-08-15
**Next Task:** Task-AI01-006 Phase 2E — Codex Independent Re-Review of the Phase 2D remediation (Phase 2C review found 3 findings; Phase 2D remediation applied 2026-08-15; not yet committed or Product-Owner-accepted). See the Governance Backlog (`OperatingSystem/README.md`) and `VERSION.md` (current version 1.2.0).

---

v1.2.0 (Stable — Governance Backlog Amendment Applied) is in progress in the working tree, not yet accepted. Governance changes are exceptional — made only via a Product-Owner-approved Governance Backlog entry when a real Epic exposes a genuine defect; v1.2.0 (Task-AI01-006) is the first such exception since v1.1.0 entered Stable. It was implemented (Phase 2B), independently reviewed with findings (Phase 2C), and remediated (Phase 2D); it is not yet committed, not yet independently re-reviewed, and not yet Product-Owner-accepted — see `VERSION.md`. Future work is driven by the Product Roadmap and executed Epic by Epic; this Kit does not generate its own work.

This Kit's governance rules begin with a single foundational axiom — see the top of [`OperatingSystem/AI_OPERATING_SYSTEM.md`](OperatingSystem/AI_OPERATING_SYSTEM.md): *"An approved Epic is a complete authorization for implementation within its approved scope"* / *"The default behaviour is CONTINUE, not STOP."*

---

## Purpose

This directory is a shared index and navigation layer for AI agents (Claude Code, ChatGPT, Codex, and others) working in the VSP repository.

It exists to give every agent a single, predictable place to look for:

- how to operate within this repository (rules, boundaries, workflow)
- where architecture, product, and repository-structure information actually lives
- reusable templates for future tasks
- a running index of AI-Kit-specific state (once populated)

This directory does **not** replace or duplicate the project's existing documentation. See "Relationship to Existing Docs" below.

---

## Entry Points (unchanged)

This kit does not change how an agent starts a task:

- **Claude Code** agents: start at root [`CLAUDE.md`](../CLAUDE.md).
- **All other AI agents** (ChatGPT, Codex, etc.): start at root [`AGENTS.md`](../AGENTS.md).

Both of those files remain the primary entry points for this repository. `AI/` is a supplementary index they may point into, not a replacement for either.

---

## AI Startup Sequence

Recommended order for an agent to load context before starting any task:

1. Root [`CLAUDE.md`](../CLAUDE.md) (Claude Code) or root [`AGENTS.md`](../AGENTS.md) (other agents)
2. Shared AI Software Engineering Standard (external repo `AI-Software-Engineering-Standard`, as required by `AGENTS.md`)
3. [`Docs/PROJECT.md`](../Docs/PROJECT.md) — project reference (scope, tech stack, solution structure)
4. [`Docs/AI_DEVELOPMENT_WORKFLOW.md`](../Docs/AI_DEVELOPMENT_WORKFLOW.md) — the Task → Approval → Implementation workflow
5. [`Docs/DEVELOPMENT_ROLES.md`](../Docs/DEVELOPMENT_ROLES.md) — roles and responsibilities
6. This file (`AI/README.md`) — to see whether a relevant AI-Kit subdirectory applies to the task at hand
7. The relevant `AI/<Category>/README.md`, if the task touches that category (e.g. an architecture task → [`AI/Architecture/README.md`](Architecture/README.md))
8. The Task Spec itself (`Docs/SPECS/Task-XXX.md`), supplied per task

Steps 1–5 are the existing, authoritative startup sequence already defined by `AGENTS.md` / `CLAUDE.md`. Steps 6–7 are additive navigation this kit provides.

---

## Directory Map

| Directory | Future Purpose | Status |
|---|---|---|
| [`OperatingSystem/`](OperatingSystem/README.md) | AI Operating System, Autonomous Development (Epic Governance), Decision Engine, Risk Matrix, Task Selection Rules, Approval Boundary | **`AI_OPERATING_SYSTEM.md`, `AUTONOMOUS_DEVELOPMENT.md` established** |
| [`Architecture/`](Architecture/README.md) | Architecture Rules, Module Boundaries, Dependency Rules, Discovery/Driver Architecture, ADR Index | README only |
| [`Repository/`](Repository/README.md) | Project Structure, Directory Guide, Module Map, Technology Stack, Naming Convention | README only |
| [`Standards/`](Standards/README.md) | Coding Rules, Testing Rules, Review Checklist, Documentation Rules, Git Rules | README only |
| [`Product/`](Product/README.md) | Product Vision, Roadmap Index, Product Backlog, Architecture Backlog, Technical Debt Backlog, Release Plan | README only |
| [`Memory/`](Memory/README.md) | Current State, Completed Tasks, Decisions, Known Technical Debt, Next Action | README only |
| [`Templates/`](Templates/README.md) | Reusable templates for future AI tasks (task plans, spec skeletons, review reports) | README only |

`OperatingSystem/` now holds two established documents (`AI_OPERATING_SYSTEM.md`, `AUTONOMOUS_DEVELOPMENT.md`); every other directory still contains only a `README.md` describing its intended future contents. No rule files, checklists, backlogs, or state files have been created yet for those — that remains intentionally out of scope until a future, separately approved task addresses each one.

---

## What Is Not Yet Built

The following are explicitly **not created** and are reserved for future, separately approved tasks:

- `DECISION_ENGINE.md`, `RISK_MATRIX.md`
- `ARCHITECTURE_RULES.md`, `MODULE_BOUNDARIES.md`, `DEPENDENCY_RULES.md`
- `CODING_RULES.md`, `TESTING_RULES.md`, `REVIEW_CHECKLIST.md` (under `AI/`; distinct from the existing `Docs/02_CODING_RULES.md`)
- `PRODUCT_BACKLOG.md`, `ARCHITECTURE_BACKLOG.md`
- `CURRENT_STATE.md`, `COMPLETED_TASKS.md`, `NEXT_ACTION.md`
- Any task templates under `Templates/`
- A new version of root `CLAUDE.md`

---

## Relationship to Existing Docs

The AI Development Kit is an index and navigation layer. It is **not** a second source of truth. Where a topic already has an authoritative document, the corresponding `AI/` README links to it instead of restating it:

| Topic | Authoritative Source | AI Kit Role |
|---|---|---|
| Architecture vision & layering | [`Docs/00_ARCHITECTURE_VISION.md`](../Docs/00_ARCHITECTURE_VISION.md), [`Docs/01_ARCHITECTURE.md`](../Docs/01_ARCHITECTURE.md) | Index only, see `Architecture/README.md` |
| Product roadmap | [`Docs/03_PRODUCT_ROADMAP.md`](../Docs/03_PRODUCT_ROADMAP.md) | Index only, see `Product/README.md` |
| Task specifications | `Docs/SPECS/Task-XXX.md` (per task) | Not indexed here; supplied per task |
| Architecture decisions | [`Docs/ADR/`](../Docs/ADR/) | Index only, see `Architecture/README.md` |
| Coding rules & AI workflow | [`Docs/02_CODING_RULES.md`](../Docs/02_CODING_RULES.md), [`Docs/AI_PLAYBOOK.md`](../Docs/AI_PLAYBOOK.md), [`Docs/AI_DEVELOPMENT_WORKFLOW.md`](../Docs/AI_DEVELOPMENT_WORKFLOW.md), external `AI-Software-Engineering-Standard` repo | Index only, see `Standards/README.md` |
| Project structure & tech stack | [`Docs/PROJECT.md`](../Docs/PROJECT.md) | Index only, see `Repository/README.md` |

Note: [`Docs/00_AI_CONTEXT.md`](../Docs/00_AI_CONTEXT.md) is an older, partially outdated context document that predates the current `CLAUDE.md` / `AGENTS.md` / `AI_DEVELOPMENT_WORKFLOW.md` setup and is not fully consistent with them. This Task does not modify, replace, or reconcile it. Agents should treat `AGENTS.md`, `CLAUDE.md`, and `Docs/AI_DEVELOPMENT_WORKFLOW.md` as the current authority where the two conflict.

See [`VERSION.md`](VERSION.md) for the current phase of this kit.
