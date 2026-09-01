# R3.4A.4 Pilot Split Policy

The split was committed in `pilot-prompts.json` before any Qwen generation.
Scenario families, prompt-template families, and split groups are confined to
one split.

| Split | Greeting | Hard negative | Product negative | Total |
| --- | ---: | ---: | ---: | ---: |
| Train | 12 | 12 | 8 | 32 |
| Dev | 9 | 9 | 6 | 24 |
| Test | 9 | 9 | 6 | 24 |
| Total | 30 | 30 | 20 | 80 |

The split labels organize error analysis only. This Pilot does not train or
tune on any split. Gold v2, the historical device failure, and any future Gold
v3 remain outside model selection and calibration.
