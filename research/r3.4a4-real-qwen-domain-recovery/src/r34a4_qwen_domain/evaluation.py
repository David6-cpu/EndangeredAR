from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any

import joblib
import numpy as np
import torch

from r34a1_greeting.evaluation import (
    expected_calibration_error,
    greeting_probabilities,
)
from r34a1_greeting.models import GreetingTextCNN
from r34a1_greeting.rules import deterministic_greeting_intent
from r34a1_greeting.tokenizer import CharacterTokenizer
from r34a1_greeting.training import predict_logits

from .pilot import load_generated_pilot


INPUT_FORMS = ("user_only", "reply_only", "user_reply_pair")
MODEL_TYPES = ("linear", "textcnn")
REPLY_GUARD_BLOCKS = (
    "不能",
    "不会",
    "无法",
    "不要",
    "拒绝",
    "动画",
    "animator",
    "wave",
    "学名",
    "科学",
    "任务",
    "进度",
    "记忆",
    "规则",
    "系统",
    "校验",
    "错误",
)


def _linear_text(row: dict[str, Any], input_form: str) -> str:
    if input_form == "user_only":
        return f"[USER]\n{row['userMessage']}"
    if input_form == "reply_only":
        return f"[ASSISTANT]\n{row['assistantReply']}"
    if input_form == "user_reply_pair":
        return f"[USER]\n{row['userMessage']}\n[ASSISTANT]\n{row['assistantReply']}"
    raise ValueError(f"unsupported input form: {input_form}")


def reply_compatibility_guard(reply: str) -> bool:
    normalized = str(reply).strip().lower()
    return bool(normalized) and not any(fragment in normalized for fragment in REPLY_GUARD_BLOCKS)


def _labels(rows: list[dict[str, Any]]) -> np.ndarray:
    return np.asarray([row["label"] == "Greeting" for row in rows], dtype=bool)


def _metrics(
    accepted: np.ndarray,
    labels: np.ndarray,
    rows: list[dict[str, Any]],
) -> dict[str, Any]:
    true_positive = int(np.sum(accepted & labels))
    false_positive = int(np.sum(accepted & ~labels))
    false_negative = int(np.sum(~accepted & labels))
    true_negative = int(np.sum(~accepted & ~labels))
    precision = true_positive / max(1, true_positive + false_positive)
    recall = true_positive / max(1, true_positive + false_negative)
    beta_squared = 0.25
    f05 = (
        (1 + beta_squared) * precision * recall / (beta_squared * precision + recall)
        if precision + recall
        else 0.0
    )
    return {
        "precision": precision,
        "recall": recall,
        "f0.5": f05,
        "accepted": int(np.sum(accepted)),
        "positiveCount": int(np.sum(labels)),
        "confusionMatrix": [[true_negative, false_positive], [false_negative, true_positive]],
        "safetyCriticalFalsePositive": int(
            sum(
                bool(value) and row["label"] == "NotGreeting" and row["safetyCritical"]
                for value, row in zip(accepted, rows)
            )
        ),
        "categoryAccepted": {
            category: int(
                sum(bool(value) and row["category"] == category for value, row in zip(accepted, rows))
            )
            for category in ("greeting", "hard_negative", "product_negative")
        },
        "errorPromptIds": [
            row["promptId"]
            for value, label, row in zip(accepted, labels, rows)
            if bool(value) != bool(label)
        ],
    }


def _split_metrics(
    accepted: np.ndarray,
    labels: np.ndarray,
    rows: list[dict[str, Any]],
) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for split in ("train", "dev", "test"):
        indices = [index for index, row in enumerate(rows) if row["split"] == split]
        split_rows = [rows[index] for index in indices]
        result[split] = _metrics(
            accepted[indices],
            labels[indices],
            split_rows,
        )
    return result


def _load_linear(
    artifact_root: Path,
    input_form: str,
    rows: list[dict[str, Any]],
) -> tuple[np.ndarray, dict[str, Any]]:
    root = artifact_root / f"linear-{input_form}"
    vectorizer, model = joblib.load(root / "linear.joblib")
    features = vectorizer.transform([_linear_text(row, input_form) for row in rows])
    logits = np.log(np.clip(model.predict_proba(features), 1e-12, 1.0))
    metrics = json.loads((root / "metrics.json").read_text(encoding="utf-8"))
    return greeting_probabilities(logits, float(metrics["calibration"]["temperature"])), metrics


def _load_textcnn(
    artifact_root: Path,
    input_form: str,
    rows: list[dict[str, Any]],
) -> tuple[np.ndarray, dict[str, Any]]:
    root = artifact_root / f"textcnn-{input_form}"
    vocab = json.loads((root / "vocab.json").read_text(encoding="utf-8"))
    tokenizer = CharacterTokenizer(
        {str(key): int(value) for key, value in vocab["tokenToId"].items()},
        int(vocab["maxLength"]),
    )
    model = GreetingTextCNN(
        len(tokenizer.token_to_id),
        embedding_dimension=32,
        channels_per_kernel=32,
        kernel_sizes=(2, 3, 4),
        dropout=0.2,
    )
    state = torch.load(root / "checkpoint.pt", map_location="cpu", weights_only=True)
    model.load_state_dict(state)
    inputs = np.stack(
        [
            tokenizer.encode(
                str(row["userMessage"]),
                str(row["assistantReply"]),
                input_form,
            )
            for row in rows
        ]
    )
    logits = predict_logits(model, inputs)
    metrics = json.loads((root / "metrics.json").read_text(encoding="utf-8"))
    return greeting_probabilities(logits, float(metrics["calibration"]["temperature"])), metrics


