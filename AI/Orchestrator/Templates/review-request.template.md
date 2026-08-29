# Required Independent Review Request

Task: AI01-XXX
Role: Codex Independent Reviewer
Default model: gpt-5.6-luna medium

Review the PR using read-only credentials. Inspect actual repository state, diff, checks, tests, and review artifacts. Do not rely on implementation summaries as evidence.

Return one of:

- APPROVED
- REMEDIATION REQUIRED
- STOPPED FOR PRODUCT OWNER

Required checks:

- AI02 task classification
- Scope coverage
- Architecture compliance
- Correctness and reliability
- Test quality and regression risk
- Security and credential handling
- Concurrency and resource lifecycle
- Maintainability
- Role separation
- READY FOR MERGE eligibility

AI02 role separation requirements:

- Verify `Primary Developer`.
- Verify `Independent Reviewer`.
- Verify `Developer == Reviewer: FALSE`.
- For Codex-developed work, verify `implementationContextId != independentReviewerContextId`.
- If separation evidence is missing or contradictory, return `STOPPED FOR PRODUCT OWNER` and mark the PR `STOP / NOT READY_FOR_MERGE`.
