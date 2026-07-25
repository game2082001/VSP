# AI Playbook

Version: 1.1

---

## Before Coding

Always read:

- PROJECT.md
- DEVELOPMENT_GUIDE.md
- DEVELOPMENT_ROLES.md
- Coding Rules
- Current Task Spec

See Principle 1 — Think Before Coding (`AI/OperatingSystem/AI_OPERATING_SYSTEM.md`, Core Principles, and its §9): no coding begins before a Task Plan is submitted.

---

## Architecture Rules

Presentation

↓

Application

↓

Domain

↓

Repository

↓

Infrastructure

Never skip layers.

---

## Naming Rules

Service

Mapper

Repository

Result

Error

ViewModel

Avoid:

Manager

Helper

Utils

---

## Coding Rules

See Principle 2 — Simplicity First and Principle 3 — Surgical Changes (`AI/OperatingSystem/AI_OPERATING_SYSTEM.md`, Core Principles) and `Docs/02_CODING_RULES.md` §2, §12 for the full rule set (readability, small methods/classes, reuse before rewrite, avoid over-engineering, avoid duplicate abstractions).

---

## Review Rules

Every task should provide:

- Modified Files
- New Files
- Build Result
- Test Result
- Risk Report
- Suggested Commit

---

## Testing

Every new feature must include Unit Tests.

Update existing tests if behavior changes.

---

## Documentation

Update:

CHANGELOG

ROADMAP

Current Spec

when implementation changes.