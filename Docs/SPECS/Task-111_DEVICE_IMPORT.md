# Task-111 Device Import

Version: 1.0

Status

Planned

Priority

High

Epic

Device Management

Feature

Device Center

---

# 1. Objective

建立 Device Import Wizard。

支援：

- CSV
- Excel (.xlsx)

將設備批次匯入 Device Center。

Import 必須經過 Validation、Duplicate Check 與 Preview。

不得直接寫入 SQLite。

---

# 2. Scope

包含：

- CSV Import
- Excel Import
- Preview
- Validation
- Duplicate Check
- Import Summary
- Error Report

不包含：

- Device Export（Task-112）
- Device Discovery
- Auto Scan

---

# 3. UI

新增：

Device Import Wizard

Step 1

Select File

↓

Step 2

Preview

↓

Step 3

Validation

↓

Step 4

Import

↓

Step 5

Summary

---

# 4. Excel Format

Template：

| Name | Brand | Model | IP Address | HTTP Port | RTSP Port | SDK Port | Username | Password | Connection Type | RTSP URL | Location |

第一列固定 Header。

---

# 5. Import Flow

Select File

↓

Read File

↓

Parse

↓

Validation

↓

Duplicate Check

↓

Preview

↓

Import

↓

Summary

---

# 6. Validation

共用：

DeviceValidationService

不得重新建立 Validation。

檢查：

- Required
- IPv4
- Port
- RTSP URL

---

# 7. Duplicate Check

檢查：

- IP Address
- Device Name

策略：

Skip

Replace

Rename

Cancel

由使用者決定。

---

# 8. Preview

顯示：

Row

Status

Reason

Example

Row 15

Warning

Duplicate IP

Row 22

Error

Invalid IPv4

不得直接寫入 SQLite。

---

# 9. Import Summary

完成後：

Total

Imported

Skipped

Failed

並可：

Export Error Report

---

# 10. Architecture

View

↓

ViewModel

↓

ImportService

↓

ValidationService

↓

Repository

↓

SQLite

ImportService 不得直接操作 UI。

---

# 11. Files

新增：

ImportWizard.xaml

ImportWizardViewModel.cs

ImportService.cs

ImportResult.cs

ImportSummary.cs

不得修改：

MainWindow

Repository

SQLite Schema

Driver Framework

---

# 12. Acceptance

✓ CSV

✓ Excel

✓ Preview

✓ Validation

✓ Duplicate Check

✓ Summary

✓ Cancel

✓ Build Success

✓ 0 Error

---

# 13. Out of Scope

Device Export

Cloud Import

Auto Discovery

REST API

---

Suggested Commit

feat(device-center): complete Task-111 Device Import