def _gate(probabilities: np.ndarray, metrics: dict[str, Any]) -> np.ndarray:
    thresholds = metrics["ruleAndLearnedGate"]["thresholds"]
    margin = np.abs(2.0 * probabilities - 1.0)
    return (probabilities >= float(thresholds["confidence"])) & (
        margin >= float(thresholds["margin"])
    )


def evaluate_frozen_pilot(
    pilot_path: Path,
    artifact_root: Path,
) -> dict[str, Any]:
    rows = load_generated_pilot(pilot_path)
    labels = _labels(rows)
    rule = np.asarray(
        [deterministic_greeting_intent(str(row["userMessage"])) for row in rows],
        dtype=bool,
    )
    result: dict[str, Any] = {
        "evaluationPolicy": {
            "trainingPerformed": False,
            "thresholdTuned": False,
            "temperatureTuned": False,
            "modelReselected": False,
            "pilotUsedForSelection": False,
        },
        "rowCount": len(rows),
        "inputSha256": hashlib.sha256(pilot_path.read_bytes()).hexdigest(),
        "ruleOnly": {
            "overall": _metrics(rule, labels, rows),
            "bySplit": _split_metrics(rule, labels, rows),
        },
        "models": {},
    }
    cached: dict[str, tuple[np.ndarray, dict[str, Any]]] = {}
    for model_type in MODEL_TYPES:
        for input_form in INPUT_FORMS:
            key = f"{model_type}-{input_form}"
            if model_type == "linear":
                probabilities, locked = _load_linear(artifact_root, input_form, rows)
            else:
                probabilities, locked = _load_textcnn(artifact_root, input_form, rows)
            learned = _gate(probabilities, locked)
            cached[key] = (probabilities, locked)
            result["models"][key] = {
                "temperature": float(locked["calibration"]["temperature"]),
                "thresholds": locked["ruleAndLearnedGate"]["thresholds"],
                "calibrationEce10": expected_calibration_error(
                    probabilities, labels.astype(np.int64)
                ),
                "learnedOnly": {
                    "overall": _metrics(learned, labels, rows),
                    "bySplit": _split_metrics(learned, labels, rows),
                },
                "ruleAndLearned": {
                    "overall": _metrics(rule & learned, labels, rows),
                    "bySplit": _split_metrics(rule & learned, labels, rows),
                },
            }

    user_probabilities, user_locked = cached["textcnn-user_only"]
    user_learned = _gate(user_probabilities, user_locked)
    reply_guard = np.asarray(
        [reply_compatibility_guard(str(row["assistantReply"])) for row in rows],
        dtype=bool,
    )
    guarded = rule & user_learned & reply_guard
    result["ruleTextcnnUserAndReplyGuard"] = {
        "overall": _metrics(guarded, labels, rows),
        "bySplit": _split_metrics(guarded, labels, rows),
    }

    hello_index = next(
        index for index, row in enumerate(rows) if row["promptId"] == "r34a4-greeting-hello"
    )
    pair_probabilities, pair_locked = cached["textcnn-user_reply_pair"]
    pair_greeting_probability = float(pair_probabilities[hello_index])
    pair_prediction = "Greeting" if pair_greeting_probability >= 0.5 else "NotGreeting"
    result["knownFailureProbe"] = {
        "promptId": rows[hello_index]["promptId"],
        "pairSha256": rows[hello_index]["pairSha256"],
        "historicalPrediction": "NotGreeting",
        "historicalConfidence": 0.7337920665740967,
        "historicalMargin": 0.46758416295051577,
        "currentPairPrediction": pair_prediction,
        "currentPairConfidence": max(pair_greeting_probability, 1.0 - pair_greeting_probability),
        "currentPairMargin": abs(2.0 * pair_greeting_probability - 1.0),
        "currentPairEligible": bool(
            rule[hello_index] and _gate(pair_probabilities, pair_locked)[hello_index]
        ),
        "currentUserOnlyEligible": bool(
            rule[hello_index] and user_learned[hello_index]
        ),
        "ruleOnlyEligible": bool(rule[hello_index]),
        "sameRuntimeDomainNotClaimedByteIdentical": True,
    }
    rule_overall = result["ruleOnly"]["overall"]
    learned_strategies = {
        key: value["ruleAndLearned"]["overall"]
        for key, value in result["models"].items()
    }
    learned_strategies["textcnn-user_only-with-reply_guard"] = result[
        "ruleTextcnnUserAndReplyGuard"
    ]["overall"]
    clearly_better = [
        key
        for key, value in learned_strategies.items()
        if value["safetyCriticalFalsePositive"] == 0
        and value["precision"] >= rule_overall["precision"]
        and value["recall"] > rule_overall["recall"]
    ]
    result["pilotDecision"] = {
        "learnedStrategyClearlyBeatsRuleOnly": bool(clearly_better),
        "clearlyBetterStrategies": clearly_better,
        "continueToFullQwenDomainRetraining": bool(clearly_better),
        "recommendedWaveResearchGate": (
            clearly_better[0] if clearly_better else "deterministic_rule_only"
        ),
        "pairDomainShiftConfirmed": (
            result["models"]["textcnn-user_reply_pair"]["ruleAndLearned"]["overall"][
                "recall"
            ]
            < 0.75
        ),
    }
    return result
