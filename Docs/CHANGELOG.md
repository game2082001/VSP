# CHANGELOG

## 2026-07-25

### Version 1.8 - Epic-003 RTSP Connection Foundation

Status:
Implementation Complete — Reviewed — Accepted by Product Owner (uncommitted — pending user commit)

Summary:
- Implemented `RtspCameraDriver.TestConnection()`: connects to the camera's exact configured `Camera.RtspUrl`, sends an RTSP DESCRIBE request, and treats any final 2xx status as success.
- On a 401 challenge, parses the `WWW-Authenticate` header (`RtspWwwAuthenticateParser`) and retries exactly once with a computed Basic or Digest (MD5, `qop=auth` and no-qop) `Authorization` header (`RtspAuthorizationHeaderBuilder`); a second 401, malformed response, invalid URL, timeout, connection failure, or unsupported challenge scheme all return `false` without throwing past the `TestConnection` boundary.
- Added `TcpRtspTransport` for the underlying socket I/O: bounded connect/read timeouts, accumulation of partial TCP reads, `\r\n\r\n` header-termination detection, and a 16 KB max response size cap; connections and streams are always disposed.
- Added `RtspDescribeRequestFactory` / `RtspDescribeResponseParser` as small protocol-focused helpers for building the DESCRIBE request and parsing the status line/headers.
- Enabled RTSP in `DeviceCenterViewModel.IsDriverImplemented` (single-line flag flip; no other UI logic changed).
- Added 34 new unit tests (`VSP.Tests/Drivers/RTSP/`) covering auth flows (Basic/Digest, single-retry-only), malformed/timeout/invalid-URL/unsupported-challenge cases, and transport-level behavior, using a bounded, self-disposing `LoopbackRtspTestServer` loopback helper (background thread; cannot hang the test process).
- Reviewed against scope (RTSP/TestConnection only — no Snapshot/SETUP/PLAY/Streaming/ONVIF/Hikvision/Dahua, no Driver Framework or Discovery changes, no new external dependencies), functional correctness, network robustness, and test quality. Accepted by Product Owner with two non-blocking follow-ups recorded below.

Files:
- VSP.Device/Drivers/RTSP/RtspCameraDriver.cs
- VSP.Device/Drivers/RTSP/RtspAuthorizationHeaderBuilder.cs
- VSP.Device/Drivers/RTSP/RtspDescribeRequestFactory.cs
- VSP.Device/Drivers/RTSP/RtspDescribeResponseParser.cs
- VSP.Device/Drivers/RTSP/RtspWwwAuthenticateParser.cs
- VSP.Device/Drivers/RTSP/TcpRtspTransport.cs
- VSP.UI/ViewModels/DeviceCenterViewModel.cs
- VSP.Tests/Drivers/RTSP/LoopbackRtspTestServer.cs
- VSP.Tests/Drivers/RTSP/RtspAuthorizationHeaderBuilderTests.cs
- VSP.Tests/Drivers/RTSP/RtspCameraDriverTests.cs
- VSP.Tests/Drivers/RTSP/RtspDescribeRequestFactoryTests.cs
- VSP.Tests/Drivers/RTSP/RtspDescribeResponseParserTests.cs
- VSP.Tests/Drivers/RTSP/RtspWwwAuthenticateParserTests.cs
- VSP.Tests/Drivers/RTSP/TcpRtspTransportTests.cs
- Docs/CHANGELOG.md

Technical Debt:
- TD-027 `TcpRtspTransport` overall operation timeout
  Reason: The current implementation enforces a per-read timeout (`NetworkStream.ReadTimeout`, reset on every `Read()` call) but not an overall deadline for the whole DESCRIBE round trip, so a server that trickles bytes just under the per-read timeout could hold the connection open indefinitely. Accepted as non-blocking for Epic-003.
- TD-028 Additional RTSP transport robustness tests
  Reason: Future enhancement to cover fragmented/multi-chunk header reads and max-response-size-cap enforcement (currently only the "server never responds" hang case is tested, not "server responds forever without a `\r\n\r\n` terminator"). Accepted as non-blocking for Epic-003.

Known documentation debt (not fixed — out of confirmed scope for this Epic):
- Docs/PROJECT_STATUS.md remains stale (predates this Epic; still shows TD-001/TD-002 from the M1 release and does not reflect the current TD-027/TD-028 numbering used in this CHANGELOG).
- No formal Epic definition document exists for Epic-003 satisfying every field required by `AUTONOMOUS_DEVELOPMENT.md` §2 (Epic ID, Objective, Scope Boundary, Risk Ceiling, Constituent Tasks, Definition of Done, Approval Record) — consistent with the same known gap recorded for Epic-002 above.

