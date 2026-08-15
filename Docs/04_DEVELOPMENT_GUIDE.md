# VSP Development Guide

版本：1.1

最後更新：2026-08-15

---

# 一、目的

本文件定義 VSP 的開發流程、Coding 流程、Review 流程與文件管理規範。

所有參與 VSP 開發的人員（包含 AI）皆須遵守。

---

# 二、開發原則

VSP 採用：

- Spec First
- Architecture First
- Review First
- Documentation First

Coding 永遠不是第一步。

任何新功能皆必須先完成：

Spec

↓

Task Plan

↓

Approval

↓

Coding

---

# 三、標準開發流程

每個 Task 必須依照以下流程：

```text
Read PROJECT.md
        ↓
Development Standard Updated?
        │
        ├── Yes
        │      ↓
        │ Read Core Documents
        │
        └── No
               ↓
Read Current Task Spec
        ↓
Task Plan
        ↓
User Approval
        ↓
Coding
        ↓
Self Build
        ↓
Self Check
        ↓
Documentation Update
        ↓
Diff
        ↓
Architecture Review
        ↓
Suggested Git Commit
        ↓
User Commit
        ↓
Task Completed
```

不得跳步。

---

# 四、Task Plan 規範

開始 Coding 前，必須提出 Task Plan。

至少包含：

1. 本次目標
2. 預計新增檔案
3. 預計修改檔案
4. 每個檔案用途
5. 是否修改 MainWindow
6. 是否修改 Repository
7. 是否修改 SQLite Schema
8. 是否修改 Driver Framework
9. 是否修改 DeviceService
10. 是否符合目前 Architecture
11. 預估影響範圍

未經 Approval，不得開始 Coding。

---

# 五、Coding Policy

Coding 必須遵守：

- 小步提交
- 不超出 Scope
- 不修改無關程式
- 保持可讀性
- 保持一致命名

禁止：

- 一次修改大量模組
- 偷加功能
- 偷改 Architecture
- 偷改 UI

---

# 六、Scope Protection

若 Task Spec 未提及：

不得修改：

- MainWindow
- DeviceCenter
- Repository
- SQLite Schema
- Driver Framework
- DeviceService

若確實需要修改：

流程：

```text
停止 Coding

↓

更新 Task Plan

↓

重新 Approval

↓

再開始 Coding
```

---

# 七、Build Policy

每個 Task 完成後：

必須：

- Build Success
- Error = 0

Warning：

若新增 Warning

必須說明原因。

Build 未成功：

不得交付。

---

# 八、Documentation Policy

Documentation 屬於 Implementation 的一部分。

每完成一個 Task，

Developer（Implementation Agent）必須自動更新：

- Docs/CHANGELOG.md
- Docs/03_ROADMAP.md
- Current Task Spec

若符合條件：

更新：

- Docs/PROJECT.md
- Docs/STATUS.md
- Docs/KNOWN_ISSUES.md

不得等待 User 提醒。

---

# 九、Documentation Ownership

Architect（目前預設工具：ChatGPT）：

負責：

- Spec
- Architecture
- Review

Developer（Implementation Agent，目前預設工具：Claude Code）：

負責：

- Coding
- Build
- Documentation Update
- Suggested Git Commit

User（Product Owner）：

負責：

- Approval
- Testing
- Git Commit（預設；例外見 §十四 Git Policy）

（註：以上角色為職能分工，非固定綁定特定工具，完整角色定義與目前預設工具指派以 `Docs/DEVELOPMENT_ROLES.md` 為準。Independent Review Agent（目前預設工具：Codex）負責獨立審查 Developer 的產出，僅於明確指派時執行實作，見 `AI/OperatingSystem/AI_OPERATING_SYSTEM.md` §27。）

---

# 十、Diff Policy

完成 Coding 後：

必須提供：

- 實際新增檔案
- 實際修改檔案
- 完整 Diff
- Build Result
- Error
- Warning
- 是否符合 Spec
- 是否修改 Scope 外內容

不得只說：

「完成了」。

---

# 十一、Review Policy

