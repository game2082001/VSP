# Epic-017 Database Backup / Restore Foundation

Status: **Accepted — Frozen (2026-08-06).** Implementation complete (§5/§6), automated suite green, Manual Validation 6/7 executed items Pass with 1 deliberately deferred by Product Owner decision (§11). See §12-14 for the Product Acceptance Report, Final Validation Summary, and Known Limitations. This header was not updated when implementation actually happened during Epic-018's Current-State Analysis window — corrected here as part of Product Acceptance, not a retroactive rewrite of what was decided when.
Feature: Cross-cutting / Data Protection
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

---

# Approval Record

- Follows `Docs/RELEASES/V1.0_READINESS_REVIEW.md` §2.2 and `Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.6, both frozen/approved 2026-08-02. Database Backup/Restore is one of the two remaining V1.0 GA blockers (the other, User/Role, is explicitly out of scope for this Epic per instruction).
- Scope dictated directly by the Product Owner (this conversation, 2026-08-03): SQLite database only, Manual Backup, Manual Restore, user-selected destination/source file, destructive-action confirmation before Restore, validate the backup before replacing the active database, preserve the current database if Restore fails, use Epic-014/015 for all failure paths. No scheduled backup, no cloud backup, no recording-file backup, no settings-file backup, no encryption, no compression beyond what is already trivially available, no database schema change. No User/Role work. No generic backup framework.
- **This document is the Current-State Analysis, Architecture Review, Task Plan, and Risk/Rollback Plan requested by the Product Owner. It is not itself an approval.** Per `AI_OPERATING_SYSTEM.md` §22, an Epic's own definition (Objective, Scope Boundary, Task breakdown, Definition of Done) is Approval Required regardless of how routine the implementation looks — the same gate Epic-015/016 passed through before any code was written.
- **2026-08-03 — direction accepted, all three open questions resolved by explicit Product Owner instruction:**
  1. **Restore completion behavior**: show a success message, tell the user VSP must restart, terminate the application cleanly once the user acknowledges the message. No automatic relaunch in this Epic.
  2. **Active recording behavior**: Backup is allowed while recording is active; Restore is blocked while recording is active, with a clear message telling the user to stop recording first.
  3. **Pre-restore backup**: a timestamped pre-restore copy of the active database (`vsp.pre-restore.yyyyMMdd-HHmmss.db`) is kept after a successful Restore, not deleted automatically. No backup-history management or cleanup logic is built in this Epic.
  Additional requirements given at the same time: exact Backup/pre-restore filename formats (§3.6), the exact Restore validation checklist (§3.7), and the exact nine-step Restore installation flow (§3.4) — all applied below, superseding this document's original recommendations wherever they differed. **This document, as updated, still awaits explicit Product Owner approval of the revised Task Plan before implementation begins** — the three resolved decisions replace the former Open Questions, they do not themselves constitute the go-ahead to write code.

---

# 1. Objective

Give VSP a minimal, working Backup and Restore capability for its one existing SQLite database (`%LocalAppData%\VSP\vsp.db`, per Epic-013): a user can manually create a backup at a destination of their choosing, and manually restore from a backup file of their choosing, with a destructive-action confirmation, pre-replacement validation, and a guarantee that a failed Restore never leaves the application without a usable database. No new product concept beyond "back this one file up" and "replace it with a validated copy of another file" — reusing Epic-014 (Logging) and Epic-015 (Error Handling) for every failure path, exactly as instructed.

---

# 2. Current-State Analysis

Verified directly against the repository (full-file reads, not summarized), current as of 2026-08-03, after Epic-016's acceptance.

### 2.1 The database file and its one access point
`VSP.Infrastructure/Database/DatabaseService.cs` is the single source of the database file's location and connection string, for every caller in the codebase:

```csharp
public class DatabaseService
{
    private static readonly string DefaultDatabaseDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSP");

    public DatabaseService() : this(DefaultDatabaseDirectory) { }
    internal DatabaseService(string databaseDirectory) { ... }   // test seam, added in Epic-015

