# Epic-018 User / Role Management Foundation

Status: **Open Questions Resolved by Product Owner (2026-08-04) — see §8. Task Plan (§5/§7) updated accordingly. Not implemented. No production file has been modified to produce this document. Awaiting a separate, explicit Product Owner Approval before implementation begins.**
Feature: Cross-cutting / Security
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

---

# Approval Record

- Follows `Docs/RELEASES/V1.0_READINESS_REVIEW.md` §2.1 and `Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.2. User/Role is the **last** of the two remaining V1.0 GA blockers — Database Backup/Restore (Epic-017) stops at Product Acceptance per instruction; no Epic-017 implementation work continues after this point.
- Scope dictated directly by the Product Owner (this conversation, 2026-08-04): exactly two roles (Admin, Operator), local username/password authentication only (no LDAP/AD/OAuth/JWT/MFA/domain login/SSO), the exact permission lists in §3 below, no enterprise framework, no generic permission engine, no plugin model, no policy engine, no multi-user sessions, no remote/web/API authentication.
- Per `AI_OPERATING_SYSTEM.md` §7/§8: this Epic is HIGH risk on two independent grounds — **Security model change** and **Database schema change** — both explicit Stop Conditions regardless of how small the implementation ends up being. This document is Current-State Analysis + Architecture Review + Task Plan only, exactly as instructed. **No code has been written.**
- Six points below are flagged as **Open Questions (§8)** rather than decided silently — each is a genuine product-shaping ambiguity in the literal requirements as given, discovered during analysis, not a stylistic implementation choice.
- **Resolved 2026-08-04**: the Product Owner has decided all six Open Questions plus stated six Additional Requirements — see §8 for the full record. **Four of the six decisions invert this document's original recommendation** (8.2, 8.3, 8.4, 8.5); the other two (8.1, 8.6) confirm the original recommendation or partially invert it. §3, §4, §5, and §7 below have been rewritten to match the actual decisions, not the original recommendations. This document has not been re-approved for implementation as a whole — only the six Open Questions are resolved; implementation still waits on a separate, explicit Product Owner Approval per `Docs/AI_DEVELOPMENT_WORKFLOW.md`'s Task Plan → Approval → Implementation flow, restated in `CLAUDE.md`.

---

# 1. Objective

Give VSP a minimal Admin/Operator authentication and permission gate: a Login screen in front of the existing `MainWindow`, a `User` table holding username + hashed password + role, and role-based visibility/enablement of the navigation and commands already itemized in §3. No new architecture layer, no configurable permission matrix — reusing every existing convention (`ObservableObject`/`RelayCommand`, the Repository split, `AppLog`/Epic-015 error tiering, `DatabaseInitializer`'s additive-table pattern) exactly as Epic-014 through Epic-017 have.

---

# 2. Current-State Analysis

Verified directly against the repository (full-file reads, not summarized), current as of 2026-08-04, after Epic-017's implementation (Backup/Restore, pending its own acceptance).

### 2.1 App startup flow — no authentication gate exists anywhere
`VSP.UI/App.xaml` declares `StartupUri="Views/MainWindow.xaml"` — plain WPF: the framework instantiates and shows `MainWindow` automatically the instant `App.OnStartup` returns. `App.xaml.cs`'s `OnStartup` (§ previously documented in Epic-014/015/016/017) does, in order: `InitializeLogging()` → load `AppSettings`/apply Theme → wire the three global exception handlers → `DatabaseInitializer.Initialize()` (terminates cleanly on failure) → **returns**. Nothing after database initialization does anything today; `MainWindow`'s appearance is entirely delegated to `StartupUri`. There is no `Program.cs`/custom entry point anywhere in the solution.

### 2.2 MainWindow construction — no session/identity concept
`VSP.UI/Views/MainWindow.xaml.cs` (14 lines, in full):
```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
```
Parameterless. `MainWindowViewModel`'s own constructor (also parameterless) does all object-graph wiring internally — no DI container exists anywhere in the solution (confirmed: no `Microsoft.Extensions.DependencyInjection` or equivalent package referenced by any project). Every dependency in the app is hand-`new`'d, matching the codebase's established convention.

### 2.3 Navigation architecture — no visibility/enablement mechanism exists
`VSP.UI/ViewModels/NavigationItem.cs` (10 lines, in full) is a **plain mutable POCO**, not even `INotifyPropertyChanged`:
```csharp
public class NavigationItem
{
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "";
    public UserControl View { get; set; } = null!;
}
```
`MainWindowViewModel`'s constructor unconditionally `Navigation.Add(...)`s five items in fixed order: Dashboard → Live View → Playback → Devices (`CameraListView`) → Settings (`SettingsView`, now also hosting Epic-017's Backup/Restore section). `MainWindow.xaml`'s `ListBox.ItemTemplate` has **zero `Visibility`/`IsEnabled` binding or style trigger** on the nav-item template. Nothing today can hide or disable a nav item declaratively — this would need to be added, or (simpler, matching the codebase's existing imperative-construction style) achieved by conditionally skipping `Navigation.Add(...)` calls based on role at construction time.

Two dead scaffold files exist and are **not** part of the real navigation path: `VSP.Core/Navigation/INavigationService.cs`/`NavigationService.cs` — `INavigationService` is declared as a `class`, not an `interface`; zero references anywhere else in the codebase. Not to be reused or extended by this Epic.

### 2.4 Where Camera Management/Discovery commands actually live (the Operator-restriction touchpoints)
`VSP.UI/ViewModels/CameraListViewModel.cs` (the live, wired-in "Devices" screen — confirmed via `MainWindowViewModel`) declares these commands, none of which have any permission awareness today:

| Command | CanExecute today | Effect |
|---|---|---|
| `AddCameraCommand` | always | raises `RequestAddCamera` → opens `CameraDetailWindow` in Add mode |
| `ImportCommand` | always | opens the Import Wizard |
| `BatchEditCommand` | `SelectedItemCount >= 2` | opens Batch Edit |
| `BatchConnectionTestCommand` | `SelectedItemCount >= 1` | opens Batch Connection Test |
| `ExportCommand` | `Cameras.Count > 0` | writes a CSV |
| `ShowDiscoveryCommand` / `ShowCameraListCommand` | always | toggles `IsShowingDiscovery` — **this is how Discovery is reached; it is an in-place tab swap inside `CameraListView`, not a separate nav item or window** |
| `ViewLiveCommand` | `SelectedCamera is not null` | raises `RequestViewLive` → `LiveViewCameraCoordinator.CameraSelected` → loads the camera into `LiveViewViewModel` and switches the nav selection to Live View |

`CameraListView.xaml.cs`'s `HandleCameraRowDoubleClick` opens `CameraDetailWindow` in view/edit mode (`CameraDetailViewModel`), which owns `EditCommand`/`SaveCommand`/`DeleteCommand`/`TestConnectionCommand` — also currently permission-unaware.

**Legacy, not a gating surface**: `DeviceCenterViewModel` is explicitly `[Obsolete(...)]`, not wired into `MainWindowViewModel`; `CameraEditorViewModel` has zero references anywhere. Neither needs to change.

### 2.5 A hard functional constraint: Live View has no camera picker of its own
`LiveViewViewModel`'s own doc comment states this explicitly: *"Camera selection always originates from CameraListViewModel via LiveViewCameraCoordinator, never from a picker internal to this ViewModel/view."* `LiveView.xaml`'s empty-state text reads *"Select a camera from the Camera Workspace to begin."* — "Camera Workspace" is the Devices/`CameraListView` screen. **This means Operator cannot be denied all access to the Devices screen** — they need at least read access to the camera list to select one and reach Live View, which they are explicitly permitted. This is the single most important finding shaping §5's Permission Enforcement Strategy and directly informs Open Question 3 (§8).

`LiveViewViewModel` also owns `StartRecordingCommand`/`StopRecordingCommand` (`IsRecording`, `CanExecute` gated only on media state today) — reachable from the same Live View screen Operator is permitted, yet "Recording" is listed as its own distinct Admin permission, separate from "Live View," in the Product Owner's requirements. This is Open Question 1 (§8).

### 2.6 Entity/Repository/Schema conventions a `User` type must follow
- **Domain entity**: `VSP.Domain/Entities/Camera.cs` — plain POCO, auto-properties with inline defaults, no attributes, no base class. Enums live in `VSP.Domain/Enums/`.
- **Schema creation**: `VSP.Infrastructure/SQLite/CameraTable.cs` — a `static class` with one `public static void Create(SqliteConnection connection)` running a raw `CREATE TABLE IF NOT EXISTS` string.
- **`DatabaseInitializer.Initialize()`** (`VSP.Infrastructure/Database/DatabaseInitializer.cs`, unchanged since Epic-015, full body):
  ```csharp
  public DatabaseInitializationResult Initialize()
  {
      try
      {
          using var connection = _databaseService.CreateConnection();
          connection.Open();
          CameraTable.Create(connection);
          return DatabaseInitializationResult.Ok();
      }
      catch (Exception ex) { return DatabaseInitializationResult.Failed(ex); }
  }
  ```
  Only one table today. Adding `User` means one more line inside the same `try`. The class deliberately never logs its own failure — the caller (`App.xaml.cs`) owns the single Error-ID log line (§8's security note carries over unchanged).
- **Two-layer repository split**: `VSP.Infrastructure/Repositories/SQLiteCameraRepository.cs` (raw ADO.NET, `AppLog.Error` + rethrow on failure, per-method `using var connection = _databaseService.CreateConnection()`) is wrapped by `VSP.Device/Repositories/CameraRepository.cs`, which implements `VSP.Device/Interfaces/ICameraRepository.cs` (the interface ViewModels actually depend on) via thin pass-through calls, including a client-side `.FirstOrDefault()` for `GetById` (no dedicated SQL lookup exists for single-row reads anywhere in the codebase today).

### 2.7 Zero existing password-hashing infrastructure — confirmed
Repository-wide grep for `Rfc2898|PBKDF2|SHA256|SHA1|SHA512|HashAlgorithm|Argon2|BCrypt|MD5|Cryptography` found exactly three hits, **none of them app-user password hashing**: `OnvifWsSecurityHeaderBuilder.cs` (SHA-1, protocol-mandated ONVIF WS-Security digest, authenticates *to cameras*) and `RtspAuthorizationHeaderBuilder.cs` (MD5, RFC 2617 RTSP Digest, also authenticates *to cameras*). Confirmed by `V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.2: *"no User/Role/auth implementation exists anywhere in the codebase."* Worth noting as context (not a defect to fix in this Epic): `Camera.Password` is stored **as plaintext** in the `Camera` table — the existing precedent for *camera* credentials is plaintext-at-rest; `User` passwords must not follow that precedent (§6.3).

