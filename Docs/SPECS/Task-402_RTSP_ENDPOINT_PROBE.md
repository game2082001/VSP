# Task-402 RTSP Endpoint Probe Foundation

Version: 1.0
Status: Approved
Feature: Discovery
Milestone: Version 1.3

---

# Purpose

Build the first RTSP probe foundation for a known endpoint.

This task is not named RTSP Discovery because the current goal is not to discover unknown devices across a network segment. The goal is to probe a specific, already-known RTSP endpoint and classify the endpoint response through a minimal protocol interaction.

This foundation should allow future workflows to answer questions such as:

- Is the RTSP endpoint reachable
- Does the endpoint speak RTSP
- Did the endpoint respond with success, authentication challenge, timeout, connection refusal, malformed response, or explicit non-RTSP protocol response

This task provides protocol-level probing only.

It does not create cameras, scan subnets, infer new devices, or build shared discovery abstractions.

---

# Current-State Analysis

Current repository state already contains an ONVIF discovery foundation under:

- `VSP.Device/Discovery/Onvif/`

That implementation establishes several useful boundaries:

- request model
- result model
- protocol transport boundary
- service orchestration
- parser or protocol-specific logic
- unit tests using fake transport behavior

The existing ONVIF foundation is specifically multicast WS-Discovery based:

- `OnvifDiscoveryService` sends a WS-Discovery probe to multicast `239.255.255.250:3702`
- responses are collected until timeout or cancellation
- results are deduplicated
- user cancellation is propagated
- timeout returns partial collected results

That behavior is appropriate for ONVIF Discovery, but it should not be treated as a generic discovery abstraction for RTSP.

RTSP differs materially:

- RTSP probe targets a known endpoint rather than a multicast discovery address
- RTSP interaction is request and response over TCP
- no endpoint enumeration is implied
- no device identity deduplication flow is required
- response meaning is centered on RTSP status classification, not discovery metadata aggregation

No current repository code appears to provide:

- RTSP endpoint probe request model
- RTSP endpoint probe result model
- RTSP TCP probe transport boundary
- RTSP request factory
- RTSP response classifier
- RTSP endpoint probe service
- RTSP endpoint probe unit tests

Therefore the smallest correct task is a protocol foundation for probing a known RTSP endpoint only.

---

# Terminology

## Discovery

Discovery means attempting to find previously unknown devices or services without the caller already specifying the exact target endpoint.

Examples:

- ONVIF WS-Discovery multicast probe
- future auto-discovery workflows
- subnet-wide device enumeration

Discovery may return zero to many candidate devices.

## Network Scan

Network Scan means iterating over a range of network addresses, ports, or host candidates in order to locate reachable endpoints.

Examples:

- scanning a subnet for open RTSP ports
- probing multiple IPs on `554`
- checking host and port reachability across an address list

Network Scan is broader than a single endpoint probe and may become an input source for later discovery workflows.

## Endpoint Probe

Endpoint Probe means checking one already-known target endpoint and classifying the response.

The caller already knows either:

- a full RTSP URI
- or Host, Port, and Path components

Endpoint Probe does not search for unknown devices.
Endpoint Probe does not scan ranges.
Endpoint Probe does not create device records.

For Task-402, Endpoint Probe is the correct term.

---

# Architecture Boundary

This task should remain inside protocol foundation code under `VSP.Device`.

The task may add protocol-specific RTSP probe code similar in spirit to the ONVIF foundation, but it must not introduce a new shared discovery framework.

Allowed boundary:

- RTSP probe request model
- RTSP probe result model
- RTSP transport boundary for TCP request and response
- RTSP request construction
- RTSP response parsing and classification
- service orchestration
- focused unit tests

Not allowed in this task:

- UI changes
- ViewModel changes
- Driver capability changes
- Repository changes
- SQLite changes
- Camera entity creation
- subnet scan logic
- credential brute force
- shared discovery abstraction layer
- production integration into broader workflows unless separately approved

---

# Relationship With Task-401 ONVIF Discovery

Task-401 and Task-402 are related foundations, but they solve different problems.

Task-401 ONVIF Discovery:

- probes unknown devices through multicast WS-Discovery
- may return multiple devices
- parses ONVIF discovery metadata
- uses deduplication and merge rules

Task-402 RTSP Endpoint Probe Foundation:

- probes one known RTSP endpoint over TCP
- returns one probe result per requested target
- classifies RTSP protocol behavior
- does not imply discovery of unknown devices

Shared principle:

