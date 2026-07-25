# Task-604 Discovery Retry Hook

Version: 1.0
Status: Draft
Feature: Discovery
Milestone: Version 1.6
Epic: Discovery Foundation

---

# 1. Purpose

Task-604 adds the Retry Hook named in Task-601 §8 Future Extension: an `IDiscoveryRunner` decorator that retries an execution that ended in `DiscoveryOrchestrationStatus.Failed`, up to a configured attempt count, using a fixed delay between attempts.

It is:

- an `IDiscoveryRunner` decorator
- a narrow, explicit retry policy (attempt count + fixed delay), using only the BCL (`Task.Delay`) — no external retry/resilience package is introduced
- optional and additive

It is not:

- a general-purpose resilience framework
- applicable to caller cancellation or invalid-request outcomes (see §3)
- a change to `DiscoveryRunner` or `DiscoveryOrchestrator`

---

# 2. Architecture Overview

```text
Caller
    v
RetryingDiscoveryRunner (decorates IDiscoveryRunner)
    v
DiscoveryRunner
    v
DiscoveryOrchestrator
```

---

# 3. Responsibilities

`RetryingDiscoveryRunner` may:

- retry the inner runner call when it returns `DiscoveryOrchestrationStatus.Failed`, up to `DiscoveryRetryPolicy.MaxAttempts` total attempts
- retry the inner runner call when it throws an exception other than `OperationCanceledException`, up to the same attempt limit
- wait `DiscoveryRetryPolicy.Delay` between attempts, honoring the caller's `CancellationToken` during the wait
- return the final attempt's result or rethrow the final attempt's exception once attempts are exhausted

`RetryingDiscoveryRunner` must not:

- retry `Cancelled` results or `OperationCanceledException` — caller cancellation must be respected immediately, per Task-505's cancellation semantics
- retry `InvalidRequest` results — a malformed request will not succeed on retry
- retry `Completed` or `CompletedWithErrors` results — both are terminal outcomes, not failures
- introduce exponential backoff, jitter, or circuit-breaking (kept out to stay within a narrow, explicit policy)

---

# 4. Dependency Rules

`RetryingDiscoveryRunner` depends on `IDiscoveryRunner` and `DiscoveryRetryPolicy` only. No new NuGet package is introduced.

---

# 5. Files

- `VSP.Device/Discovery/Execution/DiscoveryRetryPolicy.cs`
- `VSP.Device/Discovery/Execution/RetryingDiscoveryRunner.cs`
- `VSP.Tests/Discovery/RetryingDiscoveryRunnerTests.cs`

---

# 6. Out of Scope

- Exponential backoff / jitter / circuit breaker
- Retrying cancellation or invalid-request outcomes
- Progress, Session, Metrics, Timeout, Diagnostics hooks (separate Tasks)
- Commit (performed by the user)
