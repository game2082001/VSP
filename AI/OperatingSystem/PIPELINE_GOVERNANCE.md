# Pipeline Governance

**Status:** Stable
**Owner:** AI Development Kit
**Established By:** Task-AI01-007 — GB-007 Pipeline Governance Implementation
**Last Updated:** 2026-08-16
**Next Review:** Not scheduled. Governed by the AI Kit Stability Policy (see `AI/VERSION.md`) — further changes require a Governance Backlog entry, opened only when a real Epic exposes a governance defect, and approved by the Product Owner.

---

## 1. Purpose & Scope

This document formalizes the end-to-end Git/GitHub pipeline an AI Agent follows once a Task reaches implementation: the sequence of named Gates between "code exists in the working tree" and "code is merged into `main`," who holds authority at each Gate, and what evidence each Gate requires.

Two of these Gates already existed as governance mechanisms before this document — [`AI_OPERATING_SYSTEM.md`](AI_OPERATING_SYSTEM.md) §23 Commit Gate and §27 Independent Review Policy. This document does not redefine either; it cross-references them and adds the five Gates that sit around them (Push, PR, CI, Automated Review, Merge) which previously existed only as ambient practice (real CI and automated-review workflows already run in this repository — see §3) without a named governance Gate controlling their authority and evidence requirements.

This document sits at the same altitude as `AI_OPERATING_SYSTEM.md` — it governs a single Task's path to `main`. It does not alter, weaken, or replace any Stop Condition (§8), Approval Boundary, Risk Classification (§7), Worktree Safety (§15), Prohibited Action (§16), or Product Owner authority defined there. It also does not alter [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md) — Epic-level sequencing of multiple Tasks is unchanged; this document governs what happens *inside* one Task's own path from Implementation to Merge.

Opened as `GB-007` under the Governance Freeze exception process (`AI/VERSION.md`, AI Kit Stability Policy): the defect this resolves is that CI (`vsp-windows-ci.yml`) and Automated Review (`claude-code-review.yml`) already run in production against every PR, and Push/PR/Merge already carry real authority consequences, yet none of the five had a named Gate, an explicit default, or a stated evidence requirement — only Commit (§23) and Independent Review (§27) did. Approved by the Product Owner for Task-AI01-007.

---

## 2. Relationship to Existing Gates

| Gate | Defined here | Defined elsewhere | Notes |
|---|---|---|---|
| Commit Gate | Cross-referenced only | `AI_OPERATING_SYSTEM.md` §23 | No procedure duplicated |
| Push Gate | New | — | §3 below |
| PR Gate | New | — | §3 below |
| CI Gate | New | — | Maps to `.github/workflows/vsp-windows-ci.yml` |
| Automated Review Gate | New | — | Maps to `.github/workflows/claude-code-review.yml` |
| Independent Review Gate | Timing/sequencing defined here; substance cross-referenced | `AI_OPERATING_SYSTEM.md` §27 | §4 below — this is GB-007's core correction |
| Merge Gate | New | — | Distinct from, and earlier than, Release Gate |
| Release Gate | Not in scope | `AI_OPERATING_SYSTEM.md` §26 | Release Gate (Pilot/GA/Production) governs what happens *after* Merge; unaffected by this document |

---

## 3. The Seven Gates — Pipeline Sequence

```text
Implementation
    v
Local Validation
    v
Commit Gate                  (AI_OPERATING_SYSTEM.md §23)
    v
Push Gate
    v
PR Gate
    v
    +----------------------------+----------------------------------------+
    v                                                                     v
CI Gate                                                       Automated Review Gate
(.github/workflows/vsp-windows-ci.yml)                        (.github/workflows/claude-code-review.yml)
    +----------------------------+----------------------------------------+
                                  v
                       Independent Review Gate   (AI_OPERATING_SYSTEM.md §27 — only if triggered; see §4)
                                  v
                             Merge Gate
```

CI Gate and Automated Review Gate both trigger automatically from the same `pull_request` event and run independently of each other — their relative completion order is not guaranteed and must not be assumed. On Task-AI01-007's own PR (#7), Automated Review Gate consistently finished before CI Gate. Both must independently reach PASS before Independent Review Gate proceeds; neither is sequenced ahead of or behind the other by this document or by the underlying GitHub Actions triggers.

Every other Gate in this sequence — Commit, Push, PR, Independent Review, Merge — remains strictly ordered: a later Gate must not run ahead of an earlier one on the theory that it is "more efficient" — see §4 for why this specifically matters for Independent Review.

### Commit Gate

Governed in full by `AI_OPERATING_SYSTEM.md` §23. Not restated here. Default: an AI Agent does not `git add`/`git commit`. Explicit, task-scoped Product Owner authorization required.

### Push Gate

**Default:** an AI Agent does not `git push`, even while an active Commit Gate authorization exists for the same Task — push has always required its own separate, explicit authorization (§23, unchanged by this document).

**Explicit authorization:** scoped to one Task's already-committed change set, to a named branch. Force-push is never covered by a Push Gate authorization; it remains governed by §15 Worktree Safety and §16 Prohibited Actions.

