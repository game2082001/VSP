# AI Development Contract

版本：1.2

最後更新：2026-08-15

---

# 一、目的

本文件定義所有 AI Agent 在 VSP 專案中的角色、權責與工作流程。

所有 AI Agent（ChatGPT、Codex、未來其他 AI）皆必須遵守本文件。

本文件優先於個別 Task 的執行習慣。

---

# 二、AI 角色

VSP 專案共有四個功能角色（Functional Roles，非工具綁定，Task-AI01-006 新增 Independent Review Agent）：

Product Owner

↓

Architect

↓

Developer（Implementation Agent）

↓

Independent Review Agent

（Independent Review Agent 對 Developer 的產出把關，兩者不得為同一次異動的同一角色；何時強制審查見 `AI/OperatingSystem/AI_OPERATING_SYSTEM.md` §27 Independent Review Policy。）

AI02 task classification and concrete developer/reviewer assignment are governed by `AI/OperatingSystem/TASK_CLASSIFICATION.md`. The default tools listed below describe role capability, but each Product or Engineering task must still record its `TASK CLASSIFICATION` block before implementation begins.

---

## Product Owner

目前預設工具：

User

負責：

- 提出需求
- 確認需求
- Approval
- 功能驗收
- 實機測試 / Hardware Gate（見 `AI_OPERATING_SYSTEM.md` §25）
- Git Commit / Push 最終權限（見 §23 Commit Gate）
- Release（Pilot / GA / Production，見 §26 Release Gate）

Product Owner 擁有最終決策權。

---

## Architect

目前預設工具：

ChatGPT

負責：

- Product Planning
- Architecture Design
- Roadmap
- Specification / SDD Orchestration
- Task Decomposition
- Acceptance Planning
- Cross-Agent Coordination
- Code Review（架構面）
- Quality Control
- Technical Decision

Architect 不直接 Coding。

Architect 不直接修改程式。

Architect 對整體架構負責。

---

## Developer（Implementation Agent，對應 `AI_OPERATING_SYSTEM.md` §2）

目前預設工具：

Claude Code

負責：

- Repository Inspection
- TDD（見 `AI_OPERATING_SYSTEM.md` §24 TDD Policy）
- Coding
- Build / Test 執行
- Bug Fix
- Refactoring（經 Approval）
- Technical Investigation
- Remediation
- Documentation Update
- Artifact Preparation
- Task Summary
- Git staging/commit 僅限明確 Commit Gate 授權下執行（見 §23 Commit Gate）；預設不得 git add / commit / push

Developer 不決定產品方向。

Developer 不修改 Architecture。

Developer 不自行增加需求。

---

## Independent Review Agent（新增角色，Task-AI01-006，解決 GB-005 / GB-006）

目前預設工具：

Codex

負責：

- Requirement Coverage Review
- Architecture Review
- Test-Gap Analysis
- Correctness / Reliability / Security Review
- Concurrency / Resource-Lifecycle Review
- Maintainability Review
- 第二實作路徑，僅限明確指派時執行

Independent Review Agent 不得僅摘要或直接採信 Developer 的 Completion Report，必須實際檢視 repository 現況（diff、測試、build/test 輸出）。

何時為強制審查：見 `AI_OPERATING_SYSTEM.md` §27（MEDIUM/HIGH risk、架構變更、DB schema、Public API、Security，或 Developer 將成為唯一技術把關者的非瑣碎異動）；LOW risk / 純文件異動維持既有 Self-Review 即可。

Independent Review Agent 與 Developer 不得在同一 working tree / task window 內同時修改相同檔案；若經核准平行實作，必須使用獨立 branch/worktree，整合僅於 Review 完成後進行。

---

（註：以上四個角色定義的是「職能」而非固定綁定特定工具或特定 AI。每個角色下方的「目前預設工具」僅記錄現行預設指派，未來可在不修改角色定義的前提下更新該行，指派新的工具。單一 AI Agent 在同一個 Session 中可能同時扮演多個角色，只要不跳過各角色應有的把關步驟即可，詳見 `AI/OperatingSystem/AI_OPERATING_SYSTEM.md` §2 Role Overlap 與 §27 Independent Review Policy。）

---

# 三、開發流程

所有 Task 必須遵守：

```text
Read PROJECT.md
        ↓
Read DEVELOPMENT_GUIDE.md
        ↓
Read Current Spec
        ↓
Task Plan
        ↓
Approval
        ↓
Coding
        ↓
Self Build
        ↓
Documentation Update
        ↓
Diff
        ↓
Architecture Review
        ↓
Suggested Git Commit
        ↓
User Commit
```

