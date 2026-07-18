# Task-403 Network Scan Foundation

Version: 1.0
Status: Approved
Feature: Discovery
Milestone: Version 1.3

---

# Purpose

Build the formal Network Scan Foundation spec for future discovery-related work.

This task defines the architectural boundary, terminology, responsibilities, and non-goals for network scan behavior inside `VSP.Device`.

This task does not implement production code.

The goal of this foundation is to define how future scan workflows can:

- enumerate candidate targets
- schedule scan work
- control concurrency
- enforce timeout and cancellation
- report reachability results

This task does not define protocol-specific discovery behavior.

---

# Current-State Analysis

Current repository state already contains:

- `Task-401 ONVIF Discovery`
- `Task-402 RTSP Endpoint Probe Foundation`

These two tasks establish two different discovery-related categories:

- protocol-specific discovery of unknown ONVIF devices
- protocol-specific probing of a known RTSP endpoint

What is still missing is a neutral network scan foundation that sits below protocol logic and above raw candidate target definition.

That missing foundation should not duplicate:

- ONVIF WS-Discovery logic
- RTSP protocol probe logic
- auto-discovery orchestration

Instead, it should define the generic scan-layer concerns that protocol-specific or orchestration tasks may depend on later.

---

# Terminology

## Discovery

Discovery means attempting to find previously unknown devices or services without the caller already knowing the exact protocol endpoint that should be contacted.

Examples:

- ONVIF WS-Discovery multicast probe
- future auto-discovery workflows

## Endpoint Probe

Endpoint Probe means contacting one already-known target endpoint and classifying its protocol response.

Example:

- RTSP `OPTIONS` against a known RTSP URI

## Network Scan

Network Scan means enumerating one or more candidate network targets and testing basic reachability within controlled scheduling, concurrency, timeout, and cancellation boundaries.

Network Scan is an infrastructure-level scan foundation.

It is not protocol discovery.
It is not endpoint protocol classification.

## Candidate Target

Candidate Target is a scan input concept representing one item the scan engine may attempt to reach.

The foundation must support these concept types:

- `Host`
- `Host + Port`
- `Endpoint`

These concepts are intended to serve as future inputs for later tasks, including Task-404.

## Reachability

Reachability means a scan result that indicates whether a target could be contacted at the scan layer.

Reachability does not imply protocol identity or service existence.

Important examples:

- Host Reachable != RTSP Exists
- Port Open != ONVIF Exists

---

# Architecture Requirement

Network Scan != Port Scan

This distinction must be explicit in the design.

Network Scan Foundation is only responsible for:

- Target Enumeration
- Scan Scheduling
- Concurrency
- Timeout
- Cancellation
- Reachability Result

It must not include:

- Port Fingerprinting
- Banner Detection
- Protocol Identification
- Vulnerability Scan

This task defines a scan foundation, not a diagnostic, security, or protocol-analysis framework.

---

# Architecture Boundary

This task should remain inside protocol-neutral foundation design under `VSP.Device`.

The future implementation should provide scan-layer primitives only.

Allowed boundary:

- candidate target models
- scan request model
- scan result model
- scheduling rules
- concurrency rules
- timeout semantics
- cancellation semantics
- scan transport boundary if needed later
- focused unit-testable orchestration design

Not allowed in this task:

- ONVIF protocol logic
- RTSP protocol logic
- camera creation
- driver selection
- UI changes
- repository changes
- SQLite changes
- auto discovery orchestration
- service fingerprinting
- vulnerability analysis

---

# Responsibilities

Task-403 Network Scan Foundation is responsible for defining:

- how candidate targets are represented
- how scan work is sequenced or scheduled
- how concurrency is bounded
- how timeout is applied consistently
- how cancellation is propagated
- how basic reachability is reported

Task-403 is not responsible for deciding what protocol a reachable target speaks.

Task-403 is not responsible for creating application-level device records.

---

# Relationship With Task-401 ONVIF Discovery

Task-401 ONVIF Discovery:

- is protocol-specific
- uses multicast WS-Discovery
- discovers unknown ONVIF devices
- parses ONVIF discovery metadata

Task-403 Network Scan Foundation:

- is protocol-neutral
- does not use ONVIF payloads
- does not parse ONVIF metadata
- may eventually provide candidate reachable hosts or host-port pairs that future discovery workflows can use

Task-403 must not absorb Task-401 responsibilities.

---

# Relationship With Task-402 RTSP Endpoint Probe Foundation

Task-402 RTSP Endpoint Probe Foundation:

- probes a known RTSP endpoint
- uses RTSP protocol behavior
- classifies endpoint responses

Task-403 Network Scan Foundation:

- does not perform RTSP protocol exchange
- does not identify RTSP service existence
- may eventually provide candidate targets for future RTSP probe workflows

Task-403 must not absorb Task-402 responsibilities.