### PR Gate

**Default:** an AI Agent does not open a Pull Request against `main` (or any protected branch) on its own initiative.

**Explicit authorization:** may be granted alongside Commit/Push authorization for the same Task. The PR description must state the Task ID and reference the approved Task Plan; it must not silently broaden scope beyond what was authorized.

### CI Gate

**Mechanism:** `.github/workflows/vsp-windows-ci.yml` (build + `VSP.Tests` on the self-hosted Windows runner), triggered automatically on the same `pull_request` event as Automated Review Gate — independently of it, not sequentially before or after it (see §3).

**Rule:** a red CI run blocks Independent Review Gate and Merge Gate. It does not block Automated Review Gate — that workflow triggers and runs independently of CI Gate's outcome, and may complete before, during, or after it. A CI failure is triaged per `AI_OPERATING_SYSTEM.md` §12 (new failure / pre-existing failure / environment failure) before any remediation is attempted. An AI Agent must not claim CI passed without observing the actual run result.

### Automated Review Gate

**Mechanism:** `.github/workflows/claude-code-review.yml` (Claude Code Review), triggered automatically on the same `pull_request` event as CI Gate — independently of it, not sequentially before or after it (see §3). It commonly completes before CI Gate finishes; this is expected and neither shortens nor bypasses CI Gate.

**Rule:** Automated Review Gate output is evidence, not a substitute for Independent Review. **Automated Review must never be treated as satisfying a Required Independent Review** (§4 rule 8) — this holds even when Automated Review returns a clean result. Independent Review Gate requires both CI Gate and Automated Review Gate to have independently reached PASS.

### Independent Review Gate

Governed in substance by `AI_OPERATING_SYSTEM.md` §27 (trigger conditions, reviewer responsibilities, mandatory-vs-proportional review). This document governs only *where in the pipeline sequence* it runs, and that placement is GB-007's core correction — see §4.

### Merge Gate

**Default:** Merge authority belongs to the Product Owner (`AI_OPERATING_SYSTEM.md` §2, User role: "Commit / Merge / Push Authority"). An AI Agent does not merge a PR on its own initiative.

**Explicit authorization (Autonomous Merge):** a distinct, separately-grantable authorization from Commit/Push/PR. Granting Commit, Push, and PR Gate authorization for a Task never implies Merge Gate authorization — each Task's grant is evaluated on its own terms, per §5.

**Preconditions, when Autonomous Merge is granted:** CI Gate must be PASS; if Independent Review was triggered (§27), it must be PASS before Merge — never merged while REMEDIATION REQUIRED is outstanding (§4 rule 7).

---

## 4. Independent Review Gate Timing (GB-007 Core Correction)

Independent Review does not run before Commit/Push/PR/CI as a precondition for reaching them. It runs, when triggered, in its actual sequence position: after CI Gate and Automated Review Gate, before Merge Gate.

**Rules:**

1. Independent Review is not a precondition for Commit Gate.
2. Commit / Push / PR / CI may proceed per an already-approved Task Plan without waiting on Independent Review.
3. If `AI_OPERATING_SYSTEM.md` §27 triggers Independent Review, it must complete before Merge Gate.
4. Independent Review must inspect the actual, current final PR diff and the actual CI/Test/Build and Automated Review evidence — not an earlier snapshot.
5. A REMEDIATION REQUIRED verdict permits the Implementation Agent to remediate within the originally approved Scope, then re-run: Local Validation → Commit → Push → [CI Gate || Automated Review Gate] → Independent Review.
6. Every new remediation commit invalidates the prior Independent Review result; re-review is required.
7. Independent Review must be PASS before Merge Gate.
8. Automated Review Gate never substitutes for a Required Independent Review Gate.

**Rationale:** Independent Review's value depends on reviewing the actual merge candidate. If it ran before Commit/Push/PR/CI, subsequent remediation could change the diff those earlier Gates validated, leaving the Independent Review evidence detached from what actually reaches `main`. Running it late — anchored to the real final diff and real CI/Automated-Review evidence — is what keeps the review meaningful rather than ceremonial.

This rule does not change *when* §27 decides Independent Review is triggered (MEDIUM/HIGH risk, architecture change, DB schema, public API, security, or any non-trivial change where the implementer would otherwise be sole judge of its own correctness) — only *where in this pipeline* the triggered review executes.

---

## 5. Scoped Option B (Bootstrap Rollout)

This pipeline is adopted in a deliberately scoped, bootstrapped form rather than granting full end-to-end autonomy in one step:

- A Task may be granted Commit Gate, Push Gate, and PR Gate authorization together, scoped to that Task's approved change set, per the existing per-Gate rules above.
- CI Gate and Automated Review Gate run automatically once a PR exists — they require no separate per-Task grant, since they are read-only automated checks with no merge/write authority of their own.
- Independent Review Gate applies whenever `AI_OPERATING_SYSTEM.md` §27 triggers it, regardless of what else was granted.
- **Merge Gate (Autonomous Merge) is never implied by a Commit/Push/PR grant.** It is a separate, explicitly-grantable authorization, evaluated per Task. Withholding it for a given Task means the Product Owner performs that Task's Merge manually — this is the bootstrap step for a governance mechanism (this document) that is itself still new.
- Task-AI01-007 is the bootstrap instance of Scoped Option B: Commit/Push/PR are authorized; Autonomous Merge is explicitly withheld; the Product Owner merges manually once satisfied.
- A future Task may separately request Autonomous Merge delegation. That request is evaluated on its own terms against this document and `AI_OPERATING_SYSTEM.md` §22 — it is never inherited from a prior Task's Commit/Push/PR grant.