任何步驟不得省略。

（本流程圖是 `Docs/AI_DEVELOPMENT_WORKFLOW.md` 所定義的專案通用 Workflow 在 VSP 專案下的細節展開，兩者為同一流程的不同顆粒度描述。若步驟順序或名稱有出入，以 `Docs/AI_DEVELOPMENT_WORKFLOW.md` 為準。Epic 範圍內執行時，Task 之間是否停止等待確認另見該流程與 `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md`。）

---

# 四、Specification First

沒有 Spec

↓

不得 Coding

任何新功能：

必須：

Spec

↓

Task Plan

↓

Approval

↓

Coding

不得直接開始實作。

---

# 五、Task Plan

Developer 必須先提出：

- 本次目的
- 新增檔案
- 修改檔案
- 影響範圍
- 是否修改重要模組
- Build 風險

等待 Approval。

---

# 六、Scope Protection

Developer 不得：

- 修改 Scope 外內容
- 偷加功能
- 修改 Architecture
- 修改 Repository
- 修改 SQLite Schema
- 修改 Driver Framework

若需要：

停止 Coding

↓

更新 Task Plan

↓

等待 Approval

---

# 七、Documentation Responsibility

Documentation 屬於 Implementation。

Developer 必須主動更新：

- Docs/CHANGELOG.md
- Docs/03_ROADMAP.md
- Current Task Spec

必要時：

- Docs/PROJECT.md
- Docs/STATUS.md
- Docs/KNOWN_ISSUES.md

不得等待提醒。

---

# 八、Build Responsibility

Developer 必須：

Build Success

Error = 0

Warning 若增加：

必須說明。

Build 未成功：

不得交付。

---

# 九、Review Responsibility

Architect Review：

- Architecture
- MVVM
- Scope
- Maintainability
- Documentation
- Future Extension

Review 通過前：

Task 不得 Completed。

---

# 十、Git Responsibility

Developer：

提供：

Suggested Git Commit

例如：

```bash
git commit -m "feat(import): add excel import parser"
```

Developer 預設不得：

- git add
- git commit
- git push

Git 預設全部由 Product Owner 執行。

例外：Product Owner 可針對單一 Task 明確授權 Developer 執行 staging/commit（Commit Gate），完整程序見 `AI/OperatingSystem/AI_OPERATING_SYSTEM.md` §23 Commit Gate。此例外不包含 push，push 永遠需要獨立、明確的另一次授權。Local branch/worktree 建立不受此例外限制，規則同見 §23。

---

# 十一、Task Summary

每個 Task 完成後，

Developer 必須提供：

```text
==========================

Task Summary

==========================

Task

Status

Build

Error

Warning

Documentation

Git Commit

Next Task

==========================
```

不得只回覆：

「完成」。

---

# 十二、Task Completion

只有符合：

- Build Success
- Error = 0
- Documentation Updated
- Review Completed
- Suggested Git Commit
- User Commit

Task 才算真正完成。

若只有 Coding：

狀態必須：

```text
Coding Completed

Waiting Review
```

不得標示：

Task Completed

---

# 十三、合作原則

Product Owner

決定：

做什麼。

Architect

決定：

怎麼設計。

Developer

決定：

怎麼實作。

三個角色互相配合。

不得越權。

---

# 十四、最終目標

VSP 採用：

Specification Driven Development

Architecture First

Documentation First

Review Before Completion

目的不是快速完成程式，而是建立一套可長期維護、可商業部署、可持續演進的企業級 Video Management System。

---

# 十五、AI Working Principles

所有 AI Agent 必須遵守：

- 文件（Docs）為正式規範（Single Source of Truth），惟優先順序依 `AI/OperatingSystem/AI_OPERATING_SYSTEM.md` §1 Authority Order 為準（使用者當下明確指示 > Task/Epic Specification > ADR/正式架構文件 > Roadmap/Product Principles > 現有程式與測試 > AI Operating System > AI Memory）
- 不得依聊天內容直接開始 Coding（Specification First 仍適用，見下）
- 若聊天內容與文件出現無法依上述 Authority Order 解決的衝突，須提出並等待使用者決定，不得自行選擇任一方（見 `AI_OPERATING_SYSTEM.md` §1）
- 若流程需要修改，應先更新文件，再開始開發

AI 啟動順序：

```text
PROJECT.md
        ↓
04_DEVELOPMENT_GUIDE.md
        ↓
DEVELOPMENT_ROLES.md
        ↓
Current Task Spec

不得省略。
