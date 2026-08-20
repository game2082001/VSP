# Crash And Session Recovery

**Scope:** Reusable AI01 task lifecycle

The orchestrator must recover from verifiable state, not chat memory.

## Recovery Sources

Use this order:

1. GitHub PR metadata.
2. Git branch and commit history.
3. GitHub workflow/check runs.
4. Structured state JSON.
5. PR comments and review comments.
6. Chat history as a hint only.

## Recovery Procedure

1. Read PR number, base branch, head branch, and head SHA.
2. Read current workflow/check conclusions.
3. Read structured state if present.
4. Compare state `lastKnownCommit` with PR head SHA.
5. If they differ, treat GitHub/Git as authoritative.
6. Recompute current stage.
7. Check role separation.
8. Check token budget and remediation count.
9. Continue only if no Stop Condition is present.

Uncommitted local work is protected and must not be overwritten during recovery.
