# Product Roadmap

Last updated: 2026-07-31 (post-Epic-013)

This is the single actively-maintained product roadmap for VSP. `Docs/03_ROADMAP.md` is a legacy, encoding-corrupted duplicate frozen at Milestone M1 / 62% — treat it as historical only, do not edit it further. `Docs/PROJECT_STATUS.md` is also stale (frozen at v1.0.0-m1 / 88 tests) and should be refreshed or retired. See the 2026-07-28 Product Status Report for the full review behind this update.

---

# Version 1.0 — Device Import MVP

Completed

- Import Framework, Validation, Duplicate Checker, Preview, Import Wizard, SQLite Import, Import Summary

---

# Version 1.1 — Device Management

Completed (Task-201 through Task-216)

- Camera List, Toolbar, Search, Detail, Filter, Edit, Save Persistence, Add Camera, Unsaved Changes Detection, Refresh/Reload, Delete, Batch Edit, Batch Connection Test, Export, Device Status Enhancement

Carried-forward technical debt: TD-021 (no duplicate check on Add), TD-022/TD-025 (no shared confirmation dialog component), TD-026 (no background/auto refresh) — see Product Status Report, Technical Debt Ledger.

---

# Version 1.2 — Driver Framework (Foundation)

Completed

- Driver Registry (Task-301), Driver Plugin (Task-302), Driver Settings metadata (Task-303)
- Driver Settings UI driven entirely from metadata, `ConnectionType` selector added (Epic-008)

Remaining

- Hikvision ISAPI, Dahua NetSDK, and Axis all still unimplemented — RTSP and ONVIF are the only real drivers today
- `DriverCompatibilityCapability` is `null` for Hikvision/Dahua, so Discovery treats both as unconditionally compatible (see Architectural Risks)

---

# Version 1.3 — Discovery

Completed

- ONVIF WS-Discovery (Task-401), RTSP Endpoint Probe (Task-402), Network Scan (Task-403), Auto Discovery Coordinator (Task-404)
- Driver Capability / Selection / Camera Factory / Registration foundations (Task-405–408)
- Discovery Orchestrator + 5 decorator hooks: Progress, Session, Retry, Timeout, Metrics, Diagnostics (Task-501–505, 601–607)
- Camera Discovery Workspace UI, folded into Camera Management as a feature rather than a top-level tab (Epic-006)

Remaining

- No ADR documents the Discovery subsystem's architecture (only Import and Media Pipeline have ADRs)
- Discovery Session/Metrics/Diagnostics sinks are still no-ops — no persisted discovery history is retrievable anywhere (Dashboard explicitly cannot show it, per Epic-009)
- No CIDR/subnet auto-enumeration — Network Scan/RTSP Probe require explicit target input

---

# Version 1.4 — Camera Connectivity & Workspace Consolidation

Completed

- Camera Management Workspace as the primary "Devices" navigation entry, replacing `DeviceCenterView` (Epic-005)
- RTSP `TestConnection` with Basic/Digest auth (Epic-003)
- ONVIF `TestConnection` + `GetDeviceInformation` (Epic-007) — every driver where `IsDriverImplemented() == true` is now a real implementation
- Dashboard replaced with a real read-only aggregation over Camera/Driver/Connection data (Epic-009)

---

# Version 1.5 — Live View & Recording (ADR-002 Media Pipeline)

Completed

- ADR-002 media pipeline architecture accepted; ADR-003 selects FFmpeg as the media library
- Live View Foundation: RTSP session, decode, render, bounded reconnect, performance baseline (Epic-010)
- Recording Foundation: Continuous stream-copy recording on the encoded tier, Start/Stop control + indicator, organized per camera since Epic-012 (Epic-011)
- Playback Foundation: file-backed `IMediaSession`, real `IMediaClock.Seek`, camera selection, recording list, Play/Pause/Stop/Seek (Epic-012)

Remaining — per ADR-002's Future Media Pipeline Evolution table, in dependency order