### 2.8 No crypto or DI package referenced anywhere
Every `.csproj` read in full. `VSP.Infrastructure` → `Microsoft.Data.Sqlite` only. `VSP.UI` → `MaterialDesignColors`/`MaterialDesignThemes` only. No `BCrypt.Net-Next`, no Argon2 package, no DI container package anywhere. Password hashing can be implemented entirely with the .NET BCL (`System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2`, available since .NET 8) — **zero new external package required**, directly satisfying "no enterprise framework."

### 2.9 Product/architecture documentation state
- `Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.2 is the authoritative scope statement (already quoted in the Approval Record).
- `Docs/PRODUCT_CAPABILITY_MATRIX.md` line 96 still marks "User / Role / Permission" as **"Roadmap Version 4.0, planned/unscheduled"** — this is **stale** relative to the Product-Owner-approved `V1.0_CUSTOMER_RELEASE_DEFINITION.md` (2026-08-01) and `03_PRODUCT_ROADMAP.md`, both of which place it in v1.0 GA scope. Worth reconciling as a documentation update at this Epic's acceptance (not a blocker to planning).
- `Docs/PLATFORM_ARCHITECTURE_VISION.md` (the long-range, multi-year platform vision — distinct from the in-scope `00_ARCHITECTURE_VISION.md`) describes a full Management-Server-owned identity/role/session authority model for the eventual distributed product. **None of that applies to v1.0**, which is explicitly a single standalone desktop process — cited here only so it isn't mistaken for present-day guidance.
- `Docs/01_ARCHITECTURE.md` §9 states the Repository Pattern rule in the same terms every prior Epic has followed: Application → Repository → SQLite is the only allowed path; ViewModel → SQLite directly, or UI → Repository bypassing the interface layer, are both against convention. A `User` repository follows the identical `SQLiteUserRepository` (Infrastructure) → `IUserRepository`/`UserRepository` (Device, matching `ICameraRepository`'s existing home) shape.

---

# 3. Required Roles and Permissions (restated exactly as given, for reference throughout)

| Capability | Admin | Operator |
|---|---|---|
| Login | Yes | Yes |
| Dashboard | Yes | Yes |
| Camera Management (Add/Edit/Delete) | Yes | **No** |
| Camera information (view only) | Yes | Yes — **read-only, no editing** (Decision 4, §8.4) |
| Discovery | Yes | **No** |
| Live View | Yes | Yes |
| Playback | Yes | Yes |
| Recording | Yes | **No** — resolved 2026-08-04, Decision 1 (§8.1) |
| Settings | Yes | **No** |
| Backup | Yes | **No** |
| Restore | Yes | **No** |

No custom/configurable roles. No granular per-capability matrix beyond this fixed table. No self-service registration.

**Reconciliation note (Decision 1 vs. §2.5's technical finding)**: Decision 1 states Operator "may use: Dashboard, Live View, Playback. Only." Read literally, this omits the Devices/camera-list screen. But §2.5 established a hard technical constraint that pre-dates and survives this decision: Live View has no camera picker of its own, so Operator must have at least read access to the Devices screen to select a camera before Live View can be used at all. Decision 4 independently confirms Operator gets read-only Camera visibility. Reconciling the two: the Devices screen **remains visible to Operator, in read-only mode**, as necessary supporting infrastructure for the three feature areas Decision 1 actually grants — it is not a fourth independently-controllable feature (no Add/Edit/Delete/Import/Batch/Export/Discovery), consistent with the table above. This is not treated as a new escalation-worthy ambiguity — it is fully determined by combining Decision 1, Decision 4, and the pre-existing §2.5 finding — but is stated explicitly here so the reconciliation isn't silent.

---

# 4. Architecture Review — answering the seventeen analysis questions

### 4.1 Current application startup flow
Covered in §2.1. Summary: `StartupUri` → `MainWindow` shown immediately, no gate of any kind today.

### 4.2 Where Login should occur
In `App.xaml.cs`'s `OnStartup`, **after** `DatabaseInitializer.Initialize()` succeeds (the `User` table must exist and the default Admin must be seeded before Login can query it) and **before** `MainWindow` is ever constructed. Concretely: remove `StartupUri` from `App.xaml`, and after the existing database-init success path, construct and `ShowDialog()` a new `LoginWindow`/`LoginViewModel`. Only on successful authentication does `OnStartup` construct and `Show()` a `MainWindow`, passing the authenticated identity in.

### 4.3 Whether MainWindow should be hidden before authentication
**Yes, and this is achievable cleanly**: because `StartupUri` is the *only* mechanism showing `MainWindow` today (§2.1/§2.2), removing it and gating `new MainWindow(...)` behind a successful `LoginWindow.ShowDialog() == true` means `MainWindow` (and everything it constructs — `CameraListView`, `SettingsView`, every repository, `DatabaseBackupService`/`DatabaseRestoreService`, etc.) is **never instantiated at all** until authentication succeeds, not merely hidden. This is stronger than "hidden," and costs nothing extra — it falls out naturally from how the current code happens to be structured (no eager construction of `MainWindow` exists to suppress).

### 4.4 Current navigation architecture
Covered in §2.3. Summary: `ObservableCollection<NavigationItem>` + unconditional construction, zero visibility mechanism — must be added or worked around via conditional construction.

### 4.5 Permission enforcement strategy — recommended, in two parts
Given §2.5's hard constraint (Operator needs read access to the Camera Workspace to reach Live View), a pure "hide the whole Devices nav item" strategy does not work. Recommended split, deliberately avoiding any generic permission-engine abstraction:

**Part A — Nav-item-level gating** (covers Settings/Backup/Restore entirely, since nothing there is reachable by Operator and nothing else depends on that screen being visible): `MainWindowViewModel`'s constructor takes the authenticated `Role`; the `Settings` nav item is added only `if (role == Role.Admin)`. Four lines changed, no new abstraction.

**Part B — Command-level `CanExecute` gating, confined to `CameraListViewModel`** (covers Camera Management + Discovery, the one screen Operator partially sees): extend the existing `CanExecute` predicates on `AddCameraCommand`, `ImportCommand`, `BatchEditCommand`, `BatchConnectionTestCommand`, `ExportCommand`, `ShowDiscoveryCommand` with `&& _role == Role.Admin` (or equivalent), matching the exact existing idiom (`new RelayCommand(Method, () => existingPredicate && roleAllows)`) already used throughout the codebase — no new command type, no new base class. `ViewLiveCommand` is deliberately **not** gated — Operator keeps it.

**Resolved 2026-08-04 (Decision 4, §8.4)**: double-click-to-open-`CameraDetailWindow` is **allowed** for Operator, opened in a **read-only mode** — this inverts the original recommendation (block entirely). Concretely: `CameraDetailViewModel` gains a read-only/view-only mode driven by role — `EditCommand`/`SaveCommand`/`DeleteCommand` all gated `&& _role == Role.Admin`; all bound fields on `CameraDetailView.xaml` become non-editable when opened for an Operator (matching the existing convention of gating at `CanExecute` plus a view-level read-only flag, not a second window/ViewModel). This is no longer a zero-change file, and moves from "Files explicitly not to touch" to "Files to modify" in §5.

**Implementation note, not escalated as a new Open Question**: `TestConnectionCommand` is not named in Decision 4's "no editing" restriction — it inspects connectivity without modifying stored camera data. Recommendation, low-stakes: leave `TestConnectionCommand` available to Operator in the read-only Detail view, consistent with "view read-only, no editing" rather than "view-only, no interaction." Flag for confirmation during implementation if the Product Owner disagrees.

**Resolved 2026-08-04 (Decision 1, §8.1)**: Recording is Admin-only, confirming the original recommendation. `LiveViewViewModel.StartRecordingCommand`/`StopRecordingCommand` get the `&& _role == Role.Admin` extension unconditionally — this is no longer contingent on an Open Question and is now a fixed line item in §5/§7.

No `IPermissionService`, no attribute-based authorization, no declarative policy file — every gate is a plain boolean check inline at the exact point of use, matching Principle 2 (Simplicity First) and the explicit "no generic permission engine" constraint.

### 4.6 Database schema changes required
One new table, additive only (`CREATE TABLE IF NOT EXISTS`, same as `CameraTable`) — **the `Camera` table is not touched in any way**:
```sql
CREATE TABLE IF NOT EXISTS User
(
    Id                  TEXT PRIMARY KEY,
    Username            TEXT NOT NULL,
    PasswordHash        TEXT NOT NULL,
    PasswordSalt        TEXT NOT NULL,
    PasswordIterations  INTEGER NOT NULL,
    Role                INTEGER NOT NULL,
    MustChangePassword  INTEGER NOT NULL DEFAULT 0,
    CreateTime          TEXT,
    LastModifyTime      TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_User_Username ON User (Username);
```
`PasswordIterations` is stored per-row (not a fixed constant) so a future increase to the PBKDF2 work factor never invalidates already-hashed passwords — the small extra column cost is worth that forward-compatibility, and it's the only column here without a direct `Camera`-table precedent to copy from. `Role` stored as `INTEGER` (a two-value enum, `Admin = 0, Operator = 1`), matching `CameraBrand`/`ConnectionType`/`CameraStatus`'s existing convention exactly. `Username` uniqueness enforced at the schema level via a unique index, not just application-side checking — consistent with treating it as the natural key for login lookup. `MustChangePassword` (`INTEGER` as 0/1, matching SQLite's boolean-as-integer convention used elsewhere in this codebase) added 2026-08-04 to support Decision 5 (§8.5): the seeded default Admin row is inserted with `MustChangePassword = 1`; the flag is cleared to `0` once that user completes the forced password-change flow (§4.18). Any future user would default to `0` (matching the column's `DEFAULT 0`), since only the seeded default Admin is required to change their password in v1.0.

### 4.7 Password storage strategy
**PBKDF2-HMACSHA256** via `System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(...)` — a .NET BCL static method, **zero new NuGet package**. A small new static type (proposed `VSP.Core/Security/PasswordHasher.cs`, matching where other cross-cutting, framework-free utilities live — `Logging/`, `Commands/`, `MVVM/`) exposing `Hash(string password) -> (hash, salt, iterations)` and `Verify(string password, string hash, string salt, int iterations) -> bool`. Salt: 16 bytes, `RandomNumberGenerator.GetBytes(...)`, unique per user, stored alongside the hash (not appended into a single string — three separate columns, per §4.6, for clarity and because it costs nothing). Iteration count: a `PasswordHasher.DefaultIterations` constant (recommend 210,000, OWASP's current PBKDF2-HMAC-SHA256 minimum guidance as of this writing) stored per-row at hash time, never hardcoded into verification. **Never** stores or logs the plaintext password anywhere, at any point — extending Epic-015 §8's "never log credentials" rule, which already exists in this codebase for camera passwords, to the new, actually-sensitive case (a VSP user's own login credential, unlike a camera's, is what stands directly between an attacker and every camera credential already at rest in the `Camera` table).

**Confirmed 2026-08-04**: the Product Owner's Additional Requirements independently specify "PBKDF2 with a unique salt and a modern iteration count" and "no custom cryptography" — both already satisfied exactly as designed above (`Rfc2898DeriveBytes.Pbkdf2`, a per-user `RandomNumberGenerator`-sourced salt, no hand-rolled hashing). No design change required; recorded here as explicit confirmation rather than a silent match.

### 4.8 Default administrator creation
On `DatabaseInitializer.Initialize()`, immediately after `UserTable.Create(connection)`: if `SELECT COUNT(*) FROM User` returns `0`, insert **exactly one** seed row — `Username = "admin"`, `Role = Admin`, a fixed, documented default password (`"admin"`, matching the simplicity bar of this Epic), **`MustChangePassword = 1`**. Idempotent by construction (`IF NOT EXISTS`-style count check, not a version flag) — re-running `Initialize()` against an already-seeded database never inserts a second default row, mirroring `CameraTable.Create`'s own `IF NOT EXISTS` idiom at the row level instead of the table level.

**Resolved 2026-08-04**:
- **Decision 5 (§8.5)**: the default password is not left permanently valid. `MustChangePassword = 1` on the seeded row forces the change flow (§4.18) on first successful login — this inverts the original recommendation ("no forced-change flow").
- **Decision 3 (§8.3)**: **no** default Operator account is seeded — this inverts the original recommendation. `DefaultAdminSeeder` seeds exactly one row, Admin only; it is not renamed to a "default accounts" seeder as the original draft anticipated. See §4.19 for the direct consequence of combining this with Decision 2.

### 4.9 Logout behavior
Recommended: **return to the Login screen without restarting the process.** `MainWindowViewModel` gains a `LogoutRequested` event (or a `LogoutCommand` the composition root subscribes to); when raised, `MainWindow` closes and `App.xaml.cs`'s startup orchestration loops back to showing a fresh `LoginWindow`. This avoids a full process relaunch (heavier, and inconsistent with a desktop app's expected "switch user" responsiveness) while still fully discarding the previous session's in-memory state, since a brand-new `MainWindowViewModel`/`MainWindow` object graph is constructed on the next successful login — no explicit "clear session" step is needed because nothing is reused across the boundary. Where exactly a Logout affordance is placed (a button on `MainWindow`'s title bar? Inside Settings, Admin-only, which Operator can't reach?) is implementation detail, not a product decision, and will be resolved during the Task Plan's own implementation sequencing — but it must be reachable by **both** roles, so it cannot live inside the Admin-only Settings screen.

**Confirmed 2026-08-04**: the Additional Requirements explicitly require "provide a Logout action," matching this section as designed. No change required.

### 4.10 Session lifetime
A session is nothing more than "the currently authenticated `User`/`Role` held in memory for as long as `MainWindow` is open." No timeout, no idle-expiry, no persistence across restarts, no "remember me." Ends on Logout (§4.9) or on the process exiting. This is a deliberate simplicity choice directly matching "no multi-user sessions" and "keep the architecture simple" — flagged, not silently assumed, since an enterprise-minded reviewer might expect idle timeout; no idle timeout is proposed for v1.0.

**Confirmed 2026-08-04**: the Additional Requirements state this exactly — "session lifetime = until Logout or application exit," "require login again after every application restart," and, as a secondary point in §8, "no account lockout in v1.0." All three match this section and §8's secondary points as designed. No change required.

### 4.11 Logging requirements
Extends Epic-014 (`AppLog`)/Epic-015 (tiering) exactly, adding one more line to the existing "never log" list from Epic-015 §8:
- Failed login attempt → `AppLog.Warning($"Login failed for username '{username}'.")` — **never** the attempted password. Routine/expected condition (same tier as a bad Retention Days entry), not a system fault.
- A permission-denied condition reached defensively (should be unreachable in normal use, since the UI itself hides/disables the action — see §4.5) → `AppLog.Warning` if it's ever worth instrumenting; not required, since `CanExecute` already prevents the action from firing in the first place.
- **Never**: plaintext password, password hash, password salt, under any circumstances, in any log line, at any level — this is the one absolute rule carried into this Epic from Epic-015's established practice, now applied to VSP's own credentials rather than only camera credentials.
- **Resolved 2026-08-04 (Decision 6, §8.6)**: successful login **is logged** — `AppLog.Information($"Login succeeded for username '{username}'.")` (or the codebase's equivalent success-tier call) — inverting the original recommendation and Epic-015's usual "no success-event instrumentation" boundary, because the Product Owner treats a login event as security-relevant rather than a routine feature event. The absolute "never log password/hash/salt" rule above applies identically to this new success-path log line — it is exactly as important not to leak credential material on a successful login as on a failed one.
- `DatabaseInitializer` failures (including a new `UserTable.Create`/default-admin-seed failure) are already fully covered by Epic-015's existing Fatal/Error-ID/dialog/`Environment.Exit(1)` path — no new mechanism required, this Epic only adds one more thing that can fail inside the same already-instrumented `try`.

### 4.12 Error handling
Same tiering as every prior Epic: `SQLiteUserRepository`'s CRUD methods log `AppLog.Error` + rethrow, exactly like `SQLiteCameraRepository` (§2.6) — preserving the same "never silently swallow, never convert a failure into an empty/default result" rule. `PasswordHasher.Verify` never throws for a wrong password (that's an expected `false` result, not an exception) but is expected to throw only for a genuinely corrupt/malformed stored hash (should be unreachable outside of direct DB tampering) — such a failure surfaces as a failed login with `AppLog.Error` (not `Warning`, since a malformed stored hash is a data-integrity fault, not routine user error).

### 4.13 Automated tests (planned, not yet written)
- `PasswordHasherTests` — hash-then-verify round-trips true; wrong password verifies false; two hashes of the identical password differ (salt uniqueness); empty/unicode/very long password inputs handled without throwing.
- `SQLiteUserRepositoryTests` — mirrors `SQLiteCameraRepositoryTests`'s exact shape (temp-directory `DatabaseService` test seam): Add/GetByUsername/GetAll/Update/Delete round-trip; duplicate `Username` insert fails cleanly (unique index); failure-path logging asserted via `RecordingLogger`, same convention as every existing Infrastructure test.
- `DatabaseInitializerTests` (extended) — a fresh database seeds exactly one default Admin row; an already-seeded database is not re-seeded on a second `Initialize()` call.
- `LoginViewModelTests` — valid credentials succeed and expose the resulting `User`; wrong username and wrong password both fail with the **identical** generic message (no username-enumeration signal — a deliberate, low-cost good practice, not scope creep, since it costs nothing beyond phrasing one string identically for both cases); blank fields fail without hitting the repository at all; failed-attempt logging asserted the same way every other failure-path test in this codebase already does; **successful-attempt logging also asserted** (Decision 6, §8.6/§4.11 — updated 2026-08-04, inverts the original "not logged" assumption); no test asserts a password or hash value ever appears in a logged message (positive assertion that the "never log" rule holds for both paths).
- `ForcedPasswordChangeViewModelTests` (new, added 2026-08-04 for Decision 5, §8.5) — correct current password + valid new password succeeds and clears `MustChangePassword`; wrong current password fails and leaves `MustChangePassword` set; new password confirmation mismatch fails without touching the repository; the flow cannot be bypassed/cancelled into a completed state (no "skip" path).
- `MainWindowViewModelTests` (new — none exist today) — constructing with `Role.Admin` produces all five nav items including Settings; constructing with `Role.Operator` omits Settings; both roles produce a working Dashboard/Live View/Playback/Devices set.
- `CameraListViewModelTests` (extended) — with an Operator role injected, `AddCameraCommand`/`ImportCommand`/`BatchEditCommand`/`BatchConnectionTestCommand`/`ExportCommand`/`ShowDiscoveryCommand` all report `CanExecute() == false` regardless of selection state; `ViewLiveCommand` is unaffected by role, still governed purely by `SelectedCamera is not null`; **double-click-to-open-Detail is now permitted for Operator** (Decision 4 — updated 2026-08-04, inverts the original "blocked entirely" assumption), opening in read-only mode; with an Admin role, all commands behave exactly as they do today (a regression guard, not a new behavior).
- `CameraDetailViewModelTests` (new, added 2026-08-04 for Decision 4, §8.4) — with an Operator role injected, `EditCommand`/`SaveCommand`/`DeleteCommand` all report `CanExecute() == false`; `TestConnectionCommand` remains available (§4.5's implementation note); with an Admin role, all commands behave exactly as they do today (regression guard).
- `LiveViewViewModelTests` (extended) — `StartRecordingCommand`/`StopRecordingCommand` report `CanExecute() == false` for Operator regardless of media state. Updated 2026-08-04: unconditional per Decision 1 (§8.1), no longer contingent on an Open Question.

### 4.14 Manual validation plan (performed against the actual built exe, same rigor as every prior Epic)
1. Fresh/migrated `vsp.db` (no `User` table yet) → launch → confirm the `User` table is created and exactly one Admin row (`admin`, `MustChangePassword = 1`) is seeded — **no Operator row** (Decision 3) — confirm Login screen appears before any `MainWindow` content is ever visible.
2. Log in as `admin`/the default password → confirm the mandatory Forced Password Change screen appears **before** `MainWindow` (Decision 5, §4.18) and cannot be skipped/cancelled into `MainWindow`; complete it with a new password → confirm `MustChangePassword` is cleared and `MainWindow` now appears with all five nav items present, confirm Add/Edit/Delete Camera, Discovery, Settings, Backup, Restore all fully functional (regression check against Epic-016/017's own shipped behavior); confirm the successful login was logged (Decision 6) with no password/hash/salt in the log line.
3. Log out and log back in as `admin` with the **new** password → confirm the Forced Password Change screen does **not** reappear (flag already cleared) and `MainWindow` opens directly.
4. Wrong username, wrong password, blank fields → each shows the identical generic rejection message, login screen remains, no `MainWindow` is ever constructed; confirm each failed attempt was logged (Decision 6) with no password/hash/salt in the log line.
5. Log out from an Admin session → confirm return to Login, confirm no residual state (e.g. previously selected camera, previous nav selection) carries into the next login.
6. Since v1.0 has no in-app way to create an Operator account (§4.19 — Decisions 2 & 3 combined), seed one Operator row directly in the database for this test only → log in as Operator → confirm exactly the nav items resolved in §4.5 appear (no Forced Password Change screen — only the seeded Admin has `MustChangePassword = 1`), confirm the Devices screen shows a read-only camera list with `Add`/`Import`/`Batch Edit`/`Batch Connection Test`/`Export`/`Discovery` all absent or disabled, confirm double-clicking a camera opens Camera Detail in **read-only mode** (Decision 4 — fields non-editable, Edit/Save/Delete unavailable), confirm selecting a camera and reaching Live View works, confirm Playback/Dashboard fully functional, confirm Settings nav item is entirely absent, confirm Start/Stop Recording is disabled/hidden in Live View (Decision 1).
7. Restart the app after an Operator session → confirm Login is required again (no persisted/auto-login session, per §4.10).

### 4.15 Migration strategy for existing `vsp.db`
No formal migration/versioning framework exists anywhere in this codebase today (confirmed — `CameraTable.Create` has always been the only "migration," via `IF NOT EXISTS`), so this Epic does not introduce one either, per Simplicity First. `UserTable.Create(connection)` added as one more `IF NOT EXISTS` statement inside `DatabaseInitializer.Initialize()`'s existing `try` — an existing, already-deployed `vsp.db` (with only a populated `Camera` table) gets the `User` table created and seeded with the default Admin on its very next launch, with zero data loss and zero action required from the deployer. This is the exact same "just add the table" strategy the codebase has used at every prior schema-touching Epic; no dedicated `UserTable`-migration test beyond what §4.13 already covers is needed.

### 4.16 Rollback strategy
Every change is additive: reverting means removing `UserTable.Create(...)` and the seeding call from `DatabaseInitializer.Initialize()`, reverting `App.xaml`'s `StartupUri` and `App.xaml.cs`'s `OnStartup` to their pre-Epic-018 shape, reverting `MainWindow`/`MainWindowViewModel`'s constructors to parameterless, and deleting the new files listed in §7. An already-migrated database's now-unused `User` table is harmless left in place (no `DROP TABLE` needed or attempted) — the same "an unused extra table/column is acceptable collateral of a reverted Epic" reasoning already applied to Epic-016's scaffolded-but-unpopulated folders.

### 4.17 Out of Scope
See §9.

### 4.18 Forced password-change flow (new, 2026-08-04 — Decision 5, §8.5)
Not part of the original draft; added to implement Decision 5 ("the default administrator must be required to change the password after the first successful login... do not leave the default password permanently valid"), together with Decision 2's constraint that **no general/discretionary Change Password entry point exists in v1.0** ("No User Management UI in v1.0. Only Login is implemented."). Reconciling both: this is a **mandatory, one-time, login-triggered** screen — not a menu item, not reachable at will, not a general "Account Settings" feature.

Flow: `LoginViewModel` authenticates successfully → the resulting `User.MustChangePassword` is checked in `App.xaml.cs`'s orchestration (§4.2) **before** `MainWindow` is ever constructed → if `true`, show a new mandatory `ForcedPasswordChangeWindow`/`ForcedPasswordChangeViewModel` (new/current password + confirm-new-password fields, reusing `PasswordHasher` for both verifying the current password and hashing the new one) → on success, `SQLiteUserRepository` updates that user's `PasswordHash`/`PasswordSalt`/`PasswordIterations`/`MustChangePassword = 0` in one row update → **only then** does `MainWindow` get constructed. This screen cannot be dismissed/cancelled into `MainWindow` — there is no path to the main application with `MustChangePassword` still `true`. It reuses the exact same "no `MainWindow` instantiated until fully authenticated" property already established in §4.3 for Login itself — it is a second, equally-blocking gate, not an optional step.

Since only the seeded default Admin is ever created with `MustChangePassword = 1` (§4.8) and there is no in-app account creation in v1.0 (§4.19), this flow in practice triggers exactly once per fresh deployment — the very first successful `admin` login — and never again for that installation, since the flag is cleared on completion and no later flow can set it back to `true`.

### 4.19 Consequence of combining Decision 2 and Decision 3: Operator is unreachable via normal use in v1.0
Flagged explicitly, not left implicit. Decision 2 rules out any User Management UI (no in-app way to create/edit/delete accounts). Decision 3 rules out seeding a default Operator account. Together, **there is no way to create or reach an Operator account in v1.0 through the application itself** — the Operator role, permission gating (§4.5), and every Operator-specific `CanExecute`/nav-item behavior this Epic implements are all real and fully testable (§4.13's tests inject `Role.Operator` directly, independent of any seeding/UI path), but no end user can actually log in as an Operator without direct database manipulation (manually inserting a `User` row with `Role = 1` and a `PasswordHasher`-produced hash). This is accepted as-is per the Product Owner's explicit decisions — recorded here as a known v1.0 limitation, not a defect, and likely a natural scope item for whatever future Epic eventually adds User Management UI (already noted as out of scope, §9).

---

# 5. Files (Task Plan — updated 2026-08-04 to reflect the Product Owner's actual decisions, §8)

### Files to add
- `VSP.Domain/Entities/User.cs` — plain POCO, matching `Camera.cs`'s shape; includes `MustChangePassword` (§4.6, added for Decision 5).
- `VSP.Domain/Enums/Role.cs` — `public enum Role { Admin, Operator }`.
- `VSP.Core/Security/PasswordHasher.cs` — PBKDF2 hash/verify, §4.7 (confirmed as designed by the Additional Requirements — PBKDF2, unique salt, no custom cryptography).
- `VSP.Infrastructure/SQLite/UserTable.cs` — schema creation including `MustChangePassword`, §4.6.
- `VSP.Infrastructure/Database/DefaultAdminSeeder.cs` — seeds **exactly one** row, Admin only, `MustChangePassword = 1` (Decisions 3 and 5, §8.3/§8.5) — no default Operator row.
- `VSP.Infrastructure/Repositories/SQLiteUserRepository.cs` — raw ADO.NET, mirrors `SQLiteCameraRepository.cs`; includes the single-row update used by the forced password-change flow (§4.18).
- `VSP.Device/Interfaces/IUserRepository.cs`, `VSP.Device/Repositories/UserRepository.cs` — mirrors `ICameraRepository`/`CameraRepository`.
- `VSP.UI/ViewModels/LoginViewModel.cs`, `VSP.UI/Views/LoginWindow.xaml`/`.xaml.cs` — the Login screen (§4.2/§4.3); logs both success and failure (Decision 6, §8.6/§4.11).
- `VSP.UI/ViewModels/ForcedPasswordChangeViewModel.cs`, `VSP.UI/Views/ForcedPasswordChangeWindow.xaml`/`.xaml.cs` — new, mandatory, non-dismissable, login-triggered-only screen (§4.18, Decision 5). **Not** a general/discretionary Change Password feature — Decision 2 explicitly rules that out.
- `VSP.Tests/Infrastructure/Security/PasswordHasherTests.cs`, `VSP.Tests/Infrastructure/SQLiteUserRepositoryTests.cs`, `VSP.Tests/UI/LoginViewModelTests.cs` (extended to assert both success- and failure-path logging, §4.11), `VSP.Tests/UI/MainWindowViewModelTests.cs` (new file — none exists today), `VSP.Tests/UI/ForcedPasswordChangeViewModelTests.cs` (new).

### Files to modify
- `VSP.Infrastructure/Database/DatabaseInitializer.cs` — add `UserTable.Create(connection)` + default-admin-seed call inside the existing `try`.
- `VSP.UI/App.xaml` — remove `StartupUri`.
- `VSP.UI/App.xaml.cs` — orchestrate Login → (if `MustChangePassword`) Forced Password Change → MainWindow (and the Logout loop, §4.9) after successful database init. Both gates block `MainWindow` construction (§4.3, §4.18).
- `VSP.UI/Views/MainWindow.xaml.cs` — constructor takes the authenticated identity/role.
- `VSP.UI/ViewModels/MainWindowViewModel.cs` — constructor takes `Role` (or the full `User`); conditional Settings nav item (§4.5 Part A); `LogoutRequested` event.
- `VSP.UI/Views/MainWindow.xaml` — a Logout affordance reachable by both roles (§4.9, confirmed by the Additional Requirements).
- `VSP.UI/ViewModels/CameraListViewModel.cs` — extend six `CanExecute` predicates (§4.5 Part B).
- `VSP.UI/Views/CameraListView.xaml.cs` — double-click now **allowed** for Operator, opening `CameraDetailWindow` in read-only mode (Decision 4, §8.4/§4.5 — inverts the original "block entirely" recommendation).
- `VSP.UI/ViewModels/CameraDetailViewModel.cs` — **moved from "not to touch" to "to modify"** (Decision 4 inverts the original recommendation): `EditCommand`/`SaveCommand`/`DeleteCommand` gated `&& _role == Role.Admin`; `TestConnectionCommand` left available to Operator per §4.5's implementation note (flag for confirmation if the Product Owner disagrees).
- `VSP.UI/Views/CameraDetailView.xaml`/`.xaml.cs` — bound fields rendered non-editable when opened for an Operator (read-only mode, §4.5).
- `VSP.UI/ViewModels/LiveViewViewModel.cs` — extend `StartRecordingCommand`/`StopRecordingCommand`'s `CanExecute` with `&& _role == Role.Admin`. **No longer conditional** — Decision 1 (§8.1) confirms Recording is Admin-only unconditionally.
- `VSP.Tests/UI/CameraListViewModelTests.cs` — extended per §4.13.
- `VSP.Tests/UI/CameraDetailViewModelTests.cs` — new assertions: Operator role reports `EditCommand`/`SaveCommand`/`DeleteCommand` `CanExecute() == false`; Admin role unaffected (regression guard).
- `VSP.Tests/UI/LiveViewViewModelTests.cs` — extended: `StartRecordingCommand`/`StopRecordingCommand` report `CanExecute() == false` for Operator regardless of media state.
- `Docs/CHANGELOG.md`, `Docs/03_PRODUCT_ROADMAP.md`, `Directory.Build.props`, `Docs/PRODUCT_CAPABILITY_MATRIX.md` (stale row, §2.9) — updated on acceptance only, held until Product Owner acceptance (established Epic-014/015/016/017 precedent).

### Files explicitly not to touch
- `VSP.Infrastructure/SQLite/CameraTable.cs`, `VSP.Infrastructure/Repositories/SQLiteCameraRepository.cs` — no `Camera` schema or behavior change, per explicit instruction.
- `VSP.UI/ViewModels/DashboardViewModel.cs`, `PlaybackViewModel.cs` — fully permitted to both roles as-is, no gating needed.
- `VSP.UI/ViewModels/SettingsViewModel.cs` — Operator never reaches this screen (nav-item-gated), so no internal changes needed there either.
- Any Epic-010 through Epic-017 shipped/frozen file beyond the specific lines named above.
- `DeviceCenterViewModel.cs`, `CameraEditorViewModel.cs` — dead code, not touched by this Epic (a Legacy Cleanup Epic's concern, not this one's).
- No general/discretionary "Account Settings" or "Change Password" menu entry point anywhere in `MainWindow`/`SettingsView` — explicitly ruled out by Decision 2 (§8.2). Only the mandatory, login-gated `ForcedPasswordChangeWindow` (§4.18) exists.
- No `UserManagementView`/`UserListViewModel`/equivalent — explicitly ruled out by Decision 2 (§8.2). See §4.19 for the resulting v1.0 limitation.

---

# 6. Risk Ceiling

**HIGH**, per `AI_OPERATING_SYSTEM.md` §7 — both **Security model change** and **Database schema change** are named HIGH-risk categories independent of implementation size, and this Epic is squarely both. Contributing factors specific to this Epic, beyond the two named categories:
1. **Startup-flow change**: removing `StartupUri` and gating `MainWindow` construction is a structural change to how the entire application boots, touched by zero prior Epic.
2. **`MainWindowViewModel`'s constructor signature changes** — every existing caller (only `MainWindow.xaml.cs` today, per §2.2) must be updated in lockstep; low fan-out, but a public-shape change to the app's composition root.
3. **New credential material at rest for the first time in this product** — a compromised `vsp.db` today exposes camera credentials (already plaintext); after this Epic it additionally gates who can reach the application at all, raising the consequence of `PasswordHasher` or the default-admin-seed logic being wrong.
4. Mitigated by: zero new external package (§2.8), full reuse of every existing convention (result types, repository split, logging tiers, test seams), and a strategy (§4.5) that adds permission checks in exactly two existing files (`MainWindowViewModel`, `CameraListViewModel`) rather than a new cross-cutting mechanism.

No change to `Camera`'s schema or existing behavior; no new external package; no plugin/policy/permission-engine abstraction introduced.

**Updated 2026-08-04**: the resolved Open Questions add two small surfaces beyond the original draft's risk assessment, neither changing the overall HIGH ceiling: (a) a second blocking pre-`MainWindow` gate (§4.18's forced password-change screen), structurally identical in risk shape to the Login gate itself (§6 point 1); (b) `CameraDetailViewModel`/`CameraDetailView` now require a role-aware read-only mode (§4.5), a small addition to an already-shipped, previously Epic-018-untouched file. Both remain within the existing mitigation strategy (§6 point 4) — no new abstraction, same inline-boolean-check pattern.

---

# 7. Task Plan — Sequence (updated 2026-08-04, all six Open Questions now resolved — see §8)

1. `Role` enum, `User` entity (including `MustChangePassword`), `PasswordHasher` + `PasswordHasherTests` — no DB/UI dependency, safest starting point.
2. `UserTable` (including `MustChangePassword`) + `DefaultAdminSeeder` (Admin only, `MustChangePassword = 1`, no Operator row — Decisions 3 & 5) + `DatabaseInitializer` extension + extended `DatabaseInitializerTests`.
3. `SQLiteUserRepository` (including the single-row update used by §4.18's forced-change flow) + `SQLiteUserRepositoryTests`, then `IUserRepository`/`UserRepository`.
4. `LoginViewModel` + `LoginViewModelTests` (fakes for the repository, no live window; assert both success- and failure-path logging per Decision 6, §4.11).
5. `LoginWindow` (the one piece with no branching logic worth unit-testing, per this codebase's established convention — matches `SettingsView.xaml.cs`'s file-dialog precedent).
6. `ForcedPasswordChangeViewModel` + `ForcedPasswordChangeWindow` + `ForcedPasswordChangeViewModelTests` (§4.18, Decision 5) — mandatory, login-triggered only, no menu entry point (Decision 2).
7. `App.xaml`/`App.xaml.cs` orchestration: Login → (if `MustChangePassword`) Forced Password Change → MainWindow, plus the Logout loop (§4.9).
8. `MainWindowViewModel`/`MainWindow` constructor changes + conditional Settings nav item + `MainWindowViewModelTests`.
9. `CameraListViewModel`'s six `CanExecute` extensions + `CameraListView.xaml.cs`'s double-click now **allowed** for Operator (opens read-only Detail, Decision 4) + extended `CameraListViewModelTests`.
10. `CameraDetailViewModel`/`CameraDetailView` read-only mode for Operator (§4.5, Decision 4 — `EditCommand`/`SaveCommand`/`DeleteCommand` gated, fields non-editable, `TestConnectionCommand` left available pending confirmation) + `CameraDetailViewModelTests`.
11. `LiveViewViewModel`'s two `CanExecute` extensions on `StartRecordingCommand`/`StopRecordingCommand` (Decision 1, unconditional — no longer gated on an Open Question) + extended `LiveViewViewModelTests`.
12. Build + full suite; Manual Validation (§4.14, updated per §4.18's new forced-change step); `CHANGELOG.md`/`03_PRODUCT_ROADMAP.md`/`Directory.Build.props`/`PRODUCT_CAPABILITY_MATRIX.md` updates, held until acceptance; Epic Review.

Step 10 in the original draft (a conditional self-service Change Password feature) is **removed** — Decision 2 rules out any discretionary Change Password entry point; only the mandatory flow in step 6 exists.

---

# 8. Open Questions for the Product Owner — RESOLVED 2026-08-04

Six points where the literal requirements as given left a genuine product-shaping ambiguity — flagged rather than decided silently, per the Product Owner Principle (`AI_OPERATING_SYSTEM.md` §22). Each carried a recommendation; the Product Owner has now decided all six, four of which invert the stated recommendation. §3, §4, §5, and §7 above have been updated to match. Implementation still awaits a separate, explicit Product Owner Approval (see Status, bottom of document).

### 8.1 Does Operator's "Live View" permission include Start/Stop Recording? — **DECIDED: No. Confirms the original recommendation.**
"Recording" is listed as its own Admin permission, distinct from "Live View," but Start/Stop Recording controls live inside the same `LiveViewViewModel` screen Operator is explicitly permitted (§2.5/§4.5). **Decision**: "Operator cannot control Recording. Operator may use: Dashboard, Live View, Playback. Only." Recording is Admin-only. Implemented as `LiveViewViewModel.StartRecordingCommand`/`StopRecordingCommand` gated `&& _role == Role.Admin` (§4.5, §5, §7 step 11) — unconditionally, no longer contingent on this question.

### 8.2 Is any user-management UI in scope for this Epic? — **DECIDED: No. Inverts the original recommendation.**
The Admin permission list (§3) does not include "User Management," yet without *some* way to create an Operator account, the Operator role can never actually be exercised outside direct database manipulation — and without a way to change the seeded default Admin password, that password can never be rotated. The original recommendation was a minimal self-service "Change Password" for the logged-in user. **Decision**: "No User Management UI in v1.0. Only Login is implemented." No self-service/discretionary Change Password entry point of any kind ships in v1.0. This is reconciled with Decision 5 (§8.5, forced change after first login) by making the forced-change screen (§4.18) a mandatory, login-triggered, non-dismissable gate rather than a general feature — it satisfies Decision 5's requirement without being "User Management UI" or a menu-reachable "Change Password" feature, honoring Decision 2's letter. Combined with Decision 3 (§8.3), the direct consequence — no way to create an Operator account in v1.0 at all — is recorded as an accepted limitation in §4.19, not silently absorbed.

### 8.3 Should a default Operator account also be seeded, alongside the default Admin? — **DECIDED: No. Inverts the original recommendation.**
Directly follows from 8.2. The original recommendation was to seed one default Operator alongside the default Admin, so the Operator role would be reachable and testable in a fresh deployment. **Decision**: "Seed only one account: Admin. No default Operator account." `DefaultAdminSeeder` seeds exactly one row (§4.8, §5). Combined with Decision 2, this means Operator is not reachable through normal use in v1.0 (§4.19) — the role, its permission gating, and its `CanExecute` behavior are still fully implemented and unit-tested (§4.13 injects `Role.Operator` directly), but manual/exploratory testing of an actual Operator login requires direct database seeding for test purposes only (§4.14 step 6).

### 8.4 Can Operator view Camera Detail read-only, or is the Detail window Admin-only entirely? — **DECIDED: Read-only. Inverts the original recommendation.**
Operator needs to select a camera to reach Live View (§2.5), but opening `CameraDetailWindow` (via double-click) is a separate action from `ViewLiveCommand`. The original recommendation was to block Operator from opening Camera Detail entirely. **Decision**: "Operator may view Camera information in read-only mode. No editing." Implemented as: double-click now permitted for Operator (§5, `CameraListView.xaml.cs`); `CameraDetailViewModel`'s `EditCommand`/`SaveCommand`/`DeleteCommand` gated `&& _role == Role.Admin`; bound fields rendered non-editable for Operator (§4.5, §5, §7 step 10). `TestConnectionCommand` is not "editing" and is left available to Operator per §4.5's implementation note — flagged there as a low-stakes assumption open to correction, not itself escalated as a seventh Open Question.

### 8.5 Default administrator password policy — **DECIDED: forced change on first login. Inverts the original recommendation.**
The original recommendation was a fixed, documented default (`admin`/`admin`) with no forced-change flow, on simplicity grounds. **Decision**: "The default administrator must be required to change the password after the first successful login. Do not leave the default password permanently valid." Implemented as: the seeded Admin row carries `MustChangePassword = 1` (§4.6, §4.8); `App.xaml.cs`'s orchestration inserts a mandatory, non-dismissable Forced Password Change screen between Login and `MainWindow` whenever that flag is set (§4.18); the flag clears on completion and is never set again in v1.0 (no other flow can set it, since Decision 2 rules out general account/password management). This is a real, larger UX addition (a new blocking screen/state, exactly as the original recommendation anticipated as the cost of choosing this path) — reflected in §5's file list and §7's task sequence (new step 6).

### 8.6 Should a successful login be logged? — **DECIDED: Yes. Inverts the original recommendation.**
Epic-015's established precedent says no (no success-path/feature-event logging), and the original recommendation followed that precedent. **Decision**: "Successful and failed login attempts must be logged. Never log passwords or password hashes." Implemented as: both the existing failed-login `AppLog.Warning` and a new successful-login `AppLog.Information`-tier line (§4.11) — the absolute "never log password/hash/salt" rule applies identically to both, and is carried into `LoginViewModelTests` as a positive assertion (§4.13), not just an unstated convention.

### Additional Requirements (stated directly by the Product Owner alongside the six decisions above, 2026-08-04)
These were not open questions in the original draft but are recorded here as part of the same decision record, each cross-referenced to where it's now reflected:
- **Session lifetime = until Logout or application exit; require login again after every application restart.** Confirms §4.10 exactly as originally designed — no change required.
- **Provide a Logout action.** Confirms §4.9 exactly as originally designed — no change required.
- **No account lockout in v1.0.** Confirms the original draft's own secondary recommendation (§4.10/former end of this section) — no change required.
- **Use PBKDF2 with a unique salt and a modern iteration count. No custom cryptography.** Confirms §4.7 exactly as originally designed (`Rfc2898DeriveBytes.Pbkdf2`, per-user random salt, 210,000-iteration OWASP-current default, zero external package) — no change required.

---

# 9. Out of Scope

Restating the Product Owner's explicit constraints, plus what this Architecture Review itself determined belongs outside this Epic:

- LDAP, Active Directory, OAuth, JWT, MFA, Domain Login, SSO — all explicitly named out.
- Any enterprise identity framework, generic permission engine, plugin model, or policy engine.
- Multi-user concurrent sessions; remote authentication; web login; API authentication.
- Custom/configurable roles beyond the fixed Admin/Operator pair; any granular per-capability permission matrix beyond §3's fixed table.
- Self-service registration.
- **User Management UI of any kind — decided 2026-08-04 (§8.2)**: Admin creating/editing/deleting other users, and any discretionary/menu-reachable "Change Password" feature for either role, are both explicitly out of scope for v1.0. The only password-change surface in v1.0 is the mandatory, login-triggered Forced Password Change screen (§4.18), which exists solely to satisfy Decision 5 and is not a general feature.
- **Provisioning any Operator account through the application itself — decided 2026-08-04 (§8.2 + §8.3 combined)**: since neither a default Operator account is seeded nor any account-creation UI exists, there is no way to reach the Operator role in v1.0 except direct database manipulation. Accepted as-is per the Product Owner's explicit decisions; recorded in full in §4.19.
- Password reset/forgot-password flow (no email, no security questions, no recovery codes).
- Account lockout after repeated failed attempts (§8, Additional Requirements — confirmed explicitly, not just by default recommendation).
- Idle session timeout / auto-logout (§4.10, confirmed explicitly by the Additional Requirements).
- "Remember me" / persistent auto-login across app restarts.
- Audit Log (already a distinct, deferred Enterprise capability per `PRODUCT_CAPABILITY_MATRIX.md`) — beyond the `AppLog` success/failure lines now required by Decision 6 (§8.6/§4.11), no durable audit trail is introduced.
- Encryption of the `User` table beyond password hashing — `Username`/`Role`/`MustChangePassword`/timestamps stored in clear, matching the existing plaintext-at-rest posture of every other column in this database (§2.7).
- Any change to `Camera`'s schema, `SQLiteCameraRepository`, or any Epic-010 through Epic-017 shipped/frozen behavior beyond the specific `CanExecute` extensions and the read-only-mode addition to `CameraDetailViewModel` named in §5.
- `DeviceCenterViewModel`/`CameraEditorViewModel` legacy cleanup — unrelated, a future Epic's concern.
- Per-camera or per-feature granular permissions beyond the fixed table in §3 (e.g. "Operator can view Camera A but not Camera B").

---

# 10. Implementation Record (Milestones 18A-18D)

All four milestones approved and implemented, in sequence:
- **18A** (2026-08-04): `Role` enum, `User` entity, `PasswordHasher` (PBKDF2-HMACSHA256, 210,000 iterations, per-user random salt), `UserTable`, `DefaultAdminSeeder` (Admin only, `MustChangePassword = 1`), `SQLiteUserRepository`, `DatabaseInitializer` extension. Unit tests only.
- **18B** (2026-08-04): `LoginViewModel`/`LoginWindow`, `ForcedPasswordChangeViewModel`/`ForcedPasswordChangeWindow`, `IUserRepository`/`UserRepository`, `App.xaml.cs` orchestration (Login → optional Forced Password Change → MainWindow, no earlier construction possible). Generic invalid-credentials message; success/failure login logging with no credential material logged.
- **18C** (2026-08-04): Admin/Operator navigation (conditional Settings nav item), `CameraListViewModel`/`CameraDetailViewModel` role-gated `CanExecute` (read-only Camera Detail for Operator per Decision 4), Logout (`MainWindowViewModel.LogoutCommand`/`LogoutRequested`, `App.xaml.cs` session-recreation loop), password policy foundation, requirement "closing LoginWindow's X exits the app."
- **18D** (2026-08-05): `SessionService` (`VSP.UI/Services/SessionService.cs`) introduced as the single owner of `CurrentUser` — `MainWindowViewModel.CurrentUser` now delegates to it instead of owning the value itself; `App.xaml.cs` constructs one fresh `SessionService` per login. Logout now requires confirmation (`MessageBox`, same idiom as `ConfirmCreateFolder`/`ConfirmRestore` in the same file). Password policy completed: minimum 8 characters, rejects empty/whitespace/username/current password. **Gap found and fixed during 18D acceptance preparation** (not a new feature — completing already-approved Decision 1): `LiveViewViewModel.StartRecordingCommand`/`StopRecordingCommand` had never been role-gated across 18A-18C; both are now Admin-only, matching every other Admin-only command's exact `CanExecute` idiom.

Automated suite: 758/758 passing (one pre-existing, unrelated FFmpeg/RTSP integration test flakes intermittently under full-suite load — confirmed via repeated isolated and full-suite reruns to be timing-related, not a regression from this Epic).

**Known, disclosed testing limitation**: `MainWindowViewModel`'s constructor eagerly builds real WPF `UserControl`s (`LiveView`, `CameraListView`, etc.), which throws `InvalidOperationException` ("the calling thread must be STA") under xunit's default thread-pool test runner — confirmed empirically, not assumed. This codebase has no STA test infrastructure anywhere (verified: no prior test constructs a View), and adding one was judged out of scope for this Epic (would be new test infrastructure, not implementation). Consequently, nav-item visibility (Admin sees 5 items, Operator sees 4) and `SessionService`-driven Logout are verified by code review and by direct, STA-free unit tests of the underlying pieces (`SessionServiceTests`, `CameraListViewModelTests`, `CameraDetailViewModelTests`, `LiveViewViewModelTests`), not by a single end-to-end `MainWindowViewModel` test. §11 (Manual Validation) is what actually exercises the full, real `MainWindowViewModel`/View object graph.

---

# 11. Manual Validation (Milestone 18D)

Performed against the actual built `VSP.UI.exe` (Release configuration), against this developer machine's real `%LocalAppData%\VSP\vsp.db` — the same file a real deployment uses, migrated in place (Epic-018's `User` table added via `IF NOT EXISTS` on first run, per §4.15's migration strategy, matching Epic-013/016's own precedent). A backup of the pre-Epic-018 file was taken first (`vsp.db.pre-epic018-backup-<timestamp>`, same directory) as a safety measure, not because a defect was expected.

**Execution split** (Product Owner decision, 2026-08-05): the AI Agent performed only non-invasive preparation — build, a one-off automated seed of a test Operator account (`operator`/`operator123`, `MustChangePassword = 0`; the shipped `DefaultAdminSeeder` itself is unchanged, still seeding Admin only per Decision 3), and a startup smoke test (launched the exe, confirmed the process ran with window title "VSP - Login," confirmed no new Error/Fatal log lines, closed it via `Process.CloseMainWindow()` — no simulated clicks or keystrokes). The interactive click-through below was performed by the Product Owner directly, not simulated by the Agent, per explicit instruction not to drive the real desktop with UI Automation beyond application startup.

### 11.1 Validation script

Executable: `VSP.UI\bin\Release\net10.0-windows\VSP.UI.exe`. Seeded accounts: `admin`/`admin` (`MustChangePassword = 1`, first-ever login on this migrated file), `operator`/`operator123` (`MustChangePassword = 0`).

1. **Admin Login + forced password change** — launch; confirm "VSP - Login" appears before anything else; log in `admin`/`admin`; confirm the mandatory Change Password screen appears (cannot be bypassed); exercise each rejection (wrong current password, <8 chars, new password = username, new password = current password, mismatched confirmation); complete with a compliant new password; confirm MainWindow then appears with all five nav items (Dashboard/Live View/Playback/Devices/Settings) and the username shown in the title bar.
2. **Settings/Backup/Restore visibility (Admin)** — Settings nav item present and functional; Backup and Restore controls visible inside it.
3. **Camera Management (Admin)** — Devices screen: Add/Import/Batch Edit/Batch Connection Test/Export/Discovery all enabled; Camera Detail opens with Edit/Delete enabled.
4. **Live View + Recording (Admin)** — stream loads; Start/Stop Recording enabled once connected.
5. **Playback (Admin)** — unaffected baseline behavior.
6. **Logout (Admin) — requires confirmation** — Logout button shows a Yes/No confirmation; "No" leaves MainWindow open and logged in; "Yes" closes MainWindow and returns to a fresh "VSP - Login" screen (**session recreation**: no residual nav selection or camera selection carries over).
7. **Operator Login** — log in `operator`/`operator123`; no forced-change screen (flag was seeded false); MainWindow appears with exactly four nav items (Dashboard/Live View/Playback/Devices) — **no Settings**, and therefore no Backup/Restore surface at all.
8. **Camera read-only (Operator)** — Devices screen: Add/Import/Batch Edit/Batch Connection Test/Export/Discovery all disabled; camera row selection works; double-click opens Camera Detail read-only (Edit/Delete disabled, Test Connection enabled, no editable fields).
9. **Live View + Recording restriction (Operator)** — stream loads normally; Start Recording is disabled (the 18D-discovered-and-fixed gap — the single most important new thing this pass verifies).
10. **Playback (Operator)** — unaffected, identical to Admin.
11. **Logout (Operator) + session recreation with a role switch** — confirm dialog, log back in as `admin` using the *new* password from step 1 (confirms the old `admin`/`admin` no longer works and no forced-change screen reappears).
12. **Closing LoginWindow via the title-bar X exits the app** — from the Login screen, close via the window chrome's X (not a Cancel button, since none exists); confirm the process exits completely.

### 11.2 Results

Executed by the Product Owner, 2026-08-05/06.

| # | Item | Result | Notes |
|---|---|---|---|
| 1 | Admin Login + forced password change | Pass | |
| 2 | Settings/Backup/Restore visibility (Admin) | Pass | |
| 3 | Camera Management (Admin) | Pass | |
| 4 | Live View + Recording (Admin) | Pass | |
| 5 | Playback (Admin) | Pass | |
| 6 | Logout confirmation + session recreation (Admin) | Pass | |
| 7 | Operator Login | Pass | |
| 8 | Camera read-only (Operator) | Pass | |
| 9 | Live View + Recording restriction (Operator) | Pass | Initially reported as failed against a stale executable — see §11.3. Confirmed passing against a freshly clean-rebuilt `VSP.UI.exe`: Start Recording and Stop Recording both disabled for `operator`. |
| 10 | Playback (Operator) | Pass | |
| 11 | Logout + re-login as Admin with new password | Pass | |
| 12 | Closing LoginWindow via X exits the app | Pass | |

### 11.3 Step 9 Investigation — stale-executable false positive, not a code defect

First execution of §11.1 reported Step 9 failing: Operator could still click Start Recording. Before touching any production file, the AI Agent traced the entire path end-to-end against the current working tree: `SQLiteUserRepository.ReadUser` (verified directly against the live `vsp.db` — the seeded `operator` row reads back as `Role=1`/Operator) → `LoginViewModel.AuthenticatedUser` → `SessionService.CurrentUser` → `MainWindowViewModel`'s `new LiveViewViewModel(Dispatcher.CurrentDispatcher, currentUser.Role)` → `StartRecordingCommand`/`StopRecordingCommand`'s `CanExecute`, gated `&& role == Role.Admin` (§4.5, Decision 1). `git diff` confirmed the gate is genuinely present in the working tree (not reverted); `grep` confirmed `MainWindowViewModel` is the only production call site that constructs `LiveView`/`LiveViewViewModel`; the existing `OperatorRole_StartAndStopRecordingCommands_ReportCannotExecuteEvenWhenConnected` test already asserts `CanExecute()==false` for Operator. **No logic defect was found in source.**

To rule out a stale/incremental build as the cause, the Agent ran `dotnet clean` (removed all `bin`/`obj`) followed by a from-scratch `dotnet build VSP.UI -c Release` (0 errors) and the full `dotnet test` suite — 758/758 passing, including the Operator-recording regression test — with no production file modified. A stale self-contained `VSP.UI/bin/Release/net10.0-windows/win-x64/VSP.UI.exe` (dated 2026-07-31, predating Epic-018 entirely) was also noted as a possible contamination source, though it could not itself explain the symptom (that build predates the Login screen).

The Product Owner then re-ran Step 9 launching `VSP.UI\bin\Release\net10.0-windows\VSP.UI.exe` explicitly (the freshly rebuilt, documented executable) and confirmed Start Recording and Stop Recording are both disabled for `operator`. **Root cause: the original manual validation pass was run against the wrong/stale executable, not a gap in the role-gating implementation.** No production code was changed as a result of this investigation — consistent with the "no defect, no change" rule this Epic has followed throughout (§10's 18D gap being the one confirmed exception, already fixed prior to this validation pass).

---

# 12. Product Acceptance Report

**Scope delivered vs. Product Owner decisions (§8)**: exactly two roles (Admin, Operator), local username/password authentication, PBKDF2-HMACSHA256 password storage (210,000 iterations, per-user random salt, zero new external package), the fixed permission table in §3, a mandatory login-triggered forced password-change for the seeded default Admin, successful/failed login logging with no credential material ever logged, and a confirmed Logout action. No User Management UI, no default Operator account, no generic permission engine, no LDAP/OAuth/JWT/MFA/SSO — every one of these matches an explicit Product Owner decision (§8), not an AI assumption. All four milestones (18A-18D, §10) were implemented and approved in sequence.

**Verification performed**:
- Automated: 758/758 passing on a clean (`dotnet clean` + from-scratch `dotnet build`) Release build — no incremental-build ambiguity. Coverage includes dedicated Operator-role regression tests for every gated command (`CameraListViewModel`, `CameraDetailViewModel`, `LiveViewViewModel`) and both login-logging paths, per §4.13.
- Manual: all 12 items in §11.1's validation script executed by the Product Owner against the real built `VSP.UI.exe` and real `vsp.db` — **12/12 Pass** (§11.2). One item (Step 9) initially reported as failing was investigated end-to-end (§11.3), found to be a stale-executable false positive rather than a code defect, and confirmed passing on re-test against the documented executable path. No production code was modified during that investigation.

**Disclosed, accepted gaps** (all Product-Owner-decided or previously flagged, not silent): no way to create an Operator account except direct database manipulation in v1.0 (§4.19, direct consequence of Decisions 2+3); no full end-to-end `MainWindowViewModel`/View integration test exists, since this codebase has no STA test infrastructure (§10) — covered instead by manual validation (§11) plus STA-free unit tests of every underlying gated command.

**Disposition**: Epic-018 is **Accepted** by the Product Owner as of 2026-08-06 and is now **Frozen** — any future enhancement (User Management UI, self-service password change, account lockout, idle timeout, etc.) is a new Epic; Epic-018 is not reopened except for a confirmed defect.

---

# 13. Final Validation Summary

| Layer | Result |
|---|---|
| Automated suite (clean rebuild) | 758/758 passing |
| Manual validation (§11.1, real exe + real `vsp.db`) | 12/12 Pass |
| Defects found during Epic-018 implementation | 1 (Recording role-gating never wired, 18A-18C — found and fixed during 18D's own acceptance prep, §10) |
| Defects found during manual validation (§11) | 0 confirmed. 1 reported (Step 9), investigated, root-caused to a stale executable, not a code defect (§11.3) |
| Production code changes since the last Product Owner acceptance checkpoint (18D) | None |

# 14. Known Limitations (v1.0)

- **No way to reach the Operator role through normal use.** No default Operator account is seeded and no account-creation UI exists in v1.0 (Decisions 2 & 3, §8.2/§8.3); an Operator account can only be created via direct database manipulation. Recorded in full at §4.19. Natural scope for a future User Management Epic.
- **No self-service/discretionary Change Password.** The only password-change surface in v1.0 is the mandatory, login-triggered Forced Password Change screen (§4.18), which exists solely to satisfy Decision 5. Rotating the default Admin password later, or any other user's password, requires the same direct-database path as account creation.
- **No end-to-end `MainWindowViewModel`/View automated test.** This codebase has no STA test infrastructure; adding one was judged out of scope for this Epic (§10). Nav-item visibility and session/logout behavior are covered by manual validation (§11) plus STA-free unit tests of the underlying pieces, not by a single automated integration test.
- **No account lockout, no idle session timeout, no "remember me."** All three explicitly out of scope for v1.0 by Product Owner direction (§9, Additional Requirements).
- Every other out-of-scope item already itemized in §9 (LDAP/OAuth/JWT/MFA/SSO, generic permission engine, multi-user concurrent sessions, audit log beyond the two login log lines, etc.) remains out of scope and is not repeated here.

---

# Status

**Accepted — Frozen (2026-08-06).** All six Open Questions (§8) were decided by the Product Owner; §3-§9 reflect the actual decisions. All four milestones (§10) implemented and approved in sequence, including one gap (Recording role-gating, Decision 1) found during 18D's own acceptance preparation and fixed under the existing approved decision — not a new feature, not scope expansion. Manual validation (§11) is complete: 12/12 Pass, including Step 9's investigated-and-resolved stale-executable false positive (§11.3). Full automated suite: 758/758. No User Management UI, permission framework, claims, policies, LDAP, OAuth, or Active Directory were introduced, per every milestone's explicit constraints. See §12-14 for the Product Acceptance Report, Final Validation Summary, and Known Limitations. **This Epic is now frozen — any future enhancement is a new Epic; Epic-018 is not reopened except for a confirmed defect.**
