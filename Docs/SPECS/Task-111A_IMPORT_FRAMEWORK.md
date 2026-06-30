# Task-111A Import Framework

Version: 1.0

Status

Planned

Priority

High

Epic

EPIC-01 Device Management

Feature

Device Import

---

# Goal

建立 Device Import Framework。

本 Task 僅建立 Import 架構。

不得開始解析 CSV 或 Excel。

---

# Objectives

建立 Import Pipeline：

Import Wizard

↓

Import Service

↓

Parser Interface

↓

Validation

↓

Repository

↓

SQLite

Import Service 不得直接操作 UI。

---

# Scope

## Included

- Import Framework
- Import Wizard Skeleton
- IImportParser
- ImportContext
- ImportResult
- ImportSummary
- Dependency Flow

## Not Included

- CSV Parser
- Excel Parser
- Duplicate Check
- Preview
- Execute Import
- Export

---

# Architecture

Presentation

↓

ImportWizard

↓

ImportWizardViewModel

↓

ImportService

↓

IImportParser

↓

ValidationService

↓

Repository

↓

SQLite

---

# New Classes

ImportService

負責：

- 協調 Import 流程
- 呼叫 Parser
- 呼叫 Validation
- 回傳 Result

不得：

- 操作 UI
- 操作 SQLite

---

IImportParser

介面：

Parse(Stream stream)

↓

ImportRow Collection

未來：

CsvImportParser

ExcelImportParser

JsonImportParser

皆實作此介面。

---

ImportContext

包含：

- FileName
- FileType
- ImportOption
- DuplicatePolicy

---

ImportResult

包含：

- TotalRows
- SuccessRows
- FailedRows
- WarningRows

---

ImportSummary

提供：

Import 完成資訊。

---

ImportRow

建立中立資料模型。

不得直接使用 Camera。

---

# Files

New

Application/Import/

ImportService.cs

IImportParser.cs

ImportContext.cs

ImportResult.cs

ImportSummary.cs

ImportRow.cs

UI/

ImportWizard.xaml

ImportWizardViewModel.cs

---

# Do NOT Modify

MainWindow

Repository

SQLite Schema

Driver Framework

DeviceService

Legacy Device Center

---

# Acceptance

✓ Import Framework 建立

✓ Parser Interface 建立

✓ ImportRow 建立

✓ ImportResult 建立

✓ ImportSummary 建立

✓ Build Success

✓ 0 Error

---

# Suggested Commit

feat(import): create import framework