    public SqliteConnection CreateConnection()
    {
        Directory.CreateDirectory(_databaseDirectory);
        return new SqliteConnection($"Data Source={_databaseFile}");
    }
}
```
`_databaseFile` (`vsp.db`) and `_databaseDirectory` are both `private` — nothing outside `DatabaseService` can currently learn the database's actual path without re-deriving `%LocalAppData%\VSP\vsp.db` independently. No method exists today to answer "where is the live database file" for a caller that needs the raw path rather than a connection (which Backup/Restore does).

### 2.2 Connection lifecycle — confirmed by reading every caller
`grep`-verified: every single consumer of `DatabaseService.CreateConnection()` in the codebase — `SQLiteCameraRepository.Add/GetAll/Update/Delete` (`VSP.Infrastructure/Repositories/`) and `DatabaseInitializer.Initialize()` — follows the identical, exclusive pattern:

```csharp
using var connection = _databaseService.CreateConnection();
connection.Open();
// ... one command, then the method returns
```

The connection is opened, used for exactly one SQL statement (or, for `Initialize()`, one `CREATE TABLE IF NOT EXISTS`), and disposed via `using` before the method returns. **No component in the codebase holds a `SqliteConnection` open across two operations, across a UI event, or for the lifetime of a View/ViewModel.** `SQLiteCameraRepository` itself is instantiated fresh per caller too — `CameraListView`, `CameraDiscoveryView`, `ImportWizard`, `PlaybackView`, `DashboardView` (`VSP.UI/Views/*.xaml.cs`) and `DeviceService`/`CameraRepository` (`VSP.Device`) each construct their own `new CameraRepository()` → `new SQLiteCameraRepository(new DatabaseService())`, not a shared singleton — but this only means multiple short-lived-connection-issuing objects exist concurrently, not that any connection itself is long-lived.

### 2.3 Whether connections remain open during normal application use
**No, by application-level design** — per §2.2, every read/write is a fresh open-execute-close cycle lasting milliseconds. However, one caveat that the actual Restore mechanism must account for (§3.4):

`Microsoft.Data.Sqlite` (v10.0.9, already referenced by `VSP.Infrastructure`) **pools native SQLite connections by default** — the connection string built by `DatabaseService.CreateConnection()` does not set `Pooling=False`, so disposing a `SqliteConnection` returns its underlying native `sqlite3*` handle to a process-wide pool rather than necessarily closing the OS file handle immediately. In practice this means: even though no *managed* connection object is ever held open, the *native* layer can still be holding an open file handle against `vsp.db` at any point after the app has made at least one database call — which is true for essentially the entire session, since `DatabaseInitializer.Initialize()` runs at every startup. This does not block reads (Windows/SQLite's shared-read locking tolerates a file copy fine) but it can block an outright file replace/delete on Windows if not explicitly released first. `Microsoft.Data.Sqlite` exposes `SqliteConnection.ClearAllPools()` (a static, process-wide call, zero new package) for exactly this situation.

### 2.4 No existing backup/restore mechanism
Confirmed by repository-wide search: no file named `*Backup*`/`*Restore*` exists anywhere in `VSP.Infrastructure`, `VSP.Device`, `VSP.UI`, or `VSP.Tests`. This is a from-scratch capability, as already stated in `V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.6 and `V1.0_READINESS_REVIEW.md` §2.2.

### 2.5 Deployment path (Epic-013, unaffected, still current)
`%LocalAppData%\VSP\vsp.db` is the one and only database file location in every build (self-contained XCopy deploy, per Epic-013). No environment-specific or multi-tenant path exists. `%LocalAppData%\VSP\` also already holds `Logs\` (Epic-014) and `recording-settings.json` (Epic-011/016) — the natural, already-established directory for a backup's default save/browse starting point too, though the user chooses the actual destination (per required product behavior).

### 2.6 Established conventions this Epic reuses rather than invents
- **Result-object-not-bare-bool**, per Epic-015 (`DatabaseInitializationResult { Success, Exception }`) — the explicit constraint the Product Owner gave Epic-015 ("no bare `bool`, would discard the original failure") applies identically here.
- **Atomic file replace via same-directory temp file + `File.Move(overwrite: true)`**, per Epic-016 (`SettingsFileStore.Save()`) — already the codebase's own precedent for "never leave a half-written file behind," directly reusable for Restore's file-replacement step (§3.4).
- **Confirmation dialogs stay out of the ViewModel**, injected as a `Func<bool>`/`Func<string,bool>` delegate from the composition root (`MainWindowViewModel`), per Epic-016's `confirmCreateFolder` and the older `CameraDetailWindow` delete-confirmation convention — directly reusable for Restore's destructive-action confirmation.
- **`AppLog` (Epic-014) + log-level discipline (Epic-015)**: `Fatal` for unrecoverable startup-blocking failure, `Error` for an operation that failed and the user's intent was not fulfilled, `Warning` for an expected/routine rejection (e.g. a validation failure, not a system fault) — the same tiering `DatabaseInitializer`/`SQLiteCameraRepository`/`SettingsValidator` already use.
- **Recording-active guard**, per Epic-016 (`SettingsViewModel`'s `isRecordingActive` delegate, sourced from `MainWindowViewModel`'s already-retained `_liveViewViewModel.IsRecording`) — directly reusable, zero new wiring needed to ask "is a recording running right now."
- **Real-dependency testing, no ADO.NET mocking**: `SQLiteCameraRepositoryTests`/`DatabaseInitializerTests` (Epic-015) both use a real temp-directory SQLite file via `DatabaseService`'s `internal` test-seam constructor — the same approach applies directly to Backup/Restore tests.

### 2.7 What the database file actually contains (relevant to §3's security note)
`SQLiteCameraRepository`'s schema (`CameraTable.cs`) stores `Username`/`Password`/`RtspUrl` **in plain text** in the `Camera` table. This is pre-existing, out-of-scope-to-fix behavior (not introduced by this Epic), but it means a backup file is a portable copy of that same plaintext credential data — relevant to §3.10's security note, not a new exposure this Epic creates.

---

# 3. Architecture Review

Answering the twelve questions from the Product Owner's brief, in order.

### 3.1 Current database connection lifecycle
Per §2.2: open → one statement → dispose, every time, for every caller, with no exceptions found anywhere in the codebase. There is no persistent/shared connection object anywhere to coordinate with.

### 3.2 Whether connections remain open during normal application use
No managed connection is ever held open outside a single method call (§2.2), but `Microsoft.Data.Sqlite`'s default connection pooling can keep a native file handle alive between calls (§2.3). **Design consequence:** Backup does not need to contend with anything (SQLite's own Online Backup API tolerates concurrent readers/writers by design, §3.3). Restore, which must replace the live file, must explicitly call `SqliteConnection.ClearAllPools()` immediately before attempting the file-level replace, to avoid a spurious Windows sharing-violation on `File.Move`/`File.Delete` from a still-pooled native handle that no C# code is actually "using."

### 3.3 Safe SQLite backup method — recommendation: the SQLite Backup API, not a raw file copy
Three options were named in the brief. Evaluated:

| Option | Verdict |
|---|---|
| **SQLite Backup API** (`SqliteConnection.BackupDatabase(SqliteConnection destination)`, already exposed by the already-referenced `Microsoft.Data.Sqlite` package — zero new dependency) | **Recommended for the Backup direction.** This is a thin wrapper over SQLite's native Online Backup API, purpose-built for "safely copy a live database that may be mid-write," which is exactly this app's situation (short-lived connections can, in principle, be mid-transaction at the instant Backup is invoked). It produces a transactionally-consistent snapshot without requiring any exclusive lock on the live file, and needs no new package. |
| Raw `File.Copy` | Not recommended for Backup. Works correctly almost all the time given how short-lived this app's connections are, but offers no guarantee against copying a file mid-write, and there is no reason to accept that risk when the Backup API is already available at zero cost. Its simplicity is appealing, but "already existing supported mechanism" per the brief's own framing favors the API that SQLite itself documents as the safe way to do this. |
| Another mechanism (e.g. `VACUUM INTO`) | Also SQL-level and safe, but strictly redundant with the Backup API already available through the referenced driver — no reason to introduce a second technique. Not recommended. |

**Restore is a different direction and does not use the same call.** The destination for Restore is not a currently-open live connection to overwrite in place — it is the on-disk `vsp.db` file itself, which needs to be safely *replaced*, with a guaranteed rollback if anything goes wrong (§3.4). Using the Backup API in the reverse direction (open the candidate file as source, the live database as destination, call `BackupDatabase`) was considered and rejected: the Backup API copies page-by-page into the destination's *existing* database file in place; a failure partway (disk full, process interruption) can leave that file in a partially-overwritten, unusable state, which directly conflicts with "preserve the current database if Restore fails." A same-volume atomic file rename (§3.4, already the codebase's own precedent from Epic-016) gives a strictly stronger guarantee: either the whole file is replaced, or the original is completely untouched — no partial state is possible at the filesystem level.

### 3.4 How Restore can safely replace the active database — Resolved Restore Installation Flow (2026-08-03)

Reuses Epic-016's own atomic-write precedent (`SettingsFileStore.Save()` — temp file in the same directory, then an atomic same-volume rename), extended with the explicit rollback and re-validation steps this scenario additionally needs. This is the exact nine-step flow given by the Product Owner; the layering note below it clarifies which steps are `SettingsViewModel` preconditions versus which are inside `DatabaseRestoreService.Restore()` itself.

1. **Validate the selected backup** (§3.7's full checklist) — before anything about the live database is touched.
2. **Ask for destructive-action confirmation** (`Func<bool> confirmRestore`, injected into `SettingsViewModel`, same pattern as `ConfirmCreateFolder`).
3. **Confirm no recording is active** — reuses the existing `isRecordingActive` delegate already injected into `SettingsViewModel` for Epic-016's Recording Path guard. If a recording is active, Restore is blocked here with a clear message telling the user to stop recording first (§4); nothing past this point is attempted.
4. **Rename the current `vsp.db` → a timestamped pre-restore file**, `vsp.pre-restore.yyyyMMdd-HHmmss.db` (§3.6), in the same directory. Same-volume rename, near-instantaneous. If this rename fails (e.g. a handle is still somehow open even after `ClearAllPools()`, §3.2), the live database is completely untouched — `vsp.db` never moved — and Restore aborts here.
5. **Copy the validated backup to a temp file in the active database directory** (`vsp.db.{guid}.tmp`) — a `File.Copy`, never a move: the user-selected source file itself is never moved or modified (per instruction), only read. Same directory as `vsp.db`, so the install in step 6 is a true same-volume rename, not a cross-volume copy.
6. **Atomically install the temp file as `vsp.db`** (`File.Move`, same-volume, atomic at the filesystem level — the live path is free at this point because step 4 already renamed the original aside).
7. **Validate the newly installed `vsp.db` again** — the same §3.7 checklist (integrity_check + `Camera`-table check), applied to the file that is now actually sitting at the live path. This catches a copy that succeeded at the OS level but produced a file SQLite itself considers broken (e.g. an interrupted disk write) — the one failure mode steps 1-6 alone cannot detect, because step 1 validated the *source*, not the *installed copy*.
8. **On success**: the restart-required message is shown and the application terminates cleanly once the user acknowledges it (§3.5). The pre-restore file from step 4 is **kept, not deleted** (§3.6/§7).
9. **On failure at step 6 or step 7** (the only two steps that can leave something other than the original database at the live path):
   - Remove the failed replacement file at the live path, if present (a partially-moved or freshly-installed-but-invalid `vsp.db`).
   - Rename the pre-restore file from step 4 back to `vsp.db`, restoring the exact pre-Restore state.
   - Log the original exception (or, for a step-7 re-validation failure, the specific validation reason) via `AppLog.Error`.
   - Show a clear failure message to the user.
   - The application database is left usable — this is the actual guarantee the whole flow exists to provide, verified by both a unit test and a manual, real-file-system validation step (§6).

**Layering**: step 1 (`ValidateBackupFile`) and steps 4-7 plus the step-9 rollback (`Install`) all happen inside `DatabaseRestoreService` (`VSP.Infrastructure/Database/`, §5) — pure file/SQLite operations with no dependency on UI or on "is a recording active," which `VSP.Infrastructure` has no way to know about. `Install` re-runs step 1's validation on its own before touching anything (§5) rather than trusting that a prior `ValidateBackupFile` call happened, so the live database can never be replaced by an unvalidated file no matter how a future caller sequences things. Steps 2 and 3 are preconditions `SettingsViewModel` checks **between** `ValidateBackupFile` and `Install`, exactly mirroring how Epic-016's `TryPrepareNewRecordingPath` checks `isRecordingActive()` before touching any file — `DatabaseRestoreService` itself is never given a recording-state dependency, keeping `VSP.Infrastructure` free of any `VSP.UI`/`VSP.Player` reference. Step 8's restart/termination is likewise a `SettingsViewModel`/`App`-level concern, not something `DatabaseRestoreService` triggers itself (a service class terminating the process would be a surprising, hard-to-test side effect).

`SqliteConnection.ClearAllPools()` (§3.2) is called once, immediately before step 4, to release any pooled native handle against the live file before attempting to rename it.

### 3.5 Whether application restart is required after Restore — Resolved (2026-08-03)
**Restart is required and enforced, not merely suggested.** Rationale unchanged from the original recommendation: several ViewModels load camera data into memory once and keep it there (`CameraListViewModel`, `DashboardViewModel`'s aggregation, `LiveViewViewModel`'s currently-loaded camera, discovery/registration state) — none of them re-poll the database on a timer or file-change notification, so swapping the underlying `vsp.db` file out from under a running session would leave every one of those silently stale. Product Owner instruction resolves *how* restart is enforced: after a successful Restore (§3.4 step 8), show a clear success message stating VSP must restart before the restored database can be used; once the user acknowledges that message, terminate the application cleanly (`Environment.Exit(0)`, the same mechanism `App.xaml.cs` already uses for its other clean-termination paths, e.g. `HandleDatabaseInitializationFailure`). **No automatic relaunch is implemented in this Epic** — the user reopens VSP manually, consistent with the app's existing single-process, `Environment.Exit`-only shutdown model (TD-029/030, already accepted debt).

### 3.6 Backup file naming and extension — Resolved (2026-08-03)
- **Backup**: default suggested filename `VSP_Backup_yyyyMMdd_HHmmss.db` (e.g. `VSP_Backup_20260803_143000.db`), extension `.db`. The filename is only a suggestion — the user picks the actual name and destination via the standard `SaveFileDialog`.
- **Pre-restore copy** (§3.4 step 4, resolved decision §10): `vsp.pre-restore.yyyyMMdd-HHmmss.db`, written into the same directory as the live database (`%LocalAppData%\VSP\`), never user-chosen — this file is an internal safety artifact, not a user-facing backup.
- Both are byte-for-byte valid SQLite database files (the Backup API and a validated-file copy both produce one), so keeping the real `.db` extension means either can be opened directly in any standard SQLite tool if a user or support engineer needs to inspect one — no invented custom format.

### 3.7 Restore validation — Resolved checklist (2026-08-03)
Applied to the user-selected source file before any part of the live database is touched (§3.4 step 1), and again to the freshly installed file before Restore is reported as successful (§3.4 step 7):

1. File exists.
2. File is not empty (non-zero length).
3. File is not the currently active `vsp.db` (compared via `DatabaseService.GetDatabaseFilePath()`, §5) — rejects a user accidentally selecting the live database itself as its own "backup source."
4. File opens successfully as a SQLite database in **read-only** mode (`Mode=ReadOnly` in the connection string — never risk creating or altering the candidate file just by inspecting it).
5. `PRAGMA integrity_check;` returns exactly `"ok"`.
6. The required `Camera` table exists (`SELECT name FROM sqlite_master WHERE type='table' AND name='Camera';` returns exactly one row) — confirms the file is shaped like a VSP database, not an arbitrary unrelated SQLite file that happens to pass integrity_check.

Any failure at any of the six checks rejects the file — invalid or non-SQLite files are rejected **before the live database is touched** in every case, including check 3's role in guaranteeing steps 4-9 of §3.4 never run against the live file. Rejection logs `AppLog.Warning` (a rejected file is an expected/routine user-input condition, not a system fault — same tier as Epic-016's write-access validation) with a clear, specific status message shown to the user.

### 3.8 Atomic replacement and rollback behavior
Covered fully in §3.4 — same-volume rename for both the "preserve current" step and the "install new" step, with an explicit rename-back rollback if the install step fails.

### 3.9 UI scope
**Recommendation: extend the existing Settings screen**, not a new navigation item or window. Rationale:
- `SettingsViewModel` already has exactly the delegate-injection shape this needs (`isRecordingActive` is already injected from `MainWindowViewModel`, directly reusable to block Restore during an active recording, §3.4 step 3; the `confirmCreateFolder`-style pattern is directly reusable for Restore's destructive-action confirmation).
- Matches the brief's own instruction: "Do not create a generic backup framework" — a two-button section ("Backup Database…", "Restore Database…") plus a status line, inside the screen that already exists for exactly this kind of administrative, infrequent action, is the smallest possible UI surface.
- No new `NavigationItem`, no new top-level window, no new `MainWindowViewModel` wiring beyond passing two or three more delegates into the `SettingsViewModel` constructor it already builds.
- File pickers (`SaveFileDialog` for Backup, `OpenFileDialog` for Restore) live in `SettingsView.xaml.cs` code-behind, matching `BrowseRecordingPathCommand`'s existing `FolderBrowserDialog` convention — no branching logic worth unit-testing lives there.
- **`SaveFileDialog.OverwritePrompt`** (WPF built-in, default `true`) already satisfies "existing backup files require overwrite confirmation" with zero custom code — worth calling out explicitly since it means one of the four required Backup behaviors needs no new logic at all.

### 3.10 Logging and security behavior
- Every failure path logs via `AppLog`, per Epic-014/015 conventions: `Error` for an I/O failure during Backup or during Restore's file-replacement steps (the user's intent was not fulfilled); `Warning` for a Restore validation rejection (routine/expected, same tier as Epic-016's write-access check) and for the recording-active block; nothing is logged for a user declining the destructive-action confirmation (a decision, not a failure — same convention as Epic-016's decline-create-folder path).
- **Security note (not a new exposure, worth stating explicitly per Epic-015's established practice of naming what must never be logged):** per §2.7, `vsp.db` — and therefore any backup of it — contains plaintext camera credentials in its `Camera` table. This Epic does not parse or display any row-level content, so no log line in this Epic ever touches that data (only file paths, sizes, and exception details are logged, mirroring exactly what Epic-015 already established as safe to log). But the backup **file itself**, once created, is exactly as sensitive as the live database, with no encryption applied (explicitly out of scope, per instruction) — worth the Product Owner being aware that a backup is a portable copy of that same plaintext credential data, not a new risk this Epic introduces but one it does carry forward unchanged.
- No new credential material, token, or connection string is introduced by this Epic; nothing here changes what Epic-015 already classified as safe/unsafe to log.

### 3.11 Automated and manual validation plan
See §6 (Task Plan) for the concrete file-by-file test plan and §6's Sequence/Manual Validation steps.

### 3.12 Out of Scope
See §7 — restates the Product Owner's list exactly, plus items this Architecture Review itself determined should stay out.

---

# 4. Required Product Behavior — compliance check against this design

| Requirement (from the brief) | Satisfied by |
|---|---|
| Backup: user chooses destination | `SaveFileDialog` in `SettingsView.xaml.cs`, unchanged WPF convention |
| Backup: created without corrupting the active database | SQLite Backup API (§3.3) — purpose-built for a live-database-safe copy |
| Backup: existing backup files require overwrite confirmation | `SaveFileDialog.OverwritePrompt` (built-in, §3.9) — no custom code |
| Backup: success/failure clearly shown | `DatabaseBackupResult` surfaced as a `StatusMessage`, same convention as `SettingsViewModel` |
| Restore: user chooses a backup file | `OpenFileDialog` in `SettingsView.xaml.cs` |
| Restore: destructive-action confirmation | Injected `Func<bool> confirmRestore` delegate → `MessageBox.Show(...YesNo...)` in `MainWindowViewModel`, same pattern as `ConfirmCreateFolder` (§3.4 step 2) |
| Restore: blocked while recording is active, clear message | §3.4 step 3, reusing the existing `isRecordingActive` delegate; clear message telling the user to stop recording first |
| Restore: validated before any active database file is replaced | §3.4 step 1 validates the source before step 4 ever touches the live file; §3.7's six checks gate everything |
| Restore: current database preserved until replacement succeeds | §3.4 step 4 (rename-aside, not delete) before steps 5-6 (stage + install) |
| Restore: a failed Restore leaves the current database usable | §3.4 step 9 (remove failed replacement, rename pre-restore file back, log, report failure) |
| Restore: immediate vs. after-restart is clearly determined | §3.5 — restart required; success message, then clean termination on acknowledgement, no auto-relaunch |
| Pre-restore safety copy kept after a successful Restore | §3.4 step 8 / §3.6 — `vsp.pre-restore.yyyyMMdd-HHmmss.db`, kept, not auto-deleted |

---

# 5. Design — New Types

All new types live in `VSP.Infrastructure/Database/`, alongside `DatabaseService`/`DatabaseInitializer` — the layer already responsible for the database file, already referenced by `VSP.UI` (confirmed: `App.xaml.cs` already has `using VSP.Infrastructure.Database;`), so **no new project reference is needed anywhere**.

```csharp
namespace VSP.Infrastructure.Database;

// Additive method on the existing class -- exposes the path Backup/Restore need,
// without exposing _databaseDirectory/_databaseFile as public fields.
public class DatabaseService
{
    // ... existing members unchanged ...
    public string GetDatabaseFilePath();       // full path to vsp.db
    public string GetDatabaseDirectory();       // the directory vsp.db lives in
}

public sealed class DatabaseBackupResult
{
    public bool Success { get; }
    public Exception? Exception { get; }
    public static DatabaseBackupResult Ok();
    public static DatabaseBackupResult Failed(Exception exception);
}

public class DatabaseBackupService
{
    public DatabaseBackupService(DatabaseService databaseService);
    public DatabaseBackupResult Backup(string destinationFilePath);   // SQLite Backup API, §3.3
}

public sealed class DatabaseRestoreResult
{
    public bool Success { get; }
    public string? FailureMessage { get; }     // user-facing reason (validation rejection or I/O failure)
    public Exception? Exception { get; }        // set only for I/O/exception failures, null for a plain validation rejection
    public static DatabaseRestoreResult Ok();
    public static DatabaseRestoreResult ValidationFailed(string message);
    public static DatabaseRestoreResult Failed(string message, Exception exception);
}

public class DatabaseRestoreService
{
    public DatabaseRestoreService(DatabaseService databaseService);

    // Step 1 of §3.4, standalone: the six-point §3.7 checklist against sourceFilePath only --
    // never touches the live database. Lets SettingsViewModel gate its confirmation (step 2)
    // and recording-active check (step 3) on a valid file before anything destructive is even
    // asked about.
    public DatabaseRestoreResult ValidateBackupFile(string sourceFilePath);

    // Steps 4-7 and the step-9 rollback of §3.4: re-validates sourceFilePath itself first
    // (defense in depth -- this method never trusts a prior ValidateBackupFile call as
    // sufficient authorization to touch the live file, so "restore without validation" is
    // impossible even if a future caller skips step 1), stages + atomically installs it,
    // re-validates the newly installed file (step 7), and rolls back to the pre-restore copy
    // on any failure from the install step onward. Never touches recording state or shows any
    // UI -- those are SettingsViewModel's job (§3.4's Layering note), invoked only after
    // steps 2-3 have already passed.
    public DatabaseRestoreResult Install(string sourceFilePath);
}
```

No generic `IBackupService`/`IRestoreService` abstraction, no plugin/strategy pattern, no configuration object beyond the two file paths already required by the operation — matching "do not create a generic backup framework" directly.

`SettingsViewModel` gains:
```csharp
public ICommand BackupCommand { get; }
public ICommand RestoreCommand { get; }
```
via three more injected delegates in its constructor (`Func<string?> chooseBackupDestination`, `Func<string?> chooseRestoreSource`, `Func<bool> confirmRestore`), alongside the `isRecordingActive` delegate it already has. `RestoreCommand`'s handler is the orchestration point for §3.4's full nine steps, in the exact given order: `chooseRestoreSource()` to pick the file, then `DatabaseRestoreService.ValidateBackupFile(...)` (step 1) — a rejection ends the flow immediately with a status message, nothing further is asked of the user; on a valid file, `confirmRestore()` runs (step 2); if confirmed, `isRecordingActive()` is checked (step 3 — block with "Cannot restore the database while a recording is in progress. Stop the current recording and try again." if true, before any file operation on the live database begins); only then does the ViewModel call `DatabaseRestoreService.Install(...)` (steps 4-7/9, §3.4), and on success shows the restart-required message and terminates (step 8, §3.5).

---

# 6. Task Plan

### Files to add
- `VSP.Infrastructure/Database/DatabaseBackupService.cs`
- `VSP.Infrastructure/Database/DatabaseBackupResult.cs`
- `VSP.Infrastructure/Database/DatabaseRestoreService.cs`
- `VSP.Infrastructure/Database/DatabaseRestoreResult.cs`
- `VSP.Tests/Infrastructure/DatabaseBackupServiceTests.cs` — real temp-directory `DatabaseService`, same convention as `DatabaseInitializerTests`/`SQLiteCameraRepositoryTests`: backup produces a valid file with the same row data, named/located exactly as the caller specifies (filename suggestion itself is a UI-layer concern, §3.6, not the service's); backup survives being called while a connection was recently used (no corruption); backup failure (unwritable destination) returns `Failed` with the original exception, nothing thrown.
- `VSP.Tests/Infrastructure/DatabaseRestoreServiceTests.cs` — covering both `ValidateBackupFile` and `Install` against the §3.7 checklist and the §3.4 nine-step flow:
  - `ValidateBackupFile`: accepts a genuine backup; rejects a missing file, an empty file, the live `vsp.db` path itself, a non-SQLite file, a SQLite file with no `Camera` table, and a corrupted-but-openable file (forced `integrity_check` failure) — every rejection asserted to leave the live database file byte-identical to before the call.
  - `Install`: from a valid backup, replaces `vsp.db`, the new data reads correctly afterward, and a `vsp.pre-restore.yyyyMMdd-HHmmss.db` file exists alongside it with the pre-Restore content; the user-selected source file itself is asserted unchanged (never moved/modified) after a successful `Install`; a forced failure at the atomic-install step (e.g. target directory made temporarily read-only) asserts: the pre-restore file is renamed back to `vsp.db`, the original data is still readable, no orphaned temp file remains, and the returned `DatabaseRestoreResult` carries the original exception — the rollback guarantee (§3.4 step 9) proven, not just described; a forced failure at the re-validation step (step 7 — install a file that copies successfully but fails a simulated post-install integrity check) proves the same rollback triggers even when the failure is detected only *after* the file landed at the live path, not just when the copy/move itself throws.

### Files to modify
- `VSP.Infrastructure/Database/DatabaseService.cs` — add `GetDatabaseFilePath()`/`GetDatabaseDirectory()` (§5); purely additive, no change to any existing member's signature or behavior.
- `VSP.UI/ViewModels/SettingsViewModel.cs` — add `BackupCommand`/`RestoreCommand`, the three new constructor delegates (§5), and `RestoreCommand`'s orchestration of §3.4's nine steps in order (choose file → `ValidateBackupFile` → confirm → recording check → `Install` → success message + terminate, or a status message on any rejection/failure); existing four-field Save/Cancel flow untouched.
- `VSP.UI/Views/SettingsView.xaml` / `.xaml.cs` — add a "Database Backup / Restore" section (two buttons, one status area, reusing existing `Buttons.xaml` styles — no new control styles needed, same as Epic-016's own form); `.xaml.cs` hosts the actual `SaveFileDialog` (default filename `VSP_Backup_yyyyMMdd_HHmmss.db`, §3.6) and `OpenFileDialog` calls.
- `VSP.UI/ViewModels/MainWindowViewModel.cs` — pass the three new delegates into the existing `SettingsViewModel` construction call; `MessageBox`-based `ConfirmRestore` follows the exact shape of the existing `ConfirmCreateFolder`; the restart-required success dialog and `Environment.Exit(0)` call (§3.5) are triggered from here or from `SettingsViewModel` via one more injected `Action` delegate (`showRestartRequiredAndExit`), keeping the process-termination call out of the ViewModel itself, consistent with how `App.xaml.cs` — not any ViewModel — owns every other `Environment.Exit` call site in the app.
- `VSP.Tests/UI/SettingsViewModelTests.cs` — extended with Backup/Restore command tests using injected fake delegates (no live `Application`/`MessageBox`), matching the existing Save-flow test style.
- `Docs/CHANGELOG.md`, `Docs/03_PRODUCT_ROADMAP.md`, `Directory.Build.props` — updated on acceptance only, held until Product Owner acceptance (Epic-014/015/016 precedent).

### Files explicitly not to touch
- `SQLiteCameraRepository.cs`, `CameraTable.cs`, `DatabaseInitializer.cs` — no schema change, no change to any existing repository/init behavior.
- Any file under `VSP.Player`/recording — no recording-file backup, per instruction.
- `AppSettingsProvider.cs`/`SettingsFileStore.cs`/`recording-settings.json` handling — no settings-file backup, per instruction.
- Any `User`/`Role`/auth-related file — none exist yet; not to be created by this Epic.
- `MainWindow.xaml`, any nav/`NavigationItem` change — no new screen, per §3.9.

### Sequence
1. `DatabaseService.GetDatabaseFilePath()`/`GetDatabaseDirectory()` — smallest, additive, do first.
2. `DatabaseBackupResult` + `DatabaseBackupService` (SQLite Backup API, §3.3) + `DatabaseBackupServiceTests`.
3. `DatabaseRestoreResult` + `DatabaseRestoreService.ValidateBackupFile` (§3.7) + its tests.
4. `DatabaseRestoreService.Install` (§3.4 steps 4-7 + step-9 rollback) + its tests — including the rollback-proof tests, the highest-value tests in this Epic.
5. `SettingsViewModel`'s `BackupCommand`/`RestoreCommand`, the four new constructor delegates (three file/confirm delegates + `showRestartRequiredAndExit`), and the nine-step orchestration (§5/§6 "Files to modify") + `SettingsViewModelTests` extension (fakes only, no file dialogs, no live `MessageBox`, no real `Environment.Exit`).
6. `SettingsView.xaml`/`.xaml.cs` — the actual `SaveFileDialog`/`OpenFileDialog` calls and the new section's markup.
7. `MainWindowViewModel` wiring (`ConfirmRestore`, the two file-picker delegates, the restart/exit delegate, reuse of the existing `isRecordingActive`).
8. Build + full suite.
9. Manual Validation (not unit-testable, same rigor as Epic-014/015/016 — Windows UI Automation against the actual built exe): Backup to a chosen folder (default filename `VSP_Backup_yyyyMMdd_HHmmss.db`), confirm the file opens in a SQLite tool and its `Camera` rows match; Backup over an existing filename triggers the native overwrite prompt; Backup performed while a recording is active succeeds without interrupting the recording; Restore from a valid backup after adding/deleting a camera — confirm the destructive-action confirmation appears, confirm the success message and clean termination occur, relaunch the app manually, confirm the restored data is what's shown, and confirm a `vsp.pre-restore.*.db` file now exists in `%LocalAppData%\VSP\`; Restore with a non-database file selected is rejected with a clear message and the app's existing data is provably unchanged after; Restore attempted while a recording is active is blocked with the stop-recording message before any file is even requested; a forced Restore failure (e.g. destination directory made temporarily read-only mid-test) leaves the app usable with its original data intact, proving the rollback in real conditions, not just in the unit test.
10. `CHANGELOG.md`/`03_PRODUCT_ROADMAP.md`/`Directory.Build.props` updates, held until acceptance; Epic Review.

### Test plan summary
Per-component coverage in the Sequence above. The rollback guarantee (§3.4 step 9) gets both unit tests (step 4) and a manual, real-file-system proof (step 9) — this is the one behavior in the Epic where "the code looks right" is not sufficient on its own, given the explicit requirement that a failed Restore must never leave the app without a usable database.

### Rollback (of this Epic's own changes, if this Epic itself needs to be reverted)
Every new file is additive and isolated to `VSP.Infrastructure/Database/` (four new files) and their tests. `DatabaseService.cs` gets two new methods only — reverting means deleting those two methods, nothing else changes. `SettingsViewModel`/`SettingsView`/`MainWindowViewModel` each get an additive extension to their existing Epic-016 shape — reverting means removing the added commands/delegates/UI section, leaving Epic-016's own shipped behavior completely intact. No impact on any other feature area; no schema change to revert.

---

# 7. Out of Scope

Restating the Product Owner's list exactly, plus what this Architecture Review itself determined belongs outside this Epic:

- Scheduled/automatic backup.
- Cloud backup or any network destination.
- Recording-file (media) backup.
- `recording-settings.json` or any other config-file backup.
- Encryption of the backup file.
- Compression beyond whatever the SQLite Backup API does implicitly (none is added deliberately; no new compression package).
- Any SQLite schema change.
- Any new external NuGet package (`Microsoft.Data.Sqlite` already provides everything needed).
- A generic/reusable backup framework, interface, or plugin point for future backup targets.
- User/Role work of any kind.
- Multiple simultaneous backup "slots," backup history/management UI, or automatic pruning of old backups — Backup/Restore are both single-file, one-shot, user-driven actions only. This explicitly includes the pre-restore safety copies (§3.4 step 8/§3.6): they accumulate, one per Restore performed, with no automatic cleanup, no management UI, and no pruning logic of any kind built in this Epic.
- Importing/merging a backup's data into the current database — Restore is a full replacement, never a merge.
- Any self-relaunch mechanism — resolved at §3.5: after a successful Restore the application terminates cleanly once the user acknowledges the restart-required message, and the user relaunches VSP manually, consistent with the app's existing `Environment.Exit`-only shutdown model (TD-029/030, already accepted debt, not reopened by this Epic).
- Any change to `SQLiteCameraRepository`, `CameraTable`, or `DatabaseInitializer`'s existing behavior.

---

# 8. Risk Ceiling

**MEDIUM.** The large majority of the surface is additive-only: two new methods on an existing class, two new small service classes plus two new small result types (all new files, zero existing behavior touched), and an additive extension to `SettingsViewModel`/`SettingsView`/`MainWindowViewModel` following an already-established delegate-injection pattern — each of these pieces would individually be LOW. The ceiling is set to MEDIUM for two reasons specific to this Epic, not found in Epic-016:
1. **File-system-level replacement of the live application database** (§3.4) is inherently higher-consequence than a settings-file rewrite — a bug here risks the one piece of data (`Camera` records) the whole product depends on, even though the design's explicit goal is to make that risk unreachable via the rename/rollback sequence.
2. **No prior Epic in this codebase has needed to reason about SQLite connection pooling** (§2.3/§3.2) — `SqliteConnection.ClearAllPools()` is a new consideration, not a reuse of an existing pattern, and its correctness is load-bearing for Restore's file-replace step succeeding reliably on Windows.

No database schema change, no public API break to any other existing type, no new external package, no security-model change. Risk is contained by the rollback guarantee (§3.4 step 9) and its dedicated test coverage (§6, step 4) rather than by keeping the change small alone.

---

# 9. Definition of Done (draft — becomes final on Product Owner approval)

1. `DatabaseBackupService.Backup(destination)` produces a byte-valid, integrity-passing copy of the live database at the user-chosen destination via the SQLite Backup API, without interrupting or corrupting the live database — including while a recording is active — verified by a test that performs a backup immediately after a write.
2. `DatabaseRestoreService.ValidateBackupFile(source)` applies the full §3.7 checklist before anything about the live database is touched; on rejection, the live database is provably unchanged (asserted by content, not just file existence).
3. A successful Restore (`Install`) atomically replaces `vsp.db` per the exact nine-step flow in §3.4, including the step-7 post-install re-validation.
4. After a successful Restore, the application shows a success message stating VSP must restart, and terminates cleanly (`Environment.Exit(0)`) once the user acknowledges it — no automatic relaunch is implemented (§3.5).
5. A Restore that fails at the install or post-install-validation step leaves the original database renamed back into place and fully readable — proven by both unit tests and a manual, real-file-system validation step (§6, step 9).
6. `SaveFileDialog`'s native overwrite prompt covers "existing backup files require overwrite confirmation" with no custom code.
7. Restore requires an explicit destructive-action confirmation (`Func<bool> confirmRestore`) after a valid file is selected and before the recording-active check or any live-database file operation.
8. Restore is blocked while a recording is active, with a clear message telling the user to stop recording first; Backup is never blocked by recording state.
9. A timestamped pre-restore copy (`vsp.pre-restore.yyyyMMdd-HHmmss.db`) exists after every successful Restore and is not automatically deleted; no backup-history management or cleanup logic exists anywhere in this Epic's code.
10. The user-selected backup source file is never moved or modified by either Backup or Restore.
11. Every failure path logs via `AppLog` at the level specified in §3.10; no camera credential data or full row content is ever logged; the plaintext-credential note in §3.10 is acknowledged by the Product Owner, not silently carried forward.
12. Full existing suite remains green; new tests added per §6 pass; build stays passing with no new warnings.
13. No SQLite schema change; no new external package; no change to any file outside the list in §6.

---

# 10. Resolved Decisions (2026-08-03)

The three points this Architecture Review originally flagged as Open Questions were resolved by explicit Product Owner instruction, alongside the additional filename/validation/flow requirements given at the same time. Recorded here for a single point of reference; the authoritative detail for each lives at the section noted.

| Decision | Resolution | Detail |
|---|---|---|
| Restore completion behavior | Success message → user acknowledges → clean termination (`Environment.Exit(0)`). No automatic relaunch in this Epic. | §3.4 step 8, §3.5 |
| Active-recording behavior | Backup allowed during an active recording. Restore blocked during an active recording, with a clear stop-recording message. | §3.4 step 3, §4 |
| Pre-restore safety copy | Kept after a successful Restore, `vsp.pre-restore.yyyyMMdd-HHmmss.db`, not auto-deleted. No history management or cleanup logic. | §3.4 step 8, §3.6, §7 |
| Backup filename | `VSP_Backup_yyyyMMdd_HHmmss.db`, `.db` extension | §3.6 |
| Restore validation checklist | Exists, non-empty, not-the-live-file, opens read-only, `integrity_check` = `ok`, `Camera` table present — applied to the source before install and to the installed file after | §3.7, §3.4 steps 1 & 7 |
| Restore installation flow | The exact nine-step validate → confirm → recording-check → rename-aside → stage → install → re-validate → success/terminate → rollback-on-failure sequence | §3.4 |
| Source file handling | Backup/Restore never move or modify the user-selected source file — only read (Backup: read live DB via the Backup API; Restore: read the chosen backup, copied not moved) | §3.4 step 5, §7 |
| Backup mechanism | SQLite Backup API | §3.3 |
| Restore mechanism | Never a raw overwrite of the active database — always validate first, atomic install, rollback on failure | §3.4 |

No further product-shaping decisions remain open in this document. Implementation still awaits explicit Product Owner approval of this revised Task Plan (§6).

---

# 11. Manual Validation

Performed against the actual built `VSP.UI\bin\Release\net10.0-windows\VSP.UI.exe`, against this machine's real `%LocalAppData%\VSP\vsp.db`. A safety backup of the pre-validation file was taken first (`vsp.db.pre-epic017-validation-20260806-191651.db`, same directory), matching Epic-018's own precedent.

**Execution split** (same convention as Epic-018 §11): the AI Agent performed only non-invasive preparation — fixed a build-breaking bug found during this pass (`Directory.Build.props` contained `--` inside an XML comment, invalid XML, blocking every project's build; corrected the punctuation only), ran a clean (`dotnet clean` + from-scratch `dotnet build -c Release`) rebuild, ran the full automated suite, and performed a startup smoke test (launched the exe, confirmed window title "VSP - Login," confirmed no new ERROR/FATAL log lines, closed via `Process.CloseMainWindow()` — no simulated clicks or keystrokes). The interactive click-through below was performed by the Product Owner directly, per the same instruction as Epic-018 not to drive the real desktop with UI Automation beyond application startup.

### 11.1 Validation script

Reusing §6 step 9's plan exactly, itemized for execution. One correction made against that section's own prose before handing it off: §6 step 9 described the recording-active Restore block as happening "before any file is even requested" — this does not match the actual, approved flow (§3.4/§5: choose file → validate → confirm → *then* check recording) or the shipped code. Item 6 below is written to the actual §3.4/§5 order; the discrepancy is noted here rather than silently corrected in §6 itself.

1. Backup to a chosen folder (default filename `VSP_Backup_yyyyMMdd_HHmmss.db`) — confirm the file opens in a SQLite tool and its `Camera` rows match the live database.
2. Backup over an existing filename — confirm the native (`SaveFileDialog`) overwrite prompt appears.
3. Backup performed while a recording is active — succeeds without interrupting the recording.
4. Add or delete a camera, then Restore from an earlier valid backup — confirm the destructive-action confirmation appears; confirm the success message and clean termination occur; relaunch manually; confirm the restored data matches the pre-edit state; confirm a `vsp.pre-restore.yyyyMMdd-HHmmss.db` now exists in `%LocalAppData%\VSP\`.
5. Restore with a non-database file selected — rejected with a clear message; confirm the app's existing data is unchanged after.
6. Restore attempted while a recording is active (after choosing a valid file and confirming) — blocked with the stop-recording message before `Install` runs.
7. Force a Restore failure (e.g. destination directory made temporarily read-only mid-install) — app remains usable with its original data intact afterward, proving the rollback in real conditions, not just in the unit test.

### 11.2 Results

Executed by the Product Owner, 2026-08-06.

| # | Item | Result | Notes |
|---|---|---|---|
| 1 | Backup to a chosen folder | Pass | |
| 2 | Overwrite confirmation | Pass | |
| 3 | Backup during active recording | Pass | |
| 4 | Restore (destructive confirmation, success message, restart, restored data, pre-restore copy) | Pass | |
| 5 | Invalid file rejection | Pass | |
| 6 | Restore blocked during active recording | Pass | |
| 7 | Forced filesystem failure / real-conditions rollback proof | **Deferred** | Product Owner decision: an extreme failure-path validation judged outside required V1.0 GA acceptance scope. Tracked as a future regression/robustness test, not a blocker for this Epic's acceptance. Not a reported failure — not executed. |

### 11.3 Compensating coverage for the deferred item (§11.2, item 7)

Item 7's specific real-filesystem proof was not executed, by Product Owner decision. The rollback guarantee it would have proven is not entirely unverified, however — two of `DatabaseRestoreServiceTests.cs`'s automated tests inject a real forced I/O failure and assert the rollback:
- `Install_WhenCurrentDatabaseCannotBeRenamedAside_ReturnsFailedAndLeavesLiveDatabaseUntouched` — forces step 4 (rename-aside) to fail via an exclusive file lock; asserts the live database is untouched.
- `Install_WhenTempInstallFileCannotBeWritten_RollsBackAndLeavesLiveDatabaseUsable` — forces step 5 (stage-file write) to fail via an exclusive file lock; asserts the original database is restored and remains usable.

Not covered by either automated test or by the deferred manual item: a forced failure specifically at step 7 (post-install re-validation, i.e. the copy/move succeed at the OS level but the installed file itself fails `integrity_check`). This is a real, disclosed gap, not an oversight being hidden — recorded in §14.

---

# 12. Product Acceptance Report

**Scope delivered vs. the approved design (§3-§10)**: SQLite-file-only Backup and Restore via the Settings screen, SQLite Backup API for Backup, the exact nine-step validate/confirm/rename-aside/stage/install/re-validate/rollback flow for Restore (§3.4), the exact §3.7 six-point validation checklist, the exact filename conventions (§3.6), Restore blocked during active recording, Backup never blocked, a kept (not auto-deleted) timestamped pre-restore safety copy, restart-required-then-clean-termination on a successful Restore, and no schema change, no new external package, no encryption/compression, no scheduled/cloud/media/settings-file backup, no generic backup framework — every item matches an explicit Product Owner decision (§10), not an AI assumption. Implementation matches the approved design file-for-file (verified by direct reading of every changed file, not summarized).

**Verification performed**:
- Automated: 758/758 effectively passing on a clean (`dotnet clean` + from-scratch `dotnet build -c Release`) rebuild — no incremental-build ambiguity. The one full-suite failure (`RtspMediaSessionIntegrationTests.OpenAsync_AgainstRealFfmpegEncodedStream_ReceivesAndDecodesRealFrames`) is the same pre-existing FFmpeg/RTSP timing flake already documented in Epic-018 §10, unrelated to this Epic's code; reran isolated and passed (57ms). All 26 Database Backup/Restore tests pass, including the two forced-I/O-failure rollback tests (§11.3).
- Manual: 6 of 7 items in §11.1's validation script executed by the Product Owner against the real built `VSP.UI.exe` and real `vsp.db` — **6/6 Pass** on everything executed (§11.2). Item 7 (forced filesystem failure, real-conditions rollback proof) was deliberately deferred by explicit Product Owner decision, not attempted and not failed — reasoning and compensating automated coverage recorded at §11.2-§11.3.

**Disclosed, accepted gaps** (Product-Owner-decided or previously flagged, not silent):
- Manual, real-filesystem proof of the Restore rollback guarantee under a forced destination-read-only condition (§6 step 9 / §11.1 item 7) was not executed. Two automated tests exercise different forced-failure points in the same rollback path (§11.3), but no test — automated or manual — specifically forces a failure at step 7 (post-install re-validation). Deferred by Product Owner decision to a future regression/robustness pass, not blocking this Epic's acceptance.
- Backup files are unencrypted, plaintext-credential-bearing copies of the live database (§3.10, §2.7) — a carried-forward, not newly introduced, exposure; explicitly out of scope per Product Owner direction.
- Every item already itemized in §7 (Out of Scope) remains out of scope and is not repeated here.

**Disposition**: Epic-017 is **Accepted** by the Product Owner as of 2026-08-06 and is now **Frozen** — any future enhancement (scheduled/cloud backup, encryption, backup-history management, the deferred item-7 regression test) is a new Epic or a tracked follow-up test, not a reopening of this Epic; Epic-017 is not reopened except for a confirmed defect.

---

# 13. Final Validation Summary

| Layer | Result |
|---|---|
| Automated suite (clean rebuild) | 758/758 effectively passing (1 pre-existing, unrelated FFmpeg/RTSP timing flake, confirmed passing in isolation) |
| Database Backup/Restore automated tests | 26/26 passing, including 2 forced-I/O-failure rollback tests (§11.3) |
| Manual validation (§11.1, real exe + real `vsp.db`) | 6/6 Pass on executed items; 1 item (forced-filesystem-failure rollback proof) deliberately deferred by Product Owner decision, not failed |
| Defects found during this Epic's implementation or acceptance prep | 0 |
| Build-breaking issue found and fixed during acceptance prep | 1 — invalid XML (`--` inside a comment) in `Directory.Build.props`, unrelated to this Epic's design; punctuation-only fix |
| Production code changes made during this acceptance pass | None |

# 14. Known Limitations (v1.0)

- **No manual, real-filesystem proof of the Restore rollback guarantee.** Deferred by explicit Product Owner decision (§11.2, item 7) as outside required V1.0 GA acceptance scope. Two automated tests prove rollback under two different forced-failure points (§11.3); no test proves rollback specifically when failure is detected only at step 7 (post-install re-validation, after a technically-successful file copy). Tracked as a future regression/robustness test.
- **No way to reach the Operator role's view of Backup/Restore** — Settings, and therefore Backup/Restore, is Admin-only (Epic-018 §3, unaffected by this Epic).
- **Backup files carry the live database's plaintext credential data forward unencrypted** (§3.10) — a carried-forward, not new, exposure; encryption is explicitly out of scope for v1.0.
- Every other out-of-scope item already itemized in §7 (scheduled/cloud backup, encryption, compression, schema change, new package, generic backup framework, backup-history management, multiple pre-restore-copy pruning, import/merge Restore, self-relaunch) remains out of scope and is not repeated here.

---

# Status

**Accepted — Frozen (2026-08-06).** All three original Open Questions and the additional filename/validation/flow requirements were resolved and incorporated (§10). Implementation (§5/§6) matches the approved design file-for-file. Automated suite: 758/758 effectively passing (one pre-existing, unrelated FFmpeg/RTSP timing flake). Manual validation (§11) is complete for 6 of 7 items — 6/6 Pass — with item 7 (forced-filesystem-failure rollback proof) deliberately deferred by explicit Product Owner decision, not a failure, tracked as a future regression/robustness test (§14). See §12-14 for the Product Acceptance Report, Final Validation Summary, and Known Limitations. **This Epic is now frozen — any future enhancement or the deferred item-7 test is a new Epic or a tracked follow-up, not a reopening of this Epic; Epic-017 is not reopened except for a confirmed defect.**
