from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np
import onnx
import onnxruntime as ort
import torch

from .data import load_examples, split_examples
from .locked_review_evaluation import LOCKED_CANDIDATE
from .models import GreetingTextCNN
from .tokenizer import CharacterTokenizer
from .training import predict_logits


ONNX_OPSET = 17
ONNX_INPUT_NAME = "input_ids"
ONNX_OUTPUT_NAME = "greeting_logits"
PARITY_TOLERANCE = 1e-5
MODEL_VERSION = "r3.4a2-greeting-textcnn-pair-v1"


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _verify_sha(path: Path, expected: str, label: str) -> None:
    actual = _sha256(path)
    if actual != expected:
        raise ValueError(f"locked {label} SHA-256 mismatch: {actual}")


def export_and_verify_locked_onnx(
    *,
    corpus_path: Path,
    gold_path: Path,
    checkpoint_path: Path,
    vocab_path: Path,
    review_manifest_path: Path,
    output_path: Path,
) -> dict[str, object]:
    _verify_sha(corpus_path, str(LOCKED_CANDIDATE["corpusSha256"]), "corpus")
    _verify_sha(checkpoint_path, str(LOCKED_CANDIDATE["checkpointSha256"]), "checkpoint")
    _verify_sha(vocab_path, str(LOCKED_CANDIDATE["vocabSha256"]), "vocabulary")
    review_manifest = json.loads(review_manifest_path.read_text(encoding="utf-8"))
    if review_manifest.get("fullyHumanReviewed") is not True:
        raise ValueError("Gold v2 is not fully human reviewed")
    if review_manifest.get("finalGoldSha256") != _sha256(gold_path):
        raise ValueError("human-reviewed Gold v2 SHA-256 mismatch")

    vocab_payload = json.loads(vocab_path.read_text(encoding="utf-8"))
    tokenizer = CharacterTokenizer(
        {str(key): int(value) for key, value in vocab_payload["tokenToId"].items()},
        int(vocab_payload["maxLength"]),
    )
    if len(tokenizer.token_to_id) != int(LOCKED_CANDIDATE["vocabSize"]):
        raise ValueError("locked vocabulary size mismatch")

    model = GreetingTextCNN(vocab_size=len(tokenizer.token_to_id))
    model.load_state_dict(
        torch.load(checkpoint_path, map_location="cpu", weights_only=True)
    )
    model.eval()

    corpus = split_examples(load_examples(corpus_path))
    test_rows = corpus["test"]
    gold_rows = load_examples(gold_path)
    evaluation_rows = test_rows + gold_rows
    input_ids = tokenizer.encode_many(
        evaluation_rows, str(LOCKED_CANDIDATE["inputForm"])
    )
    sample = torch.from_numpy(input_ids[:1]).long()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    torch.onnx.export(
        model,
        sample,
        output_path,
        input_names=[ONNX_INPUT_NAME],
        output_names=[ONNX_OUTPUT_NAME],
        opset_version=ONNX_OPSET,
        dynamic_axes=None,
        do_constant_folding=True,
        dynamo=False,
    )

    graph = onnx.load(output_path)
    onnx.checker.check_model(graph)
    session = ort.InferenceSession(
        str(output_path), providers=["CPUExecutionProvider"]
    )
    torch_logits = predict_logits(model, input_ids)
    ort_logits = np.concatenate(
        [
            session.run(
                [ONNX_OUTPUT_NAME],
                {ONNX_INPUT_NAME: row.reshape(1, -1).astype(np.int64)},
            )[0]
            for row in input_ids
        ],
        axis=0,
    )
    difference = np.abs(torch_logits - ort_logits)
    if not np.all(np.isfinite(ort_logits)):
        raise ValueError("ONNX Runtime produced NaN or Inf")
    max_error = float(np.max(difference))
    operators = sorted({node.op_type for node in graph.graph.node})
    input_shape = [
        dimension.dim_value
        for dimension in graph.graph.input[0].type.tensor_type.shape.dim
    ]
    output_shape = [
        dimension.dim_value
        for dimension in graph.graph.output[0].type.tensor_type.shape.dim
    ]
    parity_passed = max_error <= PARITY_TOLERANCE
    if not parity_passed:
        raise ValueError(f"PyTorch/ONNX logits parity failed: {max_error}")

    return {
        "schemaVersion": "r3.4a2-reviewed-greeting-onnx-v1",
        "modelVersion": MODEL_VERSION,
        "candidate": LOCKED_CANDIDATE["name"],
        "checkpointSha256": _sha256(checkpoint_path),
        "vocabSha256": _sha256(vocab_path),
        "onnxSha256": _sha256(output_path),
        "onnxBytes": output_path.stat().st_size,
        "opset": ONNX_OPSET,
        "inputName": ONNX_INPUT_NAME,
        "inputDtype": "int64",
        "inputShape": input_shape,
        "outputName": ONNX_OUTPUT_NAME,
        "outputShape": output_shape,
        "fixedBatchSize": 1,
        "fixedSequenceLength": int(LOCKED_CANDIDATE["maxSequenceLength"]),
        "operators": operators,
        "nodeCount": len(graph.graph.node),
        "paritySampleCount": len(evaluation_rows),
        "testSampleCount": len(test_rows),
        "humanReviewedGoldSampleCount": len(gold_rows),
        "maxAbsoluteLogitError": max_error,
        "meanAbsoluteLogitError": float(np.mean(difference)),
        "parityTolerance": PARITY_TOLERANCE,
        "parityPassed": parity_passed,
        "runtime": "onnxruntime-cpu",
        "artifactPolicy": "local_only_pending_explicit_public_weight_approval",
    }