---

## 6. Autonomous Recovery Loop (Pipeline-Scoped)

Extends `AUTONOMOUS_DEVELOPMENT.md` §8 (Autonomous Recovery) to failures at a specific pipeline Gate, within a single Task already inside approved scope:

- **CI Gate failure:** triage per `AI_OPERATING_SYSTEM.md` §12 (new / pre-existing / environment failure). A new failure within approved scope is remediated, then the loop re-enters at Local Validation and proceeds forward through Commit → Push → CI again. A failure that exposes a problem outside approved scope is a Stop Condition (§8, Unrecoverable Build/Test failure) — it escalates, it is not silently absorbed into a wider fix.
- **Independent Review REMEDIATION REQUIRED:** per §4 rules 5–6 above — remediate within the originally approved Scope, then re-run Local Validation → Commit → Push → [CI Gate || Automated Review Gate] → Independent Review. Each cycle is bounded by the same approved Scope; a remediation that would require expanding Scope stops and escalates (`AI_OPERATING_SYSTEM.md` §8 Scope Expansion) instead of being folded in silently.
- The loop has no autonomous exit into Merge: reaching a passing state at every required Gate stops at Merge Gate and awaits its own authorization per §3/§5 — recovery never grants Merge authority as a side effect of resolving an earlier Gate's failure.
- Session interruption mid-pipeline (crash, context loss) is recovered the same way `AUTONOMOUS_DEVELOPMENT.md` §8 already specifies: re-enter through Continuation Mode, re-run Current-State Analysis against actual repository/PR/CI state, never trust a prior session's memory of Gate status over what the repository and CI actually show.

---

## 7. Rollback Plan

This document, together with its two companion edits, is docs-only and carries no runtime or code impact. If any part of it needs to be reverted:

- Delete `AI/OperatingSystem/PIPELINE_GOVERNANCE.md`.
- Revert `AI/OperatingSystem/README.md` and `AI/VERSION.md` to their state at the commit immediately preceding Task-AI01-007.

No other file is touched by this Task, so no other rollback step is required. Rollback authority rests with the Product Owner, consistent with Merge/Commit authority (§3).

---

## 8. Validation Plan

Local Validation for this Task (docs-only) consists of:

- **Cross-reference integrity:** every section reference and file path cited in this document resolves to an actually-existing section or file at the time of writing (`AI_OPERATING_SYSTEM.md` §2/§7/§8/§12/§15/§16/§22/§23/§26/§27; `AUTONOMOUS_DEVELOPMENT.md` §8; `.github/workflows/vsp-windows-ci.yml`; `.github/workflows/claude-code-review.yml`).
- **Non-interference check:** confirm no existing rule text in `AI_OPERATING_SYSTEM.md` or `AUTONOMOUS_DEVELOPMENT.md` is modified by this Task — this document only adds a new layer beside them, the same pattern `AI_OPERATING_SYSTEM.md` §23/§27 already used when added by Task-AI01-006.
- **Structural sanity:** headings, tables, and the pipeline diagram render as valid Markdown.

No code build/test applies — this Task adds no production code.

---

## 9. Relationship to Other Documents

| Document | Relationship |
|---|---|
| [`AI_OPERATING_SYSTEM.md`](AI_OPERATING_SYSTEM.md) | §23 Commit Gate and §27 Independent Review Policy are authoritative for those two Gates' substance; this document only sequences them among the five new Gates and does not restate their procedures |
| [`AUTONOMOUS_DEVELOPMENT.md`](AUTONOMOUS_DEVELOPMENT.md) | §8 Autonomous Recovery is the basis this document's §6 extends to pipeline-Gate-specific failures; Epic-level Task sequencing is otherwise unaffected |
| [`AI/OperatingSystem/README.md`](README.md) | Indexes this document and records `GB-007` in the Governance Backlog |
| [`AI/VERSION.md`](../VERSION.md) | Records the version bump and Phase History entry for this Task |
| `.github/workflows/vsp-windows-ci.yml` | The CI Gate's actual mechanism |
| `.github/workflows/claude-code-review.yml` | The Automated Review Gate's actual mechanism |

---

## 10. Out of Scope

This document does not:

- Grant Autonomous Merge to any Task, including Task-AI01-007 itself (§5).
- Modify `AI_OPERATING_SYSTEM.md` or `AUTONOMOUS_DEVELOPMENT.md`.
- Define Release Gate behavior (`AI_OPERATING_SYSTEM.md` §26 remains authoritative and unchanged).
- Change CI or Automated Review workflow configuration — it only names the existing workflows as the mechanism behind two of the seven Gates.
