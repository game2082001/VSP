# Milestone M2 — Foundation Review

Date: 2026-08-01
Scope: Epic-010 through Epic-015
Type: Architecture and product review only — no production code modified, no ADR created, no Epic-016 scoped.

---

## 1. Foundation Summary

### Media Foundation (Epic-010, Live View)
`VSP.Player` implements ADR-002's contracts for the first time: `IMediaSession`/`IMediaController`/`IMediaClock`/`IFrameDispatcher`/`IFrameBuffer`/`IDispatcherMetrics`/`IFrameRenderer`. FFmpeg adopted per ADR-003, via `FFmpeg.AutoGen.Bindings.DynamicallyLinked` (the `DynamicallyLoaded` binding style ADR-003 originally anticipated was found broken during implementation — a real, documented deviation, not a silent one). `RtspMediaSession` + `FfmpegVideoDecoder` + `MediaController` + `WpfFrameRenderer` compose into bounded reconnect, pause/resume, and live statistics. A dedicated architecture review at close-out found 14 issues (1 Critical, 2 High, 5 Medium, 6 Low); 5 were fixed before acceptance (native-call race, cancellation-can't-interrupt-open, CTS leak across restarts, stale-session-completes-wrong-iteration, unsynchronized cross-thread field access), 7 accepted as technical debt (below). Performance baseline established (`Docs/PERFORMANCE_BASELINE.md`) as the standing comparison point for every later Video Epic.

### Recording Foundation (Epic-011)
Continuous stream-copy recording on the encoded tier, per ADR-002's explicit "recording must not depend on decode" principle — `MediaController` gained a second, encoded-tier `FrameDispatcher<EncodedFrame>` independent of the decode path. `FfmpegRecordingSession` does real stream-copy muxing (no decode, no re-encode). One real defect found and fixed pre-acceptance: an unseeded output `time_base` silently produced a valid-looking but empty file, root-caused against FFmpeg's own reference pattern. Config-file-backed `RecordingRoot` (`RecordingPathProvider`) established the "no Settings UI yet, smallest seam, JSON file under `%LocalAppData%\VSP`" convention that Epic-012, -014, and this review's readiness assessment for Epic-016 all build on.

### Playback Foundation (Epic-012)
Closed ADR-002's v3 evolution row. `RecordedFileMediaSession` (second `IMediaSession`), `PlaybackController` (second `IMediaController`, no reconnect loop — correctly a different shape than Live, not a copy of it), `PlaybackClock` (first `IMediaClock` with a real `Seek`). Closed Epic-010's own accepted debt on schedule: the `IFfmpegDemuxSource` seam let `FfmpegVideoDecoder` stop depending on the concrete `RtspMediaSession`, exactly as that debt item anticipated. One real defect found pre-acceptance: `PlaybackController` swallowed an Open failure without ever reaching `Error` (no reconnect loop to eventually get there, unlike Live) — fixed by transitioning to `Error` immediately. Recordings reorganized per-camera as an approved scope addition.

### Deployment Foundation (Epic-013)
Narrowly and deliberately scoped: `Directory.Build.props` (shared `Version`), `vsp.db` moved to `%LocalAppData%\VSP` (was broken under a non-admin install path), a self-contained win-x64 publish profile, FFmpeg vendor payload trimmed 411 MB → 33 MB. Explicitly not installer/distribution technology — "the goal is deployment, not distribution," Product Owner's words. Clean scope, clean execution, no carried debt.

### Logging Foundation (Epic-014)
`VSP.Core/Logging` (`LogLevel`, `ILogger`, `FileLogger`, `AppLog`) — in-house, no external framework, fixed-format daily-rolling text log, 30-day retention, per-write flush-to-disk. Three global unhandled-exception handlers in `App.xaml.cs`, each with a generated Error ID. `VSP.Infrastructure` gained a `VSP.Core` reference specifically so Infrastructure-layer code could log ("logging is a platform capability, not a UI capability" — Product Owner), fulfilled one Epic later by Epic-015. Manually validated against the built exe, not just unit tests. TD-029 recorded (`Environment.Exit` as the shutdown mechanism).

