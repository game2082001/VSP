# Task-406 Driver Selection Foundation

Version: 1.0
Status: Approved
Feature: Discovery
Milestone: Version 1.3

---

# Purpose

Define the Driver Selection Foundation for producing explainable driver compatibility results from discovery evidence and driver compatibility metadata.

Task-406 defines how future selection code should evaluate:

- `AutoDiscoveryCandidate`
- mapped driver evidence
- `DriverCompatibilityCapability`
- `DriverDescriptor`

The purpose is to report explainable compatibility outcomes.

This task does not implement production code.

This task does not select a single winning driver.

This task does not create cameras.

---

# Current-State Analysis

Version 1.3 Discovery currently has:

- Task-404 Auto Discovery Coordinator
- Task-405 Driver Capability Foundation

Task-404 produces coordinator-level candidates through `AutoDiscoveryCandidate`.

Task-404 must not know about drivers and must not select drivers.

Task-405 defines Driver Compatibility Capability metadata:

- `DriverCompatibilityCapability`
- `DriverCompatibilityEvidenceKind`
- `DriverCompatibilityEvidenceRequirement`

`DriverDescriptor` can carry optional `CompatibilityCapability` metadata.

Current `DriverRegistry` is responsible for driver registration, lookup, and factory access.

`DriverRegistry` should not become the selection engine.

Task-406 should define a separate selection foundation that consumes evidence and driver metadata.

---

# Architecture Overview

Driver Selection sits after Discovery and after Evidence Mapping.

The required flow is:

`AutoDiscoveryCandidate`

-> `AutoDiscoveryCandidateEvidenceMapper`

-> `Driver Evidence Collection`

-> `DriverSelectionService`

-> `DriverCompatibilityResult`

Important boundary:

`DriverSelectionService` must consume only driver evidence and driver descriptors.

It must not consume `AutoDiscoveryCandidate` directly.

This keeps candidate interpretation isolated in the mapping layer.

---

# Responsibility Boundary

Driver Selection is responsible for:

- evaluating driver evidence against `DriverCompatibilityCapability`
- evaluating required evidence
- evaluating optional evidence
- evaluating unsupported evidence conflicts
- reporting required settings
- returning explainable compatibility results for all candidate drivers
- preserving match and rejection reasons

Driver Selection is not responsible for:

- executing Discovery
- mapping `AutoDiscoveryCandidate` directly
- probing RTSP
- running ONVIF discovery
- running Network Scan
- modifying Auto Discovery workflow
- registering drivers
- invoking driver factories
- selecting a single winning driver
- ranking drivers
- computing confidence scores
- creating cameras
- importing cameras
- writing persistence
- rendering UI

---

# Candidate To Evidence Mapping

Candidate to Evidence Mapping must be a separate component.

It must not be implemented inside `DriverSelectionService`.

Recommended component:

- `AutoDiscoveryCandidateEvidenceMapper`

Responsibilities:

- consume `AutoDiscoveryCandidate`
- convert candidate summary fields into driver evidence
- preserve distinction between source attribution and evidence kind
- return a driver evidence collection

Mapping examples:

- non-empty `Host` -> `Host` evidence
- `Port` value -> `Port` evidence
- non-empty `Endpoint` -> `Endpoint` evidence
- non-empty `Manufacturer` -> `ManufacturerHint` evidence
- non-empty `Model` -> `ModelHint` evidence
- ONVIF-derived candidate summary -> `OnvifDiscovery` evidence
- RTSP probe-derived candidate summary -> `RtspEndpointProbe` evidence

Important rule:

`AutoDiscoverySource` is not protocol evidence.

Examples:

- `NetworkScan` source does not imply RTSP.
- `NetworkScan` source does not imply ONVIF.
- `Reachability` does not imply service existence.

The mapper may use candidate summary fields to produce evidence, but it must not execute protocol logic or infer service identity from reachability alone.

---

# Driver Evidence Collection

Driver Evidence Collection should represent the mapped evidence consumed by Driver Selection.

Recommended conceptual fields:

- evidence kind
- optional qualifier
- optional display value or diagnostic value if approved later

