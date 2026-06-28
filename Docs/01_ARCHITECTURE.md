# VSP Architecture

Version: 2.0

Last Updated: 2026-06-28

---

# 一、架構目標

VSP（Video Surveillance Platform）採用模組化架構設計。

本架構主要目標：

- 高擴充性（Scalability）
- 高維護性（Maintainability）
- 高可讀性（Readability）
- 高穩定性（Stability）

VSP 並非 Demo，而是長期維護的商業產品。

---

# 二、架構設計原則

VSP 採用 Lite Clean Architecture。

核心原則：

- MVVM
- Repository Pattern
- Service Layer
- Driver Framework
- Dependency Injection
- High Cohesion
- Low Coupling

任何新功能不得破壞以上原則。

---

# 三、Solution 架構

目前 Solution：

```

VSP.sln

│

├── VSP.UI

├── VSP.Device

├── VSP.Domain

├── VSP.Infrastructure

├── VSP.Player

├── VSP.Core

└── VSP.Common

```

---

# 四、各 Project 職責

## VSP.UI

負責：

- Views
- ViewModels
- Styles
- Workspace
- Validation

不得：

- SQL
- Driver
- SQLite
- Repository

---

## VSP.Device

負責：

- DeviceService
- DriverFactory
- Driver Workflow
- Device Profile
- Business Logic

屬於系統核心。

---

## VSP.Domain

負責：

- Entity
- Enum
- Shared Model
- Interface（未來）

不得依賴：

- UI
- SQLite
- WPF

---

## VSP.Infrastructure

負責：

- SQLite
- Repository
- Database Initializer
- Migration
- SDK Adapter（未來）

不得參考 UI。

---

## VSP.Player

負責：

- Live View
- Playback
- FFmpeg（未來）
- Video Render

不得操作 Device。

---

## VSP.Core

負責：

- ObservableObject
- MVVM Base
- Helper
- Common Base Class

---

## VSP.Common

負責：

- Utility
- Constant
- Extension
- Shared Function

---

# 五、系統資料流

所有設備資料：

```

View

↓

ViewModel

↓

DeviceService

↓

Repository Interface

Repository Implementation

↓

SQLite

```

不得跳層。

例如：

View

↓

SQLite

❌ 禁止

---

# 六、MVVM

## View

允許：

- XAML
- Binding
- Style

禁止：

- SQL
- Driver
- 商業邏輯

---

## ViewModel

允許：

- Command
- Binding
- UI 狀態

禁止：

- SQL
- Driver 建立
- Repository 建立

---

## Service

允許：

- Workflow
- Business Logic
- Driver 呼叫
- Repository 呼叫

---

## Repository

允許：

- CRUD

禁止：

- 商業邏輯

---

# 七、Repository Pattern

目前：

SQLiteCameraRepository

未來：

ICameraRepository

ICameraRepository belongs to Domain or Device abstractions.
SQLiteCameraRepository belongs to Infrastructure.

↓

SQLiteCameraRepository

↓

SqlServerCameraRepository

↓

PostgreSqlCameraRepository

Repository 為唯一資料存取入口。

---

# 八、Driver Framework

目前 Driver：

- RTSP
- ONVIF
- Hikvision
- Dahua

Driver 必須：

透過 DriverFactory 建立。

不得：

View

↓

new HikvisionDriver()

---

# 九、Driver Capability

未來 Driver 必須提供：

- Live View
- Playback
- PTZ
- Snapshot
- Event
- Discovery

UI 根據 Capability 顯示功能。

不得依品牌判斷。

---

# 十、Device Profile

Device Profile 控制：

- 顯示哪些欄位
- 哪些 Port
- 哪些設定

例如：

RTSP Camera：

顯示：

- RTSP URL
- RTSP Port

隱藏：

- HTTP Port
- SDK Port

---

# 十一、Workspace

MainWindow 為唯一 Shell。

Workspace：

目前：

DeviceCenter

未來：

- Live View
- Playback
- Recording
- Event Center

Workspace 由 MainWindow 切換。

---

# 十二、DeviceCenter

DeviceCenter 為唯一設備管理介面。

功能包含：

- Device List
- Device Editor
- Device Status
- Search
- Filter
- Driver
- Connection Test

不得建立第二套設備管理畫面。

---

# 十三、Player

Player 專責：

- Video Decode
- Render
- Playback

Driver：

只提供：

- Stream
- Login
- Capability

兩者保持低耦合。

---

# 十四、資料庫

目前：

SQLite

未來：

- SQL Server
- PostgreSQL

Database 必須支援：

- Version
- Migration
- Seed
- Backup

---

# 十五、Logging

未來：

Logs/

app.log

driver.log

database.log

player.log

不得記錄：

- 密碼
- Token
- 金鑰

---

# 十六、Dependency Rules

允許：

UI

↓

Device

↓

Repository Interface

↓

Infrastructure

禁止：

Infrastructure

↓

UI

Driver

↓

ViewModel

Repository

↓

View

---

# 十七、目前狀態

完成：

- SQLite
- Repository
- Driver Framework（骨架）
- Device Profile
- Workspace

開發中：

- DeviceCenter

未完成：

- Live View
- Playback
- Recording
- AI

---

# 十八、未來規劃

Phase 1

完成 DeviceCenter。

Phase 2

完成 Live View。

Phase 3

完成 Playback。

Phase 4

完成 Recording。

Phase 5

完成 AI。

---

# 十九、Architecture Constraints

不得：

- View 操作 SQLite
- ViewModel 建立 Driver
- Driver 操作 UI
- Repository 放商業邏輯
- 建立第二套 DeviceCenter

所有新功能必須遵守 Architecture。

---

# Revision History

| Version | Date | Summary |
|----------|------------|----------------|
| 2.0 | 2026-06-28 | 重寫 VSP 架構文件，符合 Lite Clean Architecture |
