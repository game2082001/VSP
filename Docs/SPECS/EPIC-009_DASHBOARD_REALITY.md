# Epic-009 Dashboard Reality

Status: Approved (with refinements)
Feature: Dashboard
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

---

# Approval Record

- Proposed as "Dashboard Reality" — a data-only dashboard, explicitly excluding Live Video/Thumbnail/Video Decoder/Media Pipeline/Recording/Playback.
- Approved by the Product Owner with four refinements:
  1. `ConnectionType` counts must not be presented as Discovery activity or registration provenance (neither is persisted — verified during Current-State Analysis: no `DiscoverySession`/`RegistrationSource` data is ever retained). No Last Scan / Found Devices / Discovery-added counts.
  2. `Camera.Recording` must not be presented as a normal operational KPI (verified: never set `true` anywhere in production code). Omit or clearly label "Not implemented."
  3. Dashboard Reality v1's content is fixed to an explicit list (below) — nothing beyond it.
  4. `CameraDashboardSummaryBuilder` approved as a small, pure aggregation component. No schema changes, packages, charts, live thumbnails, recording, playback, or discovery persistence.
- Approved by: Product Owner (this conversation).
- Execution mode: Autonomous within this Epic, per `AUTONOMOUS_DEVELOPMENT.md` §7, until Epic Review or a defined Stop Condition (`AI_OPERATING_SYSTEM.md` §8).

---

# Objective

Replace the empty `DashboardView`/`DashboardViewModel` placeholder with a real, honest, read-only aggregation over already-existing Camera, Driver, and Connection data — nothing implied that isn't actually backed by retrievable data.

---

# Current-State Analysis Summary

(Full analysis produced before approval; summarized here for the record.)

- `DashboardViewModel` is a genuinely empty class; `DashboardView.xaml` is a placeholder label with no `DataContext` ever set by `MainWindowViewModel`.
- Camera data (`CameraQueryService`/`ICameraRepository.GetAll()`) and Driver data (`DriverRegistry.CreateDefault().GetAll()`, `DriverFactory.IsDriverImplemented`) are real, complete, and directly reusable.
- `Camera.Recording` is never set `true` anywhere in production code (only in a test fixture) — confirmed by repository-wide search.
- `DiscoverySession`/`DiscoveryMetricsSample`/`DiscoveryDiagnosticsSnapshot` are never created in practice: `VSP.UI` never uses `IDiscoveryRunner` (the shipped `CameraDiscoveryViewModel` calls `DiscoveryOrchestrator.DiscoverCandidatesAsync`/`RegisterCandidate` directly), and every sink implementation that exists (`NoOpDiscoverySessionSink`/`NoOpDiscoveryMetricsSink`/`NoOpDiscoveryDiagnosticsSink`) discards its input regardless.
- `RegistrationSource` is never persisted onto `Camera` — there is no way to know, after the fact, how a given camera was added.
- `Camera.Status` (Online/Offline/Connecting/Error) is real and persisted, but current-state-only — no history of past connection tests.

---

# Scope — Dashboard Reality v1 (fixed list, per Product Owner refinement #3)

- Total cameras
- Online / Offline / Unknown (Unknown = any `CameraStatus` other than `Online`/`Offline`, i.e. `Connecting`/`Error` today)
- Online rate
- Cameras by `ConnectionType` (neutral breakdown — not framed as Discovery activity)
- Cameras by `Brand`
- Implemented vs. unimplemented driver coverage (via `DriverFactory.IsDriverImplemented`)
- Recently added cameras (by `CreateTime`)
- Recently modified cameras (by `LastModifyTime`)
- Last dashboard refresh time
- Manual Refresh
- Load error state

`Camera.Recording` is omitted entirely — not part of the approved v1 list.

---

# Out of Scope

- Live Video, Live Thumbnail, Video Decoder, Media Pipeline, Recording, Playback.
- Any Discovery history, Last Scan, Found Devices, or Discovery-added counts (not persisted).
- Any registration-provenance breakdown (not persisted).
- Charts/graphs, any new package, any database schema change.

---

# Implementation Plan

1. `CameraDashboardSummary`/`CameraCategoryCount`/`CameraSummaryEntry` DTOs + `CameraDashboardSummaryBuilder` (static, pure) in `VSP.Device.Services`.
2. `DashboardViewModel` rewrite: composes `CameraQueryService`/`DriverRegistry.CreateDefault()`, `LoadAsync`/`RefreshAsync` with loading/error state and last-refreshed timestamp.
3. `DashboardView.xaml` rewrite: tile/list layout over the summary, matching existing card styling — no charts.
4. `DashboardView.xaml.cs`: parameterless constructor composing real dependencies, matching `CameraListView`/`CameraDiscoveryView` convention.
5. Unit tests: `CameraDashboardSummaryBuilderTests` (thorough pure-logic) + `DashboardViewModelTests` (load/refresh/error behavior).
6. Build + full suite, `CHANGELOG.md`, Epic Review.