The collection should remain independent from `AutoDiscoveryCandidate`.

It should not contain:

- `AutoDiscoverySource` as compatibility input
- raw protocol payload
- driver descriptor
- camera entity
- repository state

---

# Evidence Evaluation

Driver Selection evaluates driver evidence against capability requirements.

## Required Evidence

Each required evidence requirement must produce an evaluation.

If all required evidence is satisfied and there are no unsupported conflicts, the driver may be reported as compatible.

If any required evidence is missing, the driver must be reported as rejected or incompatible with explainable reasons.

## Optional Evidence

Optional evidence should be evaluated and reported.

Optional evidence must not determine compatibility by itself.

Optional evidence must not produce confidence score, ranking, or preferred driver behavior in this task.

## Unsupported Evidence

Unsupported evidence should be evaluated as conflicts.

If evidence conflicts with unsupported evidence requirements, the result must record the conflict and reject or mark the driver incompatible.

Unsupported evidence conflicts must be explainable.

---

# DriverEvidenceEvaluation

`DriverEvidenceEvaluation` must not use `bool IsSatisfied`.

It should use a status enum to preserve future extensibility.

Recommended enum:

- `Satisfied`
- `Missing`
- `Conflict`
- `NotApplicable`

Recommended fields:

- evidence kind
- optional qualifier
- status
- reason code
- reason message

This design allows future states without breaking result shape.

---

# Match Reasons

Match and rejection reasons must not be plain `List<string>`.

Recommended model:

- `DriverSelectionReason`

Recommended fields:

- `Code`
- `Message`

Optional future fields may include:

- severity
- related evidence kind
- related setting key
- diagnostic metadata

Reason codes should be stable enough for future UI and testing.

Reason messages should remain human-readable.

---

# DriverSelectionService

`DriverSelectionService` consumes:

- driver evidence collection
- driver descriptors

It returns:

- `DriverCompatibilityResult` collection

It must not return:

- selected driver
- camera
- imported camera
- factory-created driver instance

It must not:

- invoke `DriverDescriptor.Factory`
- register drivers
- call discovery services
- modify discovery workflow
- write persistence
- perform UI operations

---

# Selection Result Model

Task-406 should define result models that are explainable and deterministic.

Recommended models:

## DriverSelectionRequest

Conceptual fields:

- driver evidence collection
- candidate driver descriptors

The request should not contain `AutoDiscoveryCandidate` directly.

## DriverSelectionResult

Conceptual fields:

- compatibility results
- optional request-level messages

The result should not contain a selected driver.

## DriverCompatibilityResult

Conceptual fields:

- driver id
- display name
- connection type
- compatibility status
- required evidence evaluations
- optional evidence evaluations
- unsupported evidence evaluations
- required settings
- match reasons
- rejection reasons

Recommended compatibility statuses:

- `Compatible`
- `Rejected`
- `InsufficientEvidence`
- `NotApplicable`

This status is not a ranking.

This status is not a confidence score.

---

# Multiple Compatible Drivers

Multiple compatible drivers must all be returned.

Task-406 must not define:

- ranking
- confidence
- preferred driver
- tie breaking
- brand priority

If multiple drivers are compatible, the result should expose all of them with their evidence evaluations and reasons.

A future task may define deterministic selection policy if approved separately.

---

# No Match Handling

No match is a valid result, not an exception.

When no drivers are compatible:

- compatible results should be empty or absent according to the final model
- rejected or insufficient-evidence results should include reasons
- missing required evidence should be listed
- unsupported conflicts should be listed

The caller can decide how to present no-match outcomes later.

Task-406 does not define UI behavior.

---

# Vendor-Specific Matching

Vendor-specific matching must be extensible.

The core selection foundation must not hard-code vendor brands into:

- core enum values
- selection switch statements
- ranking rules
- preferred driver policy

Vendor-specific hints may be represented through evidence kind and qualifier metadata.

Future vendor plugins may declare metadata instances without changing the core selection architecture.

---

# Relationship With Task-404 Auto Discovery Coordinator

