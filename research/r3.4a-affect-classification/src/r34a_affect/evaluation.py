from __future__ import annotations

import math
from typing import Sequence

import numpy as np
from sklearn.metrics import accuracy_score, classification_report, confusion_matrix


def softmax(logits: np.ndarray) -> np.ndarray:
    shifted = logits - logits.max(axis=1, keepdims=True)
    exponent = np.exp(shifted)
    return exponent / exponent.sum(axis=1, keepdims=True)


def negative_log_likelihood(logits: np.ndarray, labels: np.ndarray, temperature: float) -> float:
    probabilities = softmax(logits / temperature)
    selected = probabilities[np.arange(len(labels)), labels]
    return float(-np.log(np.clip(selected, 1e-12, 1.0)).mean())


def fit_temperature(logits: np.ndarray, labels: np.ndarray) -> float:
    candidates = np.linspace(0.5, 3.0, 101)
    return float(min(candidates, key=lambda value: negative_log_likelihood(logits, labels, value)))


def expected_calibration_error(
    probabilities: np.ndarray, labels: np.ndarray, bins: int = 10
) -> float:
    confidence = probabilities.max(axis=1)
    prediction = probabilities.argmax(axis=1)
    edges = np.linspace(0.0, 1.0, bins + 1)
    error = 0.0
    for start, end in zip(edges[:-1], edges[1:]):
        mask = (confidence > start) & (confidence <= end)
        if not np.any(mask):
            continue
        accuracy = np.mean(prediction[mask] == labels[mask])
        error += float(np.mean(mask) * abs(accuracy - confidence[mask].mean()))
    return error


def _margins(probabilities: np.ndarray) -> np.ndarray:
    ordered = np.sort(probabilities, axis=1)
    return ordered[:, -1] - ordered[:, -2]


def classification_metrics(
    logits: np.ndarray,
    labels: np.ndarray,
    class_names: Sequence[str],
    temperature: float,
) -> dict[str, object]:
    probabilities = softmax(logits / temperature)
    predictions = probabilities.argmax(axis=1)
    report = classification_report(
        labels,
        predictions,
        labels=list(range(len(class_names))),
        target_names=list(class_names),
        output_dict=True,
        zero_division=0,
    )
    per_class = {
        name: {
            "precision": float(report[name]["precision"]),
            "recall": float(report[name]["recall"]),
            "f1": float(report[name]["f1-score"]),
            "support": int(report[name]["support"]),
        }
        for name in class_names
    }
    return {
        "accuracy": float(accuracy_score(labels, predictions)),
        "macroF1": float(report["macro avg"]["f1-score"]),
        "weightedF1": float(report["weighted avg"]["f1-score"]),
        "ece10": expected_calibration_error(probabilities, labels),
        "meanTop1Top2Margin": float(_margins(probabilities).mean()),
        "temperature": temperature,
        "perClass": per_class,
        "confusionMatrix": confusion_matrix(
            labels, predictions, labels=list(range(len(class_names)))
        ).tolist(),
    }


def tune_greeting_gate(
    logits: np.ndarray,
    labels: np.ndarray,
    class_names: Sequence[str],
    temperature: float,
) -> dict[str, float | int]:
    greeting_id = list(class_names).index("Greeting")
    probabilities = softmax(logits / temperature)
    predictions = probabilities.argmax(axis=1)
    confidence = probabilities.max(axis=1)
    margin = _margins(probabilities)
    positives = labels == greeting_id
    minimum_predictions = max(3, math.ceil(int(positives.sum()) * 0.2))
    candidates: list[dict[str, float | int]] = []
    for confidence_threshold in np.arange(0.4, 0.981, 0.02):
        for margin_threshold in np.arange(0.0, 0.501, 0.02):
            accepted = (
                (predictions == greeting_id)
                & (confidence >= confidence_threshold)
                & (margin >= margin_threshold)
            )
            predicted_count = int(accepted.sum())
            if predicted_count < minimum_predictions:
                continue
            true_positive = int((accepted & positives).sum())
            precision = true_positive / predicted_count
            recall = true_positive / max(1, int(positives.sum()))
            candidates.append(
                {
                    "confidence": round(float(confidence_threshold), 4),
                    "margin": round(float(margin_threshold), 4),
                    "precision": precision,
                    "recall": recall,
                    "accepted": predicted_count,
                }
            )
    if not candidates:
        return {
            "confidence": 0.9,
            "margin": 0.2,
            "precision": 0.0,
            "recall": 0.0,
            "accepted": 0,
        }
    return max(
        candidates,
        key=lambda item: (
            float(item["precision"]),
            float(item["recall"]),
            float(item["confidence"]),
            float(item["margin"]),
        ),
    )


def evaluate_greeting_gate(
    logits: np.ndarray,
    labels: np.ndarray,
    class_names: Sequence[str],
    temperature: float,
    gate: dict[str, float | int],
) -> dict[str, float | int]:
    greeting_id = list(class_names).index("Greeting")
    probabilities = softmax(logits / temperature)
    predictions = probabilities.argmax(axis=1)
    confidence = probabilities.max(axis=1)
    margin = _margins(probabilities)
    accepted = (
        (predictions == greeting_id)
        & (confidence >= float(gate["confidence"]))
        & (margin >= float(gate["margin"]))
    )
    positives = labels == greeting_id
    true_positive = int((accepted & positives).sum())
    predicted_count = int(accepted.sum())
    return {
        "precision": true_positive / max(1, predicted_count),
        "recall": true_positive / max(1, int(positives.sum())),
        "accepted": predicted_count,
        "truePositive": true_positive,
        "falsePositive": predicted_count - true_positive,
        "goldPositive": int(positives.sum()),
    }
