# Epic-016 Settings Foundation

Status: **Accepted — Frozen** (Product Owner acceptance 2026-08-02)
Feature: Cross-cutting / Configuration
Governed by: `AI/OperatingSystem/AUTONOMOUS_DEVELOPMENT.md` §2 (AI Development Kit v1.1.0)

---

# Approval Record

- Revision 2 accepted by the Product Owner (2026-08-02), subject to two clarifications, both applied in this document before final approval:
  1. **`RecordingPathProvider.cs` may change only its internal implementation.** Its public API — `GetRecordingRoot()`, `GetCameraRecordingDirectory(Guid)`, and both `internal` test-seam overloads — and every observable behavior (default-path resolution, blank/malformed-value fallback, directory creation, exception handling) must remain completely unchanged. This is the explicit, recorded acceptance bar for the flagged edit in §3.1/§5.3: verified by every existing assertion in `RecordingPathProviderTests.cs` passing unmodified, with no new assertions needed to describe new behavior, because none is introduced. This is the sign-off requested at revision 2 for touching a file from frozen Epic-011.
  2. **`SettingsValidator.HasWriteAccess`'s probe-file cleanup runs in a `finally` block, unconditionally.** If cleanup itself fails (the delete throws — e.g. the probe file was removed concurrently, or a transient I/O error), that failure is caught, logged once at `AppLog.Warning`, and swallowed. It must never throw out of `HasWriteAccess`, never surface as an `Error`, and never leave the application in a failed state or block the surrounding Save flow. The method's write-access verdict (`true`/`false`) is determined before cleanup is attempted, so a cleanup failure never changes that verdict. See §7's revised description and §8's revised table.
- No other change to revision 2's design. Approval is contingent on these two points being reflected in the Task Plan, not on any new implementation work — nothing has been coded.

## Product Acceptance (2026-08-02)

1. Approved: Recording Path, Retention Days, Language persistence, Theme Foundation, Manual Validation, Product Validation.
2. Accepted: 674/674 tests. The Manual Validation (§14) identified and corrected one real product defect (§13) before acceptance — per the Product Owner, that increases confidence in the implementation rather than reducing it.
3. Technical debt recorded (not implemented, per Product Owner direction — any future enhancement is a new Epic):
   - **TD-033 — Theme Migration.** The theme-switching mechanism ships with Light/Dark baseline palettes wired only at the `Brushes.xaml`-key level plus `SettingsView.xaml`'s own background/text; the nav sidebar, title bar, status bar, and every other existing View/Style (~23 files) remain hardcoded and do not respond to Theme (§14.5).
   - **TD-034** — Language persists a real, stable selection (`en-US`/`zh-TW`) with zero translated resources behind it, carried forward from §11.
   - **TD-035** — `System` theme is resolved once at startup only, no live reaction to an OS theme change while running, carried forward from §11.
   - **TD-037 — Settings UX improvements.** Unsaved-changes detection, a Restore Defaults action, and similar refinements are not present in this foundation pass.
4. `Directory.Build.props`, `Docs/CHANGELOG.md`, `Docs/03_PRODUCT_ROADMAP.md` updated to reflect acceptance.
5. Commit authorized with message `Epic-016: Settings Foundation` — per `CLAUDE.md`/`Docs/DEVELOPMENT_ROLES.md`, the AI Agent does not run `git add`/`git commit` itself; see the chat response for the exact commands.
6. **Frozen.** Any future enhancement to Settings/Theme is a new Epic. Epic-016 is not to be reopened except for a confirmed defect.

---

# 0. Preconditions and Revision Note

- Milestone M2 Foundation Review — **Accepted**. Milestone M2 Foundation Complete — **Approved**.
- TD-031 (Playback open-failure logging) and TD-032 (`App.xaml.cs` dialog duplication) — recorded, **not implemented in this Epic**.
- Epic-010 through Epic-015 are **frozen** — reopened only for a confirmed defect. See §5.6 for one narrow, flagged exception this revision proposes.
- **The Epic-016 direction (revision 1) was accepted.** This revision applies nine additional constraint sets from the Product Owner, superseding revision 1's corresponding sections in full. Revision 1's three earlier scope decisions (folder-browse button, English + Traditional Chinese, mechanism-only theming) remain in force and are refined below, not reversed.

---

# 1. Objective

Unchanged from revision 1: give VSP a working Settings screen that persists exactly the four v1.0 fields — **Recording Path, Retention Days, Language, Theme** — reusing the config-file-backed seam, zero database schema change, zero new external package. This revision adds explicit rules for validation, active-recording safety, atomic storage, single-source-of-truth architecture, failure-path logging, and Save/Cancel semantics.

---

# 2. Current-State Analysis (additions since revision 1)

Revision 1's findings (§2.1–2.11 of the prior draft) all still hold and are not repeated in full here. Two additional facts, found while resolving the architecture question in §6 of the Product Owner's new constraints, change this revision's design:

