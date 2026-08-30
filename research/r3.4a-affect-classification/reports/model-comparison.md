# R3.4A Model Comparison

Run date: 2026-08-31

Seed: `3401`

Selection data: CPED Dev only

Final diagnostic data: CPED Test and the project-authored Gold set

The full per-class precision, recall, F1, calibration values, confusion matrices,
training histories, hashes, and thresholds are in `metrics-summary.json`.

## Split after contamination removal

| Split | Raw | Retained | Removed | Greeting retained |
| --- | ---: | ---: | ---: | ---: |
| Train | 94,187 | 84,983 | 9,204 | 88 |
| Dev | 11,137 | 10,053 | 1,084 | 11 |
| Test | 27,438 | 27,401 | 37 | 60 |
| EndangeredAR Gold | 72 | 72 | 0 | 12 |

The small number of unseen Greeting examples is a material limitation. It makes
threshold precision estimates unstable and is itself evidence that CPED cannot
support the intended product gate without additional rights-cleared,
project-domain data.

## Candidate results

All values below are from CPED Test. `Gate P/R` uses confidence and top1/top2
margin selected only on Dev. Latency is batch-one inference in the current ARM64
Mac process, not an iPhone measurement.

| Model | Input | DA Macro-F1 | Emotion Macro-F1 | Greeting P/R | Gate P/R | Parameters | Artifact | Mean ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Linear LR | Reply | 0.4579 | **0.2111** | 0.1348 / 0.2000 | 0.3182 / 0.1167 | 420,014 | 4,348,258 B | 0.3191 |
| TextCNN | Reply | **0.4856** | 0.1329 | 0.0722 / 0.1167 | 0.2414 / 0.1167 | 193,790 | 777,378 B | 0.0971 |
| BiLSTM | Reply | 0.4553 | 0.1500 | 0.0545 / 0.1833 | 0.0833 / 0.1667 | 231,054 | 928,923 B | 1.1926 |
| Linear LR | Pair | 0.4459 | **0.2119** | **0.2712 / 0.2667** | **0.4500 / 0.1500** | 420,014 | 4,344,498 B | 0.3238 |
| TextCNN | Pair | 0.4480 | 0.1346 | 0.1719 / 0.1833 | 0.1837 / 0.1500 | 193,838 | 777,570 B | **0.0974** |
| BiLSTM | Pair | 0.4569 | 0.1548 | 0.1053 / 0.3000 | 0.2143 / 0.1000 | 231,102 | 929,115 B | 1.0819 |

The Pair input improves raw Greeting precision and/or recall for every model,
although it does not improve aggregate Macro-F1. The evidence therefore favors
Pair for a future Greeting-focused dataset, but no current score is high enough
to authorize an expression candidate.

Controlled metadata input was not trained. CPED has no project-owned variation
of `answerMode` or `ContentAuthority`; adding constant or synthetic metadata
would not demonstrate causal benefit. Input C is not recommended without a new
owned labeled experiment.

## Frozen technical candidate

The Dev-only F0.5 selection rule chose **TextCNN + User/Reply Pair**. This is an
ONNX and Unity compatibility candidate only.

- Dialogue temperature: `0.725`
- Emotion temperature: `0.700`
- Greeting confidence threshold: `0.44`
- Greeting margin threshold: `0.16`
- Dev gated Greeting: precision `0.4000`, recall `0.3636`, 10 accepted
- Test gated Greeting: precision `0.1837`, recall `0.1500`, 49 accepted
- Test DialogueAct accuracy/Macro-F1/Weighted-F1:
  `0.7857 / 0.4480 / 0.7778`
- Test EmotionTone accuracy/Macro-F1/Weighted-F1:
  `0.2905 / 0.1346 / 0.1921`
- Test calibration ECE10: Dialogue `0.0177`, Emotion `0.0271`
- Gold DialogueAct accuracy/Macro-F1: `0.5833 / 0.4473`
- Gold EmotionTone accuracy/Macro-F1: `0.3889 / 0.0999`
- Gold gated Greeting: precision `1.0000`, recall `0.0833`, one accepted,
  zero false positives

The Gold result is fail-closed but practically inert. It does not justify a
future Wave candidate.

## Selected Test confusion matrix

Dialogue order: Neutral, Greeting, Comfort, Appreciation, Question, Other.

| Actual \\ Predicted | Neutral | Greeting | Comfort | Appreciation | Question | Other |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Neutral | 17,947 | 38 | 74 | 28 | 1,229 | 699 |
| Greeting | 15 | 11 | 1 | 0 | 14 | 19 |
| Comfort | 88 | 0 | 15 | 0 | 3 | 4 |
| Appreciation | 66 | 0 | 0 | 86 | 5 | 5 |
| Question | 2,302 | 3 | 10 | 6 | 3,024 | 132 |
| Other | 950 | 12 | 10 | 11 | 149 | 445 |

Emotion order: Neutral, Joy, Warm, Sadness, Worry, Anger, Surprise, Other.

| Actual \\ Predicted | Neutral | Joy | Warm | Sadness | Worry | Anger | Surprise | Other |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Neutral | 6,752 | 29 | 135 | 287 | 138 | 460 | 182 | 1 |
| Joy | 2,180 | 50 | 115 | 67 | 20 | 92 | 62 | 0 |
| Warm | 3,186 | 37 | 99 | 158 | 69 | 238 | 69 | 1 |
| Sadness | 2,559 | 12 | 45 | 294 | 80 | 273 | 59 | 0 |
| Worry | 1,792 | 11 | 38 | 128 | 105 | 194 | 89 | 2 |
| Anger | 2,132 | 11 | 36 | 204 | 60 | 481 | 107 | 0 |
| Surprise | 976 | 6 | 25 | 38 | 31 | 179 | 178 | 0 |
| Other | 2,227 | 9 | 30 | 186 | 66 | 237 | 73 | 1 |

## ONNX results

| Model | Input | Bytes | Nodes | Max logit error | Operators of note |
| --- | --- | ---: | ---: | ---: | --- |
| TextCNN | Reply | 777,378 | 14 | 4.77e-7 | Gather, Conv, ReduceMax, Gemm |
| TextCNN | Pair | 777,570 | 14 | 2.83e-7 | Gather, Conv, ReduceMax, Gemm |
| BiLSTM | Reply | 928,923 | 41 | 4.77e-7 | Gather, LSTM, ReduceSum, Gemm |
| BiLSTM | Pair | 929,115 | 41 | 3.58e-7 | Gather, LSTM, ReduceSum, Gemm |

All four graphs use fixed `batch=1`, fixed `sequence=96`, `int64` input,
`dialogue_logits` and `emotion_logits` outputs, and opset 17. PyTorch and ONNX
Runtime logits pass the `1e-4` tolerance.

TextCNN is preferred over BiLSTM for the isolated runtime spike because it is
smaller, about eleven times faster in this Mac benchmark, has a much simpler
graph, and BiLSTM provides no material quality gain. Linear remains the strongest
emotion baseline and the strongest Pair Greeting baseline; the neural models
have not justified additional product complexity on classification quality.

## Research decision

- Technical ONNX candidate: TextCNN Pair.
- Recommended future input: User + Reply Pair.
- Recommended metadata input: none until independently demonstrated.
- Product candidate: **None**.
- Current thresholds: diagnostic only; not approved for animation.
- Public weights: prohibited because CPED television-dialogue derivative rights
  are not cleared.
