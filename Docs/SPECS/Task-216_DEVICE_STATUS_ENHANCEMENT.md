# Task-216 Device Status Enhancement

Status: Implemented — Pending Review/Commit
Feature: Device Management
Epic: Epic-002 (EPIC-01 Device Management continuation)

---

# 1. Purpose

Today `Camera.Status` is set once at creation time (`CameraStatus.Offline`) and never updated by anything in the `CameraListView` pathway — the Status column in the camera list is always stale. This Task makes the Batch Connection Test flow (Task-214) persist the tested connectivity result back onto each camera's `Status`, so the list's Status column reflects the last known connectivity rather than a permanent default.

Reuses `ICameraConnectionTester` exactly as anticipated by Task-214 §2 ("reusable by this Task and Task-216 (Device Status Enhancement)"). Continues the `CameraListView` / `CameraListViewModel` pathway, not the legacy `DeviceCenterViewModel` pathway, per `Docs/00_AI_CONTEXT.md`'s Legacy component rule.

---

# 2. Scope

Included:
- `BatchConnectionTestViewModel` (Task-214) additionally updates each tested camera's `Status` to `Online` or `Offline` based on the test result, and persists it via `ICameraRepository.Update()`
- After the Batch Connection Test dialog closes, `CameraListView` refreshes the camera list so updated statuses are visible in the Status column immediately

Not included:
- A dedicated single-camera "Test Connection" entry point outside the existing Batch Test flow (1 selected camera already runs through Batch Test, per Task-214 §2's "1 or more cameras" rule)
- New `CameraStatus` values (`Connecting`, `Error` remain unused by this Task)
- Any polling / background / scheduled status refresh
- Any change to `ICameraConnectionTester`, `DriverFactory`, or driver implementations
- Any SQLite schema change

---

# 3. Architecture

```text
CameraListView ("Batch Test" button, unchanged trigger)
    v
BatchConnectionTestViewModel(cameras, ICameraConnectionTester, ICameraRepository)
    v
For each camera: Test -> camera.Status = Online/Offline -> ICameraRepository.Update(camera)
    v
CameraListView.xaml.cs refreshes the list after the dialog closes
```

---

# 4. Files

- `VSP.UI/ViewModels/BatchConnectionTestViewModel.cs` (modified — accepts `ICameraRepository`, persists `Status`)
- `VSP.UI/Views/CameraListView.xaml.cs` (modified — passes `_cameraRepository`, refreshes list after dialog closes)
- `VSP.Tests/Camera/BatchConnectionTestViewModelTests.cs` (modified — updated constructor signature, new status-persistence assertions)

---

# 5. Out of Scope

- Batch Edit, Export (separate, already-completed Tasks)
- Device Group, Favorite, Device Log (separate, not-yet-started Device Management items)
- Commit (performed by the user)