---

# Relationship With Future Task-404

Task-403 should define Candidate Target concepts in a way that can feed future higher-level workflows.

The following concepts should be preserved as future-compatible inputs:

- `Host`
- `Host + Port`
- `Endpoint`

Task-404 may consume these candidate targets, but Task-403 does not define Task-404 orchestration behavior.

---

# Input Contract

The future scan foundation should accept a request that can express one or more candidate targets.

Supported conceptual inputs:

- host list
- host and port list
- endpoint list
- scan options such as timeout and concurrency limit

Candidate Target types:

## Host

Represents a host candidate without binding the scan to a specific protocol interpretation.

Examples:

- `192.168.1.10`
- `camera-a.local`

## Host + Port

Represents a host candidate with a concrete port candidate.

Examples:

- `192.168.1.10:554`
- `192.168.1.20:3702`

## Endpoint

Represents a more complete endpoint concept for future workflows, without requiring protocol identification in Task-403 itself.

Examples:

- `rtsp://192.168.1.10/stream1`
- `http://192.168.1.20/onvif/device_service`

Task-403 does not interpret these as protocol ownership claims. They remain candidate targets only.

---

# Output Contract

The future foundation should return scan-layer results only.

Recommended result fields:

- candidate target
- target kind
- scan start time
- scan end time
- response time
- reachability classification
- timeout flag
- cancellation should throw rather than be encoded as a normal success result
- optional transport-level error category
- optional transport-level error message

Recommended reachability classifications:

- `Reachable`
- `Unreachable`
- `TimedOut`
- `UnknownFailure`

Important constraint:

The output must describe scan-layer reachability only.

It must not claim:

- RTSP exists
- ONVIF exists
- service fingerprint identified
- device type identified

---

# Scheduling

The foundation should define scheduling behavior for multiple candidate targets.

Scheduling responsibilities:

- iterate through candidate targets deterministically when needed
- support bounded parallelism
- avoid unbounded fan-out
- keep timeout semantics per target clear

The foundation should not embed orchestration policies for full auto-discovery workflows.

---

# Concurrency

Concurrency is a core foundation responsibility.

The spec should support:

- configurable concurrency limit
- clear behavior when candidate targets exceed that limit
- isolation so one slow target does not block all scan progress unnecessarily

This concurrency control is scan infrastructure only.
It is not a protocol engine.

---

# Timeout And Cancellation Semantics

Rules:

- scan request defines timeout behavior
- non-positive timeout falls back to default timeout
- cancellation token is always honored
- user cancellation throws `OperationCanceledException`
- timeout should be reported as a normal reachability result, not as success
- no implicit retry loop unless a future task explicitly adds one

For multi-target scans:

- timeout should be handled per target unless a future orchestration task defines a broader batch timeout policy

---

# Reachability Semantics

Reachability is intentionally narrow.

The spec must explicitly preserve the following statements:

- Host Reachable != RTSP Exists
- Port Open != ONVIF Exists

Examples:

- a host may reply to network-level contact but not expose RTSP
- a port may accept connections but not speak the expected protocol
- an endpoint may be reachable while still failing protocol-specific validation

Therefore, protocol identification belongs to later protocol-specific tasks, not Task-403.

---

# Files To Add

This task should add:

- `Docs/SPECS/Task-403_NETWORK_SCAN_FOUNDATION.md`

This task does not add production code or tests.

---

# Unit Test Direction For Future Implementation

When Task-403 is implemented later, future tests should focus on:

- candidate target enumeration
- deterministic scheduling
- concurrency limit behavior
- timeout result handling
- cancellation propagation
- reachability result classification
- no protocol identification leakage into scan-layer results

Future tests should not require:

- ONVIF payload parsing
- RTSP request and response handling
- UI integration
- repository integration

---

# Risks

- Future work may try to overload Network Scan with protocol detection responsibilities.
- Future work may blur the distinction between scan-layer reachability and service existence.
- Without explicit terminology, teams may incorrectly treat Network Scan as a port fingerprinting or discovery framework.
- If candidate target definitions are too vague, later tasks may introduce incompatible abstractions.

---

# Out Of Scope

- ONVIF Protocol Logic
- RTSP Protocol Logic
- Camera Creation
- Driver Selection
- UI
- Repository
- SQLite
- Auto Discovery Orchestration
- Port Fingerprinting
- Banner Detection
- Protocol Identification
- Vulnerability Scan
- production implementation for Task-403

---

# Non-Goals

This task will not evolve into:

- an ONVIF discovery engine
- an RTSP probe engine
- a camera onboarding workflow
- a driver selection workflow
- an auto-discovery orchestrator
- a security scanning framework

---

# Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-403 Network Scan Foundation`

Spec filename:

- `Docs/SPECS/Task-403_NETWORK_SCAN_FOUNDATION.md`