- both are low-level protocol foundations under `VSP.Device`
- both should remain independently unit testable
- both should avoid UI and repository coupling

Important non-goal:

- Task-402 should not retrofit Task-401 into a shared abstraction just because both live under a discovery-related folder

---

# Relationship With Future Network Scan

Future Network Scan may use Task-402 as a lower-level primitive.

Possible future flow:

- enumerate candidate host and port targets
- call RTSP endpoint probe for each known candidate
- aggregate reachability and classification results

Task-402 itself must not perform any range enumeration, subnet iteration, or address generation.

---

# Relationship With Future Auto Discovery

Future Auto Discovery may combine multiple mechanisms such as:

- ONVIF Discovery
- future Network Scan
- RTSP Endpoint Probe
- vendor-specific probes

In that future architecture, Task-402 can act as one protocol capability, but this task must not build the orchestration layer yet.

Task-402 is a foundation only, not the auto-discovery workflow.

---

# Input Contract

The probe service should accept exactly one known target per call.

Supported input forms:

- known RTSP URI
- known Host only
- known Host and Port
- known Host and Path
- known Host, Port, and Path

Path may be empty.

Recommended contract behavior:

- full RTSP URI is the canonical input when available
- Host, Port, and Path input may be normalized into a URI internally
- default port may be `554` when omitted and when the input form allows omission
- empty host is invalid
- malformed URI is invalid
- if Path is omitted or empty, the resulting request target must still be well-defined

This task should not require credentials, but may accept optional username and password fields only for URI normalization context if needed later.
If credentials are accepted in the request model at all, Task-402 must still not implement retry loops or brute force behavior.

---

# Output Contract

The service should return one result object for one requested endpoint.

The result should contain enough information to classify protocol outcome without pretending that a camera has been discovered.

Recommended result fields:

- normalized endpoint URI or target description
- host
- port
- request method used
- response time
- did TCP connect succeed
- did any bytes return
- did the response parse as RTSP
- RTSP status code if available
- RTSP reason phrase if available
- response classification
- optional `WWW-Authenticate` header presence or value summary
- timeout flag
- cancellation flag should not be represented as data; user cancellation should throw
- optional raw first response line for diagnostics
- optional error category or message for non-protocol failures

Recommended response classifications:

- `Success`
- `AuthenticationRequired`
- `NotFound`
- `MethodNotAllowed`
- `ProtocolNotSupported`
- `InvalidResponse`
- `ConnectionFailed`
- `Timeout`
- `Cancelled` should not be returned as a normal result
- `UnknownFailure`

Important constraint:

The output must describe endpoint probe status, not discovered device identity.

---

# Probe Protocol

## TCP Connection

The probe connects via TCP to the known RTSP host and port.

This task does not use UDP, multicast, or subnet broadcast.

A failed TCP connection should classify as connection failure, not discovery failure.

## RTSP OPTIONS Request

After TCP connection succeeds, the probe sends a minimal RTSP `OPTIONS` request.

Recommended wire intent:

- request line: `OPTIONS <request-target> RTSP/1.0`
- include `CSeq`
- include `User-Agent` if needed for deterministic tests and diagnostics
- terminate headers correctly with CRLF

The task only needs one protocol request for foundation scope.

It should not send:

- DESCRIBE
- SETUP
- PLAY
- ANNOUNCE
- keep-alive loops

## Response Classification

`IRtspProbeTransport` is responsible only for sending the request and receiving the response bytes or text.

It is not responsible for response parsing or classification.

Response classification belongs in higher-level RTSP protocol logic.

Suggested baseline rules:

- `RTSP/1.0 200` => `Success`
- `RTSP/1.0 401` => `AuthenticationRequired`
- `RTSP/1.0 404` => `NotFound`
- `RTSP/1.0 405` => `MethodNotAllowed`
- explicit non-RTSP protocol response such as HTTP => `ProtocolNotSupported`
- any syntactically valid RTSP status line with unhandled code => deterministic fallback, likely `UnknownFailure` with status code preserved
- malformed or non-parseable first line without clear alternate protocol identity => `InvalidResponse`
- socket connect failure => `ConnectionFailed`
- read timeout => `Timeout`

Task-402 does not need vendor-specific heuristics beyond basic RTSP status parsing.

---

# Timeout And Cancellation Semantics

Timeout and cancellation should follow the same overall philosophy as Task-401, adapted for a single endpoint.

Rules:

- request model defines probe timeout
- non-positive timeout falls back to a default timeout
- user cancellation token is always honored
- user cancellation throws `OperationCanceledException`
- timeout returns a normal probe result classified as `Timeout`
- transport-level timeout should not be silently swallowed as success
- no retry loop in Task-402

