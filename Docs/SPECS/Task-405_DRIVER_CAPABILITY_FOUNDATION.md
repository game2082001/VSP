# Task-405 Driver Capability Foundation

Version: 1.0
Status: Approved
Feature: Discovery
Milestone: Version 1.3

---

# Purpose

Define the Driver Compatibility Capability foundation so Discovery and future Driver Selection can remain decoupled.

Task-405 establishes terminology, architecture boundaries, and future model direction for describing what discovery evidence a driver can accept.

This task does not implement production code.

This task does not select drivers, create cameras, modify discovery workflows, or change persistence.

---

# Current-State Analysis

Version 1.3 Discovery currently contains lower-level discovery foundations:

- Task-401 ONVIF Discovery Foundation
- Task-402 RTSP Endpoint Probe Foundation
- Task-403 Network Scan Foundation
- Task-404 Auto Discovery Coordinator

These foundations intentionally avoid driver selection and camera creation.

Task-404 returns coordinator-level discovery candidates with source attribution.

Driver Framework currently contains:

- `DriverDescriptor`
- `DriverRegistry`
- `IDriverPlugin`
- `BuiltInCameraDriverPlugin`
- `DriverSettingsDefinition`
- `DeviceCapability`

Current driver metadata is focused on registration, connection type, settings, and runtime driver behavior.

There is not yet a formal compatibility capability model that can describe which discovery evidence a driver can accept.

Without Task-405, future Driver Selection work may be tempted to make Discovery directly know about drivers.

That would violate the Task-404 boundary.

---

# Capability Terminology

Task-405 distinguishes two separate capability categories.

## Runtime Capability

Runtime Capability describes what a driver or device can do after it is selected and configured.

Examples:

- Live View
- Snapshot
- PTZ
- Events
- Playback
- Audio

The existing `DeviceCapability` model is closer to Runtime Capability.

Runtime Capability is not the focus of Task-405.

## Driver Compatibility Capability

Driver Compatibility Capability describes what evidence and connection information a driver can accept before a driver is selected.

Examples:

- acceptable discovery evidence types
- required evidence
- optional evidence
- unsupported evidence
- required settings
- required connection information

Task-405 only handles Driver Compatibility Capability.

It must not decide which driver wins.

It must not assign confidence scores.

It must not create cameras.

---

# Evidence Terminology

`AutoDiscoverySource` is not the same thing as protocol evidence.

`AutoDiscoverySource` only describes where the evidence came from.

For example:

- `NetworkScan` means the candidate came from network scan output.
- `NetworkScan` does not prove RTSP exists.
- `NetworkScan` does not prove ONVIF exists.
- `NetworkScan` does not prove any service identity.

Driver Compatibility Capability must distinguish:

- Evidence Source
- Evidence Type or Kind
- Evidence Value

Recommended conceptual model:

## Evidence Source

Evidence Source describes provenance.

Examples:

- `NetworkScan`
- `ONVIF`
- `RTSP`
- future vendor probe
- future manual input

Source is attribution only.

Source is not protocol identity.

## Evidence Type / Kind

Evidence Type describes what the evidence means.

Examples:

- `Host`
- `Port`
- `Endpoint`
- `OnvifDiscovery`
- `RtspEndpointProbe`
- `AuthenticationChallenge`
- `VendorHint`
- `ModelHint`
- `ManufacturerHint`
- `RequiredSetting`

Evidence Type should be type-safe.

It should not be a loose string list in core architecture.

## Evidence Value

Evidence Value carries the evidence payload or normalized value.

Examples:

- host value
- port number
- endpoint URI
- manufacturer name
- model name
- vendor-specific identifier
- required setting key

Evidence Value should remain separate from Source.

---

# Architecture Overview

Driver Compatibility Capability sits between Discovery and future Driver Selection.

Discovery produces candidates and evidence.

Driver Compatibility Capability describes what a driver can accept.

Future Driver Selection compares candidate evidence against driver compatibility metadata.

The dependency direction should remain:

Discovery Foundation

-> Discovery Candidate / Evidence

-> Driver Compatibility Capability Metadata

