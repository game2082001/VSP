# IMPLEMENT TASK

Version: 2.0

---

# 文件用途

本文件定義 AI（Codex）執行功能開發時的標準流程。

適用於：

- 新功能開發
- 功能修改
- 小型重構

若為以下工作，請使用對應 Workflow：

- Bug 修復
- Code Review
- Release

---

# 工作說明

開始任何功能開發前，請先閱讀：

- Docs/PROJECT.md
- Docs/SPECS/<task-spec>.md

PROJECT.md 已包含所有專案規範。

不得跳過 PROJECT.md。

---

# 開始前確認

閱讀完成後，請先確認：

1. 是否理解本次需求。
2. 是否符合目前 Architecture。
3. 是否需要修改超過 8 個檔案。

若發現 Spec 與 Architecture 衝突，

請停止修改並提出原因。

不得自行修改 Architecture。

---

# 第一步：Task Plan（必須先完成）

開始修改任何程式之前，

請先提出本次修改計畫。

格式如下：

======== Task Plan ========

本次任務：

（填寫任務名稱）

--------------------------------

預計修改檔案：

1.

檔案名稱

修改目的

2.

檔案名稱

修改目的

--------------------------------

新增檔案：

若沒有請寫：

無

--------------------------------

刪除檔案：

若沒有請寫：

無

--------------------------------

是否修改：

MainWindow：

否 / 是

Architecture：

否 / 是

Repository：

否 / 是

SQLite Schema：

否 / 是

Driver Framework：

否 / 是

--------------------------------

預估修改檔案數：

X 個

--------------------------------

預估影響範圍：

例如：

僅 DeviceCenter

--------------------------------

Build 預估：

Build Success：

是

Error：

report count

Warning：

0

--------------------------------

Rollback：

若修改失敗，

可直接還原以下檔案：

（列出檔案）

===========================

提出 Task Plan 後，

等待使用者確認。

未得到確認前，

不得修改任何程式。

---

# 第二步：開始實作

收到使用者確認後，

再開始修改程式。

請遵守：

- MVVM
- Repository Pattern
- Driver Framework
- Coding Rules

不得：

- 修改 MainWindow
- 修改 Architecture
- 修改與本次任務無關的檔案
- 建立第二套相同功能
- 刪除 Legacy 功能
- 執行任何 Git 指令

若修改超過 Task Plan，

必須先停止，

重新提出新的 Task Plan。

若開發過程中發現與本次 Task 無關的 Bug，

請不要順便修正。

請於完成本次 Task 後另外提出。

不得將其他 Bug 一併修改。

---

# 第三步：完成後請回覆

請依照以下格式：

## 一、修改檔案

列出所有實際修改的檔案。

---

## 二、修改內容

逐一說明每個檔案修改內容。

---

## 三、Task Plan 是否一致

請說明：

- 是否依照原本 Task Plan 完成。
- 是否有新增修改檔案。
- 若有，請說明原因。

---

## 四、測試方式

請說明如何在 Visual Studio 測試。

例如：

- Build
- 執行程式
- 操作步驟
- 預期結果

---

## 五、影響範圍

請說明是否影響其他功能。

若有，

請列出。

---

## 六、Build 結果

請回報：

Build Success：

是 / 否

Build Error：

X

Warning：

X

---

## 七、Spec 完成狀態

請回答：

☐ 全部完成

☐ 部分完成

☐ 無法完成

若未完成，

請列出原因。

---

## ?怒遣霅?Suggested Commit Message

請提供符合專案規範的 Git Commit Message。

格式：

SprintX-TaskY: 功能名稱

例如：

Sprint1-Task1: Device Center Device List

---

完成後請停止。

等待使用者確認。

不得自行開始下一個 Task 或 Sprint。
