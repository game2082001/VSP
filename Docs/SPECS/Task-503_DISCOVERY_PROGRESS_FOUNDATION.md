# Task-503 Discovery Progress Foundation

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.5

---

# 1. Purpose

Task-503 establishes the Discovery Progress Foundation.

`DiscoveryProgress` represents runtime execution state for Discovery.

It is:

- an execution-state model
- a stage-level progress model
- transport-agnostic progress state
- independent from UI and publisher implementations

It is not:

- UI Model
- Progress Bar
- Session
- Repository
- History
- EventBus
- Logger
- Console Output

Task-503 does not modify production code.

Task-503 does not add tests.

Task-503 does not define a publisher or observer implementation.

---

# 2. Architecture Overview

Discovery pipeline:

`Discovery Source`

-> `DiscoveryOrchestrator`

-> `DiscoveryOrchestrationResult`

Progress pipeline:

`DiscoveryRunner`

-> `DiscoveryProgress`

-> `DiscoveryProgressSnapshot`

-> `Progress Publisher`

-> `UI / SignalR / Console`

Current Task-503 scope only defines the progress domain model.

`DiscoveryRunner`, `Progress Publisher`, UI, SignalR, and Console output are future concerns.

`DiscoveryOrchestrator` must remain progress-agnostic.

---

# 3. Current-State Analysis

Current Discovery implementation includes:

- `DiscoveryOrchestrator`
- `DiscoveryOrchestrationResult`
- `DiscoveryOrchestrationSummary`
- `CandidateOrchestrationResult`
- `DiscoverySession`
- `DiscoverySessionFactory`
- `DiscoverySessionSnapshot`

Current Discovery implementation does not include:

- `DiscoveryProgress`
- `DiscoveryProgressStage`
- `DiscoveryProgressSnapshot`
- `ProgressReason`
- progress publisher
- progress observer
- progress percentage
- current operation model
- current step model
- total step model

Current UI has import wizard step labels, but those belong to Import UI workflow and are not Discovery Progress.

There is no Discovery progress dependency in UI.

There is no Discovery observer, publisher, SignalR, EventBus, Console, or logging progress mechanism.

---

# 4. DiscoveryProgress

`DiscoveryProgress` is an execution-state model.

It represents the current high-level progress state of Discovery execution.

## Allowed Fields

`DiscoveryProgress` may store:

- `Stage`
- `CompletedSteps`
- `TotalSteps`
- snapshot reference

## Disallowed Fields

`DiscoveryProgress` must not store:

- `Percentage`
- Candidate
- Driver
- Camera
- Repository
- UI State
- Progress Bar
- EventBus
- Logger
- Console writer
- service instance

## Percentage Rule

`Percentage` must not be stored.

Percentage may be derived from:

- `CompletedSteps`
- `TotalSteps`

Reason:

- storing both percentage and step counts can create inconsistent progress state.
- some future discovery flows may not know exact total work.

If `TotalSteps` is unknown or zero, callers should treat percentage as unavailable.

---

# 5. DiscoveryProgressSnapshot

`DiscoveryProgressSnapshot` represents a point-in-time view of progress state.

It should contain:

- `DiscoveryProgressStage`
- `CurrentOperation`
- `CompletedSteps`
- `TotalSteps`
- `ProgressReason[]`

It must not contain:

- Candidate Details
- Driver Objects
- Camera Objects
- Repository
- UI State
- Progress Bar
- EventBus
- Logger
- Console writer

## Immutability Rule

`DiscoveryProgressSnapshot` is immutable.

Existing snapshots must not be modified.

When progress state changes, create a new snapshot.

---

# 6. DiscoveryProgressStage

`DiscoveryProgressStage` should define stage-level progress.

Required stages:

- `NotStarted`
- `Discovering`
- `Mapping`
- `SelectingDriver`
- `AwaitingApproval`
- `CreatingCamera`
- `Registering`
- `Completed`
- `Cancelled`
- `Failed`

## Stage Semantics

