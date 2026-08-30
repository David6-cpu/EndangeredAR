# Unity Inference Compatibility Spike

Run date: 2026-08-31

This spike used a disposable Unity project. It did not change the formal
package manifest, chat completion flow, action contract, Animator, capability
assets, or scenes.

## Runtime and license

- Unity: `6000.0.76f1` ARM64.
- Package: `com.unity.ai.inference@2.4.1`, pinned exactly rather than using a
  floating version.
- Unity documents 2.4.1 as the released Inference Engine package for Unity
  6000.0. The package license identifies the code as Unity Technologies
  software governed by the Unity Terms of Service and supplied as-is.
- Models: local-only TextCNN Pair and BiLSTM Pair candidates. They remain
  untracked because the CPED television-dialogue derivative rights are not
  cleared.
- Input: fixed `batch=1`, fixed `sequence=96`, `int64` token IDs.
- Outputs: named `dialogue_logits` and `emotion_logits` float tensors.

## Import and logit parity

Both ONNX graphs imported without an unsupported-operator error. Five
project-authored fixed cases covered Greeting, Comfort, Joy, neutral science,
and a technical/negative false positive. The reference values were generated
with ONNX Runtime from the same token IDs.

| Model | Backend | Imported | Max absolute logit error | Parity tolerance | Result |
| --- | --- | --- | ---: | ---: | --- |
| TextCNN Pair | CPU | Yes | 5.96e-7 | 1e-4 | Pass |
| TextCNN Pair | GPUCompute | Yes | 7.15e-7 | 1e-3 | Pass |
| BiLSTM Pair | CPU | Yes | 9.54e-7 | 1e-4 | Pass |
| BiLSTM Pair | GPUCompute | Yes | 9.54e-7 | 1e-3 | Pass |

The BiLSTM graph therefore exercised the package LSTM operator rather than
passing only through ONNX Runtime.

## Editor performance

Each row used 20 warmups, 200 timed batch-one runs, and then 1,000 continuous
inferences. Memory columns are endpoint deltas, not peak device memory.

| Model | Backend | Mean ms | P95 ms | Unity allocation delta | Process working-set delta |
| --- | --- | ---: | ---: | ---: | ---: |
| TextCNN Pair | CPU | 0.0668 | 0.0873 | +571,192 B | +245,760 B |
| TextCNN Pair | GPUCompute | 0.6827 | 0.7298 | +247,704 B | +18,612,224 B |
| BiLSTM Pair | CPU | 1.5406 | 1.6612 | -840 B | -32,768 B |
| BiLSTM Pair | GPUCompute | 2.1833 | 2.8179 | +235,936 B | +109,035,520 B |

CPU is faster for both small graphs in this environment. GPUCompute also has a
substantially larger working-set endpoint and would share GPU scheduling with
Qwen Metal. The deployment recommendation is TextCNN on CPU.

## Lifetime behavior

- `Worker`, input tensors, and downloaded outputs were disposed after each
  owned lifetime.
- The 1,000-iteration loops completed without an exception or monotonic
  per-iteration allocation signal.
- The batch Editor process reported one persistent allocation at shutdown.
  Leak tracing points to the package-internal `ComputeTensorDataReaper` static
  initialization. The package provides runtime subsystem and application-quit
  cleanup hooks but no public manual cleanup API for this batch Editor path.
- No package patch or reflection workaround was used. This remains a package
  observation to recheck in a signed Player, not evidence of a leaked project
  `Worker`.

## iOS build and device boundary

- Unity exported the Development iOS project with zero build errors.
- An unsigned Xcode arm64 device build completed successfully for the isolated
  bundle, proving IL2CPP and iPhoneOS link compatibility.
- A distinct signed application could not be produced because the Xcode CLI
  account record could not create a new provisioning profile. Existing local
  profiles belong to already-installed R3.3C acceptance applications and were
  deliberately not reused or overwritten.
- No R3.4A application was installed or launched on the iPhone. Offline device
  latency, peak incremental memory, continuous Player lifetime behavior, and
  coexistence with Qwen/AR therefore remain unverified.

## Decision

- Unity Editor ONNX compatibility: pass for TextCNN and BiLSTM.
- Preferred runtime/backend: TextCNN Pair on CPU.
- Formal package change: none.
- iOS arm64 compile: pass without signing.
- Signed iPhone offline inference: **not passed**.
- R3.4A cannot be marked accepted until a distinct development profile is
  available and the fixed device panel is run fully offline.
