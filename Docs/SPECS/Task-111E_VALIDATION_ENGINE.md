# Task-111E Validation Engine

Version: 1.0  
Status: Completed  
Feature: Device Import  
Milestone: M1 Enterprise Device Center

---

# Purpose

Build the Device Import Validation Engine.

This task validates `ImportRow` data produced by parser steps before any UI preview or SQLite import.

Flow:

Parser
-> ImportRow
-> Validation Engine
-> Import Validation Result

---

# Validation Rules

- Name required
- Brand required
- IP Address required
- IP Address must be valid IPv4
- HTTP Port must be between 1 and 65535
- RTSP Port must be between 1 and 65535
- SDK Port must be between 1 and 65535
- Username required
- Connection Type required
- When Brand = RTSP, RTSP URL is required
- RTSP URL must start with `rtsp://`

---

# Result Model

Validation returns structured results instead of throwing validation exceptions.

Each result includes:

- Original `ImportRow`
- `RowNumber`
- `IsValid`
- `Messages`

Each validation message includes:

- `RowNumber`
- `FieldName`
- `Code`
- `Message`
- `Severity`

Severity values:

- Error
- Warning
- Info

Stable error codes used in this task:

- Required
- InvalidIpAddress
- InvalidPort
- InvalidRtspUrl

---

# Files

Added:

- `VSP.Device/Import/Validation/ImportValidationEngine.cs`
- `VSP.Device/Import/Validation/ImportValidationResult.cs`
- `VSP.Device/Import/Validation/ImportValidationMessage.cs`
- `VSP.Device/Import/Validation/ImportValidationSeverity.cs`
- `VSP.Tests/Import/ImportValidationEngineTests.cs`

Modified:

- None required

---

# Out Of Scope

This task does not include:

- Duplicate Check
- Import Preview
- SQLite Import
- UI display
- Batch Import
- Camera Entity creation
- Repository save

This task does not modify:

- MainWindow
- DeviceCenter
- Repository
- SQLite Schema
- DeviceService
- Driver Framework
- ImportWizard
- CsvImportParser
- ExcelImportParser

---

# Acceptance Criteria

- Build Success
- Test Pass
- Error = 0
- Validation Engine completed
- Validation Unit Tests completed
- No parser changes
- No UI changes
- No SQLite changes
- No Repository changes

---

# Documentation Update

Codex updated:

- `Docs/CHANGELOG.md`
- `Docs/03_ROADMAP.md`
- Current task spec

Reported with:

- Build Result
- Test Result
- Risk Report
- Next Suggested Task
- Suggested Git Commit

---

# Suggested Commit

```bash
git commit -m "feat(import): add validation engine"
```

---

# Implementation Summary

- Added `ImportValidationEngine`.
- Added `ImportValidationResult` with original `ImportRow` reference.
- Added shared `ImportValidationMessage` model.
- Added `ImportValidationSeverity` enum.
- Added unit tests for required fields, IPv4, port range, RTSP URL rules, valid row handling, multi-row row number retention, and non-throwing validation behavior.
- No parser, UI, SQLite, Repository, DeviceService, Driver Framework, ImportWizard, or Camera Entity changes were made.
