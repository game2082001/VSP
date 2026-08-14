# RC1-R01 RTSP Port Runtime Resolution

Status: Implementation Complete — Test Complete — Real-Device Validated — Pending Commit
Feature: Camera Management (Camera Detail / RTSP Driver)
Epic: Cross-references Epic-005 (Camera Management Workspace) and Epic-007 (Camera Connectivity Foundation) — not itself part of either Epic's original scope
Type: RC1 Post-Commit Remediation (defect found during RC1 Manual E2E Validation, not new feature work)
Remediation ID: RC1-R01 (referred to as "Defect 1" in `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md`)

---

# Process Note — Retroactive Specification

**This document was written after the implementation, tests, and real-device validation already existed in the working tree.** It does not describe a Spec-First → Task Plan → Approval → Implementation sequence — that sequence was not followed for this work. This document's sole purpose is to retroactively establish Specification First traceability for Task-AI00A/Task-AI00B (RC1 Remediation Baseline Recovery), per Product Owner instruction. It records what already exists; it does not authorize, redesign, or re-approve any behavior. The original discovery/fix narrative lives in `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md` ("Item E notes"); this document reformats that narrative into VSP's standard Spec structure and adds explicit Acceptance Criteria that did not previously exist as a standalone artifact.

---

# 1. Problem Statement

`Camera.RtspPort` is a persisted field, but before this fix it was never actually consulted at runtime:

- `RtspCameraDriver.TestConnection` parsed `camera.RtspUrl` directly via `Uri.TryCreate` and, when the URL had no explicit port, fell back to a hardcoded `554` inside its private `SendDescribe` helper — `camera.RtspPort` was never read.
- Nothing in the Camera Detail Save path reconciled `RtspPort` against whatever port (if any) was embedded in `RtspUrl`.

Net effect: a user could set a non-default RTSP Port in Camera Detail and see no corresponding change in actual connection behavior, because the field was cosmetic.

# 2. Root Cause

There was no single, shared notion of "the effective RTSP endpoint" for a camera. The URL's own embedded port and the separate `Camera.RtspPort` field were two independent, uncoordinated sources of truth, each consulted (or not) differently by each call site (`TestConnection`, Save, Live View connect).

# 3. Expected Behavior

Implemented via `VSP.Domain/RtspEndpointResolver.cs` (28 lines, static `TryResolve(string? rtspUrl, int rtspPort, out Uri effectiveUri)`):

- If `RtspUrl` already contains an explicit port, that port wins (preserves legacy records and anything already normalized by Camera Detail).
- If `RtspUrl` has no port, `rtspPort` is used as the fallback.

On Save (`CameraDetailViewModel.NormalizeRtspEndpoint`, lines 508+):

