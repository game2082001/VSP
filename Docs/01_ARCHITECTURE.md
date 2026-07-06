# VSP Architecture

版本：1.0

最後更新：2026-07-01

---

# 一、目的

本文件定義 VSP 的整體系統架構。

所有新功能必須遵循本架構。

若 Architecture 有重大修改，必須更新本文件。

---

# 二、Architecture Goal

VSP 採用：

- Modular
- Layered Architecture
- MVVM
- Repository Pattern
- Service-Oriented Design

設計目標：

- 易於維護
- 易於測試
- 易於擴充
- 避免高耦合

---

# 三、Solution Structure

```text
VSP.sln

├── VSP.Domain
├── VSP.Device
├── VSP.Infrastructure
├── VSP.UI
├── VSP.Tests
└── Docs
```

---

# 四、Layer Architecture

```text
Presentation (UI)

↓

ViewModel

↓

Application Service

↓

Repository / Driver

↓

SQLite / Device SDK
```

依賴方向只能往下。

不得反向依賴。

---

# 五、Project Responsibilities

## VSP.UI

負責：

- View
- ViewModel
- Dialog
- Binding
- User Interaction

不得：

- SQL
- SDK
- Driver
- Business Logic

---

## VSP.Device

負責：

- DeviceService
- Import
- Driver
- Device Logic

包含：

```text
Import

Driver

Parser

Validation

Connection Test
```

---

## VSP.Infrastructure

負責：

- SQLite
- Repository
- File Storage
- Infrastructure

不得包含：

UI

Driver

Business Logic

---

## VSP.Domain

負責：

- Entity
- Enum
- Value Object

保持純淨。

---

## VSP.Tests

負責：

- Unit Test
- Integration Test
- Regression Test

不得包含正式程式。

---

# 六、Application Flow

```text
View

↓

ViewModel

↓

Application Service

↓

Repository

↓

SQLite
```

或：

```text
View

↓

ViewModel

↓

Application Service

↓

Driver

↓

Device SDK
```

UI 永遠不能直接操作：

- SQLite
- Repository
- Driver
- SDK

---

# 七、Import Framework

Import 採用統一流程：

```text
Import Wizard

↓

Import Service

↓

IImportParser

↓

ImportRow

↓

Validation

↓

Repository

↓

SQLite
```

目前完成：

- Import Framework
- CSV Parser
- Excel Parser

後續：

- Validation
- Duplicate Checker
- Import Preview
- SQLite Import

---

# 八、Driver Framework

Driver 採用統一介面：

```text
Driver Manager

↓

IDeviceDriver

↓

Vendor Driver

↓

SDK
```

未來支援：

- RTSP
- ONVIF
- Hikvision
- Dahua
- VIVOTEK
- Axis

所有 Driver 必須實作共同 Interface。

---

# 九、Repository Pattern

所有資料存取：

```text
Application

↓

Repository

↓

SQLite
```

禁止：

```text
ViewModel

↓

SQLite
```

禁止：

```text
UI

↓

Repository
```

---

# 十、Dependency Rules

允許：

```text
UI

↓

Device

↓

Infrastructure

↓

Domain
```

禁止：

```text
Infrastructure

↓

UI
```

禁止：

```text
Driver

↓

View
```

禁止：

```text
Repository

↓

Driver
```

---

# 十一、Future Modules

未來將加入：

```text
Playback

AI

Event Center

User

Permission

Plugin

Notification

Health Monitor

License

CMS

Mobile
```

所有模組皆須遵守相同 Layer。

---

# 十二、Architecture Principles

Architecture 優先順序：

1. Low Coupling
2. High Cohesion
3. Reusability
4. Testability
5. Maintainability
6. Scalability

禁止為了快速完成而破壞 Architecture。

Architecture 一旦變更，必須同步更新本文件。