# Required Independent Review Request

Task: AI01-008
Role: Codex Independent Reviewer
Default model: gpt-5.6-luna medium

Review the PR using read-only credentials. Inspect actual repository state, diff, checks, tests, and review artifacts. Do not rely on implementation summaries as evidence.

Return one of:

- APPROVED
- REMEDIATION REQUIRED
- STOPPED FOR PRODUCT OWNER

Required checks:

- Scope coverage
- Architecture compliance
- Correctness and reliability
- Test quality and regression risk
- Security and credential handling
- Concurrency and resource lifecycle
- Maintainability
- Role separation
- READY FOR MERGE eligibility