### 2.12 `VSP.Core/Configuration/` is a third scaffolded-but-empty folder
`VSP.Core/VSP.Core.csproj` declares `<Folder Include="Configuration\" />` alongside `Logging\`, `Extensions\`, `Services\` — never populated, exactly the same "scaffolded in advance" signal Epic-014 found and filled for `Logging\`. `Configuration\` is the natural, evidence-based home for a shared, file-reading component — more directly on-point than inventing a new folder name, and it resolves the single-source-of-truth question in §6 without adding any new project reference (see below).

### 2.13 A single, already-alive place exists to answer "is a recording active right now"
`LiveViewViewModel.IsRecording` (public `bool`, already used by `StartRecordingCommand`/`StopRecordingCommand`'s `CanExecute`) reflects the one `IMediaController` instance the app runs at a time — v1.0's Live View hosts exactly one camera's controller, owned by `LiveViewViewModel`, which `MainWindowViewModel` constructs once at startup and keeps alive for the process lifetime. `MainWindowViewModel` today discards its own reference to that `LiveViewViewModel` immediately after passing it into `new LiveView(...)` — it will need to keep one, to give `SettingsViewModel` a way to ask.

---

# 3. Settings Data Model and Storage Analysis

### 3.1 Single source of truth: a new shared file-access component in `VSP.Core.Configuration`

Resolving the Product Owner's architecture question directly: **neither** of the two named options, literally — `RecordingPathProvider` does not call into `AppSettingsProvider`, and `AppSettingsProvider` does not reach into `VSP.Player`. Both would require a new cross-project reference that does not exist today (`VSP.Player` does not reference `VSP.Infrastructure`, and `VSP.Infrastructure` does not reference `VSP.Player`). Instead, the actual file I/O — the one thing that must not be duplicated — moves one level lower, into `VSP.Core.Configuration`, which **both `VSP.Player` and `VSP.Infrastructure` already reference today**. This adds **zero new project references anywhere**, which is a strictly better outcome than either option the question offered, while satisfying the same underlying requirement (one reader, one writer, one default formula):

```csharp
namespace VSP.Core.Configuration;

// The literal, permissive shape of recording-settings.json -- nullable, no domain
// enums, no defaults, no validation. Unknown JSON properties are ignored (System.Text.Json's
// default behavior); missing properties simply deserialize to null.
public sealed class SettingsFileContents
{
    public string? RecordingRoot { get; init; }
    public int? RetentionDays { get; init; }
    public string? Language { get; init; }
    public string? Theme { get; init; }
}

public sealed class SettingsFileStore
{
    public SettingsFileStore() : this(DefaultConfigDirectory) { }

    internal SettingsFileStore(string configDirectory) { ... } // test seam, same convention as FileLogger/DatabaseService/RecordingPathProvider

    public SettingsFileContents Load() { ... }               // malformed JSON -> AppLog.Warning, returns an all-null SettingsFileContents; never throws
    public void Save(SettingsFileContents contents) { ... }  // atomic write, see 3.3; never partially writes the target file
}

// The one formula for "what recording root applies when none is configured" -- a pure
// function, no side effects, no directory creation. Shared so the default is computed
// identically wherever it's needed.
public static class RecordingRootDefaults
{
    public static string Compute(string configDirectory) => Path.Combine(configDirectory, "Recordings");
}
```

- **`RecordingPathProvider.cs` (`VSP.Player`) is modified, not left untouched as revision 1 said.** Its own `File.ReadAllText` + `JsonSerializer.Deserialize<RecordingSettingsFile>` call is replaced with `new SettingsFileStore(configDirectory).Load().RecordingRoot`; its blank/absent-value fallback now calls `RecordingRootDefaults.Compute(configDirectory)` instead of its own private constant. **Its public API — `GetRecordingRoot()`, `GetCameraRecordingDirectory(Guid)`, both internal test-seam overloads, and every existing fallback/directory-creation behavior — is unchanged, verified by every existing assertion in `RecordingPathProviderTests.cs` continuing to pass unmodified.** No new project reference: `VSP.Player.csproj` already references `VSP.Core`.
- **`AppSettingsProvider` (`VSP.Infrastructure/Settings/`, new) wraps `SettingsFileStore` + `RecordingRootDefaults`** for its own `Load()`/`Save()`, mapping the raw `SettingsFileContents` to/from the domain-level `AppSettings` model (§3.2) and applying defaults for `RetentionDays`/`Language`/`Theme`. It computes the same default recording-root formula as `RecordingPathProvider` for **display** purposes but does not eagerly create the directory merely by being read — only `RecordingPathProvider` (used by actual recording I/O) and the Settings Save flow's explicit create-folder step (§4) ever call `Directory.CreateDirectory`, so opening the Settings screen has no filesystem side effect.

**Signed off in the Approval Record above**: this is a real, if narrow, edit to a file that shipped under Epic-011 (frozen). No public behavior changes — only the internal JSON-parsing implementation is swapped, and revision 1's own architecture question already presupposed touching one of `RecordingPathProvider`/`AppSettingsProvider` to eliminate duplicate parsing. Per the Approval Record: internal implementation only, public API and every observable behavior unchanged, `RecordingPathProviderTests.cs` passing unmodified is the acceptance bar. Treated as Epic-016 extending the seam Epic-011 explicitly built "for this and future Epics," not as reopening Epic-011's shipped scope.

### 3.2 Domain model — `VSP.Infrastructure/Settings/`

```csharp
namespace VSP.Infrastructure.Settings;

