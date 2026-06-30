# S1-8 Connection Test

Status

Planned

---

# Objective

完成 Device Center 的 Connection Test 功能。

使用者可選擇一台 Camera，透過目前設定的 Connection Type 呼叫對應 Driver 進行連線驗證，並回傳測試結果。

---

# Scope

包含：

- Test Button
- Driver Connection Test
- Success / Failed Result
- 基本錯誤訊息
- Device 未選取提示

不包含：

- Live View
- Snapshot
- Recording
- Driver Profile
- SDK 完整功能
- Device Discovery

---

# Modified Files

預計：

- DeviceCenterView.xaml
- DeviceCenterViewModel.cs

若目前已有 Driver Factory 或 Driver Interface，可直接使用。

---

# New Files

原則上：

無

若目前專案尚未存在 Driver Interface，可建立最小必要實作。

---

# UI

Button：

Add

Edit

Delete

Test

Refresh

---

# Flow

Selected Device

↓

Connection Test

↓

DriverFactory

↓

IDeviceDriver

↓

TestConnection(Camera)

↓

ConnectionTestResult

↓

UI

---

# Driver

依 Camera.ConnectionType 呼叫：

- RTSP
- ONVIF
- HikvisionSDK
- HikvisionISAPI
- DahuaSDK

若 Driver 尚未完成：

回傳：

Driver not implemented.

不得直接回傳 Success。

---

# Success Message

Connection Success

顯示：

- IP
- Driver
- Response Time

若 Driver 可取得：

- Model
- Firmware

---

# Failed Message

Connection Failed

可能原因：

- Authentication Failed
- Timeout
- Driver not implemented
- Connection Error

---

# ViewModel

新增：

ConnectionTestCommand

不得直接呼叫 SDK。

必須透過 Driver。

---

# Device Selection

未選設備：

Please select a device.

不得開始測試。

---

# Architecture

View

↓

ViewModel

↓

DriverFactory

↓

IDeviceDriver

↓

Driver

↓

ConnectionTestResult

---

# Not Included

- MainWindow
- Repository
- SQLite Schema
- DeviceService（除非現有介面不足）
- Live View
- Playback
- Recording

---

# Acceptance Criteria

✓ Test Button 可使用

✓ 未選設備會提示

✓ 可依 ConnectionType 呼叫 Driver

✓ Driver 尚未完成可回傳 Driver not implemented

✓ 成功可顯示 Connection Success

✓ 失敗可顯示 Connection Failed

✓ Build Success

✓ 不修改 Repository

✓ 不修改 SQLite Schema

✓ 不修改 MainWindow

✓ 符合 MVVM

# Sprint 1-8

## Connection Test

Status

Completed

---

## Goal

Provide Connection Test in Device Center.

Reuse existing DriverFactory and IDeviceDriver.

Do not create another connection framework.

---

## Scope

Completed

- Connection Test button
- TestConnectionCommand
- DriverFactory integration
- IDeviceDriver.TestConnection(Camera)
- Success message
- Failed message
- Driver not implemented message

---

## Files Changed

- DeviceCenterView.xaml
- DeviceCenterViewModel.cs

---

## Files NOT Changed

- MainWindow
- Repository
- SQLite Schema
- Driver Framework

---

## Flow

Selected Device

↓

DriverFactory

↓

IDeviceDriver

↓

TestConnection(Camera)

↓

Result

↓

Connection Success

or

Connection Failed

or

Driver not implemented

---

## Acceptance

✓ Device selected before testing

✓ Existing DriverFactory reused

✓ Existing IDeviceDriver reused

✓ No SQLite changes

✓ No Repository changes

✓ Build Success

✓ 0 Errors

---

Status

Completed