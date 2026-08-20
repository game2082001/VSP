# Stop Conditions

**Scope:** Reusable AI01 task lifecycle

The Router must stop and request Product Owner input when any condition below is reached:

- Scope Expansion.
- Product decision.
- Architecture decision outside approved scope.
- Security or authorization model decision.
- Credential decision.
- Database schema change.
- Public API or breaking contract change.
- External package or licensing decision.
- New high-impact dependency or infrastructure change not authorized by the Task Plan.
- Unrecoverable build/test failure.
- Windows self-hosted runner unavailable and the cause cannot be classified.
- Independent Review evidence unavailable.
- Agent authentication failure.
- Hardware or real-device validation is required.
- Destructive operation.
- Remediation loop limit exceeded.
- Token budget hard limit reached.
- Repeated identical finding beyond the configured limit.
- Agent conflict cannot be resolved by the authority order.
- Required role separation cannot be guaranteed.
- Merge Gate.

Each stop must render the fixed `PRODUCT OWNER DECISION REQUIRED` format from `DECISION_UX.md`, including Reason, Recommended, Why, If approved, and Alternatives. The Router must provide a recommended option and must not ask an open-ended question.

Normal queued, pending, or in-progress CI and Automated Review gates are not Stop Conditions before the configured timeout/tolerance. The Router must poll and wait without Product Owner input while gate execution is healthy.

Protected PRs and paused investigations remain out of scope unless the Product Owner explicitly includes them in the approved task plan.
