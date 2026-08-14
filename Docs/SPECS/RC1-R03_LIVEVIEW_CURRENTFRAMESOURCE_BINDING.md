# RC1-R03 Live View CurrentFrameSource Binding

Status: Implementation Complete — Test Complete — Validation Pending
Feature: Live View (not Camera Management — tracked separately from RC1-R01/RC1-R02 for this reason)
Epic: Cross-references Epic-010 (Live View Foundation) — Epic-010 has no discoverable Task-level Spec file under `Docs/SPECS/`; this is the first Spec-level document for Live View found in this repository
Type: RC1 Post-Commit Remediation (defect found alongside Item F's real-device retest, fixed and unit-tested, but never named or narrated in `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md`)
Remediation ID: RC1-R03

---

# Process Note — Retroactive Specification (independent of Defect 3 / Defect 4 / Item F)

**This document was written after the implementation and tests already existed in the working tree.** It does not describe a Spec-First → Task Plan → Approval → Implementation sequence — that sequence was not followed for this work.

**This fix is deliberately documented independently, per explicit Product Owner instruction in Task-AI00B.** The code for this fix (`LiveViewViewModel.HandleFrameRendered` and its subscribe/unsubscribe wiring) sits in the same file, and was discovered in the same working-tree state, as the Defect 3 (ONVIF Media Stream URI Resolution) and Defect 4 (RTSP/ONVIF Playback Credential Propagation) fixes — both of which remain **Validation Pending / BLOCKED** pending a Product Owner real-device retest, per `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md`'s "Item F notes." Unlike Defect 3/4, this fix is **not mentioned anywhere in that document's text** — Task-AI00A discovered it independently by comparing the `git diff` against the checklist narrative, not by reading the checklist itself. This document exists specifically so that this fix has its own traceable identity and does not stay hidden inside, or get assumed to share the blocked status of, Defect 3/4 or the general "Item F" narrative. Its status is derived solely from repository evidence (code + tests), not from any Item F conclusion.

---

# 1. Problem Statement

In Live View, the decoded video could be rendering correctly underneath the UI the entire time, yet the on-screen image would never visibly update past whatever it showed at the moment the connection reached `Connected` (typically nothing/blank, since no frame exists yet at that instant).

# 2. Root Cause

`IFrameRenderer.CurrentFrameSource` (`VSP.Player/Interfaces/IFrameRenderer.cs`) is updated in place by `WpfFrameRenderer` (`VSP.Player/Renderer/WpfFrameRenderer.cs`) on every decoded frame — the property returns the same object identity across frames, only its internal pixel content changes. WPF data binding only re-reads a bound property when that property's `INotifyPropertyChanged.PropertyChanged` event fires; an in-place mutation with no identity change and no explicit notification is invisible to the binding system.

`WpfFrameRenderer` already exposes a `FrameRendered` event (`event EventHandler? FrameRendered`, `VSP.Player/Interfaces/IFrameRenderer.cs` line 10; raised in `VSP.Player/Renderer/WpfFrameRenderer.cs` line 75) for exactly this purpose. **Both the property and the event already existed before this fix and were not modified by it** — `IFrameRenderer.cs` and `WpfFrameRenderer.cs` do not appear in this working tree's `git diff` at all. The defect was that `LiveViewViewModel` never subscribed to `FrameRendered`, so `CurrentFrameSource`'s `PropertyChanged` was never raised, and the bound `Image` control's source stayed frozen at its initial (null) value indefinitely, even while frames kept rendering successfully underneath it.

# 3. Expected Behavior

`LiveViewViewModel.cs`:

- `StartControllerWithUrl` (line 169) subscribes `controller.Renderer.FrameRendered += HandleFrameRendered` (line 175) when a controller is created.
- `HandleFrameRendered` (line 302) marshals to the UI dispatcher and raises `OnPropertyChanged(nameof(CurrentFrameSource))`.
- `DetachController` (line 271) symmetrically unsubscribes (`_controller.Renderer.FrameRendered -= HandleFrameRendered`, line 279), so a late event from an already-detached controller's renderer can never reach a `LiveViewViewModel` instance that has moved on to a different controller.

# 4. Acceptance Criteria

Derived from the existing regression tests (§6) — not newly invented for this document, and not sourced from any Item F narrative since none exists for this fix:

1. When the active controller's renderer raises `FrameRendered`, `LiveViewViewModel` must raise `PropertyChanged` for `CurrentFrameSource`.
2. After a controller is detached, that controller's renderer raising `FrameRendered` must **not** raise `PropertyChanged` on the (now former) `LiveViewViewModel` — the subscription must actually be removed, not merely superseded.
3. If an old, already-detached controller's renderer fires `FrameRendered` after a new controller has been attached, it must not be able to overwrite the current `CurrentFrameSource` — only the currently-attached controller's renderer may update it.

# 5. Existing Implementation Mapping

| File | Change | Role |
|---|---|---|
| `VSP.UI/ViewModels/LiveViewViewModel.cs` | Modified (part of this file's overall +105/-2 diff, which also contains the separate, independently-tracked Defect 3/4 changes) | `HandleFrameRendered` (line 302), subscribe at line 175, unsubscribe at line 279 |

No change to `VSP.Player/Interfaces/IFrameRenderer.cs` or `VSP.Player/Renderer/WpfFrameRenderer.cs` — both are pre-existing, unmodified infrastructure from Epic-010.

**Scope note:** `LiveViewViewModel.cs`'s diff also contains the ONVIF branching (`LoadOnvifCamera`) and credential-attachment (`StartAuthenticatedController`, `RtspCredentialUriBuilder` usage) changes that belong to Defect 3 and Defect 4 respectively. This document intentionally cites only the `FrameRendered`/`HandleFrameRendered`/`CurrentFrameSource` lines. Defect 3/4 are out of scope here (§8) and remain tracked solely under their own, not-yet-written remediation documents pending real-device validation.

# 6. Existing Test Mapping

| Test File | Tests | Coverage |
|---|---|---|
| `VSP.Tests/Player/LiveViewViewModelTests.cs` (modified, part of this file's overall +324/-1 diff) | `FrameRendered_RaisesPropertyChangedForCurrentFrameSource` (line 287), `FrameRendered_AfterControllerDetached_SubscriptionIsRemoved` (line 316), `FrameRendered_FromOldDetachedController_CannotUpdateCurrentViewModel` (line 347) | AC #1, #2, #3 respectively |

All three tests confirmed passing as part of the 850/0/0 full-suite run referenced in Task-AI00A and reconfirmed in Task-AI00B Phase 1. Like §5, this file's diff also contains separate tests for Defect 3/4, which are not cited here.

# 7. Validation Evidence

**No real-device validation evidence exists in the repository for this specific fix.** `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md` was searched in full (Task-AI00A) and contains no mention of `CurrentFrameSource`, `FrameRendered`, or any description matching this defect — under either the Item E or Item F notes. The only evidence available is the unit-test coverage in §6. Because Item F's overall row in the checklist (`# F | Live View | ... | ☒ FAIL / BLOCKED`) is where this fix would visually manifest during a real-device retest, this fix's actual behavior on real hardware is, as a practical matter, likely to be exercised whenever Defect 3/4's pending retest happens — but no such retest has occurred, and this document does not assume or forecast its outcome.

**Per Product Owner instruction, this status is capped accordingly and must not be represented as "Product Owner Accepted" until such evidence exists in the repository.**

# 8. Out of Scope

- Any further change to `LiveViewViewModel.cs`'s `FrameRendered` wiring, `IFrameRenderer`, or `WpfFrameRenderer` — this document is documentation-only.
- Defect 3 (ONVIF Media Stream URI Resolution) and Defect 4 (RTSP/ONVIF Playback Credential Propagation) — remain Validation Pending / BLOCKED, not covered here, not implied complete by this document, and not to be conflated with this fix's own (also-pending) validation status.
- RC1-R01 (RTSP Port runtime resolution) and RC1-R02 (Camera Detail field editability) — tracked independently, see their own Spec files.
- Item F diagnostic instrumentation (`MediaController.cs`, `FfmpegVideoDecoder.cs`, `RtspMediaSession.cs`) — unrelated to this fix, retained as-is per Task-AI00B instruction.
- Requesting or performing a real-device retest — that is a Product Owner action; not requested or scheduled by this document.
- Commit — performed by the Product Owner.

# 9. Status

**Implementation Complete. Test Complete. Validation Pending — no real-device evidence exists in the repository. Not "Product Owner Accepted." Not yet committed.**
