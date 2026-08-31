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
- Gold v2 is agent-reviewed and still awaits user or project-member review. It
  must not be described as fully human-reviewed.
- Checkpoints, vectorizers, vocabularies, ONNX files, package imports, and build
  output remain local until every applicable gate passes.

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
