# Task-601 Discovery Runner Foundation

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.6

---

# 1. Purpose

Task-601 establishes the Discovery Runner Foundation.

`DiscoveryRunner` is the first Execution Layer component for Discovery.

Its purpose is to provide an execution boundary around the existing Discovery Orchestrator.

It is:

- an execution boundary
- an execution context creator or carrier
- a caller-facing wrapper for Discovery execution
- a future hook point owner

It is not:

- a domain orchestrator
- a discovery implementation
- a driver selection engine
- a camera factory
- a registration service
- a retry framework
- a timeout policy
- an observability framework

Task-601 must preserve the existing Version 1.5 Discovery Foundation architecture.

The runner must call the existing `DiscoveryOrchestrator` and return the canonical `DiscoveryOrchestrationResult`.

---

# 2. Architecture Overview

Recommended architecture:

```text
Caller
    |
    v
DiscoveryRunner
    |
    v
DiscoveryOrchestrator
    |
    v
DiscoveryOrchestrationResult
```

`DiscoveryRunner` owns execution-level concerns.

`DiscoveryOrchestrator` owns domain orchestration.

`DiscoveryOrchestrationResult` remains the canonical final result.

The runner must not create a replacement result model.

The runner must not duplicate orchestrator workflow.

---

# 3. Current-State Analysis

Version 1.5 Discovery Foundation currently provides:

- Discovery candidate production through `AutoDiscoveryCoordinator`
- candidate merge and source attribution
- candidate evidence mapping through `AutoDiscoveryCandidateEvidenceMapper`
- driver compatibility evaluation through `DriverSelectionService`
- approval policy boundary through `IDriverApprovalPolicy`
- camera entity creation through `CameraFactory`
- device registration through `DeviceRegistrationService`
- canonical final result through `DiscoveryOrchestrationResult`
- lifecycle model through `DiscoverySession`
- progress model through `DiscoveryProgress`
- cancellation and partial result semantics

`DiscoveryOrchestrator.ExecuteAsync()` currently coordinates:

```text
Request Validation
    |
    v
Discovery or Candidate Input
    |
    v
Candidate Processing
    |
    v
Evidence Mapping
    |
    v
Driver Selection
    |
    v
Driver Approval
    |
    v
Camera Factory
    |
    v
Device Registration
    |
    v
Summary Creation
    |
    v
DiscoveryOrchestrationResult
```

These are domain orchestration responsibilities and must stay in `DiscoveryOrchestrator`.

The missing Version 1.6 layer is an execution boundary above the orchestrator.

---

# 4. Responsibilities

`DiscoveryRunner` may:

- provide an execution boundary
- create or pass a `CorrelationId`
- create an execution context
- pass `CancellationToken`
- call `DiscoveryOrchestrator`
- return `DiscoveryOrchestrationResult`
- reserve future hook points

`DiscoveryRunner` must not:

- parse candidates
- merge candidates
- map evidence
- select drivers
- approve drivers directly
- call Camera Factory directly
- call Device Registration directly
- create summary
- aggregate results
- invoke driver factories
- write repositories
- access SQLite
- execute retry logic
- apply timeout policy
- publish progress
- record sessions
- collect metrics
- own logging provider behavior
- implement UI workflow
- parse import data
- perform connection testing

---

# 5. Execution Context

Task-601 may define an execution context model.

First version execution context should contain only:

- `ExecutionId`
- `CorrelationId`
- `CancellationToken`
- execution options, only if required

`ExecutionId` identifies one runner execution boundary.

`CorrelationId` groups related work across components.

`CancellationToken` is supplied by the caller and passed through to orchestration.

Execution options must remain minimal.

Execution context must not contain:

- Candidate
- Driver
- Camera
- Registration
- Session
- Progress
- Metrics
- repository object
- UI state
- retry policy
- timeout policy
- logging provider

If future execution options are needed, they must not smuggle retry, timeout, progress, session, metrics, or logging behavior into Task-601.

---

# 6. Dependency Rules

First version `DiscoveryRunner` should depend only on:

- `DiscoveryOrchestrator`
- execution context model, if introduced

`DiscoveryRunner` must not depend on:

- `DiscoverySessionFactory`
- progress publisher
- metrics collector
- retry policy
- timeout policy
- repository
- SQLite
- UI
- logging framework
- driver registry
- driver selection service
- camera factory
- device registration service
- discovery protocol services directly

`DiscoveryOrchestrator` must not depend on `DiscoveryRunner`.

Dependency direction:

```text
Caller
    |
    v
DiscoveryRunner
    |
    v
DiscoveryOrchestrator
    |
    v
Version 1.5 Foundation Components
```

Forbidden direction:

```text
DiscoveryOrchestrator
    |
    v
DiscoveryRunner
```

---

# 7. Architecture Rules

## Rule 1

Runner must never parse Discovery Candidate.

Candidate parsing, normalization, and merge behavior belong below the execution boundary.

## Rule 2

Runner must never know Driver.

Driver compatibility, approval, and metadata handling belong to Version 1.5 domain orchestration and driver selection foundations.

## Rule 3

Runner must never know Camera.

Camera creation belongs to `CameraFactory`.

Runner must not inspect, mutate, or create camera entities.

## Rule 4

Runner must never know Registration.

Device registration belongs to `DeviceRegistrationService`.

Runner must not perform duplicate detection or repository persistence.

## Rule 5

Runner must not create a new canonical result.

Runner must directly return `DiscoveryOrchestrationResult` from `DiscoveryOrchestrator`.

