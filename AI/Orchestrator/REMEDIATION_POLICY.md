# Remediation Policy

**Scope:** Reusable AI01 task lifecycle

The remediation loop is automatic only inside the approved scope.

If Task Plan Approval pre-authorized execution, in-scope remediation does not require another Product Owner approval. The Router stops only when a Stop Condition is reached.

## Loop

```text
REMEDIATION_REQUIRED
    -> Router classifies findings
    -> Claude Code or Codex Worker remediates
    -> Commit/push through approved orchestrated path
    -> Windows CI and Claude Automated Review
    -> Codex Independent Re-Review
```

## Limits

Default maximum remediation cycles: 2.

The Router must stop if:

- The finding requires scope expansion.
- The finding requires a Product Owner, architecture, or security decision.
- The remediation limit is exceeded.
- The token budget hard stop is reached.
- CI failure cannot be repaired inside approved scope.

## Protected Scope

Protected PRs, paused investigations, and excluded workstreams remain untouched unless the Product Owner explicitly includes them in the approved task plan.
