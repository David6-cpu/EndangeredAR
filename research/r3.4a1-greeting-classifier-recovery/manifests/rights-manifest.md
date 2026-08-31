# R3.4A.1 Data Rights Manifest

## Product-training source

| Field | Decision |
| --- | --- |
| Source type | Project-authored synthetic scenarios |
| Generation method | Curated semantic composition from committed scenario definitions |
| Third-party source text | None |
| Private user chat | None |
| Device or cloud logs | None |
| Character Memory or Progress | None |
| Rights status | Project-controlled, no third-party source text |
| Review status | Agent-reviewed, pending user or project-member review |
| Public repository use | Generation code, scenario definitions, Gold v2, aggregate metrics, and reports may be tracked |
| Model-weight publication | Not approved in this stage |

The repository currently has no general open-source license and therefore
reserves rights by default. This manifest records provenance and project
control; it is not a substitute for a future repository license or formal
commercial legal review.

## CPED boundary

CPED is not read by the R3.4A.1 product-training pipeline. The previous R3.4A
results remain useful only as a local domain-shift and legacy comparison. CPED
source text, processed text, checkpoints, and derived ONNX files remain outside
Git and outside the application because the underlying television-dialogue and
derived-weight redistribution rights are not cleared for this product.

## Gold v2 boundary

Gold v2 contains fixed, non-private project scenarios. Its labels and safety
flags were reviewed during construction by the research agent. Until a user or
project member records approval, the dataset must be described exactly as
`agent_reviewed_pending_project_review`, not fully human-reviewed.

Gold v2 is excluded from vocabulary construction, early stopping, calibration,
threshold selection, and model selection. It is read only after the candidate
has been selected from Dev results.

## Publication decision

The following may be public in this stage:

- research and evaluation code;
- deterministic greeting rules;
- project-owned scenario definitions;
- Gold v2 with its pending-review status;
- aggregate metrics and confusion matrices;
- reproducibility and rights manifests;
- verification reports.

The following remain local:

- generated full training corpus;
- TF-IDF/vectorizer artifacts;
- vocabularies and checkpoints;
- ONNX files;
- Unity package spike projects and generated builds;
- signing, provisioning, device, thermal, and runtime logs.
