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
- The selected research candidate is
  `DeterministicGreetingIntent + ProductScopeGate`, pending independent human
  Gold acceptance.

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
