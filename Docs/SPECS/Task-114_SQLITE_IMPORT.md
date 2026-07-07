# Task-114 SQLite Import

Version: 1.0

Status: Completed

Feature: Device Import

Milestone: M1 Device Import MVP

---

# 1. Purpose

完成第一版 Device Import。

本 Task 負責將使用者於 Import Wizard 確認的 Preview 資料，轉換成 Device Entity，並透過 Repository 寫入 SQLite。

完成後，使用者可完成：

Select File

↓

Preview

↓

Import

↓

SQLite

---

# 2. Why

目前已完成：

- Parser
- Validation Engine
- Duplicate Checker
- Import Pipeline
- Import Preview Builder
- Import Wizard UI

尚缺真正的 Import Execute。

本 Task 完成後，M1 將具備完整 Import 能力。

---

# 3. Architecture

Import Wizard

↓

ImportExecutor

↓

CameraImportMapper

↓

ICameraRepository

↓

SQLite

↓

ImportResult

Import Wizard 不直接操作 Repository。

Import Wizard 不直接操作 SQLite。

---

# 4. New Files

建議新增：

VSP.Device/Import/Execution/

- ImportExecutor.cs
- CameraImportMapper.cs
- ImportResult.cs
- ImportError.cs

VSP.Tests/Import/

- ImportExecutorTests.cs

---

# 5. Responsibilities

ImportExecutor 只負責：

- Execute Import
- 呼叫 DeviceBuilder
- 呼叫 Repository
- 收集 ImportResult

ImportExecutor 不負責：

- CSV Parser
- Excel Parser
- Validation
- Duplicate Check
- UI
- SQLite SQL
- MessageBox

---

# 6. Camera Import Mapper

Preview Model 不可直接寫入 Repository。

流程必須：

ImportPreviewRow

↓

CameraImportMapper

↓

Device Entity

↓

Repository

避免 UI Model 與 Domain Entity 混用。

---

# 7. Repository

ImportExecutor 只能呼叫：

ICameraRepository

例如：

- Insert(Device)
- Update(Device)

ImportExecutor 不得直接操作 SQLite。

---

# 8. Import Result

ImportResult 至少包含：

- TotalRows
- ImportedRows
- SkippedRows
- FailedRows
- IReadOnlyList<ImportError> Errors

ImportResult 將提供：

- Import Summary
- Import History
- Future Log Viewer

共用。

---

# 9. Error Handling

Import 不得因單一資料錯誤造成 Application Crash。

必須：

- 收集 Error
- 回傳 ImportResult

不得直接 MessageBox。

---

# 10. Allowed Modifications

允許修改：

- ImportWizardViewModel
- Repository
- SQLite Layer

若有必要。

---

# 11. Forbidden

不得修改：

- CsvImportParser
- ExcelImportParser
- ValidationEngine
- DuplicateChecker
- ImportPipelineService
- ImportPreviewBuilder

除非重大 Bug。

---

# 12. Unit Tests

至少包含：

- Empty Import
- Single Device
- Multiple Devices
- Import Success
- Import Failure
- Partial Failure
- Repository Exception
- ImportResult Summary
- Error Collection

---

# 13. Acceptance Criteria

完成後：

- Build Success
- Test Pass
- Error = 0
- Wizard 可完成 Import
- SQLite 可寫入
- ImportResult 完整
- Unit Tests 完成

---

# 14. Design Decisions

Decision 1

ImportExecutor 不直接操作 SQLite。

Reason：

降低耦合。

---------------

Decision 2

Preview Model 不直接寫入 Repository。

Reason：

避免 UI Model 成為 Domain Entity。

---------------

Decision 3

ImportResult 為共用 Result Model。

Reason：

Summary、History、Log 共用。

---

# 15. Out of Scope

本 Task 不包含：

- Progress Dialog
- Rollback
- Import History
- Log Viewer
- Conflict Resolver
- Batch Update

留至 M2。

---

# 16. Documentation

更新：

- Docs/CHANGELOG.md
- Docs/03_ROADMAP.md
- 本 Spec

並提供：

- Build Result
- Test Result
- Risk Report
- Next Suggested Task
- Suggested Git Commit

---

# 17. Suggested Commit

git commit -m "feat(import): implement sqlite import"

---

# 18. Implementation Result

- Build Success
- Test Pass
- Import Wizard can execute import through ImportExecutor
- Import execution uses ICameraRepository abstraction
- Import result provides imported / skipped / failed summary with error collection