Review 必須包含：

Architecture

↓

MVVM

↓

Scope

↓

Code Quality

↓

Future Extension

↓

Documentation

↓

Git Commit

全部確認後，

Task 才可完成。

---

# 十二、Risk Report

每個 Task 完成後，

Developer 必須提供：

```text
Risk Report

目前風險：

...

尚未完成：

...

目前限制：

...

建議：

...

例如：
Risk Report

目前風險：

無

尚未完成：

Validation

Duplicate Check

SQLite Import

建議：

Task-111E Validation Engine

Risk Report 屬於 Task Summary 的一部分

---

# 十三、Task Completion Policy

Task 必須符合：

✅ Build Success

✅ Error = 0

✅ Warning 已說明

✅ Documentation Updated

✅ Diff 已提供

✅ Review Completed

✅ Suggested Git Commit 已提供

✅ User 完成 Commit

否則：

只能標示：

```text
Coding Completed

Waiting Review
```

不得標示：

```text
Task Completed
```

---

# 十四、Git Policy

Developer（Implementation Agent）預設不得：

- git add
- git commit
- git push

Developer 預設只能提供：

Suggested Commit：

例如：

```bash
git commit -m "feat(import): add csv import parser"
```

Git 操作預設全部由 User（Product Owner）執行。

例外：Product Owner 可針對單一 Task 明確授權 Developer 執行 staging/commit（Commit Gate），完整程序見 `AI/OperatingSystem/AI_OPERATING_SYSTEM.md` §23 Commit Gate，本文件不重複該程序。此例外不包含 push，push 永遠需要獨立、明確的另一次授權。唯讀 Git 指令（`git status` / `git diff` / `git log`）不受此限制，本文件 §十 Diff Policy 等既有流程所需的檢視操作維持可執行。

---
# 十五、Next Suggested Task

每個 Task 完成後，

Developer 必須主動提出：

- 下一個建議 Task
- 建議原因
- 預估修改檔案
- 是否需要新的 Spec

Developer 不得等待 User 詢問「下一步」。

---

# 十六、No Reminder Policy

Developer 不得等待 User 提醒：

例如：

- 更新 CHANGELOG
- 更新 ROADMAP
- 更新 Task Spec
- 提供 Git Commit
- 提供 Build Result

以上皆屬於 Task 的一部分。

應自動完成。

---

# 十七、Task Summary

每個 Task 完成後，

Developer 必須提供：

```text
==========================

Task Summary

==========================

Task

Status

Build

Error

Warning

Documentation

Architecture Review

Risk Report

Next Suggested Task

Suggested Git Commit

==========================

---

# 十八、Definition of Done

只有符合以下全部條件：

- Build Success
- Error = 0
- Documentation 完成
- Review 完成
- Git Commit 已提供
- User 完成 Commit

Task 才算真正完成。

---

# 十九、Version Policy

流程規範若有重大修改：

Version：

1.0

↓

1.1

↓

2.0

不得直接覆蓋既有規範。

所有變更皆須記錄於 CHANGELOG。

---

# 二十、開發理念

VSP 是一套商業產品。

開發優先順序：

Architecture

↓

Maintainability

↓

Scalability

↓

Quality

↓

Performance

↓

Development Speed

品質永遠優先於開發速度。

---

# 二十一、Development Standard Change Policy

Development Standard 更新時，

Developer 必須重新閱讀所有核心文件。

核心文件如下：

- PROJECT.md
- 01_ARCHITECTURE.md
- 02_CODING_RULES.md
- 03_ROADMAP.md
- 04_DEVELOPMENT_GUIDE.md
- DEVELOPMENT_ROLES.md

閱讀完成後，

Developer 必須：

- 確認已理解最新規範
- 說明本次更新重點
- 確認後續將依照最新規範開發

不得直接開始 Coding。

---

若僅為一般 Feature 或 Task 開發，

且 Development Standard 未更新，

Developer 不需重新閱讀全部文件。

僅需閱讀：

```text
PROJECT.md
        ↓
Current Task Spec