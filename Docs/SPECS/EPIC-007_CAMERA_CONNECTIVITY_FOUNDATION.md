# Epic-007 Camera Connectivity Foundation

Status: Approved (with refinements)
Feature: Driver Framework / Connectivity
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

---

# Approval Record

- Proposed as "Camera Connectivity Foundation" (originally scoped narrower as "ONVIF Camera Connectivity"; broadened by the Product Owner at proposal time).
- Approved by the Product Owner with four refinements, confirmed within approved scope:
  1. This Epic is the **foundation of the Camera Connectivity layer**; ONVIF is the first implementation, not the objective itself.
  2. Hikvision is explicitly **excluded** from this Epic. The shared HTTP transport must be reusable by a future Hikvision Epic, but Hikvision itself is not implemented here.
  3. Definition of Done extended: **every driver where `DriverFactory.IsDriverImplemented() == true` must have a real `TestConnection`** — no implemented driver may remain a stub.
  4. Scope extended beyond `TestConnection` to also retrieve **basic Device Information** (Model, Manufacturer, Firmware, Serial Number) when available, so the Connectivity Layer is reusable by future Dashboard and Camera Detail features.
- Approved by: Product Owner (this conversation).
- Execution mode: Autonomous within this Epic, per `AUTONOMOUS_DEVELOPMENT.md` §7, until Epic Review or a defined Stop Condition (`AI_OPERATING_SYSTEM.md` §8).

---

# Objective

Establish the foundation of the Camera Connectivity layer — a real, working `TestConnection` and basic `GetDeviceInformation` for every driver the app claims to support — beginning with ONVIF, on a shared HTTP transport designed for reuse by future drivers (starting with Hikvision, in a later Epic).

---

# Current-State Analysis Summary

(Full analysis produced and reviewed before approval; summarized here for the record.)

- `IDeviceDriver.TestConnection(Camera) -> bool` is already a uniform contract every caller goes through consistently: `CameraDetailWindow`'s single-camera Test Connection and `CameraConnectionTester` (Batch Connection Test) both call `DriverFactory.CreateCameraDriver(connectionType).TestConnection(camera)` — verified directly in `CameraConnectionTester.cs`. The gap is not architectural.
- Verified by reading every driver class directly: only `RtspCameraDriver.TestConnection` (Epic-003) is real. `OnvifCameraDriver`, `HikvisionIsapiCameraDriver`, `DahuaNetSdkCameraDriver` all unconditionally `return false;` for `TestConnection`, `StartLive`, `StopLive`, and `Snapshot`. `DriverFactory.IsDriverImplemented` already tracks exactly this (`RTSP => true`, everything else `=> false`).
- No `HttpClient`/`System.Net.Http` usage exists anywhere in `VSP.Device` (verified by search). Every protocol to date is hand-rolled sockets (`TcpClient`, `UdpClient`) with hand-built XML (`System.Xml.Linq`) — no SOAP toolkit, no WS-Security library. This is the established house style and the constraint that keeps this Epic's Risk Ceiling at MEDIUM (no new package).
- ONVIF *discovery* (WS-Discovery, Task-401) and ONVIF *device management* (what `TestConnection`/`GetDeviceInformation` need — SOAP-over-HTTP against `/onvif/device_service`) are separate ONVIF sub-protocols; the former being shipped proves nothing about the latter.
- Dahua's driver is named "NetSDK" for a reason: it implies a proprietary native vendor SDK, not a hand-rollable wire protocol like RTSP/ONVIF — a fundamentally different integration category, carrying its own External Package/licensing decision. Axis (`DeviceConnectionType.AxisVAPIX`) has no driver class at all and silently falls back to `RtspCameraDriver` via `DriverFactory`'s `?? new RtspCameraDriver()` — a pre-existing, unrelated latent issue.
- No test-double `ICameraDriver` implementer exists that won't need updating: exactly four (`DriverSelectionTests`, `DriverRegistryTests`, `DriverPluginTests`, `DriverCompatibilityCapabilityTests`, each with a private `TestCameraDriver`) — identified up front so the interface extension doesn't silently break the test suite.

---

# Architecture Review Summary

- `IDeviceDriver` needs no redesign — this Epic fills in stub bodies behind an already-correct, already-uniform interface.
- Adding `DeviceInformation? GetDeviceInformation(Camera camera)` to `IDeviceDriver` (alongside `TestConnection`) is judged the right level: device-management-plane concepts belong with `TestConnection`/`DriverId`/`Capability` on the base interface, not the camera-media-specific `ICameraDriver` (`StartLive`/`StopLive`/`Snapshot`). This is a breaking change to the interface's implementers, all of which are inside this Epic's own scope to update (4 driver classes + 4 test fakes) — treated as Implementation Authority, since the Product Owner explicitly requested exactly this reusable capability.
- `IDeviceDriver.TestConnection`/`GetDeviceInformation` remain synchronous (unchanged calling convention for `CameraDetailViewModel`/`CameraConnectionTester`/Batch Test — no UI call-site changes needed). ONVIF's HTTP calls use `HttpClient.Send(...)` (a genuinely synchronous .NET 5+ API, not a blocking wrapper over the async API), avoiding both an interface-shape change and the older thread-pool-starvation anti-pattern.
- The shared HTTP transport (`VSP.Device/Drivers/Http/`) is deliberately narrow: bounded-timeout HTTP send/receive only. Protocol-specific concerns (SOAP envelope shape, WS-Security for ONVIF; Digest auth, ISAPI paths for a future Hikvision driver) stay in each driver's own request/response code, mirroring how `RtspAuthorizationHeaderBuilder` is RTSP-specific rather than shared. This avoids speculative shared-auth abstraction before Hikvision's actual needs are known.
- `DeviceCapability.SupportsDeviceInformation` (new bool, additive, non-breaking) declares which driver types can supply device info, mirroring the existing `SupportsLiveView`/`SupportsPTZ`/etc. pattern.

