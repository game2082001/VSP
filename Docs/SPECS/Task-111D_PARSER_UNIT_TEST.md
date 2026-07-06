# Task-111D Parser Unit Test

?嚗?.0  
???Completed  
Feature嚗evice Import  
Milestone嚗1 Enterprise Device Center

---

# 銝???

撱箇? Import Parser ??Unit Test??

??Task ?芣葫閰佗?

- CsvImportParser
- ExcelImportParser
- IImportParser 頛詨銝?湔?

銝?靽格 Import Wizard??

銝?撖怠 SQLite??

銝?靽格 Repository??

---

# 鈭葫閰衣???

## CSV Parser

敹?皜祈岫嚗?

- CanParse ?舀 csv
- CanParse ?舀 .csv
- CanParse ?舀 text/csv
- Header 閫??
- RowNumber 甇?Ⅱ
- ImportRow Values 甇?Ⅱ
- 蝛箇???
- Quoted Field
- Comma inside quoted field
- Header 憭批?撖思???
- 憭?甈?銝??航炊
- 蝻箏?甈?銝? Exception

## Excel Parser

敹?皜祈岫嚗?

- CanParse ?舀 xlsx
- CanParse ?舀 .xlsx
- CanParse ?舀 Excel MIME type
- 霈?洵銝??Worksheet
- Header 閫??
- RowNumber 甇?Ⅱ
- ImportRow Values 甇?Ⅱ
- 蝛箇???
- 蝛箇 Cell 甇?Ⅱ??
- Header 憭批?撖思???

---

# 銝??

??Task 銝?嚗?

- Validation Engine
- Duplicate Check
- Import Preview
- SQLite Import
- Repository Test
- UI Test
- Import Wizard Flow

---

# ?葫閰行???

撱箄降雿輻嚗?

- xUnit

??Solution 撠??Test Project嚗?

?迂?啣?嚗?

- VSP.Tests

?仿??啣? NuGet嚗?

??閮梧?

- xunit
- xunit.runner.visualstudio
- Microsoft.NET.Test.Sdk

---

# 鈭??憓?獢?

?航?啣?嚗?

- VSP.Tests/VSP.Tests.csproj
- VSP.Tests/Import/CsvImportParserTests.cs
- VSP.Tests/Import/ExcelImportParserTests.cs

?亙歇?葫閰血?獢??蝙?冽?葫閰血?獢?

---

# ?准?敺耨??

銝?靽格嚗?

- MainWindow
- DeviceCenter
- Repository
- SQLite Schema
- DeviceService
- Driver Framework
- ImportWizard
- CsvImportParser
- ExcelImportParser

?日?皜祈岫?潛 Parser ?Ⅱ Bug嚗????嚗?敺?乩耨??

---

# 銝cceptance Criteria

摰?敺??泵??

- Build Success
- Test Pass
- Error = 0
- Parser Tests 撱箇?摰?
- CSV Parser 皜祈岫??
- Excel Parser 皜祈岫??
- 銝耨??Production Code
- 銝耨??UI
- 銝耨??SQLite
- 銝耨??Repository

---

# ?怒????辣?湔

Codex 敹??湔嚗?

- Docs/CHANGELOG.md
- Docs/03_ROADMAP.md
- ??Spec ???

銝行?靘?

- Build Result
- Test Result
- Risk Report
- Next Suggested Task
- Suggested Git Commit

---

# 銋uggested Commit

```bash
git commit -m "test(import): add parser unit tests"
```

---

# Implementation Summary

- Added VSP.Tests xUnit test project.
- Added CsvImportParserTests.
- Added ExcelImportParserTests.
- Added CSV parser coverage for supported file types, header parsing, row number, ImportRow values, quoted field, comma inside quoted field, empty row skip, UTF-8, UTF-8 BOM, UTF-8 without BOM, Big5, and null stream exception.
- Added Excel parser coverage for supported file types, first worksheet only, header parsing, row number, ImportRow values, empty row skip, blank cell handling, case-insensitive header normalization, and null stream exception.
- No production parser code changed.
- No UI / SQLite / Repository changes were made.

