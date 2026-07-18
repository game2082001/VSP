# Task-407 Camera Factory Foundation

Version: 1.0
Status: Approved
Feature: Discovery
Milestone: Version 1.3

---

# Purpose

Define the Camera Factory Foundation for creating `Camera` entities from caller-approved driver metadata and explicit camera initialization data.

Task-407 sits after Driver Selection and before Device Registration.

It creates a `Camera` entity in memory only.

It does not perform discovery, driver selection, repository persistence, SQLite operations, connection testing, credential validation, or driver factory invocation.

This task does not implement production code.

---

# Current-State Analysis

The repository currently contains:

- `Camera` entity
- `DriverDescriptor`
- Task-405 Driver Capability Foundation
- Task-406 Driver Selection Foundation

`Camera` contains fields such as:

- `Name`
- `IpAddress`
- `Brand`
- `ConnectionType`
- `Model`
- `Location`
- `HttpPort`
- `RtspPort`
- `SdkPort`
- `Username`
- `Password`
- `RtspUrl`
- `Status`
- `CreateTime`
- `LastModifyTime`

Task-406 returns explainable driver compatibility results.

Task-407 must not consume a full ambiguous driver selection result and choose a driver itself.

Instead, Task-407 must consume an explicit caller-approved driver reference.

---

# Architecture Overview

The intended flow is:

`Driver Selection`

-> caller approval of one driver

-> `ApprovedDriverReference`

-> `CameraInitializationData`

-> `CameraFactory`

-> `CameraFactoryResult`

-> `Camera`

Task-407 only builds a `Camera` entity from approved input.

Task-408 Device Registration is responsible for duplicate detection and repository persistence.

---

# Camera Factory Responsibility Boundary

Camera Factory is responsible for:

- creating a `Camera` entity
- initializing allowed `Camera` fields
- applying the approved driver's connection type
- validating required input for camera creation
- returning an explainable `CameraFactoryResult`

Camera Factory is not responsible for:

- Discovery
- Driver Selection
- Driver Ranking
- Confidence
- Tie Breaking
- Repository
- SQLite
- Device Registration
- Connection Test
- Credential Validation
- Driver Factory Invocation
- Camera import execution
- UI behavior

---

# Input Contract

Camera Factory must not receive a full `DriverSelectionResult` and choose a driver.

The caller must provide a clearly approved driver input.

Recommended models:

- `ApprovedDriverReference`
- `CameraInitializationData`
- `CameraFactoryRequest`

## ApprovedDriverReference

`ApprovedDriverReference` should represent that the caller has already approved which driver should be used.

Required conceptual fields:

- `DriverId`
- `ConnectionType`
- optional `DriverDisplayName`
- optional stable metadata copied from `DriverDescriptor`
- explicit approval semantic, such as `IsApproved`

Rules:

- Camera Factory must validate that an approved driver reference is present.
- Camera Factory must reject input where the driver is not explicitly approved.
- Camera Factory must not re-evaluate compatibility.
- Camera Factory must not rank drivers.
- Camera Factory must not invoke `DriverDescriptor.Factory`.

## CameraInitializationData

`CameraInitializationData` should carry explicit camera field values from trusted upstream flow or user input.

Recommended fields:

- `Name`
- `IpAddress`
- `Brand`
- `Model`
- `Location`
- `HttpPort`
- `RtspPort`
- `SdkPort`
- `RtspUrl`
- optional status override if approved later

Rules:

- fields must be explicit
- weak evidence must not be transformed into brand identity
- RTSP URL may only be written when explicitly provided
- credentials and secrets should not be introduced as new factory input in Task-407

## CameraFactoryRequest

`CameraFactoryRequest` should contain:

- `ApprovedDriverReference`
- `CameraInitializationData`
- optional timestamp provider or timestamp input if future implementation needs deterministic tests

The request must not contain:

- discovery commands
- driver selection commands
- repository commands
- SQLite commands
- connection test commands
- driver factory commands

---

# Output Contract

Recommended models:

- `CameraFactoryResult`
- `CameraFactoryStatus`
- `CameraFactoryReason`

## CameraFactoryResult

Recommended fields:

- status
- created `Camera` when successful
- reasons

Failure must not return a partially completed `Camera` entity.

Result must be explainable.

It must not be only:

- `bool`
- `Camera?`
- exception for normal validation failures

## CameraFactoryStatus

Recommended statuses:

- `Created`
- `Rejected`
- `Failed`

Status semantics:

- `Created` means a complete in-memory `Camera` entity was created.
- `Rejected` means input validation prevented camera creation.
- `Failed` means unexpected factory-level failure occurred.

## CameraFactoryReason

Reasons must be structured data.

Required fields:

- `Code`
- `Message`

Optional future fields:

- field name
- severity
- diagnostic metadata

---

# Camera Field Mapping

Camera Factory should define safe field mapping rules.

## Name

Source:

- `CameraInitializationData.Name`

Rules:

- required
- must not be empty or whitespace
- missing name rejects creation

## IpAddress

Source:

- explicit initialization data

Rules:

- required when the selected driver needs host-based connection information
- must not be inferred from weak evidence
- invalid or missing required address rejects creation

## ConnectionType

Source:

- `ApprovedDriverReference.ConnectionType`

Rules:

- must come from approved driver reference
- must not be selected by Camera Factory
- missing approved driver rejects creation

## Brand

Source:

- explicit trusted initialization data only

Rules:

- do not infer brand from weak evidence
- do not infer brand from source attribution alone
- if no trusted brand exists, use safe default such as `CameraBrand.Unknown`

## Model

Source:

- explicit initialization data or trusted upstream summary

