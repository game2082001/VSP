# CHANGELOG
## 2026-08-18 (AI01-008 - Autonomous Multi-Agent Development Pipeline)

### Agent Router / Orchestrator Foundation

Status:
Implementation Complete - Pending Independent Review and Product Owner Acceptance. No commit, push, PR creation, autonomous merge, PR #7 remediation, or RTSP flaky investigation work performed.

Summary:
- Added `AI/Orchestrator/` as the PR-based orchestration layer for Agent Router policy, agent contracts, structured state, token budget gates, stop conditions, crash/session recovery, role separation, and bounded remediation.
- Added PowerShell orchestrator scripts under `tools/orchestrator/` for PR metadata inspection, parallel gate evaluation, token budget checking, state reading, review request, remediation request, and router entry.
- Added local GitHub workflow files for Windows CI, Claude Code Review, Claude Code comment handling, and AI01 Orchestrator routing.
- Preserved the first-version terminal state as `READY_FOR_MERGE`; Product Owner manual merge remains required.
- Added explicit PR #7 protection and kept the paused RTSP flaky investigation out of AI01-008 scope.

Verification:
- `AI/Orchestrator/Templates/task-state.template.json` parsed successfully with PowerShell `ConvertFrom-Json`.
- `tools/orchestrator/*.ps1` parsed successfully with PowerShell parser.
- Product runtime build/test not run because this task changes governance, workflows, and orchestration scripts only; existing RTSP/decoder worktree changes remain untouched.

Files:
- `AI/Orchestrator/**`
- `tools/orchestrator/**`
- `.github/workflows/**`
- `AGENTS.md`, `CLAUDE.md`, `AI/README.md`, `Docs/AI_DEVELOPMENT_WORKFLOW.md`, `Docs/WORKFLOW/IMPLEMENT_TASK.md`, `Docs/WORKFLOW/REVIEW_TASK.md`, `Docs/CHANGELOG.md`

---
## 2026-08-14 (RC1 Manual E2E Closure — Items K/L/M/N, Task-AI00B)

### RC1 Manual E2E Validation Complete — 14/14 PASS

Status:
**RC1 Manual E2E = 14/14 PASS.** Items A through N of `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md` are all real-device validated by the Product Owner against the `VSP_v1.0.0-RC1_RC1-R05_win-x64` artifact. No defect found in Items K, L, M, or N; no production code change made for any of them.

Summary:
- **Item K — Database Restore**: Pass. Test camera `RESTORE-TEST-DELETE-ME` created and reverted correctly by restore; `89-R` and its settings survived; pre-restore safety backup `vsp.pre-restore.20260814-190428.db` (24 KB) created and retained.
- **Item L — Operator login / role restrictions**: Pass. Verified with a real Operator account against the source-confirmed permission matrix (navigation, Devices, Camera Detail, Live View recording, Playback) traced ahead of the test from `CameraListViewModel.cs`, `CameraDetailViewModel.cs`, `LiveViewViewModel.cs`, and `MainWindowViewModel.cs`.
- **Item M — Logout / login again**: Pass. Confirms `App.xaml.cs`'s `StartSession()` recursion correctly tears down an Operator session and rebuilds a fresh Admin session with the full 5-item navigation.
- **Item N — Application restart and persistence check**: Pass. Confirms no login-session persistence (by design) while the SQLite-backed camera/user data, the separate Settings JSON file, on-disk recordings, and both backup files all survive a full process restart untouched.
- No production code was changed by any of these four items — all are real-device confirmation of already-implemented, already-tested behavior.

Verification:
- `dotnet build VSP.slnx -c Debug`: 0 errors (same pre-existing `NU1903` advisory warnings only).
- `dotnet test VSP.Tests -c Debug --no-build`: 907 passed / 1 failed / 0 skipped / 908 total. The one failure, `RtspMediaSessionIntegrationTests.OpenAsync_AgainstRealFfmpegEncodedStream_ReceivesAndDecodesRealFrames`, is the same pre-existing, documented full-suite-load timing flake (confirmed passing in isolation, 2026-08-14 re-run); reported as-is, not normalized.
- `git diff --check`: exit code 0 (no whitespace/conflict-marker errors; only pre-existing LF→CRLF autocrlf notices).

Files:
- `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md` (Items K/L/M/N marked Pass with evidence, final status)
- `Docs/03_PRODUCT_ROADMAP.md`, `Docs/RELEASES/V1.0_RC1_ACCEPTANCE_REPORT.md` (updated to reflect 14/14)
- `Docs/CHANGELOG.md` (this entry)

Not covered by this entry (deliberately not decided here): Pilot Ready, GA Ready, or Production Ready — these remain distinct Product Owner release-gate decisions, and the working tree remains uncommitted.

---

## 2026-08-14 (RC1 Remediation — RC1-R04 / RC1-R05, Task-AI00B)

### RC1-R04 — Recording/Playback Storage Contract & RC1-R05 — Playback Frame Presentation Lifecycle

Status:
Implementation Complete — Test Complete — Real-Device Validated — PASS — CLOSED. Both found and fixed while preparing/executing RC1 Manual E2E Item H (Playback). Closes Item G and Item H. Uncommitted, pending user commit.

