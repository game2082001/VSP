# ADR-001 Import Architecture

## Status

Accepted

## Decision

The import workflow is:

Parser
→ Validation
→ Duplicate Checker
→ Import Pipeline
→ Preview Builder
→ Import Wizard
→ Import Executor
→ Repository
→ SQLite
→ Import Summary

## Rationale

Separate UI, Application and Persistence responsibilities.

Keep Preview model independent from Domain entities.

Use ImportResult as the common result model.

## Consequences

Easy to maintain.

Easy to extend.

Future features such as Progress, History and Rollback can reuse the existing architecture.