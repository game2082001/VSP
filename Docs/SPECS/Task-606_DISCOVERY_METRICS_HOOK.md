# Task-606 Discovery Metrics Hook (Minimal)

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.6
Epic: Discovery Foundation

---

# 1. Purpose

Task-606 adds a minimal Metrics Hook named in Task-601 §8 Future Extension: an `IDiscoveryRunner` decorator that records execution count, duration, and outcome via a small injected sink.

It is:

- an `IDiscoveryRunner` decorator
- a minimal in-process metrics record (count, duration, status, correlation id) — not an external metrics library integration
- optional and additive

It is not:

- an integration with any external metrics/telemetry package (none is introduced — a major dependency introduction would exceed this Epic's Risk Ceiling)
- aggregation, storage, or export of metrics — the sink's own consumer decides what to do with each sample
- a logging framework

---

# 2. Architecture Overview

```text
Caller
    v
MetricsRecordingDiscoveryRunner (decorates IDiscoveryRunner)
    v
DiscoveryRunner
    v
DiscoveryOrchestrator
```

---

# 3. Responsibilities

`MetricsRecordingDiscoveryRunner` may:

- measure wall-clock duration around the inner runner call
- record one `DiscoveryMetricsSample` (status, duration, correlation id) via `IDiscoveryMetricsSink` after a successful return
- return the inner runner's result unchanged

`MetricsRecordingDiscoveryRunner` must not:

- record a sample when the inner runner throws (consistent with the Session Hook's behavior in Task-603 — no record for a non-returning execution)
- aggregate, average, or export samples itself
- depend on any external metrics package

---

# 4. Dependency Rules

`MetricsRecordingDiscoveryRunner` depends on `IDiscoveryRunner` and `IDiscoveryMetricsSink` only.

---

# 5. Files

- `VSP.Device/Discovery/Metrics/DiscoveryMetricsSample.cs`
- `VSP.Device/Discovery/Metrics/IDiscoveryMetricsSink.cs`
- `VSP.Device/Discovery/Metrics/NoOpDiscoveryMetricsSink.cs`
- `VSP.Device/Discovery/Execution/MetricsRecordingDiscoveryRunner.cs`
- `VSP.Tests/Discovery/MetricsRecordingDiscoveryRunnerTests.cs`

---

# 6. Out of Scope

- External metrics/telemetry package integration
- Aggregation, storage, or export
- Progress, Session, Retry, Timeout, Diagnostics hooks (separate Tasks)
- Commit (performed by the user)
