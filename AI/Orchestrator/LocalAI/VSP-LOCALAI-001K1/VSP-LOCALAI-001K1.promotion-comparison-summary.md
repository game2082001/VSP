# VSP-LOCALAI-001K1 Promotion Validation Comparison Report

Task classification: `MEDIUM`

Starting main: `bbf0a493eff5d92dc385836b37b5c99373d0fd7f`

## Methodology

- Models: `qwen3:8b` vs `qwen2.5-coder:7b`
- Cases: `10` historical cases, with `3` seen, `7` holdout, and `6` core holdout excluding K-CASE10
- Runs: `5` per case per model, `100` total
- Context: `4096`
- Temperature: `0`
- Structured output: Ollama JSON Schema
- Prompt/evidence mode: `Simplified`
- Result-classification rubric: `Calibrated`
- Changed variable: `MODEL` only

## Dataset

- FINDINGS: `6`
- PASS: `3`
- INCONCLUSIVE: `1`
- K-CASE6 preserves the historical-time boundary and excludes later B7/B8 evidence.
- K-CASE9 is a completed workflow-run PASS control; PR #48 lifecycle status is not used as the ground truth.
- K-CASE10 is a secondary holdout control and cannot independently drive promotion.

## Top-Level Metrics

| Metric | `qwen3:8b` | `qwen2.5-coder:7b` |
|---|---:|---:|
| Schema compliance | `1.0` | `0.8` |
| JSON parse success | `1.0` | `1.0` |
| Expected result-class accuracy | `0.5` | `0.8` |
| Seen accuracy | `0.6667` | `0.6667` |
| All-holdout accuracy | `0.4286` | `0.8571` |
| Core-holdout accuracy | `0.5` | `0.8333` |
| Known/key-concept consistency | `0.6` | `0.9` |
| Known-defect detection | `0.6667` | `0.6667` |
| PASS false-positive rate | `0.0` | `0.0` |
| Authority violation rate | `0.0` | `0.2` |
| Unsupported claim rate | `0.0` | `0.0` |
| Contradiction rate | `0.0` | `0.0` |
| Median latency | `20009 ms` | `4978 ms` |
| P95 latency | `57385 ms` | `9784 ms` |

## Threshold Evaluation

`qwen2.5-coder:7b` did not satisfy the frozen promotion thresholds.

Failed thresholds:

- Schema compliance required `100%`; observed `0.8`.
- Overall expected result-class accuracy required `>= 90%`; observed `0.8`.
- Core holdout excluding K-CASE10 required `>= 85%`; observed `0.8333`.
- Authority violations required `0`; observed `0.2`.

Passed notable thresholds:

- JSON parse success: `1.0`
- All-holdout expected result-class accuracy: `0.8571`
- Findings concept detection: `0.9`
- PASS false-positive FINDINGS: `0`
- K-CASE6 ambiguity handling: target met
- Hallucinated paths: `0`
- Unsupported claims: `0`
- Contradiction rate: `0`
- Timeout rate: `0`

## Recommendation

`QWEN2_5_CODER_7B_PROMOTION_NOT_VALIDATED`

qwen2.5-coder:7b materially improves speed and all-holdout result accuracy compared with qwen3:8b, but it is not promotion-qualified on the approved expanded dataset because it fails structured reliability, overall/core-holdout accuracy, and authority-safety thresholds.

## Boundaries

- Local AI repository write: `FALSE`
- Local AI GitHub authority: `FALSE`
- Production credential access: `FALSE`
- Live PR integration: `FALSE`
- Default model changed: `FALSE`
- Baseline remains: `qwen3:8b`
- Context remains: `4096`
- Firewall/Ollama runtime changed: `FALSE`
- Existing VSP gates changed: `FALSE`
- `VSP-LOCALAI-001C = NOT_STARTED / NOT_AUTHORIZED`
