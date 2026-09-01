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
