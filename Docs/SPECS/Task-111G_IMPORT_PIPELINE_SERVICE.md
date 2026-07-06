# Task-111G Import Pipeline Service

Version: 1.0  
Status: Completed  
Feature: Device Import  
Milestone: M1 Enterprise Device Center

---

# Purpose

Build `ImportPipelineService` as the single entry point for future Import Wizard workflows.

Current pipeline:

Parser
-> ValidationEngine
-> DuplicateChecker

Future stages may include:

- DatabaseDuplicateChecker
- PreviewBuilder
- ImportExecutor
- SQLite Import

This task only orchestrates the existing stages.

---

# Files

Added:

- `VSP.Device/Import/ImportPipelineService.cs`
- `VSP.Device/Import/ImportPipelineResult.cs`
- `VSP.Tests/Import/ImportPipelineServiceTests.cs`

---

# Design

Input:

- `Stream`
- `FileType`

Output:

- `ImportPipelineResult`

`ImportPipelineResult` includes:

- `IReadOnlyList<ImportValidationResult> Results`
- `int TotalRows`
- `int ValidRows`
- `int InvalidRows`

`TotalRows` always represents the number of parsed rows before later validation or duplicate failures.

`ImportPipelineService` responsibilities:

- Select parser through a lightweight parser selection helper
- Execute parser
- Execute `ImportValidationEngine`
- Execute `DuplicateChecker`
- Aggregate final counts

It does not:

- Define validation rules
- Define duplicate rules
- Parse CSV / Excel directly
- Write to SQLite
- Build UI / preview models
- Create Camera entities

---

# Scope

This task does not modify:

- `CsvImportParser`
- `ExcelImportParser`
- `ImportValidationEngine`
- `DuplicateChecker`
- Import Wizard
- Preview
- UI
- SQLite
- Repository
- DeviceService
- Driver Framework
- MainWindow
- DeviceCenter

---

# Acceptance Criteria

- Build Success
- Test Pass
- Error = 0
- CSV pipeline covered
- Excel pipeline covered
- Validation stage execution verified
- Duplicate stage execution verified
- Invalid rows preserved
- Duplicate messages preserved
- `TotalRows` / `ValidRows` / `InvalidRows` verified
- Empty file handled
- Unsupported file type reported
- Parser exception reported

---

# Suggested Commit

```bash
git commit -m "feat(import): add import pipeline service"
```

---

# Implementation Summary

- Added `ImportPipelineService` as the import pipeline orchestration entry point.
- Added `ImportPipelineResult` with `Results`, `TotalRows`, `ValidRows`, and `InvalidRows`.
- Added a lightweight parser selection helper inside `ImportPipelineService` to isolate parser selection from orchestration.
- Reused existing `CsvImportParser`, `ExcelImportParser`, `ImportValidationEngine`, and `DuplicateChecker`.
- Added unit tests covering CSV pipeline, Excel pipeline, validation stage, duplicate stage, empty file, unsupported file type, and parser exception handling.