Important distinction from Task-401:

- Task-401 may return partial collected results after timeout because it gathers multiple messages
- Task-402 probes exactly one endpoint, so timeout should return one classified result rather than partial collection semantics

---

# Authentication Scope

Task-402 only identifies whether authentication appears to be required.

In scope:

- observe `401 Unauthorized`
- observe `WWW-Authenticate` header presence
- classify as `AuthenticationRequired`

Out of scope:

- sending credentials
- retry after challenge
- digest authentication negotiation
- basic authentication negotiation
- multiple credential attempts
- credential brute force
- secret storage concerns
- UI credential prompts

This keeps the task as a safe protocol foundation rather than an authentication workflow.

---

# Files To Add Or Modify

Spec file added by this task:

- `Docs/SPECS/Task-402_RTSP_ENDPOINT_PROBE.md`

Expected future production files if this spec is implemented later:

- `VSP.Device/Discovery/Rtsp/RtspEndpointProbeRequest.cs`
- `VSP.Device/Discovery/Rtsp/RtspEndpointProbeResult.cs`
- `VSP.Device/Discovery/Rtsp/IRtspProbeTransport.cs`
- `VSP.Device/Discovery/Rtsp/RtspRequestFactory.cs`
- `VSP.Device/Discovery/Rtsp/RtspResponseClassifier.cs`
- `VSP.Device/Discovery/Rtsp/TcpRtspProbeTransport.cs`
- `VSP.Device/Discovery/Rtsp/RtspEndpointProbeService.cs`
- `VSP.Tests/Discovery/RtspEndpointProbeServiceTests.cs`
- `VSP.Tests/Discovery/RtspResponseClassifierTests.cs`

Files explicitly not modified by this task:

- production code
- tests
- driver capability files
- UI files
- repository files
- SQLite files
- existing Task-401 uncommitted work

---

# Unit Test Plan

When implementation is approved later, unit tests should cover at minimum:

- builds correct `OPTIONS` request for known RTSP URI
- builds correct `OPTIONS` request from Host, Port, and Path input
- supports empty Path input with deterministic request target behavior
- uses default port when appropriate
- normalizes timeout when non-positive
- returns `Success` for `RTSP/1.0 200`
- returns `AuthenticationRequired` for `RTSP/1.0 401`
- returns `NotFound` for `RTSP/1.0 404`
- returns `MethodNotAllowed` for `RTSP/1.0 405`
- returns `ProtocolNotSupported` for clear HTTP response
- preserves unhandled RTSP status code in result
- classifies malformed or ambiguous non-RTSP response as `InvalidResponse`
- classifies TCP connection failure as `ConnectionFailed`
- classifies read timeout as `Timeout`
- captures response time
- propagates user cancellation
- does not retry automatically
- does not perform multi-endpoint scan
- does not create camera entities

Preferred testing style:

- fake transport boundary
- deterministic raw response strings
- no real network dependency
- focused protocol classification tests
- service orchestration tests separate from classifier tests

---

# Risks

- RTSP servers vary in strictness; some may reject absolute URI versus path-only request targets differently.
- Some devices may require authentication before returning useful protocol behavior, limiting probe detail.
- Some endpoints may accept TCP but return non-RTSP payloads or close early.
- Without real-device integration tests, compatibility remains protocol-foundation level only.
- If request-target formatting is chosen incorrectly, otherwise valid endpoints may appear to fail.
- There is a naming risk if future teams conflate endpoint probing with device discovery; terminology must stay precise.

---

# Out Of Scope

- RTSP Discovery naming unless future evidence proves unknown-device discovery behavior
- subnet scan
- IP range enumeration
- port scan
- Auto Discovery orchestration
- ONVIF integration changes
- shared discovery abstraction
- Driver capability changes
- UI integration
- Repository or SQLite integration
- Camera creation
- live stream setup
- DESCRIBE, SETUP, or PLAY workflows
- credential retry
- credential brute force
- vendor-specific probing logic
- background scheduling
- telemetry or analytics pipelines

---

# Non-Goals

This task will not evolve into:

- an RTSP Client
- a Stream Player
- Media Negotiation
- an Authentication Framework
- a Device Discovery Framework

---

# Proposed Final Task Name And Spec Filename

Approved task name:

- `Task-402 RTSP Endpoint Probe Foundation`

Spec filename:

- `Docs/SPECS/Task-402_RTSP_ENDPOINT_PROBE.md`
