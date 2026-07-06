# VSP Coding Rules

版本：1.0

最後更新：2026-07-01

---

# 一、目的

本文件定義 VSP 專案 Coding Standard。

所有程式碼皆須遵守本規範。

---

# 二、基本原則

Coding 原則：

- Readability First
- Maintainability First
- Reuse Before Rewrite
- Small Changes
- Single Responsibility

不要為了少幾行程式而降低可讀性。

---

# 三、命名規範（Naming）

## Class

PascalCase

例如：

```csharp
DeviceService
ImportService
CameraRepository
```

---

## Method

PascalCase

```csharp
LoadDevices()

ConnectAsync()

ValidateIpAddress()
```

---

## Property

PascalCase

```csharp
DeviceName

IpAddress

Status
```

---

## Private Field

使用：

```text
_camelCase
```

例如：

```csharp
_deviceRepository

_importService

_logger
```

---

## Local Variable

camelCase

```csharp
camera

device

result
```

---

## Constant

PascalCase

```csharp
DefaultTimeout

MaxRetryCount
```

---

## Interface

I 開頭

```csharp
IRepository

IDeviceDriver

IImportParser
```

---

# 四、Folder Structure

依功能分類。

例如：

```text
Device

Import

Driver

Playback

Event
```

不要：

```text
Helper

Utils

Misc

Common2
```

避免用途不明資料夾。

---

# 五、MVVM 規範

View

只負責：

- UI
- Binding

不得：

- SQL
- Driver
- Business Logic

---

ViewModel

負責：

- Command
- ObservableProperty
- UI State

不得：

- SQLite
- SDK
- Driver

---

Service

負責：

- Business Logic

不得依賴 UI。

---

Repository

只負責：

資料存取。

不得放 Business Logic。

---

# 六、Async 規範

IO 操作：

必須使用：

```csharp
async

await
```

避免：

```csharp
.Wait()

.Result
```

除非有特殊原因。

---

# 七、Exception Policy

不得：

```csharp
catch(Exception)
{
}
```

空 Catch。

---

必須：

- Logging
- 或重新拋出

例如：

```csharp
catch(Exception ex)
{
    throw;
}
```

或：

```csharp
_logger.LogError(ex);
```

---

# 八、Null Handling

使用：

```csharp
ArgumentNullException.ThrowIfNull()
```

優先於：

```csharp
if(x==null)
```

---

使用：

Nullable Reference Types。

避免：

大量 Null 檢查。

---

# 九、Magic Number

禁止：

```csharp
port=8000;

retry=3;
```

應改：

```csharp
const int DefaultPort=8000;
```

或：

```csharp
private const int DefaultPort=8000;
```

---

# 十、Comment

Comment 應說明：

為什麼（Why）

不要描述：

做什麼（What）

例如：

不好：

```csharp
//增加1
count++;
```

好：

```csharp
// Retry 次數避免無限重試
retryCount++;
```

---

# 十一、Logging

Error：

必須 Logging。

Debug：

避免大量輸出。

Release：

不得保留 Debug 測試程式。

---

# 十二、Code Style

保持：

- 小 Method
- 小 Class

一個 Method：

建議：

30~50 行。

若超過：

考慮拆分。

---

一個 Class：

建議：

300~500 行。

若超過：

考慮拆分。

---

# 十三、Dependency

禁止：

UI

↓

SQLite

禁止：

ViewModel

↓

Repository

必須：

ViewModel

↓

Service

↓

Repository

---

# 十四、Review Checklist

每次 Coding 完成前：

確認：

□ Naming

□ Scope

□ Build

□ Warning

□ Null

□ Exception

□ Async

□ Comment

□ 可讀性

---

# 十五、Refactoring

Refactoring：

不得與新功能混在同一個 Task。

若需要：

建立：

獨立 Task。

---

# 十六、Code Review 原則

Review 重點：

1. 是否符合 Architecture

2. 是否符合 MVVM

3. 是否超出 Scope

4. 是否容易維護

5. 是否容易擴充

6. 是否容易閱讀

7. 是否破壞既有功能

---

# 十七、禁止事項

不得：

- 偷加功能
- 偷改 Architecture
- 修改 Scope 外內容
- 保留測試程式
- 保留 Debug Code
- 保留未使用 Method
- 保留未使用 using

---

# 十八、Coding Philosophy

VSP 優先順序：

Architecture

↓

Maintainability

↓

Readability

↓

Scalability

↓

Performance

↓

Development Speed

品質永遠優先於開發速度。