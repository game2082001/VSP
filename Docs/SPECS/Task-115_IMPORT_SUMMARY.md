# Task-115 Import Summary

Version: 1.0

Status: Completed

Feature: Device Import

Milestone: M1 Device Import MVP

---

# 1. Purpose

完成 Import 後的 Summary 視窗。

本 Task 負責將 ImportResult 轉換成使用者可閱讀的完成資訊。

Import Summary 不負責任何 Import Logic。

---

# 2. Why

目前 Import 已可：

CSV / Excel

↓

Preview

↓

SQLite

但使用者完成後沒有完整回饋。

本 Task 建立 Import Completion UI。

---

# 3. Architecture

ImportExecutor

↓

ImportResult

↓

ImportSummaryViewModel

↓

ImportSummaryWindow

UI 永遠不直接解析 Repository 或 SQLite。

---

# 4. New Files

建議新增：

VSP.UI/ViewModels/ImportSummaryViewModel.cs

VSP.UI/Views/ImportSummaryWindow.xaml

VSP.UI/Views/ImportSummaryWindow.xaml.cs

VSP.Tests/Import/ImportSummaryViewModelTests.cs

---

# 5. Responsibilities

ImportSummaryViewModel 只負責：

- 顯示 Summary
- 顯示 Error Count
- 顯示 Error List
- Close Command

不得：

- 執行 Import
- 呼叫 Repository
- 呼叫 SQLite
- Retry
- Rollback

---

# 6. UI Layout

Summary

- Total
- Imported
- Skipped
- Failed

Error List

Columns：

- Row
- Name
- Message

Bottom：

Close Button

---

# 7. Reuse

直接使用：

ImportResult

ImportError

不得重新建立 Summary Model。

---

# 8. Error Handling

若沒有 Error：

Error List 保持空白。

不得：

MessageBox。

---

# 9. Out of Scope

本 Task 不包含：

- Retry
- Rollback
- History
- Export
- Progress
- Batch Import

全部留至 M2。

---

# 10. Unit Tests

至少包含：

- Empty Result
- Success Result
- Partial Failure
- Full Failure
- Error List
- Close Command

---

# 11. Acceptance Criteria

完成後：

- Build Success
- Test Pass
- Error = 0
- Summary 可正常顯示
- Error List 可正常顯示
- 不修改 ImportExecutor

---

# 12. Design Decisions

Decision 1

Summary 直接使用 ImportResult。

Reason：

避免建立第二份 Summary Model。

---------------

Decision 2

ImportSummaryWindow 只負責呈現。

Reason：

UI 不放 Business Logic。

---

# 13. Documentation

更新：

- CHANGELOG.md
- 03_ROADMAP.md
- 本 Spec

---

# 14. Suggested Commit

git commit -m "feat(import): add import summary"

---

# 15. Implementation Result

- Build Success
- Test Pass
- Import Summary displays execution ImportResult only
- Error List displays ImportError rows directly
- Close workflow uses RequestClose event from ViewModel and Close() in code-behind
