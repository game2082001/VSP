# Epic-006 Camera Discovery Workspace

Status: Approved (with refinements)
Feature: Discovery
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

---

# Approval Record

- Originally proposed as "Epic-006: Guided Camera Discovery" and approved by the Product Owner with three refinements.
- Refinements approved in the same message, explicitly confirmed as within approved scope, not Scope Expansion:
  1. Renamed product objective: **Camera Discovery Workspace** (not "Guided Camera Discovery").
  2. Discovery is a **persistent product workspace**, not a transient modal wizard.
  3. Candidate Review functions as a **lightweight, editable Camera List** (basic fields such as Camera Name may be edited before registration), not a fixed multi-step wizard flow.
- Approved by: Product Owner (this conversation).
- Execution mode: Autonomous within this Epic, per `AUTONOMOUS_DEVELOPMENT.md` §7, until Epic Review or a defined Stop Condition (`AI_OPERATING_SYSTEM.md` §8).

---

# Architecture Review & Refactor Record

A first implementation of this Epic was built, fully tested, and then **not accepted for commit** — the Product Owner requested an Architecture Review before any commit decision. The review (requested and delivered as a standalone review artifact, not implementation) found:

1. The first design bundled Discovery (read) and Registration (write) into one new `CameraDiscoveryWorkspaceService`, weakening Single Responsibility relative to the rest of the codebase's boundaries.
2. That service duplicated candidate-evaluation and camera-commit logic that already existed, tested and approved, inside `DiscoveryOrchestrator.ProcessCandidate` — and it bypassed the `IDriverApprovalPolicy` seam Task-501 explicitly reserved for interactive/UI-driven approval, orphaning six already-built cross-cutting hooks (Task-602–607) in the process.
3. Discovery had been placed as a top-level main-navigation item, peer to Devices — inconsistent with the Product Owner's intended product structure (`Devices → Camera List / Import / Discovery / Batch / Export`) and with the existing Import/Batch/Export precedent, all of which live inside the Camera Management Workspace rather than main navigation.
4. Naming (`CameraDiscoveryWorkspaceService`, `DiscoveryCandidatePreview`) carried UI-workflow-specific words into what should be UI-agnostic domain types.

The Product Owner accepted the review and directed a refactor before commit:
1. `DiscoveryOrchestrator` remains the single orchestration pipeline; extend its seam instead of duplicating logic.
2. Discovery ends with evaluated candidates; registration remains owned by `DeviceRegistrationService`, invoked as an explicit, separate step.
3. Discovery becomes a feature inside the Camera Management Workspace, not a Main Navigation workspace.
4. Naming stays as-is until responsibilities are finalized.

This document reflects the **refactored** design below, not the superseded first pass (which was never committed and left no trace beyond this record and the corresponding `CHANGELOG.md` note).

---

# Objective

Give the user a persistent, in-app workspace to find cameras on the local network and add them to the Camera Management Workspace, using the already-built and already-tested Discovery-to-Registration pipeline (Tasks 401–408, 501–505, 601–607). This mirrors how Epic-005 surfaced the already-built Import pipeline — Epic-006 does the same for Discovery.

---

# Current-State Analysis Summary

(Full analysis was produced and reviewed before approval; summarized here for the record.)

- The Discovery backend (ONVIF discovery, RTSP endpoint probe, Network Scan, `AutoDiscoveryCoordinator`, `AutoDiscoveryCandidateEvidenceMapper`, `DriverSelectionService`, `CameraFactory`, `DeviceRegistrationService`, `DiscoveryOrchestrator`, `IDiscoveryRunner` + hooks) is fully implemented and unit-tested (verified via `dotnet build`/`dotnet test`: build passing, 457/457 tests passing at Epic start), but has **zero UI entry point** — confirmed via reference search, `VSP.UI` contains no reference to any Discovery/Registration/Selection namespace.
- **Verified defect:** `BuiltInCameraDriverPlugin` registers Hikvision and Dahua with a `null` `DriverCompatibilityCapability`, which `DriverSelectionService` treats as "no required evidence" → both are unconditionally `Compatible` for every candidate. Combined with `AutoApproveSingleCompatiblePolicy` (only existing `IDriverApprovalPolicy`), every real-world candidate today evaluates to ≥2 compatible drivers and lands in `AwaitingApproval`, with no resolution mechanism anywhere in the codebase. This is why an interactive, human-resolvable driver choice is required for this Epic to deliver working end-to-end registration, not optional polish.
- `AutoDiscoveryCoordinator` requires `IOnvifDiscoveryService`/`INetworkScanService`/`IRtspEndpointProbeService`; the concrete `OnvifDiscoveryService`/`NetworkScanService`/`RtspEndpointProbeService` classes do not declare these interfaces (only test fakes do) — the pipeline is not composable outside test code today.
- No DI container exists anywhere in the app; every ViewModel is hand-composed (e.g. `DriverFactory`'s static default-registry pattern, or inline `new` chains in `CameraListView`'s constructor/code-behind).
- `NetworkScanRequest`/`NetworkScanTarget` require explicit per-host targets; there is no CIDR/subnet auto-enumeration anywhere in the backend.

---

# Scope Boundary (as refined, then as refactored)

