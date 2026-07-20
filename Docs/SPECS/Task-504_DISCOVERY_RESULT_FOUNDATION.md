# Task-504 Discovery Result / Summary Foundation

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.5

---

# 1. Purpose

Task-504 establishes the Discovery Result Foundation.

`DiscoveryOrchestrationResult` is the canonical Discovery final result.

It represents the final output of Discovery orchestration after candidate processing has completed, failed, or been cancelled.

Task-504 intentionally avoids creating parallel result models.

Task-504 must not create:

- `DiscoveryResult`
- `DiscoveryResultSummary`
- `DiscoveryResultReason`
- `DiscoveryResultStatistics`

Rationale:

- avoids duplicate models
- avoids counter drift
- keeps final result semantics centralized
- preserves existing Task-501, Task-502, and Task-503 boundaries

Task-504 is primarily a semantic refinement task.

It formalizes the responsibility and invariants of existing result and summary models.

---

# 2. Architecture Overview

Discovery final result pipeline:

```text
Discovery Source
    |
    v
DiscoveryOrchestrator
    |
    v
DiscoveryOrchestrationResult
    |
    v
DiscoverySessionFactory
    |
    v
DiscoverySessionSnapshot
```

Progress pipeline:

```text
DiscoveryProgress
    |
    v
DiscoveryProgressSnapshot
```

The two pipelines must remain independent.

`DiscoveryOrchestrationResult` represents final outcome.

`DiscoverySessionSnapshot` may reuse `DiscoveryOrchestrationSummary`.

`DiscoveryProgress` and `DiscoveryProgressSnapshot` must not reuse `DiscoveryOrchestrationResult` or `DiscoveryOrchestrationSummary`.

---

# 3. Current-State Analysis

Current Discovery implementation includes:

- `DiscoveryOrchestrator`
- `DiscoveryOrchestrationResult`
- `DiscoveryOrchestrationStatus`
- `DiscoveryOrchestrationReason`
- `DiscoveryOrchestrationSummary`
- `CandidateOrchestrationResult`
- `CandidateOrchestrationStatus`
- `DiscoverySession`
- `DiscoverySessionFactory`
- `DiscoverySessionSnapshot`
- `DiscoveryProgress`
- `DiscoveryProgressSnapshot`

`DiscoveryOrchestrationResult` currently contains:

- `Status`
- `CandidateResults`
- `Summary`
- `Reasons`

`DiscoveryOrchestrationSummary` currently contains:

- `TotalCandidates`
- `Registered`
- `Approved`
- `AwaitingApproval`
- `DuplicateSkipped`
- `Failed`
- `Rejected`

`DiscoverySessionSnapshot` already reuses:

- `DiscoveryOrchestrationSummary`

`DiscoveryProgress` is independent and does not depend on:

- `DiscoveryOrchestrationResult`
- `DiscoveryOrchestrationSummary`
- `DiscoverySession`

Current implementation does not contain:

- `DiscoveryResult`
- `DiscoveryResultSummary`
- `DiscoveryResultReason`
- `DiscoveryResultStatistics`

This is the desired direction.

---

# 4. Canonical Models

## DiscoveryOrchestrationResult

`DiscoveryOrchestrationResult` is the canonical Discovery final result.

It is used to describe the final outcome of one orchestration execution.

Allowed fields:

- `Status`
- `CandidateResults`
- `Summary`
- `Reasons`

It must not contain:

- `SessionId`
- Progress
- Percentage
- `CurrentOperation`
- `StartTime`
- `EndTime`
- Duration
- Repository
- UI state
- Retry state
- Timeout policy
- History records
- persistence DTOs

Responsibility:

- preserve final batch status
- preserve candidate-level final results
- preserve final summary counters
- preserve structured batch-level reasons

Non-responsibility:

- lifecycle tracking
- runtime progress tracking
- UI presentation
- persistence
- retry orchestration
- timeout execution

## DiscoveryOrchestrationSummary

`DiscoveryOrchestrationSummary` is the canonical summary for Discovery final results.

Task-504 must not introduce a second summary model.

The summary is a convenience projection over candidate-level outcomes and selected milestones.

It is not a replacement for:

- `CandidateOrchestrationResult`
- candidate-level reasons
- batch-level reasons

Current counters:

- `TotalCandidates`
- `Registered`
- `Approved`
- `AwaitingApproval`
- `DuplicateSkipped`
- `Failed`
- `Rejected`

## DiscoveryOrchestrationReason

`DiscoveryOrchestrationReason` remains the canonical structured reason model for batch-level Discovery orchestration result reasons.

Reasons should remain structured and explainable.

Required conceptual fields:

- `Code`
- `Message`

Task-504 must not introduce `DiscoveryResultReason`.

## CandidateOrchestrationResult

`CandidateOrchestrationResult` remains the candidate-level final trace.

It may contain step-specific final outputs such as:

- candidate
- driver selection result
- driver approval result
- camera factory result
- registration result
- candidate status
- candidate reasons

It is the source for candidate-level explainability.

