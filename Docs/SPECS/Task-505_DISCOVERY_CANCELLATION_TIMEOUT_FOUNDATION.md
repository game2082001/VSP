# Task-505 Discovery Cancellation / Timeout Foundation

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.5

---

# 1. Purpose

Task-505 establishes the Discovery Cancellation / Timeout Foundation.

The purpose is to formally define:

- Caller Cancellation
- Operation Timeout
- Partial Result
- Cancellation Boundary

Task-505 keeps the current Discovery Foundation architecture aligned with:

- Result
- Session
- Progress

Task-505 does not add production code.

Task-505 does not add tests.

Task-505 does not introduce a global timeout policy.

---

# 2. Architecture Overview

Discovery pipeline:

```text
Caller
    |
    v
DiscoveryOrchestrator
    |
    v
AutoDiscoveryCoordinator
    |
    v
Discovery Services
    |
    v
Transport / Probe
```

Cancellation is initiated by the caller.

Cancellation is propagated through:

- `CancellationToken`

Timeout is controlled at operation level.

Timeout is not caller cancellation.

Operation-level timeout examples:

- ONVIF Discovery timeout
- RTSP Endpoint Probe timeout
- Network Scan target timeout

Current scope must not add:

- `DiscoveryOrchestrationStatus.TimedOut`
- `DiscoverySessionStatus.TimedOut`
- `DiscoveryProgressStage.TimedOut`

---

# 3. Current-State Analysis

Current Discovery implementation already includes caller cancellation support at several levels:

- `DiscoveryOrchestrator.ExecuteAsync(..., CancellationToken)`
- `AutoDiscoveryCoordinator.DiscoverAsync(..., CancellationToken)`
- `OnvifDiscoveryService.DiscoverAsync(..., CancellationToken)`
- `RtspEndpointProbeService.ProbeAsync(..., CancellationToken)`
- `NetworkScanService.ScanAsync(..., CancellationToken)`
- transport and probe implementations

Current timeout support exists only at operation level:

- `OnvifDiscoveryRequest.Timeout`
- `RtspEndpointProbeRequest.Timeout`
- `NetworkScanRequest.Timeout`

Current orchestration result statuses include:

- `Completed`
- `CompletedWithErrors`
- `Failed`
- `Cancelled`
- `InvalidRequest`

Current session statuses include:

- `Created`
- `Running`
- `Completed`
- `CompletedWithErrors`
- `Cancelled`
- `Failed`

Current progress stages include:

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

Current implementation does not define:

- orchestration-level timeout
- timeout lifecycle status
- timeout progress stage
- timeout session status
- global timeout policy
- retry policy
- resume policy

---

# 4. Cancellation Semantics

Caller Cancellation means:

- a user or upper layer requested Discovery to stop
- the request is cooperative
- running components should observe the provided `CancellationToken`
- cancellation should not be treated as protocol failure
- cancellation should not be treated as operation timeout

Result semantics:

- caller cancellation should produce `DiscoveryOrchestrationStatus.Cancelled`
- cancellation reasons should clearly indicate caller cancellation
- cancellation must not be reported as operation timeout

Cancellation must remain cooperative.

Components that receive a token should pass it to lower-level async operations where applicable.

Components should not block indefinitely after cancellation is requested.

---

# 5. Timeout Semantics

Operation Timeout means:

- a specific discovery operation exceeded its own allowed time budget
- the timeout belongs to that operation
- the timeout does not imply caller cancellation
- the timeout does not automatically cancel the full orchestration

Examples:

- ONVIF stops collecting WS-Discovery responses after its timeout.
- RTSP probe returns timeout classification after its timeout.
- Network Scan returns timed-out reachability result for a target after its timeout.

Operation timeout is operation-level.

Operation timeout is not orchestration-level.

Task-505 must not add timeout lifecycle statuses in the first version.

Specifically, Task-505 must not add:

- `DiscoveryOrchestrationStatus.TimedOut`
- `DiscoverySessionStatus.TimedOut`
- `DiscoveryProgressStage.TimedOut`

Future orchestration-level timeout must be defined by a separate approved task.

---

# 6. Partial Result Contract

A cancelled result may retain stable candidate results that completed before cancellation was observed.

Allowed:

- completed candidate results before cancellation
- summary derived from completed candidate results included in the cancelled result
- cancellation reason at batch level

Not allowed:

- fabricated candidate results for in-flight work
- fabricated candidate results for unstarted work
- guessed statuses for candidates that were not completed
- summary based on original request candidate count when only partial completed candidates are included

`DiscoveryOrchestrationResult` may represent a cancelled execution with partial completed results.

The result must include only stable final candidate results.

An in-flight candidate is not stable.

An unstarted candidate is not stable.

---

# 7. Summary Rules

`DiscoveryOrchestrationSummary` must remain the canonical summary.

For cancelled results, summary must be consistent with `CandidateResults` included in the result.

Summary must not be created from:

- original request candidate count
- planned candidate count
- estimated candidate count
- unstarted candidate count

If future workflows need to preserve original candidate count, planned candidate count, or cancellation timing metadata, that data should belong to execution metadata.

Execution metadata must not be added to canonical summary without a separate approved architecture change.

Summary remains:

- final-result summary
- derived from included stable candidate results
- not a lifecycle model
- not a progress model

---

# 8. Relationship Diagram