---

# Scope Boundary

**In scope:**
- Real `OnvifCameraDriver.TestConnection`: calls ONVIF `GetSystemDateAndTime` (unauthenticated per ONVIF spec) against `http://{Camera.IpAddress}:{Camera.HttpPort}/onvif/device_service`; success is a well-formed, non-fault SOAP response.
- Real `OnvifCameraDriver.GetDeviceInformation`: calls ONVIF `GetDeviceInformation`, including a WS-Security UsernameToken (PasswordDigest, SHA1, BCL-only) when `Camera.Username` is non-empty; returns `Model`/`Manufacturer`/`FirmwareVersion`/`SerialNumber` when present in the response, `null` on failure/fault/unreachable.
- A shared `VSP.Device/Drivers/Http/` HTTP transport (`HttpClient`-based, synchronous via `.Send()`, bounded timeout, per-call instantiation matching existing house style) — transport only, no auth logic, explicitly designed for reuse by a future Hikvision Epic.
- `DeviceInformation` DTO (`VSP.Device/Drivers/Abstractions/`) and `IDeviceDriver.GetDeviceInformation` — implemented for real only by ONVIF in this Epic; every other current driver (`RtspCameraDriver`, `HikvisionIsapiCameraDriver`, `DahuaNetSdkCameraDriver`) gets a trivial `return null;` so the interface compiles and behaves honestly (no device-info capability claimed where none exists).
- `DeviceCapability.SupportsDeviceInformation` (new flag), set `true` only for `OnvifCameraDriver`.
- `DriverFactory.IsDriverImplemented(ONVIF) => true` once the above is real and tested.
- Unit tests for all new SOAP/HTTP/WS-Security code, using a loopback HTTP test server, following the pattern already established by `LoopbackRtspTestServer` (Epic-003).

**Explicitly excluded (Product Owner refinement #2):** Hikvision ISAPI implementation. The shared HTTP transport must not be ONVIF-only by construction, but no Hikvision driver code is written in this Epic.

---

# Risk Ceiling

**MEDIUM**, bounded by one hard constraint carried over from the original proposal: no new external package or native dependency. `HttpClient` and `System.Security.Cryptography.SHA1` are BCL, not new packages. If any implementation step reveals ONVIF device management realistically requires a SOAP/WS-Security library or anything beyond hand-rolled XML, that crosses into **External Package** — a Stop Condition, escalated rather than absorbed into this Risk Ceiling.

---

# Definition of Done

1. `OnvifCameraDriver.TestConnection` and `GetDeviceInformation` are real, evidence-based implementations — not stubs — verified against a loopback HTTP test server exercising success, SOAP fault, malformed response, and timeout cases.
2. `DriverFactory.IsDriverImplemented(ONVIF) == true`, and for **every** connection type where `IsDriverImplemented(...) == true` (today: RTSP and ONVIF), the corresponding driver's `TestConnection` is a real implementation — no implemented driver remains a stub (Product Owner refinement #3, verified by inspection at Epic Review, not just for the driver this Epic happens to touch).
3. `GetDeviceInformation` returns real, best-effort Model/Manufacturer/FirmwareVersion/SerialNumber for ONVIF cameras when the device provides them, `null` when not (Product Owner refinement #4).
4. The shared HTTP transport has no ONVIF-specific code in it (verified by inspection) — ready for a future Hikvision Epic to reuse without modification.
5. All new code has unit test coverage; the full existing suite remains green; build stays passing; `Docs/CHANGELOG.md` is updated.

---

# Out of Scope

- Hikvision ISAPI driver implementation (future Epic; only the shared transport is built now).
- Dahua NetSDK implementation (native SDK/licensing decision, separate from this Epic).
- Axis driver (none exists; not raised by current product evidence).
- `StartLive`/`StopLive`/`Snapshot` for any driver (Live View territory — separate, larger, likely-external-package Epic).
- Any change to `IDeviceDriver`/`ICameraDriver`'s synchronous calling convention, or to any UI call site's async/await shape.
- Any change to the ONVIF *discovery* layer (Task-401).
- WS-Security auth-challenge negotiation/retry (proactive auth when credentials are present is in scope; parsing ONVIF-specific SOAP Fault codes to retry with auth after an initial unauthenticated attempt is not).

---

# Implementation Strategy

Bottom-up: `DeviceInformation` DTO + `IDeviceDriver` extension first (needed for everything else to compile), then the shared HTTP transport (protocol-agnostic, Hikvision-reusable), then ONVIF-specific SOAP/WS-Security/parsing code, then wiring into `OnvifCameraDriver` + `DriverFactory`, then the trivial stub additions to the other three drivers and the four test fakes (needed for the solution to compile at all once the interface changes), then tests, then `CHANGELOG.md`. Build + relevant tests after each step; full suite before Epic Review. Stops immediately if ONVIF auth realistically requires a package beyond hand-rolled XML + BCL cryptography.
