# R3.4A.5 Deterministic Greeting Gate

R3.4A.5 closes the learned Greeting-classifier product route and evaluates a
small deterministic policy for the first Greeting-to-Wave decision method.

## Locked decision

- Linear, TextCNN, and BiLSTM remain valuable research-only evidence.
- Learned classification is not a prerequisite for the first Wave path.
- Wave v1 does not require Unity Inference Engine, ONNX, a vocabulary, or a
  classifier Worker.
- Checkpoints, ONNX files, vocabularies, and local training artifacts remain
  local-only.
- The selected Wave v1 decision method is
  `DeterministicGreetingIntent + ProductScopeGate`.
- The independent project-member Gold gate passed. R3.4A research is accepted;
  the learned classifier remains research-only and is not a Wave dependency.

No Wave asset, `AIAction`, Animator state, capability, controller behavior,
action policy, response validator, or formal completion integration is added
in this stage.

## Acceptance boundary

The policy is frozen before the independent user-only Gold v1 review package
is built. Gold v1 may be evaluated once after project-member review, but it
must not be used to change the rule. A failed Gold v1 requires a new policy
version and a new unpolluted Gold set.

## Gold v1 blind review

The tracked candidate manifest contains 150 unique project-authored,
non-private user messages: a 50/100 greeting/non-greeting design target with
70 safety-critical coverage items. It contains no expected labels, rule
results, assistant replies, model outputs, confidence, or margins. Candidate
messages are normalized and checked against both the R3.4A.4 Pilot and the
pair-level Gold v2 before a package can be built.

Generate the manifest reproducibly:

```bash
PYTHONPATH=research/r3.4a5-deterministic-greeting-gate/src \
python3 research/r3.4a5-deterministic-greeting-gate/tools/materialize_gold_v1_candidates.py \
  --output research/r3.4a5-deterministic-greeting-gate/data/deterministic-greeting-gold-v1-candidates.json
```

Build the reviewer package into a new directory outside the repository:

```bash
PYTHONPATH=research/r3.4a5-deterministic-greeting-gate/src \
python3 research/r3.4a5-deterministic-greeting-gate/tools/build_gold_v1_blind_review.py \
  --project-root "$PWD" \
  --output-directory /path/outside/repository
```

Only the CSV and instructions are reviewer-facing. The stable-ID mapping and
package manifest remain local evidence. All reviewer fields start blank, and
`fullyHumanReviewed` remains `false` until a real project member reviews every
row and resolves any `ambiguous` or `invalid` decision.

## Final review and evaluation

The project owner blind-reviewed all 150 rows. The completed workbook retained
its original SHA-256; its numeric `0.9` confidence cells were explicitly
confirmed as `high` and normalized only in the reviewed Gold evidence. The
final set contains 52 Greeting and 98 NotGreeting decisions, with two changes
from the design labels and no unresolved rows.

The frozen C# policies were evaluated once after import:

- Gold precision: `1.0000`;
- Gold recall: `0.9423`;
- F0.5: `0.9879`;
- confusion matrix (`actual NotGreeting`, `actual Greeting` rows):
  `[[98, 0], [3, 49]]`;
- safety-critical false positives: `0`;
- ProductScope vectors: `15/15` rejected as required.

The authoritative aggregate output is
[`reports/gold-v1-final-evaluation.json`](reports/gold-v1-final-evaluation.json).
Gold v1 was not used to modify either frozen policy.
