# Epic-015 Error Handling Foundation

Status: **Accepted — Frozen** (Product Owner acceptance 2026-08-01)
Feature: Cross-cutting / Diagnostics
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

---

# Approval Record

- Follows Epic-014 (Logging Foundation, Accepted/Frozen). Epic-015 was first proposed as "Feature Logging" (a Current-State Analysis for it was produced and is superseded by this document) — the Product Owner redirected: Feature Logging ("Camera Added," "Recording Started," "Playback Started," and similar business-event instrumentation) is explicitly deferred to a future Epic. Epic-015 is instead **Error Handling Foundation**: establishing consistent exception handling only where exceptions currently disappear silently.
- Scope dictated directly by the Product Owner, six items: Database initialization, Repository operations, RTSP connection failures, ONVIF connection failures, Retry failures, Media reconnect failures. Pattern: **try → Log → Return appropriate result**. No new product features. No instrumentation of normal business events.
- Both Open Questions from the original proposal resolved by the Product Owner:
  1. Whether a synchronous `OnStartup` exception reaches `DispatcherUnhandledException` is **not to be investigated** — moot regardless, since Database initialization gets its own explicit path (below).
  2. Required startup behavior on DB-init failure: log the original exception → generate/display an Error ID → tell the user to provide the latest log → **terminate startup cleanly**. The application must not continue without a working database (stronger than this document's original default, which left open the possibility of continuing).
- Signature change approved for `DatabaseInitializer`, with a constraint: **no bare `bool`** (would discard the original failure). Use the smallest explicit result shape — `DatabaseInitializationResult { Success, Exception }` — not a generic Result<T> framework.
- For Repository/RTSP/ONVIF/Retry/Media reconnect: confirmed as originally planned — preserve all existing return/retry/throw behavior exactly; log only inside existing or newly-necessary exception paths; Repository logs and rethrows (never silently converts a failure into an empty/default result); Retry's final-failure propagation (out of `RetryingDiscoveryRunner`, uncaught) remains unchanged.
- **Security constraint (new):** never log passwords, authorization headers, tokens, full credential-bearing URLs, or sensitive camera configuration. See §8.
- Confirmed out of scope, restated: no normal feature/success-event instrumentation (Camera Added, Recording Started, Playback Started, etc.).
- Approved by: Product Owner (this conversation). Implementation authorized — proceeding. Commit deferred until Product Owner acceptance (per the Epic-014 precedent).

## Product Acceptance (2026-08-01)

1. Approved: Error Handling Foundation, Manual Validation, Product Validation, Security Review.
2. Accepted: 632/633 — the remaining `RtspMediaSessionIntegrationTests` timing flake is pre-existing (documented since Epic-011/012) and not a regression.
3. Technical debt recorded (not implemented): **TD-030** — Platform Lifecycle: future versions shall replace direct process termination (`Environment.Exit`) with a unified lifecycle manager. Complements TD-029 (recorded at Epic-014 acceptance, same underlying concern — direct `Environment.Exit(1)` calls, now three of them: two from Epic-014's handlers, one from this Epic's `HandleDatabaseInitializationFailure`). No implementation now; both remain for a future Platform Lifecycle Epic.
4. `Directory.Build.props`, `Docs/CHANGELOG.md`, `Docs/03_PRODUCT_ROADMAP.md` updated to reflect acceptance.
5. Commit authorized with message `Epic-015: Error Handling Foundation` — per the same `CLAUDE.md`/`Docs/DEVELOPMENT_ROLES.md` constraint applied at Epic-014's acceptance, the AI Agent does not run `git add`/`git commit` itself; see the chat response for the exact commands.
6. **Frozen.** Any future enhancement to error handling is a new Epic. Epic-015 is not to be reopened except for a confirmed defect.

---

# Objective

For the six named components, replace "exception vanishes without a trace" with "exception is logged via `AppLog` (Epic-014), then the component does whatever it already does (or should minimally be changed to do) in response" — no new behavior beyond that, no new product-facing feature.

---

# Current-State Analysis

Verified directly against the repository (full-file reads, not summarized), current as of 2026-08-01, after Epic-014's acceptance:

### 1. Database initialization — `VSP.Infrastructure/Database/DatabaseInitializer.cs`
```csharp
public void Initialize()
{
    using var connection = _databaseService.CreateConnection();
    connection.Open();
    CameraTable.Create(connection);
}
```
No try/catch. Called exactly once, from `VSP.UI/App.xaml.cs` (`initializer.Initialize();`, immediately after Epic-014's logging/exception-handler setup), with no try/catch at the call site either. Today, a failure here (bad permissions, disk full, corrupt file, schema error) is an **unhandled exception during `OnStartup`, with zero log entry**. `DatabaseService.CreateConnection()` itself (`VSP.Infrastructure/Database/DatabaseService.cs`) also has no error handling, but every one of its callers is being given a try/catch in this Epic (this method and the repository methods below), so it needs no changes of its own.

### 2. Repository operations — `VSP.Infrastructure/Repositories/SQLiteCameraRepository.cs`
`Add(Camera)`, `GetAll()`, `Update(Camera)`, `Delete(Guid)` — all four raw ADO.NET (open connection, build command, execute), **zero error handling in any of them**, confirmed by full-file read. This class has no interface of its own; it's wrapped by `VSP.Device/Repositories/CameraRepository.cs`, which implements `VSP.Device.Interfaces.ICameraRepository` (`GetAll`/`GetById`/`Add`/`Update`/`Delete`) via one-line pass-through calls with no logic of its own. `ICameraRepository` has **25 references** across `VSP.Device`, `VSP.UI`, and `VSP.Tests` — a genuinely widely-used contract. Today, any DB failure here (malformed stored `Guid`/`DateTime`, locked file, SQLite-level error) propagates as a raw, unlogged exception up through `CameraRepository`'s pass-through to whichever caller happens to have a `catch` several layers away (e.g. `DeviceRegistrationService.cs:48`, `ImportExecutor.cs:49`, `CameraDetailViewModel.cs:358/424`, `BatchEditViewModel.cs:151` — all of which already discard the original exception down to a bare `.Message` string). No test file exists for `SQLiteCameraRepository` or `DatabaseInitializer` anywhere in `VSP.Tests` — `VSP.Infrastructure` has zero test coverage today.

### 3. RTSP connection failures — `VSP.Device/Drivers/RTSP/RtspCameraDriver.cs`
```csharp
public bool TestConnection(Camera camera)
{
    try { ... }
    catch (Exception)
    {
        return false;
    }
}
```
Already try/catch/return — the exception is caught but never bound to a variable, so it's fully discarded (not even its type or message is visible anywhere). `GetDeviceInformation` is a permanent stub (`return null;`, no I/O, nothing to fail) — out of scope, nothing to change. Existing coverage: `VSP.Tests/Drivers/RTSP/RtspCameraDriverTests.cs`.

### 4. ONVIF connection failures — `VSP.Device/Drivers/ONVIF/OnvifCameraDriver.cs`
`TestConnection` and `GetDeviceInformation` both follow the identical pattern to RTSP above — `catch (Exception) { return false; }` / `catch (Exception) { return null; }`, exception fully discarded, unbound. Existing coverage: `VSP.Tests/Drivers/ONVIF/OnvifCameraDriverTests.cs`.

### 5. Retry failures — `VSP.Device/Discovery/Execution/RetryingDiscoveryRunner.cs`
```csharp
catch (OperationCanceledException) { throw; }
catch when (!isLastAttempt)
{
    await Task.Delay(_policy.Delay, cancellationToken).ConfigureAwait(false);
    continue;
}
```
More precise than the earlier Feature-Logging survey stated: the exception filter `when (!isLastAttempt)` means **every non-final failed attempt is caught and fully discarded** (not even the exception's type survives — pure retry-and-continue), while **the final attempt's exception is not caught here at all** — it propagates out of `ExecuteAsync` uncaught, to be picked up by `DiscoveryOrchestrator`'s own top-level catch (`VSP.Device/Discovery/Orchestration/DiscoveryOrchestrator.cs:87`, out of scope for this Epic — see §6). So the actual gap is specifically the intermediate retry attempts: right now there is no record at all that attempt 1 (of, say, 3) failed and why. Existing coverage: `VSP.Tests/Discovery/RetryingDiscoveryRunnerTests.cs`.

### 6. Media reconnect failures — `VSP.Player/Control/MediaController.cs`
```csharp
try
{
    await session.OpenAsync(cancellationToken).ConfigureAwait(false);
    opened = true;
}
catch (OperationCanceledException)
{
    CleanupCurrentSession(stateChangedHandler);
    break;
}
catch
{
    // The underlying MediaError was already captured via HandleSessionStateChanged.
}
```
(`ConnectionLoopAsync`, lines 373-386.) A bare `catch` with no bound exception variable — the comment is accurate that a `MediaError` object is separately captured via the `StateChanged` event (consumed live by `LiveViewViewModel` for UI display), but that `MediaError` (which does carry the real `Exception`) is never logged or persisted anywhere. Every failed reconnect attempt vanishes except the live UI state; only the final give-up is even visible, and not durably. Existing coverage: `VSP.Tests/Player/MediaControllerReconnectTests.cs`.

### Cross-cutting: project references already in place
Confirmed via each project's `.csproj`: `VSP.Device` and `VSP.Player` already reference `VSP.Core` (predates Epic-014); `VSP.Infrastructure` gained its `VSP.Core` reference in Epic-014 specifically for this purpose. **No new project reference is needed anywhere in this Epic** — `AppLog` is already reachable from all six locations.

---

# Scope — In Scope

For each of the six, add logging via `AppLog` (Epic-014) inside a try/catch, per this component-by-component plan:

| # | Component | Change | Signature change? | Log level |
|---|---|---|---|---|
| 1 | `DatabaseInitializer.Initialize()` | Wrap body in try/catch; on exception, `AppLog.Fatal` with a generated Error ID; **return `DatabaseInitializationResult { Success, Exception }`** instead of `void`; `App.xaml.cs` checks it and, on failure, shows a message box with the Error ID and guidance to send the latest log file, then terminates startup cleanly (`Environment.Exit(1)`) — the app must not continue without a working database | Yes — `void` → `DatabaseInitializationResult` (new, minimal, single-purpose type — not a generic Result framework), one method, one call site | Fatal |
| 2 | `SQLiteCameraRepository.Add/GetAll/Update/Delete` | Wrap each body in try/catch; on exception, `AppLog.Error` naming the operation and `camera.Id` only (never the full `Camera` object), then `throw;` — preserves the exact existing exception-propagation contract that `ICameraRepository`'s 25 call sites already depend on; never converts a failure into an empty/default return | No — rethrow preserves the existing contract exactly | Error |
| 3 | `RtspCameraDriver.TestConnection` | Bind the caught exception (`catch (Exception ex)`), `AppLog.Warning` naming `camera.IpAddress` only (never `camera.RtspUrl`, which may embed credentials), keep `return false;` unchanged | No | Warning |
| 4 | `OnvifCameraDriver.TestConnection` / `GetDeviceInformation` | Same as RTSP — bind the exception, `AppLog.Warning` naming `camera.IpAddress`/`camera.HttpPort` only (never SOAP request/response bodies, which may carry WS-Security credentials), keep existing `return false;`/`return null;` unchanged | No | Warning |
| 5 | `RetryingDiscoveryRunner.ExecuteAsync` | In the `catch when (!isLastAttempt)` block, bind the exception and `AppLog.Warning` naming the attempt number (`attempt`/`_policy.MaxAttempts`) only — no request/candidate details — before the existing `Task.Delay`/`continue`. Final-attempt propagation (uncaught here) is unchanged. | No | Warning |
| 6 | `MediaController.ConnectionLoopAsync` | Change the bare `catch` to `catch (Exception ex)`, `AppLog.Warning` naming `_cameraId` (already a field, never `_rtspUrl`) and `_reconnectAttempts`, keep existing fall-through behavior unchanged | No | Warning |

**Log level rationale:** `Fatal` only for DB init (app cannot function without it). `Error` for repository operations (a specific operation failed; the app can often continue for unrelated operations, but data was not persisted/read as requested). `Warning` for connection tests, retries, and reconnects — these are expected, routine operational conditions for a camera/network product (a camera being offline is not an application defect), not exceptional application errors. See §8 for exactly what is and is not safe to include in each message.

---

# Out of Scope

- **Feature Logging** (business-event instrumentation: "Camera Added," "Recording Started," "Playback Started," Discovery start/results, and similar) — explicitly deferred by the Product Owner to a future Epic. Nothing in this Epic adds a log call for a success path, only for a caught exception.
- `DiscoveryOrchestrator`'s own top-level catch (`DiscoveryOrchestrator.cs:87`) and its `Cancelled`/`OperationCanceledException` handling (line 72) — not named in the Product Owner's six items; the Retry-failures item is scoped to `RetryingDiscoveryRunner` specifically, where the actual gap (silently-discarded intermediate attempts) lives.
- Any other currently-silent `catch` block surfaced by the earlier Feature-Logging Current-State Analysis (`CameraFactory.cs`, `DeviceRegistrationService.cs`, `ImportExecutor.cs`, `BatchEditViewModel.cs`, `CameraListViewModel.cs`, `NetworkScanService.cs`/`TcpNetworkReachabilityProbe.cs`/`RtspEndpointProbeService.cs`, `PlaybackController`/`MediaController`'s other `RecordError` call sites, etc.) — none of these were named by the Product Owner; they remain candidates for a later Epic, not this one.
- `VSP.Infrastructure/SQLite/CameraTable.cs` (the actual `CREATE TABLE` statement) — not separately wrapped; its only caller, `DatabaseInitializer.Initialize()`, already wraps it as part of item 1.
- `CameraRepository.cs` (`VSP.Device`, the `ICameraRepository` pass-through wrapper) — no changes; it has no logic of its own to wrap, and the exception is already logged one layer down in `SQLiteCameraRepository` before it re-propagates through here unchanged.
- Any change to `ICameraRepository`'s signature, or to any of the 25 call sites that consume it — explicitly avoided by using log-and-rethrow instead of a result-object redesign (see Open Question 2 rationale, §7).
- Any change to how existing `StatusMessage`/`HasError`/`MessageBox` UI-level error surfacing works — this Epic adds a parallel durable log entry, it does not touch or replace any existing UI error path.
- Any change to `RecordingPathProvider.cs`'s existing malformed-config fallback (already try/catch, already documented as intentional best-effort, not named by the Product Owner).

---

# Risk Ceiling

**MEDIUM.** Five of the six changes (RTSP, ONVIF, Retry, Media reconnect, and the repository's log-and-rethrow) are additive-only inside existing catch blocks with zero behavior or signature change — LOW individually. `DatabaseInitializer.Initialize()`'s `void` → `DatabaseInitializationResult` signature change is the one item pushing the ceiling to MEDIUM: it is a method signature change, but confined to a single class with a single call site inside this same solution (not a published/external API, not consumed by `VSP.Tests` today since no test exists for it), and explicitly approved by the Product Owner.

No database schema change, no change to `ICameraRepository`'s public contract, no new external package, no security-model change.

---

# Definition of Done

1. All six locations log via `AppLog` (Epic-014) at the level specified in §Scope, capturing the real `Exception` object (not just a message string) in every case.
2. `SQLiteCameraRepository`'s four methods rethrow after logging — `ICameraRepository`'s existing 25 call sites see no behavior change, verified by the full existing suite remaining green with no test changes required at those call sites.
3. `RtspCameraDriver`/`OnvifCameraDriver`'s `TestConnection`/`GetDeviceInformation` still return exactly `false`/`null` on failure — no behavior change beyond the added log call.
4. `RetryingDiscoveryRunner` still retries exactly as before — no behavior change beyond the added log call on each non-final failed attempt.
5. `MediaController`'s reconnect loop still behaves exactly as before — no behavior change beyond the added log call on each failed `OpenAsync` attempt.
6. `DatabaseInitializer.Initialize()` returns a `DatabaseInitializationResult` (never a bare `bool`) on failure instead of throwing unhandled, per §9: logs Fatal with an Error ID, `App.xaml.cs` shows a message box naming the Error ID and log path, then terminates cleanly — the app never proceeds to `MainWindow` without a working database.
7. New unit tests for `SQLiteCameraRepository` and `DatabaseInitializer` (neither has any today) using a real temp-file SQLite database, matching the project's existing test-seam conventions; existing test files for the other four components (`RtspCameraDriverTests`, `OnvifCameraDriverTests`, `RetryingDiscoveryRunnerTests`, `MediaControllerReconnectTests`) extended to assert the new logging occurs (via an injectable/recording `ILogger`, matching `AppLogTests`' `RecordingLogger` pattern) without asserting on exact log text.
8. Full existing suite remains green; build stays passing with no new warnings.

---

# Implementation Plan (for reference — not started; approval required first)

### Files to modify
- `VSP.Infrastructure/Database/DatabaseInitializer.cs` — try/catch, `DatabaseInitializationResult` return.
- `VSP.Infrastructure/Repositories/SQLiteCameraRepository.cs` — try/catch + rethrow in all four methods.
- `VSP.Device/Drivers/RTSP/RtspCameraDriver.cs` — bind + log in `TestConnection`'s existing catch.
- `VSP.Device/Drivers/ONVIF/OnvifCameraDriver.cs` — bind + log in both existing catches.
- `VSP.Device/Discovery/Execution/RetryingDiscoveryRunner.cs` — bind + log in the retry catch.
- `VSP.Player/Control/MediaController.cs` — bind + log in the reconnect catch.
- `VSP.UI/App.xaml.cs` — check `Initialize()`'s new `DatabaseInitializationResult`, fail startup per §9 if `!Success`.
- `VSP.Tests/Drivers/RTSP/RtspCameraDriverTests.cs`, `VSP.Tests/Drivers/ONVIF/OnvifCameraDriverTests.cs`, `VSP.Tests/Discovery/RetryingDiscoveryRunnerTests.cs`, `VSP.Tests/Player/MediaControllerReconnectTests.cs` — extended, not rewritten.

### Files to add
- `VSP.Infrastructure/Database/DatabaseInitializationResult.cs` (new — `{ bool Success, Exception? Exception }` plus `Ok()`/`Failed(Exception)` factory methods; not a generic Result framework)
- `VSP.Tests/Infrastructure/SQLiteCameraRepositoryTests.cs` (new — first test coverage this class has ever had)
- `VSP.Tests/Infrastructure/DatabaseInitializerTests.cs` (new — same)

### Files not to touch
- `DatabaseService.cs`, `CameraTable.cs`, `CameraRepository.cs` — covered transitively, no direct changes needed.
- Every file named in Out of Scope above.
- No SQLite schema change.

### Sequence
1. `SQLiteCameraRepository` (log-and-rethrow, zero contract change — lowest risk, do first) + new `SQLiteCameraRepositoryTests`.
2. `DatabaseInitializer` (the one signature change) + new `DatabaseInitializerTests` + `App.xaml.cs` call-site update.
3. `RtspCameraDriver` + `OnvifCameraDriver` (identical pattern, do together) + extend their existing test files.
4. `RetryingDiscoveryRunner` + extend `RetryingDiscoveryRunnerTests`.
5. `MediaController` reconnect catch + extend `MediaControllerReconnectTests`.
6. Build + full suite; `CHANGELOG.md`/`03_PRODUCT_ROADMAP.md` updates (subject to the same "hold until acceptance" instruction Epic-014 was given, unless the Product Owner says otherwise this time); Epic Review.

### Test plan
Each of the six gets at least one new/extended test asserting: (a) on a forced failure, the configured test logger receives exactly one call at the specified level with a non-null `Exception`; (b) the method's return value / thrown-exception behavior is unchanged from today. `SQLiteCameraRepositoryTests`/`DatabaseInitializerTests` force failure via an invalid `DatabaseService` (e.g. an unwritable directory or a pre-locked file) rather than mocking ADO.NET, matching the project's real-dependency testing convention (`RtspMediaSessionIntegrationTests` uses real FFmpeg, `RtspCameraDriverTests` uses a real loopback server).

### Rollback
Every change in this Epic is a same-file, contained edit (five files get a catch-block change only; `DatabaseInitializer.cs` + `App.xaml.cs` get the one signature change, plus one new small result type). Rollback is reverting those seven files plus deleting the three new files (two test files, one result type).

---

# 8. Security — what must never be logged

Per Product Owner instruction. Applies to every log call added in this Epic:

- **Never** log `camera.Password` or `camera.Username`.
- **Never** log `camera.RtspUrl` verbatim (it may embed credentials, e.g. `rtsp://user:pass@host/...`, even though today's driver code stores `Username`/`Password` separately) — use `camera.IpAddress` instead wherever a camera needs identifying in a log line.
- **Never** log an RTSP `Authorization`/`WWW-Authenticate` header value, or any ONVIF SOAP request/response body (which may carry WS-Security tokens, nonces, or password digests).
- **Never** log any full connection string, token, or other credential-bearing value.
- **Safe to log:** `camera.Id` (a `Guid`, not a secret), `camera.IpAddress`, `camera.HttpPort`, attempt counters, operation names, exception type/message/stack trace (the exceptions in scope here — socket/timeout/SQLite errors — do not carry credential material in their own `Message`/`StackTrace` in this codebase, verified by reading each throw site in scope).
- Every log call in this Epic is reviewed against this list as part of implementation, not deferred to a later pass.

---

# 9. Required Startup Failure Behavior (Database Initialization)

Per Product Owner instruction, `App.xaml.cs`'s handling of `DatabaseInitializer.Initialize()` returning a failed `DatabaseInitializationResult`:

1. `AppLog.Fatal` the original `Exception` (from `result.Exception`), tagged with a generated Error ID — reusing the same `NewErrorId()` pattern Epic-014 already established for its three exception handlers, not a new mechanism.
2. Show a message box naming the Error ID and the current log file's path (via `FileLogger.GetCurrentLogFilePath()`, same as Epic-014's UI-exception dialog), stating that VSP could not start because its database could not be initialized, and asking the user to send the log file and Error ID to support.
3. `Environment.Exit(1)` — clean, deliberate termination. The application must not proceed to show `MainWindow` or any other UI without a working database.

This is a fourth, DB-init-specific path alongside Epic-014's three global handlers — not a reuse of `OnDispatcherUnhandledException` (whose "log, notify, continue" behavior is wrong here, since the app cannot usefully continue without a database).

---

# 10. Implementation Notes (2026-08-01) — deviations from the plan, discovered while implementing

- **`DatabaseService.cs` was touched, despite being listed "not to touch."** Neither `SQLiteCameraRepository` nor `DatabaseInitializer` had any test coverage before this Epic, and `DatabaseService` had no way to point at anything other than the real `%LocalAppData%\VSP\vsp.db` — there was no way to force a failure deterministically in a test without either touching the real shared app-data path (flaky, risks colliding with a real running instance) or adding a test seam. Added a minimal `internal DatabaseService(string databaseDirectory)` constructor overload, matching `RecordingPathProvider`'s and `FileLogger`'s existing test-seam convention exactly — the public parameterless constructor's behavior is completely unchanged.
- **`VSP.Infrastructure/AssemblyInfo.cs`** (new) — `InternalsVisibleTo("VSP.Tests")`, needed for the above; `VSP.Infrastructure` had no `AssemblyInfo.cs` of its own before this Epic (`VSP.Core`, `VSP.Player`, `VSP.UI` all already had one).
- **`VSP.Tests/Logging/RecordingLogger.cs`** (new, extracted from `AppLogTests`) and **`VSP.Tests/Logging/AppLogTestCollection.cs`** (new) — six test classes across this Epic now call `AppLog.Initialize`, which mutates a single process-wide static target; without forcing them into one non-parallelized xunit collection, two of them running concurrently could observe each other's log calls. `AppLogTests` itself was moved into the same collection for the same reason. `RtspCameraDriverTests`, `OnvifCameraDriverTests`, `RetryingDiscoveryRunnerTests`, and `MediaControllerReconnectTests` were all given `[Collection("AppLog")]` accordingly.
- All other files matched the plan exactly.

**Test count:** 632/632 passing (baseline 620 + 12 new: 1 RTSP, 2 ONVIF, 1 Retry, 1 MediaController, 2 DatabaseInitializer, 5 SQLiteCameraRepository).

---

# 11. Product Owner Recommendation Applied (2026-08-01): single Error ID per failure

After the implementation above, the Product Owner reviewed and required one correction: the Error ID and the original exception must be logged together, once — not split across two log lines (which the first pass did: `DatabaseInitializer.Initialize()` logged the exception with no ID, and `App.xaml.cs` separately logged a second line carrying only the ID).

Fixed by moving all logging for this one path to the single point where the ID is generated:
- `DatabaseInitializer.Initialize()` no longer logs anything itself on failure — it only returns `DatabaseInitializationResult.Failed(exception)`. (This is the one place among the six Epic-015 components that does not log at its own catch site; the other five still do, since none of them involve a user-facing Error ID.)
- `App.xaml.cs`'s `HandleDatabaseInitializationFailure(Exception? exception)` generates the Error ID once and logs a single `AppLog.Fatal($"...[ErrorId: {errorId}]", exception)` call carrying both the ID and the full exception/stack trace, then uses that same `errorId` in the dialog.
- `DatabaseInitializerTests` updated: the failure test no longer asserts a log call from `Initialize()` itself; a new test (`Initialize_WhenDirectoryCannotBeCreated_DoesNotLogItself`) asserts the opposite.
- `Environment.Exit(1)` remains as-is for v1.0 — already tracked as **TD-029** (recorded at Epic-014 acceptance) for a future Platform Lifecycle Epic to provide unified graceful shutdown; the Product Owner reconfirmed this tracking is sufficient, no new debt entry needed.

---

# 12. Manual Validation (2026-08-01) — startup failure path

Performed against the actual built `VSP.UI.exe`, not inferred from code. The real `vsp.db` at `%LocalAppData%\VSP\vsp.db` was renamed aside, replaced with a same-named directory (so `SqliteConnection.Open()` fails with a real `SqliteException`, not a simulated one), the app was launched and driven via Windows UI Automation, and the real file was restored afterward — confirmed identical size (12,288 bytes) before and after, so no user data was lost.

| Requirement | Result |
|---|---|
| Fatal log is written | Yes — one line: `FATAL \| Startup aborted: database initialization failed. [ErrorId: 13FEEA58]` immediately followed by the real `Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 14: 'unable to open database file'` with full stack trace down to `DatabaseInitializer.Initialize()`. |
| Error ID is generated | Yes — `13FEEA58` (an earlier attempt in the same session produced `49839DB6`; each run gets its own ID, as expected). |
| Same Error ID throughout (no duplicates) | Yes — the ID on the log line and the ID shown in the dialog were identical (`13FEEA58` in both), confirming the §11 fix. |
| MessageBox displays the Error ID | Yes — dialog text read directly via UI Automation: *"VSP could not start because its database could not be initialized. Error ID: 13FEEA58. Please send the latest log file below along with this Error ID to support: C:\Users\game2\AppData\Local\VSP\Logs\2026-08-01.log"* |
| Latest log path is displayed | Yes — shown in the same dialog text above, and it is in fact the correct, current day's log file. |
| Application terminates cleanly | Yes — after the dialog's OK button was clicked, `Process.WaitForExit` returned with `ExitCode = 1`, matching the coded `Environment.Exit(1)`. |
| MainWindow is never created | Yes — polled all top-level windows owned by the process from launch until the dialog appeared; only "VSP - Startup Failed" ever existed, never a window titled "VSP" (the confirmed `MainWindow` title from Epic-014's validation). |

One automation note, not a product finding: two earlier attempts in the same session failed to dismiss the dialog because `SendKeys`/`SetForegroundWindow` didn't reliably steal focus from the automation script — the dialog itself was correctly modal and unresponsive to those misdirected inputs (arguably correct behavior). The result above is from a run where the OK button was clicked directly by its resolved screen coordinates, removing the focus dependency.
