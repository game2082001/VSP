# Epic-008 Driver Settings UI

Status: Approved
Feature: Driver Framework / Camera Detail
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

---

# Approval Record

- Selected by the Product Owner from a neutral, non-recommended Candidate Analysis covering six candidates (Hikvision Connectivity, Live View MVP, Dahua NetSDK Connectivity, Legacy Cleanup, Driver Settings UI, Discovery Session History UI).
- Approved with one Definition of Done extension: every driver shall render its settings exclusively from `DriverSettingsDefinition`; no implemented driver may require hardcoded driver-specific editor logic inside Camera Detail.
- Approved by: Product Owner (this conversation).
- Execution mode: Autonomous within this Epic, per `AUTONOMOUS_DEVELOPMENT.md` §7, until Epic Review or a defined Stop Condition (`AI_OPERATING_SYSTEM.md` §8).

---

# Objective

Make `CameraDetailWindow` render each driver's editable settings purely from its `DriverSettingsDefinition` metadata (Task-303, backend-only until now), instead of six hardcoded fields shown identically regardless of the selected driver.

---

# Current-State Analysis Summary

(Full analysis produced before implementation; summarized here for the record.)

- Verified zero references to `DriverSettingsDefinition`/`DriverSettingKey` anywhere in `VSP.UI`. `CameraDetailViewModel` hardcodes six fixed properties (`HttpPort`, `RtspPort`, `SdkPort`, `Username`, `Password`, `RtspUrl`), each always shown with generic validation, regardless of which driver applies — verified by reading `CameraDetailViewModel.cs` and `CameraDetailWindow.xaml` in full.
- Per-driver settings are a real, meaningful subset of those six (RTSP needs `RtspPort`+`Username`+`Password`+`RtspUrl`; ONVIF needs `HttpPort`+`Username`+`Password`; Hikvision/Dahua need `HttpPort`+`SdkPort`+`Username`+`Password`) — the current UI ignores this entirely.
- **Structural blocker found, not anticipated at proposal time:** there is no `ConnectionType` selector anywhere in Camera Detail. `ConnectionType` is set once to `Unknown` in `CreateNewCamera()` and never changed by the ViewModel — verified by full-file read. Every manually-added camera today silently resolves to the RTSP driver via `DriverFactory`'s fallback, regardless of the chosen `Brand`. Rendering settings "exclusively from `DriverSettingsDefinition`" requires knowing which definition applies; nothing in the current UI reliably provides that. Adding a `ConnectionType` selector is treated as a necessary implementation detail of the approved DoD (Implementation Authority), not a separate feature — `Brand`'s relationship to `ConnectionType` is left exactly as independent/undefined as it is today; not unified as part of this Epic.
- `CameraDetailViewModelTests.cs`: 637 lines, 26 tests, 37 references to the six fields being replaced — quantified, not estimated.
- `DriverSettingDefinition` has no value-format concept (port vs. text vs. URL) — only `IsRequired`/`IsSensitive`/`DefaultValue`. Needed for validation to also come from metadata rather than a UI-side switch on field identity.

---

# Architecture Review Summary

- No SQLite schema change: `Camera` keeps its fixed columns (`HttpPort`/`RtspPort`/`SdkPort`/`Username`/`Password`/`RtspUrl`). This Epic is UI-layer only — a generic renderer bridged onto the existing fixed schema.
- Added `DriverSettingValueKind` (`Text`/`Port`/`Url`) to `DriverSettingDefinition` (additive, default `Text`, non-breaking to `BuiltInCameraDriverPlugin`) so validation format is also metadata-driven, not a UI-side switch on `DriverSettingKey` identity.
- New `DriverSettingEditorViewModel` per definition entry (`VSP.UI.ViewModels`), collected into `CameraDetailViewModel.DriverSettings`, rebuilt when `ConnectionType` changes (values preserved by matching `DriverSettingKey` across the rebuild where possible).
- View: one generic `ItemsControl`/`DataTemplate` over `DriverSettings` — `IsSensitive` (from the item) switches TextBox/PasswordBox; `IsEditMode` (reached via standard `RelativeSource AncestorType=Window` ambient binding) switches display/edit. Zero per-driver or per-key conditionals in the template.
- One unavoidable `DriverSettingKey`-keyed bridge remains: committing the generic collection's values onto `Camera`'s fixed typed properties and reading initial values from `Camera`. This switches on the shared `DriverSettingKey` vocabulary (identical across every driver), not on driver/brand identity — persistence-mapping glue, not editor logic.
- Risk Classification: MEDIUM (substantial rewrite of an existing, tested UI component; no schema change, no public API break, no new package, no security change).

---

# Scope Boundary

**In scope:**
- `DriverSettingValueKind` + `DriverSettingDefinition` extension; `BuiltInCameraDriverPlugin` tagging.
- `DriverSettingEditorViewModel`.
- `CameraDetailViewModel`: `ConnectionType` selector, `DriverSettings` collection replacing the six hardcoded fields, rebuild-on-change with value preservation, updated `IsFormValid`/`IsDirty`/`MapToCamera`.
- `CameraDetailWindow.xaml`/`.xaml.cs`: Connection Type row, generic settings `ItemsControl`, generic PasswordBox sync.
- Full test suite update for the above.

**Explicitly not decided/introduced:** any relationship between `Brand` and `ConnectionType` beyond what exists today (both remain independently editable).

---

# Risk Ceiling

**MEDIUM.** No database schema change, no public API break, no new external package, no security-model change, no DI container introduction.

---

# Definition of Done

1. Every driver's settings in Camera Detail render exclusively from its `DriverSettingsDefinition` — no hardcoded driver-specific editor logic remains inside Camera Detail (Product Owner's DoD extension, verified by inspection: zero per-driver/per-key conditionals in `CameraDetailWindow.xaml`/`.xaml.cs`/`CameraDetailViewModel.cs` outside the single generic `DriverSettingKey`-keyed persistence bridge).
2. A `ConnectionType` selector exists and drives which definition is rendered.
3. All new/changed code has unit test coverage; the full existing suite remains green; build stays passing; `Docs/CHANGELOG.md` updated.

---

# Out of Scope

- Any SQLite schema change.
- Any relationship/auto-mapping between `Brand` and `ConnectionType`.
- Any change to driver `TestConnection`/`GetDeviceInformation` behavior (Epic-007 territory, untouched).
- Any change to `DriverRegistry`/`DriverFactory`/`BuiltInCameraDriverPlugin`'s registration behavior beyond adding `ValueKind` tags.

---

# Implementation Strategy

Bottom-up: `DriverSettingValueKind`/`DriverSettingDefinition` extension first, then `DriverSettingEditorViewModel`, then `CameraDetailViewModel` rewrite, then `CameraDetailWindow` rewrite, then the test suite update (fixture + ~15+ rewritten assertions + new coverage), then CHANGELOG/Epic Review. Build + relevant tests after each step; full suite before Epic Review.
