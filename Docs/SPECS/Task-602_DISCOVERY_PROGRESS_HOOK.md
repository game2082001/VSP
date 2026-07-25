# Task-602 Discovery Progress Hook

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.6
Epic: Discovery Foundation

---

# 1. Purpose

Task-602 adds the first runner-level hook named in Task-601 §8 Future Extension: a Progress Hook that publishes `DiscoveryProgress` (Task-503) around a `DiscoveryRunner` execution.

It is:

- an `IDiscoveryRunner` decorator
- a publisher of coarse, runner-boundary progress (started / terminal), not orchestrator-internal step tracking
- optional and additive — a caller may still use plain `DiscoveryRunner` directly

It is not:

- a change to `DiscoveryOrchestrator`, which remains progress-agnostic per Task-503 §2
- a UI, SignalR, or console publisher (those remain future concerns per Task-503 §2)
- a session, metrics, retry, or timeout mechanism

---

# 2. Architecture Overview

```text
Caller
    v
ProgressPublishingDiscoveryRunner (decorates IDiscoveryRunner)
    v
DiscoveryRunner
    v
DiscoveryOrchestrator
```

This follows Task-601 §8: hooks attach *around* the runner boundary, not inside it. `DiscoveryRunner` itself gains no new dependency; the decorator depends on `IDiscoveryRunner` and `IDiscoveryProgressPublisher` only.

---

# 3. Responsibilities

`ProgressPublishingDiscoveryRunner` may:

- publish one `DiscoveryProgress` (stage `Discovering`, 0/2 steps) immediately before delegating to the inner runner
- publish one `DiscoveryProgress` (terminal stage, 2/2 steps) immediately after the inner runner returns, mapped from `DiscoveryOrchestrationStatus`
- pass the request/cancellation token through to the inner runner unchanged
- return the inner runner's `DiscoveryOrchestrationResult` unchanged

`ProgressPublishingDiscoveryRunner` must not:

- inspect or alter `DiscoveryOrchestrationResult`
- publish intermediate orchestrator-internal stages (`Mapping`, `SelectingDriver`, `AwaitingApproval`, `CreatingCamera`, `Registering`) — the runner boundary has no visibility into those steps without reaching into orchestrator internals, which Task-601 §6 forbids
- catch or suppress exceptions raised by the inner runner

Status mapping (`DiscoveryOrchestrationStatus` -> `DiscoveryProgressStage`):

| Orchestration Status | Progress Stage |
|---|---|
| `Completed` | `Completed` |
| `CompletedWithErrors` | `Completed` (reasons carried in the snapshot) |
| `Cancelled` | `Cancelled` |
| `Failed` | `Failed` |
| `InvalidRequest` | `Failed` |

---

# 4. Dependency Rules

`ProgressPublishingDiscoveryRunner` depends only on `IDiscoveryRunner` and `IDiscoveryProgressPublisher`.

It must not depend on `DiscoverySessionFactory`, retry policy, timeout policy, metrics collector, repository, SQLite, UI, or logging framework — same boundary Task-601 §6 sets for `DiscoveryRunner` itself.

`IDiscoveryProgressPublisher` is a narrow sink interface (`void Publish(DiscoveryProgress progress)`); `NoOpDiscoveryProgressPublisher` is the default no-op implementation, mirroring the existing `NoOpDiscoverySessionSink` pattern removed from `DiscoveryRunner` in the Task-601 fix.

---

# 5. Files

- `VSP.Device/Discovery/Progress/IDiscoveryProgressPublisher.cs`
- `VSP.Device/Discovery/Progress/NoOpDiscoveryProgressPublisher.cs`
- `VSP.Device/Discovery/Execution/ProgressPublishingDiscoveryRunner.cs`
- `VSP.Tests/Discovery/ProgressPublishingDiscoveryRunnerTests.cs`

---

# 6. Out of Scope

- UI, SignalR, or console progress rendering
- Orchestrator-internal step-level progress
- Session, Metrics, Retry, Timeout, Diagnostics hooks (separate Tasks)
- Repository, SQLite, or persistence of progress history
- Commit (performed by the user)