It must not introduce:

- `DiscoveryRunnerResult`
- `ExecutionResult`
- `DiscoveryExecutionResult`

unless a future approved architecture task explicitly changes this boundary.

## Rule 6

Orchestrator must not depend on Runner.

The runner is above orchestration.

The orchestrator must remain reusable without the execution layer.

---

# 8. Future Extension

Future Version 1.6 tasks may extend the execution layer with:

- Progress Hook
- Session Hook
- Metrics Hook
- Retry Hook
- Timeout Hook
- Diagnostics Hook

These future hooks should be added around the runner boundary, not inside domain orchestration.

Future extensions must preserve:

- runner as execution boundary
- orchestrator as domain orchestrator
- canonical `DiscoveryOrchestrationResult`
- session/progress independence
- operation timeout versus caller cancellation separation

Future hook implementation must require separate approved specs.

Task-601 must not implement these hooks.

---

# 9. Relationship With Version 1.5 Foundations

## Relationship With Task-501

Task-501 defines `DiscoveryOrchestrator`.

Task-601 wraps `DiscoveryOrchestrator`.

Task-601 must not move Task-501 domain orchestration behavior into the runner.

## Relationship With Task-502

Task-502 defines `DiscoverySession`.

Task-601 must not depend on `DiscoverySessionFactory` in the first version.

Future session recording may be added as a runner hook in a separate task.

## Relationship With Task-503

Task-503 defines `DiscoveryProgress`.

Task-601 must not publish progress in the first version.

Future progress publishing may be added as a runner hook in a separate task.

## Relationship With Task-504

Task-504 defines `DiscoveryOrchestrationResult` as the canonical final result.

Task-601 must return that result directly.

Task-601 must not create duplicate result models.

## Relationship With Task-505

Task-505 defines caller cancellation and operation timeout semantics.

Task-601 should pass caller cancellation through `CancellationToken`.

Task-601 must not introduce global timeout policy or timeout lifecycle status.

---

# 10. Compatibility

## Backward Compatibility

Task-601 should be additive.

Existing callers of `DiscoveryOrchestrator` should continue to work.

No existing result, session, progress, repository, SQLite, driver, or camera contracts should be replaced.

## Forward Compatibility

The runner boundary should allow future execution services to attach without modifying `DiscoveryOrchestrator`.

Future extensions should prefer runner-level hooks over domain orchestration changes.

## Migration Impact

No data migration is required.

No SQLite schema migration is required.

No repository migration is required.

## Breaking Changes

Task-601 must not introduce breaking changes.

Breaking changes would include:

- replacing `DiscoveryOrchestrator`
- replacing `DiscoveryOrchestrationResult`
- forcing Session or Progress into current orchestration result
- changing repository contracts
- changing SQLite schema

---

# 11. Files

## Spec

This task adds:

- `Docs/SPECS/Task-601_DISCOVERY_RUNNER_FOUNDATION.md`

## Future Implementation

Future implementation may add files under:

- `VSP.Device/Discovery/Execution/`

Potential future files:

- `DiscoveryRunner.cs`
- `DiscoveryExecutionContext.cs`
- `DiscoveryExecutionId.cs`
- optional execution options model, only if approved
- corresponding unit tests

Future implementation requires a separate approved implementation plan.

---

# 12. Risks

## Runner Becoming God Service

Risk:

- Runner may absorb orchestration, progress, session, retry, timeout, metrics, logging, and UI responsibilities.

Mitigation:

- keep Runner limited to execution boundary and orchestrator invocation.

## Duplicate Orchestration

Risk:

- Runner may duplicate candidate processing or summary aggregation.

Mitigation:

- all domain orchestration remains in `DiscoveryOrchestrator`.

## Duplicate Result Model

Risk:

- Runner may introduce an execution result that competes with `DiscoveryOrchestrationResult`.

Mitigation:

- runner returns canonical result directly.

## Dependency Direction Reversal

Risk:

- Orchestrator may depend on Runner or execution context.

Mitigation:

- runner is caller-side boundary above orchestrator.

## Hidden Execution Policy

Risk:

- retry, timeout, progress, metrics, or logging behavior may be hidden inside Task-601.

Mitigation:

- reserve hooks only; implement policies in future approved tasks.

## Context Bloat

Risk:

- execution context may become a container for candidate, driver, camera, registration, session, progress, or metrics objects.

Mitigation:

- first version context contains only execution identity, correlation, cancellation, and minimal approved options.

---

# 13. Out Of Scope

- Production code
- Tests
- Task-601 implementation planning
- Task-601 implementation
- Retry
- Timeout
- Session Recorder
- Progress Publisher
- Metrics Collector
- Logging
- Observability
- Diagnostics implementation
- Repository
- SQLite
- UI
- Pipeline Framework
- Middleware
- Driver Selection changes
- Driver Approval changes
- Camera Factory changes
- Device Registration changes
- Discovery protocol changes
- Import parser changes
- Version Tag
- GitHub Release
- Commit

---

# 14. Non-Goals

Task-601 will not evolve into:

- a second Discovery Orchestrator
- a discovery pipeline framework
- middleware infrastructure
- a retry engine
- a global timeout policy
- a progress publisher
- a session recorder
- a metrics collector
- a logging framework
- a UI workflow
- a repository workflow
- a camera import flow

---

# 15. Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-601 Discovery Runner Foundation`

Spec filename:

- `Docs/SPECS/Task-601_DISCOVERY_RUNNER_FOUNDATION.md`
