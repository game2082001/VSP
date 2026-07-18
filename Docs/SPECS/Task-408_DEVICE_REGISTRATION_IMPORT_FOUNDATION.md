# Task-408 Device Registration / Import Foundation

Version: 1.0
Status: Approved
Feature: Discovery
Milestone: Version 1.3

---

# Purpose

Define the Device Registration / Import Foundation for safely registering camera entities into the system.

Task-408 covers the boundary between already-created `Camera` entities and repository persistence.

It does not perform discovery, driver selection, camera factory work, connection testing, or UI behavior.

This task does not implement production code.

---

# Current-State Analysis

The repository already contains:

- `Camera` entity
- `ICameraRepository`
- `CameraRepository`
- `SQLiteCameraRepository`
- existing import preview and execution flow
- existing duplicate checking for import preview rows

Existing import duplicate checking is tied to import rows and preview validation.

Task-408 should define a registration foundation for already-created camera entities, not reuse import preview concepts directly as the core registration model.

Device Registration should sit after Camera Factory and before repository persistence.

---

# Assumptions And Dependencies

## Task-407 Dependency

Task-407 Camera Factory Spec is a prerequisite dependency for Task-408.

Task-408 assumes Camera Factory has already produced a valid or partially valid `Camera` entity to be registered.

Task-408 must not create `Camera` entities itself.

Current repository note:

- `Docs/SPECS/Task-407_CAMERA_FACTORY_FOUNDATION.md` is expected as a dependency.
- If the Task-407 formal spec is missing at implementation time, Task-408 implementation must stop and request dependency clarification.

## Repository Spec Assumption

The repository layer exists in code through `ICameraRepository`, `CameraRepository`, and `SQLiteCameraRepository`.

If there is no formal repository spec, Task-408 assumes:

- registration depends only on `ICameraRepository`
- repository implementation details are outside Task-408
- SQLite behavior is hidden behind repository abstraction

Task-408 must not directly depend on `SQLiteCameraRepository`.

---

# Architecture Overview

Device Registration sits after Camera Factory:

`Camera Factory`

-> `Camera Entity`

-> `Device Registration`

-> `ICameraRepository`

-> Repository implementation

Task-408 is a persistence boundary foundation.

It validates whether an already-created camera may be registered and returns an explainable result.

---

# Responsibility Boundary

Device Registration is responsible for:

- accepting an already-created `Camera`
- validating registration prerequisites
- checking duplicates against existing registered cameras
- applying duplicate policy
- calling `ICameraRepository` to persist when allowed
- returning explainable registration results

Device Registration is not responsible for:

- Discovery
- Auto Discovery workflow
- Driver Selection
- Camera Factory
- Camera creation
- Driver Factory invocation
- Driver Registry modification
- Connection Test
- UI behavior
- SQLite schema creation or migration
- repository implementation details

---

# RegistrationSource

Task-408 should reserve a `RegistrationSource` concept.

Examples:

- `Discovery`
- `Import`
- `Manual`
- `API`

Registration source is provenance for registration.

It must not change discovery behavior.

It must not trigger driver selection.

It must not trigger camera creation.

Task-408 Spec reserves this model concept, but this task does not require production implementation.

---

# DuplicatePolicy

Task-408 should reserve a `DuplicatePolicy` concept.

Allowed implementation policies for Task-408:

- `Reject`
- `Skip`

Meaning:

- `Reject` returns a failed or rejected result when duplicates are found.
- `Skip` returns a skipped duplicate result without writing to repository.

Future extension policies:

- `Replace`
- `Merge`

Future policies must not be implemented in Task-408.

Replace and merge have data-loss and conflict-resolution implications and require separate approval.

---

# Input Contract

Recommended model:

- `DeviceRegistrationRequest`

Recommended fields:

- `Camera`
- `RegistrationSource`
- `DuplicatePolicy`
- optional correlation id or context id

Rules:

- `Camera` must already exist before registration.
- request must not contain discovery workflow commands.
- request must not contain driver selection commands.
- request must not contain camera factory commands.

---

# Output Contract

Recommended model:

- `DeviceRegistrationResult`

Recommended fields:

- registration status
- registered camera id when successful
- duplicate check results
- reasons
- repository error summary when failure occurs

Result must be explainable.

Reasons must be structured data, not plain strings.

Recommended reason model:

- `Code`
- `Message`

Optional future fields:

- severity
- field name
- duplicate camera id
- diagnostic metadata

---

# Registration Status

Recommended statuses:

- `Registered`
- `Rejected`
- `SkippedDuplicate`
- `Failed`

Status semantics:

- `Registered` means repository write was attempted and succeeded.
- `Rejected` means validation or duplicate policy prevented registration.
- `SkippedDuplicate` means duplicate was found and policy was `Skip`.
- `Failed` means registration could not complete due to repository or unexpected infrastructure failure.

---

# Repository Interaction

Device Registration may write repository.

Rules:

- depend only on `ICameraRepository`
- never depend directly on `SQLiteCameraRepository`
- read existing cameras through `ICameraRepository`
- write new camera through `ICameraRepository`
- repository exceptions should become explainable failure results where possible