**In scope:**
- Discovery as a **feature inside the Camera Management Workspace** (`CameraListView`), reached via a "Discovery" toggle button, not a Main Navigation item and not a modal dialog. The section is persistent within the workspace (state — scan results, in-progress edits — survives toggling back to Camera List and back), per the Architecture Review's Direction 3.
- Discovery controls (method selection, Network Scan target input, Start/Cancel) hosted directly in this section.
- A **candidate list styled and behaving like a lightweight Camera List** (grid, not wizard steps):
  - Each discovered candidate is a row with an **editable Name field** (basic-field editing before registration).
  - A per-row driver indicator; when a candidate has more than one compatible driver, an inline chooser lets the user resolve the ambiguity — implemented as `DiscoveryOrchestrator.DiscoverCandidatesAsync` evaluation output plus an inline `ComboBox`, not a separate approval screen.
  - A Register action (per selected row, consistent with the existing Camera List's batch-selection pattern) that commits the (possibly edited) candidate via `DiscoveryOrchestrator.RegisterCandidate`, which itself calls `CameraFactory` + `DeviceRegistrationService` — the same commit logic `ExecuteAsync` already used, reused rather than duplicated, per the Architecture Review's Direction 1.
- `DiscoveryOrchestrator` extended (not duplicated) with two new public entry points — `DiscoverCandidatesAsync` (evaluation only, never writes) and `RegisterCandidate` (commit for one already-evaluated candidate) — reusing its existing private evaluation/commit logic; its existing `ExecuteAsync` behavior is unchanged.
- A composition helper wiring `DiscoveryOrchestrator` for real use (`DriverFactory`-style static factory, no DI container).
- A minimal Network Scan target input (explicit host list / simple range) — no CIDR/subnet math.
- Unit tests for all new and refactored production code.

**Still explicitly a Product Owner decision, not assumed:** whether to also correct the Hikvision/Dahua "always compatible" driver-metadata gap. Default: **left untouched**; the in-grid driver chooser is the resolution mechanism for this Epic.

---

# Risk Ceiling

**MEDIUM** (unchanged from original approval, and unchanged by the post-review refactor). An extension to an existing internal service (`DiscoveryOrchestrator`), a UI ViewModel/View nested inside an existing workspace, and a hand-written composition helper — no database schema change, no public API break, no new external package, no security-model change, no DI container introduction.

---

# Definition of Done

A user, from the Discovery section of the Camera Management Workspace, can: choose discovery methods (ONVIF always available; Network Scan/RTSP probe with explicit target input) → start a scan → see discovered candidates appear in an editable, Camera-List-like grid → edit basic fields (e.g. Name) and resolve any ambiguous driver match inline → register one or more candidates → see them appear in the Camera Management Workspace's Camera List (on next refresh). All new/refactored code has unit test coverage, the full existing suite remains green (including the pre-existing `DiscoveryOrchestrator` tests, unmodified in behavior), build stays passing, and `Docs/CHANGELOG.md` is updated.

---

# Constituent Tasks (internal, AI-Agent-owned sequencing per Implementation Authority)

Original pass (superseded, see Architecture Review record above):

1. ~~Task-701 — Interface adapters so `AutoDiscoveryCoordinator` is composable outside test code~~ — **retained**, unaffected by the refactor.
2. ~~Task-702 — Network Scan target input parser~~ — **retained**, unaffected by the refactor.
3. ~~Task-703/704 — `CameraDiscoveryWorkspaceService` + its composition factory~~ — **superseded**.
4. ~~Task-705 — `CameraDiscoveryViewModel`/`CameraDiscoveryView`~~ — **retained in concept, rewired** to depend on `DiscoveryOrchestrator` directly.
5. ~~Task-706 — Add "Discovery" to `MainWindowViewModel` navigation~~ — **superseded**.
6. ~~Task-707 — Unit tests, build/test validation, `CHANGELOG.md` entry~~ — carried forward.

Post-review refactor tasks:

7. **Task-708** — Extend `DiscoveryOrchestrator` with `DiscoverCandidatesAsync`/`RegisterCandidate`, refactoring `ProcessCandidate` into shared `EvaluateCandidate`/`CommitCandidate` helpers with zero behavior change to `ExecuteAsync`.
8. **Task-709** — Delete `CameraDiscoveryWorkspaceService`/`DiscoveryCandidatePreview`; add `CameraDiscoveryOrchestratorFactory` composing `DiscoveryOrchestrator` directly.
9. **Task-710** — Rewire `CameraDiscoveryCandidateViewModel`/`CameraDiscoveryViewModel` to `CandidateOrchestrationResult`/`DiscoveryOrchestrator`.
10. **Task-711** — Move Discovery from `MainWindowViewModel` navigation into `CameraListView` as a toggleable, persistent section (`IsShowingDiscovery`/`IsShowingCameraList` on `CameraListViewModel`).
11. **Task-712** — Test suite update (new `DiscoveryOrchestrator` two-phase tests, rewritten `CameraDiscoveryViewModelTests`, new `CameraListViewModel` toggle tests), `CHANGELOG.md` update, Epic Completion Report.

---

# Out of Scope

- Automatic full-LAN/CIDR subnet enumeration.
- `RejectAmbiguousPolicy` / `HighestConfidencePolicy` / ranking or confidence-scoring approval policies.
- Correcting Hikvision/Dahua `DriverCompatibilityCapability` metadata (Driver Framework territory).
- `TestConnection` implementations for ONVIF/Hikvision/Dahua.
- Discovery Session history / audit UI.
- User-configurable Retry/Timeout policy in the UI.
- `DeviceCenterView`/`ViewModel` legacy removal.
- Any database schema change, new external package, or DI container introduction.

---

# Implementation Strategy

Bottom-up: interface adapters and orchestration extension first (nothing in the UI can function without them), then UI rewiring, then product-structure placement (nesting inside the Camera Management Workspace), then test hardening. Build + the relevant test set run after each constituent Task; full suite run at least once before Epic Review. No Task starts before the previous one's own completion. Stop only on a Risk Ceiling breach or one of the eight Stop Conditions (`AI_OPERATING_SYSTEM.md` §8) — otherwise continue autonomously through Task-712 per the Epic's approved default.