- If the user explicitly edited the RTSP Port field during this edit session (`DriverSettingEditorViewModel.WasExplicitlyEdited`, set unconditionally at the top of the `Value` setter, before `SetProperty`'s equality check — line 36), that value is authoritative and `RtspUrl` is rewritten to match, even if the newly entered value equals the port the URL already had.
- Otherwise, an explicit port already embedded in `RtspUrl` is preserved, and `RtspPort` is synced to match it, so the two fields agree from that Save onward.

# 4. Acceptance Criteria

Derived from the existing regression tests (§6) and the checklist's Item E description — not newly invented for this document:

1. A camera with a legacy `RtspUrl` containing an explicit port, whose Port field is not touched this session, has `RtspPort` synced to that URL's port on Save, and the URL itself is left unchanged.
2. Explicitly re-entering the Port field with the value it already displays (e.g. `554` → `554`) still counts as an explicit edit — the value becomes authoritative and the URL is normalized to match, not silently treated as "unedited."
3. Explicitly editing the Port field to a non-default value makes it authoritative; the URL is rewritten to carry that port.
4. For a new camera, setting the RTSP Port once (without separately typing the same port into the RTSP URL) is sufficient — the URL is normalized to include it on Save.
5. Programmatic re-seeding of driver settings (construction, or a `ConnectionType` switch rebuilding the settings list) must never itself mark a field as explicitly edited.
6. `RtspCameraDriver.TestConnection` must resolve the effective endpoint via `RtspEndpointResolver` (not a hardcoded `554` fallback), honoring a configured `RtspPort` when the URL has none, and preferring the URL's own port when the URL already specifies one.

# 5. Implementation Mapping

| File | Change | Role |
|---|---|---|
| `VSP.Domain/RtspEndpointResolver.cs` | New, 28 lines | Shared `TryResolve` logic (§3) |
| `VSP.Device/Drivers/RTSP/RtspCameraDriver.cs` | Modified, +13/-6 (lines 28–81) | `TestConnection` now calls `RtspEndpointResolver.TryResolve` instead of raw `Uri.TryCreate` + hardcoded 554; uses the resolved `endpointUri.Port` in `SendDescribe`'s TCP transport call |
| `VSP.UI/ViewModels/CameraDetailViewModel.cs` | Modified, +36 (lines 497, 508+) | New `NormalizeRtspEndpoint` method, called from the existing Save path |
| `VSP.UI/ViewModels/DriverSettingEditorViewModel.cs` | Modified, +13 (lines 36, 54) | New `WasExplicitlyEdited` flag |

# 6. Test Mapping

| Test File | Tests | Coverage |
|---|---|---|
| `VSP.Tests/Domain/RtspEndpointResolverTests.cs` (new) | 4 `[Fact]`/`[Theory]` (lines 9, 18, 27, 39) | `TryResolve` URL-port-wins, fallback-to-configured-port, invalid-URL cases |
| `VSP.Tests/Drivers/RTSP/RtspCameraDriverTests.cs` (modified, +30) | `TestConnection_UsesConfiguredRtspPort_WhenUrlHasNoPort`, `TestConnection_PrefersExplicitUrlPort_OverConfiguredRtspPort` | AC #6 |
| `VSP.Tests/Camera/CameraDetailViewModelTests.cs` (modified, +125, lines 686–800) | `SaveCommand_RtspPortNotEdited_PreservesLegacyUrlPortAndSyncsRtspPort` (686), `SaveCommand_RtspPortExplicitlyEditedToSameValue_BecomesAuthoritativeAndNormalizesUrl` (707), `SaveCommand_RtspPortExplicitlyEditedToNonDefault_BecomesAuthoritativeAndNormalizesUrl` (730), `SaveCommand_NewCamera_ExplicitRtspPort_DoesNotRequireEnteringPortTwice` (750), `DriverSettingEditorViewModel_Construction_DoesNotMarkExplicitlyEdited` (769), `SelectingConnectionType_SeededRtspPort_IsNotMarkedExplicitlyEdited` (779), `ChangingConnectionType_RebuiltSetting_ValueSurvivesButExplicitEditFlagResets` (789) | AC #1–#5 |

All tests confirmed passing as part of the 850/0/0 full-suite run referenced in Task-AI00A and reconfirmed in Task-AI00B Phase 1.

# 7. Real-Device Validation Evidence

Quoted directly from `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md`, "Item E notes (resolved)":

> **Defect 1 — RtspPort not honored at runtime — FIXED.** `RtspPort` was persisted but never consulted by `RtspCameraDriver.TestConnection`/`LiveViewViewModel` (hardcoded `554` fallback / raw `RtspUrl` passthrough). Fixed via `RtspEndpointResolver` + session-edit-intent-aware save normalization in `CameraDetailViewModel`. Verified with regression tests and manual retest.

And, from the same document's closing line for Item E:

> **Corrected fix** ... implemented, approved, and **confirmed Pass by Product Owner manual retest** against the real camera (192.168.0.89, port 1025): saved Camera Port persisted correctly after reopen. Item E closed as Pass.

This document does not re-execute or reinterpret that validation — it is cited as existing repository evidence.

# 8. Out of Scope

- Any change to `RtspEndpointResolver`, `RtspCameraDriver`, `CameraDetailViewModel`, or `DriverSettingEditorViewModel` beyond what already exists in the working tree — this document is documentation-only.
- RC1-R02 (Camera Detail field editability) and RC1-R03 (Live View `CurrentFrameSource` binding) — tracked independently, see their own Spec files.
- Defect 3 (ONVIF Media Stream URI Resolution) and Defect 4 (RTSP/ONVIF Playback Credential Propagation) — remain Validation Pending / BLOCKED, not covered here, not implied complete by this document.
- Commit — performed by the Product Owner.

# 9. Status

**Implementation Complete. Test Complete. Real-device validated (Product Owner manual retest, 192.168.0.89:1025). Documentation now traced by this Spec. Not yet committed.**