Task-408 may cause SQLite data to be created indirectly through repository implementation.

Task-408 must not:

- create SQLite schema
- migrate SQLite schema
- call SQLite APIs directly
- bypass repository abstraction

---

# Duplicate Detection Strategy

Duplicate detection should be deterministic and conservative.

Recommended duplicate fields:

- camera name
- IP address
- RTSP URL when non-empty

Rules:

- duplicate comparison should be case-insensitive for text fields where appropriate
- empty RTSP URL should not be treated as duplicate against another empty RTSP URL
- duplicate detection should return structured duplicate results
- duplicate detection should not silently overwrite data

Recommended duplicate reason codes:

- `DuplicateName`
- `DuplicateIpAddress`
- `DuplicateRtspUrl`

Task-408 should not implement replace or merge behavior.

---

# Registration Flow

Conceptual flow:

1. Receive `DeviceRegistrationRequest`.
2. Validate request and camera presence.
3. Validate minimum camera registration fields.
4. Load existing cameras from `ICameraRepository`.
5. Run duplicate detection.
6. Apply duplicate policy.
7. If allowed, call repository add.
8. Return explainable result.

The flow must not call:

- discovery service
- driver selection service
- camera factory
- driver factory
- connection test
- UI service

---

# Relationship With Existing Import Framework

Existing import framework already handles:

- file parsing
- row validation
- preview generation
- import execution
- duplicate checking within preview rows

Task-408 defines registration of `Camera` entities.

Future import flows may use Device Registration after import mapping creates a camera.

Task-408 should not replace the existing import parser, preview, or mapper.

Task-408 should not directly depend on import preview row models.

---

# Relationship With Task-407 Camera Factory

Task-407 owns creation of `Camera` entities and initialization data.

Task-408 consumes `Camera` entities.

Task-408 must not:

- create Camera
- infer Camera fields from discovery
- select driver
- invoke driver factory

If Camera Factory output is incomplete, registration may reject the request with explainable reasons.

---

# Relationship With Task-406 Driver Selection

Task-406 owns driver compatibility evaluation.

Task-408 does not evaluate drivers.

Task-408 does not inspect evidence or driver capability metadata.

Task-408 may receive a camera that was produced after driver selection and camera factory steps, but it must not perform those steps itself.

---

# Files To Add

This task should add:

- `Docs/SPECS/Task-408_DEVICE_REGISTRATION_IMPORT_FOUNDATION.md`

This task does not add production code or tests.

---

# Future Implementation Direction

If implementation is approved later, possible files may include:

- `VSP.Device/Registration/DeviceRegistrationService.cs`
- `VSP.Device/Registration/DeviceRegistrationRequest.cs`
- `VSP.Device/Registration/DeviceRegistrationResult.cs`
- `VSP.Device/Registration/DeviceRegistrationStatus.cs`
- `VSP.Device/Registration/DeviceRegistrationReason.cs`
- `VSP.Device/Registration/RegistrationSource.cs`
- `VSP.Device/Registration/DuplicatePolicy.cs`
- `VSP.Device/Registration/DeviceDuplicateCheckResult.cs`

Any implementation requires a separate approved Task Plan.

---

# Unit Test Direction For Future Implementation

Future tests should cover:

- rejects null request
- rejects missing camera
- rejects missing required camera fields
- detects duplicate name
- detects duplicate IP address
- detects duplicate RTSP URL when non-empty
- `Reject` policy does not write repository
- `Skip` policy does not write repository and returns skipped result
- successful registration calls `ICameraRepository.Add`
- repository failure returns explainable failed result
- service does not create camera
- service does not execute discovery
- service does not execute driver selection
- service does not invoke driver factory
- service does not directly depend on SQLite repository

---

# Risks

- Registration may accidentally become camera creation if boundaries are not enforced.
- Duplicate policy may cause data loss if replace or merge is added too early.
- Direct SQLite dependency would break repository abstraction.
- Existing import duplicate checker may be reused incorrectly despite being row-preview oriented.
- Connection test may be requested during registration, but that belongs in a separate task.
- Missing Task-407 formal spec may make the camera input contract ambiguous.

---

# Out Of Scope

- Production Code
- Tests
- Discovery
- Auto Discovery workflow
- Driver Selection
- Camera Factory
- Camera Creation
- Driver Factory Invocation
- Driver Registry modification
- Connection Test
- UI
- SQLite direct access
- SQLite schema changes
- Repository implementation rewrite
- Import file parsing
- Import preview generation
- Replace duplicate policy
- Merge duplicate policy
- Camera update workflow
- Task-409

---

# Non-Goals

Task-408 will not evolve into:

- a discovery workflow
- a driver selection workflow
- a camera factory
- a connection testing workflow
- a SQLite migration workflow
- a UI import wizard
- a duplicate merge engine

---

# Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-408 Device Registration / Import Foundation`

Spec filename:

- `Docs/SPECS/Task-408_DEVICE_REGISTRATION_IMPORT_FOUNDATION.md`
