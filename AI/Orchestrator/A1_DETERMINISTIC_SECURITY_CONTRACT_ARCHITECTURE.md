# A1 Deterministic Security-Contract Replacement Architecture

## Status and authority

This document is the design record for `VSP-AI02-001TI-A1-DS1`. It defines a replacement architecture; it does not implement a policy source, generator, generated contract, or publication path.

The original `VSP-AI02-001TI-A1` identity is retired from direct model-authored security-contract execution. Its initial and cause-specific remediation attempts are consumed. Neither historical artifact is normative or eligible for aggregate admission.

The Product Owner-approved P0/P1 policy is normative. Future generated schemas and templates are derived artifacts. Model output is never a source of normative policy.

## Replacement task family

The replacement uses three explicit tasks:

1. `VSP-AI02-001TI-A1D-POLICY` — define and approve the canonical machine-readable policy and its meta-schema.
2. `VSP-AI02-001TI-A1D-GEN` — implement the deterministic generator/compiler and generator-owned tests.
3. `VSP-AI02-001TI-A1D-VALIDATE` — execute generation, validate the exact four-file child, obtain independent review, and package it through P2 as the A1 replacement GENESIS child.

These are replacement tasks, not A1 attempt 3. Each requires its own Product Owner authorization fixture before implementation.

## Future file identities

The future canonical policy and implementation paths are fixed as follows:

- Canonical policy source: `AI/Orchestrator/Policy/artifact-intake-policy.json`
- Policy-source meta-schema: `AI/Orchestrator/Policy/artifact-intake-policy.schema.json`
- Deterministic generator: `tools/orchestrator/generate-artifact-intake-contract.ps1`
- Deterministic tests: `tools/orchestrator/test-generate-artifact-intake-contract.ps1`

The generator eventually owns exactly these four derived A1 child outputs:

- `AI/Orchestrator/Templates/artifact-intake-request.schema.json`
- `AI/Orchestrator/Templates/artifact-intake-request.template.json`
- `AI/Orchestrator/Templates/artifact-intake-decision.schema.json`
- `AI/Orchestrator/Templates/artifact-intake-decision.template.json`

Policy, meta-schema, generator, and generator tests are governance/infrastructure inputs. They do not enter the four-file A1 aggregate ownership set.

## Normative policy model

The policy source must be strict, reject unknown fields, and encode all of the following without model-selected values.

### Identity and provenance

- request schema ID `vsp.ai02.artifact-intake-request/1.0`
- decision schema ID `vsp.ai02.artifact-intake-decision/1.0`
- policy version `vsp-ai02-intake-v1`
- repository, task, source SHA, workflow ID, run ID, run attempt, artifact ID, and artifact-name models
- GitHub artifact digest as the primary acquisition anchor
- required package, authorization-manifest, authorization-state, request, and artifact hash bindings
- optional inner pins only from a separately trusted source
- producer assertions never grant write, transport, publication, or merge authority

### Repository path policy

A file path is printable ASCII, non-empty, repository-relative, and at most 240 characters. Each segment is non-empty and at most 100 characters. Leading slash, backslash, colon, empty segment, `.` segment, `..` segment, trailing slash, control characters, and non-ASCII characters are rejected. The same definition governs authorization paths, approved files, inner-pin paths, and every other repository-relative path-bearing request field.

### Numeric ceilings

| Name | Exact value |
|---|---:|
| `maxOuterArtifactCompressedBytes` | 26214400 |
| `maxInnerPublicationZipCompressedBytes` | 20971520 |
| `maxTotalInnerUncompressedBytes` | 52428800 |
| `maxChangedFiles` | 200 |
| `maxPerFileUncompressedBytes` | 10485760 |
| `maxManifestJsonBytes` | 262144 |
| `maxResultJsonBytes` | 262144 |
| `maxRepositoryRelativePathCharacters` | 240 |
| `maxPathSegmentCharacters` | 100 |
| `maxJsonNestingDepth` | 16 |
| `maxSanitizedEvidenceArtifactBytes` | 1048576 |
| `maxCompressionRatio` | 100 |
| `requiredOuterPublicationFileCount` | 3 |

