# R3.4A.1 Greeting Classifier Recovery

This directory contains a research-only recovery of the failed R3.4A affect
classifier. The product question is intentionally narrowed to one binary
decision:

```text
GreetingEligibility = Greeting | NotGreeting
```

The classifier studies whether the original user message and final assistant
reply together form a greeting interaction that could later become a Wave
candidate. It does not emit an animation candidate, `AIAction`, Animator
trigger, memory write, progress write, or business command.

## Boundaries

- R3.3C production chat and validation code are unchanged.
- Animator, Character Capability, action policy, and completion flow are
  unchanged.
- CPED remains a local domain-shift reference only and is not used for product
  training in this recovery.
- The generated training corpus is project-controlled but remains outside Git.
- Gold v2 is project-authored and contains no private chat, device log, Memory,
  Progress, identity, or third-party source text.
- Gold v2 has completed a blind 320/320 project-member review. The original
  agent-reviewed rows remain under `data/history/`, and the non-sensitive
  review evidence is recorded in `manifests/gold-v2-project-review-manifest.json`.
- Checkpoints, vectorizers, vocabularies, ONNX files, package imports, and build
  output remain local until every applicable gate passes.

## Current gate status

- The Dev-locked TextCNN Pair still passes Test at precision `1.0000`, recall
  `0.7889`, and zero safety-critical false positives.
- The human-reviewed Gold v2 passes at precision `1.0000`, recall `0.9909`,
  and zero safety-critical false positives.
- PyTorch/ONNX Runtime parity and isolated Unity Inference Engine 2.4.1 CPU
  parity pass for the reviewed checkpoint.
- The independent full-App Development build signed, installed, launched, and
  completed a fully offline 1,000/1,000 CPU inference loop on iPhone. Fixed
  fixtures, runtime parity, Worker reload, Product Scope, and Qwen/AR
  coexistence checks passed.
- The required real final validated Qwen greeting did not pass: deterministic
  intent and product scope were true, but the locked TextCNN predicted
  `NotGreeting`. The science Qwen pair was not recorded by the exact fixture
  matcher, leaving the required real-Qwen evidence at 2/3 pairs.

R3.4A is therefore not fully accepted, and R3.4B remains closed. No Wave,
Animator, capability, action, or completion integration was added.

See the sanitized signed-device outcome in
[`2026-08-31-r3.4a3-signed-iphone-classifier-acceptance.md`](../../EndangeredAR/docs/verification/2026-08-31-r3.4a3-signed-iphone-classifier-acceptance.md).

## Reproduction

Use the pinned environment recorded by the parent R3.4A research spike, then
materialize the corpus outside the repository and Gold v2 at its tracked path:

```bash
PYTHONPATH=research/r3.4a1-greeting-classifier-recovery/src \
python -m r34a1_greeting.materialize \
  --corpus-output <local-output>/project-corpus.jsonl \
  --gold-output research/r3.4a1-greeting-classifier-recovery/data/endangeredar_gold_v2.jsonl \
  --manifest-output research/r3.4a1-greeting-classifier-recovery/manifests/data-manifest.json
```

Run the experiment with an output directory outside the repository:

```bash
PYTHONPATH=research/r3.4a1-greeting-classifier-recovery/src \
python -m r34a1_greeting.experiment \
  --project-root . \
  --output <local-output>/run
```

The experiment fixes the split before training, builds vocabulary from Train
only, selects thresholds and the candidate from Dev only, evaluates Test, then
reads Gold v2 for final acceptance evidence. Gold v2 is never used for model
selection or calibration.
