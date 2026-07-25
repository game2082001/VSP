# Task-213 Batch Edit

Status: Implemented — Pending Review/Commit
Feature: Device Management
Epic: Epic-002 (EPIC-01 Device Management continuation)

---

# 1. Purpose

Allow selecting multiple cameras in the current Device Center camera list and applying shared field changes to all of them in one action.

Continues the `CameraListView` / `CameraListViewModel` / `CameraDetailViewModel` architecture (the clean, DI-friendly, tested pathway) — not the legacy `DeviceCenterViewModel` / `AddDeviceWindow` pathway, per `Docs/00_AI_CONTEXT.md`'s Legacy component rule.

---

# 2. Scope

Included:
- Multi-select checkbox per row in the camera list
- A "Batch Edit" action, enabled once 2 or more cameras are selected
- A dialog exposing optional-apply fields: Brand, Location, Username, Password — each with its own "apply" toggle, so only explicitly checked fields are changed
- Looping the existing `ICameraRepository.Update()` per selected camera (no repository interface change)

Not included:
- Batch-editing Name, IP Address, or ports (not sensible to set identically across devices)
- A new repository batch-update method (deferred unless proven necessary)
- Any SQLite schema change
- Legacy `DeviceView` / `AddDeviceWindow`

---

# 3. Architecture

```text
CameraListView (checkbox column, Batch Edit button)
    v
CameraListViewModel (SelectedItemCount, BatchEditCommand, RequestBatchEdit event)
    v
BatchEditWindow / BatchEditViewModel
    v
ICameraRepository.Update() (looped per selected camera)
```

---

# 4. Files

- `VSP.UI/ViewModels/CameraListItemViewModel.cs` (modified — add `IsSelected`)
- `VSP.UI/ViewModels/CameraListViewModel.cs` (modified — selection tracking, `BatchEditCommand`, `RequestBatchEdit` event)
- `VSP.UI/ViewModels/BatchEditViewModel.cs` (new)
- `VSP.UI/Views/BatchEditWindow.xaml` / `.xaml.cs` (new)
- `VSP.UI/Views/CameraListView.xaml` / `.xaml.cs` (modified — checkbox column, button, wiring)
- `VSP.Tests/Camera/BatchEditViewModelTests.cs` (new)

---

# 5. Out of Scope

- Batch Connection Test, Export, Device Status Enhancement (separate Tasks)
- Commit (performed by the user)
