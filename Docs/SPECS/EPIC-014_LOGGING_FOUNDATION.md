# Epic-014 Logging Foundation

Status: **Accepted — Frozen** (Product Owner acceptance 2026-08-01)
Feature: Cross-cutting / Diagnostics
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

## Product Acceptance (2026-08-01)

1. Epic-014 officially accepted.
2. No further production code changes under this Epic.
3. Technical debt recorded (not implemented): **TD-029** — the fatal-path shutdown uses `Environment.Exit(1)` directly; acceptable for v1.0 Logging Foundation, but a future Platform Lifecycle Epic should provide a unified graceful-shutdown strategy across UI, Services, Plugins, and future distributed components. See `Docs/CHANGELOG.md`'s Epic-014 entry.
4. `Directory.Build.props`, `Docs/CHANGELOG.md`, and `Docs/03_PRODUCT_ROADMAP.md` updated to reflect acceptance (deferred until this point, per the Pre-Acceptance Review below).
5. Commit authorized by the Product Owner with message `Epic-014: Logging Foundation` — per `CLAUDE.md` ("Do not run git add, git commit, or git push. Git operations are performed by the user (Product Owner)") and `Docs/DEVELOPMENT_ROLES.md` §十 (Git Responsibility is exclusively the Product Owner's), the actual `git add`/`git commit` execution is left to the Product Owner rather than run by the AI Agent, even though explicitly requested — see the chat response for the exact ready-to-run command and file list.
6. **Frozen.** Any future enhancement to logging is a new Epic. Epic-014 is not to be reopened except for a confirmed defect in what it delivered.

---

# Approval Record

- Selected by the Product Owner over Settings (the Current-State Analysis's recommendation) as the actual next Epic — reason given: every remaining Epic benefits from logging, and customer support depends on it more than Settings.
- Scope dictated directly by the Product Owner: global unhandled exception handler, simple text log, rolling log, log levels, no external logging framework, no telemetry, no cloud, no database logging, minimal implementation.
- All four Open Questions from the original proposal resolved by the Product Owner (see §7 — retained for the record, no longer open):
  1. UI-thread exceptions are recoverable: log → show error message → continue (`e.Handled = true`). Non-UI-thread/fatal exceptions: log → graceful shutdown.
  2. Log retention: 30 days (auto-delete older files) — new scope beyond the original "keep everything" default.
  3. Log everything, no level filtering for v1.0 — confirmed as originally defaulted.
  4. `VSP.Infrastructure` **may** reference `VSP.Core` — "logging is a platform capability, not a UI capability." This grants the reference; it does not, by itself, request adding log calls inside `VSP.Infrastructure` — see §6.
- Additional requirements set at approval: fixed log line format (no per-caller variation); daily log file naming exactly `YYYY-MM-DD.log`.
- Additional Out of Scope confirmed at approval: Serilog, NLog, ETW, OpenTelemetry, Elastic, database logging, network logging, cloud logging.
- Approved by: Product Owner (this conversation). Implementation authorized — proceeding.

## Pre-Acceptance Review (2026-08-01)

First implementation pass reviewed by the Product Owner and returned with four adjustments before acceptance:

1. **`Version`/`CHANGELOG.md`/`03_PRODUCT_ROADMAP.md` must not be updated until Product Owner acceptance and commit.** Reverted: `Directory.Build.props` back to `0.13.0`, `CHANGELOG.md` and `03_PRODUCT_ROADMAP.md` back to their pre-Epic-014 (Epic-013-only) content. This Epic spec document (`EPIC-014_LOGGING_FOUNDATION.md`) and `V1.0_CUSTOMER_RELEASE_DEFINITION.md` are unaffected by this instruction — the Product Owner named exactly three documents.
2. **Improve the recoverable-exception dialog**: add an Error ID and guidance to send the latest log file to support. Implemented: each of the three exception handlers generates a short (8-character) Error ID and includes it in its log line; the UI-thread dialog additionally shows the Error ID and the current log file's full path to the user, with guidance to send both to support.
3. **`FileLogger` must flush immediately after every write; crash logs must not be buffered.** Implemented: `Log` now opens an explicit `FileStream`, writes, and calls `Flush(flushToDisk: true)` before returning, replacing the prior `File.AppendAllText` call (which was already open-write-close per call, but did not explicitly force an OS-buffer-to-disk flush).
4. **Do not add feature logging yet — that begins with Epic-015.** Confirmed unchanged: no log call exists in any existing feature (Camera Management, Discovery, Live View, Recording, Playback, Dashboard, `VSP.Infrastructure`) in this Epic.

Manual validation of the three exception paths (UI/background/unobserved-task), against the adjusted implementation, is recorded in §8.

---

# Objective

Give VSP a minimal, in-process logging mechanism — so that (a) an otherwise-silent crash or unhandled exception is captured to disk instead of lost, and (b) any future Epic has a `Log` call available to it — without introducing any external package, telemetry, cloud dependency, or database logging.

This Epic delivers the **mechanism only**. It does not instrument existing features with log calls (see Out of Scope, §6) — that is what "Foundation" means here, consistent with how Epic-011/012 delivered Recording/Playback foundations before later Epics build on them.

---

# Current-State Analysis Summary

Verified directly against the repository, not assumed:

- **Zero logging today.** Repository-wide search for `ILogger`, Serilog, NLog, log4net, `Console.WriteLine`, `Debug.WriteLine`, and `Trace.WriteLine` returns no matches anywhere in the codebase. No unhandled-exception handling exists in `VSP.UI/App.xaml.cs` — `OnStartup` only constructs `DatabaseService`/`DatabaseInitializer`; an exception anywhere in the app today has no capture path at all.
- **`VSP.Core` already has an empty `Logging\` folder** declared in `VSP.Core.csproj` (`<Folder Include="Logging\" />`, alongside empty `Configuration\`/`Extensions\`/`Services\` folders) — scaffolded in advance, never populated. This is the natural home for the new logging types.
- **Project reference graph** (verified from each `.csproj`): `VSP.Core` and `VSP.Domain` are the two base projects with no internal dependencies. `VSP.Device` and `VSP.Player` both reference `VSP.Core`. `VSP.UI` references `VSP.Core`, `VSP.Device`, `VSP.Infrastructure`, `VSP.Player`. **`VSP.Infrastructure` references only `VSP.Domain` — it does not reference `VSP.Core`.** Putting the logger in `VSP.Core` therefore makes it reachable from `VSP.UI`, `VSP.Device`, and `VSP.Player` without any new reference, but *not* from `VSP.Infrastructure` (e.g. `DatabaseService`) without adding one. See Open Question 4 (§7).
- **No DI container exists anywhere in the codebase.** Every existing cross-cutting dependency is wired by hand — `DatabaseService`/`DatabaseInitializer` constructed directly in `App.xaml.cs`; `RecordingPathProvider` (`VSP.Player.Recording`) is a plain `static` class reading a JSON config file under `%LocalAppData%\VSP`, explicitly documented as "the smallest seam... for this and future Epics." A static gateway is the pattern consistent with the rest of the codebase, not constructor-injected `ILogger` instances threaded through every ViewModel.
- **`%LocalAppData%\VSP`** is the established convention for VSP's own runtime state on disk (`vsp.db` since Epic-013, `recording-settings.json` since Epic-011) — a log directory belongs alongside them, e.g. `%LocalAppData%\VSP\Logs\`.
- No existing test infrastructure for logging; `RecordingPathProviderTests.cs` is the closest precedent for testing a static, file-backed component via a configurable-directory test seam.

---

# Scope — In Scope

1. **`LogLevel`** — a minimal enum: `Debug`, `Info`, `Warning`, `Error`, `Fatal`.
2. **`ILogger`** — a minimal interface: `Log(LogLevel level, string message, Exception? exception = null)`.
3. **`FileLogger`** — the one production `ILogger` implementation: appends plain-text lines to a rolling log file, in a **fixed format**: `yyyy-MM-dd HH:mm:ss.fff | LEVEL  | message`, followed by an indented `exception.ToString()` block on the next line when an exception is supplied. No caller may vary this format. Thread-safe (single `lock` around the file write — matches the simplicity of the rest of the codebase, no async I/O pipeline).
4. **Rolling strategy: one file per calendar day**, named exactly `YYYY-MM-DD.log` (e.g. `2026-08-01.log`) under `%LocalAppData%\VSP\Logs\`, per Product Owner naming requirement. No size-based rolling, no compression, no archival.
5. **Retention: 30 days**, fixed default (`FileLogger.DefaultRetentionDays = 30`), not user-configurable in v1.0. A purge runs once at startup: any `YYYY-MM-DD.log` file whose date is older than 30 days from today is deleted.
6. **`AppLog`** — a static gateway (`AppLog.Initialize(ILogger)` + static `Debug`/`Info`/`Warning`/`Error`/`Fatal` methods) so call sites don't need an injected instance, matching the codebase's existing static/hand-wired conventions. Defaults to a no-op logger before `Initialize` is called, so any test or code path that doesn't explicitly wire logging never throws or writes to disk unexpectedly.
7. **Global unhandled exception capture**, wired in `VSP.UI/App.xaml.cs` `OnStartup`, split by the Product Owner's two-category decision:
   - `Application.DispatcherUnhandledException` (UI-thread exceptions) = **Recoverable**: `AppLog.Error` → `MessageBox.Show` (generic, non-localized message) → `e.Handled = true` → application continues running.
   - `AppDomain.CurrentDomain.UnhandledException` (non-UI-thread, terminating exceptions) = **Fatal**: `AppLog.Fatal` (synchronous, flushed write) → deliberate `Environment.Exit(1)` as the graceful-shutdown path, rather than leaving the process to whatever default crash behavior Windows/.NET would otherwise show. The CLR terminates this path regardless of any handler — "graceful" means we choose the exit deterministically after the log write is guaranteed to have completed, not that termination itself is prevented.
   - `TaskScheduler.UnobservedTaskException` (unobserved `Task` exceptions) — not explicitly named in the Product Owner's two categories; classified here as **Recoverable** (modern .NET does not terminate the process for this by default): `AppLog.Error` → `e.SetObserved()`. No message box (no meaningful UI-thread context to show one from).
8. **`VSP.Infrastructure` gains a `ProjectReference` to `VSP.Core`**, per Product Owner approval, so Infrastructure-layer code has the same `AppLog` available as everything else. No log call is added inside `VSP.Infrastructure` itself in this Epic — see §6 Out of Scope.

---

# Out of Scope

- Any external logging framework or package — explicit Product Owner instruction, confirmed exhaustively at approval: Serilog, NLog, log4net, `Microsoft.Extensions.Logging`, ETW, OpenTelemetry, Elastic, or any other.
- Telemetry, crash-reporting services, network logging, or any cloud log shipping — explicit Product Owner instruction.
- Database-backed logging (no SQLite log table) — explicit Product Owner instruction.
- Instrumenting any existing feature (Camera Management, Discovery, Live View, Recording, Playback, Dashboard) with actual log calls. This Epic delivers the mechanism only; adding `AppLog.Info(...)` calls into existing features — including inside `VSP.Infrastructure`, despite it gaining the `VSP.Core` reference — is a separate, later decision.
- An in-app log viewer, log export button, or any Settings-page UI for logging (Settings itself is a separate, not-yet-approved Epic).
- Runtime-configurable minimum log level (e.g. a Settings toggle) or retention period. Both are fixed constants for v1.0 (log everything; 30-day retention), not user-facing.
- The 30-day retention figure is explicitly distinct from the "Retention Days" Setting in `Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.4, which governs recordings, not logs — the two are unrelated numbers that happen to share a concept name.

---

# Risk Ceiling

**MEDIUM** — a new internal service/component (`AI_OPERATING_SYSTEM.md` §7 MEDIUM example), additive only. No database schema change, no public API break to existing types, no new external package, no security-model change, no DI container introduced.

---

# Definition of Done

1. A UI-thread exception is logged, shown to the user via a message box, and the application continues running. A non-UI-thread (fatal) exception is logged and the process exits deliberately via `Environment.Exit`. An unobserved `Task` exception is logged and marked observed. All three verified by manual reproduction.
2. A plain-text log file, named exactly `YYYY-MM-DD.log`, rolls to a new file at the start of each calendar day, under `%LocalAppData%\VSP\Logs\`, in the fixed line format defined in §5 item 3.
3. `LogLevel` (`Debug`/`Info`/`Warning`/`Error`/`Fatal`) is supported end-to-end from `AppLog` through to the written line, with no filtering — every level is always written.
4. Log files older than 30 days are deleted automatically on startup.
5. No new NuGet package added; no telemetry, ETW, OpenTelemetry, cloud, network, or database sink exists anywhere in the change.
6. `FileLogger`/`AppLog` have unit test coverage using a configurable-directory-and-clock seam (matching `RecordingPathProviderTests` convention); full existing suite remains green; build stays passing with no new warnings; `Docs/CHANGELOG.md` and `Docs/03_PRODUCT_ROADMAP.md` updated.

---

# Implementation Plan

### Files to add
- `VSP.Core/AssemblyInfo.cs` — `[assembly: InternalsVisibleTo("VSP.Tests")]`, matching `VSP.Player/AssemblyInfo.cs`'s existing convention (needed for `FileLogger`'s internal clock-injection test seam).
- `VSP.Core/Logging/LogLevel.cs`
- `VSP.Core/Logging/ILogger.cs`
- `VSP.Core/Logging/FileLogger.cs`
- `VSP.Core/Logging/AppLog.cs`
- `VSP.Tests/Logging/FileLoggerTests.cs`
- `VSP.Tests/Logging/AppLogTests.cs`

### Files to modify
- `VSP.Infrastructure/VSP.Infrastructure.csproj` — add `<ProjectReference Include="..\VSP.Core\VSP.Core.csproj" />` (Product Owner-approved; no code inside `VSP.Infrastructure` otherwise changes).
- `VSP.UI/App.xaml.cs` — construct `FileLogger`, call `AppLog.Initialize`, purge old logs, wire the three global exception handlers in `OnStartup`.
- `Docs/CHANGELOG.md` — Epic-014 entry on completion.
- `Docs/03_PRODUCT_ROADMAP.md` — mark Logging Foundation delivered, on completion.

### Files not to touch
- Every existing feature area (Camera Management, Discovery, Live View, Recording, Playback, Dashboard, Settings placeholder) — no log calls added to any of them in this Epic, including no new code inside `VSP.Infrastructure` beyond its `.csproj` reference.
- No SQLite schema/table changes (`CameraTable.cs`, `DatabaseInitializer.cs` untouched).

### Sequence
1. `LogLevel`, `ILogger`, `FileLogger` (rolling text writer + 30-day retention purge) + `VSP.Core/AssemblyInfo.cs` + `FileLoggerTests` (temp-directory seam, verifies file creation, rolling-by-day, fixed-format line content, retention purge boundary, thread-safety under concurrent writes).
2. `AppLog` static gateway + `AppLogTests` (no-op default before `Initialize`, delegates correctly after).
3. `VSP.Infrastructure.csproj`: add the `VSP.Core` reference.
4. `App.xaml.cs` wiring: construct `FileLogger`, `AppLog.Initialize`, purge, the three exception handlers. Manual verification of each handler (throw synchronously on UI thread → error dialog + continue; throw on a background thread → logged fatal + `Environment.Exit`; throw an unobserved `Task` exception → logged, no crash) — not unit-testable in the same way as `FileLogger` itself.
5. Build + full suite; `Docs/CHANGELOG.md` and `Docs/03_PRODUCT_ROADMAP.md` updates; Epic Review.

### Compatibility impact
Purely additive. No existing public member changes. `App.xaml.cs`'s `OnStartup` gains new statements but its existing `DatabaseService`/`DatabaseInitializer` construction is untouched.

### Test plan
- `FileLoggerTests`: file gets created under an injectable test directory; a new file starts when the injected "current day" seam advances; each `LogLevel` appears correctly in the written line; concurrent calls from multiple threads don't corrupt or interleave a single line.
- `AppLogTests`: calling `AppLog.Debug/Info/Warning/Error/Fatal` before `Initialize` is a no-op (does not throw, does not write); after `Initialize`, calls are delegated to the supplied `ILogger` with the right level/message/exception.
- Full existing suite (currently 611/611 passing per Epic-013) must remain green.

### Rollback
If implementation needs to be reverted: delete the five new `VSP.Core/**` files and the two new `VSP.Tests/Logging/*.cs` files, revert `VSP.Infrastructure/VSP.Infrastructure.csproj`'s added `ProjectReference`, and revert `App.xaml.cs` to its current form (construct `DatabaseService`/`DatabaseInitializer` only). No other file is touched, so rollback is a clean revert of exactly seven new files plus two modified files.

---

# 8. Manual Validation (2026-08-01, post-adjustment)

Performed against the built `VSP.UI.exe` (Debug), not inferred from code reading. Driven via a temporary, env-var-gated trigger added to `App.xaml.cs` for this purpose only and removed immediately afterward (confirmed absent from the final diff) — no test-only code shipped. Windows UI Automation (`System.Windows.Automation`) was used to read the real dialog's rendered text and dismiss it; process state was read via `Get-Process`/exit codes; log content was read directly from disk.

**UI-thread exception (recoverable):**
- Triggered a synchronous throw on the UI thread. A window titled "VSP - Unexpected Error" appeared within under a second.
- Its actual rendered text, read via UI Automation (not reconstructed from source): *"An unexpected error occurred. VSP will continue running, but you may want to save your work and restart. Error ID: 10C4E58C. If you contact support, please send the latest log file below along with this Error ID: C:\Users\game2\AppData\Local\VSP\Logs\2026-08-01.log"* — confirms Error ID and log-file guidance are both present, per adjustment 2.
- Dismissed via its OK button (localized "確定" on this system). After dismissal: dialog gone, `VSP.UI` process still running and `Responding: True`, main window ("VSP") still present — confirms continue-after-log behavior.
- Log file contained a matching `ERROR` line with `[ErrorId: 10C4E58C]` and the full exception/stack trace.

**Background-thread exception (fatal):**
- Triggered a throw on a plain `Thread`. Process exited on its own; `HasExited: True`, `ExitCode: 1` — confirms deliberate `Environment.Exit(1)` path.
- Log file contained a matching `FATAL` line with its own Error ID and full exception/stack trace, confirmed present (readable from disk) after process exit — consistent with adjustment 3 (explicit flush-to-disk before the process could exit).

**Unobserved Task exception:**
- Triggered a faulting `Task.Run` left deliberately unobserved, then forced two `GC.Collect()`/`WaitForPendingFinalizers()` passes to surface it.
- Process remained running and `Responding: True` throughout — confirms no crash.
- Log file contained a matching `ERROR` line (`System.AggregateException: ... unobserved exception was rethrown by the finalizer thread`) with its own Error ID and inner exception detail.

**Not covered by this pass:** the 30-day retention purge's real-world effect (covered by `FileLoggerTests`' unit coverage instead, which controls the clock directly — a real 30-day wait is impractical); the flush-to-disk guarantee under an actual OS-level crash (`FileStream.Flush(true)` was exercised on every write above, including immediately before `Environment.Exit`, but a genuine process kill / power-loss scenario was not simulated).
