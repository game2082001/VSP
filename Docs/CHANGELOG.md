# CHANGELOG

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

