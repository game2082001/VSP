# VSP AI Context

Version: 2.0

Last Updated: 2026-06-28

---

# 一、專案定位

VSP（Video Surveillance Platform）是一套企業級影像管理平台（Video Management System）。

本專案並非 Demo，也不是練習作品。
- Scalability
- Maintainability
- Readability
- Stability

- Device Center
- Live View
- Playback
- Recording
- Event Center
- Map
- AI Analytics
- Multi Site
- User / Role
- NVR Integration
- IoT Device Integration
- 穩定性
- 長期演進能力

---

# 二、專案目標

VSP 最終將包含：

- 設備管理（Device Center）
- 即時監看（Live View）
- 錄影回放（Playback）
- 錄影管理（Recording）
- 事件中心（Event Center）
- 地圖管理（Map）
- AI 分析（AI Analytics）
- 多站點管理（Multi Site）
- 使用者權限（User / Role）
- NVR 整合
- 門禁設備整合
- IoT 設備整合

---

# 三、目前 Solution

目前 Solution 包含：

- VSP.UI
- VSP.Device
- VSP.Domain
- VSP.Infrastructure
- VSP.Player
- VSP.Core
- VSP.Common

各 Project 皆有明確職責，不得混用。

---

# 四、開發原則

所有開發必須遵守：

1. MVVM
2. Repository Pattern
3. Driver Framework
4. Service Layer
5. Lite Clean Architecture

任何新功能不得破壞以上原則。

---

# 五、目前主要工作區

目前主要 Workspace：

DeviceCenter

未來將加入：

- LiveView
- Playback
- Recording
- AI

DeviceCenter 為設備管理唯一入口。

不得建立第二套設備管理介面。

---

# 六、目前完成狀態

已完成：

- SQLite 基礎
- Camera Entity
- Repository 基礎
- Driver Framework 骨架
- Device Profile
- MainWindow Workspace
- DeviceCenter 基本介面

開發中：

- Device List
- Device Editor

未開始：

- Live View
- Playback
- Recording
- AI

---

# 七、AI 工作模式

本專案採用 AI 協作開發。

角色如下：

ChatGPT：

- 系統架構
- 技術規劃
- Code Review
- Spec 撰寫

Codex：

- 程式開發
- Build
- Refactor
- 回報修改

使用者：

- Visual Studio 測試
??Suggested commit message provided; actual Git commit is done by user.
- 功能驗收

---

# 八、Codex 工作規則

每次開始工作前，必須先閱讀：

- 00_AI_CONTEXT.md
- 01_ARCHITECTURE.md
- 02_CODING_RULES.md
- 03_ROADMAP.md
- 本次 Sprint Spec

若 Spec 與程式衝突：

不得自行修改架構。

必須先停止並提出原因。

---

# 九、Coding 原則

View：

只負責 UI。

不得包含商業邏輯。

---

ViewModel：

只負責：

- Binding
- Command
- UI 狀態

不得：

- SQL
- Driver
- SQLite

---

Service：

負責：

- 商業流程
- Driver 呼叫
- Repository 呼叫

---

Repository：

只負責 CRUD。

不得包含商業邏輯。

---

Driver：

只負責設備通訊。

不得操作 UI。

---

# 十、目前開發方向

Sprint 1

完成 DeviceCenter。

Sprint 2

完成 LiveView。

Sprint 3

完成 Playback。

Sprint 4

完成 Recording。

Sprint 5

完成 AI Framework。

---

# 十一、開發流程

固定流程：

Spec

↓

Codex

↓

Build

↓

Review

↓

User Git Commit

↓

更新文件

---

# 十二、Git 原則

Codex：

不得執行：

git add

git commit

git push

Git 一律由使用者操作。

---

# 十三、重要規定

不得：

- 修改 MainWindow 架構
- 建立第二套 DeviceCenter
- 直接操作 SQLite
- View 建立 Driver
- ViewModel 建立 Repository
- Repository 放商業邏輯

---

# 十四、Legacy 元件

目前保留：

- DeviceView
- AddDeviceWindow

原因：

避免影響舊功能。

新功能不得開發於 Legacy 元件。

---

# 十五、Definition of Done

每完成一項功能必須符合：

□ Build 成功

□ Build 0 Error

□ 功能正常

□ 符合 MVVM

□ 符合 Repository Pattern

□ 通過 Review

□ Git Commit（由使用者完成）

---

# 十六、VSP 開發目標

VSP 並非單一程式。

而是一套可持續開發的商業產品。

所有設計皆以：

- 可閱讀
- 可維護
- 可測試
- 可擴充

為最高原則。

---

# Revision History

| Version | Date | Summary |
|----------|------------|--------------------------|
| 2.0 | 2026-06-28 | 全面改版，建立 VSP AI 開發規範 |
