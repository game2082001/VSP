# Task-605 Discovery Timeout Hook

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.6
Epic: Discovery Foundation

---

# 1. Purpose

Task-605 adds the Timeout Hook named in Task-601 §8 Future Extension: an `IDiscoveryRunner` decorator that enforces a per-execution operation timeout, distinct from caller cancellation, per the separation Task-505 §4/§5 already established.

It is:

- an `IDiscoveryRunner` decorator
- a single operation-level timeout around one runner execution
- optional and additive

It is not:

- a change to `DiscoveryOrchestrationStatus`, `DiscoverySessionStatus`, or `DiscoveryProgressStage` — Task-505 §5 explicitly forbids adding a `TimedOut` value to any of these in the current line of work; doing so would touch shared result/session/progress contracts consumed across the whole Discovery subsystem, which this Epic's Risk Ceiling requires stopping for
- a replacement for caller cancellation — the caller's own `CancellationToken` is still honored exactly as before

---

# 2. Architecture Overview

```text
Caller
    v
TimeoutDiscoveryRunner (decorates IDiscoveryRunner)
    v
DiscoveryRunner
    v
DiscoveryOrchestrator
```

Internally, `TimeoutDiscoveryRunner` links a timeout-driven `CancellationTokenSource` with the caller's token (`CancellationTokenSource.CreateLinkedTokenSource`) and passes the linked token to the inner runner.

---

# 3. Responsibilities

`TimeoutDiscoveryRunner` may:

- start an internal `CancellationTokenSource` with `DiscoveryTimeoutPolicy.Timeout` and link it with the caller's token
- pass the linked token to the inner runner
- if the inner runner ends with `OperationCanceledException` **and** the timeout token (not the caller's token) requested cancellation, throw `DiscoveryTimeoutException` instead of letting the ambiguous `OperationCanceledException` propagate
- if the inner runner ends with `OperationCanceledException` **and** the caller's own token requested cancellation, rethrow that exception unchanged — caller cancellation is never reinterpreted as a timeout
- return the inner runner's result unchanged on success

`TimeoutDiscoveryRunner` must not:

- add a timeout status to `DiscoveryOrchestrationStatus`, `DiscoverySessionStatus`, or `DiscoveryProgressStage`
- cancel the caller's own token
- suppress a genuine caller cancellation

---

# 4. Dependency Rules

`TimeoutDiscoveryRunner` depends on `IDiscoveryRunner` and `DiscoveryTimeoutPolicy` only. No new NuGet package is introduced.

---

# 5. Files

- `VSP.Device/Discovery/Execution/DiscoveryTimeoutPolicy.cs`
- `VSP.Device/Discovery/Execution/DiscoveryTimeoutException.cs`
- `VSP.Device/Discovery/Execution/TimeoutDiscoveryRunner.cs`
- `VSP.Tests/Discovery/TimeoutDiscoveryRunnerTests.cs`

---

# 6. Out of Scope

- Any change to `DiscoveryOrchestrationStatus`, `DiscoverySessionStatus`, or `DiscoveryProgressStage`
- Per-candidate or per-service timeout (already covered by Task-402/403/404 request-level `Timeout` fields)
- Progress, Session, Retry, Metrics, Diagnostics hooks (separate Tasks)
- Commit (performed by the user)