public enum AppTheme { System, Light, Dark }

public enum AppLanguage { English, TraditionalChinese }

public static class AppSettingsLimits
{
    public const int MinRetentionDays = 1;
    public const int MaxRetentionDays = 3650;   // 10 years -- generous upper sanity bound against
                                                  // fat-finger/corrupt values; not a claim that 10
                                                  // years of retention is realistic or enforced (no
                                                  // retention behavior exists yet, see Out of Scope)
    public const int DefaultRetentionDays = 30;
}

public sealed class AppSettings
{
    public required string RecordingPath { get; init; }
    public required int RetentionDays { get; init; }
    public required AppLanguage Language { get; init; }
    public required AppTheme Theme { get; init; }
}
```

`AppSettingsLimits` is defined once here and referenced both by `AppSettingsProvider.Load()` (clamps/defaults an out-of-range persisted value, e.g. from hand-editing the file) and by `VSP.UI`'s `SettingsValidator` (§7) for interactive input validation — one set of bounds, not two.

`Language`/`Theme` persist as **strings**, never enum ordinals (ordinals break if a member is ever reordered). Mapping is explicit and defensive, owned by `AppSettingsProvider`, not `SettingsFileStore` (which stays domain-free):

```csharp
// Language: exactly "en-US" / "zh-TW" persisted, per Product Owner instruction. Unrecognized
// or missing value -> English default. A *present but unrecognized* value additionally logs
// AppLog.Warning (distinct from *absent*, which is normal first-run/pre-Epic-016 behavior and
// not logged).
// Theme: exactly "System" / "Light" / "Dark" persisted. Same unrecognized-vs-absent distinction.
```

### 3.3 Atomic save
`SettingsFileStore.Save()` writes to a uniquely-named temp file in the same directory (`.recording-settings.{guid}.tmp`) and completes with `File.Move(tempFile, finalFile, overwrite: true)` — a same-volume rename, atomic at the filesystem level; a failure partway through the write leaves the temp file only, never a truncated/partial `recording-settings.json`. The temp file is removed in a `finally` if the move itself throws.

### 3.4 Backward compatibility, unknown/missing properties, corrupt JSON
- The file's name and location stay exactly `recording-settings.json` under `%LocalAppData%\VSP` — not renamed, so `RecordingPathProvider`'s existing filename constant needs no change and any hand-authored or Epic-011-era file continues to resolve.
- Deserialization uses `System.Text.Json`'s default behavior (no `UnmappedMemberHandling.Disallow` opt-in): unknown JSON properties are silently ignored, never a load failure.
- Any property absent from the file (including every pre-Epic-016 file, which has at most `RecordingRoot`) deserializes to `null` on `SettingsFileContents` and is defaulted by `AppSettingsProvider.Load()` per §3.2/§8.
- Syntactically invalid JSON (unparseable) is caught once, inside `SettingsFileStore.Load()`: `AppLog.Warning`, returns an all-null `SettingsFileContents`, `AppSettingsProvider.Load()` then applies full defaults on top. Never throws out to a caller.
- **No secrets are stored in this file** — the four persisted values (a filesystem path, an integer, a language code, a theme name) carry no credential material; nothing about this Epic's storage design changes that.

---

# 4. UI Scope

`SettingsViewModel` (real implementation):
- Constructor takes two injected delegates from its composition root, in addition to `AppSettingsProvider`/`SettingsValidator`/`ThemeService`: `Func<bool> isRecordingActive` and `Func<string, bool> confirmCreateFolder`. This keeps Save's multi-branch decision logic (§4.1) unit-testable without a live WPF `Application` or a real `MessageBox` — consistent with, but a deliberate step beyond, `CameraDetailWindow`'s existing convention of keeping confirmation dialogs out of the ViewModel (that flow has one branch; this one has several, worth testing directly).
- Loads current `AppSettings` via `AppSettingsProvider.Load()` on construction and keeps that as the "last-saved snapshot" baseline.
- Bindable properties: `RecordingPath`, `RetentionDaysText`, `SelectedLanguage`, `SelectedTheme`, `StatusMessage`. **None of these apply anything on change** — no live theme preview, no side effect — only `SaveCommand` does (§8).
- `SaveCommand` (`RelayCommand`) — see §4.1.
- `CancelCommand` (`RelayCommand`) — resets all four bound properties back to the last-saved snapshot and clears `StatusMessage`. No confirmation needed (nothing has been applied yet to discard).
- `BrowseRecordingPathCommand`'s actual `FolderBrowserDialog` call lives in `SettingsView.xaml.cs` code-behind (no branching logic worth unit-testing there, unlike Save) — sets `RecordingPath` on selection, leaves it unchanged on cancel.

### 4.1 Save flow (the actual sequenced logic, exact order)
1. Validate `RetentionDaysText` (`SettingsValidator.IsValidRetentionDays`, §7). Invalid → `StatusMessage` = inline error, abort. **Not logged** — user input validation is not a failure path (§9).
2. If `RecordingPath` is unchanged from the last-saved snapshot, skip straight to step 5.
3. If `RecordingPath` changed **and** `isRecordingActive()` is true → `StatusMessage` = "Cannot change Recording Path while a recording is in progress. Stop the current recording and try again." Abort — **nothing is persisted**, including the other three fields, keeping Save all-or-nothing and easy to reason about.
4. If `RecordingPath` changed and recording is not active:
   a. If the folder does not exist, call `confirmCreateFolder(path)`. Declined → abort, `StatusMessage` = "Save cancelled." (no log — a user decision, not a failure).
   b. Create the folder (`Directory.CreateDirectory`). Failure → `AppLog.Error`, `StatusMessage` = failure message, abort.
   c. `SettingsValidator.HasWriteAccess(path)` (§7). False → `AppLog.Warning`, `StatusMessage` = "Recording Path is not writable.", abort.
5. `AppSettingsProvider.Save(...)` with all four current values. Failure (I/O exception) → `AppLog.Error`, `StatusMessage` = "Could not save settings.", abort — no partial application.
6. On success: if `SelectedTheme` changed, `ThemeService.Apply(SelectedTheme)` (never throws, see §8). Update the last-saved snapshot to the new values. `StatusMessage` = "Settings saved."

`SettingsView.xaml`: the placeholder `TextBlock` becomes a form (Recording Path as `VspTextBox` + "Browse…" button; Retention Days as `VspTextBox`; Language and Theme as `VspComboBox`), a `PrimaryButton` "Save," a `SecondaryButton` "Cancel," and a status text area — all via existing styles, no new control styles needed. A short static caption under Language states it has no effect on displayed text yet.

---

# 5. Architecture Impact

### 5.1 `VSP.Core/Configuration/` populated (new)
`SettingsFileContents.cs`, `SettingsFileStore.cs`, `RecordingRootDefaults.cs` (§3.1). No new project reference — `VSP.Player` and `VSP.Infrastructure` both already reference `VSP.Core`.

### 5.2 `VSP.Infrastructure/Settings/` populated (as revision 1 anticipated, now concretely designed)
`AppTheme.cs`, `AppLanguage.cs`, `AppSettingsLimits.cs`, `AppSettings.cs`, `AppSettingsProvider.cs` (§3.2). No new project reference.

### 5.3 `VSP.Player/Recording/RecordingPathProvider.cs` — modified, behavior preserved
See §3.1's flagged sign-off note. `MediaController.cs`, `PlaybackController.cs` remain untouched; TD-031 not implemented.

### 5.4 `VSP.UI.csproj` gains `<UseWindowsForms>true</UseWindowsForms>`
Unchanged from revision 1 — needed for `FolderBrowserDialog`. Project setting, not a new NuGet package.

### 5.5 Theme-switching mechanism
Unchanged in shape from revision 1 (`VSP.UI/Themes/Dark.xaml`, `Light.xaml`, `VSP.UI/Services/ThemeService.cs`, App-level `Brushes.xaml` keys only), with two refinements from this round's constraints:
- **Default is `System`**, not `Dark` (§6). `ThemeService.Apply(AppTheme.System)` resolves via one registry read (`HKEY_CURRENT_USER\...\Personalize\AppsUseLightTheme`) **at startup only** — no `SystemEvents.UserPreferenceChanged` subscription, no live reaction to an OS theme change while VSP is running. This is a deliberate scope line per instruction, not an oversight: WPF has no built-in light/dark-change event, and wiring `Microsoft.Win32.SystemEvents` correctly (subscribe at startup, unsubscribe at shutdown to avoid a static leak) is a small but real addition, not "already trivial" — held out of scope.
- A registry read failure resolves to Dark and logs `AppLog.Warning` (§9) — never throws, never blocks startup.
- **Honesty note for the Task Plan, not a new open question**: because only `Brushes.xaml`'s keys and the new Settings screen consume `DynamicResource` (§5.6 of revision 1, TD-033 below), a `System` resolution to Light will only visibly change the few elements wired that way — the rest of the app keeps its current hardcoded-dark appearance regardless of OS theme, until TD-033 is paid down. Stated here so it isn't mistaken for a bug when found later.

### 5.6 `MainWindowViewModel.cs` — new, small addition to give Settings a way to check active recording
Currently `MainWindowViewModel` constructs `new LiveView(new LiveViewViewModel(...))` inline, keeping no reference of its own to the `LiveViewViewModel` instance. It gains a private field (`_liveViewViewModel`), constructed first and passed into `LiveView`'s existing constructor unchanged, then passed as `() => _liveViewViewModel.IsRecording` into `SettingsViewModel`'s new `isRecordingActive` delegate parameter (§4). No change to `LiveView`, `LiveViewViewModel`, or `IMediaController` — this reads an already-public property through a reference `MainWindowViewModel` simply didn't previously retain.

### 5.7 No DI container introduced
Every new type follows the established hand-wired, public-constructor-plus-internal-test-seam convention (§2.4 of revision 1).

---

# 6. Migration / Default-Value Behavior

| Field | Default when absent | Rationale |
|---|---|---|
| Recording Path | `RecordingRootDefaults.Compute(...)` (via `RecordingPathProvider`'s existing default, unchanged) | Identical to today's behavior for any existing installation |
| Retention Days | **30** (`AppSettingsLimits.DefaultRetentionDays`) | Per instruction; no retention behavior exists yet to be affected |
| Language | English (`en-US`) | The only language with actual translated text in the app (none — see TD-034) |
| Theme | **System** | Per instruction (changed from revision 1's proposed `Dark`). Resolves to Dark on most current dev/user machines observed so far, so this is not expected to visibly change the app's appearance for most users on upgrade — but unlike a hardcoded `Dark` default, a user whose OS is already in Light mode will now see VSP's (partial, §5.5) Light palette immediately, without opening Settings first. |

A persisted value that is syntactically valid JSON but semantically invalid (e.g. `RetentionDays: -5`, or a `Language` string that isn't `en-US`/`zh-TW`) is treated the same as absent for that one field — defaulted, with `AppLog.Warning` since, unlike absence, it indicates the file was hand-edited or corrupted outside the app.

`AppSettingsProvider.Save()` always writes all four current fields in one pass — changing one field never resets another to a default the user didn't touch.

---

# 7. Validation Plan

`SettingsValidator` (`VSP.UI/Validation/`, static, `CameraValidator`-shaped):

| Field | Rule | Notes |
|---|---|---|
| Recording Path | Non-blank; syntactically valid path | Existence/creation and write-access are handled as explicit Save-flow steps (§4.1), not folded into a single boolean, because each needs its own message and its own decision (confirm-create vs. hard-fail) |
| Retention Days | Parses as an integer; `AppSettingsLimits.MinRetentionDays` (1) ≤ n ≤ `AppSettingsLimits.MaxRetentionDays` (3650) | Same constants `AppSettingsProvider.Load()` uses (§3.2) — one definition |
| Language | Fixed two-value enum via `ComboBox` | Cannot produce an invalid value by construction |
| Theme | Fixed three-value enum via `ComboBox` | Cannot produce an invalid value by construction |

`SettingsValidator.HasWriteAccess(string path)`: writes a uniquely-named probe file (`.vsp-write-test-{guid}.tmp`) into `path`, inside a `try`, and determines the write-access verdict (`true`/`false`) from whether that write succeeded — never throws. Deletion of the probe file happens **unconditionally in a `finally` block**, so it runs whether the write succeeded, failed, or threw, satisfying "do not validate by leaving a permanent test file behind." The verdict is fixed before cleanup runs, so cleanup can never change it. **If the cleanup delete itself fails** (caught separately, inside the `finally`), `HasWriteAccess` logs `AppLog.Warning` once, for that cleanup failure specifically, and returns its already-determined verdict normally — the failure is swallowed, never rethrown, never escalated to `Error`, never surfaced as a Save-blocking condition beyond what the write-access verdict itself already says. The verdict (`false` = not writable) is still not logged by the validator itself — that remains the caller's decision, per §8's table; only the cleanup failure is logged here, since only `HasWriteAccess` knows a temp file might have been left behind.

---

# 8. Logging and Error Handling (Epic-014/015 foundations, failure paths only)

| Failure path | Where | Level | Behavior |
|---|---|---|---|
| Settings load: corrupt/unparseable JSON | `SettingsFileStore.Load()` (`VSP.Core.Configuration`) | Warning | Returns all-null contents; `AppSettingsProvider` applies full defaults |
| Settings load: present-but-invalid field value (bad enum string, out-of-range int) | `AppSettingsProvider.Load()` | Warning | That one field defaults; other valid fields are kept |
| Settings save: I/O failure during atomic write | `SettingsFileStore.Save()` / surfaced through `AppSettingsProvider.Save()` | Error | Exception reaches `SettingsViewModel`, which aborts and shows a status message — nothing partially applied |
| Folder creation failure (user confirmed create, `Directory.CreateDirectory` throws) | `SettingsViewModel` Save flow | Error | Inline status message, Save aborted |
| Write-access validation failure (probe write fails) | `SettingsViewModel`, after calling `SettingsValidator.HasWriteAccess` | Warning | Inline status message, Save aborted — routine/expected condition, not a system fault (same tier Epic-015 used for connection-test failures) |
| Write-access probe cleanup failure (temp file delete fails in the `finally`, independent of whether the write itself succeeded) | `SettingsValidator.HasWriteAccess`, inside its own `finally` | Warning | Logged and swallowed inside the validator; never rethrown, never `Error`; the already-determined write-access verdict is returned unchanged and Save proceeds according to that verdict — a cleanup failure alone never blocks Save and never leaves the app in a failed state (Approval Record) |
| Theme application failure (registry read throws, or resource-dictionary swap throws) | `ThemeService.Apply` | Warning | Falls back to Dark, continues, never throws out |

**Not logged, by design**: ordinary user-input validation (bad Retention Days text, a blank path) and user decisions (declining the create-folder prompt) — these are normal interactive UI outcomes, not failures, per the explicit instruction not to add feature-event logging beyond failure paths. No `AppLog.Info("Settings saved")` or similar success-path logging exists anywhere in this Epic.

---

# 9. Task Plan

### Files to add
- `VSP.Core/Configuration/SettingsFileContents.cs`
- `VSP.Core/Configuration/SettingsFileStore.cs`
- `VSP.Core/Configuration/RecordingRootDefaults.cs`
- `VSP.Infrastructure/Settings/AppTheme.cs`
- `VSP.Infrastructure/Settings/AppLanguage.cs`
- `VSP.Infrastructure/Settings/AppSettingsLimits.cs`
- `VSP.Infrastructure/Settings/AppSettings.cs`
- `VSP.Infrastructure/Settings/AppSettingsProvider.cs`
- `VSP.UI/Themes/Dark.xaml`, `VSP.UI/Themes/Light.xaml`
- `VSP.UI/Services/ThemeService.cs`
- `VSP.UI/Validation/SettingsValidator.cs`
- `VSP.Tests/Core/SettingsFileStoreTests.cs`
- `VSP.Tests/Infrastructure/AppSettingsProviderTests.cs`
- `VSP.Tests/UI/SettingsValidatorTests.cs`
- `VSP.Tests/UI/ThemeServiceTests.cs` (System-resolution logic only — the live `MergedDictionaries` swap is manually validated, same tier as Epic-014's exception dialogs)
- `VSP.Tests/UI/SettingsViewModelTests.cs` (the Save flow's branches, §4.1, via injected fake `isRecordingActive`/`confirmCreateFolder` delegates and a temp-directory `AppSettingsProvider` — no live `Application` needed)

### Files to modify
- `VSP.Player/Recording/RecordingPathProvider.cs` — internal file-reading swapped to `SettingsFileStore`/`RecordingRootDefaults`; public behavior unchanged (§3.1, flagged for explicit sign-off given Epic-011 is frozen).
- `VSP.UI/ViewModels/SettingsViewModel.cs`, `VSP.UI/Views/SettingsView.xaml` / `.xaml.cs` — real implementation (§4).
- `VSP.UI/ViewModels/MainWindowViewModel.cs` — retain `_liveViewViewModel` reference, wire `isRecordingActive` into `SettingsViewModel` (§5.6).
- `VSP.UI/App.xaml.cs` — construct `AppSettingsProvider`, load settings, `ThemeService.Apply(...)` after `InitializeLogging()`, before database init.
- `VSP.UI/VSP.UI.csproj` — `<UseWindowsForms>true</UseWindowsForms>`.
- `Docs/CHANGELOG.md`, `Docs/03_PRODUCT_ROADMAP.md` — on completion only, held until acceptance (Epic-014/015 precedent).

### Files explicitly not to touch
- `MediaController.cs`, `PlaybackController.cs` (TD-031 not implemented).
- `App.xaml.cs`'s existing three exception handlers and `HandleDatabaseInitializationFailure` (TD-032 not implemented).
- `Styles/Buttons.xaml`, `Inputs.xaml`, `Cards.xaml`, `Colors.xaml`, `Controls.xaml`, `Typography.xaml`, and every other file with a hardcoded color (TD-033).
- Any SQLite/`CameraTable` file — no schema change.
- `MaterialDesignColors`/`MaterialDesignThemes` — remain unused.
- No `.resx` file anywhere; no existing UI string rewritten.

### Sequence
1. `SettingsFileContents`, `SettingsFileStore`, `RecordingRootDefaults` + `SettingsFileStoreTests` (temp-directory seam: default resolution, round-trip, malformed-JSON → Warning + defaults, atomic-write-survives-a-simulated-mid-write-failure, unknown-property tolerance).
2. `RecordingPathProvider` internals swapped to use the above; **full existing `RecordingPathProviderTests.cs` run unmodified and must stay green** — the acceptance bar for "public behavior preserved."
3. `AppTheme`, `AppLanguage`, `AppSettingsLimits`, `AppSettings`, `AppSettingsProvider` + `AppSettingsProviderTests` (defaults, round-trip, legacy-`RecordingRoot`-only file loads correctly, out-of-range/invalid values default with a logged Warning, partial-update preserves other fields).
4. `SettingsValidator` + `SettingsValidatorTests` (including the write-access probe leaves nothing behind, verified by asserting the temp directory's file list before/after).
5. `Dark.xaml`/`Light.xaml` + `ThemeService` + `ThemeServiceTests` (System-resolution logic, registry-failure fallback).
6. `VSP.UI.csproj` WinForms interop; `MainWindowViewModel` wiring; `SettingsViewModel` (full Save flow, §4.1) + `SettingsViewModelTests`; `SettingsView.xaml`/`.xaml.cs`.
7. `App.xaml.cs` startup wiring.
8. Manual validation (not unit-testable): folder-browse dialog; create-folder confirmation prompt (accept and decline paths); Save blocked with a clear message while a recording is active, then succeeds once stopped; Theme Light/Dark/System (forcing the OS to each mode) changes the Settings screen's own colors on Save; restart the app and confirm all four persisted values are still in effect; Cancel discards in-progress edits without affecting the running app.
9. Build + full suite; `CHANGELOG.md`/`03_PRODUCT_ROADMAP.md` updates deferred until acceptance; Epic Review.

### Test plan
Per-component coverage listed in the Sequence above. Full existing suite (632/632 per Epic-015, plus revision-1's own new tests once added) remains green throughout, with `RecordingPathProviderTests.cs` specifically called out as unchanged/still-passing proof that step 2 didn't alter behavior.

### Rollback
All new files are additive and isolated to `VSP.Core/Configuration/`, `VSP.Infrastructure/Settings/`, `VSP.UI/Themes/`, `VSP.UI/Services/ThemeService.cs`, `VSP.UI/Validation/SettingsValidator.cs`, and their test files. `RecordingPathProvider.cs` has a single, contained internal-only change to revert. `SettingsViewModel`/`SettingsView`/`MainWindowViewModel`/`App.xaml.cs`/`VSP.UI.csproj` revert to their pre-Epic-016 shape. No impact on any other feature area.

---

# 10. Risk Ceiling

**MEDIUM** — unchanged rating from revision 1, re-justified: still additive-only in the vast majority of surface area (two previously-scaffolded folders populated, one small new service, one project-setting change, no new package, no DB schema change, no public API break to any *other* existing type). The one item that could argue for pushing higher is `RecordingPathProvider.cs`'s internal edit inside a frozen Epic's file — mitigated by the "verified via unmodified existing tests" acceptance bar in Sequence step 2 and the explicit sign-off flag in §3.1/§5.3, not by skipping the discussion.

---

# 11. New Technical Debt Candidates From This Epic (proposed, not assigned)

- **TD-033 (proposed, now explicitly required to be recorded per instruction)**: Theme mechanism ships with Light/Dark baseline palettes wired only at the `Brushes.xaml`-key level plus the new Settings screen; the ~23 existing XAML files that hardcode colors directly, and `Buttons.xaml`/`Inputs.xaml`/`Cards.xaml` themselves, are not retrofitted to theme-aware `DynamicResource` bindings. Neither Light mode nor a `System`-resolved-to-Light session will visually apply to most of the existing app until a future per-view retrofit pass.
- **TD-034 (proposed)**: Language setting persists a real, stable selection (`en-US` / `zh-TW`) with zero actual translated resources behind it — explicitly a placeholder per this round's instruction, not an accidental gap. No visible effect until a future Localization Epic builds `.resx`-based translation and wires `CurrentUICulture`.
- **TD-035 (new, proposed)**: `System` theme is resolved once at startup only; VSP does not react to the OS theme changing while it is running (would need a `Microsoft.Win32.SystemEvents.UserPreferenceChanged` subscription with matching shutdown cleanup). Explicitly out of scope per instruction, recorded so it isn't rediscovered as an oversight later.

---

# 12. Out of Scope

- Actual recording retention cleanup (deleting/rotating recordings based on `RetentionDays`) — value is persisted only.
- Full localization — no `.resx`, no `CurrentUICulture` wiring, no rewritten UI strings, no translation framework of any kind. See TD-034.
- Full theme migration — only the switching mechanism and App-level baseline palettes. See TD-033.
- Real-time reaction to a Windows theme change while VSP is running. See TD-035.
- Moving or rewriting existing recordings when Recording Path changes — new path applies to future recordings only.
- Storage quota management.
- Multiple recording roots.
- Network-share credential management.
- Installer changes.
- Any database schema change.
- Any new external NuGet package.
- Adopting `MaterialDesignColors`/`MaterialDesignThemes`.
- Any settings beyond the four named in `V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.4.
- TD-031, TD-032 — recorded, not implemented.
- Any change to Epic-010, Epic-012 through Epic-015's shipped scope — all remain frozen; only the one flagged, behavior-preserving edit to Epic-011's `RecordingPathProvider.cs` is proposed (§3.1), pending explicit sign-off.

