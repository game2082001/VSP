# Role Separation

**Task:** AI01-008

Required Independent Review must be independent from implementation.

## Rules

- Claude Code cannot complete Required Independent Review for its own implementation.
- Codex Worker cannot complete Required Independent Review for a PR it modified.
- Codex Independent Reviewer must use read-only repository credentials.
- Reviewer credentials must not be stored in the repository.
- Implementation evidence and review evidence must be recorded separately.
- Review must inspect actual PR state, not only an implementation summary.
- VSP-AI-Implementation may author approved feature-branch remediation commits, but it is not a reviewer identity.
- Claude Automated Review may allow the trusted `vsp-ai-implementation` bot as a trigger actor only; it remains a read-only review gate and must not receive the Implementation write credential.

## Violation Handling

If the Router cannot prove role separation, it must set:

```text
STOPPED_FOR_PRODUCT_OWNER
```

and request a new reviewer context or Product Owner direction.
