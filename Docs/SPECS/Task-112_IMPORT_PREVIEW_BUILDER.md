# Task-112 Import Preview Builder

Version: 1.0
Status: Completed

Feature: Device Import

Milestone: M1 Enterprise Device Center

---

# Purpose

Build `ImportPreviewBuilder`.

It transforms:

`ImportPipelineResult`
-> `ImportPreviewResult`
-> `ImportPreviewRow`

The builder only performs model transformation.

---

# Architecture

`ImportPreviewBuilder` belongs to the Application Layer.

It is not:

- Presentation Layer
- Domain Layer
- Infrastructure Layer

---

# Files

Added:

- `VSP.Device/Import/Preview/ImportPreviewBuilder.cs`
- `VSP.Device/Import/Preview/ImportPreviewResult.cs`
- `VSP.Device/Import/Preview/ImportPreviewRow.cs`
- `VSP.Tests/Import/ImportPreviewBuilderTests.cs`

---

# Responsibilities

`ImportPreviewBuilder` only handles:

- Business model
-> Preview model

It does not:

- Parse CSV
- Validation
- Duplicate Check
- SQLite
- Repository
- Device Builder

---

# Preview Model

`ImportPreviewRow`

- `RowNumber`
- `Name`
- `Brand`
- `Model`
- `IPAddress`
- `Location`
- `IsValid`
- `Status`
- `Messages`

`Messages` directly reuses `ImportValidationMessage`.

`ImportPreviewResult`

- `IReadOnlyList<ImportPreviewRow> Rows`
- `TotalRows`
- `ValidRows`
- `InvalidRows`

`ImportPreviewRow` remains a plain data model and does not use:

- `ObservableCollection`
- `ICommand`
- `INotifyPropertyChanged`
- WPF types

`Status` currently outputs `Valid` / `Invalid`, but remains extensible for future states such as:

- `Warning`
- `Imported`
- `Skipped`
- `Updated`

---

# Scope

This task does not modify:

- Parser
- `ImportValidationEngine`
- `DuplicateChecker`
- UI
- SQLite
- Repository

---

# Acceptance Criteria

- Build Success
- Test Pass
- Error = 0
- Preview Builder completed
- Preview models completed
- Unit tests completed
- No parser changes
- No validation changes
- No duplicate changes
- No UI changes

---

# Suggested Commit

```bash
git commit -m "feat(import): add import preview builder"
```

---

# Implementation Summary

- Added `ImportPreviewBuilder`.
- Added `ImportPreviewResult`.
- Added `ImportPreviewRow`.
- `ImportPreviewRow` remains UI-independent and uses only plain data fields.
- Reused `ImportValidationMessage` directly without introducing a preview-specific message model.
- Added unit tests for empty result, single row, multiple rows, valid row, invalid row, duplicate row, messages mapping, summary count, row order, and null safety.