`NotStarted`:

- entered before discovery execution begins.

`Discovering`:

- entered when discovery source or `DiscoveryOrchestrator` begins obtaining candidates.

`Mapping`:

- entered when candidate data is mapped into driver evidence.

`SelectingDriver`:

- entered when driver compatibility is evaluated.

`AwaitingApproval`:

- entered when driver approval cannot proceed automatically.

`CreatingCamera`:

- entered when an approved driver reference exists and camera creation begins.

`Registering`:

- entered when a camera exists and registration begins.

`Completed`:

- entered when discovery workflow completes successfully.

`Cancelled`:

- entered when caller cancellation stops execution.

`Failed`:

- entered when request-level or unrecoverable execution failure occurs.

Stage-level progress is intentionally coarse-grained.

Candidate-level progress is future scope.

---

# 7. ProgressReason

`ProgressReason` stores structured progress reason data.

Required fields:

- `Code`
- `Message`

Rules:

- `Code` must not be empty.
- `Message` must not be empty.
- Reasons should be stable enough for tests and human-readable diagnostics.

Future optional fields may include:

- `Severity`
- `Metadata`

Task-503 must not introduce those optional fields unless implementation is separately approved.

---

# 8. Progress Granularity

Task-503 supports stage-level progress only.

Examples:

- `Mapping`
- `SelectingDriver`
- `Registering`

Task-503 does not support candidate-level progress.

Candidate-level progress may be added in Version 2.

Reason:

- candidate-level progress requires careful handling of candidate identifiers and privacy-safe display values.
- candidate-level progress may require orchestration hooks or a runner.
- adding it now risks coupling progress to Discovery domain objects.

---

# 9. Relationship Diagram

```text
Discovery Source
    |
    v
DiscoveryOrchestrator
    |
    v
DiscoveryOrchestrationResult

Future:

DiscoveryRunner
    |
    v
DiscoveryProgress
    |  Stage
    |  CompletedSteps
    |  TotalSteps
    |  Snapshot Reference
    v
DiscoveryProgressSnapshot
    |  Stage
    |  CurrentOperation
    |  CompletedSteps
    |  TotalSteps
    |  Reasons
    v
Progress Publisher
    |
    v
UI / SignalR / Console
```

## Relationship With DiscoverySession

`DiscoverySession` represents execution lifecycle.

`DiscoveryProgress` represents current execution state.

`DiscoveryProgress` must not be owned by `DiscoverySession`.

Future work may correlate progress with a session by using `DiscoverySessionId`, but Task-503 does not require that field.

## Relationship With DiscoveryOrchestrator

`DiscoveryOrchestrator` must remain progress-agnostic.

Task-503 must not require `DiscoveryOrchestrator` to publish progress directly.

Future progress reporting should be added through a runner, wrapper, or approved orchestration boundary.

## Relationship With DiscoveryOrchestrationResult

`DiscoveryOrchestrationResult` is final output.

`DiscoveryProgress` is runtime state.

Progress models must not replace orchestration result models.

---

# 10. Architecture Rules

## Rule 1

`DiscoveryProgress` is an execution-state model.

It must not run Discovery, select drivers, create cameras, register devices, or publish events.

## Rule 2

`DiscoveryProgressSnapshot` is immutable.

New progress state must create a new snapshot.

Existing snapshots must not be modified.

## Rule 3

`DiscoveryProgress` must remain transport-agnostic.

It must not depend on:

- WPF
- WinForms
- SignalR
- REST
- Console
- Logging
- EventBus

## Rule 4

`DiscoveryOrchestrator` must remain progress-agnostic.

The orchestrator must not directly create, mutate, publish, or depend on `DiscoveryProgress`.

---

# 11. Compatibility

## Backward Compatibility

Task-503 should be additive.

It should not modify:

- `DiscoveryOrchestrator`
- `DiscoverySession`
- `DiscoveryOrchestrationResult`
- UI
- repository contracts

Existing callers should continue to work without progress models.

## Forward Compatibility