### Replay, stale-input, and transport policy

- source policy is `EXACT_BASE_ONLY`
- an empty consumed-identity collection means first use/not yet consumed
- a matching successfully consumed identity is rejected as replay
- stale packages require regeneration and reauthorization; they are never silently advanced
- unknown or unsupported input fails closed
- handoff is `VALIDATED_INLINE_BYTES`
- interface decision is `001T_INTERFACE_MINIMAL_EXTENSION_REQUIRED`
- the Task Manifest/trusted governance record is the transport authority source
- Trusted Intake does not invoke Repository Transport

### Credential invariants

- Claude GitHub write authority: false
- Claude artifact-read credential: false unless a later architecture is separately approved
- Trusted Intake repository write authority: false
- Trusted Intake branch/PR authority: false
- checkout persisted credentials: false
- Repository Transport is the sole automated repository-write boundary
- automated merge authority: false
- Product Owner merge authority: true

## Normative decision state machine

`transportInvoked` is globally false in Trusted Intake.

### `REJECTED`

- `decision = REJECTED`
- `transportAuthorized = false`
- `transportInvoked = false`
- the request reference and request SHA binding are present
- artifact/package/manifest/state provenance remains present where validation reached those inputs
- `failureCategory != NONE`
- no successful transport-ready status is permitted

### `ACCEPTED_FOR_TRANSPORT`

- `decision = ACCEPTED_FOR_TRANSPORT`
- `transportAuthorized = true`
- `transportInvoked = false`
- request reference and valid request SHA binding are present
- artifact identity and GitHub artifact/package/manifest/state digest bindings are present and valid
- validation status is the approved valid-package state
- `failureCategory = NONE`

A future `transportRequestSha256` may be documented as a potential handoff binding, but it cannot be added to the normative contract without a separate Product Owner policy decision.

## Authorship boundaries

Immutable Product Owner policy includes identifiers, required provenance, numeric ceilings, authority boundaries, state-machine semantics, replay/stale rules, path grammar, credential invariants, and Transport boundaries.

The deterministic generator produces required arrays, properties, constants, enums, numeric constraints, conditional clauses, shared definitions, template policy blocks, safe examples, and conformance vectors.

Models may author non-normative descriptions, explanatory documentation, and test suggestions that cannot change policy. Claude is a mandatory security cross-reviewer for the policy/generator implementation and the final four-file replacement candidate; Claude is not the substantive normative author.

## Generator determinism and validation

The future generator must provide:

- byte-identical output for identical policy input
- UTF-8 without BOM and LF line endings
- fixed property, array, and file ordering
- no timestamps, random IDs, environment-derived values, or network dependency
- fail-closed behavior for unknown or missing policy fields
- validation of policy input against the generator-owned meta-schema
- validation of generated output against generator-owned meta-contracts
- repeat-generation byte comparison
- exhaustive positive and negative path vectors across every path-bearing field
- exhaustive accepted/rejected decision-state combinations
- exact checks for identifiers, provenance fields, limits, replay/stale semantics, credentials, and Transport boundaries

## Historical reference evidence

Historical artifacts are `DIGEST_PINNED_READ_ONLY_REFERENCE` forensic inputs only.

### Run `34480651384`

