from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Sequence

import numpy as np
import torch
from sklearn.metrics import confusion_matrix

from .data import load_examples, split_examples
from .evaluation import binary_metrics, evaluate_binary_gate, greeting_probabilities
from .models import GreetingTextCNN, parameter_count
from .rules import deterministic_greeting_intent
from .schema import GreetingExample
from .tokenizer import CharacterTokenizer
from .training import predict_logits


LOCKED_CANDIDATE: dict[str, object] = {
    "name": "textcnn-user_reply_pair",
    "model": "character_textcnn",
    "inputForm": "user_reply_pair",
    "temperature": 0.5,
    "confidenceThreshold": 0.5,
    "marginThreshold": 0.0,
    "checkpointSha256": "7dadb70b6c62b25e476212c11571507d70861754663af38d76a20fbe842866b4",
    "vocabSha256": "4f2ac1077a00f075db3843b0d2ed38e0de46112eb9dba6008dcc91f30e0cee22",
    "corpusSha256": "4480b48741dcc35d16da4c89a55acfd34b7d8b09baa7a07c7b35b5e829be4478",
    "vocabSize": 566,
    "maxSequenceLength": 96,
    "parameterCount": 27618,
}
QUALITY_TARGET = {
    "minimumPrecision": 0.95,
    "minimumRecall": 0.70,
    "maximumSafetyCriticalFalsePositives": 0,
}


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _labels(rows: Sequence[GreetingExample]) -> np.ndarray:
    return np.asarray([1 if row.label == "Greeting" else 0 for row in rows], dtype=np.int64)


def _gate_mask(probabilities: np.ndarray, rule_mask: np.ndarray) -> np.ndarray:
    margin = np.abs(2.0 * probabilities - 1.0)
    return (
        (probabilities >= float(LOCKED_CANDIDATE["confidenceThreshold"]))
        & (margin >= float(LOCKED_CANDIDATE["marginThreshold"]))
        & rule_mask.astype(bool)
    )


def evaluate_locked_partition(
    rows: Sequence[GreetingExample],
    probabilities: np.ndarray,
    rule_mask: np.ndarray,
) -> dict[str, object]:
    labels = _labels(rows)
    probabilities = np.asarray(probabilities, dtype=np.float64)
    rule_mask = np.asarray(rule_mask, dtype=bool)
    if probabilities.shape != labels.shape or rule_mask.shape != labels.shape:
        raise ValueError("locked evaluation shape mismatch")
    if not np.all(np.isfinite(probabilities)):
        raise ValueError("locked evaluation received NaN or Inf")

    gate = {
        "confidence": float(LOCKED_CANDIDATE["confidenceThreshold"]),
        "margin": float(LOCKED_CANDIDATE["marginThreshold"]),
    }
    gate_metrics = evaluate_binary_gate(probabilities, labels, gate, rule_mask)
    accepted = _gate_mask(probabilities, rule_mask)
    matrix = confusion_matrix(labels, accepted.astype(np.int64), labels=[0, 1])
    tn, fp, fn, tp = (int(value) for value in matrix.ravel())
    safety_critical = np.asarray([row.safety_critical for row in rows], dtype=bool)
    gate_metrics.update(
        {
            "acceptedPositiveCount": tp,
            "confusionMatrix": [[tn, fp], [fn, tp]],
            "safetyCriticalFalsePositive": int(
                np.sum(accepted & (labels == 0) & safety_critical)
            ),
        }
    )
    return {
        "rowCount": len(rows),
        "positiveCount": int(np.sum(labels == 1)),
        "negativeCount": int(np.sum(labels == 0)),
        "raw": binary_metrics(probabilities, labels),
        "gate": gate_metrics,
    }


def quality_gate(
    test_gate: dict[str, object], gold_gate: dict[str, object]
) -> dict[str, object]:
    def partition_passed(metrics: dict[str, object]) -> bool:
        return (
            float(metrics["precision"]) >= float(QUALITY_TARGET["minimumPrecision"])
            and float(metrics["recall"]) >= float(QUALITY_TARGET["minimumRecall"])
            and int(metrics["safetyCriticalFalsePositive"])
            <= int(QUALITY_TARGET["maximumSafetyCriticalFalsePositives"])
        )

    test_passed = partition_passed(test_gate)
    gold_passed = partition_passed(gold_gate)
    return {
        "target": QUALITY_TARGET,
        "testPassed": test_passed,
        "humanReviewedGoldPassed": gold_passed,
        "goldV2ProjectMemberReviewGate": "passed" if gold_passed else "failed",
        "passed": test_passed and gold_passed,
    }


def _load_tokenizer(path: Path) -> CharacterTokenizer:
    payload = json.loads(path.read_text(encoding="utf-8"))
    token_to_id = {str(key): int(value) for key, value in payload["tokenToId"].items()}
    max_length = int(payload["maxLength"])
    if len(token_to_id) != int(LOCKED_CANDIDATE["vocabSize"]):
        raise ValueError("locked vocabulary size mismatch")
    if max_length != int(LOCKED_CANDIDATE["maxSequenceLength"]):
        raise ValueError("locked maximum sequence length mismatch")
    return CharacterTokenizer(token_to_id, max_length)


def _assert_locked_artifact(path: Path, expected_sha256: str, label: str) -> None:
    actual = _sha256(path)
    if actual != expected_sha256:
        raise ValueError(f"locked {label} SHA-256 mismatch: {actual}")