The model should allow future:

- progress publisher
- progress observer
- progress recorder
- UI progress adapter
- SignalR adapter
- console adapter
- Web API adapter

without changing core progress state shape.

## Migration Impact

No migration is required for Task-503 spec.

Future implementation should add new files only.

## Breaking Changes

Task-503 must not introduce breaking changes.

---

# 12. Future Extension

Version 2 may add:

- `IDiscoveryProgressPublisher`
- `IDiscoveryProgressObserver`
- SignalR adapter
- Console adapter
- Web API adapter
- Progress Recorder

Future extension should avoid modifying `DiscoveryOrchestrator`.

Recommended future approach:

- introduce `DiscoveryRunner` or wrapper around orchestration
- runner creates progress snapshots
- publisher sends snapshots to observers or transports
- transport adapters convert snapshots into UI, SignalR, Console, or Web API output

Progress publishing must remain separate from progress model.

---

# 13. Files

## Spec

This task adds:

- `Docs/SPECS/Task-503_DISCOVERY_PROGRESS_FOUNDATION.md`

## Future Implementation

Future implementation may add:

- `VSP.Device/Discovery/Progress/DiscoveryProgress.cs`
- `VSP.Device/Discovery/Progress/DiscoveryProgressStage.cs`
- `VSP.Device/Discovery/Progress/DiscoveryProgressSnapshot.cs`
- `VSP.Device/Discovery/Progress/ProgressReason.cs`

Optional future extension files may include:

- `VSP.Device/Discovery/Progress/IDiscoveryProgressPublisher.cs`
- `VSP.Device/Discovery/Progress/IDiscoveryProgressObserver.cs`
- `VSP.Device/Discovery/Progress/DiscoveryProgressRecorder.cs`
- `VSP.Device/Discovery/Progress/DiscoveryRunner.cs`

## Future Tests

Future implementation tests may include:

- `VSP.Tests/Discovery/DiscoveryProgressTests.cs`
- `VSP.Tests/Discovery/DiscoveryProgressSnapshotTests.cs`

Test coverage should verify:

- progress stores stage and step counts
- percentage is not stored
- snapshot is immutable
- reasons are structured
- progress does not hold candidate details
- progress does not depend on UI or transport types
- orchestrator remains progress-agnostic

---

# 14. Risks

## Progress Becoming UI Model

Risk:

- Progress could contain WPF, progress bar, or UI-specific fields.

Mitigation:

- keep progress transport-agnostic and UI-free.

## Session Coupling

Risk:

- Progress may be embedded into `DiscoverySession`.

Mitigation:

- keep progress separate from session lifecycle.

## Mutable Snapshot

Risk:

- mutating snapshots would make future history or replay unreliable.

Mitigation:

- snapshots are immutable.

## Progress Owning Domain Objects

Risk:

- progress may store candidates, drivers, cameras, or repositories.

Mitigation:

- store stage and display-safe operation text only.

## Percentage Inconsistency

Risk:

- stored percentage may conflict with completed and total step counts.

Mitigation:

- do not store percentage.
- derive it from counts when possible.

## Progress Becoming God Object

Risk:

- progress may absorb publishing, retry, timeout, history, and logging.

Mitigation:

- Task-503 defines only domain progress state.

---

# 15. Out Of Scope

- Production code
- Tests
- Task-503 implementation
- UI
- SignalR
- EventBus
- Repository
- History
- Retry
- Progress publisher implementation
- Observer implementation
- Timeout
- Logging
- Console Output
- Web API
- DiscoveryRunner implementation
- Commit

---

# 16. Non-Goals

Task-503 will not evolve into:

- a UI progress bar
- a SignalR hub
- an EventBus
- a logging framework
- a repository workflow
- a history store
- a retry engine
- a timeout policy
- a discovery orchestrator

---

# 17. Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-503 Discovery Progress Foundation`

Spec filename:

- `Docs/SPECS/Task-503_DISCOVERY_PROGRESS_FOUNDATION.md`
