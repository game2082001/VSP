# VSP Project

版本：1.0  
最後更新：2026-07-01

---

# 一、專案定位

VSP（Video Surveillance Platform）是一套企業級影像管理平台（VMS）。

本專案目標不是單純開發一個 Camera Viewer，而是建立一套可長期維護、可商業部署、可持續擴充的安防平台。

VSP 未來需支援：

- IP Camera
- NVR
- Door Controller
- AI Box
- Decoder
- Display Wall
- IoT Device
- Multi-site CMS

---

# 二、技術基礎

開發語言：

- C#

開發框架：

- .NET
- WPF

主要架構：

- MVVM
- Repository Pattern
- Driver Framework
- Lite Clean Architecture

資料庫：

- SQLite

目前主要模組：

- Device Center
- Import Framework
- Driver Framework Skeleton

---

# 三、Solution 結構

目前 VSP 主要結構：

```text
VSP.sln

├── VSP.Domain
├── VSP.Device
├── VSP.Infrastructure
├── VSP.UI
├── VSP.Tests
└── Docs

---

# 四、AI Reading Order

所有 AI Agent（ChatGPT、Codex、未來其他 AI）開始工作前，必須依照以下順序閱讀文件：

```text
PROJECT.md
        ↓
04_DEVELOPMENT_GUIDE.md
        ↓
DEVELOPMENT_ROLES.md
        ↓
Current Task Spec


## Development Standard

Current Version：

VSP Development Standard v1.0

若版本變更，

Developer 必須重新閱讀所有 Core Documents。

目前版本：

- PROJECT.md v1.0
- 01_ARCHITECTURE.md v1.0
- 02_CODING_RULES.md v1.0
- 03_ROADMAP.md v1.0
- 04_DEVELOPMENT_GUIDE.md v1.0
- DEVELOPMENT_ROLES.md v1.0