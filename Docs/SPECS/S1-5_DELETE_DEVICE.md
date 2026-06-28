# Sprint 1 - Task 5：Delete Device

Version：1.0

---

# 一、功能目標

完成 DeviceCenter 的 Delete Device 流程。

使用者選取 Device 後，可刪除設備。

刪除後更新 SQLite，重新整理 Device List，並同步更新 Device Detail。

---

# 二、功能需求

完成以下功能：

1.
Delete 按鈕必須綁定 DeleteDeviceCommand。

2.
若未選取 Device：

提示：

Please select a device.

不得刪除。

3.
若已選取 Device：

跳出確認視窗：

Delete Device

Are you sure you want to delete this device?

[Yes]

[No]

4.

按 Yes：

透過 DeviceService.DeleteCamera()

刪除 SQLite。

5.

刪除完成：

重新呼叫 LoadDevices()

更新 Device List。

6.

若仍有 Device：

自動選取第一台。

若沒有 Device：

SelectedDevice = null。

Device Detail 清空。

---

# 三、架構規則

必須遵守：

- MVVM
- Repository Pattern
- DeviceService
- 不直接操作 SQLite

---

# 四、本次不可修改

不得：

- 修改 MainWindow
- 修改 Architecture
- 修改 SQLite Schema
- 修改 Driver Framework
- 修改 Search
- 修改 Filter
- 修改 Connection Test

---

# 五、預計修改檔案

預期：

- DeviceCenterView.xaml
- DeviceCenterViewModel.cs

若 DeviceService 已有 DeleteCamera()

請直接使用。

不要重新建立 DeleteService。

---

# 六、驗收條件

完成後必須：

- Build Success
- Error = 0
- Delete Button 可使用
- Confirm Dialog 正常
- SQLite 資料刪除
- Device List 更新
- Device Detail 更新
- 不修改 MainWindow
- 不修改 Driver

---

# 七、測試方式

1.

新增兩台 Camera。

2.

選第一台。

3.

Delete。

4.

按 Yes。

確認：

SQLite 少一筆。

Device List 更新。

5.

刪除最後一台。

確認：

Device List 為空。

Detail 清空。

---

# 八、完成後回報

請回報：

1.
修改檔案

2.
完整 Diff

3.
Task Plan 是否一致

4.
Build Result

5.
Error 數量

6.
Warning 數量

7.
是否符合 Spec

8.
Commit Message

---

# 九、Completion

Status：

Not Started

Build：

Not Yet

Reviewed：

Not Yet