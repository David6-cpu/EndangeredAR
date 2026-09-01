# R3.4A.4 Frozen Model Comparison

All learned results use the R3.4A.1 frozen artifacts and Dev-locked
temperature/threshold values. No training or tuning used this Pilot.

| Strategy | Precision | Recall | Safety FP | Result |
| --- | ---: | ---: | ---: | --- |
| Rule only | 1.0000 | 0.8667 | 0 | Best Pilot tradeoff |
| Rule + Linear User-only | 1.0000 | 0.8667 | 0 | No gain over rule |
| Rule + TextCNN User-only | 1.0000 | 0.8333 | 0 | Lower recall |
| Rule + Linear Reply-only | 1.0000 | 0.8333 | 0 | Lower recall |
| Rule + TextCNN Reply-only | 1.0000 | 0.0333 | 0 | Response-domain collapse |
| Rule + Linear Pair | 1.0000 | 0.8333 | 0 | Lower recall |
| Rule + TextCNN Pair | 1.0000 | 0.1667 | 0 | Response-domain collapse |
| Rule + TextCNN User-only + reply guard | 1.0000 | 0.8333 | 0 | Lower recall |

Learned-only User and Linear Pair variants accepted safety-critical negatives;
the deterministic first gate removed those false positives but left no quality
gain over Rule-only. Reply-only and TextCNN Pair were especially brittle on
the production-semantics Qwen reply style.

## Pilot recommendation

Do not spend the next phase generating 2,200 pairs and retraining a learned
Wave gate. Keep the affect classifier as research evidence. For the first
Greeting-to-Wave product path, take Rule-only plus the separately required
Product Scope, capability, validator, controller, and single-action gates into
a dedicated acceptance decision.

This recommendation does not approve Wave, R3.4B, or formal product
integration. The Pilot lacks project-reviewed Real-Qwen Gold v3 and is too
small to serve as a final acceptance set.
