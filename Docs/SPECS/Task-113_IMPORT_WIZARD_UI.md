# Task-113 Import Wizard UI

---

## Purpose

Build the first Import Wizard UI.

This Wizard will become the only UI entry point for future Device Import.

Current architecture:

CSV / Excel

↓

ImportPipelineService

↓

ImportPreviewBuilder

↓

ImportPreviewResult

↓

Import Wizard UI

The UI only consumes PreviewResult.

It does not execute business logic.

---

## Goals

The Wizard is responsible for:

- Selecting import file
- Calling ImportPipelineService
- Calling ImportPreviewBuilder
- Displaying preview
- Displaying summary
- Preparing user for Import

No database write in this task.

---

## Scope

### Included

- Import Wizard Window
- File selection
- Preview display
- Summary display
- Import button (disabled or Not Implemented)
- Cancel button

### Excluded

- CSV parsing
- Excel parsing
- Validation
- Duplicate checking
- SQLite Import
- Repository
- DeviceBuilder
- Progress dialog
- Rollback

---

## Wizard Flow

Step 1

Select File

↓

Step 2

Run:

ImportPipelineService

↓

ImportPreviewBuilder

↓

ImportPreviewResult

↓

Display Preview

↓

Step 3

Ready to Import

No actual import.

---

## UI Layout

Top:

File selector

Browse Button

Refresh Button

Middle:

Preview Grid

Columns:

- Row Number
- Name
- Brand
- Model
- IP Address
- Location
- Status
- Messages

Bottom:

Summary

- Total Rows
- Valid Rows
- Invalid Rows

Buttons

- Import
- Cancel

Import remains disabled or displays:

Not implemented.

---

## Responsibilities

The Wizard only:

- Select file
- Execute ImportPipelineService
- Execute ImportPreviewBuilder
- Display ImportPreviewResult

The Wizard must NOT:

- Parse CSV
- Parse Excel
- Validate
- Duplicate Check
- Build CameraEntity
- Write SQLite

---

## Reuse

Reuse directly:

- ImportPipelineService
- ImportPreviewBuilder
- ImportPreviewResult
- ImportPreviewRow
- ImportValidationMessage

Do not duplicate models.

---

## Error Handling

Display friendly message when:

- Empty file
- Unsupported extension
- Parser exception
- Pipeline exception

No application crash.

---

## UI Principle

Keep UI simple.

No optimization.

No paging.

No sorting.

No filtering.

No color theme work.

No performance optimization.

Only verify the Import pipeline.

---

## Unit Tests

At minimum:

- Empty preview
- Single row preview
- Multiple rows preview
- Invalid rows preview
- Duplicate rows preview
- Summary counts
- Empty file
- Invalid extension
- Exception handling
- Cancel closes window

---

## Out of Scope

Not included:

- SQLite Import
- Progress
- Rollback
- Import History
- Device Update
- Conflict Resolution

These belong to future tasks.

---

## Future Tasks

Task-114

SQLite Import

Task-115

Import Progress

Task-116

Import Summary

Task-117

Rollback

Task-118

Import History