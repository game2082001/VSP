# Task-111F Duplicate Checker

Version: 1.0  
Status: Completed  
Feature: Device Import  
Milestone: M1 Enterprise Device Center

---

# Purpose

Build the Device Import `DuplicateChecker`.

This task checks in-memory duplicate values after `ValidationEngine` and before Preview / Import.

Pipeline:

Parser
-> ValidationEngine
-> DuplicateChecker
-> Preview / Import

---

# Scope

Duplicate rules implemented in this task:

- Name
- IP Address
- RTSP URL

Not included in this task:

- Name + IP Address composite duplicate
- SQLite duplicate check
- Import Preview
- SQLite Import
- UI display
- Camera Entity creation
- Repository save

---

# Architecture

Location:

- `VSP.Device/Import/Validation/DuplicateChecker.cs`

Namespace:

- `VSP.Device.Import.Validation`

Input:

- `IReadOnlyList<ImportValidationResult>`

Output:

- `IReadOnlyList<ImportValidationResult>`

The checker does not modify `ValidationEngine` core logic.

It reuses existing validation models:

- `ImportValidationResult`
- `ImportValidationMessage`
- `ImportValidationSeverity`

---

# Duplicate Rules

For all duplicate rules in this task:

- Comparison is case-insensitive
- Values are trimmed before comparison
- Empty / whitespace values are ignored
- Duplicate messages are added as `Error`

Stable error codes:

- `DuplicateName`
- `DuplicateIpAddress`
- `DuplicateRtspUrl`

Message format:

- `<FieldName> is duplicated with row(s): <row list>.`

---

# Files

Added:

- `VSP.Device/Import/Validation/DuplicateChecker.cs`
- `VSP.Tests/Import/ImportDuplicateCheckerTests.cs`

Modified:

- `Docs/03_ROADMAP.md`
- `Docs/CHANGELOG.md`
- Current task spec

---

# Forbidden Changes

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
- Camera Entity

---

# Acceptance Criteria

- Build Success
- Test Pass
- Error = 0
- Duplicate Checker completed
- Duplicate unit tests completed
- No parser changes
- No UI changes
- No SQLite changes
- No Repository changes
- No DeviceService changes
- No Driver Framework changes

---

# Suggested Commit

```bash
git commit -m "feat(import): add duplicate checker"
```

---

# Implementation Summary

- Added `DuplicateChecker` to the existing Validation layer.
- Reused `ImportValidationResult`, `ImportValidationMessage`, and `ImportValidationSeverity`.
- Implemented duplicate rules for `Name`, `IP Address`, and `RTSP URL`.
- Comparison is case-insensitive, trims whitespace, and ignores empty values.
- Duplicate checker preserves original `ImportValidationResult` data and appends duplicate error messages.
- Added unit tests for non-duplicate, field duplicates, case-insensitive matching, trim handling, empty value handling, multiple duplicate rows, row number preservation, and message correctness.
- No parser, UI, SQLite, Repository, DeviceService, Driver Framework, ImportWizard, or Camera Entity changes were made.
