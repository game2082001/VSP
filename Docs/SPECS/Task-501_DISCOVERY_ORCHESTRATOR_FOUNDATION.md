# Task-501 Discovery Orchestrator Foundation

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.5

---

# 1. Purpose

Task-501 establishes the Discovery-to-Registration Orchestration Foundation.

The purpose is to connect existing discovery, driver compatibility, camera factory, and registration foundations into a single explainable workflow.

Task-501 orchestrates already-approved foundation components.

It does:

- call existing Discovery foundation components
- map `AutoDiscoveryCandidate` into driver evidence
- call existing Driver Selection foundation
- call a driver approval policy
- call existing Camera Factory foundation
- call existing Device Registration foundation
- aggregate candidate-level results into a batch result

It does not:

- reimplement Discovery
- reimplement Driver Selection
- reimplement Camera Factory
- reimplement Registration
- decide driver ranking
- compute confidence
- directly write repository or SQLite
- perform UI workflow

---

# 2. Current-State Analysis

The repository currently contains the following foundation components.

## Discovery Foundation

`AutoDiscoveryCoordinator` exists and produces `AutoDiscoveryResult`.

`AutoDiscoveryResult` contains `AutoDiscoveryCandidate` items.

`AutoDiscoveryCandidate` includes coordinator-owned summary fields:

- `CandidateKey`
- `Sources`
- `Host`
- `Port`
- `Endpoint`
- `Name`
- `Location`
- `Model`
- `Manufacturer`
- `Reachability`
- `ProbeClassification`
- `Warnings`

`AutoDiscoveryCoordinator` currently coordinates:

- ONVIF Discovery
- Network Scan
- RTSP Endpoint Probe
- candidate merge

It already uses async APIs and accepts `CancellationToken`.

## Driver Evidence Mapping

`AutoDiscoveryCandidateEvidenceMapper` exists.

It maps `AutoDiscoveryCandidate` into `DriverEvidenceCollection`.

Important rule:

- `AutoDiscoverySource` is source attribution.
- `AutoDiscoverySource.NetworkScan` does not imply RTSP, ONVIF, HTTP, or any service identity.
- Protocol evidence is produced only when candidate summary fields support it.

## Driver Selection

`DriverSelectionService` exists.

It consumes:

- `DriverEvidenceCollection`
- candidate `DriverDescriptor` list

It returns:

- `DriverSelectionResult`
- one `DriverCompatibilityResult` per candidate driver descriptor
- structured match and rejection reasons

It does not return a selected driver.

It does not rank drivers.

It does not compute confidence.

It does not invoke driver factories.

## Camera Factory

`CameraFactory` exists.

It consumes:

- `CameraFactoryRequest`
- `ApprovedDriverReference`
- `CameraInitializationData`

It returns:

- `CameraFactoryResult`

It creates an in-memory `Camera` entity only.

It does not run discovery, driver selection, repository persistence, connection testing, or driver factory invocation.

## Device Registration

`DeviceRegistrationService` exists.

It consumes:

- `DeviceRegistrationRequest`
- an already-created `Camera`
- `RegistrationSource`
- `DuplicatePolicy`

It returns:

- `DeviceRegistrationResult`

It depends only on `ICameraRepository`.

It handles duplicate detection and repository add when allowed.

It does not create cameras, select drivers, run discovery, or call SQLite directly.

## Missing Orchestration Layer

There is not yet a service that connects:

`AutoDiscoveryCoordinator`

-> `AutoDiscoveryCandidateEvidenceMapper`

-> `DriverSelectionService`

-> `IDriverApprovalPolicy`

-> `CameraFactory`

-> `DeviceRegistrationService`

Task-501 defines that orchestration boundary.

---

# 3. Architecture Overview

The Discovery Orchestrator is a workflow coordinator.

Recommended pipeline:

`Discovery Source`

-> `AutoDiscoveryCandidate`

-> `Evidence Mapper`

-> `DriverSelectionService`

-> `DriverApprovalRequest`

-> `IDriverApprovalPolicy`

-> `DriverApprovalResult`

-> `ApprovedDriverReference`

-> `CameraFactory`

-> `DeviceRegistrationService`

-> `DiscoveryOrchestrationResult`

The orchestrator must preserve explainability at every step.

Each candidate should be evaluated independently after discovery has produced candidates.

Candidate-level failures should be captured in candidate results instead of hiding the reason in a batch-level message.

---

# 4. Responsibility Boundary

The Discovery Orchestrator may:

