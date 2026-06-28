# Sprint 1 - Task 4：Edit Device

Version：1.0

---

# 一、功能目標

完成 DeviceCenter 的 Edit Device 流程。

使用者選取左側 Camera 後，可以開啟既有 AddDeviceWindow 進行修改，儲存後更新 SQLite，並重新載入 Device List 與 Device Detail。

---

# 二、功能需求

1. 必須選取 Camera 才能編輯。
2. 可透過雙擊 Device List 項目，或新增 Edit 按鈕開啟編輯視窗。
3. 編輯視窗優先使用既有 AddDeviceWindow(Camera camera)。
4. 儲存後透過 DeviceService 更新 Camera。
5. 不得直接在 ViewModel 操作 SQLite。
6. 更新後 Device List 重新載入。
7. 更新後重新選取原本 Camera。
8. 右側 Device Detail 顯示更新後資料。

---

# 三、本次不可修改

- 不修改 MainWindow
- 不修改 Driver Framework
- 不修改 SQLite Schema
- 不新增第二套 Edit 視窗
- 不做 Delete
- 不做 Search / Filter
- 不做 Connection Test

---

# 四、驗收條件

- Build 成功
- Error = 0
- 未選取 Camera 時不可編輯或需提示
- 編輯後資料寫入 SQLite
- 關閉程式再開，修改後資料仍存在
- Device Detail 顯示更新後資料

---

# Result

Status

Completed

Implementation

- DeviceCenter Edit Device Button
- EditDeviceCommand
- Existing AddDeviceWindow(Camera)
- Load Existing Camera Data
- DeviceService.UpdateCamera()
- SQLite Update
- Reload Device List
- Auto Select Updated Camera
- Device Detail Refresh

Architecture

- MVVM
- Repository Pattern
- Existing DeviceService
- Existing Camera Repository
- Existing SQLite Schema

Not Included

- Delete Device
- Search
- Filter
- Connection Test
- Real-time Validation

Review Result

✓ Passed

Verified

✓ Edit Existing Camera
✓ Update SQLite
✓ Reload Device List
✓ Auto Select Updated Camera
✓ Device Detail Refresh