- v4 Motion-triggered recording / basic AI — `IAiPipeline`, `IMetadataBus`, `MotionTriggered` recording mode
- v5 Recording Server — `IRecordingSession` with zero `IFrameRenderer` subscribers (headless recording)
- v6 Transcoding — `IVideoEncoder` re-encoding onto the existing Dispatcher/Buffer pattern
- v7 Cluster — `Remote` session relaying another node, cross-node metrics aggregation
- v8 Cloud — cloud-relayed viewing and cloud storage as a Recorder target

Per the 2026-07-29 Product Owner scope freeze for VSP v1.0 (no AI, no Cloud, no Cluster, no Mobile), v4/v7/v8 above are deferred to v2.0+ and are not candidate next Epics until that freeze is lifted.

---

# Version 2.0 — Event Center (Planned, unscheduled, deferred past v1.0 GA)

- Motion, Alarm, Face, Vehicle, ANPR, Metadata — no work started; depends on the v4 AI pipeline above

---

# Version 3.0 — AI Device Center (Planned, unscheduled, deferred past v1.0 GA)

- AI Assistant, AI Device Analysis, AI Configuration, AI Diagnostics — aspirational, no task breakdown exists yet

---

# Version 4.0 — Enterprise (Planned, unscheduled)

- User, Role, Permission, Audit Log, Backup, Restore, License, Plugin, Notification, Health Monitor
- User/Role/Permission remain in scope for v1.0 GA per the 2026-07-29 scope freeze ("remaining v1.0 core capabilities"); Audit Log/Backup/Restore/Plugin/Notification/Health Monitor are not required for GA and may land after it.

---

# Version 0.13.0 — Deployment Foundation

Completed

- Epic-013: shared `Version` (0.13.0) across every project via `Directory.Build.props`; `vsp.db` moved to `%LocalAppData%\VSP` (was hardcoded next to the executable, which fails under a non-admin install location); a self-contained win-x64 publish profile (zero .NET runtime prerequisite on the target machine); trimmed the FFmpeg vendor package's non-Windows payload (411 MB -> 33 MB) from build/publish output. Verified: xcopy install to an arbitrary path, launch with no `dotnet`/MinGW reachable on `PATH`, and a real SQLite + FFmpeg functional smoke test against that installed copy.
- Deliberately out of scope (Product Owner direction — deployment, not distribution): no installer/wizard technology, no auto-update, no code signing, no branding/icons, no CI/CD, no single-file/ReadyToRun/AOT, no upgrade or uninstall semantics beyond xcopy replace-in-place.

---

# Version 0.14.0 — Logging Foundation

Completed, Product Owner Accepted (2026-08-01)

- Epic-014: `VSP.Core/Logging` (`LogLevel`, `ILogger`, `FileLogger`, `AppLog`) — a minimal, in-house, file-backed logging mechanism. Fixed-format lines, `YYYY-MM-DD.log` daily rolling file under `%LocalAppData%\VSP\Logs\`, explicit per-write flush-to-disk, 30-day retention purge on startup, log-everything (no level filtering) for v1.0. `VSP.Infrastructure` gained a `VSP.Core` reference ("logging is a platform capability, not a UI capability" — Product Owner). `VSP.UI/App.xaml.cs` wires three global unhandled-exception handlers, each tagged with a generated Error ID: UI-thread exceptions are recoverable (log, message box naming the Error ID and log file path, continue); non-UI-thread exceptions are fatal (log, deliberate `Environment.Exit`); unobserved `Task` exceptions are logged and marked observed. Manually validated against the built exe (not just unit tests) — see `Docs/SPECS/EPIC-014_LOGGING_FOUNDATION.md` §8.
- Deliberately out of scope (Product Owner direction — mechanism only, not distribution or instrumentation): no external logging framework (Serilog/NLog/log4net/`Microsoft.Extensions.Logging`), no ETW, no OpenTelemetry, no Elastic, no telemetry, no cloud or network logging, no database logging, no log viewer UI, no runtime-configurable level or retention, and no log calls added inside any existing feature — feature-level logging begins with Epic-015.
- Technical debt recorded: TD-029, `Environment.Exit(1)` as the fatal-shutdown path — acceptable for v1.0, a future Platform Lifecycle Epic should unify graceful shutdown across UI/Services/Plugins/distributed components. Not implemented now, per Product Owner direction.
- First entry under `Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.3 (Logging, moved from Optional to Required) to actually ship.
- **Frozen** — any future enhancement is a new Epic; Epic-014 is not reopened except for a confirmed defect.