---

# 13. Implementation Note — Settings screen theme wiring (found during Manual Validation)

Sequence step 6 originally shipped `SettingsView.xaml` with the same hardcoded hex colors as the
placeholder it replaced (`Background="#1E1E1E"`, `Foreground="White"`/`"#B0B0B0"`), not bound to
the new `Themes/Dark.xaml`/`Light.xaml` brushes. That contradicted §5.5's own stated design
("only Brushes.xaml's keys **and the new Settings screen** consume `DynamicResource`") and meant
`ThemeService.Apply` had no visible effect anywhere in the app, including on the one screen meant
to demonstrate it. Caught during Manual Validation (§14) before Product Acceptance, not a
Product-Owner-requested change: `SettingsView.xaml`'s own background and text elements (page
background, "Settings" title, the four field labels, the Language caption, the status line) were
changed from hardcoded hex to `{DynamicResource BrushBackground}` / `BrushTextPrimary` /
`BrushTextSecondary`. Its `Save`/`Cancel`/`Browse...` buttons and its `TextBox`/`ComboBox`
controls still use `Buttons.xaml`/`Inputs.xaml`'s existing hardcoded-color styles, unchanged —
see §14.5, this remains TD-033. No other file touched by this note.

---

# 14. Manual Validation (2026-08-02)

