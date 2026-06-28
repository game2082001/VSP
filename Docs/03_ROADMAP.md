# VSP Development Roadmap

Version: 2.0

Last Updated: 2026-06-28

---

# 一、Roadmap 目的

本文件定義 VSP 的整體開發方向。

所有 Sprint 與功能開發皆以本文件為依據。

若需求有變更，請更新本文件後再開始實作。

---

# 二、開發原則

VSP 採用 Sprint 開發模式。

每一個 Sprint 都必須：

- 有明確目標
- 有 Spec
- 可獨立驗收
- Build 0 Error
- 通過 Code Review

---

# 三、目前版本

目前版本：

v0.2（開發中）

目前完成：

- Solution 架構
- SQLite 基礎
- Repository 基礎
- Driver Framework 骨架
- Device Profile
- MainWindow
- Workspace
- DeviceCenter 基本畫面

---

# 四、Version Roadmap

v0.3

完成 DeviceCenter

v0.4

完成 Live View

v0.5

完成 Playback

v0.6

完成 Recording

v0.7

完成 Event Center

v0.8

完成 AI Framework

v0.9

完成 系統整合

v1.0

正式 Release

---

# 五、Sprint 規劃

## Sprint 1

主題：

Device Center

目標：

建立完整設備管理中心。

包含：

- Device List
- Device Editor
- CRUD
- Driver
- Connection Test
- Search
- Filter

完成後可管理所有設備。

---

## Sprint 2

主題：

Live View

包含：

- Workspace
- Video Player
- Snapshot
- Multi View
- Layout
- Full Screen

---

## Sprint 3

主題：

Playback

包含：

- Timeline
- Search
- Export
- Download
- Bookmark

---

## Sprint 4

主題：

Recording

包含：

- Recording Plan
- Recording Status
- Disk
- Storage

---

## Sprint 5

主題：

Driver

完成：

- RTSP
- ONVIF
- Hikvision
- Dahua

建立統一 Driver Contract。

---

## Sprint 6

主題：

AI

包含：

- Motion
- Object
- Face
- Vehicle
- Metadata

---

# 六、未來功能

完成 V1.0 後：

- Alarm Center
- Map
- User Role
- Multi Site
- Door Controller
- NVR Cluster
- Web Client
- Mobile App

---

# 七、每個 Sprint 必須包含

每個 Sprint 必須至少包含：

- Spec
- Prompt
- Checklist
- Code Review
- Suggested commit message; user commits.
- CHANGELOG 更新

---

# 八、Definition of Done

每個 Sprint 完成時必須：

☑ Build Success

☑ Build 0 Error

☑ 功能完成

☑ 通過測試

☑ 通過 Review

☑ 更新文件

??User Git Commit

---

# 九、優先順序

Priority 1

- DeviceCenter
- Live View

Priority 2

- Playback
- Recording

Priority 3

- Driver
- AI

Priority 4

- Map
- Alarm
- Mobile

---

# 十、目前 Sprint

目前：

Sprint 1

目前工作：

✅ S1-1 Device List


✅ S1-2 Device Detail
Completed

下一步：

S1-3 CRUD

S1-4 Connection Test

S1-5 Driver Profile

---

# 十一、文件更新規則

若：

Architecture 修改

↓

更新：

Docs/01_ARCHITECTURE.md

若：

Coding 規範修改

↓

更新：

Docs/02_CODING_RULES.md

若：

Sprint 完成

↓

更新：

Docs/03_ROADMAP.md

Docs/CHANGELOG.md

---

# 十二、版本規劃

目前：

v0.2

目標：

v0.3

Sprint：

Sprint 1

完成後：

建立 Release：

v0.3

---

# Revision History

| Version | Date | Summary |
|----------|------------|------------------------------|
| 2.0 | 2026-06-28 | 建立 VSP 完整開發 Roadmap |
