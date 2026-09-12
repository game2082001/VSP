# AI02 phased artifact chaining

## Authority boundary

This contract supports the phased recovery of `VSP-AI02-001TI-A`. It does not authorize a child task, publication, Repository Transport, or repository writes. Claude receives no artifact-read or repository-write credential. A trusted acquisition step may receive narrowly scoped artifact-read authority only while acquiring an explicitly bound predecessor artifact.

The three child ownership sets are immutable:

- A1 owns the four request/decision schema and template files.
- A2 owns `tools/orchestrator/artifact-intake-contract.ps1`.
- A3 owns `tools/orchestrator/test-artifact-intake-contract.ps1` and `AI/Orchestrator/ARTIFACT_INTAKE_SCHEMA.md`.

Predecessor files are readable inputs, never child output. A later phase must fail if any predecessor byte changes.

## Descriptor identity

`ai02-predecessor-descriptor.schema.json` binds a child package to its task, phase, recovery repository SHA, workflow/run/attempt, artifact identity and digest, package and manifest hashes, exact owned files, per-file hashes/sizes/modes, validation result, and parent aggregate-state digest. Mutable branch names are not identities.

Descriptors are accepted only after the GitHub artifact metadata, downloaded outer archive, publication package, publication manifest, and every file agree with the descriptor. Unknown versions and malformed or extra fields fail closed.

## Canonical aggregate state

The canonical digest input is UTF-8 JSON without a byte-order mark or trailing newline. Properties appear in this fixed order:

1. `schemaVersion`
2. `recoveryRepositorySha`
3. `predecessors`
4. `fileOwnership`

Predecessors are ordered by numeric `sequence` and then ordinal `childTaskId`. Every descriptor uses its schema property order. `ownedFiles` and `fileOwnership` are ordered by ordinal repository path. No insignificant whitespace is emitted. The digest is:

`aggregateStateDigest = "sha256:" + lowercase SHA256(canonical digest-input bytes)`

The persisted aggregate state appends `aggregateStateDigest` as the final property. Equivalent logical inputs therefore produce identical canonical bytes and digests regardless of input descriptor order.

A1 must bind `GENESIS`. A2 must bind the verified A1 aggregate digest. A3 must bind the verified A1+A2 aggregate digest. A changed predecessor digest makes every descendant bound to the earlier lineage stale.

## Incremental immutable checkpoint chain (schema 2.0)

Every newly authorized child after AGV2 uses one schema 2.0 checkpoint for one transition. A checkpoint contains three deliberately separate authorities: `parentCheckpoint` identifies the exact preceding checkpoint and its terminal child; `parentPublication` identifies the authoritative repository merge that published that terminal child's files; and `child` identifies the next child's actual immutable execution commit and package evidence. These repository identities are cross-validated and are never collapsed into a single recovery SHA.

The historical A1 schema 1.0 aggregate is an immutable import checkpoint. Its original bytes, file SHA-256, internal digest, terminal descriptor bytes, GENESIS lineage, execution commit, package hashes, and production merge are pinned. It is neither rewritten nor silently migrated. Existing authorized schema 1.0 same-base operations remain available, but schema 2.0 cannot be converted back to schema 1.0.

Checkpoint construction is incremental:

`D1 + M1 -> D2`

`D2 + M2 -> D3`

Publication evidence is therefore an input to the next transition; an existing checkpoint is never mutated to add a later publication. Full-lineage validation requires the ordered canonical bytes for every checkpoint, terminal descriptor, package result, and intervening publication. Missing, truncated, duplicated, skipped, or downgraded evidence fails closed.

For schema 2.0, `aggregateStateDigest` is `sha256:` plus the lowercase SHA-256 of the canonical checkpoint bytes excluding only that final property. Canonical bytes use fixed property order, compressed JSON, UTF-8 without BOM or trailing newline, ordinal file ordering, unchanged ordered merge parents, canonical integers, and no timestamps, randomness, environment values, or local paths. Semantically equivalent but noncanonical authoritative bytes are rejected.

Publication verification uses only immutable local Git objects. It requires a two-parent merge whose first parent is the terminal child's execution commit and whose second parent is the published production head, an exact merge diff equal to the terminal ownership set, exact `100644` blobs/sizes/SHA-256 values at both the merge and next child execution trees, and ancestry from the publication merge to the next child execution commit. A missing object fails; validation never fetches a replacement.

The schema 2.0 operations create or validate checkpoint evidence only. They do not acquire artifacts, materialize repository-merge predecessors, publish files, invoke Repository Transport, or change the Artifact Developer workflow. Those acquisition and workflow responsibilities remain with the separately authorized PM1 lifecycle.

## Materialization and child changes

The trusted phase runner performs these steps:

1. Check out the immutable recovery SHA without persisted credentials.
2. Acquire each predecessor by immutable run and artifact identity.
3. Verify metadata and hashes before materializing files.
4. Reject unsafe, duplicate, colliding, missing, extra, oversized, or invalid-mode archive entries.
5. Materialize only validated predecessor files in the isolated aggregate workspace.
6. Write a baseline containing predecessor hashes and the aggregate-state digest.
7. Export SHA-256 bindings for the baseline and aggregate-state bytes through immutable pre-Claude step outputs.
8. Remove artifact-read credentials before Claude starts.
9. Allow Claude to change only the current child ownership set.
10. Verify the baseline/state byte bindings and predecessor hashes are unchanged after Claude.
11. Reject links, reparse points, non-regular files, and any actual filesystem or Git mode other than `100644`.
12. Determine child changes from Git status after subtracting only verified immutable predecessor paths.
13. Require exact equality with the child allowlist and package only those child files.

Predecessor materialization is not publication and is never represented as a child change.

## Final aggregate gate

The final gate accepts only the exact seven-file union. It verifies lineage, ownership, hashes, sizes, actual regular-file modes, predecessor immutability, and the P0 identities, policies, and numeric ceilings. It then invokes the deterministic focused command `pwsh -NoProfile -File tools/orchestrator/test-artifact-intake-contract.ps1`.

The focused suite must exit zero and emit a final one-line JSON evidence record with schema `vsp.ai02.artifact-intake-focused-suite/1.0`. The record binds the suite identity, test/failed counts, the exact required security-check set, and the complete P0 policy/ceiling values. A no-op script, missing check, altered policy value, malformed evidence, or nonzero exit rejects completion.

Only a `FULLY_VALIDATED_FINAL_AGGREGATE` may later be submitted to a separately authorized one-time bootstrap publication. Child artifacts and failed aggregates are never publication inputs.