---

# Version 0.15.0 — Error Handling Foundation

Completed, Product Owner Accepted (2026-08-01)

- Epic-015: consistent exception handling for six places where exceptions previously vanished silently — Database initialization (`DatabaseInitializer`, now returns `DatabaseInitializationResult { Success, Exception }` instead of throwing unhandled), Repository operations (`SQLiteCameraRepository`'s four methods, now log-and-rethrow with `ICameraRepository`'s 25-call-site contract unchanged), RTSP/ONVIF `TestConnection`/`GetDeviceInformation` (exception now bound and logged, `return false`/`null` unchanged), Retry failures (`RetryingDiscoveryRunner`, each non-final attempt now logged before retrying), and Media reconnect failures (`MediaController.ConnectionLoopAsync`, each failed reconnect attempt now logged). Redirected at proposal from an initial "Feature Logging" framing — normal business-event instrumentation (Camera Added, Recording Started, Playback Started) is explicitly deferred to a future Epic; this Epic is error paths only.
- Database-initialization failure gets its own explicit startup path: a single Error ID generated once and logged together with the original exception in one line (never split across two log entries), a dialog naming the Error ID and the current log file's path, then clean termination (`Environment.Exit(1)`) — the app never proceeds to `MainWindow` without a working database. Manually validated against the built exe by forcing a real `SqliteException` — see `Docs/SPECS/EPIC-015_ERROR_HANDLING_FOUNDATION.md` §12.
- Security review (Product Owner instruction): no log call added in this Epic includes a password, authorization header, token, or credential-bearing URL — verified by code review and asserted directly in several new tests.
- Deliberately out of scope: any feature/success-event instrumentation, any change to `ICameraRepository`'s public contract, `DiscoveryOrchestrator`'s own catch (only `RetryingDiscoveryRunner`'s was in scope), and every other currently-silent catch block not named in the six-item scope (candidates for a later Epic).
- Technical debt recorded: TD-030, Platform Lifecycle — a future version should replace direct `Environment.Exit` calls (now three: two from Epic-014, one from this Epic) with a unified lifecycle manager. Complements TD-029 (Epic-014). Not implemented now, per Product Owner direction.
- **Frozen** — any future enhancement is a new Epic; Epic-015 is not reopened except for a confirmed defect.

---

# Current Status

Current Version: 0.15.0 (Epic-015 Error Handling Foundation) — Implementation Complete, Product Owner Accepted, pending commit. Epic-013 (0.13.0, Deployment Foundation) remains separately pending its own commit in the same working tree — neither Epic-014's nor Epic-015's acceptance constitutes Epic-013 acceptance.

Current Epic: Epic-015 (Accepted, Frozen)

Product direction (2026-07-29 scope freeze, refined 2026-08-01 by `Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md`): planning is frozen for VSP v1.0 around User/Role (Admin + Operator only), Logging (Required — done, Epic-014), Settings (Recording Path/Retention Days/Language/Theme), Deployment (xcopy, done Epic-013), and Database Backup/Restore (SQLite file only, new). AI, Cluster, Cloud, Timeline, Analytics, Plugin, Mobile, and broader Enterprise capabilities remain Future. Remaining v1.0 priorities: (1) Playback Foundation — done Epic-012; (2) Logging Foundation — done Epic-014; (3) Error Handling Foundation — done Epic-015 (not itself one of the five named v1.0 capabilities, but a cross-cutting prerequisite the Product Owner prioritized ahead of them); (4) User/Role, Settings, and Database Backup/Restore, per `V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2 (sequencing not yet decided); (5) v1.0 GA.

Candidate next Epics: User/Role, Settings, or Database Backup/Restore, per `Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2 — sequencing is a Product Owner decision. The 2026-07-28 Product Status Report's candidate list is otherwise superseded by that document where the two disagree.

---

# Roadmap Maintenance

Every Epic must update this file's relevant Version/Milestone entry as part of its own documentation step — do not wait for a separate review to catch up a backlog of undocumented Epics again.