- call Discovery
- call Mapper
- call Driver Selection
- call Driver Approval Policy
- receive `ApprovedDriverReference`
- call Camera Factory
- call Registration
- aggregate candidate results
- aggregate batch summary
- preserve structured reasons
- pass `CancellationToken`

The Discovery Orchestrator must not:

- perform ranking
- compute confidence
- make driver selection decisions by itself
- directly choose a driver from multiple compatible drivers
- invoke driver factories
- create drivers
- directly depend on repository implementations
- directly depend on SQLite
- write SQLite
- implement UI flow
- parse import files
- perform connection testing
- retry protocol operations
- parse protocol payloads
- infer protocol identity from reachability alone

---

# 5. Driver Approval

Driver approval is a strategy boundary.

The orchestrator must not hard-code driver approval behavior.

The orchestrator calls `IDriverApprovalPolicy` with a `DriverApprovalRequest` and receives a `DriverApprovalResult`.

The policy decides whether a compatible driver can be approved for camera creation.

The policy is responsible for converting compatible driver information into an `ApprovedDriverReference` when approval is safe.

## DriverApprovalRequest

Recommended conceptual fields:

- `AutoDiscoveryCandidate`
- `DriverSelectionResult`
- compatible `DriverCompatibilityResult` collection
- optional correlation id
- optional policy context

Rules:

- Request must contain selection output.
- Request must not contain UI commands.
- Request must not contain repository commands.
- Request must not contain camera factory commands.
- Request must not ask the orchestrator to rank drivers.

## IDriverApprovalPolicy

Recommended responsibility:

- evaluate driver compatibility results
- decide whether a driver may be approved
- return an explainable `DriverApprovalResult`
- produce `ApprovedDriverReference` only when approved

It must not:

- execute discovery
- execute driver selection
- invoke driver factory
- create cameras
- write repository
- access SQLite
- perform UI interaction directly

## DriverApprovalResult

Recommended conceptual fields:

- `DriverApprovalStatus`
- `ApprovedDriverReference`
- compatible driver results considered
- reasons

Reasons must be structured data.

Recommended reason model:

- `Code`
- `Message`

## DriverApprovalStatus

Recommended statuses:

- `Approved`
- `AwaitingApproval`
- `NoCompatibleDriver`
- `Rejected`
- `Failed`

Status semantics:

- `Approved` means a policy returned a valid `ApprovedDriverReference`.
- `AwaitingApproval` means policy cannot approve without a future external or manual decision.
- `NoCompatibleDriver` means no compatible driver exists for the candidate.
- `Rejected` means policy intentionally rejected the candidate or ambiguous match.
- `Failed` means policy failed unexpectedly.

## First Version Policy

Task-501 first implementation should support:

- `AutoApproveSingleCompatiblePolicy`

Policy behavior:

- compatible driver count equals 1 -> approve and return `ApprovedDriverReference`
- compatible driver count greater than 1 -> return `AwaitingApproval`
- compatible driver count equals 0 -> return `NoCompatibleDriver`

`AutoApproveSingleCompatiblePolicy` must not:

- rank drivers
- compare confidence
- prefer brands
- break ties
- invoke driver factories

## Future Policies

Spec reserves future policies:

- `ManualApprovalPolicy`
- `RejectAmbiguousPolicy`
- `HighestConfidencePolicy`

`ManualApprovalPolicy` may return `AwaitingApproval` when one or more compatible drivers exist.

`RejectAmbiguousPolicy` may approve a single compatible driver and reject multiple compatible drivers.

`HighestConfidencePolicy` is future-only and must not be implemented until confidence/ranking has a separate approved foundation.

Adding future policies should not require changing the orchestrator workflow.

---

# 6. Candidate Result

Task-501 should define `CandidateOrchestrationResult`.

Recommended fields:

- `AutoDiscoveryCandidate Candidate`
- `DriverSelectionResult DriverSelectionResult`
- `DriverApprovalResult DriverApprovalResult`
- `CameraFactoryResult CameraFactoryResult`
- `DeviceRegistrationResult RegistrationResult`
- candidate status
- reasons

Rules:

- Candidate result must preserve all relevant step results.
- Missing downstream results are allowed when an earlier step prevents continuation.
- Reasons must explain why processing stopped or succeeded.
- A candidate with multiple compatible drivers should not silently choose one.
- A candidate with no compatible driver should not be treated as an exception.

Recommended candidate statuses:

- `Registered`
- `AwaitingApproval`
- `NoCompatibleDriver`
- `CameraFactoryRejected`
- `RegistrationRejected`
- `DuplicateSkipped`
- `Failed`
- `Cancelled`

These statuses are candidate-level statuses, not batch-level statuses.

---

# 7. Batch Result