def _assert_test_matches_baseline(
    recomputed: dict[str, object], baseline_metrics_path: Path
) -> dict[str, object]:
    baseline = json.loads(baseline_metrics_path.read_text(encoding="utf-8"))
    if baseline.get("model") != LOCKED_CANDIDATE["model"]:
        raise ValueError("baseline model does not match the locked candidate")
    if baseline.get("inputForm") != LOCKED_CANDIDATE["inputForm"]:
        raise ValueError("baseline input form does not match the locked candidate")
    thresholds = baseline["ruleAndLearnedGate"]["thresholds"]
    expected_thresholds = (
        float(LOCKED_CANDIDATE["confidenceThreshold"]),
        float(LOCKED_CANDIDATE["marginThreshold"]),
        float(LOCKED_CANDIDATE["temperature"]),
    )
    actual_thresholds = (
        float(thresholds["confidence"]),
        float(thresholds["margin"]),
        float(baseline["calibration"]["temperature"]),
    )
    if actual_thresholds != expected_thresholds:
        raise ValueError("baseline thresholds or temperature are not Dev-locked values")

    expected_test = baseline["ruleAndLearnedGate"]["test"]
    actual_test = recomputed["gate"]
    for key in ("precision", "recall", "f0.5"):
        if not np.isclose(float(actual_test[key]), float(expected_test[key]), atol=1e-12):
            raise ValueError(f"recomputed Test {key} drifted from the locked baseline")
    for key in ("accepted", "truePositive", "falsePositive", "falseNegative", "positiveCount"):
        if int(actual_test[key]) != int(expected_test[key]):
            raise ValueError(f"recomputed Test {key} drifted from the locked baseline")
    if int(actual_test["safetyCriticalFalsePositive"]) != int(
        baseline["ruleAndLearnedGate"]["testSafetyCriticalFalsePositive"]
    ):
        raise ValueError("recomputed Test safety-critical FP drifted from the locked baseline")
    return {
        "baselineMetricsSha256": _sha256(baseline_metrics_path),
        "testMetricsMatchLockedBaseline": True,
    }


def reevaluate_locked_candidate(
    *,
    corpus_path: Path,
    gold_path: Path,
    checkpoint_path: Path,
    vocab_path: Path,
    review_manifest_path: Path,
    baseline_metrics_path: Path,
) -> dict[str, object]:
    _assert_locked_artifact(
        corpus_path, str(LOCKED_CANDIDATE["corpusSha256"]), "corpus"
    )
    _assert_locked_artifact(
        checkpoint_path, str(LOCKED_CANDIDATE["checkpointSha256"]), "checkpoint"
    )
    _assert_locked_artifact(
        vocab_path, str(LOCKED_CANDIDATE["vocabSha256"]), "vocabulary"
    )

    review_manifest = json.loads(review_manifest_path.read_text(encoding="utf-8"))
    if review_manifest.get("fullyHumanReviewed") is not True:
        raise ValueError("Gold v2 is not fully human reviewed")
    if review_manifest.get("finalGoldSha256") != _sha256(gold_path):
        raise ValueError("human-reviewed Gold v2 SHA-256 mismatch")

    corpus = split_examples(load_examples(corpus_path))
    test_rows = corpus["test"]
    gold_rows = load_examples(gold_path)
    if any(row.review_status != "project_member_reviewed" for row in gold_rows):
        raise ValueError("Gold v2 contains rows outside project-member review")

    tokenizer = _load_tokenizer(vocab_path)
    model = GreetingTextCNN(vocab_size=len(tokenizer.token_to_id))
    state = torch.load(checkpoint_path, map_location="cpu", weights_only=True)
    model.load_state_dict(state)
    if parameter_count(model) != int(LOCKED_CANDIDATE["parameterCount"]):
        raise ValueError("locked TextCNN parameter count mismatch")

    input_form = str(LOCKED_CANDIDATE["inputForm"])
    temperature = float(LOCKED_CANDIDATE["temperature"])

    def evaluate(rows: list[GreetingExample]) -> dict[str, object]:
        input_ids = tokenizer.encode_many(rows, input_form)
        logits = predict_logits(model, input_ids)
        probabilities = greeting_probabilities(logits, temperature)
        rule_mask = np.asarray(
            [deterministic_greeting_intent(row.user) for row in rows], dtype=bool
        )
        return evaluate_locked_partition(rows, probabilities, rule_mask)

    test = evaluate(test_rows)
    gold = evaluate(gold_rows)
    baseline_check = _assert_test_matches_baseline(test, baseline_metrics_path)
    gate = quality_gate(test["gate"], gold["gate"])
    return {
        "schemaVersion": "r3.4a2-locked-quality-reevaluation-v1",
        "candidate": LOCKED_CANDIDATE,
        "noTrainingPerformed": True,
        "noThresholdSelectionPerformed": True,
        "goldUsedForFinalEvaluationOnly": True,
        "reviewEvidence": {
            "rowCount": review_manifest["rowCount"],
            "reviewerRole": review_manifest["reviewerRole"],
            "changedLabelCount": review_manifest["changedLabelCount"],
            "fullyHumanReviewed": review_manifest["fullyHumanReviewed"],
            "finalGoldSha256": review_manifest["finalGoldSha256"],
        },
        "artifactVerification": {
            "corpusSha256": _sha256(corpus_path),
            "checkpointSha256": _sha256(checkpoint_path),
            "vocabSha256": _sha256(vocab_path),
            **baseline_check,
        },
        "test": test,
        "humanReviewedGoldV2": gold,
        "qualityGate": gate,
    }
