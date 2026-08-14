# RC1-R05 Playback Frame Presentation Lifecycle

Status: Implementation Complete — Test Complete — Real-Device Validated — PASS — CLOSED (2026-08-14)
Feature: Playback (Epic-012 Playback Foundation)
Type: RC1 Post-Commit Remediation — defect found via Product Owner real-device retest of Item H (Playback), immediately after RC1-R04 (Recording/Playback Storage Contract) was confirmed real-device Pass
Remediation ID: RC1-R05
Task: Task-AI00B

---

# 1. Real-Device Symptom

Product Owner real-device retest, repeatable pattern:

1. Select camera and recording, click **Play** → the position/timeline advances, but the video area stays **black**.
2. Click **Pause** → a valid, current video frame **immediately becomes visible**.
3. Click **Play** again (Resume) → video **plays normally** with moving image.
4. Click **Stop**, then **Play** again (same or different recording) → position advances again, video area is **black again** — the exact same pattern repeats, deterministically.

Confirmed not a recording, storage-contract (RC1-R04), camera, RTSP, or ONVIF issue: the recording is discovered correctly and the media pipeline visibly renders and plays correctly once Pause→Resume has occurred once.

# 2. Root Cause

`PlaybackViewModel` never subscribed to `controller.Renderer.FrameRendered`. `PlayAsync` wired only `controller.StateChanged += HandleControllerStateChanged;` — no `FrameRendered` subscription existed anywhere in the file, and `DetachController` correspondingly never unsubscribed one.

`WpfFrameRenderer.CurrentFrameSource` (`VSP.Player/Renderer/WpfFrameRenderer.cs`) is a plain auto-property backed by a lazily-allocated `WriteableBitmap` (`_bitmap`, starts `null`, allocated on the first `RenderOnUiThread` call). Per its own doc comment, `WritePixels` updates the existing bitmap in place, so a bound `Image` control repaints automatically **once attached** — but attaching requires exactly one `PropertyChanged` pulse fired *after* the bitmap exists. `PlaybackController.OpenAndReadAsync` transitions `Connecting → Connected` before the first frame decodes (mirroring the exact pre-RC1-R03 Live View race, `LiveViewViewModelTests.cs:399-402`). With no `FrameRendered` subscription, the only source of `PropertyChanged(CurrentFrameSource)` in `PlaybackViewModel` was `RaiseAllChanged()`, invoked at controller-construction time (bitmap still null) and on `StateChanged` transitions:

- **Play**: Connected-transition pulse reads `CurrentFrameSource` while still `null` → `Image.Source` binds to `null` → black, while `_clock.Advance()`/position timer continue independently in the background (proves decode is healthy; proves nothing about rendering).
- **Pause**: the Paused-transition pulse fires *after* several seconds of background decoding — `_bitmap` now exists and holds a real frame — the binding successfully attaches to it → frame becomes visible.
- **Resume**: the Connected-transition pulse re-confirms the (already-correct, same-object) attachment; `WritePixels`'s own dirty-rect invalidation keeps painting every subsequent frame with no further `PropertyChanged` needed → smooth playback.
- **Stop → Play**: a brand-new `PlaybackController`/`WpfFrameRenderer`/`_bitmap` is constructed (correct disposal, no stale-renderer leak) — the entire race repeats from a clean slate, deterministically.

# 3. Comparison with RC1-R03

Identical defect class to RC1-R03 (Live View `CurrentFrameSource` binding), in the sibling ViewModel that never received the same fix:

| | Live View (fixed, RC1-R03) | Playback (broken, pre-fix) |
|---|---|---|
| Subscribes `Renderer.FrameRendered` | Yes — `HandleFrameRendered` | No |
| Unsubscribes on detach | Yes | No (nothing to unsubscribe) |
| `IFrameRenderer`/`WpfFrameRenderer` | Unmodified by RC1-R03, unmodified here | Unmodified |

# 4. Minimum Fix Implemented

Confined entirely to `VSP.UI/ViewModels/PlaybackViewModel.cs`, reusing the exact RC1-R03 pattern:

1. `PlayAsync`, alongside `controller.StateChanged += HandleControllerStateChanged;`, now also does `controller.Renderer.FrameRendered += HandleFrameRendered;`.
2. New `HandleFrameRendered(object? sender, EventArgs e)` — dispatches to the UI thread and calls `OnPropertyChanged(nameof(CurrentFrameSource))`, same shape as `LiveViewViewModel.HandleFrameRendered`.
3. `DetachController` now also does `_controller.Renderer.FrameRendered -= HandleFrameRendered;` before the controller is discarded/replaced.

No change to `PlaybackController`, `RecordedFileMediaSession`, `FfmpegVideoDecoder`, `WpfFrameRenderer`, `IFrameRenderer`, `PlaybackView.xaml`, `RecordingCatalog`, `RecordingPathProvider`, `MediaController`, `LiveViewViewModel`, RC1-R04 storage behavior, RTSP/ONVIF/authentication, recording format/path contract, or playback clock/position/seek logic — all confirmed unmodified.

# 5. Acceptance Criteria