Task-501 should define `DiscoveryOrchestrationResult`.

Recommended fields:

- overall status
- candidate results
- summary
- reasons

## Overall Status

Required statuses:

- `Completed`
- `CompletedWithErrors`
- `Failed`
- `Cancelled`

Recommended semantics:

- `Completed` means orchestration completed and all candidates reached a non-error terminal state.
- `CompletedWithErrors` means orchestration completed but at least one candidate had a rejection, failure, no match, or awaiting approval.
- `Failed` means a request-level or infrastructure-level failure prevented meaningful orchestration.
- `Cancelled` means caller cancellation stopped orchestration.

## Summary

Recommended summary fields:

- total candidate count
- registered count
- awaiting approval count
- no compatible driver count
- camera factory rejected count
- registration rejected count
- duplicate skipped count
- failed count

Summary is not a replacement for candidate-level reasons.

It is a convenience for callers.

---

# 8. Error Semantics

Task-501 should distinguish candidate-level outcomes from batch-level failures.

## Candidate-Level Outcomes

The following should affect only the current candidate:

- `NoCompatibleDriver`
- `AwaitingApproval`
- `RegistrationRejected`
- `DuplicateSkipped`
- `RepositoryFailure`
- `CameraFactoryRejected`

Candidate-level outcomes should be represented in `CandidateOrchestrationResult`.

They should not stop processing other candidates unless the caller cancels the operation.

## Batch-Level Outcomes

The following may stop the entire batch:

- invalid orchestration request that prevents any work
- failure to execute the discovery source before candidates exist
- missing required orchestrator dependency
- caller cancellation
- unrecoverable unexpected orchestrator failure

Repository failure for one candidate should normally remain candidate-level when other candidates can continue.

## Specific Semantics

`NoCompatibleDriver`:

- occurs when driver selection returns no compatible drivers
- should not call approval policy for approval beyond no-match reporting unless the policy owns no-match formatting
- should not call Camera Factory
- should not call Registration

`AwaitingApproval`:

- occurs when policy cannot safely approve a driver
- expected for multiple compatible drivers under `AutoApproveSingleCompatiblePolicy`
- should not call Camera Factory
- should not call Registration

`RegistrationRejected`:

- occurs when Device Registration rejects the camera
- includes duplicate reject or validation rejection
- affects only the candidate

`DuplicateSkipped`:

- occurs when Device Registration skips a duplicate under `DuplicatePolicy.Skip`
- affects only the candidate
- should be explainable and not treated as repository failure

`RepositoryFailure`:

- occurs when Device Registration returns failed result due to repository failure
- affects only the candidate when processing can continue
- may contribute to batch `CompletedWithErrors`

`CameraFactoryRejected`:

- occurs when Camera Factory rejects initialization data
- should not call Registration
- affects only the candidate

---

# 9. Async

Task-501 orchestration should be async.

Recommended public API shape:

- async method returning `Task<DiscoveryOrchestrationResult>`
- accepts `CancellationToken`

The orchestrator should pass cancellation to async discovery operations.

The orchestrator should check cancellation between candidate processing steps.

Cancellation should produce a `Cancelled` batch result when it can be represented safely.

If caller cancellation is thrown by a lower-level component and not recoverable as a result, the implementation plan must define whether it is propagated or translated.

## Timeout

Timeout is out of scope for Task-501.

Current lower-level discovery components already own operation-specific timeouts.

Orchestration-level timeout policy is reserved for a future task.

Task-501 should not introduce global timeout behavior.

---

# 10. Future Extension

Task-501 should leave room for:

- Session
- Progress
- Retry
- Timeout
- UI Approval
- History

Future extension rules:

- Session should not be required for first orchestration foundation.
- Progress should not be mixed into first result model unless explicitly approved.
- Retry should not be hidden inside orchestration.
- Timeout should be defined separately from existing protocol-level timeouts.
- UI approval should be implemented through a policy or caller boundary, not direct UI dependency.
- History should not imply persistence inside the orchestrator.

---

# 11. Relationship With Tasks 405-408

## Relationship With Task-405

Task-405 defines driver compatibility capability metadata.

Task-501 does not define new driver capability metadata.

Task-501 consumes selection results that were produced from Task-405 metadata.

## Relationship With Task-406

Task-406 defines candidate evidence mapping and driver selection.

Task-501 must keep mapping and selection separate:

`AutoDiscoveryCandidate`

-> `AutoDiscoveryCandidateEvidenceMapper`

-> `DriverEvidenceCollection`

-> `DriverSelectionService`

Task-501 must not move mapping into selection.

