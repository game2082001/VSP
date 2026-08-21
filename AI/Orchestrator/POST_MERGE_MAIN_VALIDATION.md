# Post-Merge Main Validation

Post-merge validation is a read-only evidence path for the exact `main` commit created by a PR merge.

## Trigger

`VSP Windows CI` runs on:

- `pull_request` targeting `main`
- `push` to `main`
- manual `workflow_dispatch`

The `push` trigger exists so a PR merge that advances `main` creates Windows CI evidence for the resulting main HEAD SHA.

## Rules

- The validation target is `github.sha`.
- The workflow records `GITHUB_EVENT_NAME`, `GITHUB_REF`, and `GITHUB_SHA` before build/test.
- The workflow uses read-only repository permissions.
- The workflow does not commit, push, open PRs, request reviews, invoke implementation remediation, or merge.
- A post-merge failure is evidence only. It must not modify `main` or start automatic remediation.

## Separation

- Claude Automated Review remains a PR lifecycle gate and is not triggered by main push validation.
- Codex Independent Review remains a PR lifecycle gate and is not triggered by main push validation.
- AI01 Orchestrator routing remains PR/manual/comment driven and is not triggered by main push validation.

## Recursion

Main validation does not write to the repository, so it cannot trigger itself through a follow-up push. A normal PR receives one PR CI run, and after merge receives one main-head CI run for the merge commit.