Performed against the actual built `VSP.UI.exe`, not inferred from code or from the unit suite.
The app was launched and driven with Windows UI Automation (`System.Windows.Automation` +
native `SendMessage`/`GetDlgItem` for the Win32 folder-picker dialog), screenshots were captured
with `Graphics.CopyFromScreen` and visually inspected, and the process was fully killed and
relaunched (not just navigated away and back) for every "Restart" step below, so each
"Persistence" result reflects a real `AppSettingsProvider.Load()` against
`recording-settings.json` on disk, not in-memory ViewModel state surviving in the same process.
The developer machine's real `%LocalAppData%\VSP\recording-settings.json` did not exist before
this pass (fresh install state); it was deleted again afterward so the app returns to that same
pristine, all-defaults state for the Product Owner's own testing. The developer machine's OS
dark/light registry value (`HKCU\...\Personalize\AppsUseLightTheme`) was Dark before this pass
and was restored to Dark afterward, confirmed by re-reading the key.

### 14.1 Recording Path

| Step | Result |
|---|---|
| Browse | `Browse...` opened the real Explorer-style folder picker; a target folder was typed into its path field and accepted. The `RecordingPath` field updated to the selected folder immediately — confirmed both by reading the `TextBox`'s UIA value and by screenshot. |
| Save | Status line read exactly `Settings saved.`; `recording-settings.json`'s `RecordingRoot` matched the selected folder. |
| Restart | Process killed and relaunched. |
| Persistence | `RecordingPath` field showed the same folder after restart, read from disk via `AppSettingsProvider.Load()` — not carried over in memory. |

