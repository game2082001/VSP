# Task-404 Auto Discovery Coordinator

Version: 1.0
Status: Approved
Feature: Discovery
Milestone: Version 1.3

---

# Purpose

Build the formal Auto Discovery Coordinator spec for future discovery orchestration work.

Auto Discovery Coordinator is a pure workflow coordinator.

It coordinates lower-level discovery and scan foundations without implementing protocol logic, network scan logic, driver logic, persistence, UI behavior, or camera creation.

This task does not implement production code.

---

# Current-State Analysis

Current repository state contains three lower-level discovery foundations:

- `Task-401 ONVIF Discovery`
- `Task-402 RTSP Endpoint Probe Foundation`
- `Task-403 Network Scan Foundation`

These foundations have distinct responsibilities:

- Task-401 handles ONVIF protocol-specific unknown-device discovery.
- Task-402 handles RTSP protocol-specific probing of a known endpoint.
- Task-403 handles protocol-neutral network scan foundation concerns.

Auto Discovery Coordinator should sit above these foundations as an orchestration layer.

It should not duplicate or absorb any lower-level responsibility.

---

# Architecture Overview

AutoDiscoveryCoordinator is responsible for workflow coordination only.

Allowed responsibilities:

- Scheduling
- Calling Task-403
- Calling Task-401
- Calling Task-402
- Merge Results
- Source Attribution

Forbidden responsibilities:

- Retry Protocol Operations
- Parse Protocol Payload
- Detect Protocol
- Build Requests
- Driver Selection
- Driver Capability
- Camera Creation

Coordinator output should represent discovery candidates, not persisted devices.

---

# Architecture Boundary

AutoDiscoveryCoordinator belongs to the discovery workflow layer.

It may depend on abstractions exposed by lower-level foundations, but it must not contain their internal logic.

It must not know:

- how ONVIF probe messages are built
- how ONVIF responses are parsed
- how RTSP requests are built
- how RTSP responses are classified
- how network scan reachability is implemented
- how drivers are selected
- how cameras are created
- how data is persisted
- how UI presents candidates

---

# Responsibilities

AutoDiscoveryCoordinator is responsible for:

- receiving an auto-discovery workflow request
- deciding which approved foundation steps should run
- scheduling foundation calls
- invoking ONVIF discovery through Task-401 boundaries
- invoking Network Scan through Task-403 boundaries
- invoking RTSP Endpoint Probe through Task-402 boundaries
- merging returned foundation results
- preserving source attribution for every merged candidate
- returning coordinator-level candidate results

AutoDiscoveryCoordinator is not responsible for interpreting protocol payloads or identifying device drivers.

---

# Workflow

Recommended conceptual workflow:

1. Receive auto-discovery request.
2. Determine enabled workflow stages.
3. Optionally call Task-401 ONVIF Discovery.
4. Optionally call Task-403 Network Scan.
5. Optionally transform scan-layer candidate targets into Task-402 inputs where the request explicitly enables that workflow.
6. Optionally call Task-402 RTSP Endpoint Probe.
7. Merge all results into coordinator-level candidates.
8. Preserve source attribution for each candidate.
9. Return results without creating cameras or writing to persistence.

The coordinator may schedule foundation calls, but it must not retry protocol operations by itself.

Retry behavior, if ever needed, belongs in explicitly approved future tasks.

---

# Source Attribution

Merged results must preserve source information for future UI and driver selection workflows.

Source attribution examples:

- `NetworkScan`
- `ONVIF`
- `RTSP`

A merged candidate may have more than one source.

Examples:

- a host appears in NetworkScan only
- a device appears in ONVIF only
- a target appears in NetworkScan and is later confirmed by RTSP Endpoint Probe
- a target appears in ONVIF and is also reachable through NetworkScan

Source attribution must be retained without forcing driver selection or camera creation.

---

# Merge Results

Merge behavior should combine lower-level results into coordinator-level candidates.

Merge responsibilities:

- preserve original source results where practical
- retain source attribution
- avoid duplicate candidate entries when lower-level results clearly refer to the same candidate
- preserve uncertainty when identity cannot be proven
- avoid inventing device identity from reachability alone

Merge behavior must not:

- parse protocol payloads
- fingerprint services
- infer driver capability
- create camera entities
- write to repository or SQLite

Reachability is not service existence.

Examples:

- NetworkScan reachability does not prove RTSP exists.
- NetworkScan reachability does not prove ONVIF exists.
- RTSP probe success does not imply ONVIF support.
- ONVIF discovery success does not imply RTSP stream availability.

---