1. When the active controller's renderer raises `FrameRendered`, `PlaybackViewModel` raises `PropertyChanged` for `CurrentFrameSource`.
2. After Stop → Play replaces the controller, the old controller's renderer raising `FrameRendered` must not raise `PropertyChanged` on the current `PlaybackViewModel` — the subscription must actually be removed, not merely superseded.
3. If an old, already-detached controller's renderer fires `FrameRendered` after a new controller has been attached, it must not overwrite `CurrentFrameSource` — only the currently-attached controller's renderer may update it.
4. Existing Play/Pause/Resume/Stop, `CanExecute`, status-message, and Seek behavior unchanged.

# 6. Test Mapping

| Test File | Tests | Acceptance Criteria |
|---|---|---|
| `VSP.Tests/Player/PlaybackViewModelTests.cs` | `FrameRendered_RaisesPropertyChangedForCurrentFrameSource` | AC #1 |
| `VSP.Tests/Player/PlaybackViewModelTests.cs` | `FrameRendered_AfterStopThenReplacedController_SubscriptionIsRemoved` | AC #2 |
| `VSP.Tests/Player/PlaybackViewModelTests.cs` | `FrameRendered_FromOldControllerAfterStopPlay_CannotUpdateCurrentPlaybackPresentation` | AC #3 |
| `VSP.Tests/Player/PlaybackViewModelTests.cs` (pre-existing, unmodified) | `LoadCamerasAsync_PopulatesCamerasFromRepository`, `SelectingCamera_WithNoRecordings_ReportsNoneFound`, `PlayCommand_NotExecutableWithoutSelectedRecording`, `PlayCommand_ExecutableOnceRecordingSelected`, `PlayCommand_StartsControllerAgainstSelectedRecordingPath`, `ControllerStateChanged_ToConnected_UpdatesStatusMessage`, `PauseCommand_OnlyExecutableWhenConnected`, `StopCommand_StopsController`, `Seek_WithNoActiveSession_DoesNotThrow`, `Seek_WithActiveSession_CallsClockSeek` | AC #4 (regression, unmodified, confirmed still passing) |
| `VSP.Tests/Player/LiveViewViewModelTests.cs` (RC1-R03, pre-existing, unmodified) | `FrameRendered_RaisesPropertyChangedForCurrentFrameSource`, `FrameRendered_AfterControllerDetached_SubscriptionIsRemoved`, `FrameRendered_FromOldDetachedController_CannotUpdateCurrentViewModel` | Not weakened or removed — reused as the source pattern only |

New tests reuse `LiveViewViewModelTests.FakeFrameRenderer` (already accessible internal class, same test assembly) for the same `RaiseFrameRendered()` test seam RC1-R03 established — no new fake type introduced.

# 7. Automated Verification

- `dotnet build VSP.slnx -c Debug`: 0 errors (pre-existing `NU1903` advisory + xUnit-style warnings only).
- `dotnet test VSP.Tests -c Debug --no-build`: **907 passed / 1 failed / 0 skipped / 908 total.** The one failure, `RtspMediaSessionIntegrationTests.OpenAsync_AgainstRealFfmpegEncodedStream_ReceivesAndDecodesRealFrames`, is the same pre-existing, documented full-suite-load timing flake carried since Epic-010/011 — re-run once in isolation per instruction (no timeout/parallelization change): **2 passed / 0 failed / 0 skipped / 2 total**, confirming it is unrelated to this change.
- `git diff --check`: exit code 0. Output shows only pre-existing LF→CRLF autocrlf notices across many files (including files this change did not touch) — not errors.

# 8. Out of Scope

- `PlaybackController`, `RecordedFileMediaSession`, `FfmpegVideoDecoder`, `WpfFrameRenderer`, `IFrameRenderer`, `PlaybackView.xaml`, `RecordingCatalog`, `RecordingPathProvider`, `MediaController`, `LiveViewViewModel`.
- RC1-R04 storage behavior, RTSP/ONVIF/authentication, recording format/path contract, playback clock/position/seek logic — all unmodified.
- Diagnostic instrumentation — not added; implementation evidence matched the approved RCA exactly, with no contradiction requiring it.
- Item H real-device validation — not performed by this document; Item H remains not-Pass.
- Rewriting Item G/H checklist, roadmap, or changelog closure text — deferred until Product Owner real-device validation of this fix.
- Commit — performed by the Product Owner.

# 9. Real-Device Validation Evidence

**Product Owner real-device retest, 2026-08-14, `VSP_v1.0.0-RC1_RC1-R05_win-x64` artifact: PASS.**

- First Play immediately shows video: Pass.
- Pause: Pass.
- Resume: Pass.
- Stop: Pass.
- Play after Stop immediately shows video: Pass.
- Second Pause → Play cycle: Pass.

The previously observed defect (Play → timeline advances, black image; Pause reveals frame) is no longer reproducible. The Stop → Play lifecycle also passed, confirming the replacement controller/renderer's `FrameRendered` subscription is correctly re-established on each new controller and correctly torn down on each detach.

# 10. Status

**Implementation Complete. Test Complete (908 tests total, 907 passing, 1 pre-existing unrelated flake confirmed passing 2/2 in isolation). Real-device validated — PASS — CLOSED (2026-08-14).** Artifact: `Releases\VSP_v1.0.0-RC1_RC1-R05_win-x64\` / `.zip` (RC1-R04's artifact left untouched). **Product Owner Accepted. Item H — Playback is now Pass. Not yet committed.**
