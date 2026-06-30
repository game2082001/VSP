# Task-111B CSV Parser

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

建立 CSV Import Parser。

本 Task 僅負責將 CSV 檔案解析成 ImportRow 集合。

不得寫入 SQLite。

不得做 Preview UI。

不得做 Excel Parser。

---

# Scope

## Included

- CsvImportParser
- Implement IImportParser
- Parse CSV stream
- Read Header row
- Map each CSV row into ImportRow
- Support UTF-8
- Support Big5 / Default Encoding fallback if feasible
- Basic CSV quoted field support
- Empty row skip

## Not Included

- Excel Parser
- Import Preview
- Validation
- Duplicate Check
- SQLite Write
- Repository change
- DeviceService change

---

# CSV Format

First row must be Header.

Required headers:

| Name | Brand | Model | IP Address | HTTP Port | RTSP Port | SDK Port | Username | Password | Connection Type | RTSP URL | Location |

Header names are case-insensitive.

---

# Parser Contract

CsvImportParser must implement:

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

CsvImportParser

↓

ImportRow

CsvImportParser must not depend on UI.

CsvImportParser must not write SQLite.

CsvImportParser must not create Camera entity.

---

# Files

Expected New

- VSP.Device/Import/CsvImportParser.cs

Expected Modify

- None

Allowed Modify

- ImportService.cs only if needed for parser registration or parser support

Do NOT Modify

- MainWindow
- DeviceCenter
- Repository
- SQLite Schema
- DeviceService
- Driver Framework
- ImportWizard UI

---

# Acceptance

✓ CsvImportParser created

✓ Implements IImportParser

✓ CanParse supports:

- csv
- .csv
- text/csv

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

feat(import): add csv import parser