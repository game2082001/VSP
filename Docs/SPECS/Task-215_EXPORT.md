# Task-215 Export

Status: Implemented — Pending Review/Commit
Feature: Device Management
Epic: Epic-002 (EPIC-01 Device Management continuation)

---

# 1. Purpose

Allow exporting the camera list currently shown in the Device Center (respecting active search/filter) to a CSV file, using the same column layout as `CsvImportParser` so an exported file can be re-imported without transformation.

Continues the `CameraListView` / `CameraListViewModel` pathway, not the legacy `DeviceCenterViewModel` pathway, per `Docs/00_AI_CONTEXT.md`'s Legacy component rule.

---

# 2. Scope

Included:
- An "Export" action on the camera list, enabled whenever the filtered list is non-empty (no selection required — this exports the current view, unlike the selection-based Batch actions)
- A CSV writer producing the same header row as `CsvImportParser.RequiredHeaders` (Name, Brand, Model, IP Address, HTTP Port, RTSP Port, SDK Port, Username, Password, Connection Type, RTSP URL, Location), one row per camera in the current filtered view
- A native Save File dialog for choosing the destination path, mirroring the existing `ImportFileSelector` pattern (`VSP.UI/Helpers`)
- A success/failure `MessageBox`, consistent with existing code-behind dialogs in this pathway (e.g. `CameraDetailWindow.xaml.cs`)

Not included:
- Excel (`.xlsx`) export (CSV only; Excel import exists but export does not need to mirror every import format)
- Any change to `CsvImportParser`, the Import pipeline, or `ICameraRepository`
- Any SQLite schema change
- Legacy `DeviceView` / `AddDeviceWindow` / `DeviceCenterViewModel`

Note: exported CSV rows include the `Password` column in plaintext, matching the existing Import CSV contract (`CsvImportParser` already reads/writes this column today). This is an existing data-handling characteristic of the Import/Export CSV format, not a new risk introduced by this Task.

---

# 3. Architecture

```text
CameraListView ("Export" button)
    v
CameraListViewModel (ExportCommand, RequestExport event exposing current filtered Cameras)
    v
CameraListView.xaml.cs (code-behind: ExportFileSelector -> CameraExportWriter -> File.WriteAllText)
    v
CameraExportWriter (new, VSP.Device/Export) — pure IReadOnlyList<Camera> -> CSV string
```

---

# 4. Files

- `VSP.Device/Export/CameraExportWriter.cs` (new)
- `VSP.UI/Helpers/ExportFileSelector.cs` (new — mirrors `ImportFileSelector`)
- `VSP.UI/ViewModels/CameraListViewModel.cs` (modified — `ExportCommand`, `RequestExport` event)
- `VSP.UI/Views/CameraListView.xaml` / `.xaml.cs` (modified — button, wiring)
- `VSP.Tests/Export/CameraExportWriterTests.cs` (new)

---

# 5. Out of Scope

- Batch Edit, Batch Connection Test, Device Status Enhancement (separate Tasks)
- Commit (performed by the user)