The summary must not hide or replace candidate-level details.

---

# 5. Summary Counter Semantics

## TotalCandidates

Number of candidates included in the final orchestration result.

Rules:

- must be greater than or equal to zero
- should equal the number of `CandidateResults` when candidate results are available
- may be zero for request-level invalid, failed, or cancelled results where candidate processing never started

## Registered

Number of candidates that successfully reached device registration.

Rules:

- must be greater than or equal to zero
- represents a terminal successful outcome
- should count candidates with `CandidateOrchestrationStatus.Registered`

## Approved

Number of candidates approved by the driver approval policy.

Rules:

- must be greater than or equal to zero
- represents a workflow milestone
- does not guarantee registration
- may overlap with candidates counted in `Registered`, `DuplicateSkipped`, `RegistrationRejected`, `CameraFactoryRejected`, or `Failed`

## AwaitingApproval

Number of candidates that require future approval before camera creation can continue.

Rules:

- must be greater than or equal to zero
- represents a terminal candidate outcome for the current execution
- should count candidates with `CandidateOrchestrationStatus.AwaitingApproval`

## DuplicateSkipped

Number of candidates skipped because registration detected a duplicate and the duplicate policy allowed skip behavior.

Rules:

- must be greater than or equal to zero
- represents a terminal candidate outcome
- should count candidates with `CandidateOrchestrationStatus.DuplicateSkipped`
- should not be treated as repository failure

## Failed

Number of candidates that failed due to an unexpected or unrecoverable candidate-level issue.

Rules:

- must be greater than or equal to zero
- represents a terminal candidate outcome
- should count candidates with `CandidateOrchestrationStatus.Failed`

## Rejected

Number of candidates rejected by explicit candidate-level workflow outcomes.

Rules:

- must be greater than or equal to zero
- represents terminal rejected candidate outcomes
- may include no compatible driver, camera factory rejection, registration rejection, or generic rejection
- must not replace candidate-level reason details

---

# 6. Summary Invariants

Required invariants:

- `TotalCandidates >= 0`
- `Registered >= 0`
- `Approved >= 0`
- `AwaitingApproval >= 0`
- `DuplicateSkipped >= 0`
- `Failed >= 0`
- `Rejected >= 0`

Implementation should reject or avoid producing negative counters.

## Terminal Outcome Counters

Terminal outcome counters represent where a candidate ended in the current execution.

Current terminal outcome counters include:

- `Registered`
- `AwaitingApproval`
- `DuplicateSkipped`
- `Failed`
- `Rejected`

These counters are closer to final candidate status.

They may be used for high-level completion summaries.

## Milestone Counters

Milestone counters represent a step reached during processing.

Current milestone counters include:

- `Approved`

`Approved` is a milestone because an approved candidate may still fail camera creation, be rejected during registration, or be skipped as a duplicate.

## Counter Addition Rule

Consumers must not assume all counters can be directly added together to equal `TotalCandidates`.

Reason:

- milestone counters can overlap with terminal outcome counters
- terminal counters may intentionally be broad categories
- future counters may represent diagnostic or explainability dimensions

The only safe source of candidate-level truth is `CandidateOrchestrationResult`.

`DiscoveryOrchestrationSummary` is a convenience summary, not a normalized fact table.

---

# 7. Relationship Diagram

```text
Discovery Source
    |
    v
DiscoveryOrchestrator
    |
    v
DiscoveryOrchestrationResult
    |  Status
    |  CandidateResults
    |  Summary
    |  Reasons
    v
DiscoveryOrchestrationSummary

DiscoveryOrchestrationResult
    |
    v
DiscoverySessionFactory
    |
    v
DiscoverySessionSnapshot
    |  reuses DiscoveryOrchestrationSummary

DiscoveryProgress
    |
    v
DiscoveryProgressSnapshot
    |  independent runtime progress state
```

Relationship rules:

- `DiscoveryOrchestrationResult` owns final outcome.
- `DiscoveryOrchestrationSummary` is the single canonical summary.
- `DiscoverySessionSnapshot` may reuse `DiscoveryOrchestrationSummary`.
- `DiscoveryProgress` must not reuse `DiscoveryOrchestrationSummary`.
- `DiscoveryProgressSnapshot` must not depend on final result models.
- `DiscoveryOrchestrator` must not produce session or progress state as part of the final result.

---

# 8. Architecture Rules

## Rule 1

`DiscoveryOrchestrationResult` is the canonical final result.

No parallel `DiscoveryResult` model should be introduced without a separate approved architecture change.

## Rule 2

`DiscoveryOrchestrationSummary` is the single source of truth for Discovery summary counters.

No parallel `DiscoveryResultSummary` or `DiscoverySessionSummary` should be introduced in Task-504.

## Rule 3

Result must remain session-agnostic.

`DiscoveryOrchestrationResult` must not contain:

- `SessionId`
- session status
- start time
- end time
- duration
- history state

Session concerns belong to Task-502 models.

## Rule 4

Result must remain progress-agnostic.

`DiscoveryOrchestrationResult` must not contain:

