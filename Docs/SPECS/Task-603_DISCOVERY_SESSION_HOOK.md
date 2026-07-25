# Task-603 Discovery Session Hook

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.6
Epic: Discovery Foundation

---

# 1. Purpose

Task-603 adds the Session Hook named in Task-601 §8 Future Extension: an `IDiscoveryRunner` decorator that records a `DiscoverySession` (Task-502) for each execution via the existing `DiscoverySessionFactory` and publishes it through an injected `IDiscoverySessionSink`.

This supersedes the ad-hoc session-recording code that was originally embedded directly in `DiscoveryRunner`'s constructor (rejected during Task-601 review for violating Task-601 §4/§6/§9/§13/§14) and removed from `DiscoveryRunner` in the Task-601 fix. The behavior is equivalent; the architecture is corrected to attach around the runner boundary instead of inside it.

It is:

- an `IDiscoveryRunner` decorator
- the only component that constructs a `DiscoverySession` for a runner execution
- optional and additive — a caller may still use plain `DiscoveryRunner` directly

It is not:

- a change to `DiscoveryRunner` or `DiscoveryOrchestrator`
- persistence, history, or a repository entity (`DiscoverySession` remains an in-memory lifecycle model per Task-502 §1)

---

# 2. Architecture Overview

```text
Caller
    v
SessionRecordingDiscoveryRunner (decorates IDiscoveryRunner)
    v
DiscoveryRunner
    v
DiscoveryOrchestrator
```

Matches the formal pipeline in Task-502 §2 (`DiscoveryOrchestrator -> DiscoveryOrchestrationResult -> DiscoverySessionFactory -> DiscoverySession`), with the factory call now made by the decorator rather than by `DiscoveryRunner` itself.

---

# 3. Responsibilities

`SessionRecordingDiscoveryRunner` may:

- record `StartTime` before delegating to the inner runner and `EndTime` after it returns
- build a `DiscoverySessionFactoryRequest` from the result, correlation id, start/end time
- call `DiscoverySessionFactory.Create(...)` and publish the resulting `DiscoverySession` via `IDiscoverySessionSink`
- return the inner runner's `DiscoveryOrchestrationResult` unchanged, exactly once per execution (mirrors the removed code's `ExecuteAsync_PublishesSessionExactlyOnce` guarantee)

`SessionRecordingDiscoveryRunner` must not:

- alter the `DiscoveryOrchestrationResult`
- persist the session (no repository, no SQLite)
- suppress an exception raised by the inner runner (if the inner runner throws, no session is published for that execution)

---

# 4. Dependency Rules

`SessionRecordingDiscoveryRunner` depends on `IDiscoveryRunner`, `DiscoverySessionFactory`, and `IDiscoverySessionSink` only.

`DiscoveryRunner` itself continues to depend on none of these, per the Task-601 fix — the dependency Task-601 §6 forbade on `DiscoveryRunner` now lives exclusively in this decorator, consistent with Task-601 §8: "future hooks should be added around the runner boundary, not inside domain orchestration."

---

# 5. Files

- `VSP.Device/Discovery/Sessions/IDiscoverySessionSink.cs`
- `VSP.Device/Discovery/Sessions/NoOpDiscoverySessionSink.cs`
- `VSP.Device/Discovery/Execution/SessionRecordingDiscoveryRunner.cs`
- `VSP.Tests/Discovery/SessionRecordingDiscoveryRunnerTests.cs`

---

# 6. Out of Scope

- Session persistence/history/repository
- UI or SignalR consumption of sessions
- Progress, Metrics, Retry, Timeout, Diagnostics hooks (separate Tasks)
- Commit (performed by the user)