### 14.2 Retention Days

| Step | Result |
|---|---|
| Save | Changed to `90`; Save produced `RetentionDays: 90` in `recording-settings.json`. |
| Restart | Process killed and relaunched. |
| Persistence | `RetentionDaysText` showed `90` after restart. |

### 14.3 Language

| Step | Result |
|---|---|
| Save | Changed to `TraditionalChinese`; Save produced `"Language": "zh-TW"` in `recording-settings.json`. |
| Restart | Process killed and relaunched. |
| Persistence | Language combo showed `TraditionalChinese` after restart. |

Combined into the same Save/restart cycle as Retention Days for efficiency (both fields are
written together by the same `AppSettingsProvider.Save()` call regardless), on top of
`AppSettingsProviderTests.Save_ChangingOneField_PreservesPreviouslySavedFields` already covering
the one-field-changes-don't-drop-another-field case at the unit level.

### 14.4 Theme

All four scenarios verified by screenshot after a full process restart (registry value set,
`recording-settings.json`'s `Theme` field set via the Settings screen and Save, then killed and
relaunched):

| Theme setting | Windows mode at restart | Result |
|---|---|---|
| System | Light (`AppsUseLightTheme=1`) | Settings page rendered with the Light palette (white background, dark text). |
| System | Dark (`AppsUseLightTheme=0`) | Settings page rendered with the Dark palette (`#1E1E1E` background, white text). |
| Light (explicit) | Dark | Settings page rendered Light — the explicit choice correctly overrides the OS's Dark setting, and the switch was visible **immediately on Save**, before any restart, confirming `SettingsViewModel`'s Save flow calls `ThemeService.Apply` live (§4.1 step 6) — then still Light after restart. |
| Dark (explicit) | Light | Settings page rendered Dark after restart — the explicit choice correctly overrides the OS's Light setting. |