Task-501 must not make `DriverSelectionService` consume `AutoDiscoveryCandidate` directly.

Task-501 must not turn selection into ranking or confidence.

## Relationship With Task-407

Task-407 defines Camera Factory.

Task-501 may call Camera Factory only after `IDriverApprovalPolicy` returns an approved `ApprovedDriverReference`.

Task-501 must not call Camera Factory when approval is missing, ambiguous, rejected, or no compatible driver exists.

Task-501 must not create `Camera` directly.

## Relationship With Task-408

Task-408 defines Device Registration.

Task-501 may call Device Registration only after Camera Factory returns a created `Camera`.

Task-501 must not write repository directly.

Task-501 must not call SQLite directly.

Task-501 must not modify Device Registration duplicate policy behavior.

---

# 12. Files To Add

This task should add:

- `Docs/SPECS/Task-501_DISCOVERY_ORCHESTRATOR_FOUNDATION.md`

This task does not add production code or tests.

---

# 13. Future Implementation Files

Future implementation may include:

- `VSP.Device/Discovery/Orchestration/DiscoveryOrchestrator.cs`
- `VSP.Device/Discovery/Orchestration/DiscoveryOrchestrationRequest.cs`
- `VSP.Device/Discovery/Orchestration/DiscoveryOrchestrationResult.cs`
- `VSP.Device/Discovery/Orchestration/DiscoveryOrchestrationStatus.cs`
- `VSP.Device/Discovery/Orchestration/DiscoveryOrchestrationReason.cs`
- `VSP.Device/Discovery/Orchestration/DriverApprovalRequest.cs`
- `VSP.Device/Discovery/Orchestration/DriverApprovalResult.cs`
- `VSP.Device/Discovery/Orchestration/DriverApprovalStatus.cs`
- `VSP.Device/Discovery/Orchestration/IDriverApprovalPolicy.cs`
- `VSP.Device/Discovery/Orchestration/AutoApproveSingleCompatiblePolicy.cs`
- `VSP.Device/Discovery/Orchestration/CandidateOrchestrationResult.cs`
- corresponding unit tests

Any implementation requires a separate approved Task Plan.

---

# 14. Unit Test Direction For Future Implementation

Future implementation tests should cover:

- orchestrator calls discovery source and processes candidates
- candidate maps to driver evidence before selection
- selection service receives evidence, not raw candidate
- approval policy receives selection result
- single compatible driver is auto-approved by `AutoApproveSingleCompatiblePolicy`
- multiple compatible drivers return awaiting approval
- no compatible driver returns no-compatible-driver result
- approved driver reference is passed to Camera Factory
- Camera Factory is not called when approval is missing
- Registration is called only when Camera Factory creates a camera
- registration reject remains candidate-level
- duplicate skip remains candidate-level
- repository failure from registration is explainable
- one candidate failure does not stop the full batch
- cancellation is honored
- orchestrator does not invoke driver factory
- orchestrator does not directly depend on SQLite
- orchestrator does not perform ranking or confidence

---

# 15. Risks

- Orchestrator may accidentally become a God Service.
- Orchestrator may start choosing drivers instead of delegating approval to policy.
- `AutoApproveSingleCompatiblePolicy` may be misunderstood as ranking.
- Multiple compatible drivers may be silently resolved without approval.
- Camera initialization mapping may infer brand from weak evidence.
- Candidate-level failures may be collapsed into vague batch-level failure.
- Registration duplicate handling may become inconsistent with Task-408.
- Direct dependency on SQLite would violate repository boundaries.
- UI approval may be coupled directly into orchestration too early.
- Timeout, retry, progress, and session concerns may make Task-501 too large.

---

# 16. Out Of Scope

- Production code
- Tests
- Task-501 implementation
- Session
- Progress
- Retry
- Timeout
- UI
- UI approval screen
- Manual approval workflow
- Repository rewrite
- SQLite
- SQLite schema
- Driver Ranking
- Confidence
- Tie Breaking
- Preferred Driver
- HighestConfidencePolicy implementation
- Driver Factory invocation
- Connection Testing
- Import parser changes
- Import framework rewrite
- Version Tag
- GitHub Release

---

# 17. Non-Goals

Task-501 will not evolve into:

- a discovery protocol implementation
- a driver ranking engine
- a confidence scoring engine
- a camera factory
- a device registration service
- a repository service
- a SQLite persistence workflow
- a UI wizard
- an import parser
- a connection tester
- a retry engine

---

# 18. Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-501 Discovery Orchestrator Foundation`

Spec filename:

- `Docs/SPECS/Task-501_DISCOVERY_ORCHESTRATOR_FOUNDATION.md`