-> Future Driver Selection

Driver Selection may read both candidate evidence and driver compatibility metadata.

Discovery must not read driver metadata.

Driver capability metadata must not execute discovery.

---

# Architecture Boundary

Allowed for Task-405:

- define Driver Compatibility Capability terminology
- define evidence source / kind / value separation
- define required evidence concept
- define optional evidence concept
- define unsupported evidence concept
- define required settings concept
- describe relationship with existing Driver Plugin architecture
- describe future implementation direction

Not allowed for Task-405:

- production implementation
- unit tests
- automatic driver selection
- confidence scoring
- ranking
- preferred driver logic
- tie breaking
- camera creation
- discovery workflow changes
- UI changes
- repository changes
- SQLite changes

---

# Responsibilities

Driver Compatibility Capability is responsible for describing:

- which evidence kinds a driver can accept
- which evidence kinds are required
- which evidence kinds are optional
- which evidence kinds are unsupported
- which settings or connection information are required before use

Driver Compatibility Capability is not responsible for:

- collecting evidence
- probing endpoints
- scanning networks
- parsing protocol payloads
- choosing the best driver
- creating cameras
- writing persistence
- rendering UI

---

# Required Evidence

Required Evidence describes the minimum evidence needed for a driver to be considered compatible by a future selection workflow.

Examples:

- RTSP driver may require an RTSP endpoint value.
- ONVIF driver may require ONVIF service endpoint evidence.
- Vendor SDK driver may require a host and vendor-specific connection information.

Required Evidence is descriptive metadata only.

Task-405 does not evaluate whether a candidate satisfies it.

That evaluation belongs to a future Driver Selection task.

---

# Optional Evidence

Optional Evidence describes evidence that can improve future selection or configuration but is not mandatory.

Examples:

- manufacturer hint
- model hint
- optional port
- authentication challenge
- supported service hint

Task-405 does not define scoring behavior for optional evidence.

Optional Evidence must not become confidence scoring in this task.

---

# Unsupported Evidence

Unsupported Evidence describes evidence kinds that a driver cannot use.

Examples:

- a generic RTSP driver may not use ONVIF-only metadata.
- a generic ONVIF driver may not use vendor SDK-specific evidence.
- a vendor driver may reject evidence that belongs to a different vendor family.

Unsupported Evidence is descriptive metadata only.

It must not perform selection or ranking by itself.

---

# Required Settings

Required Settings describe configuration values needed before a driver can be instantiated or used safely.

Examples:

- username
- password
- HTTP port
- RTSP port
- SDK port
- RTSP URL
- service endpoint

Required Settings should align with existing `DriverSettingsDefinition` concepts where possible.

Task-405 does not collect settings.

Task-405 does not validate credentials.

Task-405 does not create camera records from settings.

---

# Vendor-Specific Evidence Extensibility

Vendor-specific evidence must be extensible.

The foundation must not hard-code specific vendors into the core architecture.

The core model should not be limited to:

- Hikvision
- Dahua
- VIVOTEK
- Axis
- any fixed vendor list

Vendor names may appear later as metadata instances.

Vendor-specific evidence should be represented through extensible evidence kinds, namespaces, or metadata values.

The foundation should allow future vendors to add compatibility metadata without changing core capability architecture.

---

# Relationship With Existing Driver Plugin Architecture

`IDriverPlugin` continues to provide `DriverDescriptor` objects.

Task-405 should not change plugin registration behavior.

Future architecture may allow `DriverDescriptor` to hold compatibility metadata.

Future architecture may allow `BuiltInCameraDriverPlugin` to declare compatibility metadata for built-in drivers.

Important boundaries:

- Discovery does not know Driver.
- Driver Capability does not execute Discovery.
- Driver Plugin does not run Auto Discovery.
- Driver Selection is a future task.
- Camera Creation is not part of Task-405.

---

# Relationship With DeviceCapability

The current `DeviceCapability` model represents runtime behavior such as live view, playback, PTZ, audio, events, snapshot, and discovery support.

Task-405 does not replace or modify `DeviceCapability`.

Task-405 defines a separate Driver Compatibility Capability concept.

