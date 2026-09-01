# R3.4A.4 Real-Qwen Domain Recovery Pilot

This research area evaluates whether a learned Greeting gate adds value over
the existing deterministic intent rule when assistant replies come from the
production-semantics Qwen domain.

## Scope

- 80 project-authored, non-private prompts: 30 Greeting, 30 hard negative,
  and 20 product-domain negative.
- Scenario families are assigned to Train, Dev, or Test before generation.
- Replies are generated offline with the same Qwen GGUF and production prompt
  semantics, then kept outside Git.
- Existing Linear and TextCNN artifacts are evaluated without retraining,
  threshold tuning, temperature tuning, rule changes, or model reselection.
- No Animator, capability, action policy, formal completion, or Wave behavior
  is changed.

## Tracked boundary

Tracked files contain generation code, project-authored prompt metadata,
rights and split policies, tests, aggregate metrics, and the recommendation.
They do not contain generated Qwen replies, checkpoints, ONNX files,
vocabularies, device logs, signing values, or local paths.

The Pilot is agent-reviewed and is not Real-Qwen Gold v3. Its result can decide
whether a larger recovery experiment is worth funding; it cannot by itself
make R3.4A fully accepted.

## Reproduction outline

1. Start the approved Qwen GGUF in a loopback-only `llama-server` using the
   generation profile in `manifests/pilot-prompts.json`.
2. Run `tools/generate_pilot.py` with an output path outside the repository.
3. Run `tools/evaluate_pilot.py` against the frozen R3.4A.1 artifact directory,
   again writing raw evaluation output outside the repository.
4. Compare the resulting SHA-256 and aggregate values with
   `reports/pilot-metrics.json`.
