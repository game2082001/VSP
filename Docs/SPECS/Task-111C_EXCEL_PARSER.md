# Task-111C Excel Parser

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

建立 Excel Import Parser。

本 Task 僅負責將 .xlsx 檔案解析成 ImportRow 集合。

不得寫入 SQLite。

不得做 Preview UI。

不得做 Validation。

不得做 Duplicate Check。

---

# Scope

## Included

- ExcelImportParser
- Implement IImportParser
- Parse .xlsx stream
- Read first worksheet
- Read Header row
- Map each Excel row into ImportRow
- Empty row skip

## Not Included

- CSV Parser 修改
- Import Preview
- Validation
- Duplicate Check
- SQLite Write
- Repository change
- DeviceService change
- UI flow

---

# Excel Format

First row must be Header.

Required headers:

| Name | Brand | Model | IP Address | HTTP Port | RTSP Port | SDK Port | Username | Password | Connection Type | RTSP URL | Location |

Header names are case-insensitive.

---

# Parser Contract

ExcelImportParser must implement:

IImportParser

Methods:

- ParserName
- CanParse(string fileType)
- Parse(Stream stream)

---

# Output

Parser output:

IEnumerable<ImportRow>

ImportRow must contain:

- RowNumber
- Values

Values key should use header name.

Example:

Values["Name"]

Values["IP Address"]

Values["Connection Type"]

---

# Architecture

ImportService

↓

IImportParser

↓

ExcelImportParser

↓

ImportRow

ExcelImportParser must not depend on UI.

ExcelImportParser must not write SQLite.

ExcelImportParser must not create Camera entity.

---

# Package

Preferred:

ClosedXML

If package is not installed:

- Add ClosedXML only to the project that contains ExcelImportParser.
- Do not add unrelated packages.

---

# Files

Expected New

- VSP.Device/Import/ExcelImportParser.cs

Allowed Modify

- VSP.Device.csproj only if ClosedXML package reference is required.

Do NOT Modify

- MainWindow
- DeviceCenter
- Repository
- SQLite Schema
- DeviceService
- Driver Framework
- ImportWizard UI
- CsvImportParser.cs

---

# Acceptance

✓ ExcelImportParser created

✓ Implements IImportParser

✓ CanParse supports:

- xlsx
- .xlsx
- application/vnd.openxmlformats-officedocument.spreadsheetml.sheet

✓ Parses first worksheet

✓ Parses header row

✓ Creates ImportRow per data row

✓ Preserves RowNumber

✓ Skips empty rows

✓ Does not write SQLite

✓ Does not create Camera

✓ Build Success

✓ 0 Error

---

# Suggested Commit

feat(import): add excel import parser