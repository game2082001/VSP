# IMPLEMENT TASK

Version: 3.1

---

本文件定義單一 Task 的 Task Plan 格式與執行細節。是否需要在 Task 之間停止等待確認，於 Epic 範圍內執行時改由 `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` 的 Epic Execution Model 決定，見本文件後段的說明。

---

# 文件用途

本文件定義 AI（ChatGPT / Codex / Claude Code）執行功能開發時的標準流程。

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
3. 本次變更的 Risk Classification（LOW / MEDIUM / HIGH，見 `AI_OPERATING_SYSTEM.md` §7 —— 依變更的性質判斷，不是依修改檔案數量判斷）。

若發現 Spec 與 Architecture 衝突：

- 停止修改
- 說明原因
- 提出建議方案

不得自行修改 Architecture。

若 Risk Classification 為 HIGH：

請於 Task Plan 中說明：

- 原因
- 影響範圍
- 是否可再拆分 Task

（v3.1：先前以「修改超過 8 個檔案」作為停止門檻已移除——檔案數量本身不代表風險高低，一律改以 Risk Classification 判斷。）

---

# 第一步：Task Plan（必須先完成）

開始修改任何程式之前，

請先提出本次修改計畫。

格式如下：

======== Task Plan ========

本次任務：

（填寫任務名稱）

--------------------------------

Current-State Analysis

說明目前程式現況。

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

Build / Test Plan：

預計 Build：

dotnet build

預計 Test：

dotnet test

預期：

- Build Success
- 不新增 Build Error
- 不新增功能性 Warning

--------------------------------

Risks：

列出本次可能風險。

--------------------------------

Out of Scope：

列出本次不包含內容。

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

（若本 Task 是在已核准的 Epic 範圍內執行，是否需要在此等待確認改由 `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` 的 Epic Execution Model 決定——Task Plan 仍需產出，但屬於內部產物，不一定是對外停等關卡，見該文件 §7。單一 Task〔非 Epic 範圍內〕時，本節規則維持不變。）

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
- 執行任何 Git 寫入指令（git add / git commit / git push）

（唯讀 Git 指令如 `git status`／`git diff`／`git log`，為確認現況與提供 Diff 所需，不受此限制。Product Owner 可針對單一 Task 明確授權 staging/commit，即 Commit Gate，完整程序見 `AI/OperatingSystem/AI_OPERATING_SYSTEM.md` §23，本文件不重複該程序；push 永遠需要獨立、明確的另一次授權。）

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

列出：

- Modified Files
- Added Files
- Deleted Files

---

## 二、修改內容

逐一說明：

- 每個檔案修改內容
- 修改目的

---

## 三、Task Plan 是否一致

請說明：

- 是否依照原本 Task Plan 完成
- 是否新增修改檔案
- 若有，請說明原因

---

## 四、Architecture Summary

說明：

- 是否符合目前 Architecture
- 是否新增新的 Layer
- 是否變更既有責任分工

---

## 五、測試方式

請說明如何在 Visual Studio 驗證。

例如：

- Build
- 執行程式
- 操作步驟
- 預期結果

---

## 六、影響範圍

請說明是否影響其他功能。

若有，

請列出。

---

## 七、Build Result

請回報：

Build Success：

是 / 否

Build Error：

X

Warning：

X

---

## 八、Test Result

請回報：

- Unit Tests
- Integration Tests（若有）
- Manual Verification

例如：

Passed：

118

Failed：

0

Skipped：

0

---

## 九、Spec 完成狀態

請回答：

☐ 全部完成

☐ 部分完成

☐ 無法完成

若未完成，

請列出原因。

---

## 十、Risk Report

列出：

- 已知限制
- 已知風險
- 後續注意事項

---

## 十一、Technical Debt

若有，

請列出：

例如：

TD-001：

後續可改善項目。

---

## 十二、Suggested Commit Message

請提供符合 Conventional Commits 的 Commit Message。

格式：

<type>(<scope>): <description>

例如：

feat(camera): add camera delete flow

feat(driver): add plugin-based driver registration

feat(discovery): add initial onvif discovery

fix(import): preserve duplicate validation results

docs(ai): update implementation workflow

---

完成後請停止。

等待使用者確認。

不得自行開始下一個 Task 或 Sprint。

（若本 Task 是在已核准的 Epic 範圍內執行，本規則由 `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` 的 Epic Execution Model 取代：Epic 核准後預設行為為 CONTINUE、不是 STOP，AI Agent 僅在遇到 Stop Condition〔見 `AI_OPERATING_SYSTEM.md` §8〕時才停止，內部 Task 完成不需個別等待確認。單一 Task〔非 Epic 範圍內〕時，本規則維持不變。）