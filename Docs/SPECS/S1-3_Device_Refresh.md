# Sprint 1 - Task 3：Add Device

Version：1.0

---

# 一、功能目標

完成 DeviceCenter 的 Add Device 流程。

目前 DeviceCenter 上方已有 Add Device 按鈕，但點擊後沒有任何動作。

本次目標是：

按下 Add Device 後，開啟新增設備視窗，輸入 Camera 基本資料，儲存後寫入 SQLite，並重新載入 Device List。

---

# 二、功能需求

完成以下功能：

1. Add Device 按鈕必須綁定 Command。

2. 點擊 Add Device 後，開啟新增 Camera 視窗。

3. 新增視窗至少可輸入：

- Name
- Brand
- Model
- IP Address
- Connection Type
- Username
- Password
- RTSP URL

4. 儲存後，資料必須透過 Service / Repository 寫入 SQLite。

5. 新增成功後，DeviceCenter 必須重新載入 Device List。

6. 新增後，新增的 Camera 應可被選取。

7. 右側 Device Detail 應可顯示新增 Camera 的資料。

---

# 三、架構規則

本次必須遵守：

1. View 只負責 UI / XAML / Binding。

2. ViewModel 不得直接操作 SQLite。

3. 寫入資料必須透過 DeviceService。

4. Repository 只負責資料存取。

5. 不得修改 MainWindow 架構。

6. 不得修改 Driver Framework。

7. 不得修改 SQLite Schema，除非目前欄位不足；若需要改 Schema，必須先停止並回報。

---

# 四、本次不可修改

本次禁止：

- 修改 MainWindow
- 修改 Workspace 架構
- 修改 Driver Framework
- 重構 Repository 架構
- 修改與 Add Device 無關的功能
- 實作 Edit Device
- 實作 Delete Device
- 實作 Search / Filter
- 實作 Connection Test

---

# 五、可修改範圍

預期可能修改：

- DeviceCenterView.xaml
- DeviceCenterViewModel.cs
- AddDeviceWindow.xaml
- AddDeviceWindow.xaml.cs
- AddDeviceViewModel.cs
- DeviceService.cs
- Camera Repository 相關檔案

若需要修改超過以上範圍，請先提出 Task Plan，不要直接修改。

---

# 六、驗收條件

完成後必須符合：

- Build 成功
- Error = 0
- 點擊 Add Device 會開啟新增設備視窗
- 可輸入 Camera 基本資料
- 按 Save 後資料寫入 SQLite
- Device List 自動重新載入
- 新增 Camera 出現在左側 Device List
- 選取新增 Camera 後，右側 Device Detail 顯示資料
- 不修改 MainWindow
- 不修改 Driver Framework
- 不實作 Edit / Delete / Search

---

# 七、測試方式

測試步驟：

1. 開啟 VSP。
2. 進入 Device Center。
3. 點擊 Add Device。
4. 輸入 Camera 資料。
5. 點擊 Save。
6. 確認新增視窗關閉。
7. 確認 Device List 出現新 Camera。
8. 點選該 Camera。
9. 確認右側 Device Detail 顯示資料。
10. 使用 DB Browser for SQLite 確認 Camera Table 有新增資料。

---

# 八、完成後回報

完成後請回報：

1. 實際修改檔案。
2. 完整 Diff。
3. 是否與 Task Plan 一致。
4. Build Result。
5. Error 數量。
6. Warning 數量。
7. 是否完全符合 S1-3 Spec。
8. 是否修改 Task Plan 以外內容。
9. 建議 Commit Message。

---

# 九、Completion

Status：
Not Started

Build：
Not Yet

Reviewed：
Not Yet


---

# Result

Status

Completed

Implementation

- DeviceCenter Add Device Button
- DeviceCenterViewModel AddDeviceCommand
- Existing AddDeviceWindow
- DeviceService.AddCamera()
- SQLite Insert
- Reload Device List
- Auto Select New Camera
- Device Detail Refresh

Architecture

- MVVM
- Repository Pattern
- DeviceService
- Existing Camera Repository
- Existing SQLite Schema

Not Included

- Edit
- Delete
- Search
- Filter
- Connection Test
- Real-time Validation

Review Result

✓ Passed

Verified

✓ Save Camera
✓ Reload List
✓ Select New Camera
✓ Detail Binding