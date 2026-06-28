# S1-6 Search Device

## 1. 本次目的

完成 DeviceCenter 的 Search 功能。

搜尋僅針對目前已載入記憶體的 Devices 集合做 Filter。

不得修改 SQLite。

不得新增 Repository Search。

不得修改 Driver。

---

## 2. 預計修改檔案

- DeviceCenterView.xaml
- DeviceCenterViewModel.cs

可能會讀取：

- DeviceService.cs（只讀，不修改）

---

## 3. 修改目的

### DeviceCenterView.xaml

新增：

- Search TextBox
- Search Button
- Clear Button（若目前已有 Icon Button 可重用）

版面放在：

Device List 上方。

---

### DeviceCenterViewModel.cs

新增：

SearchKeyword

SearchCommand

ClearSearchCommand

新增：

ApplySearch()

搜尋來源：

目前記憶體中的 Camera List。

不得重新查 SQLite。

---

## 4. 搜尋欄位

支援：

- Name
- IP
- Brand
- Model

大小寫不區分。

Keyword 空白代表全部。

---

## 5. 搜尋流程

LoadDevices()

↓

Memory Devices

↓

ApplySearch()

↓

FilteredDevices

↓

UI 更新

不得：

LoadDevices()

↓

SQLite

↓

Search SQL

---

## 6. UI
Search : [_______________] [Search] [Clear]

Clear：

清空文字

重新顯示全部 Device。

---

## 7. 是否新增檔案

否。

---

## 8. 是否刪除檔案

否。

---

## 9. 是否修改

MainWindow：否

Architecture：否

Repository：否

SQLite Schema：否

Driver Framework：否

---

## 10. 預估影響

僅：

DeviceCenter Search

Device List

不影響：

Add

Edit

Delete

Driver

Live View

---

## 11. Architecture

維持：

View

↓

ViewModel

↓

Memory Filter

↓

UI

不得：

ViewModel

↓

SQLite

---

## 12. 驗收

可以搜尋：

Name

IP

Brand

Model

Clear 後恢復全部。

不得重新查 SQLite。