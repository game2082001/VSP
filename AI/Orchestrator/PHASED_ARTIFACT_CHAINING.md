# AI02 phased artifact chaining

## Authority boundary

This contract supports the phased recovery of `VSP-AI02-001TI-A`. It does not authorize a child task, publication, Repository Transport, or repository writes. Claude receives no artifact-read or repository-write credential. A trusted acquisition step may receive narrowly scoped artifact-read authority only while acquiring an explicitly bound Actions artifact. An authoritative-repository-merge predecessor is acquired without an artifact-read, GitHub, App, or repository-write credential.

The three child ownership sets are immutable:

- A1 owns the four request/decision schema and template files.
- A2 owns `tools/orchestrator/artifact-intake-contract.ps1`.
- A3 owns `tools/orchestrator/test-artifact-intake-contract.ps1` and `AI/Orchestrator/ARTIFACT_INTAKE_SCHEMA.md`.

Predecessor files are readable inputs, never child output. A later phase must fail if any predecessor byte changes.

## Descriptor identity

`ai02-predecessor-descriptor.schema.json` binds a child package to its task, phase, recovery repository SHA, workflow/run/attempt, artifact identity and digest, package and manifest hashes, exact owned files, per-file hashes/sizes/modes, validation result, and parent aggregate-state digest. Mutable branch names are not identities.

Descriptors are accepted only after the GitHub artifact metadata, downloaded outer archive, publication package, publication manifest, and every file agree with the descriptor. Unknown versions and malformed or extra fields fail closed.

## Predecessor source binding

`ai02-predecessor-binding.schema.json` is a closed discriminator with exactly two source types. Missing, unknown, ambiguous, or mixed source fields are rejected.

`ACTIONS_ARTIFACT` preserves the existing acquisition contract: immutable run/attempt and descriptor/package artifact identities and digests, recovery SHA, workflow provenance recovered from the validated descriptor and GitHub run, safe archive extraction, package/manifest agreement, and scoped artifact-read credential use.

`AUTHORITATIVE_REPOSITORY_MERGE` binds the repository, merge commit, exact ordered parents, published production head, predecessor task/phase/sequence and historical execution SHA, checkpoint version and bytes, descriptor/package/manifest/result hashes, and the exact production file set. Each file binds its repository path, `100644` mode, Git blob ID, byte size, and SHA-256. The canonical checkpoint, descriptor, and package-result evidence travels in the binding; its decoded bytes must reproduce the bound hashes exactly.

Repository-merge acquisition uses only immutable objects already present in the credentialless checkout. It verifies the exact two-parent publication merge, both parent positions, the published-head and merge trees, ancestry to the actual child execution commit, and the unchanged child-execution tree. A missing object fails closed and is never fetched. Verified Git blob bytes are then materialized into the workspace so checkout line-ending conversion cannot alter the predecessor evidence.

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

The schema 2.0 operations create or validate checkpoint evidence only. PM1 routes and materializes predecessor sources, then consumes those AGV2 operations without redefining the checkpoint model. For a repository-merge A2 child, the immutable A1 v1 checkpoint and A1 publication are verified separately; after the child package is built, AGV2 creates the A2 schema 2.0 checkpoint with the actual A2 execution SHA. The A1 checkpoint bytes and historical execution SHA are never rewritten.

## Materialization and child changes

The trusted phase runner performs these steps:

1. Check out the immutable recovery SHA without persisted credentials.
2. Classify the closed predecessor source binding and select exactly one acquisition route.
3. Acquire Actions predecessors by immutable run/artifact identity with the scoped artifact-read credential, or verify repository-merge predecessors from immutable local Git objects with no credential.
4. Verify metadata, canonical evidence, Git provenance, modes, sizes, blobs, and hashes before materializing files.
5. Reject unsafe, duplicate, colliding, missing, extra, oversized, or invalid-mode inputs.
6. Materialize only validated predecessor files in the isolated aggregate workspace.
7. Write a deterministic baseline containing predecessor hashes and the aggregate/checkpoint digest.
8. Export SHA-256 bindings for the baseline and aggregate/checkpoint bytes through immutable pre-Claude step outputs.
9. Remove artifact-read credentials before Claude starts; repository-merge acquisition never receives one.
10. Allow Claude to change only the current child ownership set.
11. Verify the baseline/state byte bindings and predecessor hashes are unchanged after Claude.
12. Reject links, reparse points, non-regular files, and any actual filesystem or Git mode other than `100644`.
13. Determine child changes from Git status after subtracting only verified immutable predecessor paths.
14. Require exact equality with the child allowlist and package only those child files.

Predecessor materialization is not publication and is never represented as a child change.

## Final aggregate gate

The final gate accepts only the exact seven-file union. It verifies lineage, ownership, hashes, sizes, actual regular-file modes, predecessor immutability, and the P0 identities, policies, and numeric ceilings. It then invokes the deterministic focused command `pwsh -NoProfile -File tools/orchestrator/test-artifact-intake-contract.ps1`.

The focused suite must exit zero and emit a final one-line JSON evidence record with schema `vsp.ai02.artifact-intake-focused-suite/1.0`. The record binds the suite identity, test/failed counts, the exact required security-check set, and the complete P0 policy/ceiling values. A no-op script, missing check, altered policy value, malformed evidence, or nonzero exit rejects completion.

Only a `FULLY_VALIDATED_FINAL_AGGREGATE` may later be submitted to a separately authorized one-time bootstrap publication. Child artifacts and failed aggregates are never publication inputs.
