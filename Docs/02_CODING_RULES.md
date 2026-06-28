# VSP Coding Rules

Version: 2.0

Last Updated: 2026-06-28

---

# 一、目的

本文件定義 VSP 專案的程式開發規範。

所有開發（包含 Codex）皆必須遵守本文件。

若與 Spec 衝突，以 Spec 為主。

若與 Architecture 衝突，以 Architecture 為主。

---

# 二、基本原則

所有程式必須符合：

- 易閱讀
- 易維護
- 易測試
- 易擴充

不要追求最短程式碼。

優先考慮可讀性。

---

# 三、命名規範

## 類別（Class）

使用 PascalCase。

例如：

Camera

DeviceService

DriverFactory

DeviceCenterViewModel

---

## 方法（Method）

使用 PascalCase。

例如：

LoadDevices()

SaveCamera()

TestConnection()

---

## 屬性（Property）

使用 PascalCase。

例如：

CameraName

IpAddress

SelectedDevice

---

## 私有欄位（Field）

使用底線開頭。

例如：

_cameraRepository

_selectedDevice

_deviceService

---

## 區域變數

使用 camelCase。

例如：

camera

result

deviceList

---

# 四、View 規範

View 只負責：

- UI
- XAML
- Binding
- Style

禁止：

- SQL
- Driver
- Repository
- 商業邏輯

Code-behind 只允許：

- InitializeComponent()
- 必要 UI 事件

不得撰寫商業邏輯。

---

# 五、ViewModel 規範

ViewModel 只負責：

- UI State
- Command
- Binding

不得：

new SQLiteCameraRepository()

new HikvisionDriver()

任何 SQL

任何 SQLite

任何 Driver 通訊

---

# 六、Service 規範

Service 負責：

- 商業流程
- Driver 呼叫
- Repository 呼叫
- Workflow

所有商業邏輯集中於 Service。

---

# 七、Repository 規範

Repository 只負責：

- 新增
- 修改
- 刪除
- 查詢

不得：

- Driver
- UI
- Business Logic

Repository 必須保持單一職責。

---

# 八、Driver 規範

Driver 只負責：

設備通訊。

不得：

- UI
- SQLite
- Repository

Driver 必須透過 DriverFactory 建立。

禁止：

new HikvisionDriver()

直接出現在 ViewModel。

---

# 九、SQLite 規範

SQLite 僅能由 Repository 存取。

禁止：

View

↓

SQLite

ViewModel

↓

SQLite

---

# 十、例外處理

不得：

catch(Exception)
{
}

忽略所有錯誤。

應：

記錄

↓

回傳 Result

↓

ViewModel 顯示

---

# 十一、Async 規範

所有：

- 網路
- Driver
- Playback

未來皆改為：

Async

避免阻塞 UI。

---

# 十二、XAML 規範

盡量：

Binding

避免：

Code Behind

所有 Style 放於：

Resources

不得：

每頁重新定義相同 Style。

---

# 十三、UI 規範

整體風格：

Dark Theme

Primary Color：

Blue

Danger：

Red

Success：

Green

Warning：

Orange

---

# 十四、Build 規範

每完成一項功能：

必須：

Build Success

不得：

Error must be 0.
Warnings must be reported; warning policy depends on the task.
若新增 Warning：

必須說明原因。

---

# 十五、Git 規範

Codex：

不得：

git add

??Suggested commit message provided; actual Git commit is done by user.

git push

Git 一律由使用者執行。

---

# 十六、Codex 工作規範

開始工作前：

必須閱讀：

Docs/PROJECT.md

00_AI_CONTEXT.md

01_ARCHITECTURE.md

02_CODING_RULES.md

03_ROADMAP.md

Spec

完成後：

必須回覆：

一、修改檔案

二、修改內容

三、測試方式

四、風險

五、建議 Commit Message

不得自行開始下一個 Sprint。

---

# 十七、禁止事項

不得：

修改 MainWindow 架構。

建立第二套 DeviceCenter。

直接修改 SQLite。

ViewModel 建立 Driver。

Repository 放商業邏輯。

未閱讀 Spec 即開始修改。

---

# 十八、Definition of Done

每個 Task 必須符合：

□ Build 成功

□ Build 0 Error

□ 功能正常

□ UI 正常

□ MVVM

□ Repository Pattern

□ Driver Framework

□ 通過 Review

□ Git Commit

---

# 十九、Commit Message 規範

格式：

SprintX-TaskY: 功能名稱

例如：

Sprint1-Task1: Device Center Device List

Sprint1-Task2: Device Editor

Sprint2-Task1: Live View Workspace

---

# 二十、程式修改原則

每次修改：

優先：

最小修改。

不要：

一次重構整個 Solution。

若需要修改超過 10 個檔案：

必須先提出原因。

---

# Revision History

| Version | Date | Summary |
|----------|------------|----------------------------|
| 2.0 | 2026-06-28 | 建立 VSP Coding Rules |
