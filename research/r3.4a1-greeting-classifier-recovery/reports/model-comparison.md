# R3.4A.1 Model Comparison

All thresholds and candidate selection use Dev only. Test is evaluated
after selection logic is fixed; Gold v2 is read after the candidate has
been locked and does not alter model or input selection.

## Rule baseline

| Split | Precision | Recall | F0.5 | Accepted | Safety-critical FP |
| --- | ---: | ---: | ---: | ---: | ---: |
| dev | 1.0000 | 0.7222 | 0.9286 | 65 | 0 |
| test | 1.0000 | 0.8333 | 0.9615 | 75 | 0 |
| gold | 1.0000 | 1.0000 | 1.0000 | 110 | 0 |

## Learned candidates

The table reports the required deterministic rule AND learned gate.

| Candidate | Test P/R | Gold P/R | Test hard FP | Gold hard FP | Dev confidence/margin | Parameters | Artifact bytes | Mac mean ms |
| --- | --- | --- | ---: | ---: | --- | ---: | ---: | ---: |
| linear-user_only | 1.0000/0.8333 | 1.0000/1.0000 | 0 | 0 | 0.5000/0.0000 | 4894 | 203686 | 0.0848 |
| textcnn-user_only | 1.0000/0.8333 | 1.0000/0.8636 | 0 | 0 | 0.5000/0.0000 | 24642 | 102582 | 0.0903 |
| linear-reply_only | 1.0000/0.7778 | 1.0000/1.0000 | 0 | 0 | 0.5000/0.0000 | 2885 | 122470 | 0.0926 |
| textcnn-reply_only | 1.0000/0.6111 | 1.0000/0.8000 | 0 | 0 | 0.5000/0.0000 | 20418 | 85686 | 0.0922 |
| linear-user_reply_pair | 1.0000/0.8333 | 1.0000/1.0000 | 0 | 0 | 0.5000/0.0000 | 7862 | 328886 | 0.1035 |
| textcnn-user_reply_pair | 1.0000/0.7889 | 1.0000/0.9909 | 0 | 0 | 0.5000/0.0000 | 27618 | 114486 | 0.0893 |

## Effective independent inputs

User-only and Reply-only interaction counts contain repeated inputs
because one fixed message may pair with multiple assistant replies.
They are therefore reported but are not eligible to satisfy the Gold
v2 size gate. Pair has 110 unique positive and 210 unique safety-critical
negative Gold inputs.

## Selection and gate

- Dev-selected candidate: `textcnn-user_reply_pair`.
- Numerical Test and Gold thresholds passed: `true`.
- Selected input data adequacy passed: `true`.
- Gold fully human-reviewed: `false`.
- Product quality gate passed: `false`.
- BiLSTM was not retrained because Linear and TextCNN already reached the
  numerical target and no evidence justified reopening the heavier model.
- ONNX, Unity, and signed iPhone work remain blocked by the product
  quality gate and were not started in this recovery run.
