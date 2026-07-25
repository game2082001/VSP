# Task-214 Batch Connection Test

Status: Implemented — Pending Review/Commit
Feature: Device Management
Epic: Epic-002 (EPIC-01 Device Management continuation)

---

# 1. Purpose

Allow testing connectivity for one or more selected cameras in the current Device Center camera list in a single action, reusing the existing Driver Framework (`DriverFactory` / `IDeviceDriver.TestConnection`) rather than creating a second connection-test mechanism.

Note: the `CameraListView` pathway currently has no connection-test capability at all (the only existing "Test Connection" button lives in the legacy `DeviceCenterViewModel`, wired to the legacy `AddDeviceWindow` pathway per `Docs/00_AI_CONTEXT.md`). This Task reuses the underlying Driver Framework only — not the legacy ViewModel/Window.

---

# 2. Scope

Included:
- A "Batch Test" action on the camera list, enabled once 1 or more cameras are selected
- A results dialog listing each tested camera's name, IP address, and Success/Failed outcome
- A shared `ICameraConnectionTester` service wrapping `DriverFactory.CreateCameraDriver(camera.ConnectionType).TestConnection(camera)`, reusable by this Task and Task-216 (Device Status Enhancement)

Not included:
- Any change to `DriverFactory`, `IDeviceDriver`, or existing driver implementations
- Live View, Snapshot, Recording
- Legacy `DeviceCenterViewModel` / `AddDeviceWindow`

---

# 3. Architecture

```text
CameraListView ("Batch Test" button)
    v
CameraListViewModel (BatchConnectionTestCommand, RequestBatchConnectionTest event)
    v
BatchConnectionTestWindow / BatchConnectionTestViewModel
    v
ICameraConnectionTester (new, VSP.Device/Services)
    v
DriverFactory.CreateCameraDriver(...).TestConnection(camera)  [existing, unmodified]
```

---

# 4. Files

- `VSP.Device/Services/ICameraConnectionTester.cs` (new)
- `VSP.Device/Services/CameraConnectionTester.cs` (new)
- `VSP.Device/Services/CameraConnectionTestResult.cs` (new)
- `VSP.UI/ViewModels/BatchConnectionTestViewModel.cs` (new)
- `VSP.UI/ViewModels/BatchConnectionTestItemViewModel.cs` (new)
- `VSP.UI/Views/BatchConnectionTestWindow.xaml` / `.xaml.cs` (new)
- `VSP.UI/ViewModels/CameraListViewModel.cs` (modified — `BatchConnectionTestCommand`, `RequestBatchConnectionTest` event)
- `VSP.UI/Views/CameraListView.xaml` / `.xaml.cs` (modified — button, wiring)
- `VSP.Tests/Camera/BatchConnectionTestViewModelTests.cs` (new)

---

# 5. Out of Scope

- Batch Edit, Export, Device Status Enhancement (separate Tasks)
- Commit (performed by the user)
