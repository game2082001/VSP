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

- Audit Log, License, Plugin, Notification, Health Monitor
- User/Role/Permission shipped in v1.0 as a deliberately minimal foundation (Epic-018, Admin/Operator only, local auth only) — **not** the full Enterprise identity/session model (LDAP/OAuth/JWT/MFA/SSO, User Management UI, multi-user/remote sessions), which remains Version 4.0/Future per Vision §21. Database Backup/Restore shipped in v1.0 as SQLite-file-only (Epic-017, Accepted/Frozen 2026-08-06) — the full Enterprise Backup/Restore model (Vision §14) remains Future. Audit Log/License/Plugin/Notification/Health Monitor are not required for v1.0 GA and remain fully unscheduled.

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

# Version 0.16.0 — Settings Foundation

Completed, Product Owner Accepted (2026-08-02)

- Epic-016: a working Settings screen persisting the four v1.0 fields — Recording Path, Retention Days, Language, Theme — reusing Epic-011's config-file-backed seam. New `VSP.Core/Configuration/` (`SettingsFileStore`/`SettingsFileContents`/`RecordingRootDefaults`) is the single reader/writer for `recording-settings.json`, shared by `RecordingPathProvider` (internal implementation only, public behavior unchanged, verified by its existing tests passing unmodified) and the new `VSP.Infrastructure.Settings.AppSettingsProvider` (`Load()`/`Save()` only, a single immutable snapshot). `VSP.UI.Services.ThemeService` owns all theme-selection logic — `System` resolves via one registry read at startup, falls back to Dark on any failure, never throws. Changing Recording Path takes effect immediately after Save (no restart) since `RecordingPathProvider` re-reads from disk on every call rather than caching.
- Manual Validation against the actual built exe (Windows UI Automation, real screenshots, full process restarts) caught and corrected one real defect before acceptance: `SettingsView.xaml` shipped with hardcoded colors instead of binding to the theme brushes, so switching themes had no visible effect anywhere, including on Settings itself — fixed. All four Theme scenarios (System/Light/Dark crossed with the OS's own Light/Dark mode) then verified correct after restart; Recording Path, Retention Days, and Language persistence each independently verified the same way. See `Docs/SPECS/EPIC-016_SETTINGS_FOUNDATION.md` §13-14.
- Deliberately out of scope (Product Owner direction): actual retention cleanup (value is persisted only, no deletion/rotation), full localization (`.resx`/`CurrentUICulture`), full theme migration beyond the switching mechanism and Settings' own background/text, live reaction to an OS theme change while running, moving/rewriting existing recordings on a path change.
- Technical debt recorded: **TD-033** Theme Migration (~23 existing Views/Styles remain hardcoded, not retrofitted to `DynamicResource`); TD-034 (Language has no translated resources yet); TD-035 (`System` theme resolved at startup only); **TD-037** Settings UX improvements (unsaved-changes detection, Restore Defaults). None implemented now, per Product Owner direction.
- **Frozen** — any future enhancement to Settings/Theme is a new Epic; Epic-016 is not reopened except for a confirmed defect.

---

# Version 0.17.0 — Database Backup / Restore Foundation

Completed, Product Owner Accepted (2026-08-06) — **Frozen**

- Epic-017: a minimal, manual Backup/Restore capability for VSP's one SQLite database (`%LocalAppData%\VSP\vsp.db`) — Backup via the SQLite Online Backup API to a user-chosen destination (never blocked by an active recording); Restore via a nine-step validate/confirm/rename-aside/stage/install/re-validate/rollback flow (destructive-action confirmation, blocked while a recording is active, a kept timestamped pre-restore safety copy, restart-required-then-clean-termination on success). Two new services (`DatabaseBackupService`, `DatabaseRestoreService`) plus two result types in `VSP.Infrastructure/Database/`, an additive Backup/Restore section on the existing Settings screen — no new screen, no generic backup framework, no schema change, no new external package.
- Manual Validation (2026-08-06) against the actual built `VSP.UI.exe` and the real `vsp.db` — 6/6 Pass on the 6 items executed. Item 7 (a forced-filesystem-failure rollback proof under real conditions) was deliberately deferred by Product Owner decision as outside required V1.0 GA acceptance scope, not a failure — two automated tests already prove rollback at two other forced-failure points; tracked as a future regression/robustness test. See `Docs/SPECS/EPIC-017_DATABASE_BACKUP_RESTORE_FOUNDATION.md` §11-14 for the full script, results, and Product Acceptance Report.
- Deliberately out of scope (Product Owner direction): scheduled/cloud backup, recording-file or settings-file backup, encryption, compression, backup-history management/pruning, import/merge Restore, self-relaunch after Restore, any User/Role work.
- **Frozen** — any future enhancement (including the deferred item-7 regression test) is a new Epic or a tracked follow-up; Epic-017 is not reopened except for a confirmed defect.

---

# Version 0.18.0 — User / Role Management Foundation

Completed, Product Owner Accepted (2026-08-06) — **Frozen**

- Epic-018: a minimal Admin/Operator authentication and permission gate — Login screen gates `MainWindow` construction entirely (never merely hidden), `User` table (PBKDF2-HMACSHA256 hash, 210,000 iterations, per-user random salt, zero new external package), mandatory forced password-change for the seeded default Admin on first login, and role-based nav/command gating across `MainWindowViewModel`, `CameraListViewModel`, `CameraDetailViewModel` (read-only Camera Detail for Operator), and `LiveViewViewModel` (Recording is Admin-only). No User Management UI, no default Operator account, no LDAP/OAuth/JWT/MFA/SSO, no generic permission engine — all per explicit Product Owner decisions (§8 of the spec).
- Manual Validation (2026-08-05/06) against the actual built `VSP.UI.exe` and the real `vsp.db` — 12/12 Pass. One item (Operator Recording restriction) was initially reported failing, investigated end-to-end without touching production code, and root-caused to a stale executable rather than a code defect — confirmed passing on re-test against the documented, freshly clean-rebuilt exe. See `Docs/SPECS/EPIC-018_USER_ROLE_MANAGEMENT_FOUNDATION.md` §11-14 for the full script, results, investigation, and Product Acceptance Report.
- Known, disclosed limitations (not defects): no way to reach the Operator role through normal use in v1.0 (no seeded default Operator account and no account-creation UI — direct database manipulation required); no self-service/discretionary Change Password; no end-to-end `MainWindowViewModel`/View automated test (this codebase has no STA test infrastructure, covered instead by manual validation plus STA-free unit tests of every gated command); no account lockout/idle timeout/"remember me" (explicitly out of scope for v1.0).
- **Frozen** — any future enhancement (User Management UI, self-service password change, account lockout, idle timeout) is a new Epic; Epic-018 is not reopened except for a confirmed defect.

---

# Current Status

Current Version: 0.18.0 (Epic-018 User / Role Management Foundation) is the last version actually assigned via `Directory.Build.props`'s `<Version>`; 0.17.0 (Epic-017) is now also Accepted/Frozen (§ above) though it completed acceptance after 0.18.0 chronologically. Both — Implementation Complete, Product Owner Accepted, Frozen, pending commit.

Current Epic: none — both remaining V1.0 GA blockers (Epic-018, Epic-017) are now Accepted and Frozen.

Product direction (2026-07-29 scope freeze, refined 2026-08-01 by `Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md`): planning is frozen for VSP v1.0 around User/Role (Admin + Operator only — done, Epic-018), Logging (Required — done, Epic-014), Settings (Recording Path/Retention Days/Language/Theme — done, Epic-016), Deployment (xcopy, done Epic-013), and Database Backup/Restore (SQLite file only — done, Epic-017). AI, Cluster, Cloud, Timeline, Analytics, Plugin, Mobile, and broader Enterprise capabilities remain Future. v1.0 priorities: (1) Playback Foundation — done Epic-012; (2) Logging Foundation — done Epic-014; (3) Error Handling Foundation — done Epic-015; (4) Settings Foundation — done Epic-016; (5) User/Role — done Epic-018; (6) Database Backup/Restore — done Epic-017 (2026-08-06, see Version 0.17.0 above). Per this document's own stated criterion ("(7) v1.0 GA once Epic-017 is accepted"), all Epics named as v1.0 GA blockers are now accepted — **formally declaring v1.0 GA is a distinct Product Owner decision this document does not make on its own** and has not yet been made explicitly; flagged here as the natural next step, not acted on.

Candidate next Epic: none proposed. Per explicit Product Owner instruction (2026-08-06), Epic-019 does not begin before v1.0 GA is explicitly declared.

---

# Roadmap Maintenance

Every Epic must update this file's relevant Version/Milestone entry as part of its own documentation step — do not wait for a separate review to catch up a backlog of undocumented Epics again.
