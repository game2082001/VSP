# VSP Development Guide

Version: 1.0

Last Updated: 2026-06-30

---

# 1. Purpose

本文件定義 VSP 的開發規範。

所有新功能、Bug 修正、重構與版本發布皆應遵循本文件。

---

# 2. Development Workflow

所有功能皆遵循下列流程：

Requirement

↓

Specification (Spec)

↓

Task Plan

↓

Architecture Review

↓

Implementation

↓

Build

↓

Code Review

↓

Testing

↓

Documentation Update

↓

Git Commit

不得跳過任何步驟。

---

# 3. Project Structure

VSP

├── Domain

├── Application

├── Infrastructure

├── Driver

├── UI

├── Resources

├── Docs

└── Tests

每個資料夾均有單一責任。

不得跨 Layer。

---

# 4. Architecture Rules

Architecture：

View

↓

ViewModel

↓

Application Service

↓

Repository

↓

SQLite

Driver：

View

↓

ViewModel

↓

Application Service

↓

Driver Factory

↓

IDeviceDriver

↓

Vendor Driver

↓

SDK / Protocol

禁止：

ViewModel → SQLite

ViewModel → SDK

Repository → Driver

View → Business Logic

---

# 5. MVVM Rules

View

僅負責：

- UI
- Binding
- Style
- Animation

不得：

- SQL
- SDK
- Business Logic

ViewModel

負責：

- Command
- ObservableProperty
- Application Service 呼叫

不得：

- SQLite
- Vendor SDK
- MessageBox（除非 UI Framework 限制）

---

# 6. Repository Rules

Repository：

僅負責：

- CRUD
- SQL
- Transaction

不得：

- Validation
- Driver
- Network

---

# 7. Driver Rules

所有設備均透過：

DriverFactory

↓

IDeviceDriver

↓

Vendor Driver

新增品牌時：

只新增 Driver。

不得修改 Device Center。

---

# 8. Validation Rules

Validation 必須可重複使用。

例如：

DeviceValidationService

↓

Add Device

↓

Edit Device

↓

Import Device

↓

Batch Edit

不得重複撰寫相同 Validation。

---

# 9. UI Rules

所有 UI 必須共用 Style。

包含：

- Button
- ComboBox
- TextBox
- Dialog
- ListView
- DataGrid

不得自行建立不同風格。

---

# 10. Naming Rules

Class

PascalCase

Property

PascalCase

Method

PascalCase

Private Field

_camelCase

Interface

IXXXX

Async Method

XXXXAsync

Boolean

IsXXX

CanXXX

HasXXX

---

# 11. Git Rules

Commit 格式：

feat(module): description

fix(module): description

refactor(module): description

docs: description

test: description

例如：

feat(device-center): complete S1-11 Device Import

fix(driver): fix hikvision login timeout

docs: update roadmap

---

# 12. Documentation Rules

每完成一個 Sprint 必須更新：

Docs/CHANGELOG.md

Docs/03_ROADMAP.md

Docs/SPECS/

不得省略。

---

# 13. Build Rules

每次 Build：

必須：

Build Success

Error：

0

Warning：

必須確認來源。

---

# 14. Code Review Checklist

每次 Review 必須確認：

□ Build Success

□ 0 Error

□ Warning 已確認

□ 符合 Spec

□ 未修改 Scope 外內容

□ 未破壞 Architecture

□ MVVM 正確

□ Repository 正確

□ Driver 正確

□ 文件更新

□ Git Commit

---

# 15. Sprint Completion Checklist

每個 Sprint 必須完成：

✓ Spec

✓ Task Plan

✓ Coding

✓ Build

✓ Review

✓ Testing

✓ CHANGELOG

✓ ROADMAP

✓ SPEC Completion

✓ Git Commit

---

# 16. Technical Debt Policy

禁止：

為了趕功能而破壞 Architecture。

若需暫時實作：

必須建立 TODO 並加入 Roadmap。

不得永久保留 Hack Code。

---

# 17. Future Expansion

VSP 必須支援：

Camera

NVR

Door Controller

AI Box

Decoder

Display

IoT Device

CMS

任何新功能皆不得限制未來擴充。

---

# Revision History

| Version | Date | Summary |
|----------|------------|------------------------------|
| 1.0 | 2026-06-30 | 建立 Development Guide |