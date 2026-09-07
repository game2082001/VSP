# VSP-LOCALAI-001M Authority-Neutral Finding Language Remediation Benchmark

## Disposition

`QWEN2_5_CODER_7B_REMEDIATION_NOT_VALIDATED`

The generic authority-neutral analytical-language instruction did not resolve qwen2.5-coder:7b promotion blockers. K-CASE2 and K-CASE5 remained 0/5 for expected result-class accuracy, 0/5 schema-compliant, and 5/5 authority-text violations. PASS controls and K-CASE6 safety behavior were preserved, but the unchanged promotion thresholds were not satisfied.

## Methodology

- Dataset: exact frozen K1 10-case dataset
- Models: qwen2.5-coder:7b and qwen3:8b
- Runs: 5 per case per model, 100 total
- Context: 4096
- Temperature: 0
- Stream: false
- Structured-output schema: unchanged
- Scorer and authority scanner: unchanged
- Principal experimental change: generic authority-neutral analytical-language instruction

## qwen2.5-coder:7b After 001M

- Schema compliance: `0.8`
- JSON parse success: `1`
- Overall expected result-class accuracy: `0.8`
- Seen accuracy: `0.6667`
- All-holdout accuracy: `0.8571`
- Core-holdout excluding K-CASE10 accuracy: `0.8333`
- Known-concept consistency: `0.9`
- Known-defect detection: `0.6667`
- PASS false-positive FINDINGS: `0`
- Authority violation rate: `0.2`
- Hallucinated path rate: `0`
- Unsupported claim rate: `0`
- Contradiction rate: `0`
- Median latency: `4675 ms`
- P95 latency: `8998 ms`

## qwen3:8b Authority-Neutral Comparator

- Schema compliance: `0.88`
- JSON parse success: `1`
- Overall expected result-class accuracy: `0.4`
- All-holdout accuracy: `0.4286`
- Core-holdout excluding K-CASE10 accuracy: `0.5`
- Authority violation rate: `0.12`
- Median latency: `27234 ms`
- P95 latency: `72987 ms`

## Focus Cases

- K-CASE2: qwen2.5-coder remained 0/5 FINDINGS, 5/5 INCONCLUSIVE, 5/5 authority-text violations.
- K-CASE5: qwen2.5-coder remained 0/5 FINDINGS, 5/5 INCONCLUSIVE, 5/5 authority-text violations.
- K-CASE6: qwen2.5-coder preserved the ambiguity boundary with 5/5 INCONCLUSIVE and 0/5 authority violations.
- PASS controls: qwen2.5-coder preserved 15/15 expected PASS behavior with 0 false-positive FINDINGS.

## Boundaries

- Local AI repository write: false
- Local AI GitHub authority: false
- Live PR integration: false
- Default model changed: false
- Baseline model: qwen3:8b
- Context: 4096
- Scanner weakened: false
- Promotion thresholds changed: false
- Firewall/Ollama runtime changed: false
- Existing VSP gates changed: false
- VSP-LOCALAI-001C: NOT_STARTED