Rules:

- optional
- preserve value when provided
- do not use model to infer brand in Task-407

## Location

Source:

- explicit initialization data or trusted upstream summary

Rules:

- optional
- preserve value when provided

## Port

Sources:

- `HttpPort`
- `RtspPort`
- `SdkPort`

Rules:

- ports must be valid TCP/UDP port values from 1 to 65535
- invalid ports reject creation
- safe defaults may follow existing `Camera` defaults when no explicit value is provided

## RtspUrl

Source:

- explicit initialization data only

Rules:

- only write RTSP URL when explicitly provided
- missing RTSP URL rejects creation only when required by the approved driver or requested initialization contract
- invalid endpoint rejects creation

## Status

Source:

- factory default unless explicitly approved later

Rules:

- recommended default is existing safe default such as `CameraStatus.Offline`
- factory must not run connection test to determine online status

## CreatedAt / UpdatedAt

Mapped to current existing fields:

- `CreateTime`
- `LastModifyTime`

Rules:

- should be initialized consistently
- deterministic timestamp injection may be considered in implementation tests
- factory should not depend on repository-generated timestamps

---

# Credentials / Secrets

Task-407 must distinguish:

- Camera Entity Initialization
- Driver Settings
- Credentials / Secrets

If there is no formal secrets storage strategy:

- do not add a new secrets storage strategy in Camera Factory
- do not introduce new credential models
- do not introduce new plaintext secret persistence behavior
- do not validate credentials

Existing `Camera` currently has credential-related fields such as:

- `Username`
- `Password`

Task-407 does not change the existing security model.

Task-407 does not endorse plaintext credential storage.

Credentials / Secrets handling is out of scope and should be handled by a future approved security-oriented task.

---

# Validation Rules

Camera Factory must reject creation for normal validation failures using `CameraFactoryResult`.

Recommended validation failures:

- missing approved driver
- driver not approved
- missing name
- missing required connection information
- invalid port
- invalid endpoint
- unsupported initialization data

Rules:

- failure should not create a partial `Camera`
- validation reasons should be structured
- normal validation failure should not be represented as an unhandled exception

Recommended reason codes:

- `MissingApprovedDriver`
- `DriverNotApproved`
- `MissingName`
- `MissingConnectionInformation`
- `InvalidPort`
- `InvalidEndpoint`
- `UnsupportedInitializationData`

---

# Relationship With Task-406 Driver Selection

Task-406 produces driver compatibility results.

Task-407 does not evaluate driver compatibility.

Task-407 requires caller approval of a driver before camera creation.

Task-407 must not:

- consume full `DriverSelectionResult` and pick a driver
- rank compatible drivers
- compute confidence
- perform tie breaking
- prefer a driver

---

# Relationship With Task-408 Device Registration

Task-407 outputs a successfully created in-memory `Camera` entity.

Task-408 receives already-created `Camera` entities.

Task-408 must not recreate or repair Camera Factory output.

Task-408 is responsible for:

- duplicate detection
- duplicate policy
- repository persistence
- registration result

Task-407 must not write repository.

Task-407 must not create SQLite data.

---

# Files To Add

This task should add:

- `Docs/SPECS/Task-407_CAMERA_FACTORY_FOUNDATION.md`

This task does not add production code or tests.

---

# Files To Add In Future Implementation

Future implementation may include:

- `VSP.Device/Cameras/Factory/CameraFactory.cs`
- `VSP.Device/Cameras/Factory/CameraFactoryRequest.cs`
- `VSP.Device/Cameras/Factory/CameraFactoryResult.cs`
- `VSP.Device/Cameras/Factory/CameraFactoryStatus.cs`
- `VSP.Device/Cameras/Factory/CameraFactoryReason.cs`
- `VSP.Device/Cameras/Factory/CameraInitializationData.cs`
- `VSP.Device/Cameras/Factory/ApprovedDriverReference.cs`
- corresponding unit tests

Any implementation requires a separate approved Task Plan.

---

# Unit Test Direction For Future Implementation

Future tests should cover:

- creates camera when approved driver and required initialization data are valid
- rejects missing approved driver
- rejects driver reference that is not approved
- rejects missing name
- rejects missing required connection information
- rejects invalid ports
- rejects invalid endpoint
- preserves explicit RTSP URL
- does not infer brand from weak evidence
- defaults brand to `Unknown` when no trusted brand is provided
- defaults status without running connection test
- does not invoke driver factory
- does not call repository
- does not create SQLite data
- does not run driver selection

---

# Risks

- Camera Factory may accidentally become Driver Selection if it accepts full selection result and chooses a driver.
- Camera Factory may accidentally become Device Registration if it writes repository.
- Weak evidence may be used to infer brand incorrectly.
- Credential handling may be introduced without a secrets strategy.
- Driver factory invocation may pull runtime behavior into entity creation.
- Missing validation may create partially initialized cameras.

---

# Out Of Scope

- Production code
- Tests
- Discovery
- Driver Selection
- Driver Ranking
- Confidence
- Tie Breaking
- Driver Factory Invocation
- Repository
- SQLite
- Device Registration
- Camera Import
- UI
- Credential Validation
- Secrets Storage
- Connection Testing
- Task-408 Implementation

---

# Non-Goals

Task-407 will not evolve into:

- a discovery workflow
- a driver selection workflow
- a driver ranking engine
- a repository workflow
- a SQLite persistence workflow
- a connection testing workflow
- a credential storage strategy
- a camera import workflow

---

# Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-407 Camera Factory Foundation`

Spec filename:

- `Docs/SPECS/Task-407_CAMERA_FACTORY_FOUNDATION.md`
