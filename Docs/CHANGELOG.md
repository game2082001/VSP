# CHANGELOG

本文件記錄 VSP 專案的重要功能變更。

---

# Version 2.0

---

## Sprint 1

### S1-1 Device List
日期：2026-06-28

#### 新增
- DeviceCenter 採用 DeviceCenterViewModel。
- Device List 改為透過 DeviceService.GetAllCameras() 載入 SQLite Camera 資料。
- 左側 Device List 使用 ListBox。
- 完成 Devices 與 SelectedDevice Binding。
- 新增 RefreshCommand，可重新載入 Camera 清單。
- 新增 DeviceCount 顯示。

#### UI
- 左側顯示：
  - Camera Name
  - Brand
  - IP Address
  - Connection Type
- 右側 Device Editor 保持 Placeholder。

#### 架構
- 符合 MVVM。
- ViewModel 不直接存取 SQLite。
- 經由 DeviceService 取得資料。
- 未修改 MainWindow。
- 未修改 Repository。
- 未修改 SQLite Schema。
- 未修改 Legacy DeviceView。

#### Build
- Build Success
- Error：0
- Warning：NU1903（SQLite 套件安全性警告）

---
## Sprint 1

### S1-2 Device Detail

Status:
Completed

Summary:
- Device Detail now binds directly to SelectedDevice.
- Display fields:
  - Name
  - Brand
  - Model
  - IP Address
  - Connection Type
- No fake properties added.
- No placeholder values added.
- Repository / SQLite / Driver Framework unchanged.

Files:
- DeviceCenterView.xaml

Reviewed:
2026-06-28

-----
# Changelog

---

## 2026-06-28

### Sprint 1 - Task 3
### Device Center - Add Device

Status:
Completed

Summary:

- 完成 Device Center Add Device 流程
- Add Device 按鈕已綁定 AddDeviceCommand
- 使用既有 AddDeviceWindow，不新增新視窗
- Save 後透過 DeviceService.AddCamera() 寫入 SQLite
- 新增完成後重新載入 Device List
- 自動選取剛新增的 Camera
- Device Detail 同步顯示新增資料

Architecture:

- 維持 MVVM
- ViewModel 不直接存取 SQLite
- Repository Pattern 不變
- SQLite Schema 無修改
- Driver Framework 無修改

Not Included:

- Edit
- Delete
- Search
- Filter
- Connection Test
- Real-time Validation

Verified:

✓ Add Device
✓ SQLite Save
✓ Refresh Device List
✓ Detail Binding

---
## 2026-06-28

### Sprint 1 - Task 4
### Device Center - Edit Device

Status:
Completed

Summary:

- 完成 Device Center Edit Device 流程
- 新增 Edit Device 按鈕
- Edit Device 使用既有 AddDeviceWindow(Camera)
- 視窗自動載入目前 Camera 資料
- Save 後透過 DeviceService.UpdateCamera() 更新 SQLite
- 更新完成重新載入 Device List
- 自動重新選取修改後 Camera
- Device Detail 同步更新

Architecture:

- 維持 MVVM
- ViewModel 不直接操作 SQLite
- Repository Pattern 不變
- SQLite Schema 無修改
- Driver Framework 無修改

Not Included:

- Delete Device
- Search
- Filter
- Connection Test
- Real-time Validation

Verified:

✓ Edit Device
✓ SQLite Update
✓ Reload Device List
✓ Auto Select Updated Camera
✓ Device Detail Refresh