---

### Version 1.7 - Epic-002 Device Management Continuation (Task-213–216)

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- Completed Task-213 Batch Edit: multi-select checkbox column on the camera list, a "Batch Edit" dialog applying Brand/Location/Username/Password to 2+ selected cameras via looped `ICameraRepository.Update()`. This Task's implementation was already present in the working tree at Epic resume time; this entry is its first CHANGELOG record.
- Completed Task-214 Batch Connection Test: a "Batch Test" action reusing the Driver Framework via a new `ICameraConnectionTester` service, showing per-camera Success/Failed results in a dialog. The service, dialog ViewModel/View, and `CameraListItemViewModel.IsSelected` plumbing already existed in the working tree at Epic resume time; this Task completed the missing piece — wiring `BatchConnectionTestCommand`/`RequestBatchConnectionTest` into `CameraListViewModel`/`CameraListView`, and adding the missing `BatchConnectionTestViewModelTests`.
- Added Task-215 Export: an "Export" action on the camera list, enabled whenever the current filtered view is non-empty, writing a CSV using the same column layout as `CsvImportParser` (round-trip compatible with Import) via a native Save File dialog.
- Added Task-216 Device Status Enhancement: `BatchConnectionTestViewModel` now persists each tested camera's `Status` (Online/Offline) via `ICameraRepository.Update()`, and `CameraListView` refreshes the list after the Batch Test dialog closes so the Status column reflects real connectivity instead of a permanent `Offline` default.
- Task-215 and Task-216 had no prior Task Specification; both were drafted directly as implementation artifacts of this already-approved Epic (Implementation Authority, `AI_OPERATING_SYSTEM.md` §22) and are included in this entry.

Files:
- VSP.UI/ViewModels/CameraListItemViewModel.cs
- VSP.UI/ViewModels/CameraListViewModel.cs
- VSP.UI/ViewModels/BatchEditViewModel.cs
- VSP.UI/Views/BatchEditWindow.xaml / .xaml.cs
- VSP.UI/Views/CameraListView.xaml / .xaml.cs
- VSP.Device/Services/ICameraConnectionTester.cs
- VSP.Device/Services/CameraConnectionTester.cs
- VSP.Device/Services/CameraConnectionTestResult.cs
- VSP.UI/ViewModels/BatchConnectionTestViewModel.cs
- VSP.UI/ViewModels/BatchConnectionTestItemViewModel.cs
- VSP.UI/Views/BatchConnectionTestWindow.xaml / .xaml.cs
- VSP.Device/Export/CameraExportWriter.cs
- VSP.UI/Helpers/ExportFileSelector.cs
- VSP.Tests/Camera/BatchEditViewModelTests.cs
- VSP.Tests/Camera/CameraListViewModelBatchSelectionTests.cs
- VSP.Tests/Camera/BatchConnectionTestViewModelTests.cs
- VSP.Tests/Export/CameraExportWriterTests.cs
- Docs/SPECS/Task-213_BATCH_EDIT.md
- Docs/SPECS/Task-214_BATCH_CONNECTION_TEST.md
- Docs/SPECS/Task-215_EXPORT.md
- Docs/SPECS/Task-216_DEVICE_STATUS_ENHANCEMENT.md
- Docs/03_PRODUCT_ROADMAP.md
- Docs/CHANGELOG.md