Task-404 owns discovery workflow coordination and candidate creation.

Task-406 must not modify `AutoDiscoveryCoordinator`.

Task-406 must not make `AutoDiscoveryCoordinator` select drivers.

Task-406 may define a mapper that consumes `AutoDiscoveryCandidate` after discovery has completed.

This keeps discovery independent from driver selection.

---

# Relationship With Task-405 Driver Capability Foundation

Task-405 owns descriptive compatibility metadata.

Task-406 consumes that metadata.

Task-406 must not redefine runtime capability.

Task-406 must not add confidence, ranking, preferred driver, or tie-breaking policy to Task-405 capability metadata.

---

# Relationship With Driver Registry

`DriverRegistry` owns registration and descriptor lookup.

Task-406 should not move selection logic into `DriverRegistry`.

Future callers may pass descriptors from `DriverRegistry.GetAll()` into Driver Selection.

Driver Selection must not register drivers.

Driver Selection must not invoke driver factories.

---

# Files To Add

This task should add:

- `Docs/SPECS/Task-406_DRIVER_SELECTION_FOUNDATION.md`

This task does not add production code or tests.

---

# Future Implementation Direction

If implementation is approved later, possible files may include:

- `VSP.Device/Drivers/Selection/DriverSelectionRequest.cs`
- `VSP.Device/Drivers/Selection/DriverSelectionResult.cs`
- `VSP.Device/Drivers/Selection/DriverCompatibilityResult.cs`
- `VSP.Device/Drivers/Selection/DriverCompatibilityStatus.cs`
- `VSP.Device/Drivers/Selection/DriverEvidence.cs`
- `VSP.Device/Drivers/Selection/DriverEvidenceCollection.cs`
- `VSP.Device/Drivers/Selection/DriverEvidenceEvaluation.cs`
- `VSP.Device/Drivers/Selection/DriverEvidenceEvaluationStatus.cs`
- `VSP.Device/Drivers/Selection/DriverSelectionReason.cs`
- `VSP.Device/Drivers/Selection/DriverSelectionService.cs`
- `VSP.Device/Drivers/Selection/AutoDiscoveryCandidateEvidenceMapper.cs`

Any implementation requires a separate approved Task Plan.

---

# Unit Test Direction For Future Implementation

Future tests should cover:

- mapper converts candidate host into host evidence
- mapper converts candidate port into port evidence
- mapper converts endpoint into endpoint evidence
- mapper does not treat `NetworkScan` source as RTSP or ONVIF evidence
- selection satisfies required evidence
- selection reports missing required evidence
- selection reports optional evidence as present or missing
- selection reports unsupported evidence conflicts
- multiple compatible drivers are all returned
- no match returns explainable rejection results
- service does not invoke driver factory
- service does not create camera
- service does not execute discovery

---

# Risks

- Treating `AutoDiscoverySource` as protocol evidence could produce false matches.
- Putting mapping inside `DriverSelectionService` would couple selection to discovery models.
- Adding ranking or confidence too early could hide business policy in foundation code.
- Returning only a driver id would make results impossible to explain.
- Vendor-specific matching could become hard-coded if qualifiers or metadata extensibility are ignored.
- Required settings could be confused with credential validation or camera import.

---

# Out Of Scope

- Production Code
- Tests
- Discovery workflow
- AutoDiscoveryCoordinator changes
- ONVIF Logic
- RTSP Logic
- NetworkScan Logic
- Driver Factory Invocation
- Driver Registration
- Driver factory instance creation
- Camera Creation
- Camera Import
- Repository
- SQLite
- UI
- Confidence Score
- Ranking Formula
- Preferred Driver
- Tie Breaking
- Brand priority
- Task-407

---

# Non-Goals

Task-406 will not evolve into:

- a discovery engine
- a camera creation workflow
- a camera import workflow
- a driver factory
- a driver registry
- a ranking engine
- a confidence scoring engine
- a UI selection workflow
- a persistence workflow

---

# Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-406 Driver Selection Foundation`

Spec filename:

- `Docs/SPECS/Task-406_DRIVER_SELECTION_FOUNDATION.md`
