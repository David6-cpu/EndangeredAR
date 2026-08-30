# R3.4A Project Taxonomy

The selected model shape is dual-head because a dialogue act answers what a
reply is doing while emotion answers how it is expressed. Greeting is the
future Wave-relevant signal; Joy alone is deliberately insufficient.

## DialogueAct

| Label | Meaning | Product note |
| --- | --- | --- |
| `Neutral` | Informational answer, acknowledgement, or ordinary statement | Default non-expression class |
| `Greeting` | Opening, returning, or reciprocal greeting | Only class considered for a future Wave candidate |
| `Comfort` | Consoling, listening, or emotional support | No animation mapping in R3.4A |
| `Appreciation` | Thanking or expressing gratitude | No animation mapping in R3.4A |
| `Question` | Assistant asks a genuine question | No animation mapping in R3.4A |
| `Other` | Commands, refusals, apologies, irony, exclamations, or unmapped acts | Fail-closed bucket |

`Encouragement` is not retained in v1. CPED has no dependable corresponding
class, and the project-authored gold set cannot also serve as training data.

## EmotionTone

| Label | CPED sources |
| --- | --- |
| `Neutral` | neutral |
| `Joy` | happy |
| `Warm` | grateful, relaxed, positive-other |
| `Sadness` | sadness, depress |
| `Worry` | worried, fear |
| `Anger` | anger |
| `Surprise` | astonished |
| `Other` | disgust, negative-other |

The map keeps the product-facing output small while avoiding a false equation
between positive emotion and Greeting. Scarce CPED act labels are merged rather
than advertised as model capabilities they cannot support.

## Strongly typed research output

```text
AffectPrediction
  dialogueAct
  emotionTone
  dialogueConfidence
  emotionConfidence
  dialogueMargin
  emotionMargin
  modelVersion
```

No output field names an Animator trigger, state, `AIAction`, Unity method,
GameObject, memory operation, progress operation, or business command.
