from __future__ import annotations

import math

import numpy as np
from sklearn.metrics import (
    average_precision_score,
    confusion_matrix,
    fbeta_score,
    precision_recall_curve,
)


def softmax(logits: np.ndarray) -> np.ndarray:
    shifted = logits - logits.max(axis=1, keepdims=True)
    exponent = np.exp(shifted)
    return exponent / exponent.sum(axis=1, keepdims=True)


def fit_temperature(logits: np.ndarray, labels: np.ndarray) -> float:
    def nll(temperature: float) -> float:
        probabilities = softmax(logits / temperature)
        selected = probabilities[np.arange(len(labels)), labels]
        return float(-np.log(np.clip(selected, 1e-12, 1.0)).mean())

    candidates = np.linspace(0.5, 3.0, 101)
    return float(min(candidates, key=nll))


def greeting_probabilities(logits: np.ndarray, temperature: float) -> np.ndarray:
    return softmax(logits / temperature)[:, 1]


def expected_calibration_error(
    probabilities: np.ndarray, labels: np.ndarray, bins: int = 10
) -> float:
    confidence = np.maximum(probabilities, 1.0 - probabilities)
    predictions = probabilities >= 0.5
    edges = np.linspace(0.0, 1.0, bins + 1)
    error = 0.0
    for start, end in zip(edges[:-1], edges[1:]):
        mask = (confidence > start) & (confidence <= end)
        if not np.any(mask):
            continue
        accuracy = np.mean(predictions[mask] == labels[mask])
        error += float(np.mean(mask) * abs(accuracy - confidence[mask].mean()))
    return error


def evaluate_binary_gate(
    probabilities: np.ndarray,
    labels: np.ndarray,
    gate: dict[str, float | int | bool],
    rule_mask: np.ndarray | None = None,
) -> dict[str, float | int]:
    margin = np.abs(probabilities - (1.0 - probabilities))
    accepted = (probabilities >= float(gate["confidence"])) & (
        margin >= float(gate["margin"])
    )
    if rule_mask is not None:
        accepted &= rule_mask.astype(bool)
    positives = labels == 1
    true_positive = int(np.sum(accepted & positives))
    false_positive = int(np.sum(accepted & ~positives))
    false_negative = int(np.sum(~accepted & positives))
    precision = true_positive / max(1, true_positive + false_positive)
    recall = true_positive / max(1, true_positive + false_negative)
    beta_squared = 0.25
    f05 = (
        (1.0 + beta_squared) * precision * recall / (beta_squared * precision + recall)
        if precision + recall > 0.0
        else 0.0
    )
    return {
        "precision": precision,
        "recall": recall,
        "f0.5": f05,
        "accepted": int(np.sum(accepted)),
        "truePositive": true_positive,
        "falsePositive": false_positive,
        "falseNegative": false_negative,
        "positiveCount": int(np.sum(positives)),
    }


def select_binary_gate(
    probabilities: np.ndarray,
    labels: np.ndarray,
    minimum_precision: float,
    minimum_recall: float,
    rule_mask: np.ndarray | None = None,
) -> dict[str, float | int | bool]:
    candidates: list[dict[str, float | int | bool]] = []
    for confidence in np.arange(0.50, 0.991, 0.01):
        for margin in np.arange(0.0, 0.981, 0.02):
            gate: dict[str, float | int | bool] = {
                "confidence": round(float(confidence), 4),
                "margin": round(float(margin), 4),
            }
            result = evaluate_binary_gate(probabilities, labels, gate, rule_mask)
            candidates.append({**gate, **result})
    qualified = [
        row
        for row in candidates
        if float(row["precision"]) >= minimum_precision
        and float(row["recall"]) >= minimum_recall
    ]
    pool = qualified or candidates
    selected = max(
        pool,
        key=lambda row: (
            float(row["precision"]) >= minimum_precision,
            float(row["recall"]) >= minimum_recall,
            float(row["f0.5"]),
            float(row["precision"]),
            float(row["recall"]),
            -float(row["confidence"]),
        ),
    )
    return {
        "confidence": float(selected["confidence"]),
        "margin": float(selected["margin"]),
        "devPrecision": float(selected["precision"]),
        "devRecall": float(selected["recall"]),
        "devF0.5": float(selected["f0.5"]),
        "qualityTargetMet": selected in qualified,
    }


def binary_metrics(probabilities: np.ndarray, labels: np.ndarray) -> dict[str, object]:
    predictions = probabilities >= 0.5
    matrix = confusion_matrix(labels, predictions, labels=[0, 1])
    tn, fp, fn, tp = (int(value) for value in matrix.ravel())
    precision = tp / max(1, tp + fp)
    recall = tp / max(1, tp + fn)
    return {
        "accuracy": float(np.mean(predictions == labels)),
        "precision": precision,
        "recall": recall,
        "f0.5": float(fbeta_score(labels, predictions, beta=0.5, zero_division=0)),
        "averagePrecision": float(average_precision_score(labels, probabilities)),
        "ece10": expected_calibration_error(probabilities, labels),
        "meanTop1Top2Margin": float(np.mean(np.abs(2.0 * probabilities - 1.0))),
        "confusionMatrix": [[tn, fp], [fn, tp]],
    }


def precision_recall_points(
    probabilities: np.ndarray, labels: np.ndarray
) -> list[dict[str, float]]:
    precision, recall, thresholds = precision_recall_curve(labels, probabilities)
    points: list[dict[str, float]] = []
    for index, threshold in enumerate(thresholds):
        points.append(
            {
                "threshold": float(threshold),
                "precision": float(precision[index]),
                "recall": float(recall[index]),
            }
        )
    return points