Technical Debt:

- `DeviceCapability.cs` currently appears under a path named `VSP.Domain/Enums` while using the namespace `VSP.Domain.Entities`.
- This path / namespace mismatch is technical debt.
- Task-405 records the issue only.
- Task-405 does not fix it unless it directly blocks the approved architecture.

At the current spec level, the mismatch does not block the Driver Compatibility Capability architecture.

---

# Relationship With Task-404 Auto Discovery Coordinator

Task-404 produces discovery candidates and source attribution.

Task-405 describes how drivers may later declare what evidence they can accept.

Task-404 must not select drivers.

Task-405 must not modify Task-404 workflow.

Future Driver Selection may consume:

- `AutoDiscoveryCandidate`
- evidence extracted from candidates
- driver compatibility metadata

That future step is explicitly outside Task-405.

---

# Input Contract Direction

Future Driver Compatibility Capability metadata should be declared by driver metadata, not by discovery workflow.

Recommended metadata inputs:

- driver id
- supported evidence kinds
- required evidence kinds
- optional evidence kinds
- unsupported evidence kinds
- required setting keys
- optional setting keys if needed later

Input should avoid:

- ranking weights
- confidence scores
- selection priority
- preferred driver flags
- camera creation instructions

---

# Output Contract Direction

Future Driver Compatibility Capability metadata should be queryable as descriptive metadata.

Recommended output concepts:

- compatibility capability definition
- required evidence list
- optional evidence list
- unsupported evidence list
- required settings list

Output must not include:

- selected driver
- ranked driver list
- confidence score
- camera entity
- persistence command
- UI instruction

---

# Files To Add

This task should add:

- `Docs/SPECS/Task-405_DRIVER_CAPABILITY_FOUNDATION.md`

This task does not add production code or tests.

---

# Future Implementation Direction

If implementation is approved later, possible files may include:

- `VSP.Device/Drivers/Capabilities/DriverCompatibilityCapability.cs`
- `VSP.Device/Drivers/Capabilities/DriverCompatibilityEvidenceKind.cs`
- `VSP.Device/Drivers/Capabilities/DriverCompatibilityEvidenceRequirement.cs`
- `VSP.Device/Drivers/Capabilities/DriverCompatibilityMetadata.cs`
- tests for capability metadata construction and validation

Possible future minimal modification:

- `DriverDescriptor` may hold optional compatibility metadata.

Any such implementation requires a separate approved Task Plan.

---

# Unit Test Direction For Future Implementation

Future implementation tests should focus on:

- required evidence metadata can be expressed
- optional evidence metadata can be expressed
- unsupported evidence metadata can be expressed
- required settings can be expressed
- source and evidence kind are distinct
- vendor-specific evidence can be represented without hard-coded vendor enums
- `DriverDescriptor` can carry metadata if approved
- no driver selection occurs
- no discovery service is called
- no camera is created

---

# Risks

- Future work may incorrectly treat `AutoDiscoverySource` as protocol evidence.
- Driver Compatibility Capability may drift into Driver Selection if confidence or ranking is added too early.
- Vendor-specific support may become hard-coded if the evidence model is not extensible.
- Runtime Capability and Compatibility Capability may be confused because both use the word capability.
- Existing `DeviceCapability` path / namespace mismatch may cause future developer confusion.

---

# Out Of Scope

- Production Code
- Tests
- Driver Selection
- Confidence / Ranking
- Preferred Driver
- Tie Breaking
- Automatic Driver Selection
- Camera Creation
- Discovery Workflow
- ONVIF Logic
- RTSP Logic
- Network Scan Logic
- UI
- Repository
- SQLite
- Task-406

---

# Non-Goals

Task-405 will not evolve into:

- a driver selector
- a camera onboarding workflow
- a discovery workflow
- a confidence scoring engine
- a ranking framework
- a vendor-specific hard-coded rules engine
- a repository or persistence workflow

---

# Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-405 Driver Capability Foundation`

Spec filename:

- `Docs/SPECS/Task-405_DRIVER_CAPABILITY_FOUNDATION.md`
