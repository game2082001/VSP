# VSP Development Roadmap

Version: 2.1

Last Updated: 2026-06-28

---

# 一、Roadmap 目的

本文件定義 VSP 的整體開發方向。

所有 Sprint、Spec 與功能開發皆以本文件為依據。

若需求變更，必須先更新 Roadmap，再開始實作。

---

# 二、開發原則

VSP 採 Sprint 開發模式。

每個 Sprint 必須：

- 有明確目標
- 有獨立 Spec
- 可獨立驗收
- Build 0 Error
- 通過 Code Review
- 更新文件

---

# 三、目前版本

目前版本：

**v0.2（Development）**

目前完成：

- Project Architecture
- SQLite Repository
- Driver Framework Skeleton
- Device Entity
- Device Service
- Main Window
- Workspace
- Device Center UI

---

# 四、Version Roadmap

## v0.3

完成 Device Center MVP

包含：

- Device List
- Device Detail
- Add Device
- Edit Device
- Delete Device
- Search
- Filter
- Connection Test

---

## v0.4

完成 Live View

---

## v0.5

完成 Playback

---

## v0.6

完成 Recording

---

## v0.7

完成 Event Center

---

## v0.8

完成 AI Framework

---

## v0.9

完成整體系統整合

---

## v1.0

正式 Release

---

# 五、Sprint 規劃

---

## Sprint 1

主題：

Device Center

目標：

完成 Device Center MVP。

### S1-1 Device List

- Device 清單
- 選取
- Reload

### S1-2 Device Detail

- Detail Binding

### S1-3 Add Device

- Add Device
- Save SQLite
- Reload
- Auto Select

### S1-4 Edit Device

- Edit Device
- Update SQLite
- Reload
- Auto Select

### S1-5 Delete Device

- Delete Confirm
- Delete SQLite
- Reload
- Auto Select

### S1-6 Search

- Name
- IP
- Brand
- Model

### S1-7 Filter

- Brand
- Connection Type
- Status

### S1-8 Connection Test

- Driver Connection
- Timeout
- Result Display

---

## Sprint 2

主題：

Live View

包含：

- Workspace
- Video Player
- Layout
- Snapshot
- Full Screen
- Multi View

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

Driver Framework

包含：

- Driver Manager
- Driver Factory
- RTSP Driver
- ONVIF Driver
- Hikvision Driver
- Dahua Driver

---

## Sprint 6

主題：

AI Framework

包含：

- Motion Detection
- Object Detection
- Face Detection
- Vehicle Detection
- Metadata

---

# 六、未來功能

Version 1.x

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

每個 Sprint 必須完成：

- Spec
- Task Plan
- Checklist
- Code Review
- Suggested Commit Message
- CHANGELOG 更新

Git Commit 由 User 執行。

---

# 八、Definition of Done

每個 Sprint 完成必須符合：

- ☑ Build Success
- ☑ Build 0 Error
- ☑ 功能完成
- ☑ 通過測試
- ☑ 通過 Review
- ☑ 更新 Spec
- ☑ 更新 CHANGELOG
- ☑ 更新 Roadmap
- ☑ User Git Commit

---

# 九、Priority

## Priority 1

Device Center

Live View

---

## Priority 2

Playback

Recording

---

## Priority 3

Driver Framework

AI Framework

---

## Priority 4

Alarm

Map

Mobile

---

# 十、目前 Sprint

目前：

## Sprint 1

### 已完成

- ✅ S1-1 Device List
- ✅ S1-2 Device Detail
- ✅ S1-3 Add Device
- ✅ S1-4 Edit Device
- ✅ S1-5 Delete Device

### 下一步

- ⏳ S1-6 Search

### 後續

- S1-7 Filter
- S1-8 Connection Test

---

# 十一、文件更新規則

若修改：

## Architecture

更新：

- Docs/01_ARCHITECTURE.md

---

若修改：

## Coding Rules

更新：

- Docs/02_CODING_RULES.md

---

若完成：

## Sprint

更新：

- Docs/SPECS/*
- Docs/CHANGELOG.md
- Docs/03_ROADMAP.md

---

# 十二、版本規劃

目前：

v0.2

目前 Sprint：

Sprint 1

下一版本：

v0.3

Release 條件：

完成 Sprint 1 所有功能。

---

# Revision History

| Version | Date | Summary |
|----------|------------|----------------------------------------------|
| 2.1 | 2026-06-28 | 更新 Sprint 1 Roadmap，拆分 Add/Edit/Delete/Search/Filter/Connection Test，調整 Version Roadmap 與 Definition of Done。 |
| 2.0 | 2026-06-28 | 建立 VSP 完整開發 Roadmap。 |