# Relationship With Task-401 ONVIF Discovery

Task-401 owns ONVIF discovery behavior.

AutoDiscoveryCoordinator may:

- call ONVIF discovery
- consume ONVIF discovery results
- merge ONVIF-derived candidates
- attribute candidate source as `ONVIF`

AutoDiscoveryCoordinator must not:

- build ONVIF probe messages
- parse ONVIF response payloads
- implement WS-Discovery
- modify ONVIF timeout or deduplication behavior internally

---

# Relationship With Task-402 RTSP Endpoint Probe Foundation

Task-402 owns RTSP endpoint probe behavior.

AutoDiscoveryCoordinator may:

- call RTSP endpoint probe
- consume RTSP probe results
- merge RTSP-derived candidates
- attribute candidate source as `RTSP`

AutoDiscoveryCoordinator must not:

- build RTSP requests
- parse RTSP responses
- classify RTSP response status
- retry RTSP protocol operations
- infer protocol identity from endpoint text

---

# Relationship With Task-403 Network Scan Foundation

Task-403 owns Network Scan Foundation behavior.

AutoDiscoveryCoordinator may:

- call Network Scan
- consume scan-layer reachability results
- consume candidate targets
- merge scan-derived candidates
- attribute candidate source as `NetworkScan`

AutoDiscoveryCoordinator must not:

- enumerate targets internally
- implement scan scheduling internals
- implement concurrency internals
- implement scan reachability logic
- detect protocols from scan results

---

# Input Contract

The future coordinator request should describe which foundation workflows are enabled.

Recommended request fields:

- enable ONVIF discovery
- enable Network Scan
- enable RTSP Endpoint Probe
- network scan request or reference
- known RTSP endpoint probe requests
- coordinator-level timeout if approved later
- cancellation token supplied by caller

The request should not include:

- driver selection directives
- camera creation commands
- repository write options
- UI display options

---

# Output Contract

The future coordinator should return coordinator-level discovery candidates.

Recommended candidate fields:

- candidate key or identity hint
- source attribution list
- optional ONVIF result reference or summary
- optional Network Scan result reference or summary
- optional RTSP probe result reference or summary
- confidence or merge status if approved later
- errors or warnings at workflow level

The output must not be a `Camera` entity.

The output must not imply driver selection.

The output must not imply persistence.

---

# Scheduling

The coordinator may schedule foundation calls.

Scheduling should remain workflow-level.

Examples:

- run ONVIF discovery and Network Scan as independent stages
- run RTSP Endpoint Probe after candidate targets are available
- preserve cancellation across scheduled work

Scheduling must not duplicate Task-403 scan scheduling internals.

---

# Timeout And Cancellation Semantics

Rules:

- user cancellation must be honored
- user cancellation should propagate as `OperationCanceledException`
- lower-level foundation timeout semantics should remain owned by each foundation
- coordinator-level timeout policy may be defined in a future implementation task

The coordinator should not convert protocol operation failures into protocol-specific retry loops.

---

# Files To Add

This task should add:

- `Docs/SPECS/Task-404_AUTO_DISCOVERY_COORDINATOR.md`

This task does not add production code or tests.

---

# Unit Test Direction For Future Implementation

When implementation is approved later, tests should focus on:

- coordinator calls enabled foundation services
- disabled foundation services are not called
- source attribution is retained
- results from multiple sources are merged deterministically
- cancellation propagates
- no camera creation occurs
- no repository interaction occurs
- no protocol parsing or request building occurs in coordinator tests

Tests should use fake foundation services.

---

# Risks

- Coordinator scope may expand into protocol behavior if boundaries are not enforced.
- Merge logic may accidentally imply device identity from weak reachability evidence.
- Future UI or driver selection needs may pressure the coordinator to create cameras too early.
- Retry logic may be requested later, but it should be introduced deliberately as a separate task.

---

# Out Of Scope

- RTSP Protocol Logic
- ONVIF Protocol Logic
- Network Scan Logic
- Driver Logic
- Driver Selection
- Driver Capability
- Camera Creation
- UI
- Repository
- SQLite
- Retry Protocol Operations
- Parse Protocol Payload
- Detect Protocol
- Build Requests
- production implementation for Task-404

---

# Non-Goals

This task will not evolve into:

- an RTSP protocol engine
- an ONVIF protocol engine
- a network scanner
- a driver selector
- a camera creation workflow
- a persistence workflow
- a UI workflow

---

# Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-404 Auto Discovery Coordinator`

Spec filename:

- `Docs/SPECS/Task-404_AUTO_DISCOVERY_COORDINATOR.md`