### 14.5 Views participating in Theme switching vs. hardcoded colors

Audited by grepping every `.xaml` file under `VSP.UI` for `DynamicResource` against the six
`Brush*` keys. Result: **exactly one file** references them.

| Area | Uses `DynamicResource` (theme-aware) | Still hardcoded |
|---|---|---|
| `Views/SettingsView.xaml` | Page background, "Settings" title, the four field labels, the Language caption, the status line | Its own `Save`/`Cancel`/`Browse...` buttons and `TextBox`/`ComboBox` controls (via `Buttons.xaml`/`Inputs.xaml`) |
| Everything else — `MainWindow.xaml` (title bar, nav sidebar, status bar), `DashboardView`, `LiveView`, `PlaybackView`, `CameraListView`, `DeviceView`, `CameraDetailWindow`, `CameraDiscoveryView`, `DeviceCenterView`, `BatchEditWindow`, `BatchConnectionTestWindow`, `ImportWizard`, `AddDeviceWindow`, and the shared `Styles/Buttons.xaml`/`Inputs.xaml`/`Cards.xaml`/`Colors.xaml`/`Controls.xaml` | — | 100% hardcoded hex colors; unaffected by `ThemeService.Apply` |

**This is not full theme support and is not claimed as such.** Confirmed directly in the
screenshots above: switching to Light only changes the Settings screen's own background/text —
the nav sidebar, title bar, status bar, and every other screen stay on the original dark palette
regardless of Theme or Windows mode. This is the exact, already-documented scope of **TD-033**
(§11) — this validation pass confirms the limitation is real and precisely as scoped, not
broader or narrower than documented, after the §13 fix brought Settings itself into agreement
with §5.5's original intent.

### 14.6 Automated suite

`dotnet build VSP.slnx` succeeds; `dotnet test VSP.slnx` — **674/674** passing, including the 6
`RecordingPathProviderTests` unmodified (Epic-011 public-behavior acceptance bar, §3.1/§5.3).
