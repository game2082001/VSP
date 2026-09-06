# VSP-LOCALAI-001J Model Comparison Summary

Task classification: `MEDIUM`

Starting main: `a0c57457b4b94b4543ff7792d8773296b5cdabe6`

## Methodology

- Endpoint: `http://192.168.0.21:11434`
- Runtime: `Ollama 0.33.2`
- Hardware target: Windows, RTX 3050 8GB, 64GB RAM
- Context: `4096`
- Temperature: `0`
- Stream: `false`
- Structured output: Ollama JSON Schema
- Prompt/evidence mode: `Simplified`
- Result-classification rubric: `Calibrated`
- Dataset: exactly CASE1, CASE2, CASE3 from the existing Local AI replay harness
- Runs: `5` per case per model, `45` total

## Model Identity

| Model | Digest | Size bytes | Parameters | Quantization | Context length | Downloaded in 001J |
|---|---:|---:|---:|---:|---:|---:|
| `qwen3:8b` | `500a1f067a9f782620b40bee6f7b0c89e17ae61f686b92c24933e4ca4b2b8b41` | `5225388164` | `8.2B` | `Q4_K_M` | `40960` | `FALSE` |
| `qwen2.5-coder:7b` | `dae161e27b0e90dd1856c8bb3209201fd6736d8eb66298e75ed87571486f4364` | `4683087561` | `7.6B` | `Q4_K_M` | `32768` | `TRUE` |
| `llama3.1:8b` | `46e0c10c039e019119339687c3c1757cc81b9da49709a3b3924863ba87ca666e` | `4920753328` | `8.0B` | `Q4_K_M` | `131072` | `TRUE` |

## Comparison

| Metric | `qwen3:8b` | `qwen2.5-coder:7b` | `llama3.1:8b` |
|---|---:|---:|---:|
| Schema compliance | `15/15` | `15/15` | `15/15` |
| JSON parse success | `15/15` | `15/15` | `15/15` |
| Expected result-class accuracy | `10/15` | `15/15` | `10/15` |
| Result-class consistency | `0.6667` | `1.0` | `0.6667` |
| Known-defect detection | `10/10` | `10/10` | `10/10` |
| CASE1 detection | `5/5` | `5/5` | `5/5` |
| CASE2 detection | `0/5 FINDINGS`, `5/5 concept` | `5/5 FINDINGS`, `5/5 concept` | `5/5 FINDINGS`, `5/5 concept` |
| Control false positives | `0/5` | `0/5` | `0/5` |
| CASE3 expected PASS | `5/5` | `5/5` | `0/5` |
| INCONCLUSIVE rate | `0.3333` | `0.0` | `0.3333` |
| Known-concept consistency | `1.0` | `1.0` | `1.0` |
| Grounding consistency | `1.0` | `1.0` | `1.0` |
| Contradiction rate | `0.0` | `0.0` | `0.0` |
| Hallucinated paths | `0/15` | `0/15` | `0/15` |
| Unsupported claims | `0/15` | `0/15` | `0/15` |
| Authority violations | `0/15` | `0/15` | `0/15` |
| Analytical digest equality | `0.6667` | `0.3333` | `0.6667` |
| Median latency | `23558 ms` | `4421 ms` | `5067 ms` |
| P95 latency | `35879 ms` | `25351 ms` | `9256 ms` |
| Timeout rate | `0.0` | `0.0` | `0.0` |
| OOM/failure behavior | none observed | none observed | none observed |
| Semantic repeatability | `SEMANTICALLY_UNSTABLE` | `SEMANTICALLY_STABLE` | `SEMANTICALLY_UNSTABLE` |

## Per-Case Distribution

| Model | CASE1 expected FINDINGS | CASE2 expected FINDINGS | CASE3 expected PASS |
|---|---:|---:|---:|
| `qwen3:8b` | `0 PASS / 5 FINDINGS / 0 INCONCLUSIVE` | `0 PASS / 0 FINDINGS / 5 INCONCLUSIVE` | `5 PASS / 0 FINDINGS / 0 INCONCLUSIVE` |
| `qwen2.5-coder:7b` | `0 PASS / 5 FINDINGS / 0 INCONCLUSIVE` | `0 PASS / 5 FINDINGS / 0 INCONCLUSIVE` | `5 PASS / 0 FINDINGS / 0 INCONCLUSIVE` |
| `llama3.1:8b` | `0 PASS / 5 FINDINGS / 0 INCONCLUSIVE` | `0 PASS / 5 FINDINGS / 0 INCONCLUSIVE` | `0 PASS / 0 FINDINGS / 5 INCONCLUSIVE` |

## Decision

`qwen2.5-coder:7b` is the only tested candidate that materially improved CASE2 while preserving control precision, schema reliability, grounding, zero hallucinated paths, zero unsupported claims, and zero authority violations.

Final recommendation:

`RECOMMEND_QWEN2_5_CODER_7B`

## Boundaries

- Local AI repository write: `FALSE`
- Local AI GitHub authority: `FALSE`
- Production credential access: `FALSE`
- Live PR integration: `FALSE`
- Firewall changed: `FALSE`
- Ollama runtime changed: `FALSE`
- Baseline `qwen3:8b` removed: `FALSE`
- Permanent/default model changed: `FALSE`
- Context changed: `FALSE`
- Existing VSP gates changed: `FALSE`
- `VSP-LOCALAI-001C`: `NOT_STARTED`
