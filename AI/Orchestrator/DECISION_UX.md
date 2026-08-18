# Product Owner Decision UX

**Task:** AI01-008

The Orchestrator must never leave the Product Owner guessing what to answer.

Every Product Owner stop must output this fixed format:

```text
PRODUCT OWNER DECISION REQUIRED

Reason:
<why orchestration stopped>

Recommended:
<recommended decision>

Why:
<short reason>

If approved:
<what the Orchestrator will do automatically, and to which gate>

Alternatives:
1. <Recommended>
2. <Alternative>
3. <Alternative>
4. Stop / Defer
```

The Product Owner can answer with:

- `Approve recommended`
- `Option 1`
- `Option 2`
- `Option 3`
- `Option 4`

The Orchestrator must not end a Product Owner stop with only:

- `Awaiting Approval`
- `What would you like to do?`
- `Please advise next step`
- Any open-ended question without a recommended decision

## Pre-Authorized Execution

Task Plan Approval may include execution authorization for the normal lifecycle:

```text
Implementation
-> Local Validation
-> Commit
-> Push feature branch
-> Open / update PR
-> CI Gate and Automated Review Gate
-> Required Independent Review
-> In-scope remediation loop
-> Remediation commit / push / gates
-> READY_FOR_MERGE
```

When this authorization is present, the Router must not ask again for commit, push, PR creation/update, CI, automated review, independent review, or in-scope remediation. Only Stop Conditions interrupt the lifecycle.

## Merge Gate

AI01-008 V1.0 forbids autonomous merge.

After all gates pass, the Orchestrator must output a Product Owner decision using the fixed format with:

- PR number
- HEAD SHA
- CI result
- Automated Review result
- Independent Review result
- Remediation iterations
- Remaining known risks

The recommendation is:

```text
Merge PR <number>
```

The Product Owner remains the merge authority.