- workflow run: `34480651384`
- source SHA: `e595a3c7beed6d58723d4ef1cfc2ad4b1f606b56`
- publication artifact ID: `10153855735`
- GitHub artifact digest: `sha256:a05e506c0d7e956ccca58e22869d35aecd20ee8af6de5a23635d89fdb64c08ca`
- package SHA-256: `f24d8e75e79cd35af18c7a83de784574bab572c523e8f4b69c96066454c00454`
- manifest SHA-256: `8cd65fbe2c26280d9881ba5f06dd5cb9ffcca586db21cbfab6b38d715054c2da`
- request schema: `3e5adce9f1353d3b08a7d41a5811e96945c921ef9d0472f898bd575f68168637`
- request template: `15b4fb98f6012780c010cf142f10d958df3364e0e3ca590c20919e96cc86cf92`
- decision schema: `8f14e4fde76c5a4f65a8ef3d4661a2f156cbdb931451af1313c860bd0ff7a332`
- decision template: `9142e301b8c41847f6622e228d49aa2ff82d0b1fb4fb01f6aa876196eb17c5f1`
- permitted use: recovery of known-good structural/security fields
- defect: `A1_REQUEST_SCHEMA_PATH_POLICY_DEFECT`

### Run `34490250854`

- workflow run: `34490250854`
- source SHA: `d2de098a6920c50bbc8ac5d8442ea113ab9cce71`
- publication artifact ID: `10157773359`
- GitHub artifact digest: `sha256:b36c419c472a1eb2990a33d0b20de21fccf0f7defea038f926375ab635b5771b`
- package SHA-256: `fd719d4e57a98792c92cf9acdd54d3fbc3ac865a8435a58fcc68fb9e9987f5c6`
- manifest SHA-256: `c504a486580f47a4e5d3d021f22932e70b44089396b72d7b23ac225c14fe3eb9`
- request schema: `73ea3bde1a762aa241e7dced17028b9008890f8418c305a595e87cc58c4da9ca`
- request template: `74488aedf2a70b4561865e1c68610fa86e0f25f4d3abdf2c659a2fa8bc245bd4`
- decision schema: `0160cf69cdc965727107ecca827e1de694a5fcb38de34d74b80e589b835f5945`
- decision template: `d5c361da009db1de6f67d0f55dd1edb611d8b3a3b478a5f0a0a44101c6fba799`
- permitted use: validated path grammar, path vectors, and prohibited-drift evidence
- disposition: non-path security regression; independent review failed

Neither package can become authoritative, be repaired and relabeled, be published, or enter aggregate lineage.

## Attempt budgets and developer lifecycle

- Original A1: exhausted permanently; no reset and no attempt 3.
- `A1D-POLICY`: initial implementation plus at most one cause-specific remediation; no automatic retry.
- `A1D-GEN`: initial implementation plus at most one cause-specific remediation; no automatic retry.
- `A1D-VALIDATE`: one semantic generation/admission attempt. A rerun is permitted only for a proven infrastructure execution failure before semantic output, using the same immutable source and deterministic inputs. Semantic failure stops for Product Owner review and requires a newly authorized remediation task.

The policy/generator implementation developer must be separately assigned by the Product Owner. Codex is recommended for deterministic implementation under an explicit CRITICAL bootstrap exception; Developer must not equal the Separate Codex Independent Reviewer. Claude performs mandatory security cross-review. Product Owner remains sole merge authority.

## P2 and publication lifecycle

P2, P2-R1, and P2-R2 remain complete and unchanged. No P2 tooling change is required. P2 must explicitly authorize the replacement child task ID `VSP-AI02-001TI-A1D-VALIDATE` as phase A1, sequence 1, predecessors `[]`, and parent lineage `GENESIS`; this is an authorization mapping/fixture, not a P2 implementation change.

After deterministic generation and validation, the exact four-file artifact follows: Separate Codex Independent Artifact Review, P2 descriptor and GENESIS append, aggregate-admission review, later A2/A3 lineage, final seven-file aggregate validation, mandatory Claude Cross Review, Repository Transport, PR/CI/reviews, and Product Owner merge. No child artifact is published directly.

`VSP-AI02-001TI-A2` remains blocked until the replacement four-file child is fully validated and admitted to valid P2 GENESIS lineage.
