# S1-7 Filter Device

## 1. 本次目的

完成 DeviceCenter 的 Filter 功能。

Filter 僅針對目前已載入記憶體中的 Camera 集合做篩選。

不得修改 SQLite。

不得新增 Repository Filter。

不得修改 Driver。

---

## 2. 預計修改檔案

- DeviceCenterView.xaml
- DeviceCenterViewModel.cs

可能會讀取：

- DeviceService.cs（只讀，不修改）

---

## 3. 修改目的

### DeviceCenterView.xaml

新增 Filter UI。

包含：

- Brand Filter
- Connection Filter

放置於 Search 下方。

---

### DeviceCenterViewModel.cs

新增：

SelectedBrand

SelectedConnection

ApplyFilter()

Filter 必須與 Search 共用同一套 ApplySearch() 流程。

---

## 4. Filter 項目

Brand：

- All
- Hikvision
- Dahua
- Axis
- Uniview
- RTSP
- ONVIF

Connection：

- All
- SDK
- RTSP
- ONVIF

未來新增 Driver 時可自動增加。

---

## 5. Filter 流程

LoadDevices()

↓

_allDevices

↓

Brand Filter

↓

Connection Filter

↓

Search

↓

Devices

不得：

重新查 SQLite。

---

## 6. UI
Search

[____________]

Brand

[ All ▼ ]

Connection

[ All ▼ ]

Filter 改變立即更新。

---

## 7. 是否新增檔案

否。

---

## 8. 是否刪除檔案

否。

---

## 9. 是否修改

MainWindow：否

Architecture：否

Repository：否

SQLite Schema：否

Driver Framework：否

---

## 10. 預估影響

僅：

DeviceCenter

Device List

Search

Filter

不影響：

Add

Edit

Delete

Driver

Live View

---

## 11. Architecture

View

↓

ViewModel

↓

Memory Filter

↓

UI

不得：

ViewModel

↓

SQLite

---

## 12. 驗收

可依 Brand Filter。

可依 Connection Filter。

可與 Search 同時使用。

All 可恢復全部。

不得重新查 SQLite。

---
---

# Completion

Status

Completed

Build Result

Build Success

Errors

0

Warnings

7（SQLitePCLRaw.lib.e_sqlite3 既有弱點警告）

Modified Files

- DeviceCenterView.xaml
- DeviceCenterViewModel.cs

Architecture

無修改

Repository

無修改

SQLite Schema

無修改

Driver Framework

無修改

DeviceService

無修改

Implementation Summary

- 新增 Brand Filter
- 新增 Connection Filter
- 第一項皆為 All
- Filter 與 Search 共用同一套記憶體篩選流程
- Search 建立於 Filter 後執行
- 未重新查詢 SQLite
- Clear Search 同時重設 Search Keyword、Brand Filter、Connection Filter
- Device List 即時更新
- Device Detail 保持同步

Verification

✓ Filter 可依 Brand 篩選

✓ Filter 可依 Connection 篩選

✓ Search 與 Filter 可同時作用

✓ Clear 可恢復全部資料

✓ Build Success

✓ 符合 S1-7 Spec