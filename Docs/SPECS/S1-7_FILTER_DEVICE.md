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