- progress stage
- percentage
- current operation
- completed steps
- total steps

Progress concerns belong to Task-503 models.

## Rule 5

Result must remain transport-agnostic.

`DiscoveryOrchestrationResult` and `DiscoveryOrchestrationSummary` must not depend on:

- WPF
- WinForms
- SignalR
- REST
- Console
- logging framework
- repository implementation
- SQLite

---

# 9. Compatibility

## Backward Compatibility

Task-504 should be backward compatible.

Because Task-504 is primarily semantic refinement, it should not require existing callers to change when no model changes are approved.

Existing references to:

- `DiscoveryOrchestrationResult`
- `DiscoveryOrchestrationSummary`
- `DiscoveryOrchestrationReason`

should remain valid.

## Forward Compatibility

Future extensions should consume canonical result models through adapters or mappers.

Future reporting, export, metrics, or audit models should not require `DiscoveryOrchestrator` to change.

## Migration Impact

No data migration is required for Task-504.

No SQLite schema migration is required.

No repository migration is required.

If future persistence DTOs are introduced, they should be mapped from canonical result models in a separate approved task.

## Breaking Changes

Task-504 must not introduce breaking changes.

Breaking changes would include:

- replacing `DiscoveryOrchestrationResult` with `DiscoveryResult`
- replacing `DiscoveryOrchestrationSummary` with a second summary model
- moving session fields into result
- moving progress fields into result
- changing repository or SQLite contracts

---

# 10. Future Extension

Version 2 may add:

- Export
- Reporting
- Metrics
- Audit

Recommended approach:

```text
DiscoveryOrchestrationResult
    |
    v
Export / Reporting / Metrics / Audit Adapter
    |
    v
External DTO / Report / Audit Record
```

Future adapters may:

- map result data to export DTOs
- produce reporting views
- compute metrics
- create audit records

Future adapters must not:

- modify `DiscoveryOrchestrator`
- mutate `DiscoveryOrchestrationResult`
- add UI or persistence concerns into result models
- treat summary counters as always directly additive

---

# 11. Files

## Spec

This task adds:

- `Docs/SPECS/Task-504_DISCOVERY_RESULT_FOUNDATION.md`

## Future Implementation

Task-504 is primarily semantic refinement.

Future implementation may require no new production model files.

If implementation is approved later, it may modify existing result model validation or documentation comments only when needed.

Potential files to review in future implementation:

- `VSP.Device/Discovery/Orchestration/DiscoveryOrchestrationResult.cs`
- `VSP.Device/Discovery/Orchestration/DiscoveryOrchestrationSummary.cs`
- `VSP.Device/Discovery/Orchestration/DiscoveryOrchestrationReason.cs`
- `VSP.Device/Discovery/Orchestration/CandidateOrchestrationResult.cs`
- corresponding unit tests if behavior or validation changes

Task-504 should not add:

- `DiscoveryResult.cs`
- `DiscoveryResultSummary.cs`
- `DiscoveryResultReason.cs`
- `DiscoveryResultStatistics.cs`

---

# 12. Risks

## Result Becoming Session

Risk:

- final result may absorb session identity, lifecycle, timing, or history fields.

Mitigation:

- keep session state in Task-502 models.
- keep `DiscoveryOrchestrationResult` session-agnostic.

## Result Becoming Progress

Risk:

- final result may absorb current operation, stage, step, or percentage fields.

Mitigation:

- keep progress state in Task-503 models.
- keep `DiscoveryOrchestrationResult` progress-agnostic.

## Summary Duplication

Risk:

- adding `DiscoveryResultSummary` or `DiscoverySessionSummary` creates duplicate counter models.

Mitigation:

- reuse `DiscoveryOrchestrationSummary`.
- introduce future persistence DTOs only through approved mapper tasks.

## Statistics Inconsistency

Risk:

- consumers may assume all counters add up to `TotalCandidates`.

Mitigation:

- distinguish terminal outcome counters from milestone counters.
- document that candidate results are the source of detailed truth.

## Result Becoming God Object

Risk:

- result may absorb export, reporting, metrics, audit, UI, repository, retry, and timeout concerns.

Mitigation:

- use future adapters or mappers.
- keep result focused on final orchestration outcome.

---

# 13. Out Of Scope

- Production code
- Tests
- Task-504 implementation
- UI
- Repository
- SQLite
- History
- Progress
- Progress publisher
- Session repository
- Retry
- Timeout
- Export implementation
- Reporting implementation
- Metrics implementation
- Audit implementation
- Discovery workflow changes
- Driver selection changes
- Camera factory changes
- Device registration changes
- Version tag
- GitHub Release
- Commit

---

# 14. Non-Goals

Task-504 will not evolve into:

- a session model
- a progress model
- a reporting engine
- a metrics engine
- an audit store
- a repository workflow
- a UI result screen
- a retry engine
- a timeout policy

---

# 15. Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-504 Discovery Result / Summary Foundation`

Spec filename:

- `Docs/SPECS/Task-504_DISCOVERY_RESULT_FOUNDATION.md`