Summary:
- **RC1-R04** (`Docs/SPECS/RC1-R04_RECORDING_PLAYBACK_STORAGE_CONTRACT.md`): `LiveViewViewModel`'s production `MediaController` factory never supplied a `cameraId`, so every recording landed flat in `%LocalAppData%\VSP\Recordings\` regardless of camera, even though `MediaController`/`RecordingPathProvider`/`RecordingCatalog`/Playback already implemented and expected a per-camera subfolder. New recordings from Live View were therefore never discoverable from Playback. Fixed by widening `LiveViewViewModel`'s controller-factory delegate to carry `camera.Id` (the `Camera` instance already in scope at the real call site) straight into `MediaController`, so recordings now land under `<Recording Root>\<cameraId:N>\`, matching Playback's existing scan contract exactly. Pre-fix flat-root recordings are left untouched — no deterministic camera-ownership signal exists in their filenames to safely auto-migrate them.
- **RC1-R05** (`Docs/SPECS/RC1-R05_PLAYBACK_FRAME_PRESENTATION_LIFECYCLE.md`): once RC1-R04 made a recording discoverable, real-device retest of Playback found `PlaybackViewModel` had never received the RC1-R03 fix applied to `LiveViewViewModel` — it never subscribed to `Renderer.FrameRendered`, so clicking Play showed the position timeline advancing while the video area stayed black; Pause incidentally revealed a frame (via an unrelated `StateChanged`-triggered property refresh); Resume then played normally; Stop → Play repeated the same race deterministically on the freshly-constructed controller/renderer. Fixed by mirroring RC1-R03 exactly: `PlaybackViewModel` now subscribes/unsubscribes `Renderer.FrameRendered` alongside `StateChanged`, raising `PropertyChanged(nameof(CurrentFrameSource))` on every frame.
- Both real-device validated by the Product Owner (2026-08-14) against dedicated artifacts (`VSP_v1.0.0-RC1_RC1-R04_win-x64`, then `VSP_v1.0.0-RC1_RC1-R05_win-x64`). RC1-R04: Live View → Start/Stop Recording → per-camera storage → Playback auto-discovery, zero manual steps. RC1-R05: Play immediately shows video (no black screen), Pause/Resume/Stop, and a repeated Stop→Play cycle all confirmed working.
- `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md`: Item G and Item H marked Pass; Recording Path documentation corrected to the fixed per-camera contract, with the pre-fix flat-root behavior preserved as an explicit historical note (not silently rewritten away).

Verification:
- `dotnet build VSP.slnx -c Debug`: 0 errors (pre-existing `NU1903`/xUnit-style warnings only).
- `dotnet test VSP.Tests -c Debug --no-build`: 907 passed / 1 failed / 0 skipped / 908 total. The one failure, `RtspMediaSessionIntegrationTests.OpenAsync_AgainstRealFfmpegEncodedStream_ReceivesAndDecodesRealFrames`, is the same pre-existing, documented full-suite-load timing flake carried since Epic-010/011 (confirmed passing 2/2 in isolation); this run is reported exactly as-is, not normalized to "clean," per explicit instruction to keep the automated baseline and real-device Pass status distinct.
- `git diff --check`: exit code 0 (no whitespace/conflict-marker errors; only pre-existing LF→CRLF autocrlf notices).

Files:
- `VSP.UI/ViewModels/LiveViewViewModel.cs` (RC1-R04), `VSP.UI/ViewModels/PlaybackViewModel.cs` (RC1-R05)
- `VSP.Tests/Player/LiveViewViewModelTests.cs`, `MediaControllerRecordingTests.cs`, `RecordingCatalogTests.cs`, `RecordingPathProviderTests.cs` (RC1-R04); `PlaybackViewModelTests.cs` (RC1-R05)
- `Docs/SPECS/RC1-R04_RECORDING_PLAYBACK_STORAGE_CONTRACT.md`, `RC1-R05_PLAYBACK_FRAME_PRESENTATION_LIFECYCLE.md` (new)
- `Docs/CHANGELOG.md` (this entry), `Docs/03_PRODUCT_ROADMAP.md`, `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md`

---

## 2026-08-10 (RC1 Remediation — Traceability Recovery, Task-AI00B Phase 1)

### RC1-R01 / RC1-R02 / RC1-R03 — Retroactive Specification for RC1 Post-Commit Remediation

Status:
Documentation only — **no production code or test changed by this entry.** This entry retroactively establishes Specification First traceability for implementation, tests, and (where applicable) real-device validation that already existed, uncommitted, in the working tree as of Task-AI00A (2026-08-10 assessment). It does not itself commit that code. See `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md` for the original discovery/fix narrative this entry formalizes.

Summary:
- **RC1-R01 — RTSP Port Runtime Resolution** (`Docs/SPECS/RC1-R01_RTSP_PORT_RUNTIME_RESOLUTION.md`): `Camera.RtspPort` was persisted but never consulted at runtime (`RtspCameraDriver.TestConnection` hardcoded `554` as its fallback). Fixed via new `VSP.Domain/RtspEndpointResolver.cs` plus session-edit-intent-aware save normalization (`DriverSettingEditorViewModel.WasExplicitlyEdited`, `CameraDetailViewModel.NormalizeRtspEndpoint`). **Implementation Complete, Test Complete (13 tests: 4 in `RtspEndpointResolverTests.cs` + 2 in `RtspCameraDriverTests.cs` + 7 in `CameraDetailViewModelTests.cs`), real-device validated** by Product Owner manual retest against camera 192.168.0.89:1025 — confirmed Pass.
- **RC1-R02 — Camera Detail Field Editability** (`Docs/SPECS/RC1-R02_CAMERA_DETAIL_FIELD_EDITABILITY.md`): driver-setting fields (Port, Username, RtspUrl, etc.) were permanently read-only in Camera Detail for every connection type, due to an unqualified `{Binding IsEditMode}` inside a reused `ItemsControl` template whose `DataContext` has no such property. Fixed via `{Binding DataContext.IsEditMode, RelativeSource={RelativeSource AncestorType=Window}}` in `CameraDetailWindow.xaml` — after one incorrect attempt (which additionally broke the working Name/Model/IP Address/Location fields) was caught and withdrawn by a Product Owner real-device retest. **Implementation Complete, real-device validated** (192.168.0.89:1025, confirmed Pass); **no automated test exists for this binding fix** (disclosed limitation — this codebase has no STA/UI-Automation test infrastructure, consistent with the gap already recorded in Epic-018).
- **RC1-R03 — Live View `CurrentFrameSource` Binding** (`Docs/SPECS/RC1-R03_LIVEVIEW_CURRENTFRAMESOURCE_BINDING.md`): `LiveViewViewModel` never subscribed to the pre-existing `IFrameRenderer.FrameRendered` event, so `CurrentFrameSource`'s `PropertyChanged` was never raised and the Live View `Image` control's bound source stayed frozen even while frames decoded and rendered successfully underneath it. Fixed via `HandleFrameRendered` subscribe/unsubscribe wiring in `LiveViewViewModel`. **Implementation Complete, Test Complete (3 tests)** — tracked independently of Defect 3/4 per explicit Product Owner instruction, because this fix is not named anywhere in the existing Manual E2E Validation checklist narrative. **Validation Pending — no real-device evidence exists in the repository. Not Product Owner Accepted.**

Explicitly NOT covered by this entry (remain Validation Pending / BLOCKED, unaffected by this documentation pass):
- **Defect 3 — ONVIF Media Stream URI Resolution** and **Defect 4 — RTSP/ONVIF Playback Credential Propagation** (both "Item F" in the Manual E2E Validation checklist) — implementation and unit tests exist in the working tree, but real-device retest against 192.168.0.89:1025 has not been performed. Not documented as Specs in this pass; will be handled in a future Task-AI00B phase once Product Owner real-device validation is available.
- Item F diagnostic instrumentation (`VSP.Player/Control/MediaController.cs`, `VSP.Player/Decoder/FfmpegVideoDecoder.cs`, `VSP.Player/Decoder/RtspMediaSession.cs`) — retained as-is, unrelated to this documentation pass.

Verification:
- `dotnet build VSP.slnx -c Debug`: 0 Error (pre-existing `NU1903` advisory warnings unchanged).
- `dotnet test VSP.Tests -c Debug --no-build`: baseline unchanged by this documentation-only pass — see Task-AI00B Phase 1 Completion Report for the exact re-run result.

Files:
- `Docs/SPECS/RC1-R01_RTSP_PORT_RUNTIME_RESOLUTION.md`, `RC1-R02_CAMERA_DETAIL_FIELD_EDITABILITY.md`, `RC1-R03_LIVEVIEW_CURRENTFRAMESOURCE_BINDING.md` (new)
- `Docs/CHANGELOG.md` (this entry)
- `Docs/03_PRODUCT_ROADMAP.md`, `Docs/RELEASES/V1.0_RC1_MANUAL_E2E_VALIDATION_CHECKLIST.md` (updated for cross-reference — see those files)

---

## 2026-08-06 (Epic-017)

### Version 0.17.0 - Epic-017 Database Backup / Restore Foundation

Status:
Implementation Complete — Product Owner Accepted — **Frozen** (uncommitted — pending user commit)

Summary:
- Objective: a minimal, manual Backup/Restore capability for VSP's one SQLite database (`%LocalAppData%\VSP\vsp.db`) — a user can manually create a backup at a destination of their choosing and manually restore from a backup file of their choosing, with a destructive-action confirmation, pre-replacement validation, and a guarantee that a failed Restore never leaves the app without a usable database.
- `VSP.Infrastructure/Database/DatabaseBackupService.cs`: Backup via `SqliteConnection.BackupDatabase` (the SQLite Online Backup API), safe against a live, in-use database — never blocked by an active recording. `DatabaseRestoreService.cs`: `ValidateBackupFile` applies the six-point checklist (exists, non-empty, not the live file, opens read-only, `integrity_check = ok`, `Camera` table present); `Install` executes the nine-step validate/confirm/rename-aside/stage/install/re-validate/rollback flow — same-volume atomic `File.Move`, `SqliteConnection.ClearAllPools()` before touching the live file, and a rollback that renames the pre-restore copy back into place on any failure from the install step onward.
- `SettingsViewModel` gains `BackupCommand`/`RestoreCommand` and four new injected delegates (two file pickers in `SettingsView.xaml.cs` via `SaveFileDialog`/`OpenFileDialog`, `confirmRestore`, `showRestartRequiredAndExit`); `MainWindowViewModel` wires them, matching the existing `confirmCreateFolder` delegate-injection convention. Restore is blocked while a recording is active with a clear message; a successful Restore shows a restart-required message and terminates cleanly (`Environment.Exit(0)`) once acknowledged — no automatic relaunch.
- Manual Validation (2026-08-06) against the actual built `VSP.UI.exe` and the real `%LocalAppData%\VSP\vsp.db` (safety backup taken first) — **6/6 Pass** on the 6 items executed. Item 7 (a forced-filesystem-failure rollback proof under real conditions) was deliberately deferred by explicit Product Owner decision as outside required V1.0 GA acceptance scope — not a failure, not attempted; two automated tests already prove rollback at two other forced-failure points. Full transcript in `Docs/SPECS/EPIC-017_DATABASE_BACKUP_RESTORE_FOUNDATION.md` §11-14.
- A pre-existing, unrelated bug was found and fixed during acceptance prep: `Directory.Build.props` contained `--` inside an XML comment (invalid XML), breaking every project's build. Corrected the punctuation only — no other change to that file's content.

Technical Debt / Known Limitations:
- No manual, real-filesystem proof of the Restore rollback guarantee specifically at the post-install re-validation step (step 7) — deferred by Product Owner decision; tracked as a future regression/robustness test. Two automated tests cover rollback at two other forced-failure points instead.
- Settings, and therefore Backup/Restore, is Admin-only (Epic-018) — Operators cannot reach this screen at all.
- Backup files are unencrypted and carry the live database's plaintext camera credentials forward — a carried-forward, not new, exposure; encryption explicitly out of scope for v1.0.
- No scheduled/cloud backup, no recording-file/settings-file backup, no backup-history management or pruning, no import/merge Restore, no self-relaunch after Restore — all explicitly out of scope for v1.0 by Product Owner direction.

Verification:
- New tests: `DatabaseBackupServiceTests`, `DatabaseRestoreServiceTests` (26 tests total, including two forced-I/O-failure rollback tests).
- Full suite: 758/758 effectively passing on a clean (`dotnet clean` + from-scratch `dotnet build -c Release`) rebuild — the one full-suite failure is the same pre-existing, unrelated FFmpeg/RTSP timing flake documented in Epic-018, confirmed passing in isolation.
- Manual validation (2026-08-06) against the actual built `VSP.UI.exe`: 6/6 Pass on executed items, 1 item deliberately deferred by Product Owner decision. See `Docs/SPECS/EPIC-017_DATABASE_BACKUP_RESTORE_FOUNDATION.md` §11 for the full script and results, and §12-14 for the Product Acceptance Report, Final Validation Summary, and Known Limitations.

Files:
- VSP.Infrastructure/Database/DatabaseBackupService.cs, DatabaseBackupResult.cs, DatabaseRestoreService.cs, DatabaseRestoreResult.cs (new)
- VSP.Infrastructure/Database/DatabaseService.cs (`GetDatabaseFilePath()`/`GetDatabaseDirectory()`, additive)
- VSP.UI/ViewModels/SettingsViewModel.cs, VSP.UI/Views/SettingsView.xaml/.xaml.cs, VSP.UI/ViewModels/MainWindowViewModel.cs
- VSP.Tests/Infrastructure/DatabaseBackupServiceTests.cs, DatabaseRestoreServiceTests.cs (new)
- Directory.Build.props (pre-existing XML-comment fix; `<Version>` deliberately left at 0.18.0 — see file comment, versioning-scheme decision flagged to Product Owner rather than made unilaterally)
- Docs/CHANGELOG.md, Docs/03_PRODUCT_ROADMAP.md, Docs/PRODUCT_CAPABILITY_MATRIX.md, Docs/SPECS/EPIC-017_DATABASE_BACKUP_RESTORE_FOUNDATION.md

---

## 2026-08-06 (Epic-018)

### Version 0.18.0 - Epic-018 User / Role Management Foundation

Status:
Implementation Complete — Product Owner Accepted — **Frozen** (uncommitted — pending user commit)

Summary:
- Objective: a minimal Admin/Operator authentication and permission gate — a Login screen in front of the existing `MainWindow`, a `User` table (hashed password, role, forced-change flag), and role-based visibility/enablement of the navigation and commands already shipped by Epic-014 through Epic-017. Exactly two roles (Admin, Operator), local username/password only — no LDAP/AD/OAuth/JWT/MFA/SSO, no generic permission engine, no User Management UI.
- `VSP.Core/Security/PasswordHasher.cs`: PBKDF2-HMACSHA256 via the .NET BCL (`Rfc2898DeriveBytes.Pbkdf2`), 210,000 iterations, 16-byte per-user random salt, zero new external package. `VSP.Infrastructure/SQLite/UserTable.cs` + `DefaultAdminSeeder`: one additive table, one seeded row (`admin`, `MustChangePassword = 1`) — no default Operator row (Decision 3).
- `LoginViewModel`/`LoginWindow` gate `MainWindow` construction entirely — `App.xaml.cs`'s `OnStartup` never constructs `MainWindow` until a successful login (and, for the seeded Admin's first login, a completed mandatory `ForcedPasswordChangeWindow`, Decision 5) — both are non-dismissable blocking gates, not merely hidden windows. Identical generic rejection message for wrong username/wrong password (no username-enumeration signal); both success and failure are logged (Decision 6), with no password/hash/salt ever logged.
- Permission enforcement is two plain, inline boolean checks — no `IPermissionService`, no attribute-based authorization: (a) nav-item-level, `MainWindowViewModel`'s Settings item added only for Admin; (b) command-level `CanExecute` gating on `CameraListViewModel` (Add/Import/Batch Edit/Batch Connection Test/Export/Discovery), `CameraDetailViewModel` (Edit/Save/Delete — Operator gets read-only Camera Detail, Decision 4), and `LiveViewViewModel` (Start/Stop Recording, Decision 1). `SessionService` (Milestone 18D) is the single owner of `CurrentUser` for the lifetime of `MainWindow`; Logout requires confirmation and reconstructs a brand-new `MainWindowViewModel`/`SessionService`/`LiveViewViewModel` object graph, discarding all previous session state.
- Manual Validation (2026-08-05/06) against the actual built `VSP.UI.exe` and the real `%LocalAppData%\VSP\vsp.db` (migrated in place, pre-Epic-018 backup taken first) — **12/12 Pass**. One item (Operator Recording restriction) was initially reported failing; investigated end-to-end before touching any production file, found to be a stale-executable false positive (the tester's first pass ran against a non-current build), confirmed passing on re-test against the documented `VSP.UI\bin\Release\net10.0-windows\VSP.UI.exe` path after a clean (`dotnet clean` + from-scratch build) rebuild. No production code was changed by this investigation. Full transcript in `Docs/SPECS/EPIC-018_USER_ROLE_MANAGEMENT_FOUNDATION.md` §11-14.

Technical Debt / Known Limitations:
- No way to reach the Operator role through normal application use in v1.0 — no default Operator account is seeded and no account-creation UI exists (Decisions 2 & 3); requires direct database manipulation. Natural scope for a future User Management Epic.
- No self-service/discretionary Change Password — only the mandatory, login-triggered Forced Password Change screen exists (Decision 2 + 5 combined).
- No end-to-end `MainWindowViewModel`/View automated test — this codebase has no STA test infrastructure; covered by manual validation plus STA-free unit tests of every underlying gated command instead.
- No account lockout, no idle session timeout, no "remember me" — all explicitly out of scope for v1.0 by Product Owner direction.

Verification:
- New tests: `PasswordHasherTests`, `SQLiteUserRepositoryTests`, `LoginViewModelTests`, `ForcedPasswordChangeViewModelTests`, `SessionServiceTests`; extended `DatabaseInitializerTests`, `CameraListViewModelTests`, `CameraDetailViewModelTests`, `LiveViewViewModelTests` (Operator-role regression guards on every gated command).
- Full suite: 758/758, verified on a clean (`dotnet clean` + from-scratch `dotnet build -c Release`) rebuild — not an incremental build.
- Manual validation (2026-08-05/06) against the actual built `VSP.UI.exe`: 12/12 Pass. See `Docs/SPECS/EPIC-018_USER_ROLE_MANAGEMENT_FOUNDATION.md` §11 for the full script and results, §11.3 for the Step-9 stale-executable investigation, and §12-14 for the Product Acceptance Report, Final Validation Summary, and Known Limitations.

Files:
- VSP.Domain/Entities/User.cs, VSP.Domain/Enums/Role.cs (new)
- VSP.Core/Security/PasswordHasher.cs (new)
- VSP.Infrastructure/SQLite/UserTable.cs, VSP.Infrastructure/Database/DefaultAdminSeeder.cs (new)
- VSP.Infrastructure/Repositories/SQLiteUserRepository.cs (new)
- VSP.Device/Interfaces/IUserRepository.cs, VSP.Device/Repositories/UserRepository.cs (new)
- VSP.UI/ViewModels/LoginViewModel.cs, VSP.UI/Views/LoginWindow.xaml/.xaml.cs (new)
- VSP.UI/ViewModels/ForcedPasswordChangeViewModel.cs, VSP.UI/Views/ForcedPasswordChangeWindow.xaml/.xaml.cs (new)
- VSP.UI/Services/SessionService.cs (new)
- VSP.Infrastructure/Database/DatabaseInitializer.cs
- VSP.UI/App.xaml, VSP.UI/App.xaml.cs
- VSP.UI/Views/MainWindow.xaml, MainWindow.xaml.cs, VSP.UI/ViewModels/MainWindowViewModel.cs
- VSP.UI/ViewModels/CameraListViewModel.cs, VSP.UI/Views/CameraListView.xaml.cs
- VSP.UI/ViewModels/CameraDetailViewModel.cs, VSP.UI/Views/CameraDetailView.xaml/.xaml.cs
- VSP.UI/ViewModels/LiveViewViewModel.cs
- VSP.Tests/Infrastructure/Security/PasswordHasherTests.cs, VSP.Tests/Infrastructure/SQLiteUserRepositoryTests.cs, VSP.Tests/UI/LoginViewModelTests.cs, VSP.Tests/UI/ForcedPasswordChangeViewModelTests.cs, VSP.Tests/UI/SessionServiceTests.cs (new)
- VSP.Tests/UI/CameraListViewModelTests.cs, VSP.Tests/Camera/CameraDetailViewModelTests.cs, VSP.Tests/Player/LiveViewViewModelTests.cs, VSP.Tests/Infrastructure/DatabaseInitializerTests.cs
- Directory.Build.props
- Docs/CHANGELOG.md, Docs/03_PRODUCT_ROADMAP.md, Docs/PRODUCT_CAPABILITY_MATRIX.md, Docs/SPECS/EPIC-018_USER_ROLE_MANAGEMENT_FOUNDATION.md

---

## 2026-08-02 (Epic-016)

### Version 0.16.0 - Epic-016 Settings Foundation

Status:
Implementation Complete — Product Owner Accepted (uncommitted — pending user commit)

Summary:
- Objective: give VSP a working Settings screen persisting the four v1.0 fields — Recording Path, Retention Days, Language, Theme — reusing the config-file-backed seam Epic-011's `RecordingPathProvider` established, zero database schema change, zero new external package.
- Single source of truth: new `VSP.Core/Configuration/` (`SettingsFileContents`, `SettingsFileStore`, `RecordingRootDefaults`) is the one reader/writer for `recording-settings.json`, shared by both `VSP.Player.RecordingPathProvider` (internal implementation swapped only; public API and every observable behavior unchanged, verified by `RecordingPathProviderTests` passing unmodified) and the new `VSP.Infrastructure.Settings.AppSettingsProvider` (`Load()`/`Save(AppSettings)` only — a single immutable snapshot, no field-level getters, so `AppSettings` cannot drift into a per-field-access API).
- `VSP.UI.Validation.SettingsValidator` (static, `CameraValidator`-shaped): non-blank/syntactically-valid path, Retention Days bounds (1-3650, sharing `AppSettingsLimits` with `AppSettingsProvider` so the bound is defined once), write-access probe with unconditional cleanup that never blocks Save on a cleanup failure.
- `VSP.UI.Services.ThemeService`: the only place theme-selection logic lives — `System` resolves via one `HKCU` registry read at startup only (no live OS-theme-change reaction, TD-035), falls back to Dark and logs a Warning on any registry-read or resource-dictionary-swap failure, never throws.
- `SettingsViewModel`'s Save flow (exact sequence): validate Retention Days → if Recording Path changed, block while a recording is active (`LiveViewViewModel.IsRecording`, threaded through `MainWindowViewModel`) → validate path syntax → confirm-create if the folder doesn't exist → create the folder → write-access check → `AppSettingsProvider.Save()` → `ThemeService.Apply()` only if Theme changed → update the last-saved snapshot. Cancel discards in-progress edits with no confirmation (nothing has been applied yet). `isRecordingActive`/`confirmCreateFolder` are constructor-injected delegates from the composition root (`MainWindowViewModel`), keeping the multi-branch Save flow unit-testable without a live `Application` or a real `MessageBox`.
- Changing Recording Path takes effect immediately after Save, no restart required: `RecordingPathProvider` re-reads `recording-settings.json` from disk on every call rather than caching it, so the very next recording session picks up the new path.
- `App.xaml.cs` loads settings and calls `ThemeService.Apply()` once at startup, after `InitializeLogging()` and before database init.
- `VSP.UI.csproj` gained `<UseWindowsForms>true</UseWindowsForms>` for `FolderBrowserDialog`; the implicit `System.Windows.Forms`/`System.Drawing` global usings it introduces were removed via `<Using Remove>` (they collided with WPF's `UserControl`/`Application`/`TextBox`/`Control`/`Brush` across every existing View) rather than qualifying every pre-existing file.
- Manual Validation (2026-08-02) against the actual built `VSP.UI.exe` caught and corrected one real defect before acceptance: `SettingsView.xaml` originally used the same hardcoded hex colors as the placeholder it replaced, so `ThemeService.Apply` had no visible effect anywhere, including on Settings itself. Fixed to bind its own background/text to the new theme brushes via `DynamicResource`. All four Theme scenarios (System+Windows Light, System+Windows Dark, explicit Light overriding OS Dark, explicit Dark overriding OS Light) then verified by screenshot after a full process restart; Recording Path (Browse/Save/Restart/Persistence), Retention Days, and Language persistence each independently verified the same way, driven via Windows UI Automation. Full transcript in `Docs/SPECS/EPIC-016_SETTINGS_FOUNDATION.md` §13-14.

Technical Debt:
- TD-033: Theme Migration — the ~23 existing Views/Styles with hardcoded colors are not retrofitted to `DynamicResource` theming; only `SettingsView.xaml`'s own background/text respond to Theme today. Recorded, not implemented — Product Owner direction.
- TD-034: Language persists a real, stable selection (`en-US`/`zh-TW`) with zero translated resources behind it — a placeholder until a future Localization Epic.
- TD-035: `System` theme is resolved once at startup only; VSP does not react to the OS theme changing while running.
- TD-037: Settings UX improvements — unsaved-changes detection, a Restore Defaults action, and similar refinements are not present in this foundation pass. Recorded, not implemented — Product Owner direction.

Verification:
- New tests: `SettingsFileStoreTests`, `AppSettingsProviderTests`, `SettingsValidatorTests`, `ThemeServiceTests`, `SettingsViewModelTests`.
- Full suite: 674/674.
- Manual validation (2026-08-02) against the actual built `VSP.UI.exe`, driven via Windows UI Automation with real screenshots inspected, killing and relaunching the process for every restart check. See `Docs/SPECS/EPIC-016_SETTINGS_FOUNDATION.md` §14 for the full transcript, including the one real defect the validation pass caught and corrected before acceptance (§13).

Files:
- VSP.Core/Configuration/SettingsFileContents.cs, SettingsFileStore.cs, RecordingRootDefaults.cs (new)
- VSP.Infrastructure/Settings/AppTheme.cs, AppLanguage.cs, AppSettingsLimits.cs, AppSettings.cs, AppSettingsProvider.cs (new)
- VSP.Player/Recording/RecordingPathProvider.cs (internal implementation only; public API/behavior unchanged)
- VSP.UI/Themes/Dark.xaml, Light.xaml (new)
- VSP.UI/Services/ThemeService.cs (new)
- VSP.UI/Validation/SettingsValidator.cs (new)
- VSP.UI/ViewModels/SettingsViewModel.cs, VSP.UI/Views/SettingsView.xaml, SettingsView.xaml.cs
- VSP.UI/ViewModels/MainWindowViewModel.cs
- VSP.UI/App.xaml.cs
- VSP.UI/VSP.UI.csproj
- VSP.Tests/Core/SettingsFileStoreTests.cs, VSP.Tests/Infrastructure/AppSettingsProviderTests.cs, VSP.Tests/UI/SettingsValidatorTests.cs, VSP.Tests/UI/ThemeServiceTests.cs, VSP.Tests/UI/SettingsViewModelTests.cs (new)
- Directory.Build.props
- Docs/CHANGELOG.md, Docs/03_PRODUCT_ROADMAP.md, Docs/SPECS/EPIC-016_SETTINGS_FOUNDATION.md

---

## 2026-08-01 (Epic-015)

### Version 0.15.0 - Epic-015 Error Handling Foundation

Status:
Implementation Complete — Product Owner Accepted (uncommitted — pending user commit)

Summary:
- Objective (Product Owner, 2026-08-01): establish consistent exception handling only where exceptions currently disappear silently, using `AppLog` (Epic-014). Redirected from an initial "Feature Logging" proposal — normal business-event instrumentation (Camera Added, Recording Started, Playback Started, etc.) is explicitly deferred to a future Epic. Pattern: try → log → return the appropriate result, preserving all existing behavior.
- Six components, each verified by full-file read before changing: **Database initialization** (`DatabaseInitializer.Initialize()`, `VSP.Infrastructure`) had zero error handling — now returns a new minimal `DatabaseInitializationResult { Success, Exception }` (not a generic Result framework) instead of `void`/throwing unhandled. **Repository operations** (`SQLiteCameraRepository`'s four methods) had zero error handling — now log-and-rethrow, preserving `ICameraRepository`'s 25-call-site contract exactly. **RTSP**/**ONVIF** `TestConnection`/`GetDeviceInformation` already caught-and-returned but discarded the exception unbound — now bind and log it. **Retry failures** (`RetryingDiscoveryRunner`) — every non-final retry attempt was silently discarded; now logged before each retry, final-attempt propagation (uncaught here, by design) unchanged. **Media reconnect failures** (`MediaController.ConnectionLoopAsync`) — bare `catch` discarded the exception; now bound and logged per failed attempt.
- **Startup failure behavior (Database initialization)**: on failure, `VSP.UI/App.xaml.cs` generates a single Error ID, logs it together with the original exception in one `AppLog.Fatal` call (never split across two log lines — a correction applied after Product Owner review of the first implementation pass, so the same ID that appears in the dialog is always on the same log line as the actual exception/stack trace), shows a dialog naming the Error ID and the current log file's path, then `Environment.Exit(1)` — the app never proceeds to `MainWindow` without a working database.
- **Security review** (Product Owner instruction): no log call in this Epic includes a password, authorization header, token, or credential-bearing URL. Camera identification in logs uses `Id`/`IpAddress`/`HttpPort` only; RTSP/Live-View logging never includes `RtspUrl` (which may embed credentials); ONVIF logging never includes SOAP request/response bodies (which may carry WS-Security material). Verified by code review and asserted directly in several new tests (`DoesNotContain` the URL/credentials in logged messages).
- No new project reference required for the five call-site changes (`VSP.Device`, `VSP.Player` already referenced `VSP.Core`); `VSP.Infrastructure`'s reference was already added at Epic-014 acceptance.
- Implementation-discovered deviations from the approved file list (disclosed, not silent): `DatabaseService.cs` gained a minimal `internal` test-seam constructor (same convention as `RecordingPathProvider`/`FileLogger`) since neither `SQLiteCameraRepository` nor `DatabaseInitializer` had any prior test coverage and there was no way to force a failure deterministically otherwise; new `VSP.Infrastructure/AssemblyInfo.cs` (`InternalsVisibleTo`) to support it; new shared `VSP.Tests/Logging/RecordingLogger.cs` + `AppLogTestCollection.cs` (`DisableParallelization`) since six test classes across this Epic now mutate `AppLog`'s single process-wide static target.

Technical Debt:
- TD-030: Platform Lifecycle — future versions shall replace direct process termination (`Environment.Exit`) with a unified lifecycle manager. Complements TD-029 (Epic-014) — same underlying concern, now three `Environment.Exit` call sites total (two from Epic-014, one from this Epic). Recorded only, not implemented — Product Owner direction.

Verification:
- New/extended tests: `SQLiteCameraRepositoryTests` (new, 5 cases — round-trip Add/Update/Delete plus two log-and-rethrow failure cases), `DatabaseInitializerTests` (new, 3 cases — success, failure result shape, and confirms `Initialize()` does not log itself), plus one new case each in `RtspCameraDriverTests`, `OnvifCameraDriverTests` (x2), `RetryingDiscoveryRunnerTests`, and `MediaControllerReconnectTests` asserting the new logging occurs at the right level with the exception present, and that no credential material leaks into the message.
- Full suite: 632/633 in Debug (baseline 620 + 13 new); the one failure is the pre-existing `RtspMediaSessionIntegrationTests` timing flake (documented since Epic-011/012), confirmed passing 1/1 in isolation — not a regression, Product-Owner-accepted as such.
- Manual validation (2026-08-01) of the startup failure path against the actual built `VSP.UI.exe`: the real `vsp.db` was renamed aside, replaced with a same-named directory to force a genuine `SqliteException`, the app was launched and driven via Windows UI Automation, and the real file was restored afterward (confirmed identical size, 12,288 bytes, before and after — no data lost). Confirmed: Fatal log written with the original exception; a single Error ID generated and present on that same log line; the same ID shown in the dialog; the dialog also shows the correct current log file path; the process exits with code 1 after the dialog is dismissed; `MainWindow` is never created. Full transcript in `Docs/SPECS/EPIC-015_ERROR_HANDLING_FOUNDATION.md` §12.

Files:
- VSP.Infrastructure/AssemblyInfo.cs (new)
- VSP.Infrastructure/Database/DatabaseInitializationResult.cs (new)
- VSP.Infrastructure/Database/DatabaseInitializer.cs, DatabaseService.cs
- VSP.Infrastructure/Repositories/SQLiteCameraRepository.cs
- VSP.Device/Drivers/RTSP/RtspCameraDriver.cs, VSP.Device/Drivers/ONVIF/OnvifCameraDriver.cs
- VSP.Device/Discovery/Execution/RetryingDiscoveryRunner.cs
- VSP.Player/Control/MediaController.cs
- VSP.UI/App.xaml.cs
- VSP.Tests/Infrastructure/SQLiteCameraRepositoryTests.cs, DatabaseInitializerTests.cs (new)
- VSP.Tests/Logging/RecordingLogger.cs, AppLogTestCollection.cs (new)
- VSP.Tests/Drivers/RTSP/RtspCameraDriverTests.cs, VSP.Tests/Drivers/ONVIF/OnvifCameraDriverTests.cs, VSP.Tests/Discovery/RetryingDiscoveryRunnerTests.cs, VSP.Tests/Player/MediaControllerReconnectTests.cs
- Directory.Build.props
- Docs/CHANGELOG.md, Docs/03_PRODUCT_ROADMAP.md, Docs/SPECS/EPIC-015_ERROR_HANDLING_FOUNDATION.md

---

## 2026-08-01

### Version 0.14.0 - Epic-014 Logging Foundation

Status:
Implementation Complete — Product Owner Accepted (uncommitted — pending user commit)

Summary:
- Objective (Product Owner, 2026-08-01): give VSP a minimal, in-process logging mechanism so an otherwise-silent crash is captured to disk, and any future Epic has a `Log` call available — no external logging framework, no telemetry, no cloud, no database logging. Selected ahead of Settings because "every remaining Epic will benefit from having logging" and customer support depends on it more.
- Added `VSP.Core/Logging/`: `LogLevel` (`Debug`/`Info`/`Warning`/`Error`/`Fatal`), `ILogger`, `FileLogger` (the one production implementation — fixed-format lines, `YYYY-MM-DD.log` daily rolling file, 30-day retention purge, explicit `FileStream`/`Flush(flushToDisk: true)` per write so a crash log is never left in an OS write buffer), and `AppLog` (a static gateway, matching the codebase's existing no-DI-container, hand-wired convention already used by `RecordingPathProvider`/`DatabaseService`; defaults to a no-op logger until `Initialize` is called). Lives in `VSP.Core/Logging/` — a folder already scaffolded, empty, in `VSP.Core.csproj` since before this Epic.
- `VSP.Infrastructure` gained a `ProjectReference` to `VSP.Core` (Product Owner-approved: "logging is a platform capability, not a UI capability") — no log call was added inside `VSP.Infrastructure` itself in this Epic; only the reference exists so a future Epic can log from there without a further architecture change.
- `VSP.UI/App.xaml.cs` wires three global unhandled-exception handlers in `OnStartup`, split by the Product Owner's approved two-category behavior: `DispatcherUnhandledException` (UI thread) is **recoverable** — log with a generated Error ID, show a message box naming the Error ID and the current log file's path (guidance to send both to support), `e.Handled = true`, application continues; `AppDomain.CurrentDomain.UnhandledException` (non-UI thread) is **fatal** — log with an Error ID, then a deliberate `Environment.Exit(1)`; `TaskScheduler.UnobservedTaskException` is logged (with an Error ID) and marked observed. A 30-day log retention purge runs once at startup.
- Explicitly out of scope, per Product Owner instruction: Serilog, NLog, log4net, `Microsoft.Extensions.Logging`, ETW, OpenTelemetry, Elastic, database logging, network logging, cloud logging, telemetry. No call site in any existing feature (Camera Management, Discovery, Live View, Recording, Playback, Dashboard) was instrumented with a log call — this Epic delivers the mechanism only; feature-level logging begins with Epic-015.
- `Directory.Build.props` bumped to `0.14.0`, continuing the per-Epic version convention established in Epic-013.

Technical Debt:
- TD-029: the fatal-path shutdown uses `Environment.Exit(1)` directly. Acceptable for v1.0 Logging Foundation; a future Platform Lifecycle Epic should provide a unified graceful-shutdown strategy across UI, Services, Plugins, and future distributed components. Recorded only, not implemented — Product Owner direction.

Verification:
- New tests: `FileLoggerTests` (9 cases — fixed-format line content, exception detail append, same-day append, day-boundary rollover via an injectable clock seam, retention-purge boundary and count, concurrent-write safety under 8 threads x 50 lines) and `AppLogTests` (2 cases — delegation of all five levels including exception, and reconfiguring the target logger via `Initialize`).
- Full suite: 620/620 passing in Debug (previous baseline 611 + 9 new), zero new warnings, build green. One intermediate run showed the pre-existing `RtspMediaSessionIntegrationTests` timing flake (documented since Epic-011/012); confirmed passing both in isolation and on the final full run — not a regression.
- Manual validation (2026-08-01, against the actual built `VSP.UI.exe`, via Windows UI Automation + process/log inspection, not code reading): UI-thread exception showed the dialog with a real Error ID and log file path, was dismissed via its OK button, and the app kept running afterward; background-thread exception exited the process with code 1 and left a matching `FATAL` log entry (confirms flush-before-exit held under a real exit); unobserved Task exception logged a matching `ERROR` entry and the process stayed responsive. Full detail in `Docs/SPECS/EPIC-014_LOGGING_FOUNDATION.md` §8.

Files:
- VSP.Core/AssemblyInfo.cs (new)
- VSP.Core/Logging/LogLevel.cs, ILogger.cs, FileLogger.cs, AppLog.cs (new)
- VSP.Tests/Logging/FileLoggerTests.cs, AppLogTests.cs (new)
- VSP.Infrastructure/VSP.Infrastructure.csproj
- VSP.UI/App.xaml.cs
- Directory.Build.props
- Docs/CHANGELOG.md, Docs/03_PRODUCT_ROADMAP.md, Docs/SPECS/EPIC-014_LOGGING_FOUNDATION.md, Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md

---

## 2026-07-31

### Version 0.13.0 - Epic-013 Deployment Foundation

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- Objective (Product Owner, 2026-07-31): a clean Windows machine can Publish -> Install (xcopy, not a wizard) -> Launch -> Use VSP. Explicitly out of scope: installer technology evaluation, auto-update, code signing, branding/icons, CI/CD, single-file publish, ReadyToRun/AOT, any new product capability.
- Added `Directory.Build.props` at the repo root: single `<Version>0.13.0</Version>` shared by all 8 projects, replacing the previous accidental, disconnected `1.0.0.0` SDK default on every project. This is the first Epic where the CHANGELOG version header and the actual shipped assembly version are the same number. Per Product Owner direction, VSP becomes `1.0.0` only when all V1.0 GA requirements are complete and GA is formally approved -- `0.13.0` here reflects Epic-013, not a GA claim.
- Fixed `DatabaseService` (`VSP.Infrastructure`): `vsp.db` was hardcoded to `AppContext.BaseDirectory` (next to the executable), which fails to create/open under a standard, non-admin install location (e.g. `Program Files`). Moved to `%LocalAppData%\VSP\vsp.db`, creating the directory on first connection -- mirrors the pattern `RecordingPathProvider` already used correctly for recordings.
- Added `VSP.UI/Properties/PublishProfiles/win-x64.pubxml`: `RuntimeIdentifier=win-x64`, `SelfContained=true`, no single-file/ReadyToRun/AOT. This is the one supported, repeatable publish process (`dotnet publish VSP.UI\VSP.UI.csproj -c Release -p:PublishProfile=win-x64`) -- the .NET/WPF runtime is bundled, so a clean machine needs nothing preinstalled. Debug/dev inner-loop builds are unaffected (the profile only applies at publish time). Added a narrow `.gitignore` exception (`!VSP.UI/Properties/PublishProfiles/win-x64.pubxml`) since the repo's default `*.pubxml` rule would otherwise silently exclude this intentionally-checked-in profile (it carries no secrets, unlike the web-deploy profiles that rule was written for).
- Fixed a packaging defect found during verification: `DevEnvy.FFmpeg.Binaries.LGPL`'s own copy targets (`CopyFFmpegBinaries` / `PublishFFmpegBinaries`) unconditionally copy every RID under `ffmpeg\` (win-x64, linux-x64, linux-arm64, linux-musl-x64, osx-x64, osx-arm64) into every build, even though VSP.UI is win-x64-only. Added two small `Target`s in `VSP.UI.csproj` (must live in the published project itself, not `VSP.Player` -- a project-local `Target` does not run for a project that merely references it, only NuGet's `buildTransitive` targets propagate that way) that remove the non-Windows subfolders right after the vendor's own targets run. Measured: ffmpeg payload dropped from 411 MB to 33 MB (win-x64 only) in both plain `Build` and `Publish` output.

Verification:
- Published via the new profile, confirmed self-contained (`coreclr.dll`/`hostfxr.dll`/`PresentationFramework.dll` bundled, `runtimeconfig.json` shows `includedFrameworks`), `ffmpeg\` contains win-x64 only, SQLite native asset is win-x64 only, total publish size 197 MB (down from 574 MB pre-trim).
- Copied the published folder to an unrelated arbitrary path (simulating an xcopy install into e.g. `Program Files`) and launched it there directly -- confirmed `vsp.db` was created fresh under `%LocalAppData%\VSP\vsp.db`, not next to the exe.
- Launched the installed copy with `PATH` stripped to bare `System32`/`Windows` (zero `dotnet.exe`, zero MinGW runtime DLLs reachable) -- app started and stayed responsive, proving no .NET runtime prerequisite.
- Real functional smoke test against that same installed, PATH-stripped instance (not a mock): added a camera via the running app's own SQLite database, selected it in Live View, and confirmed a genuine `Live: <camera>` connection via the bundled, win-x64-only FFmpeg binaries -- both SQLite and FFmpeg native dependencies confirmed present and functional post-trim.
- Full suite: 611/611 passing in Release; 610/611 in Debug with the one failure being the same pre-existing `RtspMediaSessionIntegrationTests` timing flake documented since Epic-011/012 (confirmed passes 1/1 in isolation; not modified, unrelated to this Epic's changes). Debug and Release builds both green, zero new warnings beyond the pre-existing baseline.

Final pre-commit deployment validation (2026-07-31, Product Owner-requested):
- Requested as a clean Windows VM/equivalent isolated environment; no Hyper-V, Windows Sandbox, or WSL is available in this environment (session not elevated, neither feature installed), so this was explicitly run as a **proxy validation on the development machine**, not an independently provisioned clean Windows VM -- documented here as such, not represented as VM-certified.
- Full sequence performed for real: deleted any previous deployment folder and `%LocalAppData%\VSP`; published fresh via the approved `win-x64` profile; copied the published folder to a completely unrelated path; launched from there with `PATH` stripped of `dotnet`/MinGW. Verified in order: application starts; `vsp.db` is recreated fresh under `%LocalAppData%\VSP`; a camera is added through the real Add Camera dialog (not a DB shortcut) and persists via the real repository; Live View reaches a genuine `Live: <camera>` state; Recording produces a valid MP4 (verified with the deployed instance's own `ffprobe.exe`); Playback opens that recording and Play/Pause/Resume/Seek/Stop all work against real decode. Closed the application, relaunched it, and confirmed the camera and its recording were both still present and selectable -- persistence across restart holds.
- One anomaly investigated and not attributed to any code path: during UI-automation testing, the Add Camera dialog intermittently disappeared and, in one instance, the whole process exited (clean `ExitCode=0`, not a crash -- no exception, no stack trace, nothing in `CameraDetailViewModel`/`CameraDetailWindow` involves a timer or delayed close). `query user` showed this machine's real interactive session as `Active` with zero idle time throughout testing, i.e. genuinely concurrent real-user activity on a shared desktop already running many unrelated applications -- the most likely explanation is input/focus interference from that concurrent use, not a reproducible product defect. Per instruction, no production code was changed on the basis of this unattributed, non-reproducible observation; a dedicated isolated environment would be needed to fully rule it out.

Explicitly out of scope (per Product Owner direction, not gaps to silently fix):
- No installer/wizard (MSI, MSIX, Inno Setup) -- "the goal is deployment, not distribution."
- No auto-update, code signing, branding/icons, or CI/CD.
- No single-file publish, ReadyToRun, or AOT.
- Upgrade strategy / uninstall semantics beyond xcopy replace-in-place are not addressed -- deferred to whenever installer technology is revisited.

Files:
- Directory.Build.props (new)
- VSP.Infrastructure/Database/DatabaseService.cs
- VSP.UI/Properties/PublishProfiles/win-x64.pubxml (new)
- VSP.UI/VSP.UI.csproj
- .gitignore
- Docs/CHANGELOG.md, Docs/03_PRODUCT_ROADMAP.md

---

## 2026-07-29

### Version 1.16 - Epic-012 Playback Foundation

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- Closes ADR-002's v3 Playback evolution row (file-backed `IMediaSession`, `IMediaClock.Seek` becomes meaningful) per the approved Epic-012 scope: Camera selection, recording list, Play, Pause, Stop, Seek only -- no Timeline, Calendar, Search, Bookmark, Snapshot, Export, Smart Search, variable playback rate, or multi-camera playback.
- Added `RecordedFileMediaSession` (`VSP.Player.Decoder`), a second, small `IMediaSession` implementation (`Kind=RecordedFile`) alongside the untouched `RtspMediaSession` -- mirrors its open/read-loop/dispose shape and native-call locking pattern (`avformat_open_input` is protocol-agnostic) but differs where a file genuinely needs to: a real-time-paced read loop (local-delta pacing between consecutive packets' PTS, chunked in <=200ms waits so cancellation/seek stay responsive), a pause gate so Pause actually stops the file position from advancing (not just the renderer, unlike Live), a natural EOF-to-Closed transition instead of Faulted, and seek via `av_seek_frame`.
- Closed accepted Epic-010 technical debt on schedule: `FfmpegVideoDecoder`'s constructor took the concrete `RtspMediaSession`, explicitly flagged as blocking Playback reuse. Added an internal `IFfmpegDemuxSource` seam (both sessions implement it via explicit interface implementation, keeping their existing internal `GetVideoCodecParameters`/`GetVideoStreamTimeBase` methods untouched) and changed the decoder's constructor to depend on that instead -- its accessibility became internal in the process, since its parameter type is internal (no FFmpeg-adjacent type may appear in a public signature, per ADR-002/ADR-003's isolation guarantee).
- Added `PlaybackClock` (`VSP.Player.Pipeline`), a second `IMediaClock` implementation whose `Seek` is real (unlike the existing `MediaClock`, hardcoded to return null for Live) -- delegates to a callback `PlaybackController` wires to the session's seek plus a decoder flush.
- Added `PlaybackController` (`VSP.Player.Control`), a second, small `IMediaController` implementation -- no reconnect loop (the wrong shape for "open a finished file, seek around in it, stop"), no encoded-tier recording dispatch. `IMediaController` itself gained exactly two new members (`Clock`, `Duration`) -- additive only, every existing member unchanged; `MediaController` (Live) implements both minimally (an unwired `MediaClock`; `Duration` always null) with zero behavior change to the already-shipped Live path.
- Recordings are now organized per camera (approved scope addition): `RecordingPathProvider.GetCameraRecordingDirectory(Guid)` and a new `MediaController(..., cameraId)` constructor parameter (optional, defaults to the old flat-root behavior for existing tests) route Epic-011's recording writer into `{RecordingRoot}\{cameraId:N}\...`. New public `RecordingCatalog.ListRecordings(Guid cameraId)` lists a camera's `*.mp4` files (filesystem enumeration only, timestamp parsed from the existing filename convention -- no database, no catalog).
- `PlaybackViewModel`/`PlaybackView` (previously empty placeholders, matching the pre-Epic-009 Dashboard shape) are now real: camera dropdown (`CameraQueryService`, reused from Dashboard), recording dropdown (`RecordingCatalog`), Play/Pause/Stop buttons, a seek slider (seeks on click-to-position/drag-release only, never on every position update, to avoid fighting the OneWay position binding), and a 500ms position-refresh timer. `MainWindowViewModel` needed no changes -- it already called the parameterless `new PlaybackView()`, now wired to real dependencies exactly like `DashboardView`'s own composition.

Real defect found and fixed during implementation:
- **`PlaybackController` swallowed an Open failure without ever reaching the Error state.** Unlike `MediaController`, `PlaybackController` has no reconnect loop to eventually reach Error after exhausting attempts -- its generic catch around a failed `OpenAsync` only cleaned up and returned, leaving the controller stuck at Connecting forever. Fixed by transitioning to Error immediately (using the `MediaError` already captured via the session's own Faulted `StateChanged` event) whenever Open fails for a reason other than cancellation. Caught by `PlaybackControllerTests.OpenAlwaysFails_ReachesError`.

Test-fidelity defects found and fixed during implementation:
- Two new `RecordedFileMediaSessionTests` initially asserted Duration/Seek against a raw `.mjpeg` elementary-stream test fixture (matching `RtspMediaSessionIntegrationTests`' existing convention) -- raw elementary streams carry no container-level duration/index metadata, so both came back null/failed, correctly, not a product defect. Fixed by switching the test source to an `.mp4` container (what a real Epic-011 recording actually is), which both capabilities need.
- `PauseReading_StopsPacketFlow_ResumeReadingContinues` initially raced: `PauseReading` only takes effect before the read loop's next iteration, so a packet already past that check could still land after Pause was requested. Fixed by giving that one (at most) in-flight packet time to arrive before the test snapshots the "stable" count.

Verification:
- Full suite: 610/611 passing (582 pre-existing + 28 new/modified), Debug and Release. The one failure under full-suite load is the same pre-existing `RtspMediaSessionIntegrationTests` flake already documented in Epic-011's changelog (confirmed passes 1/1 in isolation here too) -- not modified, out of this Epic's scope.
- New tests: `RecordedFileMediaSessionTests` (real, non-fake FFmpeg round trip -- decode, EOF-to-Closed, real-time pacing, Pause/Resume, Seek), `PlaybackControllerTests` (fake-session-based, mirroring `MediaControllerReconnectTests`), `PlaybackViewModelTests` (mirroring `LiveViewViewModelTests`), `RecordingCatalogTests`, and `RecordingPathProviderTests` additions for the per-camera directory.
- Deployment: build green in both Debug and Release, zero new warnings beyond the pre-existing baseline; `VSP.UI.exe` launches and stays running (non-interactive smoke test), consistent with how Epic-010/011 were verified.

Accepted technical debt (not addressed this Epic):
- Seeking is only as precise as `av_seek_frame`'s nearest-preceding-keyframe; no frame-accurate seek.
- Pacing sleeps are held under the same native lock a seek also needs, so a seek issued during an unusually large inter-packet gap (bounded at 2s) can be delayed up to that long in the worst case -- acceptable for Foundation scope given typical frame-rate gaps are tens of milliseconds; noted for a future Epic if it proves visible in practice.
- No frame-accurate position display beyond the 500ms UI refresh timer.
- Recordings made before this Epic (flat-root, no camera subfolder) are not migrated and will not appear in Playback's per-camera list -- Recording itself was still uncommitted/unreleased at the time of this Epic, so no real user data exists to migrate.
- Search, Bookmark, Export, Snapshot, Smart Search, Timeline/Calendar UI, variable playback rate, and multi-camera playback are all explicitly out of the approved Epic-012 scope.

Files:
- VSP.Player/Interfaces/IMediaController.cs (added Clock, Duration)
- VSP.Player/Control/MediaController.cs (Clock/Duration implementation, cameraId ctor parameter, per-camera `BuildRecordingFilePath`), PlaybackController.cs (new)
- VSP.Player/Decoder/IFfmpegDemuxSource.cs, IPlaybackControl.cs, RecordedFileMediaSession.cs (new); RtspMediaSession.cs (implements IFfmpegDemuxSource); FfmpegVideoDecoder.cs (constructor depends on IFfmpegDemuxSource, now internal)
- VSP.Player/Pipeline/PlaybackClock.cs (new)
- VSP.Player/Recording/RecordingPathProvider.cs (GetCameraRecordingDirectory), RecordingCatalog.cs (new)
- VSP.UI/ViewModels/PlaybackViewModel.cs (real implementation, RecordingItem), Views/PlaybackView.xaml, PlaybackView.xaml.cs
- VSP.Tests/Player/RecordedFileMediaSessionTests.cs, PlaybackControllerTests.cs, PlaybackViewModelTests.cs, RecordingCatalogTests.cs (new); RecordingPathProviderTests.cs (per-camera directory tests); LiveViewViewModelTests.cs (FakeMediaController implements Clock/Duration)
- Docs/CHANGELOG.md, Docs/03_PRODUCT_ROADMAP.md

Known limitations (out of approved scope for this Epic):
- No Timeline, Calendar, Search, Bookmark, Export, Snapshot, Smart Search, variable playback rate, or multi-camera playback -- all explicitly deferred per the approved Epic-012 scope.
- Not manually smoke-tested against a real camera/real recording in the running WPF UI -- verification here is build + full automated suite + real (non-fake) FFmpeg record/playback round trips + non-interactive app-launch smoke test, consistent with how Epic-010/011 were reported in this environment.

---

## 2026-07-28

### Version 1.15 - Epic-011 Recording Foundation

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- Implemented continuous, stream-copy recording on top of the existing ADR-002 media pipeline and the Epic-010 FFmpeg integration, per the approved Epic-011 scope: Continuous mode only, a minimal Start/Stop Recording control + status indicator on Live View, `IMediaController.StartRecordingAsync`/`StopRecordingAsync`/`IsRecording` as the only new integration surface, and a single config-file-backed `RecordingRoot` setting (default `%LocalAppData%\VSP\Recordings`, auto-created, no Settings page, no folder browsing, no storage management).
- Added the ADR-002 Recorder-mode contracts that had not yet been implemented: `IRecordingSession`, `IRecordingMode`, `RecordingModeContext` (`VSP.Player.Interfaces`/`Entities`), and `ContinuousRecordingMode` (`VSP.Player.Recording`) -- `ShouldRecord` always `true`, the only mode in this Epic.
- `MediaController`: added a second, encoded-tier `FrameDispatcher<EncodedFrame>` that every received packet is dispatched to independent of decode (recording must not depend on decode succeeding or a Renderer being attached, per ADR-002). `StartRecordingAsync`/`StopRecordingAsync`/`IsRecording` subscribe/unsubscribe a recording session against that dispatcher with `BlockProducerWhenFull`; recording is intentionally independent of Pause/Resume (which remain renderer-only) and is finalized (trailer written) on `StopAsync`/`Dispose` so no in-progress recording is ever abandoned.
- `FfmpegRecordingSession` (`VSP.Player.Recording`): real FFmpeg stream-copy muxer -- `avformat_alloc_output_context2` + `avformat_new_stream` + `avcodec_parameters_copy` + `avformat_write_header` + `av_interleaved_write_frame` + `av_write_trailer`. No decode, no encode call anywhere in this class. Takes the concrete `RtspMediaSession` (not `IMediaSession`), mirroring `FfmpegVideoDecoder`'s existing constructor pattern, purely to read codec parameters/time_base once at `StartAsync` via two small internal seams added to `RtspMediaSession` (`GetVideoCodecParameters` already existed for the decoder; `GetVideoStreamTimeBase` is new). Skips packets until the first keyframe so the output is playable from byte zero.
- `RecordingPathProvider` (`VSP.Player.Recording`): resolves `RecordingRoot` from a small JSON file (`%LocalAppData%\VSP\recording-settings.json`) if present, otherwise the fixed default, and creates the directory if missing. Deliberately the smallest possible seam -- no Settings UI, no schema change.
- `LiveViewViewModel`/`LiveView.xaml`: `StartRecordingCommand`/`StopRecordingCommand` (enabled based on `IMediaController.State`/`IsRecording`, mirroring the existing Pause/Resume pattern) and a small "REC" indicator next to the status text.

Real defect found and fixed during implementation:
- **FfmpegRecordingSession produced a valid-looking but permanently empty output file.** The output stream's `time_base` was never seeded before `avformat_write_header` (only read back afterward for rescaling), so every packet's rescaled pts/dts collapsed to non-monotonic/degenerate values that the MP4 muxer silently rejected -- confirmed via `ffprobe` on a real recorded file (valid container, zero readable packets) and root-caused against FFmpeg's own remuxing.c reference pattern, which explicitly seeds `out_stream->time_base` from the input stream before writing the header. Fixed by setting `outputStream->time_base = _sourceTimeBase` alongside the existing `codec_tag = 0` reset; verified via a real (non-fake) FFmpeg round-trip test that now passes.

Test-infrastructure defect found and fixed during implementation:
- **A new `LiveViewViewModelTests` case froze the entire test suite indefinitely** (reproduced consistently; confirmed via VSTest's `--blame-hang`/`Sequence.xml` diagnostic, which pinpointed the exact hung test after ~580 other tests had already passed). The test combined `await Task.Delay(50)` with `Dispatcher.PushFrame` -- since xUnit does not install a captured `SynchronizationContext`, the continuation after the delay resumed on an arbitrary thread-pool thread rather than the thread that owned the `Dispatcher`, and pumping a `Dispatcher`'s message loop from a thread other than its own thread hangs rather than throwing. Fixed by removing the artificial delay and keeping the test fully synchronous (`FakeMediaController`'s recording methods already complete synchronously), matching every other test in this file. This was purely a new-test defect, not a pre-existing or production issue -- confirmed by re-running the full suite clean (582/582, ~5s) immediately after the fix.

Known pre-existing flake (not modified -- out of Epic-011 scope):
- `RtspMediaSessionIntegrationTests.OpenAsync_AgainstRealFfmpegEncodedStream_ReceivesAndDecodesRealFrames` (Epic-010) occasionally times out under full-suite load (same class of "subscribe after Open" race as the one fixed in this Epic's own `RecordingIntegrationTests`) but passes reliably in isolation (verified 4/4) and in two clean full-suite runs. Flagged here for visibility; fixing it would mean editing an unrelated Epic-010 test file, outside this Epic's approved scope.

Verification:
- Full suite: 582/582 passing (567 pre-existing + 15 new), Debug and Release, ~5s each after the hang fix.
- New tests: `ContinuousRecordingModeTests`, `RecordingPathProviderTests` (default/configured/malformed/blank config), `MediaControllerRecordingTests` (encoded-tier dispatch, start/stop lifecycle, state guards, Pause-does-not-stop-recording, Stop-finalizes-recording), a `LiveViewViewModelTests` case for the new commands/indicator, and `RecordingIntegrationTests` -- a real (non-fake) FFmpeg round-trip: encodes a genuine source via the bundled `ffmpeg.exe`, records it via `FfmpegRecordingSession`, then re-opens and decodes the recorded file for real, asserting correct dimensions and non-empty pixel data as evidence of a valid stream-copy (no re-encode call exists in the writer at all).
- Deployment: build green in both Debug and Release; `VSP.UI.exe` launches and stays running (non-interactive smoke test), consistent with how Epic-010 was verified.

Accepted technical debt (not addressed this Epic):
- `av_interleaved_write_frame`'s return code is not checked -- a mux-level failure for a single packet is currently silent rather than surfaced.
- `StartRecordingAsync`'s check-then-act guard against concurrent calls has the same theoretical TOCTOU window as the pre-existing `PauseAsync`/`ResumeAsync`/`StartAsync` -- not a new risk category, consistent with the existing accepted pattern.
- Scheduled and Motion-triggered recording modes, a Recording Settings page, folder browsing, storage management/quotas/cleanup, and multiple recording roots are all explicitly out of scope per the approved Epic Definition.

Files:
- VSP.Player/Entities/MediaErrorCategory.cs (added `Recording`), RecordingModeContext.cs (new)
- VSP.Player/Interfaces/IRecordingMode.cs, IRecordingSession.cs (new); IMediaController.cs (added `IsRecording`/`StartRecordingAsync`/`StopRecordingAsync`)
- VSP.Player/Recording/ContinuousRecordingMode.cs, FfmpegRecordingSession.cs, RecordingPathProvider.cs (new)
- VSP.Player/Decoder/RtspMediaSession.cs (added `GetVideoStreamTimeBase` seam)
- VSP.Player/Control/MediaController.cs (encoded-tier dispatcher, recording lifecycle)
- VSP.UI/ViewModels/LiveViewViewModel.cs, VSP.UI/Views/LiveView.xaml (Start/Stop Recording + indicator)
- VSP.Tests/Player/ContinuousRecordingModeTests.cs, RecordingPathProviderTests.cs, MediaControllerRecordingTests.cs, RecordingIntegrationTests.cs (new); LiveViewViewModelTests.cs (recording command test + `IMediaController` fake updated)
- Docs/CHANGELOG.md

Known limitations (out of approved scope for this Epic):
- No Playback, Timeline, Export, Motion recording, AI, multi-camera synchronization, retention policy, cloud, or cluster support -- all deferred per ADR-002's Future Evolution table and the approved Epic Definition.
- Recording survives a mid-recording reconnect transparently (packets from a reconnected session continue into the same file, since the encoded-tier Dispatcher outlives any single `IMediaSession` instance) -- this was a natural consequence of the design, not a specifically tested scenario this Epic.
- Not manually smoke-tested against a real camera in the running WPF UI -- verification here is build + full automated suite + a real (non-fake) FFmpeg record/remux round-trip + non-interactive app-launch smoke test, consistent with how Epic-010 was reported in this environment.

---

## 2026-07-26

### Version 1.14 - Epic-010 Live View Foundation

Status:
**Complete — Epic Close-Out Accepted** (uncommitted — pending user commit)

FFmpeg Decision (ADR-003):
Product Owner selected FFmpeg as the media library, evaluated against the ADR-002 target architecture. Status updated to Accepted; recorded as "Implemented by: Epic-010." Binding package is `FFmpeg.AutoGen.Bindings.DynamicallyLinked` (compile-time `DllImport`), not `FFmpeg.AutoGen.Bindings.DynamicallyLoaded` as first assumed — see the runtime-defect note below and Docs/DECISIONS/ADR-003_MEDIA_LIBRARY_SELECTION.md for the updated record.

Summary:
- Implemented the ADR-002 media pipeline as the first real `VSP.Player` implementation: `IMediaSession`/`IMediaController`/`IMediaClock`/`IFrameDispatcher`/`IFrameBuffer`/`IDispatcherMetrics`/`IFrameRenderer`, plus the three closing abstractions from ADR-003 — a neutral `DecodedFrame`/`EncodedFrame` payload (no FFmpeg type crosses out of `VSP.Player.Decoder`), a hardware-frame-capable shape (`FrameStorage.Cpu`/`Gpu`, `IGpuFrameHandle`, unused by the v1 software decoder but present so a future GPU decoder doesn't require a contract change), and a normalized `MediaError` type (FFmpeg codes/strings translated at the boundary, never surfaced raw).
- FFmpeg adopted per ADR-003 via `FFmpeg.AutoGen` + `DevEnvy.FFmpeg.Binaries.LGPL` (real LGPL-licensed native binaries, `--disable-gpl --disable-nonfree`, auto-copied to output on Build/Publish). **Discovered and fixed a real runtime defect during implementation:** `FFmpeg.AutoGen.Bindings.DynamicallyLoaded`'s runtime `Marshal.GetDelegateForFunctionPointer`-based resolution throws `NotSupportedException` for every single function call (reproduced on both net8.0 and net10.0, independent of DLL search path and library version alignment — root-caused via the package's own upstream source). Switched to `FFmpeg.AutoGen.Bindings.DynamicallyLinked` (compile-time `DllImport` against the bundled DLLs' exact names, e.g. `avutil-60`), which works correctly; verified via an isolated repro before adopting it in `VSP.Player`.
- `RtspMediaSession` (FFmpeg-backed `IMediaSession`): real `avformat_open_input`/`avformat_find_stream_info`/`av_read_frame`, forced `rtsp_transport=tcp`, a native `AVIOInterruptCB` wired to a cancellation flag so `Stop`/`Dispose` can interrupt a blocked read from another thread (not just set-and-hope).
- `FfmpegVideoDecoder` (FFmpeg-backed `IVideoDecoder`): real `avcodec_send_packet`/`avcodec_receive_frame` + `sws_scale` conversion to BGRA32 (matching WPF's native pixel layout).
- `MediaController`: composes session/decoder/dispatcher/renderer against the neutral interfaces (not the concrete FFmpeg types) via injectable factories — the reconnect state machine is unit-tested against a fake `IMediaSession` without a real network dependency. Bounded reconnect, pause/resume (renderer stops/starts without tearing down the connection), explicit stop (no further reconnect), `MediaSessionStatistics` (connected duration, reconnect attempts, last error).
- `WpfFrameRenderer`: `WriteableBitmap.WritePixels` updates in place — a bound `Image` repaints automatically without a property-changed notification per frame; drops a frame rather than growing the UI dispatcher queue unbounded under UI lag.
- **Camera selection now originates from the Camera Workspace, not internal Live View discovery**, per Product Owner direction: `LiveViewCameraCoordinator` mediates a "View Live" command on `CameraListViewModel` to `MainWindowViewModel` (composition root), which switches nav to Live View and loads the camera — `CameraListViewModel`/`LiveViewViewModel` stay decoupled from each other.
- Live View reuses the existing `Camera.RtspUrl` field (Epic-007/008) — no new persistence, no schema change.

Performance Baseline (1920×1080, synthetic test source, real FFmpeg decode/render/reconnect, measured against the actual production code via a standalone harness):
- Decode+convert per frame: avg 2.84 ms, P95 3.30 ms, max 6.05 ms — well inside the 33.3 ms/frame budget for 30 fps.
- Render per frame (real `WpfFrameRenderer` on a real pumped Dispatcher): P95 0.98 ms steady-state; one-time first-frame outlier (~152 ms, `WriteableBitmap` allocation/JIT warmup) pulls the average up — see Docs/PERFORMANCE_BASELINE.md for the full breakdown.
- Reconnect: real `MediaController` against a real (finite, EOF-triggered) fault — 3017 ms fault-to-reconnected against a configured 3000 ms `reconnectDelay`, i.e. ~17 ms of actual reopen overhead beyond the intentional backoff.
- Unthrottled throughput: 274–339 fps across runs (local file, not rate-limited like live RTSP) — confirms comfortable headroom above the 30 fps target.
- CPU: ~100–127% of one logical core during decode (this harness process only, excluding the separate ffmpeg.exe encoder process used to generate the test source).
- Memory: ~24–25 MB working set, no measurable growth over 150 frames.
- Caveat, stated plainly: measured against MJPEG (available without GPL codecs), not H.264 (the real-world camera case); reconnect measured against local file reopen, not a real network/camera RTSP re-handshake. Full methodology, environment, and numbers are recorded in Docs/PERFORMANCE_BASELINE.md, which is now the baseline for all future Video Epics.

Deployment Verification:
- Full solution build green in both Debug and Release; native FFmpeg binaries (`ffmpeg/win-x64/*.dll` + `ffmpeg.exe`/`ffprobe.exe`) confirmed present in both output folders.
- `VSP.UI.exe` launches and stays running (no startup crash) in both Debug and Release, confirming the new Live View composition-root wiring doesn't break app startup.

Media Verification checklist (Open/Play/Pause/Resume/Stop/Reconnect/Dispose) — all verified by automated tests, no manual step skipped:
- Open/Play: `MediaControllerReconnectTests.StartAsync_SuccessfulOpen_ReachesConnectedAndRecordsStatistics`, `PacketReceived_DecodesAndDispatchesFrames`; real (non-fake) decode end-to-end in `RtspMediaSessionIntegrationTests`.
- Pause/Resume: `PauseAndResume_ToggleStateAndRejectInvalidTransitions`.
- Stop: `StopAsync_StopsWithoutFurtherReconnectAttempts`.
- Reconnect: `SessionFault_ReconnectsAndReturnsToConnected`, `OpenAlwaysFails_ExceedsMaxAttemptsAndReachesError`.
- Dispose: `Dispose_StopsLoopAndRejectsFurtherUse`.
- Full suite: 567/567 passing (547 pre-existing + 20 new). Build passing (Debug and Release).

Architecture Review Summary:
A dedicated Architecture Review (SOLID/SRP/God Objects, layer violations, FFmpeg abstraction leaks, memory hotspots, thread safety, lock contention, cancellation correctness, dispose/resource lifetime, native resource management, reconnect state machine correctness, event subscription leaks, performance bottlenecks, test coverage gaps, deployment risks) was performed against ADR-002, ADR-003, and the Epic-010 Definition of Done. 14 findings were raised (1 Critical, 2 High, 5 Medium, 6 Low). Per Product Owner direction, 5 were fixed and re-verified (full suite green, no regressions):
- **Fixed (Critical):** `Dispose()` on `FfmpegVideoDecoder`/`RtspMediaSession` could free native pointers concurrently with an in-flight `Decode()`/read-loop call. Fixed with a native-call gate (`_nativeGate` lock) around every native-touching method in both classes.
- **Fixed (High):** `OpenAsync`'s `CancellationToken` had no way to interrupt the blocking native open call — only `CloseAsync`/`Dispose` could set `_interruptRequested`, which can't run until the open call already returns (a chicken-and-egg hang risk for `Stop`/`Dispose` during a slow reconnect attempt). Fixed by registering the token to set `_interruptRequested` directly, wiring external cancellation into the already-present `AVIOInterruptCB`.
- **Fixed (High):** `MediaController._controllerCts` was never disposed across repeated Start→Stop→Start cycles — a `CancellationTokenSource` leak per restart. Fixed by disposing the previous instance before replacing it.
- **Fixed (Medium):** `_sessionEndedTcs` was a single shared field reused across reconnect iterations — a late event from an old, just-cleaned-up session could complete the wrong iteration's completion source. Fixed by capturing it as a per-iteration local closed over by that iteration's own event handler.
- **Fixed (Medium):** `MediaController._session`/`_decoder` were read/written across threads without synchronization, risking dropped first-frame(s) after (re)connect. Fixed by marking both fields `volatile`.
- **Accepted as technical debt (not addressed this Epic):** see below.

Accepted Technical Debt (explicitly deferred, not addressed in Epic-010):
- **MediaController decomposition** — the class combines the reconnect state machine, statistics aggregation, and session/decoder/dispatcher/renderer lifecycle orchestration (~400 lines, SRP concern, not yet a God Object). Future video Epics (Recording, Playback) will add further composition here.
- **Decoder abstraction redesign** — `FfmpegVideoDecoder`'s only constructor takes the concrete `RtspMediaSession`, not `IMediaSession`; a future file-based session (Playback) can't reuse it without a source change.
- **Buffer pooling** — `RtspMediaSession`/`FfmpegVideoDecoder` allocate a new `byte[]` per packet and per decoded frame (≈8.3 MB for 1080p BGRA32) with no pooling (e.g. `ArrayPool<byte>`).
- **Busy-poll optimization** — `FrameDispatcher<T>.Subscription.Pump()` polls via `Thread.Sleep(2)` rather than an event-driven wait; one dedicated thread per subscriber.
- **`AddDllDirectory` migration** — `FfmpegNativeLibraryLoader` uses `SetDllDirectory` (process-global, replaces rather than appends) instead of the additive `AddDllDirectory`.
- **Error message normalization** — `MediaError.Message` embeds the raw native `av_strerror` text/code; normalized at the type level, not fully at the content level.
- **Minor lock optimization** — `MediaController.SetState` acquires its lock twice in immediate succession.

These are recorded here as the authoritative deferred-debt list for this Epic; none block Epic-010 completion, and any future Epic that would be affected by one of these items should check this list first.

Files:
- Docs/DECISIONS/ADR-002_MEDIA_PIPELINE_ARCHITECTURE.md
- Docs/DECISIONS/ADR-003_MEDIA_LIBRARY_SELECTION.md
- VSP.Player/VSP.Player.csproj
- VSP.Player/AssemblyInfo.cs
- VSP.Player/Entities/*.cs (VideoSourceKind, MediaSessionState, MediaControllerState, BufferPolicy, FramePixelFormat, FrameStorage, MediaErrorCategory, FrameTimestamp, MediaError, IGpuFrameHandle, EncodedFrame, DecodedFrame, MediaSessionStatistics, MediaSessionStateChangedEventArgs, MediaControllerStateChangedEventArgs, EncodedPacketReceivedEventArgs, FrameDroppedEventArgs)
- VSP.Player/Interfaces/*.cs (IMediaSession, IVideoDecoder, IMediaClock, IFrameConsumer, IStreamingFrameConsumer, IFrameBuffer, IDispatcherMetrics, IFrameDispatcher, IFrameRenderer, IMediaController)
- VSP.Player/Pipeline/*.cs (FrameBuffer, DispatcherMetrics, FrameDispatcher, MediaClock)
- VSP.Player/Decoder/*.cs (FfmpegNativeLibraryLoader, FfmpegErrorTranslator, MediaSessionOpenException, RtspMediaSession, FfmpegVideoDecoder)
- VSP.Player/Renderer/WpfFrameRenderer.cs
- VSP.Player/Control/MediaController.cs
- VSP.UI/Services/LiveViewCameraCoordinator.cs
- VSP.UI/ViewModels/CameraListViewModel.cs
- VSP.UI/ViewModels/LiveViewViewModel.cs
- VSP.UI/ViewModels/MainWindowViewModel.cs
- VSP.UI/Views/CameraListView.xaml, CameraListView.xaml.cs
- VSP.UI/Views/LiveView.xaml, LiveView.xaml.cs
- VSP.UI/Helpers/InverseBooleanToVisibilityConverter.cs
- VSP.UI/AssemblyInfo.cs (InternalsVisibleTo VSP.Tests, needed for the fake-based reconnect/ViewModel tests)
- VSP.Tests/Player/*.cs (FrameBufferTests, FrameDispatcherTests, MediaControllerReconnectTests, LiveViewViewModelTests, RtspMediaSessionIntegrationTests)
- Docs/PERFORMANCE_BASELINE.md
- Docs/CHANGELOG.md

Known limitations (not addressed — out of approved scope for this Epic):
- Single active stream only — no multi-camera grid view (future Epic).
- No Recording, Playback, AI/Motion, hardware-accelerated decode implementation (abstraction present, not implemented), Transcoding, Recording Server, Cluster, or Cloud — all deferred per ADR-002's Future Evolution table.
- Not manually smoke-tested interactively against a real camera or in the running WPF UI — verification here is build + full automated test suite + a real (non-fake) FFmpeg decode/render/reconnect measurement harness + non-interactive app-launch smoke test, consistent with how prior Epics were reported in this environment.
- Performance baseline uses MJPEG test content and a standalone harness, not H.264 against a real camera through the shipped UI's interactive render path (see Docs/PERFORMANCE_BASELINE.md for full methodology and caveats).
- See "Accepted Technical Debt" above for the deferred architecture-review findings.

---

### Version 1.13 - Epic-009 Dashboard Reality

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- Replaced the empty `DashboardView`/`DashboardViewModel` placeholder (verified: no members, no `DataContext` ever set) with a real, read-only aggregation over already-existing Camera/Driver/Connection data — Total cameras, Online/Offline/Unknown, online rate, cameras by `ConnectionType`, cameras by `Brand`, implemented-vs-unimplemented driver coverage, recently added/modified cameras, last-refreshed timestamp, manual Refresh, and load-error state. Exactly the fixed v1 list approved by the Product Owner — nothing beyond it.
- Added `CameraDashboardSummaryBuilder` (`VSP.Device/Services/`): a small, pure, static aggregation function over an already-loaded `IReadOnlyList<Camera>` + `IReadOnlyList<DriverDescriptor>` — no I/O, no repository access of its own, no schema change, no new package.
- **Per Product Owner refinement, `ConnectionType`/`Brand` breakdowns are presented as neutral current-state counts only — never framed as Discovery activity, Last Scan, Found Devices, or registration provenance.** This is not just a labeling choice: verified during Current-State Analysis that neither is retrievable. `VSP.UI` never invokes `IDiscoveryRunner` (the shipped Discovery workspace calls `DiscoveryOrchestrator` directly, per the Epic-006 refactor), and every `IDiscoverySessionSink`/`IDiscoveryMetricsSink`/`IDiscoveryDiagnosticsSink` implementation that exists is a no-op. `RegistrationSource` is never persisted onto `Camera`. None of this data exists to show, so nothing claims to show it.
- **Per Product Owner refinement, `Camera.Recording` is omitted entirely from `CameraDashboardSummary`** — not included and not labeled "Not implemented," simply absent, since it was outside the approved v1 field list. Verified during Current-State Analysis that no production code path ever sets it `true`.
- "Unknown" status bucket is `CameraStatus.Connecting`/`CameraStatus.Error` (any status that is neither `Online` nor `Offline`) — an explicit interpretation of the Product Owner's "Online / Offline / Unknown" wording, stated here rather than left implicit.
- No charts, no live thumbnails, no new package, no database schema change — plain WPF tiles/lists matching existing card styling, consistent with the approved constraint.
- Added `CameraDashboardSummaryBuilderTests` (10 tests, pure-logic, thorough: empty input, status bucketing, online-rate rounding, grouping, driver coverage, recency ordering/limiting, null-argument guards) and `DashboardViewModelTests` (6 tests: load-once semantics, refresh-always-reloads, error state on repository failure, error-state recovery on next successful refresh).
- Full suite: 547/547 passing (532 pre-existing + 15 new), stable across 2 consecutive runs. Build passing.

Files:
- Docs/SPECS/EPIC-009_DASHBOARD_REALITY.md
- VSP.Device/Services/CameraDashboardSummary.cs
- VSP.Device/Services/CameraCategoryCount.cs
- VSP.Device/Services/CameraSummaryEntry.cs
- VSP.Device/Services/CameraDashboardSummaryBuilder.cs
- VSP.UI/ViewModels/DashboardViewModel.cs
- VSP.UI/Views/DashboardView.xaml
- VSP.UI/Views/DashboardView.xaml.cs
- VSP.Tests/Services/CameraDashboardSummaryBuilderTests.cs
- VSP.Tests/Camera/DashboardViewModelTests.cs
- Docs/CHANGELOG.md

Known limitations (not addressed — out of approved scope for this Epic):
- No Discovery activity history, Last Scan, Found Devices, or Discovery-added counts — the underlying data does not exist (see Summary above), not merely deferred.
- No registration-provenance breakdown — `RegistrationSource` is never persisted.
- No Recording metric of any kind.
- Not manually smoke-tested in a running instance of the application — WPF's interactive GUI is outside what this environment can exercise directly; verification here is build + full automated test suite only, consistent with how Epic-008 was reported.
- No charts/graphs; plain numeric tiles and lists only, per approved constraint.

---

### Version 1.12 - Epic-008 Driver Settings UI

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- `CameraDetailWindow` now renders every driver's editable settings exclusively from `DriverSettingsDefinition` (Task-303, backend-only until now) instead of six hardcoded fields (`HttpPort`, `RtspPort`, `SdkPort`, `Username`, `Password`, `RtspUrl`) shown identically regardless of the selected driver. Verified by inspection: the only `DriverSettingKey`-keyed switch remaining anywhere in `CameraDetailViewModel`/`CameraDetailWindow` is the persistence bridge mapping the generic settings collection onto `Camera`'s fixed columns — the same shared vocabulary across every driver, not a per-driver/per-key conditional (no `Hikvision`/`Dahua`/`ONVIF`/`RTSP` string literals appear anywhere in either file).
- **Found and fixed a structural gap discovered during Current-State Analysis, not anticipated when this Epic was proposed:** there was no `ConnectionType` selector anywhere in Camera Detail — only `Brand`. `ConnectionType` (the field `DriverRegistry` actually keys off) was set once to `Unknown` in `CreateNewCamera()` and never changed, meaning every manually-added camera silently fell back to the RTSP driver regardless of the chosen Brand. Added a `ConnectionType` selector (mirroring `Brand`'s existing edit/display pattern) as a necessary prerequisite for the approved DoD — not a new feature, and not a resolution of `Brand`'s (still independent, still undefined) relationship to `ConnectionType`.
- Added `DriverSettingValueKind` (`Text`/`Port`/`Url`) to `DriverSettingDefinition` (additive, default `Text`) so validation format also comes from metadata, not a UI-side switch on field identity — the alternative would still have been a form of hardcoding, just on key identity instead of driver identity.
- Added `DriverSettingEditorViewModel`: one per definition entry, self-validating from `IsRequired`/`ValueKind`, computed masked `DisplayValue` from `IsSensitive`.
- `CameraDetailViewModel.DriverSettings` rebuilds whenever `ConnectionType` changes, preserving values for `DriverSettingKey`s present in both the old and new definition (e.g. `Username`/`Password` survive switching between Hikvision and Dahua, both HTTP-based) rather than discarding in-progress edits.
- `CameraDetailWindow.xaml` replaced three hardcoded field rows with one generic `ItemsControl`/`DataTemplate` over `DriverSettings` — `IsSensitive` (from the item) switches TextBox/PasswordBox, `IsEditMode` (reached via standard `RelativeSource AncestorType=Window` ambient binding) switches display/edit. `.xaml.cs`'s single hardcoded `Password`/`PasswordBox` wiring was replaced with generic `Loaded`/`PasswordChanged` handlers keyed off each item's own `DataContext`, working for any sensitive setting on any driver without change.
- **A real, foreseeable behavior change surfaced by the new `ValueKind.Url` validation, not a regression:** `RtspUrl` is now format-validated client-side (must parse as an absolute URI) before Test Connection is attempted, where previously a malformed value would only be caught by the RTSP driver itself failing to connect. Two pre-existing tests whose non-absolute-URI test value ("not-a-valid-uri") was previously only caught by the driver now get caught one step earlier by this validation; repointed to a syntactically-valid-but-wrong-scheme URL to keep exercising the driver-level failure path they were written to test.
- Rewrote `CameraDetailViewModelTests.cs` (637 lines, 26 tests, 37 references to the removed fields — quantified during Current-State Analysis, not estimated): test fixture now sets a real `ConnectionType` matching its `Brand`; all references to the removed properties rewritten against `DriverSettings`; added coverage for connection-type-change rebuild/value-preservation/dropped-keys behavior and for `MapToCamera` only touching keys in the active definition (verified a camera's `RtspPort` survives a Hikvision-context save untouched). Added `DriverSettingEditorViewModelTests.cs` (new) for the editor ViewModel in isolation.
- Full suite: 532/532 passing (509 pre-existing + 23 new/net). Build passing.

Files:
- Docs/SPECS/EPIC-008_DRIVER_SETTINGS_UI.md
- VSP.Device/Drivers/Settings/DriverSettingValueKind.cs
- VSP.Device/Drivers/Settings/DriverSettingDefinition.cs
- VSP.Device/Drivers/Plugins/BuiltInCameraDriverPlugin.cs
- VSP.UI/ViewModels/DriverSettingEditorViewModel.cs
- VSP.UI/ViewModels/CameraDetailViewModel.cs
- VSP.UI/Views/CameraDetailWindow.xaml
- VSP.UI/Views/CameraDetailWindow.xaml.cs
- VSP.Tests/Camera/CameraDetailViewModelTests.cs
- VSP.Tests/Camera/DriverSettingEditorViewModelTests.cs
- Docs/CHANGELOG.md

Known limitations (not addressed — out of approved scope for this Epic):
- No relationship between `Brand` and `ConnectionType` was introduced or fixed; both remain independently editable, exactly as undefined as before this Epic.
- Not manually smoke-tested in a running instance of the application — WPF's interactive GUI is outside what this environment can exercise directly; verification here is build + full automated test suite only. Flagged explicitly rather than claimed.
- Axis (`DeviceConnectionType.AxisVAPIX`) still has no registered driver or settings definition; selecting it in the new Connection Type dropdown yields an empty settings list (consistent, honest behavior for an unregistered type, not a new gap introduced by this Epic).
- `DriverSettingValueKind` currently has three cases (`Text`/`Port`/`Url`); no validation exists yet for other conceivable kinds (e.g. IP address) since none of the current four drivers' definitions need one.

---

### Version 1.11 - Epic-007 Camera Connectivity Foundation

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- Established the foundation of the Camera Connectivity layer: real `TestConnection` and `GetDeviceInformation` for ONVIF, the first implementation on a shared, Hikvision-reusable HTTP transport. Per the approved Definition of Done, **every driver where `DriverFactory.IsDriverImplemented() == true` now has a real implementation** — today that is RTSP (Epic-003) and ONVIF (this Epic); no implemented driver remains a stub.
- Added `IDeviceDriver.GetDeviceInformation(Camera) -> DeviceInformation?` (new interface member, alongside `TestConnection`) and `DeviceCapability.SupportsDeviceInformation` (new, additive flag). `OnvifCameraDriver` is the only driver that implements it for real (`SupportsDeviceInformation = true`); `RtspCameraDriver`, `HikvisionIsapiCameraDriver`, and `DahuaNetSdkCameraDriver` return `null`, honestly reflecting that they don't support it yet, rather than claiming a capability that doesn't exist.
- `OnvifCameraDriver.TestConnection` calls ONVIF `GetSystemDateAndTime` (unauthenticated per the ONVIF spec) against `http://{Camera.IpAddress}:{Camera.HttpPort}/onvif/device_service`; success requires both a 2xx HTTP status and a well-formed, non-SOAP-Fault response. `GetDeviceInformation` calls ONVIF `GetDeviceInformation`, including a WS-Security UsernameToken (PasswordDigest profile, SHA-1 via BCL `System.Security.Cryptography`, no external package) whenever `Camera.Username` is non-empty, and returns `Manufacturer`/`Model`/`FirmwareVersion`/`SerialNumber` when present in the response (`null` field-by-field when a given field is absent, `null` overall on failure/fault).
- Added `VSP.Device/Drivers/Http/HttpDriverTransport`: a small, protocol-agnostic, static HTTP send/receive helper (`HttpClient.Send`, a genuinely synchronous .NET 5+ API — not a blocking wrapper over the async API, and not a change to `IDeviceDriver`'s synchronous calling convention). Deliberately mirrors `TcpRtspTransport`'s static, throw-on-failure shape (the caller's own `try/catch` translates failures to `false`/`null`, exactly as `RtspCameraDriver` already does) rather than introducing a Result-wrapper/interface pattern foreign to this layer. Carries zero ONVIF-specific logic, verified by inspection, and is ready for a future Hikvision ISAPI Epic to reuse without modification.
- Added `OnvifDeviceManagementRequestFactory`/`OnvifDeviceManagementResponseParser`/`OnvifWsSecurityHeaderBuilder`, hand-rolled XML via `System.Xml.Linq`, matching this repository's existing house style (`OnvifWsDiscoveryProbeBuilder`/`ResponseParser`) rather than introducing a SOAP toolkit or WS-Security package.
- `DriverFactory.IsDriverImplemented(ONVIF)` flipped `false -> true`.
- **Found and fixed a real bug during test-writing, not a pre-existing one:** `StringContent`'s `mediaType` constructor parameter must be a bare media type (e.g. `"application/soap+xml"`) — passing `"application/soap+xml; charset=utf-8"` (charset already appended) caused every real HTTP round trip to fail fast. Caught by the new `OnvifCameraDriverTests` loopback-server tests before this reached the Product Owner, not after.
- Extending `IDeviceDriver` required updating every implementer to keep the solution compiling: 3 stub drivers (RTSP/Hikvision/Dahua, one-line `return null;` each) and **5** test-double `ICameraDriver` fakes across `DriverSelectionTests`, `DriverRegistryTests`, `DriverPluginTests`, `DriverCompatibilityCapabilityTests`, and `DriverSettingsTests` (the fifth was missed by an initial grep that didn't match its fully-qualified `VSP.Device.Drivers.Abstractions.ICameraDriver` usage — caught by the very next build).
- Flipping `IsDriverImplemented(ONVIF)` broke one pre-existing test whose "unimplemented driver" example hard-coded ONVIF (`CameraDetailViewModelTests.TestConnectionCommand_UnimplementedDriver_ReportsNotImplemented`); repointed to Hikvision ISAPI, which is still genuinely unimplemented — the test's intent is unchanged, only the example driver.
- Added 28 new unit tests: `OnvifWsSecurityHeaderBuilderTests` (digest correctness independently recomputed, nonce randomness/length), `OnvifDeviceManagementRequestFactoryTests` (Security header present/absent), `OnvifDeviceManagementResponseParserTests` (success/fault/malformed/partial-field parsing), `OnvifCameraDriverTests` (end-to-end against a real loopback HTTP server — `LoopbackHttpTestServer`, `HttpListener`-based, bound to a specific loopback port to avoid the Windows URL-ACL requirement that wildcard prefixes need). Full suite: 509/509 passing (481 pre-existing + 28 new), stable across 3 consecutive full runs. Build passing.

Files:
- Docs/SPECS/EPIC-007_CAMERA_CONNECTIVITY_FOUNDATION.md
- VSP.Device/Drivers/Abstractions/IDeviceDriver.cs
- VSP.Device/Drivers/Abstractions/DeviceInformation.cs
- VSP.Device/Drivers/DriverFactory.cs
- VSP.Device/Drivers/ONVIF/OnvifCameraDriver.cs
- VSP.Device/Drivers/ONVIF/OnvifDeviceManagementRequestFactory.cs
- VSP.Device/Drivers/ONVIF/OnvifDeviceManagementResponseParser.cs
- VSP.Device/Drivers/ONVIF/OnvifWsSecurityHeaderBuilder.cs
- VSP.Device/Drivers/RTSP/RtspCameraDriver.cs
- VSP.Device/Drivers/Hikvision/HikvisionIsapiCameraDriver.cs
- VSP.Device/Drivers/Dahua/DahuaNetSdkCameraDriver.cs
- VSP.Device/Drivers/Http/HttpDriverRequest.cs
- VSP.Device/Drivers/Http/HttpDriverResponse.cs
- VSP.Device/Drivers/Http/HttpDriverTransport.cs
- VSP.Domain/Enums/DeviceCapability.cs
- VSP.Tests/Drivers/ONVIF/LoopbackHttpTestServer.cs
- VSP.Tests/Drivers/ONVIF/OnvifWsSecurityHeaderBuilderTests.cs
- VSP.Tests/Drivers/ONVIF/OnvifDeviceManagementRequestFactoryTests.cs
- VSP.Tests/Drivers/ONVIF/OnvifDeviceManagementResponseParserTests.cs
- VSP.Tests/Drivers/ONVIF/OnvifCameraDriverTests.cs
- VSP.Tests/Drivers/DriverSelectionTests.cs
- VSP.Tests/Drivers/DriverRegistryTests.cs
- VSP.Tests/Drivers/DriverPluginTests.cs
- VSP.Tests/Drivers/DriverCompatibilityCapabilityTests.cs
- VSP.Tests/Drivers/DriverSettingsTests.cs
- VSP.Tests/Camera/CameraDetailViewModelTests.cs
- Docs/CHANGELOG.md

Known limitations (not addressed — out of approved scope for this Epic):
- Hikvision ISAPI implementation itself is explicitly excluded (Product Owner refinement); only the shared, protocol-agnostic HTTP transport is built now.
- Dahua NetSDK (native vendor SDK, not a hand-rollable wire protocol) and Axis (no driver class exists at all) are both untouched — separate, larger decisions flagged during Current-State Analysis, not resolved here.
- WS-Security auth is sent proactively when credentials are configured; ONVIF-specific SOAP Fault-code parsing to retry-with-auth after an initial unauthenticated attempt is not implemented (explicitly out of scope).
- `StartLive`/`StopLive`/`Snapshot` remain stubs for every driver, including ONVIF — Live View territory, a separate, larger, likely-external-package Epic, not started.
- No change to `IDeviceDriver`'s synchronous calling convention or to any UI call site — `CameraDetailViewModel`'s Test Connection command and `CameraConnectionTester` (Batch Test) needed no changes to pick up the real ONVIF behavior, since both already went through the existing uniform interface.

---

### Version 1.10 - Epic-006 Camera Discovery Workspace (post-Architecture-Review refactor)

Status:
Implementation Complete — Reviewed — Accepted by Product Owner (uncommitted — pending user commit)

Summary:
- **Superseded a same-day, pre-review design** after the Product Owner's Architecture Review (see `Docs/SPECS/EPIC-006_CAMERA_DISCOVERY_WORKSPACE.md`) identified real duplication and coupling problems in it; that design was never committed. This entry describes the design actually being submitted for acceptance.
- `DiscoveryOrchestrator` remains the single orchestration pipeline. It was extended, not duplicated: `ProcessCandidate` was split into `EvaluateCandidate` (evidence mapping + driver selection + approval-policy evaluation — unchanged logic, just extracted) and `CommitCandidate` (`CameraFactory` + `DeviceRegistrationService` — unchanged logic, just extracted, plus an additive optional name override). Two new public entry points reuse those same private helpers: `DiscoverCandidatesAsync` (evaluates every candidate, never calls `CameraFactory`/`DeviceRegistrationService` — discovery always ends at candidates) and `RegisterCandidate` (commits one previously-evaluated candidate given an approved driver and, optionally, an edited name). The existing single-pass `ExecuteAsync` is behaviorally unchanged — its own full test suite passes without modification, plus a new test asserts `ExecuteAsync` and `DiscoverCandidatesAsync`+`RegisterCandidate` produce identical outcomes for the same candidate.
- `CandidateOrchestrationStatus.Approved` (an enum member that already existed, unused, in the Task-501 foundation) is now the status `DiscoverCandidatesAsync` returns for a candidate with exactly one compatible driver — no new status vocabulary was invented.
- Deleted `CameraDiscoveryWorkspaceService` and `DiscoveryCandidatePreview`, which duplicated `DiscoveryOrchestrator`'s evaluation/commit logic and bypassed the `IDriverApprovalPolicy` seam Task-501 reserved for exactly this need. `CameraDiscoveryOrchestratorFactory` (`VSP.Device/Discovery/Workspace/`) replaces `CameraDiscoveryWorkspaceServiceFactory` as the hand-wired composition root — it now composes `DiscoveryOrchestrator` directly.
- `CameraDiscoveryViewModel`/`CameraDiscoveryCandidateViewModel` now depend on `DiscoveryOrchestrator`/`CandidateOrchestrationResult` directly (no intermediate service). Ambiguous driver matches are still resolved inline via a per-row driver `ComboBox`, now populated from `CandidateOrchestrationResult.DriverApprovalResult.CompatibleDrivers` and pre-selected from `DriverApprovalResult.ApprovedDriver` when the policy already approved exactly one.
- **Discovery moved from a top-level main-navigation tab into a feature inside the Camera Management Workspace**, per the Product Owner's Architecture Review direction (`Devices → Camera List / Import / Discovery / Batch / Export`, not `Devices` / `Discovery` as navigation siblings). `CameraListViewModel` gained `IsShowingDiscovery`/`IsShowingCameraList` and `ShowDiscoveryCommand`/`ShowCameraListCommand`; `CameraListView.xaml` embeds `CameraDiscoveryView` as a persistent (state-preserving, not recreated on toggle), Visibility-toggled section reached via a "Discovery" button next to the workspace title, instead of a `MainWindowViewModel` navigation entry.
- `OnvifDiscoveryService`/`NetworkScanService`/`RtspEndpointProbeService` implementing their `AutoDiscovery` interfaces, and `NetworkScanTargetParser`, are unaffected by this refactor and retained as-is.
- Naming (e.g. `CameraDiscoveryOrchestratorFactory`) was left otherwise unoptimized per the Product Owner's explicit direction to finalize responsibilities before naming; the factory's own name was changed only because its previous name became factually inaccurate once the type it constructs changed.
- Full suite: 481/481 passing (457 pre-existing + 7 `NetworkScanTargetParserTests`, kept + 9 new/rewritten `DiscoveryOrchestratorTests` cases + 5 `CameraDiscoveryViewModelTests` + 3 new `CameraListViewModel` toggle tests). Build passing.

Files:
- Docs/SPECS/EPIC-006_CAMERA_DISCOVERY_WORKSPACE.md
- VSP.Device/Discovery/Onvif/OnvifDiscoveryService.cs
- VSP.Device/Discovery/NetworkScan/NetworkScanService.cs
- VSP.Device/Discovery/Rtsp/RtspEndpointProbeService.cs
- VSP.Device/Discovery/Orchestration/DiscoveryOrchestrator.cs
- VSP.Device/Discovery/Workspace/NetworkScanTargetParser.cs
- VSP.Device/Discovery/Workspace/CameraDiscoveryOrchestratorFactory.cs
- VSP.UI/ViewModels/CameraDiscoveryCandidateViewModel.cs
- VSP.UI/ViewModels/CameraDiscoveryViewModel.cs
- VSP.UI/Views/CameraDiscoveryView.xaml / .xaml.cs
- VSP.UI/ViewModels/CameraListViewModel.cs
- VSP.UI/Views/CameraListView.xaml
- VSP.Tests/Discovery/Workspace/NetworkScanTargetParserTests.cs
- VSP.Tests/Discovery/DiscoveryOrchestratorTests.cs
- VSP.Tests/Camera/CameraDiscoveryViewModelTests.cs
- VSP.Tests/Camera/CameraListViewModelTests.cs
- Docs/CHANGELOG.md

Known limitations (not addressed — out of approved scope for this Epic):
- Network Scan / RTSP Probe require explicit target input (single hosts, comma/newline lists, or a last-octet range); there is no CIDR/subnet auto-enumeration.
- The Hikvision/Dahua "always compatible" driver-metadata gap itself is unchanged — resolved at the UI layer (inline driver choice) for this Epic, per the Product Owner's default scope decision recorded in the Epic definition. Correcting the underlying metadata remains Driver Framework (Task-405) territory.
- Registering a camera from the Discovery section does not automatically refresh the Camera List grid if it was already loaded in the same session (pre-existing app pattern — `CameraListView` only reloads on its own explicit `Refresh` action or first load, consistent with how it already behaved for Import/Batch actions before this Epic).
- The embedded `CameraDiscoveryView` keeps its own internal header/footer chrome inside the Camera Management Workspace's Discovery section, which reads as a minor nested-chrome redundancy rather than a fully unified layout; not addressed, since restyling either view was not part of this Epic's approved scope.
- No Discovery Session history/audit UI, no user-configurable Retry/Timeout policy in the UI, no `RejectAmbiguousPolicy`/`HighestConfidencePolicy` — all explicitly out of scope.
- `DiscoverCandidatesAsync`'s UI-side "suggested name" pre-fill (`CameraDiscoveryCandidateViewModel.BuildSuggestedName`) duplicates the small, non-authoritative "pick first non-empty string" heuristic also used internally by `DiscoveryOrchestrator.CreateInitializationData`'s own fallback (triggered only when a caller submits a blank name override). This is judged to be a display-only concern, not the evaluation/registration logic Direction 1 protects, since the fallback that actually governs what gets persisted remains solely in `DiscoveryOrchestrator`.

---

### Version 1.9 - Epic-005 Camera Management Workspace

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- Established `CameraListView`/`CameraListViewModel` as the primary device management workspace, hosted in `MainWindowViewModel`'s "Devices" navigation tab in place of the older `DeviceCenterView`/`DeviceCenterViewModel`. This makes the already-built, already-tested camera list, search/filter, `CameraDetailWindow` (Save/Delete with validation and unsaved-changes protection), Batch Edit, Batch Connection Test, and Export flows reachable from the running application for the first time.
- Added an "Import" entry point to `CameraListView`, wired to the existing `ImportWizard` (CSV/Excel import, preview, validation, duplicate handling, `ImportSummaryWindow`), which previously had no reachable entry point anywhere in the UI. The camera list refreshes automatically after the Import Wizard closes.
- Added a dedicated "Test Connection" action to `CameraDetailWindow` (hidden in New Mode, matching the `Delete` button's visibility rule), restoring the single-camera connection test experience previously available in `DeviceCenterView`. Tests the *current, possibly-unsaved form values* (not the last-saved camera), validating the form first and reporting one of "Connection successful.", "Connection failed.", "Driver not implemented.", or a validation message. Added `DriverFactory.IsDriverImplemented(DeviceConnectionType)` as the single shared source of truth for the implemented/not-implemented distinction, replacing the copy that previously lived only in the now-retired `DeviceCenterViewModel`.
- `DeviceCenterView`/`DeviceCenterView.xaml.cs`/`DeviceCenterViewModel` are no longer hosted in `MainWindowViewModel` but were **not deleted** — marked `[Obsolete]` with an explanatory comment ("superseded by CameraListView/CameraListViewModel, scheduled for removal in a future Legacy Cleanup Epic") to minimize migration risk. Confirmed via reference search that nothing else in the solution depends on them.
- Added 6 unit tests for the new Test Connection behavior in `CameraDetailViewModel` (unimplemented driver, RTSP failure, RTSP success via loopback server, validation blocking, disabled in New Mode, and current-vs-stale form values). All other functionality in this Epic is integration/wiring work over already-tested components (Import, Batch Edit, Batch Connection Test predate this Epic and keep their existing test coverage unchanged).
- Import flow confirmed end-to-end: **Import Wizard → Import Summary → Camera List Refresh.** Clicking "Import" in `ImportWizardViewModel` fires `ImportCompleted`, which `ImportWizard` handles by showing `ImportSummaryWindow` modally; once the user closes Import Summary *and* then closes the Import Wizard itself, `CameraListView.xaml.cs`'s `HandleRequestImport` resumes past `ShowDialog()` and calls `_viewModel.RefreshAsync()`. The refresh is keyed to the Import Wizard closing, not to the Summary closing — if a user runs a second import within the same Wizard session, the list only refreshes once, when the Wizard itself finally closes.

Files:
- VSP.UI/ViewModels/MainWindowViewModel.cs
- VSP.UI/ViewModels/CameraListViewModel.cs
- VSP.UI/ViewModels/CameraDetailViewModel.cs
- VSP.UI/ViewModels/DeviceCenterViewModel.cs (marked `[Obsolete]`, not deleted)
- VSP.UI/Views/CameraListView.xaml
- VSP.UI/Views/CameraListView.xaml.cs
- VSP.UI/Views/CameraDetailWindow.xaml
- VSP.UI/Views/DeviceCenter/DeviceCenterView.xaml.cs (marked `[Obsolete]`, not deleted)
- VSP.Device/Drivers/DriverFactory.cs
- VSP.Tests/Camera/CameraDetailViewModelTests.cs
- Docs/CHANGELOG.md

Known limitations (not addressed — out of approved scope for this Epic):
- `DeviceCenterView`/`DeviceCenterViewModel` remain in the codebase as unreferenced, `[Obsolete]`-marked legacy code. Their actual removal is deferred to a future Legacy Cleanup Epic, not this one.
- `CameraConnectionTestResult` (used by Batch Test) still reports only `IsSuccess`, not the "driver not implemented" distinction that `CameraDetailWindow`'s new single-camera Test Connection action now surfaces via `DriverFactory.IsDriverImplemented`. This is pre-existing behavior from Epic-002's Batch Connection Test feature; extending Batch Test to use the same distinction was not part of this Epic's approved scope.
- `CameraListViewModel.BrandOptions` (`All, Hikvision, Dahua, VIVOTEK`) does not match the `CameraBrand` enum (`Unknown, Hikvision, Dahua, ONVIF, RTSP`) — a pre-existing inconsistency predating this Epic, left unchanged per Surgical Changes (not related to this Epic's scope).

---

## 2026-07-25

### Version 1.8 - Epic-003 RTSP Connection Foundation

Status:
Implementation Complete — Reviewed — Accepted by Product Owner (uncommitted — pending user commit)

Summary:
- Implemented `RtspCameraDriver.TestConnection()`: connects to the camera's exact configured `Camera.RtspUrl`, sends an RTSP DESCRIBE request, and treats any final 2xx status as success.
- On a 401 challenge, parses the `WWW-Authenticate` header (`RtspWwwAuthenticateParser`) and retries exactly once with a computed Basic or Digest (MD5, `qop=auth` and no-qop) `Authorization` header (`RtspAuthorizationHeaderBuilder`); a second 401, malformed response, invalid URL, timeout, connection failure, or unsupported challenge scheme all return `false` without throwing past the `TestConnection` boundary.
- Added `TcpRtspTransport` for the underlying socket I/O: bounded connect/read timeouts, accumulation of partial TCP reads, `\r\n\r\n` header-termination detection, and a 16 KB max response size cap; connections and streams are always disposed.
- Added `RtspDescribeRequestFactory` / `RtspDescribeResponseParser` as small protocol-focused helpers for building the DESCRIBE request and parsing the status line/headers.
- Enabled RTSP in `DeviceCenterViewModel.IsDriverImplemented` (single-line flag flip; no other UI logic changed).
- Added 34 new unit tests (`VSP.Tests/Drivers/RTSP/`) covering auth flows (Basic/Digest, single-retry-only), malformed/timeout/invalid-URL/unsupported-challenge cases, and transport-level behavior, using a bounded, self-disposing `LoopbackRtspTestServer` loopback helper (background thread; cannot hang the test process).
- Reviewed against scope (RTSP/TestConnection only — no Snapshot/SETUP/PLAY/Streaming/ONVIF/Hikvision/Dahua, no Driver Framework or Discovery changes, no new external dependencies), functional correctness, network robustness, and test quality. Accepted by Product Owner with two non-blocking follow-ups recorded below.

Files:
- VSP.Device/Drivers/RTSP/RtspCameraDriver.cs
- VSP.Device/Drivers/RTSP/RtspAuthorizationHeaderBuilder.cs
- VSP.Device/Drivers/RTSP/RtspDescribeRequestFactory.cs
- VSP.Device/Drivers/RTSP/RtspDescribeResponseParser.cs
- VSP.Device/Drivers/RTSP/RtspWwwAuthenticateParser.cs
- VSP.Device/Drivers/RTSP/TcpRtspTransport.cs
- VSP.UI/ViewModels/DeviceCenterViewModel.cs
- VSP.Tests/Drivers/RTSP/LoopbackRtspTestServer.cs
- VSP.Tests/Drivers/RTSP/RtspAuthorizationHeaderBuilderTests.cs
- VSP.Tests/Drivers/RTSP/RtspCameraDriverTests.cs
- VSP.Tests/Drivers/RTSP/RtspDescribeRequestFactoryTests.cs
- VSP.Tests/Drivers/RTSP/RtspDescribeResponseParserTests.cs
- VSP.Tests/Drivers/RTSP/RtspWwwAuthenticateParserTests.cs
- VSP.Tests/Drivers/RTSP/TcpRtspTransportTests.cs
- Docs/CHANGELOG.md

Technical Debt:
- TD-027 `TcpRtspTransport` overall operation timeout
  Reason: The current implementation enforces a per-read timeout (`NetworkStream.ReadTimeout`, reset on every `Read()` call) but not an overall deadline for the whole DESCRIBE round trip, so a server that trickles bytes just under the per-read timeout could hold the connection open indefinitely. Accepted as non-blocking for Epic-003.
- TD-028 Additional RTSP transport robustness tests
  Reason: Future enhancement to cover fragmented/multi-chunk header reads and max-response-size-cap enforcement (currently only the "server never responds" hang case is tested, not "server responds forever without a `\r\n\r\n` terminator"). Accepted as non-blocking for Epic-003.

Known documentation debt (not fixed — out of confirmed scope for this Epic):
- Docs/PROJECT_STATUS.md remains stale (predates this Epic; still shows TD-001/TD-002 from the M1 release and does not reflect the current TD-027/TD-028 numbering used in this CHANGELOG).
- No formal Epic definition document exists for Epic-003 satisfying every field required by `AUTONOMOUS_DEVELOPMENT.md` §2 (Epic ID, Objective, Scope Boundary, Risk Ceiling, Constituent Tasks, Definition of Done, Approval Record) — consistent with the same known gap recorded for Epic-002 above.

---

### Version 1.7 - Epic-002 Device Management Continuation (Task-213–216)

Status:
Implementation Complete — Pending Product Owner Acceptance (uncommitted — pending user commit)

Summary:
- Completed Task-213 Batch Edit: multi-select checkbox column on the camera list, a "Batch Edit" dialog applying Brand/Location/Username/Password to 2+ selected cameras via looped `ICameraRepository.Update()`. This Task's implementation was already present in the working tree at Epic resume time; this entry is its first CHANGELOG record.
- Completed Task-214 Batch Connection Test: a "Batch Test" action reusing the Driver Framework via a new `ICameraConnectionTester` service, showing per-camera Success/Failed results in a dialog. The service, dialog ViewModel/View, and `CameraListItemViewModel.IsSelected` plumbing already existed in the working tree at Epic resume time; this Task completed the missing piece — wiring `BatchConnectionTestCommand`/`RequestBatchConnectionTest` into `CameraListViewModel`/`CameraListView`, and adding the missing `BatchConnectionTestViewModelTests`.
- Added Task-215 Export: an "Export" action on the camera list, enabled whenever the current filtered view is non-empty, writing a CSV using the same column layout as `CsvImportParser` (round-trip compatible with Import) via a native Save File dialog.
- Added Task-216 Device Status Enhancement: `BatchConnectionTestViewModel` now persists each tested camera's `Status` (Online/Offline) via `ICameraRepository.Update()`, and `CameraListView` refreshes the list after the Batch Test dialog closes so the Status column reflects real connectivity instead of a permanent `Offline` default.
- Task-215 and Task-216 had no prior Task Specification; both were drafted directly as implementation artifacts of this already-approved Epic (Implementation Authority, `AI_OPERATING_SYSTEM.md` §22) and are included in this entry.

Files:
- VSP.UI/ViewModels/CameraListItemViewModel.cs
- VSP.UI/ViewModels/CameraListViewModel.cs
- VSP.UI/ViewModels/BatchEditViewModel.cs
- VSP.UI/Views/BatchEditWindow.xaml / .xaml.cs
- VSP.UI/Views/CameraListView.xaml / .xaml.cs
- VSP.Device/Services/ICameraConnectionTester.cs
- VSP.Device/Services/CameraConnectionTester.cs
- VSP.Device/Services/CameraConnectionTestResult.cs
- VSP.UI/ViewModels/BatchConnectionTestViewModel.cs
- VSP.UI/ViewModels/BatchConnectionTestItemViewModel.cs
- VSP.UI/Views/BatchConnectionTestWindow.xaml / .xaml.cs
- VSP.Device/Export/CameraExportWriter.cs
- VSP.UI/Helpers/ExportFileSelector.cs
- VSP.Tests/Camera/BatchEditViewModelTests.cs
- VSP.Tests/Camera/CameraListViewModelBatchSelectionTests.cs
- VSP.Tests/Camera/BatchConnectionTestViewModelTests.cs
- VSP.Tests/Export/CameraExportWriterTests.cs
- Docs/SPECS/Task-213_BATCH_EDIT.md
- Docs/SPECS/Task-214_BATCH_CONNECTION_TEST.md
- Docs/SPECS/Task-215_EXPORT.md
- Docs/SPECS/Task-216_DEVICE_STATUS_ENHANCEMENT.md
- Docs/03_PRODUCT_ROADMAP.md
- Docs/CHANGELOG.md

Known documentation debt (found during this Epic's Current-State Analysis, not fixed — out of confirmed scope):
- Docs/03_ROADMAP.md contains pre-existing mojibake (not UTF-8-clean Chinese text, predates this Epic) and uses a different Task/Epic numbering scheme (EPIC-01/Task-101...) than the actively-maintained Docs/03_PRODUCT_ROADMAP.md (Task-2xx). Only 03_PRODUCT_ROADMAP.md was updated by this entry, to avoid risking further corruption of 03_ROADMAP.md's encoding.
- Docs/PROJECT_STATUS.md is stale (predates the Discovery Epic and this Device Management continuation; still shows 88 tests and "Next Milestone: Device Management").
- No formal Epic definition document exists for Epic-002 satisfying every field required by `AUTONOMOUS_DEVELOPMENT.md` §2 (Epic ID, Objective, Scope Boundary, Risk Ceiling, Constituent Tasks, Definition of Done, Approval Record) — the Task-213/214 spec headers only informally reference "Epic-002 (EPIC-01 Device Management continuation)". This continuation proceeded on the basis that the user's current, explicit instruction is the highest-authority source per `AI_OPERATING_SYSTEM.md` §1.

---

### Version 1.6 - Epic Discovery Foundation (Task-601 fix, Task-602–607)

Status:
Completed (uncommitted — pending user commit)

Summary:
- Fixed Task-601 `DiscoveryRunner` to match its approved spec: removed the `DiscoverySessionFactory`/`IDiscoverySessionSink` dependency that had been embedded directly in its constructor (a scope violation caught in review), and introduced `IDiscoveryRunner` so future hooks decorate the runner from the outside instead of adding dependencies to it.
- Added Task-602 Progress Hook: `ProgressPublishingDiscoveryRunner` publishes a start and a terminal `DiscoveryProgress` around an execution.
- Added Task-603 Session Hook: `SessionRecordingDiscoveryRunner` records a `DiscoverySession` per execution via `DiscoverySessionFactory` — properly re-implementing, as an opt-in decorator, the capability removed from `DiscoveryRunner` in the Task-601 fix.
- Added Task-604 Retry Hook: `RetryingDiscoveryRunner` retries a `Failed` result or a non-cancellation exception up to a configured attempt count with a fixed delay; never retries `Cancelled` or `InvalidRequest` outcomes or `OperationCanceledException`.
- Added Task-605 Timeout Hook: `TimeoutDiscoveryRunner` enforces a per-execution operation timeout distinct from caller cancellation, raising `DiscoveryTimeoutException` rather than adding a `TimedOut` value to `DiscoveryOrchestrationStatus` (explicitly disallowed by Task-505 §5).
- Added Task-606 Metrics Hook: `MetricsRecordingDiscoveryRunner` records a minimal `DiscoveryMetricsSample` (status, duration, correlation id) per execution, no external metrics package.
- Added Task-607 Diagnostics Hook: `DiagnosticsRecordingDiscoveryRunner` publishes a `DiscoveryDiagnosticsSnapshot` (diagnostic id, timestamp, correlation id, status, reasons) per execution.
- Every hook is an independent `IDiscoveryRunner` decorator; none adds a dependency to `DiscoveryRunner` or `DiscoveryOrchestrator` itself.

Files:
- VSP.Device/Discovery/Execution/IDiscoveryRunner.cs
- VSP.Device/Discovery/Execution/DiscoveryRunner.cs
- VSP.Device/Discovery/Execution/ProgressPublishingDiscoveryRunner.cs
- VSP.Device/Discovery/Progress/IDiscoveryProgressPublisher.cs
- VSP.Device/Discovery/Progress/NoOpDiscoveryProgressPublisher.cs
- VSP.Device/Discovery/Execution/SessionRecordingDiscoveryRunner.cs
- VSP.Device/Discovery/Sessions/IDiscoverySessionSink.cs
- VSP.Device/Discovery/Sessions/NoOpDiscoverySessionSink.cs
- VSP.Device/Discovery/Execution/RetryingDiscoveryRunner.cs
- VSP.Device/Discovery/Execution/DiscoveryRetryPolicy.cs
- VSP.Device/Discovery/Execution/TimeoutDiscoveryRunner.cs
- VSP.Device/Discovery/Execution/DiscoveryTimeoutPolicy.cs
- VSP.Device/Discovery/Execution/DiscoveryTimeoutException.cs
- VSP.Device/Discovery/Execution/MetricsRecordingDiscoveryRunner.cs
- VSP.Device/Discovery/Metrics/DiscoveryMetricsSample.cs
- VSP.Device/Discovery/Metrics/IDiscoveryMetricsSink.cs
- VSP.Device/Discovery/Metrics/NoOpDiscoveryMetricsSink.cs
- VSP.Device/Discovery/Execution/DiagnosticsRecordingDiscoveryRunner.cs
- VSP.Device/Discovery/Diagnostics/DiscoveryDiagnosticsSnapshot.cs
- VSP.Device/Discovery/Diagnostics/IDiscoveryDiagnosticsSink.cs
- VSP.Device/Discovery/Diagnostics/NoOpDiscoveryDiagnosticsSink.cs
- VSP.Tests/Discovery/DiscoveryRunnerTests.cs
- VSP.Tests/Discovery/ProgressPublishingDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/SessionRecordingDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/RetryingDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/TimeoutDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/MetricsRecordingDiscoveryRunnerTests.cs
- VSP.Tests/Discovery/DiagnosticsRecordingDiscoveryRunnerTests.cs
- Docs/SPECS/Task-602_DISCOVERY_PROGRESS_HOOK.md
- Docs/SPECS/Task-603_DISCOVERY_SESSION_HOOK.md
- Docs/SPECS/Task-604_DISCOVERY_RETRY_HOOK.md
- Docs/SPECS/Task-605_DISCOVERY_TIMEOUT_HOOK.md
- Docs/SPECS/Task-606_DISCOVERY_METRICS_HOOK.md
- Docs/SPECS/Task-607_DISCOVERY_DIAGNOSTICS_HOOK.md
- Docs/CHANGELOG.md

Known documentation debt (found during this Epic's Current-State Analysis, not fixed — out of confirmed scope):
- Docs/03_PRODUCT_ROADMAP.md's Discovery entry (Version 1.3) is stale and does not reflect Task-402–607.
- Docs/PROJECT_STATUS.md is stale (predates this entire body of Discovery work).
- No ADR exists yet for the Discovery subsystem's architecture.
- Task-402 through Task-601 were never individually logged in this CHANGELOG; this entry only covers the Task-601 fix and Task-602–607.

---

## 2026-07-13

### Version 1.3 - Task-401 ONVIF Discovery

Status:
Completed

Summary:
- Added the first ONVIF Discovery foundation with WS-Discovery Probe message building, response parsing, and discovery orchestration.
- Added a minimal transport boundary so discovery logic can be unit tested without real multicast or ONVIF devices.
- Implemented deterministic deduplication using EndpointReference, normalized XAddr, and remote sender IP fallback.
- Implemented timeout and explicit cancellation semantics without adding UI, SQLite, repository, or camera-creation logic.

Files:
- VSP.Device/Discovery/Onvif/OnvifDiscoveryRequest.cs
- VSP.Device/Discovery/Onvif/OnvifDiscoveryResult.cs
- VSP.Device/Discovery/Onvif/WsDiscoveryTransportMessage.cs
- VSP.Device/Discovery/Onvif/IWsDiscoveryTransport.cs
- VSP.Device/Discovery/Onvif/OnvifWsDiscoveryProbeBuilder.cs
- VSP.Device/Discovery/Onvif/OnvifWsDiscoveryResponseParser.cs
- VSP.Device/Discovery/Onvif/UdpWsDiscoveryTransport.cs
- VSP.Device/Discovery/Onvif/OnvifDiscoveryService.cs
- VSP.Tests/Discovery/OnvifWsDiscoveryResponseParserTests.cs
- VSP.Tests/Discovery/OnvifDiscoveryServiceTests.cs
- Docs/03_PRODUCT_ROADMAP.md

---

## 2026-07-13

### Version 1.2 - Task-303 Driver Settings

Status:
Completed

Summary:
- Added immutable Driver Settings metadata models for driver setting keys, field definitions, and per-driver settings definitions.
- Extended DriverDescriptor to optionally carry Driver Settings metadata without changing driver runtime interfaces.
- Added conservative built-in settings definitions for Hikvision ISAPI, Dahua NetSDK, ONVIF, and RTSP drivers.
- Kept actual per-device values in Camera and did not add UI, SQLite, repository, or JSON settings changes.

Files:
- VSP.Device/Drivers/Settings/DriverSettingKey.cs
- VSP.Device/Drivers/Settings/DriverSettingDefinition.cs
- VSP.Device/Drivers/Settings/DriverSettingsDefinition.cs
- VSP.Device/Drivers/DriverDescriptor.cs
- VSP.Device/Drivers/Plugins/BuiltInCameraDriverPlugin.cs
- VSP.Tests/Drivers/DriverSettingsTests.cs
- Docs/03_PRODUCT_ROADMAP.md

---

## 2026-07-13

### Version 1.2 - Task-302 Driver Plugin

Status:
Completed

Summary:
- Added a minimal IDriverPlugin contract for in-process driver extension.
- Added BuiltInCameraDriverPlugin as the single source of truth for built-in driver descriptors.
- Added atomic plugin registration through DriverRegistry.RegisterPlugin(...).
- Preserved DriverFactory static API and RTSP fallback behavior.
- No DLL loading, reflection scanning, plugin folders, or settings were introduced.

Files:
- VSP.Device/Drivers/Plugins/IDriverPlugin.cs
- VSP.Device/Drivers/Plugins/BuiltInCameraDriverPlugin.cs
- VSP.Device/Drivers/DriverRegistry.cs
- VSP.Tests/Drivers/DriverPluginTests.cs
- Docs/03_PRODUCT_ROADMAP.md

---
?祆?隞嗉???VSP 撠???閬??質??氬?
---

# Version 2.0

---

## Sprint 1

### S1-1 Device List
?交?嚗?026-06-28

#### ?啣?
- DeviceCenter ?∠ DeviceCenterViewModel??- Device List ?寧?? DeviceService.GetAllCameras() 頛 SQLite Camera 鞈???- 撌血 Device List 雿輻 ListBox??- 摰? Devices ??SelectedDevice Binding??- ?啣? RefreshCommand嚗?頛 Camera 皜??- ?啣? DeviceCount 憿舐內??
#### UI
- 撌血憿舐內嚗?  - Camera Name
  - Brand
  - IP Address
  - Connection Type
- ?喳 Device Editor 靽? Placeholder??
#### ?嗆?
- 蝚血? MVVM??- ViewModel 銝?亙???SQLite??- 蝬 DeviceService ??鞈???- ?芯耨??MainWindow??- ?芯耨??Repository??- ?芯耨??SQLite Schema??- ?芯耨??Legacy DeviceView??
#### Build
- Build Success
- Error嚗?
- Warning嚗U1903嚗QLite 憟辣摰?扯郎??

---
## Sprint 1

### S1-2 Device Detail

Status:
Completed

Summary:
- Device Detail now binds directly to SelectedDevice.
- Display fields:
  - Name
  - Brand
  - Model
  - IP Address
  - Connection Type
- No fake properties added.
- No placeholder values added.
- Repository / SQLite / Driver Framework unchanged.

Files:
- DeviceCenterView.xaml

Reviewed:
2026-06-28

-----
# CHANGELOG
## 2026-08-18 (AI01-008 - Autonomous Multi-Agent Development Pipeline)

### Agent Router / Orchestrator Foundation

Status:
Implementation Complete - Pending Independent Review and Product Owner Acceptance. No commit, push, PR creation, autonomous merge, PR #7 remediation, or RTSP flaky investigation work performed.

Summary:
- Added `AI/Orchestrator/` as the PR-based orchestration layer for Agent Router policy, agent contracts, structured state, token budget gates, stop conditions, crash/session recovery, role separation, and bounded remediation.
- Added PowerShell orchestrator scripts under `tools/orchestrator/` for PR metadata inspection, parallel gate evaluation, token budget checking, state reading, review request, remediation request, and router entry.
- Added local GitHub workflow files for Windows CI, Claude Code Review, Claude Code comment handling, and AI01 Orchestrator routing.
- Preserved the first-version terminal state as `READY_FOR_MERGE`; Product Owner manual merge remains required.
- Added explicit PR #7 protection and kept the paused RTSP flaky investigation out of AI01-008 scope.

Verification:
- `AI/Orchestrator/Templates/task-state.template.json` parsed successfully with PowerShell `ConvertFrom-Json`.
- `tools/orchestrator/*.ps1` parsed successfully with PowerShell parser.
- Product runtime build/test not run because this task changes governance, workflows, and orchestration scripts only; existing RTSP/decoder worktree changes remain untouched.

Files:
- `AI/Orchestrator/**`
- `tools/orchestrator/**`
- `.github/workflows/**`
- `AGENTS.md`, `CLAUDE.md`, `AI/README.md`, `Docs/AI_DEVELOPMENT_WORKFLOW.md`, `Docs/WORKFLOW/IMPLEMENT_TASK.md`, `Docs/WORKFLOW/REVIEW_TASK.md`, `Docs/CHANGELOG.md`

---
---

## 2026-06-28

### Sprint 1 - Task 3
### Device Center - Add Device

Status:
Completed

Summary:

- 摰? Device Center Add Device 瘚?
- Add Device ??撌脩?摰?AddDeviceCommand
- 雿輻?Ｘ? AddDeviceWindow嚗??啣??啗?蝒?- Save 敺? DeviceService.AddCamera() 撖怠 SQLite
- ?啣?摰?敺??啗???Device List
- ?芸??詨??憓? Camera
- Device Detail ?郊憿舐內?啣?鞈?

Architecture:

- 蝬剜? MVVM
- ViewModel 銝?亙???SQLite
- Repository Pattern 銝?
- SQLite Schema ?∩耨??- Driver Framework ?∩耨??
Not Included:

- Edit
- Delete
- Search
- Filter
- Connection Test
- Real-time Validation

Verified:

??Add Device
??SQLite Save
??Refresh Device List
??Detail Binding

---
## 2026-06-28

### Sprint 1 - Task 4
### Device Center - Edit Device

Status:
Completed

Summary:

- 摰? Device Center Edit Device 瘚?
- ?啣? Edit Device ??
- Edit Device 雿輻?Ｘ? AddDeviceWindow(Camera)
- 閬??芸?頛?桀? Camera 鞈?
- Save 敺? DeviceService.UpdateCamera() ?湔 SQLite
- ?湔摰??頛 Device List
- ?芸???詨?靽格敺?Camera
- Device Detail ?郊?湔

Architecture:

- 蝬剜? MVVM
- ViewModel 銝?交?雿?SQLite
- Repository Pattern 銝?
- SQLite Schema ?∩耨??- Driver Framework ?∩耨??
Not Included:

- Delete Device
- Search
- Filter
- Connection Test
- Real-time Validation

Verified:

??Edit Device
??SQLite Update
??Reload Device List
??Auto Select Updated Camera
??Device Detail Refresh

---

## 2026-06-28

### S1-5 Delete Device

摰? Device ?芷瘚???
?啣?嚗?
- DeleteDeviceCommand
- Delete 蝣箄?撠店獢?- DeviceService.DeleteCamera()
- ?芷敺???LoadDevices()
- ?芸??湔 SelectedDevice
- Device Detail ?郊?瑟

?萄?嚗?
- 銝耨??Architecture
- 銝耨??Repository
- 銝耨??SQLite Schema
- 銝憓?DeleteView
- 銝憓?DeleteDialog
- 銝憓?DeleteService

----
# CHANGELOG
## 2026-08-18 (AI01-008 - Autonomous Multi-Agent Development Pipeline)

### Agent Router / Orchestrator Foundation

Status:
Implementation Complete - Pending Independent Review and Product Owner Acceptance. No commit, push, PR creation, autonomous merge, PR #7 remediation, or RTSP flaky investigation work performed.

Summary:
- Added `AI/Orchestrator/` as the PR-based orchestration layer for Agent Router policy, agent contracts, structured state, token budget gates, stop conditions, crash/session recovery, role separation, and bounded remediation.
- Added PowerShell orchestrator scripts under `tools/orchestrator/` for PR metadata inspection, parallel gate evaluation, token budget checking, state reading, review request, remediation request, and router entry.
- Added local GitHub workflow files for Windows CI, Claude Code Review, Claude Code comment handling, and AI01 Orchestrator routing.
- Preserved the first-version terminal state as `READY_FOR_MERGE`; Product Owner manual merge remains required.
- Added explicit PR #7 protection and kept the paused RTSP flaky investigation out of AI01-008 scope.

Verification:
- `AI/Orchestrator/Templates/task-state.template.json` parsed successfully with PowerShell `ConvertFrom-Json`.
- `tools/orchestrator/*.ps1` parsed successfully with PowerShell parser.
- Product runtime build/test not run because this task changes governance, workflows, and orchestration scripts only; existing RTSP/decoder worktree changes remain untouched.

Files:
- `AI/Orchestrator/**`
- `tools/orchestrator/**`
- `.github/workflows/**`
- `AGENTS.md`, `CLAUDE.md`, `AI/README.md`, `Docs/AI_DEVELOPMENT_WORKFLOW.md`, `Docs/WORKFLOW/IMPLEMENT_TASK.md`, `Docs/WORKFLOW/REVIEW_TASK.md`, `Docs/CHANGELOG.md`

---
---

## [Unreleased]

### Added

#### Sprint 1 - S1-6 Search Device

摰? DeviceCenter ?????
?批捆嚗?
- ?啣? Search TextBox
- ?啣? Search Button
- ?啣? Clear Button
- SearchKeyword ?單???嚗extChanged嚗?- ?舀 Name ??
- ?舀 IP ??
- ?舀 Brand ??
- ?舀 Model ??
- ??憭批?撖思????- 雿輻閮擃???(_allDevices) ?脰?蝭拚
- ?啣? ApplySearch()
- 靽??桀? SelectedDevice
- ?⊥?撠???憿舐內 No matching devices found.
- Clear 敺敺拙???Device List

Architecture嚗?
- 銝耨??Repository
- 銝耨??SQLite Schema
- 銝耨??Driver Framework
- 銝憓?SQL Query
- Search ? ViewModel 閮擃???Filter

# CHANGELOG
## 2026-08-18 (AI01-008 - Autonomous Multi-Agent Development Pipeline)

### Agent Router / Orchestrator Foundation

Status:
Implementation Complete - Pending Independent Review and Product Owner Acceptance. No commit, push, PR creation, autonomous merge, PR #7 remediation, or RTSP flaky investigation work performed.

Summary:
- Added `AI/Orchestrator/` as the PR-based orchestration layer for Agent Router policy, agent contracts, structured state, token budget gates, stop conditions, crash/session recovery, role separation, and bounded remediation.
- Added PowerShell orchestrator scripts under `tools/orchestrator/` for PR metadata inspection, parallel gate evaluation, token budget checking, state reading, review request, remediation request, and router entry.
- Added local GitHub workflow files for Windows CI, Claude Code Review, Claude Code comment handling, and AI01 Orchestrator routing.
- Preserved the first-version terminal state as `READY_FOR_MERGE`; Product Owner manual merge remains required.
- Added explicit PR #7 protection and kept the paused RTSP flaky investigation out of AI01-008 scope.

Verification:
- `AI/Orchestrator/Templates/task-state.template.json` parsed successfully with PowerShell `ConvertFrom-Json`.
- `tools/orchestrator/*.ps1` parsed successfully with PowerShell parser.
- Product runtime build/test not run because this task changes governance, workflows, and orchestration scripts only; existing RTSP/decoder worktree changes remain untouched.

Files:
- `AI/Orchestrator/**`
- `tools/orchestrator/**`
- `.github/workflows/**`
- `AGENTS.md`, `CLAUDE.md`, `AI/README.md`, `Docs/AI_DEVELOPMENT_WORKFLOW.md`, `Docs/WORKFLOW/IMPLEMENT_TASK.md`, `Docs/WORKFLOW/REVIEW_TASK.md`, `Docs/CHANGELOG.md`

---
## [Unreleased]

### Added

#### S1-7 Filter Device

- ?啣? Device Brand Filter嚗ll + CameraBrand嚗?- ?啣? Connection Filter嚗ll + DeviceConnectionType嚗?- Filter ??Search ?梁??憟??園?蝭拚瘚?
- Search ?寧 Filter 敺銵?- 銝??唳閰?SQLite
- ?啣? BrandOptions ??ConnectionOptions
- ?啣? SelectedBrand?electedConnection
- Clear Search ???身 Search?rand Filter?onnection Filter
- ?∩耨??Repository
- ?∩耨??SQLite Schema
- ?∩耨??Driver Framework
- Build Success嚗? Error / 7 Existing Warnings嚗?
---
## [v0.2] - 2026-06-30

### Added
- S1-8 Connection Test completed.
- Added Connection Test button in Device Center.
- Connected Device Center to existing DriverFactory workflow.
- Test now calls IDeviceDriver.TestConnection(Camera).
- Displays Connection Success / Connection Failed / Driver not implemented.

---
## [v0.2] - 2026-06-30

### Added
- S1-9 Device Validation completed.
- Added validation before calling DeviceService.
- Required field validation for Name, IP Address, Username and Connection Type.
- Added IPv4 validation.
- Added HTTP / SDK / RTSP Port range validation.
- Added RTSP URL validation for RTSP devices.
- Save is blocked when validation fails.

----
## [Unreleased]

### Added
- S1-10 Realtime Validation
  - Added realtime validation for Add/Edit Device dialog.
  - Save button is enabled only when all required fields are valid.
  - Validation messages are displayed below each invalid field.
  - Invalid controls are highlighted immediately while typing.
  - Existing S1-9 final validation before save is retained.

  ---
  ## Unreleased

### Added

- Task-111A Import Framework
  - 撱箇? ImportService
  - 撱箇? IImportParser
  - 撱箇? ImportRow
  - 撱箇? ImportResult
  - 撱箇? ImportWizard Skeleton

- Task-111B CSV Parser
  - ?啣? CsvImportParser
  - ?舀 UTF8 / Big5
  - ?舀 quoted field
  - ?舀 Header Parsing

- Task-111C Excel Parser
  - ?啣? ExcelImportParser
  - ?∠ ClosedXML
  - ?舀 xlsx
  - 蝚砌???Worksheet
  - Header Parsing

- Task-111D Parser Unit Test
  - Added VSP.Tests
  - Added CsvImportParserTests
  - Added ExcelImportParserTests
  - Added CSV parser tests for supported file types, header parsing, row mapping, quoted field, comma-in-quoted-field, empty row skip
  - Added CSV encoding tests for UTF-8, UTF-8 BOM, UTF-8 without BOM, and Big5
  - Added Excel parser tests for first worksheet parsing, header parsing, row mapping, blank cell handling, and empty row skip
  - No production parser code changed

- Task-111E Validation Engine
  - Added ImportValidationEngine
  - Added ImportValidationResult with original ImportRow reference
  - Added shared ImportValidationMessage model
  - Added ImportValidationSeverity enum
  - Added validation rules for required fields, IPv4, HTTP / RTSP / SDK port range, RTSP URL required, and rtsp:// prefix
  - Added ImportValidationEngineTests
  - No parser, UI, SQLite, Repository, DeviceService, Driver Framework, ImportWizard, or Camera Entity changes

- Task-111F Duplicate Checker
  - Added DuplicateChecker to the Validation layer
  - Reused ImportValidationResult, ImportValidationMessage, and ImportValidationSeverity
  - Added duplicate rules for Name, IP Address, and RTSP URL
  - Duplicate comparison is case-insensitive, trims whitespace, and ignores empty values
  - Duplicate checker appends duplicate error messages and preserves existing validation messages
  - Added ImportDuplicateCheckerTests
  - No parser, UI, SQLite, Repository, DeviceService, Driver Framework, ImportWizard, or Camera Entity changes

- Task-111G Import Pipeline Service
  - Added ImportPipelineService as the single import pipeline entry point
  - Added ImportPipelineResult with Results, TotalRows, ValidRows, and InvalidRows
  - Added a lightweight parser selection helper to isolate parser selection from orchestration
  - Reused CsvImportParser, ExcelImportParser, ImportValidationEngine, and DuplicateChecker
  - Added ImportPipelineServiceTests for CSV, Excel, validation stage, duplicate stage, empty file, unsupported file type, and parser exception reporting
  - No parser, UI, SQLite, Repository, DeviceService, Driver Framework, ImportWizard, MainWindow, or DeviceCenter changes

- Task-112 Import Preview Builder
  - Added ImportPreviewBuilder
  - Added ImportPreviewResult
  - Added ImportPreviewRow
  - ImportPreviewRow remains UI-independent and uses plain data fields only
  - Reused ImportValidationMessage directly without adding a preview-specific message model
  - Added ImportPreviewBuilderTests for empty result, single row, multiple rows, valid row, invalid row, duplicate row, messages mapping, summary count, row order, and null safety
  - No parser, validation engine, duplicate checker, UI, SQLite, or Repository changes

- Task-113 Import Wizard UI
  - Updated ImportWizard to browse import files and display preview results
  - Injected ImportPipelineService and ImportPreviewBuilder into ImportWizardViewModel through constructor parameters
  - Added ImportFileSelector helper to isolate file dialog usage from the ViewModel
  - Added preview summary fields for total, valid, and invalid rows
  - Added preview grid columns for row number, device fields, status, and validation messages
  - Added Refresh support to reload the currently selected file without browsing again
  - Added ImportWizardViewModelTests covering empty preview, preview display, summary counts, refresh behavior, cancel, invalid file type, and exception handling
  - No parser, validation, duplicate, SQLite, Repository, DeviceService, Driver Framework, or MainWindow changes

- Task-114 SQLite Import
  - Added ImportExecutor to orchestrate ImportPreviewResult -> CameraImportMapper -> ICameraRepository -> ImportResult
  - Added CameraImportMapper to map ImportPreviewRow into Camera entities without repository or UI logic
  - Added ImportResult and ImportError models for import execution summary and error collection
  - Reused the existing ICameraRepository abstraction for import execution
  - Updated CameraRepository to delegate to SQLiteCameraRepository for repository-backed imports
  - Connected ImportWizard Import button to execution flow and simple status updates
  - Added ImportExecutorTests for empty import, multiple rows, skipped invalid rows, partial failure, repository exception, and error collection
  - Updated ImportWizardViewModelTests to cover import command enablement and import status
  - No parser, validation engine, duplicate checker, import pipeline service, import preview builder, SQLite schema, driver framework, or DeviceService changes

- Task-115 Import Summary
  - Added ImportSummaryViewModel to display execution ImportResult data and expose a RequestClose event
  - Added ImportSummaryWindow with summary counts, error list, and a Close button
  - Connected ImportWizard to open ImportSummaryWindow after import completion
  - Reused ImportResult and ImportError directly from the execution layer without creating a new summary model
  - Added ImportSummaryViewModelTests for success, partial failure, full failure, empty result, error list, and close command
  - Updated ImportWizardViewModelTests to verify ImportCompleted event is raised
  - Corrected one existing ImportExecutor test case so skipped and failed rows are both exercised
  - No parser, validation, duplicate, pipeline, preview builder, import executor, or repository changes

- Task-201 Camera List
  - Added CameraQueryService to wrap the existing ICameraRepository read flow
  - Added CameraListViewModel and CameraListItemViewModel for read-only camera display
  - Added standalone CameraListView with a read-only DataGrid for Name, IP Address, Brand, Status, and Location
  - Reused the existing repository contract without changing sync CRUD methods
  - Added CameraListViewModelTests for empty repository, multiple cameras, repository exception, and mapping
  - Did not modify MainWindow, Import flow, SQLite schema, or Driver Framework

- Task-202 Camera Management Toolbar
  - Added toolbar layout to CameraListView with Search, Clear, Brand, Status, Refresh, and Add Camera controls
  - Added bottom status bar showing total camera count and the current status message
  - Added placeholder toolbar bindings in CameraListViewModel without introducing search, filter, refresh, or add business logic
  - Preserved the existing Camera List load behavior from Task-201
  - Added ViewModel tests for toolbar skeleton state and placeholder commands
  - Did not modify CameraQueryService, Repository, SQLite, Import flow, or MainWindow

- Task-203 Camera Search
  - Implemented Camera Search in CameraListViewModel using SearchCommand and ClearCommand
  - Search scope is limited to Camera Name and IP Address
  - Clear restores the full list, clears SearchKeyword, and updates total count and status message
  - Search continues to use ICameraRepository.GetAll() through CameraQueryService, followed by LINQ filtering in ViewModel
  - No Repository.Search() or SQLite changes were introduced
  - Added unit tests for name search, IP search, excluded fields, blank keyword restore, and clear behavior

- Task-204 Camera Detail
  - Added read-only CameraDetailWindow and CameraDetailViewModel
  - Camera detail opens by double-clicking a row in CameraListView
  - Reused the already loaded camera data without adding repository query paths
  - Displayed camera fields including ports, credentials, RTSP URL, status, recording, location, and timestamps
  - Masked password display in Camera Detail
  - Added Close button and a disabled Edit button placeholder only
  - No Repository, SQLite, Import flow, MainWindow, or Driver Framework changes
  - Added CameraDetailViewModelTests for field mapping, masking, close flow, and null-safe handling

- Task-205 Camera Filter
  - Implemented Brand and Status filter in CameraListViewModel
  - Search and Filter now share the same ApplyFilters() pipeline
  - Filter scope is limited to Brand (All, Hikvision, Dahua, VIVOTEK) and Status (All, Online, Offline)
  - Clear resets SearchKeyword, SelectedBrand, and SelectedStatus without reloading data
  - Filtering continues to use _allCameras + LINQ without Repository or SQLite changes
  - Added unit tests for brand filter, status filter, composed search/filter, clear reset, and selected item clearing

- Task-206 Camera Edit
  - Added edit mode to CameraDetailViewModel and CameraDetailWindow
  - Editable fields now support validation without Repository or SQLite persistence
  - Read-only fields remain locked in the detail view
  - Added ApplyEditCommand as a validation-only placeholder apply flow
  - PasswordBox code-behind only synchronizes password into the ViewModel
  - Added validation for required Name, IPv4 IP Address, and HTTP / RTSP / SDK port range
  - Added unit tests for edit mode, validation, apply flow, and close behavior

- Task-207 Camera Save Persistence
  - Renamed ApplyEditCommand to SaveCommand and connected Camera Detail save flow to ICameraRepository.Update()
  - Save flow now validates, maps ViewModel data to Camera, calls Repository.Update(), refreshes LastModifyTime, and updates StatusMessage
  - Save success displays "Camera saved successfully." and keeps the detail window open
  - Save failure catches repository exceptions, updates StatusMessage, and avoids crashing
  - Added unit tests for save success, validation blocking, repository exception handling, LastModifyTime refresh, and repository call count
  - Technical Debt: TD-017 Unsaved changes detection before closing the window

- Task-208 Add Camera
  - Reused Camera Detail window for New Mode and added explicit new camera defaults for Brand, ConnectionType, Status, Recording, and ports
  - Connected Add Camera flow to ICameraRepository.Add() with existing validation and repository exception handling
  - Add success now closes the detail window, refreshes Camera List, and reselects the newly added camera when visible
  - Added unit tests for New Mode defaults, add success, add failure, add command event routing, and refresh selection behavior
  - Technical Debt: TD-021 Duplicate camera detection before Add()

- Task-210 Unsaved Changes Detection
  - Added dirty tracking for Camera Detail in both Edit Mode and New Mode
  - Close flow now requests confirmation only when unsaved changes exist, while unchanged forms close immediately
  - Save from unsaved-changes confirmation now closes only after successful persistence; discard closes without saving; cancel keeps the current edits
  - Kept confirmation dialog handling in CameraDetailWindow.xaml.cs so the ViewModel does not call MessageBox directly
  - Added unit tests for dirty state changes, save clearing dirty state, and close flows for save, discard, and cancel
  - Technical Debt: TD-022 Shared confirmation dialog component

- Task-211 Camera Refresh / Reload
  - Added a dedicated refresh reload flow that always re-reads repository data instead of relying on the initial load guard
  - Refresh now preserves SearchKeyword, Brand Filter, Status Filter, and restores SelectedCamera by Camera.Id after ApplyFilters()
  - Refresh success ends with "Camera list refreshed." and refresh failure ends with "Failed to refresh camera list."
  - Refresh failure now keeps the current visible list instead of clearing it unnecessarily
  - Added unit tests for repository reload, preserved search and filters, selection restore and clear behavior, and exception handling
  - Technical Debt: TD-026 Background refresh / auto refresh

- Task-212 Camera Delete
  - Added Delete command to Camera Detail for persisted cameras only, with confirmation handled in the View layer
  - Delete now calls ICameraRepository.Delete(camera.Id), closes Camera Detail on success, and refreshes Camera List using the existing Task-211 refresh flow
  - Delete confirmation remains separate from unsaved-changes handling, so explicit delete does not trigger Save / Discard / Cancel close flow
  - Delete failure keeps the detail window open, preserves current edited values, and updates StatusMessage without crashing
  - Added unit tests for delete confirmation request, delete success, cancel, failure handling, and unsaved-changes interaction
  - Technical Debt: TD-025 Shared confirmation dialog component/service

- Task-301 Driver Registry
  - Added immutable DriverDescriptor for driver metadata and factory delegate registration
  - Added DriverRegistry as an instance-based registry with explicit duplicate rejection for DriverId and DeviceConnectionType
  - Updated DriverFactory to use a default DriverRegistry instance internally while preserving the existing RTSP fallback behavior
  - Added unit tests for descriptor validation, registry registration and lookup, duplicate handling, built-in driver registration, and DriverFactory fallback compatibility

