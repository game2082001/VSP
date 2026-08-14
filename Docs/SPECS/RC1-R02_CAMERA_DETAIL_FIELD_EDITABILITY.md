# RC1-R02 Camera Detail Field Editability

Status: Implementation Complete — Real-Device Validated — No Automated Test Coverage (see §6) — Pending Commit
Feature: Camera Management (Camera Detail)
Epic: Cross-references Epic-005 (Camera Management Workspace) and Epic-008 (Driver Settings UI) — not itself part of either Epic's original scope
Type: RC1 Post-Commit Remediation (defect found during RC1 Manual E2E Validation, not new feature work)
Remediation ID: RC1-R02 (referred to as "Defect 2" in `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md`)

---

# Process Note — Retroactive Specification

**This document was written after the implementation and real-device validation already existed in the working tree.** It does not describe a Spec-First → Task Plan → Approval → Implementation sequence — that sequence was not followed for this work, and this fix additionally went through one incorrect attempt before the correct one (see §2). This document's sole purpose is to retroactively establish Specification First traceability for Task-AI00A/Task-AI00B (RC1 Remediation Baseline Recovery), per Product Owner instruction. It records what already exists; it does not authorize, redesign, or re-approve any behavior. The original discovery/fix/withdrawal/re-fix narrative lives in `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md` ("Item E notes"); this document reformats that narrative into VSP's standard Spec structure.

---

# 1. Problem Statement

In Camera Detail's Add/Edit mode, driver-setting fields (Port, Username, RtspUrl, and other connection-type-specific fields rendered from `DriverSettingsDefinition`) stayed permanently read-only, for every `ConnectionType` (RTSP, ONVIF). A user could never type into these fields at all, regardless of Edit mode.

# 2. Root Cause

`CameraDetailWindow.xaml`'s shared `DetailValueStyle`/`EditableTextBoxStyle` resource styles originally bound their visibility-toggling `DataTrigger` to `{Binding IsEditMode}`. These styles are reused, via `BasedOn`, inside the driver-settings `ItemsControl` template, where each item's `DataContext` is a `DriverSettingEditorViewModel` instance — which has no `IsEditMode` property. The binding therefore silently failed to resolve, the trigger never fired, and the field stayed in its default (read-only) `Visibility`.

**This fix was attempted twice.** The first attempt added `RelativeSource={RelativeSource AncestorType=Window}` without a `DataContext.` prefix — this retargets the binding's *Source* to the ancestor `Window` element itself (which also has no `IsEditMode` property, since that property lives on the Window's `DataContext`, `CameraDetailViewModel`). That attempt did not fix the driver-setting fields, and additionally broke the previously-working Name/Model/IP Address/Location fields, which had relied on the original unqualified `{Binding IsEditMode}` correctly resolving against their own `DataContext` (`CameraDetailViewModel`) — redirecting the `Source` to the Window element broke that resolution for them too. This regression was caught by a Product Owner real-device retest against camera 192.168.0.89:1025, and the initial "Pass" report for this item was withdrawn.

# 3. Expected Behavior

The corrected binding, `{Binding DataContext.IsEditMode, RelativeSource={RelativeSource AncestorType=Window}}` (`VSP.UI/Views/CameraDetailWindow.xaml`, lines 27 and 36): the `RelativeSource` locates the ancestor `Window` element, and the `Path` then explicitly reads `IsEditMode` off that Window's `DataContext` (`CameraDetailViewModel`) — correct regardless of which element's own `DataContext` the trigger's host control sits inside. All fields — basic fields (Name/Model/IP Address/Location) and driver-setting fields (Port/Username/RtspUrl/etc., for every `ConnectionType`) — become editable in Edit mode and revert to read-only display outside it.

# 4. Acceptance Criteria

Derived from the checklist's Item E description — not newly invented for this document:

1. ONVIF `HttpPort` is editable in Camera Detail's Add and Edit modes.
2. RTSP `RtspPort` is editable in Camera Detail's Add and Edit modes.
3. Existing basic fields (Name, Model, IP Address, Location) continue to work exactly as before this fix — no regression from the corrected binding.
4. A Port value edited and saved persists correctly and is shown correctly when Camera Detail is reopened.

# 5. Implementation Mapping

| File | Change | Role |
|---|---|---|
| `VSP.UI/Views/CameraDetailWindow.xaml` | Modified, +11/-2 (lines 27, 36) | Two `DataTrigger` bindings corrected from `{Binding IsEditMode}` → `{Binding DataContext.IsEditMode, RelativeSource={RelativeSource AncestorType=Window}}` |

This is an XAML-only change. No ViewModel or code-behind file was modified for this specific fix (RC1-R01's `DriverSettingEditorViewModel.WasExplicitlyEdited` change is a separate, independent addition — see `RC1-R01_RTSP_PORT_RUNTIME_RESOLUTION.md`).

# 6. Test Mapping

**No automated test exists for this fix, and none is proposed by this document.** WPF `DataTrigger`/`RelativeSource` binding resolution is not exercised by this codebase's existing test infrastructure — consistent with the disclosed limitation already recorded in Epic-018's CHANGELOG entry ("this codebase has no STA test infrastructure"). This gap is stated here for transparency, not remedied — adding such test infrastructure is out of scope for RC1 remediation (see §8).

# 7. Real-Device Validation Evidence

Quoted directly from `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md`, "Item E notes (resolved)":

> **Defect 2 — Camera Port (and other driver-setting fields) not editable in Camera Detail UI — FIXED.** ... Fixed by adding `RelativeSource={RelativeSource AncestorType=Window}` to the two triggers. XAML-only change; verified manually for ONVIF HttpPort and RTSP RtspPort.

> Both fixes were reported verified in an initial Item E retest, but that Pass was **withdrawn** — a more rigorous manual retest against a real camera (192.168.0.89:1025) found the Camera Detail Port-editability fix did not actually work ... this incorrect fix additionally broke the previously-working Name field (and by the same mechanism, Model/IP Address/Location) ...

> **Corrected fix** (`{Binding DataContext.IsEditMode, RelativeSource={RelativeSource AncestorType=Window}}`) implemented, approved, and **confirmed Pass by Product Owner manual retest** against the real camera (192.168.0.89, port 1025): saved Camera Port persisted correctly after reopen. Item E closed as Pass.

This document does not re-execute or reinterpret that validation — it is cited as existing repository evidence. The confirmed-Pass retest covered Camera Port persistence specifically; it is the only real-device evidence in the repository for this fix, and this document does not extend that evidence to claim broader verification (e.g. of every basic field) than what was actually retested.

# 8. Out of Scope

- Any further change to `CameraDetailWindow.xaml`'s binding — this document is documentation-only.
- Adding automated (STA/UI Automation) test coverage for this binding — the gap identified in §6 is disclosed, not remediated, by this document.
- RC1-R01 (RTSP Port runtime resolution) and RC1-R03 (Live View `CurrentFrameSource` binding) — tracked independently, see their own Spec files.
- Defect 3 (ONVIF Media Stream URI Resolution) and Defect 4 (RTSP/ONVIF Playback Credential Propagation) — remain Validation Pending / BLOCKED, not covered here, not implied complete by this document.
- Commit — performed by the Product Owner.

# 9. Status

**Implementation Complete. Real-device validated (Product Owner manual retest, 192.168.0.89:1025, after one withdrawn attempt). No automated test coverage exists (disclosed limitation, not a defect in this document). Not yet committed.**