### Error Handling Foundation (Epic-015)
Six previously-silent exception paths given consistent handling: Database initialization (now returns `DatabaseInitializationResult` instead of throwing unhandled, with its own explicit single-Error-ID startup-failure dialog), Repository operations (log-and-rethrow, zero contract change to `ICameraRepository`'s 25 call sites), RTSP/ONVIF connection tests (exception now bound and logged, return value unchanged), Retry failures (`RetryingDiscoveryRunner`, non-final attempts now logged), Media reconnect failures (`MediaController`, each failed attempt now logged). Security-reviewed: no credential material in any new log call. Manually validated end-to-end (forced a real `SqliteException`, confirmed single-ID correlation, clean termination, no `MainWindow`). TD-030 recorded, explicitly complementing TD-029.

---

## 2. Architecture Consistency Review

### Strong, consistent patterns (worth naming as strengths, not just absence of problems)
- **Real-dependency testing over mocking**, unbroken since Epic-010: real FFmpeg round-trips (`RtspMediaSessionIntegrationTests`, `RecordingIntegrationTests`, `RecordedFileMediaSessionTests`), real loopback servers (`LoopbackRtspTestServer`, `LoopbackHttpTestServer`), real temp-file SQLite (this Epic's `SQLiteCameraRepositoryTests`/`DatabaseInitializerTests`). No epic in this range introduced a mock-based shortcut.
- **The "internal constructor + configurable path, public constructor defaults to the real location" test-seam convention** is now used identically by four unrelated classes: `RecordingPathProvider` (Epic-011), `FileLogger` (Epic-014), `DatabaseService` (Epic-015), and matches `AppLog`'s no-DI-container philosophy. This is genuine, deliberate convention reuse across three different epics and two different projects (`VSP.Player`, `VSP.Core`, `VSP.Infrastructure`) — a real strength, not a coincidence.
- **"New implementation against unchanged ADR-002 contracts," not new architecture per capability** — exactly as ADR-002 §Consequences demanded. `RecordedFileMediaSession`/`PlaybackController`/`PlaybackClock` are all textbook examples: `IMediaSession`, `IMediaController`, `IMediaClock` were never modified to accommodate them, only extended (`IMediaController` gained `Clock`/`Duration` additively, per Epic-012's own changelog).
- **Deferred-debt items get closed on schedule when a later Epic naturally touches the area** — Epic-010's "Decoder abstraction redesign" debt was explicitly closed by Epic-012's `IFfmpegDemuxSource` seam, on the Epic that predicted it. This is the debt-tracking process actually working, not just accumulating.

### Inconsistency found (concrete, not speculative)
**`MediaController` and `PlaybackController` now diverge on error-handling maturity for the structurally identical "session open failed" case.** `MediaController.ConnectionLoopAsync`'s open-failure catch (line ~383) was given `AppLog.Warning` logging by Epic-015 ("Media reconnect failures" was explicitly in scope). `PlaybackController.OpenAndReadAsync`'s open-failure catch (`PlaybackController.cs:222`) is the same shape — `catch (Exception) { /* MediaError already captured via HandleSessionStateChanged */ }`, exception still unbound, still unlogged — because "Playback open failures" was never named in Epic-015's six approved items. This isn't a defect in Epic-015 (its scope was followed exactly as approved), but it means today, a Live View connection failure leaves a log trail and a Playback session-open failure does not, for what a user experiences as the same kind of failure. Legitimate candidate for a narrowly-scoped follow-up, not a reason to reopen Epic-015.

**Two now-independent Error-ID-and-dialog implementations exist in `App.xaml.cs`** (`OnDispatcherUnhandledException` from Epic-014, `HandleDatabaseInitializationFailure` from Epic-015) with near-identical shape (generate ID → log → build a multi-line message naming the ID and log path → `MessageBox.Show`) but no shared helper — the message-building logic is duplicated inline in both methods. Minor, cosmetic, zero functional risk today; a third such dialog would be the point at which factoring out a shared `ShowFatalErrorDialog(...)` stops being premature.

### Deliberate, documented non-sharing (not a defect — flagging so it isn't mistaken for one)
`RetryingDiscoveryRunner` (Discovery, retry-with-delay) and `MediaController`'s reconnect loop (Live View, retry-with-delay) are two independently-built state machines of similar shape. ADR-002 itself calls this out by name: "the same pattern already validated by `RetryingDiscoveryRunner` elsewhere in this codebase, not shared code but a precedented shape" — i.e., the duplication was a conscious architectural choice at design time, not something that crept in unnoticed. Recorded here as confirmed-still-true, not re-litigated.

### Unnecessary complexity / simplification opportunities
- **`MediaController`'s SRP concern, flagged at Epic-010 acceptance, has grown for three consecutive Epics without being paid down.** Epic-010 accepted it at ~400 lines combining reconnect state machine + statistics + lifecycle orchestration. Epic-011 added a second `FrameDispatcher` and recording lifecycle. Epic-012 added `Clock`/`Duration`. Epic-015 added logging. Each addition was individually justified and small, but the debt item itself has never been revisited — this is the single most concrete "opportunities for simplification" finding in this review, and the only one where a future Epic doing nothing else *specifically about it* means it keeps growing by default with every touch to Live View.
- **DB-error and media-error normalization intentionally differ, and that's correct, not inconsistent**: `FfmpegErrorTranslator` (Epic-010) exists because ADR-002/003 require FFmpeg-specific types never cross out of `VSP.Player` into `VSP.UI`. Epic-015's repository logging passes the raw `SqliteException` straight to `AppLog.Error` with no translation layer — correct, because `AppLog`'s `Exception` parameter is already a generic `System.Exception` and nothing about that boundary requires hiding the concrete type the way the UI-facing `MediaError` boundary does. Noted so this isn't mistaken for a missed pattern later.

---

## 3. Technical Debt Review

### Existing, still open (from Epic-010/011/012, unrelated to Epic-014/015, unaddressed since acceptance)
| From | Item | Status |
|---|---|---|
| Epic-010 | `MediaController` decomposition (SRP) | Open, worsened — see §2 |
| Epic-010 | Decoder abstraction redesign | **Closed** by Epic-012's `IFfmpegDemuxSource` |
| Epic-010 | Buffer pooling (`ArrayPool<byte>`) | Open |
| Epic-010 | Busy-poll `Thread.Sleep(2)` in `FrameDispatcher` | Open |
| Epic-010 | `AddDllDirectory` migration (from `SetDllDirectory`) | Open |
| Epic-010 | `MediaError.Message` embeds raw native error text | Open |
| Epic-010 | `MediaController.SetState` double lock-acquire | Open, trivial |
| Epic-011 | `av_interleaved_write_frame` return code unchecked | Open |
| Epic-011 | `StartRecordingAsync` TOCTOU (consistent with pre-existing accepted pattern) | Open, accepted-as-is |
| Epic-012 | Seek precision (nearest keyframe only) | Open |
| Epic-012 | Pacing sleep held under native lock during large gaps | Open |
| Epic-012 | No frame-accurate position display | Open |
| Epic-012 | Pre-Epic-012 flat-root recordings not migrated | Moot — no real user data existed at the time |

None of these block Epic-016 (see §5). Six of thirteen original items remain genuinely open with zero movement across three subsequent Epics touching the same files — worth the Product Owner's attention as a batch if a future "Media Pipeline Hardening" Epic is ever scoped, but not urgent.

### New, from this review
- **TD-031 (new, proposed)**: `PlaybackController.OpenAndReadAsync`'s open-failure path does not log, unlike `MediaController`'s structurally identical path (Epic-015 closed the Live View instance, not the Playback one — see §2). Not implemented; recording only.
- **TD-032 (new, proposed)**: `App.xaml.cs`'s two Error-ID dialog implementations (`OnDispatcherUnhandledException`, `HandleDatabaseInitializationFailure`) duplicate message-construction logic with no shared helper. Cosmetic; worth factoring out if a third such dialog is ever added, not before.

These two numbers are proposed, not assigned — recording them here per this review's Objective; formal numbering/ledger entry is the Product Owner's call, consistent with how TD-029/030 were handled at Epic-014/015 acceptance.

### Confirmed priorities
- TD-029/TD-030 (`Environment.Exit`, Platform Lifecycle) remain the most architecturally significant open item — three call sites now depend on it, and any future distributed/multi-process component (Vision's Management/Recording Server split) will need this resolved before it can exist.
- `MediaController` decomposition is the second most significant — not urgent, but the cost of deferring it keeps compounding.
- Everything else in the existing list is genuinely low-severity and can continue to wait.

---

## 4. Foundation Freeze Review

| Epic | Status | Reasoning |
|---|---|---|
| Epic-010 (Live View) | **Frozen, carries debt** | Shipped scope stable and unmodified since acceptance; 6 of 7 accepted debt items still open and have been touched-around (not through) by three later Epics. Not blocking; a future Epic should account for this list, not reopen this Epic to fix it. |
| Epic-011 (Recording) | **Frozen** | Both accepted debt items are low-severity and consistent with existing accepted risk patterns elsewhere in the codebase; no reason to revisit. |
| Epic-012 (Playback) | **Needs follow-up Epic** | Functionally frozen, but this review surfaced a real, concrete gap (`OpenAndReadAsync`'s unlogged failure path, TD-031) that a future narrowly-scoped Error Handling extension should close — not urgent, but a real follow-up, not just "carries debt" in the abstract. |
| Epic-013 (Deployment) | **Frozen** | Clean scope, clean execution, zero carried debt. |
| Epic-014 (Logging) | **Frozen** | Explicitly frozen by Product Owner at acceptance; TD-029 tracked, nothing else outstanding. |
| Epic-015 (Error Handling) | **Frozen** | Explicitly frozen by Product Owner at acceptance; TD-030 tracked. The `PlaybackController` gap (TD-031) belongs to Epic-012's surface, not a defect in Epic-015's own approved scope, which was satisfied exactly as approved. |

No Epic in this range is "Needs cleanup" — that category (something actively wrong, not just incomplete) doesn't apply to any of the six. Epic-012 is the one "Needs follow-up Epic" case with a concrete, named finding.

---

## 5. Readiness for Epic-016 (Settings Foundation)

**Ready.** Nothing in Epic-010 through Epic-015 architecturally blocks Settings work:

- Settings' four approved fields (`Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md` §2.4 — Recording Path, Retention Days, Language, Theme) are orthogonal to the media/error-handling/deployment/logging foundations reviewed here. No shared contract, no shared mutable state, no ordering dependency.
- **Direct reuse available, not just non-interference**: `RecordingPathProvider` (Epic-011) already reads a config-file-backed `RecordingRoot` from `recording-settings.json` under `%LocalAppData%\VSP`, documented in its own source as "the smallest seam... for this and future Epics." Settings' "Recording Path" field is this same value, with a UI finally put in front of it — not a new mechanism.
- The established conventions Settings would follow are now proven across three Epics (`RecordingPathProvider`, `FileLogger`, `DatabaseService`, `AppLog`): no DI container, hand-wired composition, internal test-seam constructors, config-file-backed values under `%LocalAppData%\VSP`. Settings has a clear, precedented shape to follow rather than needing to invent one.
- `SettingsView`/`SettingsViewModel` already exist as wired-in placeholders (identical shape Dashboard was pre-Epic-009), confirmed in Epic-014's Current-State Analysis — the navigation and composition-root wiring Settings needs already exists and works.

**Nothing found in this review changes that assessment.** The two new findings (TD-031, TD-032) and the six carried-forward Epic-010/011/012 items are all independent of Settings — none of them touch `VSP.UI/Views/Settings*`, `RecordingPathProvider`, or the config-file convention Settings would extend.

---

## 6. Milestone Conclusion

**Milestone M2 Foundation Complete.**

Six Epics (010–015) delivered a coherent, internally consistent platform foundation: a media pipeline that has honored its own architecture document's contracts through three implementations without a single contract change, a deployment path that actually installs and runs on a clean machine, and — as of the two Epics just closed — a logging and error-handling substrate that finally gives the other four something to report through. The one architecture-level concern worth carrying forward with real weight is `MediaController`'s accumulating SRP debt (TD from Epic-010, never paid down across three subsequent touches); everything else found in this review is either already resolved, low-severity, or a narrow, nameable follow-up (TD-031, TD-032) rather than a systemic problem.

The platform is ready for Epic-016 (Settings Foundation) with no architectural blockers.
