# R3.4A Affect Classification Research

This directory contains a research-only spike for classifying the dialogue act
and emotion tone of an assistant reply. It is deliberately isolated from the
production chat and animation pipelines.

## Product boundary

Input candidates:

1. assistant reply only;
2. original user message plus final assistant reply;
3. the pair plus project-owned typed metadata, only if a controlled experiment
   later demonstrates a material gain.

Output is limited to typed labels, confidence, margin, and model version. The
research model never emits an Animator trigger, `AIAction`, Unity object path,
memory write, progress write, or business command.

R3.4A does not modify the production `Packages/manifest.json`, does not connect
to AI completion, and does not change Animator or capability assets.

## Data boundary

The only text data allowed in Git is the project-authored gold set under
`data/endangeredar_gold.jsonl`. Third-party source text, processed third-party
text, checkpoints, ONNX files, caches, and run directories are ignored and
must remain local until their rights are separately approved.

The local CPED experiment is pinned to upstream commit
`1e4b81c28a123f22387e06664f37e5dc9322380f`. That pin records provenance; it
does not grant rights to redistribute television dialogue or derived weights.

## Reproduction outline

The executable pipeline lives under `src/`. It reads a local CPED checkout,
applies the fixed mappings and split rules, trains each candidate with the same
seed, and writes generated artifacts outside Git. The committed reports contain
aggregate metrics only and never contain restricted source utterances.