```text
Caller
    |
    | CancellationToken
    v
DiscoveryOrchestrator
    |
    | passes token
    v
AutoDiscoveryCoordinator
    |
    | passes token
    v
Discovery Services
    |
    | passes token
    v
Transport / Probe

Caller Cancellation
    |
    v
DiscoveryOrchestrationResult
    |  Status = Cancelled
    |  CandidateResults = completed stable candidates only
    |  Summary = derived from included CandidateResults
    v
DiscoverySessionFactory
    |
    v
DiscoverySession
    |  Status = Cancelled
    v
DiscoverySessionSnapshot
    |  reuses DiscoveryOrchestrationSummary

Future Progress Runner / Publisher
    |
    v
DiscoveryProgress
    |  Stage = Cancelled
    v
DiscoveryProgressSnapshot
```

Relationship rules:

- Result can represent cancellation.
- Session can map cancellation from result.
- Progress can represent cancellation stage.
- Session must not control cancellation.
- Progress must not control cancellation.
- Result must not control cancellation.
- Caller owns cancellation request.
- Orchestrator coordinates cancellation observation and result creation.

---

# 9. Architecture Rules

## Rule 1

Caller Cancellation and Operation Timeout have different semantics.

Caller Cancellation means the caller requested the workflow to stop.

Operation Timeout means one operation exceeded its own time budget.

## Rule 2

Cancellation must be cooperative.

Cancellation must be represented by `CancellationToken` propagation.

Components should observe cancellation at safe boundaries.

## Rule 3

Cancelled Result may preserve completed candidates.

Only stable final candidate results may be included.

## Rule 4

Cancelled Result must not create in-flight candidate results.

An in-flight candidate has no stable final result.

## Rule 5

Cancelled Result must not create unstarted candidate results.

Unstarted work must not be represented as completed, failed, skipped, rejected, or cancelled candidate output unless a future approved model explicitly supports planned work metadata.

## Rule 6

Operation Timeout must not automatically become Orchestration Timeout.

Operation-level timeout may be represented in operation-specific result models or candidate evidence.

It must not automatically produce a timed-out orchestration lifecycle.

## Rule 7

Session and Progress may map cancellation.

They must not control cancellation.

Session maps final lifecycle state from result.

Progress represents runtime state through future runner or publisher boundaries.

---

# 10. Compatibility

## Backward Compatibility

Task-505 should be backward compatible.

Existing cancellation status remains:

- `DiscoveryOrchestrationStatus.Cancelled`
- `DiscoverySessionStatus.Cancelled`
- `DiscoveryProgressStage.Cancelled`

Existing operation-level timeout behavior remains unchanged.

## Forward Compatibility

Future work may add:

- global timeout policy
- timeout result reason codes
- timeout-aware execution metadata
- progress closeout behavior
- session recorder integration
- retry or resume policies

These extensions should not require changing the basic distinction between caller cancellation and operation timeout.

## Migration Impact

Task-505 requires no repository migration.

Task-505 requires no SQLite schema migration.

Task-505 requires no data migration.

## Breaking Changes

Task-505 must not introduce breaking changes.

Breaking changes would include:

- treating operation timeout as caller cancellation
- replacing existing cancelled statuses
- adding required timeout fields to canonical result models
- changing repository or SQLite contracts
- making Session or Progress control cancellation

---

# 11. Future Extension

Version 2 may add:

- Global Timeout Policy
- Timeout Status
- Resume
- Retry
- Pause
- Lifecycle Expansion
- Cancellation metadata
- Execution metadata
- Progress closeout publisher
- Session recorder integration

All future extensions require separate approved specs.

Global timeout policy should be distinct from operation timeout.

Retry and resume should not be hidden inside cancellation handling.

Pause should not be modeled as cancellation.

---

# 12. Risks

## Half-Completed Result

Risk:

- cancelled result may include unstable or guessed candidate results.

Mitigation:

- include only completed stable candidate results.
- do not fabricate in-flight or unstarted candidates.

## Summary Inconsistency

Risk:

- summary may be calculated from original request size instead of included candidate results.

Mitigation:

- derive summary from included stable candidate results.
- move original request count to future execution metadata if needed.

## Timeout / Cancellation Confusion

Risk:

- operation timeout may be mistaken for caller cancellation.

Mitigation:

- keep operation timeout in operation-specific result models.
- use `Cancelled` only for caller cancellation.

## In-Flight Candidate Fabrication

Risk:

- orchestration may create partial candidate result for work that did not finish.

Mitigation:

- in-flight work must not be emitted as a final candidate result.

## Zombie Discovery

Risk:

- a lower-level operation may not observe cancellation promptly.

Mitigation:

- define cancellation as cooperative.
- require token propagation through services, transports, and probes.

---

# 13. Out Of Scope

- Production code
- Tests
- Task-505 implementation
- Driver changes
- Timeout Policy
- Orchestration-level timeout
- Retry
- Resume
- Pause
- UI
- Repository
- SQLite
- Metrics
- Reporting
- Export
- Session recorder implementation
- Progress publisher implementation
- Global lifecycle redesign
- Version tag
- GitHub Release
- Commit

---

# 14. Non-Goals

Task-505 will not evolve into:

- a retry engine
- a timeout scheduler
- a progress publisher
- a session recorder
- a repository workflow
- a UI cancellation workflow
- a driver execution framework
- a global lifecycle state machine

---

# 15. Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-505 Discovery Cancellation / Timeout Foundation`

Spec filename:

- `Docs/SPECS/Task-505_DISCOVERY_CANCELLATION_TIMEOUT_FOUNDATION.md`
