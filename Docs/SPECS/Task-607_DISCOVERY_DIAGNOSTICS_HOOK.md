# Task-607 Discovery Diagnostics Hook

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.6
Epic: Discovery Foundation

---

# 1. Purpose

Task-607 adds the last hook named in Task-601 §8 Future Extension: a Diagnostics Hook that captures a lightweight, human-readable debug snapshot of one runner execution — distinct in purpose from Metrics (Task-606, a narrow numeric sample) and Session (Task-603, a business lifecycle record).

It is:

- an `IDiscoveryRunner` decorator
- a debug-facing snapshot: a generated diagnostic id, correlation id, timestamp, result status, and result reasons
- optional and additive

It is not:

- a replacement for Metrics or Session recording
- a logging framework or log sink
- persistence of any kind

---

# 2. Architecture Overview

```text
Caller
    v
DiagnosticsRecordingDiscoveryRunner (decorates IDiscoveryRunner)
    v
DiscoveryRunner
    v
DiscoveryOrchestrator
```

---

# 3. Responsibilities

`DiagnosticsRecordingDiscoveryRunner` may:

- generate a new diagnostic id (`Guid`) per execution, independent of `DiscoveryRunner`'s internal `DiscoveryExecutionId` (not exposed at the `IDiscoveryRunner` boundary, per Task-601's minimal execution context)
- capture a UTC timestamp, the request's correlation id, the result's status, and the result's reasons (code/message pairs)
- publish one `DiscoveryDiagnosticsSnapshot` via `IDiscoveryDiagnosticsSink` after a successful return
- return the inner runner's result unchanged

`DiagnosticsRecordingDiscoveryRunner` must not:

- publish a snapshot when the inner runner throws (same convention as Session and Metrics hooks)
- write to any log, file, or external system itself — that is the sink implementation's concern, not this decorator's

---

# 4. Dependency Rules

`DiagnosticsRecordingDiscoveryRunner` depends on `IDiscoveryRunner` and `IDiscoveryDiagnosticsSink` only.

---

# 5. Files

- `VSP.Device/Discovery/Diagnostics/DiscoveryDiagnosticsSnapshot.cs`
- `VSP.Device/Discovery/Diagnostics/IDiscoveryDiagnosticsSink.cs`
- `VSP.Device/Discovery/Diagnostics/NoOpDiscoveryDiagnosticsSink.cs`
- `VSP.Device/Discovery/Execution/DiagnosticsRecordingDiscoveryRunner.cs`
- `VSP.Tests/Discovery/DiagnosticsRecordingDiscoveryRunnerTests.cs`

---

# 6. Out of Scope

- Logging framework integration
- Persistence
- Progress, Session, Retry, Timeout, Metrics hooks (separate Tasks)
- Commit (performed by the user)
