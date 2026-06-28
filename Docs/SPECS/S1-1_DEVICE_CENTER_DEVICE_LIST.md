# Sprint 1 - Task 1：設備列表（Device List）

Version: 1.0

---

# 一、功能目標

完成 DeviceCenter 左側設備列表。

目前 DeviceCenter 左側仍使用假資料（Cam-001、Cam-002...）。

本次工作改為：

Bind real Camera data from DeviceService.GetAllCameras(), backed by SQLite through repository/service layers.

---

# 二、功能需求

完成以下功能：

1. DeviceCenterView 使用 DeviceCenterViewModel。

2. DeviceCenterViewModel 透過 DeviceService.GetAllCameras() 載入設備。

3. 左側設備列表使用 ListBox。

4. ListBox 的 ItemsSource 綁定 Devices。

5. SelectedItem 綁定 SelectedDevice。

6. 每筆設備至少顯示：

- Name
- Brand
- IpAddress
- ConnectionType

7. 右側 Device Editor 保持目前 Placeholder，不需實作。

---

# 三、本次不可修改

本次禁止：

- 修改 MainWindow
- 修改 Workspace 架構
- 修改 Driver Framework
- 修改 Repository 架構
- 修改 SQLite Schema
- 重新啟用 AddDeviceWindow
- 刪除 DeviceView
- 刪除 Legacy 元件

---

# 四、完成條件

完成後必須符合：

- Build 成功
- Build 0 Error
- 左側顯示 SQLite 設備資料
- SelectedDevice 能正常切換
- 不再出現 Cam-001、Cam-002、Cam-003 假資料

---

# 五、Codex 工作規範

開始修改前：

請先閱讀：

- Docs/PROJECT.md
- Docs/00_AI_CONTEXT.md
- Docs/01_ARCHITECTURE.md
- Docs/02_CODING_RULES.md
- Docs/03_ROADMAP.md

若發現目前程式與 Spec 衝突，

請先停止修改並回報。

不得自行修改 Architecture。

---

# 六、完成後請回覆

請列出：

1. 修改了哪些檔案。

2. 每個檔案修改了哪些內容。

3. 如何在 Visual Studio 測試。

4. 是否有影響其他功能。

5. Suggested Git Commit Message

完成後停止，不要繼續下一個 Sprint。
