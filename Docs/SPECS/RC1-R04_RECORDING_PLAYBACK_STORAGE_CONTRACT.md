# RC1-R04 Recording / Playback Storage Contract

Status: Implementation Complete — Test Complete — Real-Device Validated — PASS — CLOSED (2026-08-14)
Feature: Live View Recording / Playback (Epic-011 Recording Foundation, Epic-012 Playback Foundation)
Type: RC1 Post-Commit Remediation — defect found during Item H (Playback) real-device-validation preparation, immediately after Item G (Recording) was confirmed Pass
Remediation ID: RC1-R04
Task: Task-AI00B

---

# 1. Problem Statement

A recording produced through the normal, production Live View UI (Start Recording → Stop Recording) could not be discovered through the normal, production Playback UI for the same camera. Selecting the camera in Playback showed an empty Recording list ("No recordings found for {camera}") even though a valid, non-zero-size `.mp4` file existed on disk from that camera's own Item G real-device recording.

# 2. Root Cause

Two independently-correct halves of the recording pipeline used inconsistent contracts for where a recording file lives:

- **Write side**: `LiveViewViewModel`'s real production controller factory (`LiveViewViewModel.cs`, public constructor) constructed `MediaController` as `new MediaController(rtspUrl, dispatcher)` — never supplying a `cameraId`. `MediaController.BuildRecordingFilePath()` already correctly branches on `_cameraId.HasValue` (Epic-012), but since it was always `null` in production, every recording fell back to the flat `RecordingPathProvider.GetRecordingRoot()` path (`%LocalAppData%\VSP\Recordings\<file>.mp4`).
- **Read side**: `PlaybackViewModel.LoadRecordingsForSelectedCamera()` → `RecordingCatalog.ListRecordings(cameraId)` → `RecordingPathProvider.GetCameraRecordingDirectory(cameraId)` — this has always scanned only the per-camera subfolder (`%LocalAppData%\VSP\Recordings\<cameraId:N>\`).

Camera identity was available the entire time — `Camera.Id` (`VSP.Domain.Entities.Camera.cs`, non-nullable `Guid`) is threaded as a direct method parameter through `LoadCamera` → `LoadOnvifCamera`/`LoadRtspUrlCamera` → `StartAuthenticatedController`/`StartControllerWithUrl` — but `StartControllerWithUrl`'s call to `_controllerFactory(effectiveRtspUrl, _uiDispatcher)` never passed it, because the factory delegate's shape (`Func<string, Dispatcher, IMediaController>`) had no parameter for it.

The read side (`RecordingCatalog`/`RecordingPathProvider.GetCameraRecordingDirectory`) is correct and was not changed — confirmed by source inspection per explicit instruction not to alter Playback's discovery contract unless proven architecturally wrong (it was not).

# 3. Intended / Expected Behavior

`<configured Recording Root>\<cameraId:N>\<recording>.mp4` — recordings are written into the same per-camera directory Playback already scans, for every camera, with no manual file movement, folder creation, or database lookup required by the Product Owner.

# 4. Minimum Fix Implemented

Confined entirely to `VSP.UI/ViewModels/LiveViewViewModel.cs`:

1. `_controllerFactory` field widened: `Func<string, Dispatcher, IMediaController>` → `Func<string, Dispatcher, Guid, IMediaController>`.
2. Public (production) constructor's default factory: `static (rtspUrl, dispatcher) => new MediaController(rtspUrl, dispatcher)` → `static (rtspUrl, dispatcher, cameraId) => new MediaController(rtspUrl, dispatcher, cameraId: cameraId)`.
3. Internal test-seam constructor's `controllerFactory` parameter widened to match.
4. The one real call site, `StartControllerWithUrl`: `_controllerFactory(effectiveRtspUrl, _uiDispatcher)` → `_controllerFactory(effectiveRtspUrl, _uiDispatcher, camera.Id)` — `camera` is the `Camera` instance already in scope as a method parameter at that exact call site; no global/current-camera property was introduced or consulted.

No change to `MediaController`'s recording-path logic, `RecordingPathProvider`, `RecordingCatalog`, `PlaybackViewModel`, `PlaybackController`, `IMediaController`, `LiveView.xaml`, `PlaybackView.xaml`, the recording filename format, recording lifecycle behavior (Start/Stop/Pause/Stop-Live-finalizes), RTSP/ONVIF behavior, authentication, or decoder/renderer behavior — all confirmed unmodified by this change.

# 5. Acceptance Criteria

1. `LiveViewViewModel` passes the exact selected `Camera.Id` to the controller factory on every `LoadCamera` call.
2. A recording started for Camera A resolves under `<Recording Root>\<CameraAId:N>\`.
3. A recording started for Camera B resolves under `<Recording Root>\<CameraBId:N>\`.
4. Camera A's and Camera B's recording directories are distinct.
5. `RecordingCatalog.ListRecordings` for Camera A's directory discovers a recording written there.
6. `RecordingCatalog.ListRecordings` for Camera A's directory does not discover a recording written under Camera B's directory.
7. The write-side directory (`RecordingPathProvider.GetCameraRecordingDirectory`) and the read-side scan target (`RecordingCatalog.ListRecordings`) are the literal same path for a given camera id and config root — no manual file move.
8. A configured custom Recording Root (`recording-settings.json` / Settings screen) still produces `<Custom Root>\<cameraId:N>\`, not the default root.
9. Existing recording lifecycle behavior (Start Recording, Stop Recording, Pause-does-not-stop-recording, Stop-Live-finalizes-recording, already-recording guard, not-connected guard) is unchanged.
10. `RecordingIntegrationTests` (real, non-fake FFmpeg round-trip) remains passing unmodified.

# 6. Implementation Mapping

| File | Change |
|---|---|
| `VSP.UI/ViewModels/LiveViewViewModel.cs` | `_controllerFactory` field and both constructors' delegate type widened to include `Guid cameraId`; `StartControllerWithUrl` passes `camera.Id` |

# 7. Test Mapping

| Test File | Tests | Acceptance Criteria |
|---|---|---|
| `VSP.Tests/Player/LiveViewViewModelTests.cs` | `LoadCamera_PassesExactSelectedCameraIdToControllerFactory`, `LoadCamera_DifferentCameras_EachPassesItsOwnIdToControllerFactory` | AC #1 |
| `VSP.Tests/Player/MediaControllerRecordingTests.cs` | `StartRecordingAsync_WithCameraId_WritesUnderCameraSpecificDirectory`, `StartRecordingAsync_TwoDifferentCameraIds_ResolveToDistinctDirectories` | AC #2, #3, #4 |
| `VSP.Tests/Player/RecordingCatalogTests.cs` | `ListRecordings_ForCameraDirectory_DiscoversRecordingWrittenToThatSameDirectory`, `ListRecordings_ForCameraADirectory_DoesNotDiscoverCameraBRecording` | AC #5, #6, #7 |
| `VSP.Tests/Player/RecordingPathProviderTests.cs` | `GetCameraRecordingDirectory_WithConfiguredRoot_AppendsCameraSubfolderUnderCustomRoot` | AC #8 |
| `VSP.Tests/Player/MediaControllerRecordingTests.cs` (pre-existing, unmodified) | `StartRecordingAsync_WhenConnected_StartsSessionAndDispatchesEncodedFrames`, `StartRecordingAsync_WhenNotConnected_Throws`, `StartRecordingAsync_WhenAlreadyRecording_Throws`, `StopRecordingAsync_WhenNotRecording_IsNoOp`, `StopRecordingAsync_WhileRecording_FinalizesAndClearsState`, `StopAsync_WhileRecording_FinalizesRecordingBeforeDisconnecting`, `PauseAsync_WhileRecording_DoesNotStopRecording` | AC #9 (regression, unmodified, confirmed still passing) |
| `VSP.Tests/Player/RecordingIntegrationTests.cs` (pre-existing, unmodified) | `Recording_AgainstRealFfmpegSource_ProducesPlayableStreamCopiedFile` | AC #10 (regression, unmodified, confirmed still passing) |
| Every other existing `controllerFactory` lambda across `LiveViewViewModelTests.cs` (~29 call sites) | Mechanically widened to accept the new third `Guid` parameter (unused, discarded) | No behavior/assertion change — compiler-verified |

# 8. Automated Verification

- `dotnet build VSP.slnx -c Debug`: 0 errors (pre-existing `NU1903` advisory + xUnit-style warnings only, unchanged in kind from before this fix).
- `dotnet test VSP.Tests -c Debug --no-build`: 904 passed / 905 total. The one failure, `RtspMediaSessionIntegrationTests.OpenAsync_AgainstRealFfmpegEncodedStream_ReceivesAndDecodesRealFrames`, is the same pre-existing, documented full-suite-load timing flake carried since Epic-010/011 — confirmed passing 2/2 in isolation immediately afterward, unrelated to this change.
- `git diff --check`: exit code 0 (no whitespace/conflict-marker errors flagged). Output shows only pre-existing LF→CRLF autocrlf notices across many files (including files this change did not touch), not errors.

# 9. Legacy Recording Policy

Existing flat-root recordings (`%LocalAppData%\VSP\Recordings\<file>.mp4`, written before this fix — including the Item G real-device validation file, `20260813_212051_8f86ca69f6fc4f0dac0cbc247102fd97.mp4`) are **left exactly as they are**: not moved, not deleted, not auto-migrated, not auto-associated with any camera.

**Determined infeasible to safely auto-associate with a camera**, not merely undesirable: the flat-root filename format is `{yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4` (`MediaController.BuildRecordingFilePath`) — the trailing GUID is a fresh, random per-recording identifier generated at record time, not the camera's id, and carries no camera-identity signal. `RecordingCatalog` itself has no database or index (by its own design, Epic-012) that could supply ownership after the fact. No deterministic ownership signal exists anywhere in the current system for pre-fix files. Playback was **not** changed to scan the flat root as a fallback, per explicit instruction and because doing so would make multi-camera correctness impossible to guarantee (a flat scan cannot distinguish which camera a legacy file belongs to).

The Item G evidence file remains valid, retained evidence that Recording itself functions correctly; it is not expected to appear in Playback and does not need to for Item G's own Pass status to stand.

# 10. Out of Scope

- Any migration tool or UI for legacy flat-root recordings.
- Any change to `MediaController` recording-path logic, `RecordingPathProvider`, `RecordingCatalog`, `PlaybackViewModel`, `PlaybackController`, `IMediaController`, recording filename format, recording lifecycle behavior, RTSP/ONVIF behavior, authentication, or decoder/renderer behavior.
- Item H (Playback) real-device validation — not performed by this document; Item H remains not-Pass.
- Rewriting Item G's or the roadmap's/changelog's closure text describing the prior (now-fixed) flat-root behavior — deferred until Product Owner real-device validation of this fix.
- Commit — performed by the Product Owner.

# 11. Real-Device Validation Evidence

**Product Owner real-device retest, 2026-08-14, `VSP_v1.0.0-RC1_RC1-R04_win-x64` artifact: PASS.**

Validated production workflow: Live View → Start Recording → Stop Recording → per-camera recording storage → Playback camera selection → recording automatically discovered. No manual file move, GUID-folder creation, or database manipulation was required at any step.

# 12. Status

**Implementation Complete. Test Complete (905 tests total, 904 passing, 1 pre-existing unrelated flake confirmed passing in isolation). Real-device validated — PASS — CLOSED (2026-08-14).** Artifact: `Releases\VSP_v1.0.0-RC1_RC1-R04_win-x64\` / `.zip`. **Product Owner Accepted. Not yet committed.**
