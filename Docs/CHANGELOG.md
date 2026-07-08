# CHANGELOG

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

