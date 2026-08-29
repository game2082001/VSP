# Role Separation

**Scope:** Reusable AI01 task lifecycle

Required Independent Review must be independent from implementation.

AI02 makes this evidence mandatory for every Product or Engineering PR that claims `READY_FOR_MERGE`.

## Rules

- Claude Code cannot complete Required Independent Review for its own implementation.
- Codex Worker cannot complete Required Independent Review for a PR it modified.
- Codex Independent Reviewer must use read-only repository credentials.
- Reviewer credentials must not be stored in the repository.
- Implementation evidence and review evidence must be recorded separately.
- Structured state must record `implementationContextId`, `independentReviewerContextId`, and `developerEqualsReviewer`.
- For Codex-developed work, `implementationContextId` must not equal `independentReviewerContextId`.
- Review must inspect actual PR state, not only an implementation summary.
- VSP-AI-Implementation may author approved feature-branch remediation commits, but it is not a reviewer identity.
- Claude Automated Review may allow the trusted `vsp-ai-implementation` bot as a trigger actor only; it remains a read-only review gate and must not receive the Implementation write credential.

## Violation Handling

If the Router cannot prove role separation, it must set:

```text
STOPPED_FOR_PRODUCT_OWNER
```

and request a new reviewer context or Product Owner direction.

The PR must also be treated as:

```text
STOP / NOT READY_FOR_MERGE
```