Known documentation debt (found during this Epic's Current-State Analysis, not fixed — out of confirmed scope):
- Docs/03_ROADMAP.md contains pre-existing mojibake (not UTF-8-clean Chinese text, predates this Epic) and uses a different Task/Epic numbering scheme (EPIC-01/Task-101...) than the actively-maintained Docs/03_PRODUCT_ROADMAP.md (Task-2xx). Only 03_PRODUCT_ROADMAP.md was updated by this entry, to avoid risking further corruption of 03_ROADMAP.md's encoding.
- Docs/PROJECT_STATUS.md is stale (predates the Discovery Epic and this Device Management continuation; still shows 88 tests and "Next Milestone: Device Management").
- No formal Epic definition document exists for Epic-002 satisfying every field required by `AUTONOMOUS_DEVELOPMENT.md` §2 (Epic ID, Objective, Scope Boundary, Risk Ceiling, Constituent Tasks, Definition of Done, Approval Record) — the Task-213/214 spec headers only informally reference "Epic-002 (EPIC-01 Device Management continuation)". This continuation proceeded on the basis that the user's current, explicit instruction is the highest-authority source per `AI_OPERATING_SYSTEM.md` §1.

---

### Version 1.6 - Epic Discovery Foundation (Task-601 fix, Task-602–607)

Status:
Completed (uncommitted — pending user commit)

Summary:
- Fixed Task-601 `DiscoveryRunner` to match its approved spec: removed the `DiscoverySessionFactory`/`IDiscoverySessionSink` dependency that had been embedded directly in its constructor (a scope violation caught in review), and introduced `IDiscoveryRunner` so future hooks decorate the runner from the outside instead of adding dependencies to it.
- Added Task-602 Progress Hook: `ProgressPublishingDiscoveryRunner` publishes a start and a terminal `DiscoveryProgress` around an execution.
- Added Task-603 Session Hook: `SessionRecordingDiscoveryRunner` records a `DiscoverySession` per execution via `DiscoverySessionFactory` — properly re-implementing, as an opt-in decorator, the capability removed from `DiscoveryRunner` in the Task-601 fix.
- Added Task-604 Retry Hook: `RetryingDiscoveryRunner` retries a `Failed` result or a non-cancellation exception up to a configured attempt count with a fixed delay; never retries `Cancelled` or `InvalidRequest` outcomes or `OperationCanceledException`.
- Added Task-605 Timeout Hook: `TimeoutDiscoveryRunner` enforces a per-execution operation timeout distinct from caller cancellation, raising `DiscoveryTimeoutException` rather than adding a `TimedOut` value to `DiscoveryOrchestrationStatus` (explicitly disallowed by Task-505 §5).
- Added Task-606 Metrics Hook: `MetricsRecordingDiscoveryRunner` records a minimal `DiscoveryMetricsSample` (status, duration, correlation id) per execution, no external metrics package.
- Added Task-607 Diagnostics Hook: `DiagnosticsRecordingDiscoveryRunner` publishes a `DiscoveryDiagnosticsSnapshot` (diagnostic id, timestamp, correlation id, status, reasons) per execution.
- Every hook is an independent `IDiscoveryRunner` decorator; none adds a dependency to `DiscoveryRunner` or `DiscoveryOrchestrator` itself.

Files:
- VSP.Device/Discovery/Execution/IDiscoveryRunner.cs
- VSP.Device/Discovery/Execution/DiscoveryRunner.cs
- VSP.Device/Discovery/Execution/ProgressPublishingDiscoveryRunner.cs
- VSP.Device/Discovery/Progress/IDiscoveryProgressPublisher.cs
- VSP.Device/Discovery/Progress/NoOpDiscoveryProgressPublisher.cs
- VSP.Device/Discovery/Execution/SessionRecordingDiscoveryRunner.cs
- VSP.Device/Discovery/Sessions/IDiscoverySessionSink.cs
- VSP.Device/Discovery/Sessions/NoOpDiscoverySessionSink.cs
- VSP.Device/Discovery/Execution/RetryingDiscoveryRunner.cs
- VSP.Device/Discovery/Execution/DiscoveryRetryPolicy.cs
- VSP.Device/Discovery/Execution/TimeoutDiscoveryRunner.cs
- VSP.Device/Discovery/Execution/DiscoveryTimeoutPolicy.cs
- VSP.Device/Discovery/Execution/DiscoveryTimeoutException.cs
- VSP.Device/Discovery/Execution/MetricsRecordingDiscoveryRunner.cs
- VSP.Device/Discovery/Metrics/DiscoveryMetricsSample.cs
- VSP.Device/Discovery/Metrics/IDiscoveryMetricsSink.cs
- VSP.Device/Discovery/Metrics/NoOpDiscoveryMetricsSink.cs
- VSP.Device/Discovery/Execution/DiagnosticsRecordingDiscoveryRunner.cs
- VSP.Device/Discovery/Diagnostics/DiscoveryDiagnosticsSnapshot.cs
- VSP.Device/Discovery/Diagnostics/IDiscoveryDiagnosticsSink.cs
- VSP.Device/Discovery/Diagnostics/NoOpDiscoveryDiagnosticsSink.cs
- VSP.Tests/Discovery/DiscoveryRunnerTests.cs
- VSP.Tests/Discovery/ProgressPublishingDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/SessionRecordingDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/RetryingDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/TimeoutDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/MetricsRecordingDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/DiagnosticsRecordingDiscoveryRunnerTests.cs
- Docs/SPECS/Task-602_DISCOVERY_PROGRESS_HOOK.md
- Docs/SPECS/Task-603_DISCOVERY_SESSION_HOOK.md
- Docs/SPECS/Task-604_DISCOVERY_RETRY_HOOK.md
- Docs/SPECS/Task-605_DISCOVERY_TIMEOUT_HOOK.md
- Docs/SPECS/Task-606_DISCOVERY_METRICS_HOOK.md
- Docs/SPECS/Task-607_DISCOVERY_DIAGNOSTICS_HOOK.md
- Docs/CHANGELOG.md

Known documentation debt (found during this Epic's Current-State Analysis, not fixed — out of confirmed scope):
- Docs/03_PRODUCT_ROADMAP.md's Discovery entry (Version 1.3) is stale and does not reflect Task-402–607.
- Docs/PROJECT_STATUS.md is stale (predates this entire body of Discovery work).
- No ADR exists yet for the Discovery subsystem's architecture.
- Task-402 through Task-601 were never individually logged in this CHANGELOG; this entry only covers the Task-601 fix and Task-602–607.

---

## 2026-07-13

### Version 1.3 - Task-401 ONVIF Discovery

Status:
Completed

Summary:
- Added the first ONVIF Discovery foundation with WS-Discovery Probe message building, response parsing, and discovery orchestration.
- Added a minimal transport boundary so discovery logic can be unit tested without real multicast or ONVIF devices.
- Implemented deterministic deduplication using EndpointReference, normalized XAddr, and remote sender IP fallback.
- Implemented timeout and explicit cancellation semantics without adding UI, SQLite, repository, or camera-creation logic.

Files:
- VSP.Device/Discovery/Onvif/OnvifDiscoveryRequest.cs
- VSP.Device/Discovery/Onvif/OnvifDiscoveryResult.cs
- VSP.Device/Discovery/Onvif/WsDiscoveryTransportMessage.cs
- VSP.Device/Discovery/Onvif/IWsDiscoveryTransport.cs
- VSP.Device/Discovery/Onvif/OnvifWsDiscoveryProbeBuilder.cs
- VSP.Device/Discovery/Onvif/OnvifWsDiscoveryResponseParser.cs
- VSP.Device/Discovery/Onvif/UdpWsDiscoveryTransport.cs
- VSP.Device/Discovery/Onvif/OnvifDiscoveryService.cs
- VSP.Tests/Discovery/OnvifWsDiscoveryResponseParserTests.cs
- VSP.Tests/Discovery/OnvifDiscoveryServiceTests.cs
- Docs/03_PRODUCT_ROADMAP.md

---

## 2026-07-13

### Version 1.2 - Task-303 Driver Settings

Status:
Completed

Summary:
- Added immutable Driver Settings metadata models for driver setting keys, field definitions, and per-driver settings definitions.
- Extended DriverDescriptor to optionally carry Driver Settings metadata without changing driver runtime interfaces.
- Added conservative built-in settings definitions for Hikvision ISAPI, Dahua NetSDK, ONVIF, and RTSP drivers.
- Kept actual per-device values in Camera and did not add UI, SQLite, repository, or JSON settings changes.

Files:
- VSP.Device/Drivers/Settings/DriverSettingKey.cs
- VSP.Device/Drivers/Settings/DriverSettingDefinition.cs
- VSP.Device/Drivers/Settings/DriverSettingsDefinition.cs
- VSP.Device/Drivers/DriverDescriptor.cs
- VSP.Device/Drivers/Plugins/BuiltInCameraDriverPlugin.cs
- VSP.Tests/Drivers/DriverSettingsTests.cs
- Docs/03_PRODUCT_ROADMAP.md

---

## 2026-07-13

### Version 1.2 - Task-302 Driver Plugin

Status:
Completed

Summary:
- Added a minimal IDriverPlugin contract for in-process driver extension.
- Added BuiltInCameraDriverPlugin as the single source of truth for built-in driver descriptors.
- Added atomic plugin registration through DriverRegistry.RegisterPlugin(...).
- Preserved DriverFactory static API and RTSP fallback behavior.
- No DLL loading, reflection scanning, plugin folders, or settings were introduced.

Files:
- VSP.Device/Drivers/Plugins/IDriverPlugin.cs
- VSP.Device/Drivers/Plugins/BuiltInCameraDriverPlugin.cs
- VSP.Device/Drivers/DriverRegistry.cs
- VSP.Tests/Drivers/DriverPluginTests.cs
- Docs/03_PRODUCT_ROADMAP.md

---
?祆?隞嗉???VSP 撠???閬??質??氬?
---

# Version 2.0

---

## Sprint 1

### S1-1 Device List
?交?嚗?026-06-28

#### ?啣?
- DeviceCenter ?∠ DeviceCenterViewModel??- Device List ?寧?? DeviceService.GetAllCameras() 頛 SQLite Camera 鞈???- 撌血 Device List 雿輻 ListBox??- 摰? Devices ??SelectedDevice Binding??- ?啣? RefreshCommand嚗?頛 Camera 皜??- ?啣? DeviceCount 憿舐內??
#### UI
- 撌血憿舐內嚗?  - Camera Name
  - Brand
  - IP Address
  - Connection Type
- ?喳 Device Editor 靽? Placeholder??
#### ?嗆?
- 蝚血? MVVM??- ViewModel 銝?亙???SQLite??- 蝬 DeviceService ??鞈???- ?芯耨??MainWindow??- ?芯耨??Repository??- ?芯耨??SQLite Schema??- ?芯耨??Legacy DeviceView??
#### Build
- Build Success
- Error嚗?
- Warning嚗U1903嚗QLite 憟辣摰?扯郎??

---
## Sprint 1

### S1-2 Device Detail

Status:
Completed

Summary:
- Device Detail now binds directly to SelectedDevice.
- Display fields:
  - Name
  - Brand
  - Model
  - IP Address
  - Connection Type
- No fake properties added.
- No placeholder values added.
- Repository / SQLite / Driver Framework unchanged.

Files:
- DeviceCenterView.xaml

Reviewed:
2026-06-28

-----
# Changelog

---

## 2026-06-28

### Sprint 1 - Task 3
### Device Center - Add Device

Status:
Completed

Summary:

- 摰? Device Center Add Device 瘚?
- Add Device ??撌脩?摰?AddDeviceCommand
- 雿輻?Ｘ? AddDeviceWindow嚗??啣??啗?蝒?- Save 敺? DeviceService.AddCamera() 撖怠 SQLite
- ?啣?摰?敺??啗???Device List
- ?芸??詨??憓? Camera
- Device Detail ?郊憿舐內?啣?鞈?

Architecture:

- 蝬剜? MVVM
- ViewModel 銝?亙???SQLite
- Repository Pattern 銝?
- SQLite Schema ?∩耨??- Driver Framework ?∩耨??
Not Included:

- Edit
- Delete
- Search
- Filter
- Connection Test
- Real-time Validation

Verified:

??Add Device
??SQLite Save
??Refresh Device List
??Detail Binding

---
## 2026-06-28

### Sprint 1 - Task 4
### Device Center - Edit Device

Status:
Completed

Summary:

- 摰? Device Center Edit Device 瘚?
- ?啣? Edit Device ??
- Edit Device 雿輻?Ｘ? AddDeviceWindow(Camera)
- 閬??芸?頛?桀? Camera 鞈?
- Save 敺? DeviceService.UpdateCamera() ?湔 SQLite
- ?湔摰??頛 Device List
- ?芸???詨?靽格敺?Camera
- Device Detail ?郊?湔

Architecture:

- 蝬剜? MVVM
- ViewModel 銝?交?雿?SQLite
- Repository Pattern 銝?
- SQLite Schema ?∩耨??- Driver Framework ?∩耨??
Not Included:

- Delete Device
- Search
- Filter
- Connection Test
- Real-time Validation

Verified:

??Edit Device
??SQLite Update
??Reload Device List
??Auto Select Updated Camera
??Device Detail Refresh

---

## 2026-06-28

### S1-5 Delete Device

摰? Device ?芷瘚???
?啣?嚗?
- DeleteDeviceCommand
- Delete 蝣箄?撠店獢?- DeviceService.DeleteCamera()
- ?芷敺???LoadDevices()
- ?芸??湔 SelectedDevice
- Device Detail ?郊?瑟

?萄?嚗?
- 銝耨??Architecture
- 銝耨??Repository
- 銝耨??SQLite Schema
- 銝憓?DeleteView
- 銝憓?DeleteDialog
- 銝憓?DeleteService

----
# Changelog

---

## [Unreleased]

### Added

#### Sprint 1 - S1-6 Search Device

摰? DeviceCenter ?????
?批捆嚗?
- ?啣? Search TextBox
- ?啣? Search Button
- ?啣? Clear Button
- SearchKeyword ?單???嚗extChanged嚗?- ?舀 Name ??
- ?舀 IP ??
- ?舀 Brand ??
- ?舀 Model ??
- ??憭批?撖思????- 雿輻閮擃???(_allDevices) ?脰?蝭拚
- ?啣? ApplySearch()
- 靽??桀? SelectedDevice
- ?⊥?撠???憿舐內 No matching devices found.
- Clear 敺敺拙???Device List

Architecture嚗?
- 銝耨??Repository
- 銝耨??SQLite Schema
- 銝耨??Driver Framework
- 銝憓?SQL Query
- Search ? ViewModel 閮擃???Filter

# Changelog

## [Unreleased]

### Added

#### S1-7 Filter Device

- ?啣? Device Brand Filter嚗ll + CameraBrand嚗?- ?啣? Connection Filter嚗ll + DeviceConnectionType嚗?- Filter ??Search ?梁??憟??園?蝭拚瘚?
- Search ?寧 Filter 敺銵?- 銝??唳閰?SQLite
- ?啣? BrandOptions ??ConnectionOptions
- ?啣? SelectedBrand?electedConnection
- Clear Search ???身 Search?rand Filter?onnection Filter
- ?∩耨??Repository
- ?∩耨??SQLite Schema
- ?∩耨??Driver Framework
- Build Success嚗? Error / 7 Existing Warnings嚗?
---
## [v0.2] - 2026-06-30

### Added
- S1-8 Connection Test completed.
- Added Connection Test button in Device Center.
- Connected Device Center to existing DriverFactory workflow.
- Test now calls IDeviceDriver.TestConnection(Camera).
- Displays Connection Success / Connection Failed / Driver not implemented.

---
## [v0.2] - 2026-06-30

### Added
- S1-9 Device Validation completed.
- Added validation before calling DeviceService.
- Required field validation for Name, IP Address, Username and Connection Type.
- Added IPv4 validation.
- Added HTTP / SDK / RTSP Port range validation.
- Added RTSP URL validation for RTSP devices.
- Save is blocked when validation fails.

----
## [Unreleased]

### Added
- S1-10 Realtime Validation
  - Added realtime validation for Add/Edit Device dialog.
  - Save button is enabled only when all required fields are valid.
  - Validation messages are displayed below each invalid field.
  - Invalid controls are highlighted immediately while typing.
  - Existing S1-9 final validation before save is retained.

  ---
  ## Unreleased

### Added

- Task-111A Import Framework
  - 撱箇? ImportService
  - 撱箇? IImportParser
  - 撱箇? ImportRow
  - 撱箇? ImportResult
  - 撱箇? ImportWizard Skeleton

- Task-111B CSV Parser
  - ?啣? CsvImportParser
  - ?舀 UTF8 / Big5
  - ?舀 quoted field
  - ?舀 Header Parsing

- Task-111C Excel Parser
  - ?啣? ExcelImportParser
  - ?∠ ClosedXML
  - ?舀 xlsx
  - 蝚砌???Worksheet
  - Header Parsing

- Task-111D Parser Unit Test
  - Added VSP.Tests
  - Added CsvImportParserTests
  - Added ExcelImportParserTests
  - Added CSV parser tests for supported file types, header parsing, row mapping, quoted field, comma-in-quoted-field, empty row skip
  - Added CSV encoding tests for UTF-8, UTF-8 BOM, UTF-8 without BOM, and Big5
  - Added Excel parser tests for first worksheet parsing, header parsing, row mapping, blank cell handling, and empty row skip
  - No production parser code changed

- Task-111E Validation Engine
  - Added ImportValidationEngine
  - Added ImportValidationResult with original ImportRow reference
  - Added shared ImportValidationMessage model
  - Added ImportValidationSeverity enum
  - Added validation rules for required fields, IPv4, HTTP / RTSP / SDK port range, RTSP URL required, and rtsp:// prefix
  - Added ImportValidationEngineTests
  - No parser, UI, SQLite, Repository, DeviceService, Driver Framework, ImportWizard, or Camera Entity changes

- Task-111F Duplicate Checker
  - Added DuplicateChecker to the Validation layer
  - Reused ImportValidationResult, ImportValidationMessage, and ImportValidationSeverity
  - Added duplicate rules for Name, IP Address, and RTSP URL
  - Duplicate comparison is case-insensitive, trims whitespace, and ignores empty values
  - Duplicate checker appends duplicate error messages and preserves existing validation messages
  - Added ImportDuplicateCheckerTests
  - No parser, UI, SQLite, Repository, DeviceService, Driver Framework, ImportWizard, or Camera Entity changes

- Task-111G Import Pipeline Service
  - Added ImportPipelineService as the single import pipeline entry point
  - Added ImportPipelineResult with Results, TotalRows, ValidRows, and InvalidRows
  - Added a lightweight parser selection helper to isolate parser selection from orchestration
  - Reused CsvImportParser, ExcelImportParser, ImportValidationEngine, and DuplicateChecker
  - Added ImportPipelineServiceTests for CSV, Excel, validation stage, duplicate stage, empty file, unsupported file type, and parser exception reporting
  - No parser, UI, SQLite, Repository, DeviceService, Driver Framework, ImportWizard, MainWindow, or DeviceCenter changes

- Task-112 Import Preview Builder
  - Added ImportPreviewBuilder
  - Added ImportPreviewResult
  - Added ImportPreviewRow
  - ImportPreviewRow remains UI-independent and uses plain data fields only
  - Reused ImportValidationMessage directly without adding a preview-specific message model
  - Added ImportPreviewBuilderTests for empty result, single row, multiple rows, valid row, invalid row, duplicate row, messages mapping, summary count, row order, and null safety
  - No parser, validation engine, duplicate checker, UI, SQLite, or Repository changes

- Task-113 Import Wizard UI
  - Updated ImportWizard to browse import files and display preview results
  - Injected ImportPipelineService and ImportPreviewBuilder into ImportWizardViewModel through constructor parameters
  - Added ImportFileSelector helper to isolate file dialog usage from the ViewModel
  - Added preview summary fields for total, valid, and invalid rows
  - Added preview grid columns for row number, device fields, status, and validation messages
  - Added Refresh support to reload the currently selected file without browsing again
  - Added ImportWizardViewModelTests covering empty preview, preview display, summary counts, refresh behavior, cancel, invalid file type, and exception handling
  - No parser, validation, duplicate, SQLite, Repository, DeviceService, Driver Framework, or MainWindow changes

- Task-114 SQLite Import
  - Added ImportExecutor to orchestrate ImportPreviewResult -> CameraImportMapper -> ICameraRepository -> ImportResult
  - Added CameraImportMapper to map ImportPreviewRow into Camera entities without repository or UI logic
  - Added ImportResult and ImportError models for import execution summary and error collection
  - Reused the existing ICameraRepository abstraction for import execution
  - Updated CameraRepository to delegate to SQLiteCameraRepository for repository-backed imports
  - Connected ImportWizard Import button to execution flow and simple status updates
  - Added ImportExecutorTests for empty import, multiple rows, skipped invalid rows, partial failure, repository exception, and error collection
  - Updated ImportWizardViewModelTests to cover import command enablement and import status
  - No parser, validation engine, duplicate checker, import pipeline service, import preview builder, SQLite schema, driver framework, or DeviceService changes

- Task-115 Import Summary
  - Added ImportSummaryViewModel to display execution ImportResult data and expose a RequestClose event
  - Added ImportSummaryWindow with summary counts, error list, and a Close button
  - Connected ImportWizard to open ImportSummaryWindow after import completion
  - Reused ImportResult and ImportError directly from the execution layer without creating a new summary model
  - Added ImportSummaryViewModelTests for success, partial failure, full failure, empty result, error list, and close command
  - Updated ImportWizardViewModelTests to verify ImportCompleted event is raised
  - Corrected one existing ImportExecutor test case so skipped and failed rows are both exercised
  - No parser, validation, duplicate, pipeline, preview builder, import executor, or repository changes

- Task-201 Camera List
  - Added CameraQueryService to wrap the existing ICameraRepository read flow
  - Added CameraListViewModel and CameraListItemViewModel for read-only camera display
  - Added standalone CameraListView with a read-only DataGrid for Name, IP Address, Brand, Status, and Location
  - Reused the existing repository contract without changing sync CRUD methods
  - Added CameraListViewModelTests for empty repository, multiple cameras, repository exception, and mapping
  - Did not modify MainWindow, Import flow, SQLite schema, or Driver Framework

- Task-202 Camera Management Toolbar
  - Added toolbar layout to CameraListView with Search, Clear, Brand, Status, Refresh, and Add Camera controls
  - Added bottom status bar showing total camera count and the current status message
  - Added placeholder toolbar bindings in CameraListViewModel without introducing search, filter, refresh, or add business logic
  - Preserved the existing Camera List load behavior from Task-201
  - Added ViewModel tests for toolbar skeleton state and placeholder commands
  - Did not modify CameraQueryService, Repository, SQLite, Import flow, or MainWindow

- Task-203 Camera Search
  - Implemented Camera Search in CameraListViewModel using SearchCommand and ClearCommand
  - Search scope is limited to Camera Name and IP Address
  - Clear restores the full list, clears SearchKeyword, and updates total count and status message
  - Search continues to use ICameraRepository.GetAll() through CameraQueryService, followed by LINQ filtering in ViewModel
  - No Repository.Search() or SQLite changes were introduced
  - Added unit tests for name search, IP search, excluded fields, blank keyword restore, and clear behavior

- Task-204 Camera Detail
  - Added read-only CameraDetailWindow and CameraDetailViewModel
  - Camera detail opens by double-clicking a row in CameraListView
  - Reused the already loaded camera data without adding repository query paths
  - Displayed camera fields including ports, credentials, RTSP URL, status, recording, location, and timestamps
  - Masked password display in Camera Detail
  - Added Close button and a disabled Edit button placeholder only
  - No Repository, SQLite, Import flow, MainWindow, or Driver Framework changes
  - Added CameraDetailViewModelTests for field mapping, masking, close flow, and null-safe handling

- Task-205 Camera Filter
  - Implemented Brand and Status filter in CameraListViewModel
  - Search and Filter now share the same ApplyFilters() pipeline
  - Filter scope is limited to Brand (All, Hikvision, Dahua, VIVOTEK) and Status (All, Online, Offline)
  - Clear resets SearchKeyword, SelectedBrand, and SelectedStatus without reloading data
  - Filtering continues to use _allCameras + LINQ without Repository or SQLite changes
  - Added unit tests for brand filter, status filter, composed search/filter, clear reset, and selected item clearing

- Task-206 Camera Edit
  - Added edit mode to CameraDetailViewModel and CameraDetailWindow
  - Editable fields now support validation without Repository or SQLite persistence
  - Read-only fields remain locked in the detail view
  - Added ApplyEditCommand as a validation-only placeholder apply flow
  - PasswordBox code-behind only synchronizes password into the ViewModel
  - Added validation for required Name, IPv4 IP Address, and HTTP / RTSP / SDK port range
  - Added unit tests for edit mode, validation, apply flow, and close behavior

- Task-207 Camera Save Persistence
  - Renamed ApplyEditCommand to SaveCommand and connected Camera Detail save flow to ICameraRepository.Update()
  - Save flow now validates, maps ViewModel data to Camera, calls Repository.Update(), refreshes LastModifyTime, and updates StatusMessage
  - Save success displays "Camera saved successfully." and keeps the detail window open
  - Save failure catches repository exceptions, updates StatusMessage, and avoids crashing
  - Added unit tests for save success, validation blocking, repository exception handling, LastModifyTime refresh, and repository call count
  - Technical Debt: TD-017 Unsaved changes detection before closing the window

- Task-208 Add Camera
  - Reused Camera Detail window for New Mode and added explicit new camera defaults for Brand, ConnectionType, Status, Recording, and ports
  - Connected Add Camera flow to ICameraRepository.Add() with existing validation and repository exception handling
  - Add success now closes the detail window, refreshes Camera List, and reselects the newly added camera when visible
  - Added unit tests for New Mode defaults, add success, add failure, add command event routing, and refresh selection behavior
  - Technical Debt: TD-021 Duplicate camera detection before Add()

- Task-210 Unsaved Changes Detection
  - Added dirty tracking for Camera Detail in both Edit Mode and New Mode
  - Close flow now requests confirmation only when unsaved changes exist, while unchanged forms close immediately
  - Save from unsaved-changes confirmation now closes only after successful persistence; discard closes without saving; cancel keeps the current edits
  - Kept confirmation dialog handling in CameraDetailWindow.xaml.cs so the ViewModel does not call MessageBox directly
  - Added unit tests for dirty state changes, save clearing dirty state, and close flows for save, discard, and cancel
  - Technical Debt: TD-022 Shared confirmation dialog component

- Task-211 Camera Refresh / Reload
  - Added a dedicated refresh reload flow that always re-reads repository data instead of relying on the initial load guard
  - Refresh now preserves SearchKeyword, Brand Filter, Status Filter, and restores SelectedCamera by Camera.Id after ApplyFilters()
  - Refresh success ends with "Camera list refreshed." and refresh failure ends with "Failed to refresh camera list."
  - Refresh failure now keeps the current visible list instead of clearing it unnecessarily
  - Added unit tests for repository reload, preserved search and filters, selection restore and clear behavior, and exception handling
  - Technical Debt: TD-026 Background refresh / auto refresh

- Task-212 Camera Delete
  - Added Delete command to Camera Detail for persisted cameras only, with confirmation handled in the View layer
  - Delete now calls ICameraRepository.Delete(camera.Id), closes Camera Detail on success, and refreshes Camera List using the existing Task-211 refresh flow
  - Delete confirmation remains separate from unsaved-changes handling, so explicit delete does not trigger Save / Discard / Cancel close flow
  - Delete failure keeps the detail window open, preserves current edited values, and updates StatusMessage without crashing
  - Added unit tests for delete confirmation request, delete success, cancel, failure handling, and unsaved-changes interaction
  - Technical Debt: TD-025 Shared confirmation dialog component/service

- Task-301 Driver Registry
  - Added immutable DriverDescriptor for driver metadata and factory delegate registration
  - Added DriverRegistry as an instance-based registry with explicit duplicate rejection for DriverId and DeviceConnectionType
  - Updated DriverFactory to use a default DriverRegistry instance internally while preserving the existing RTSP fallback behavior
  - Added unit tests for descriptor validation, registry registration and lookup, duplicate handling, built-in driver registration, and DriverFactory fallback compatibility

