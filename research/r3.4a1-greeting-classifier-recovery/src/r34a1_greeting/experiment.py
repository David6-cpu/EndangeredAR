from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import platform
import time
from typing import Any, Callable, Sequence

import joblib
import numpy as np
import sklearn
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
import torch

from .data import assert_gold_isolation, split_examples
from .evaluation import (
    binary_metrics,
    evaluate_binary_gate,
    fit_temperature,
    greeting_probabilities,
    precision_recall_points,
    select_binary_gate,
)
from .generation import build_gold_v2, build_project_corpus
from .models import GreetingTextCNN, parameter_count
from .rules import deterministic_greeting_intent
from .schema import GreetingExample
from .tokenizer import CharacterTokenizer
from .training import benchmark_batch_one, predict_logits, set_seed, train_textcnn


@dataclass
class RunOutput:
    key: str
    model_name: str
    input_form: str
    metrics: dict[str, Any]
    gold_predictor: Callable[[Sequence[GreetingExample]], tuple[np.ndarray, np.ndarray]]


def _json_write(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _labels(rows: Sequence[GreetingExample]) -> np.ndarray:
    return np.asarray([1 if row.label == "Greeting" else 0 for row in rows], dtype=np.int64)


def _input_key(row: GreetingExample, input_form: str) -> str:
    if input_form == "user_only":
        return row.user
    if input_form == "reply_only":
        return row.reply
    if input_form == "user_reply_pair":
        return f"{row.user}\n{row.reply}"
    raise ValueError(f"unsupported input form: {input_form}")


def _effective_input_counts(
    rows: Sequence[GreetingExample], input_form: str
) -> dict[str, int | bool]:
    positive = {
        _input_key(row, input_form) for row in rows if row.label == "Greeting"
    }
    hard_negative = {
        _input_key(row, input_form)
        for row in rows
        if row.label == "NotGreeting" and row.safety_critical
    }
    return {
        "uniqueGreeting": len(positive),
        "uniqueSafetyCriticalNegative": len(hard_negative),
        "meetsTestStabilityTarget": len(positive) >= 70 and len(hard_negative) >= 70,
        "meetsGoldV2Target": len(positive) >= 100 and len(hard_negative) >= 200,
    }


def _rule_mask(rows: Sequence[GreetingExample]) -> np.ndarray:
    return np.asarray([deterministic_greeting_intent(row.user) for row in rows], dtype=bool)


def _linear_text(row: GreetingExample, input_form: str) -> str:
    if input_form == "user_only":
        return f"[USER]\n{row.user}"
    if input_form == "reply_only":
        return f"[ASSISTANT]\n{row.reply}"
    if input_form == "user_reply_pair":
        return f"[USER]\n{row.user}\n[ASSISTANT]\n{row.reply}"
    raise ValueError(f"unsupported input form: {input_form}")


def _hard_negative_false_positives(
    probabilities: np.ndarray,
    rows: Sequence[GreetingExample],
    gate: dict[str, float | int | bool],
    rule_mask: np.ndarray | None,
) -> int:
    margin = np.abs(2.0 * probabilities - 1.0)
    accepted = (probabilities >= float(gate["confidence"])) & (
        margin >= float(gate["margin"])
    )
    if rule_mask is not None:
        accepted &= rule_mask
    return int(
        sum(
            bool(value) and row.label == "NotGreeting" and row.safety_critical
            for value, row in zip(accepted, rows)
        )
    )


def _rule_metrics(rows: Sequence[GreetingExample]) -> dict[str, float | int]:
    labels = _labels(rows)
    accepted = _rule_mask(rows)
    positives = labels == 1
    true_positive = int(np.sum(accepted & positives))
    false_positive = int(np.sum(accepted & ~positives))
    false_negative = int(np.sum(~accepted & positives))
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
        "truePositive": true_positive,
        "falsePositive": false_positive,
        "falseNegative": false_negative,
        "safetyCriticalFalsePositive": int(
            sum(
                bool(value) and row.label == "NotGreeting" and row.safety_critical
                for value, row in zip(accepted, rows)
            )
        ),
    }


def _evaluate_model(
    dev_logits: np.ndarray,
    test_logits: np.ndarray,
    dev_rows: Sequence[GreetingExample],
    test_rows: Sequence[GreetingExample],
    config: dict[str, Any],
) -> dict[str, Any]:
    dev_labels = _labels(dev_rows)
    test_labels = _labels(test_rows)
    temperature = fit_temperature(dev_logits, dev_labels)
    dev_probabilities = greeting_probabilities(dev_logits, temperature)
    test_probabilities = greeting_probabilities(test_logits, temperature)
    target = config["qualityGate"]
    learned_gate = select_binary_gate(
        dev_probabilities,
        dev_labels,
        float(target["minimumPrecision"]),
        float(target["minimumRecall"]),
    )
    dual_gate = select_binary_gate(
        dev_probabilities,
        dev_labels,
        float(target["minimumPrecision"]),
        float(target["minimumRecall"]),
        _rule_mask(dev_rows),
    )
    return {
        "calibration": {
            "temperature": temperature,
            "devEce10": binary_metrics(dev_probabilities, dev_labels)["ece10"],
        },
        "raw": {
            "dev": binary_metrics(dev_probabilities, dev_labels),
            "test": binary_metrics(test_probabilities, test_labels),
        },
        "precisionRecallCurveDev": precision_recall_points(dev_probabilities, dev_labels),
        "learnedGate": {
            "thresholds": learned_gate,
            "dev": evaluate_binary_gate(dev_probabilities, dev_labels, learned_gate),
            "test": evaluate_binary_gate(test_probabilities, test_labels, learned_gate),
            "testSafetyCriticalFalsePositive": _hard_negative_false_positives(
                test_probabilities, test_rows, learned_gate, None
            ),
        },
        "ruleAndLearnedGate": {
            "thresholds": dual_gate,
            "dev": evaluate_binary_gate(
                dev_probabilities, dev_labels, dual_gate, _rule_mask(dev_rows)
            ),
            "test": evaluate_binary_gate(
                test_probabilities, test_labels, dual_gate, _rule_mask(test_rows)
            ),
            "testSafetyCriticalFalsePositive": _hard_negative_false_positives(
                test_probabilities, test_rows, dual_gate, _rule_mask(test_rows)
            ),
        },
    }


def _benchmark_linear(
    vectorizer: TfidfVectorizer,
    model: LogisticRegression,
    rows: Sequence[GreetingExample],
    input_form: str,
    iterations: int = 200,
) -> dict[str, float]:
    timings: list[float] = []
    for index in range(iterations + 10):
        row = rows[index % len(rows)]
        started = time.perf_counter()
        model.predict_proba(vectorizer.transform([_linear_text(row, input_form)]))
        if index >= 10:
            timings.append((time.perf_counter() - started) * 1000.0)
    return {
        "meanMs": float(np.mean(timings)),
        "p50Ms": float(np.percentile(timings, 50)),
        "p95Ms": float(np.percentile(timings, 95)),
    }


def run_linear(
    input_form: str,
    splits: dict[str, list[GreetingExample]],
    config: dict[str, Any],
    output_root: Path,
    seed: int,
) -> RunOutput:
    set_seed(seed)
    model_root = output_root / f"linear-{input_form}"
    model_root.mkdir(parents=True, exist_ok=True)
    values = config["linear"]
    vectorizer = TfidfVectorizer(
        analyzer="char",
        ngram_range=(1, 4),
        max_features=int(values["maxFeatures"]),
        min_df=2,
        sublinear_tf=True,
        dtype=np.float32,
    )
    train_features = vectorizer.fit_transform(
        [_linear_text(row, input_form) for row in splits["train"]]
    )
    dev_features = vectorizer.transform(
        [_linear_text(row, input_form) for row in splits["dev"]]
    )
    test_features = vectorizer.transform(
        [_linear_text(row, input_form) for row in splits["test"]]
    )
    model = LogisticRegression(
        class_weight="balanced",
        max_iter=int(values["maxIterations"]),
        random_state=seed,
        solver="liblinear",
    ).fit(train_features, _labels(splits["train"]))
    dev_logits = np.log(np.clip(model.predict_proba(dev_features), 1e-12, 1.0))
    test_logits = np.log(np.clip(model.predict_proba(test_features), 1e-12, 1.0))
    metrics = _evaluate_model(
        dev_logits, test_logits, splits["dev"], splits["test"], config
    )
    artifact = model_root / "linear.joblib"
    joblib.dump((vectorizer, model), artifact)
    metrics.update(
        {
            "model": "character_ngram_logistic_regression",
            "inputForm": input_form,
            "featureCount": len(vectorizer.vocabulary_),
            "parameterCount": int(model.coef_.size + model.intercept_.size),
            "artifactBytes": artifact.stat().st_size,
            "artifactSha256": _sha256(artifact),
            "macBatchOneLatency": _benchmark_linear(
                vectorizer, model, splits["test"], input_form
            ),
            "effectiveInputCounts": {
                "test": _effective_input_counts(splits["test"], input_form),
            },
        }
    )

    def predict_gold(rows: Sequence[GreetingExample]) -> tuple[np.ndarray, np.ndarray]:
        features = vectorizer.transform([_linear_text(row, input_form) for row in rows])
        return np.log(np.clip(model.predict_proba(features), 1e-12, 1.0)), _labels(rows)

    _json_write(model_root / "metrics.json", metrics)
    return RunOutput(f"linear-{input_form}", "linear", input_form, metrics, predict_gold)


def run_textcnn(
    input_form: str,
    splits: dict[str, list[GreetingExample]],
    config: dict[str, Any],
    output_root: Path,
    seed: int,
) -> RunOutput:
    set_seed(seed)
    model_root = output_root / f"textcnn-{input_form}"
    model_root.mkdir(parents=True, exist_ok=True)
    token_values = config["tokenizer"]
    tokenizer = CharacterTokenizer.build(
        splits["train"],
        int(token_values["vocabSize"]),
        int(token_values["maxSequenceLength"]),
        input_form,
    )
    tokenizer.save(model_root / "vocab.json")
    encoded = {
        split: tokenizer.encode_many(rows, input_form) for split, rows in splits.items()
    }
    labels = {split: _labels(rows) for split, rows in splits.items()}
    values = config["textCnn"]
    model = GreetingTextCNN(
        len(tokenizer.token_to_id),
        embedding_dimension=int(values["embeddingDimension"]),
        channels_per_kernel=int(values["channelsPerKernel"]),
        kernel_sizes=tuple(int(value) for value in values["kernelSizes"]),
        dropout=float(values["dropout"]),
    )
    training_values = config["training"]
    trained = train_textcnn(
        model,
        encoded["train"],
        labels["train"],
        encoded["dev"],
        labels["dev"],
        seed=seed,
        learning_rate=float(training_values["learningRate"]),
        batch_size=int(training_values["batchSize"]),
        epochs=int(training_values["epochs"]),
        patience=int(training_values["earlyStoppingPatience"]),
    )
    test_logits = predict_logits(trained.model, encoded["test"])
    metrics = _evaluate_model(
        trained.dev_logits, test_logits, splits["dev"], splits["test"], config
    )
    checkpoint = model_root / "checkpoint.pt"
    torch.save(trained.model.state_dict(), checkpoint)
    metrics.update(
        {
            "model": "character_textcnn",
            "inputForm": input_form,
            "vocabSize": len(tokenizer.token_to_id),
            "maxSequenceLength": tokenizer.max_length,
            "parameterCount": parameter_count(trained.model),
            "parameterBytesFloat32": parameter_count(trained.model) * 4,
            "checkpointBytes": checkpoint.stat().st_size,
            "checkpointSha256": _sha256(checkpoint),
            "bestEpoch": trained.best_epoch,
            "trainingHistory": trained.history,
            "macBatchOneLatency": benchmark_batch_one(trained.model, encoded["test"]),
            "effectiveInputCounts": {
                "test": _effective_input_counts(splits["test"], input_form),
            },
        }
    )

    def predict_gold(rows: Sequence[GreetingExample]) -> tuple[np.ndarray, np.ndarray]:
        return (
            predict_logits(trained.model, tokenizer.encode_many(rows, input_form)),
            _labels(rows),
        )

    _json_write(model_root / "metrics.json", metrics)
    return RunOutput(f"textcnn-{input_form}", "textcnn", input_form, metrics, predict_gold)


def _selection_key(run: RunOutput) -> tuple[bool, bool, float, float, float, int]:
    gate = run.metrics["ruleAndLearnedGate"]
    thresholds = gate["thresholds"]
    dev = gate["dev"]
    size = int(run.metrics.get("artifactBytes", run.metrics.get("checkpointBytes", 0)))
    return (
        bool(run.metrics["effectiveInputCounts"]["test"]["meetsTestStabilityTarget"]),
        bool(thresholds["qualityTargetMet"]),
        float(dev["f0.5"]),
        float(dev["precision"]),
        float(dev["recall"]),
        -size,
    )


def _gold_metrics(
    run: RunOutput, gold: list[GreetingExample]
) -> dict[str, Any]:
    logits, labels = run.gold_predictor(gold)
    temperature = float(run.metrics["calibration"]["temperature"])
    probabilities = greeting_probabilities(logits, temperature)
    learned_gate = run.metrics["learnedGate"]["thresholds"]
    dual_gate = run.metrics["ruleAndLearnedGate"]["thresholds"]
    rule_mask = _rule_mask(gold)
    return {
        "raw": binary_metrics(probabilities, labels),
        "learnedGate": {
            **evaluate_binary_gate(probabilities, labels, learned_gate),
            "safetyCriticalFalsePositive": _hard_negative_false_positives(
                probabilities, gold, learned_gate, None
            ),
        },
        "ruleAndLearnedGate": {
            **evaluate_binary_gate(probabilities, labels, dual_gate, rule_mask),
            "safetyCriticalFalsePositive": _hard_negative_false_positives(
                probabilities, gold, dual_gate, rule_mask
            ),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    project_root = args.project_root.resolve()
    output_root = args.output.resolve()
    if output_root == project_root or project_root in output_root.parents:
        raise ValueError("generated model artifacts must remain outside the repository")
    output_root.mkdir(parents=True, exist_ok=True)

    research_root = project_root / "research/r3.4a1-greeting-classifier-recovery"
    config = json.loads((research_root / "configs/recovery.json").read_text(encoding="utf-8"))
    corpus = build_project_corpus()
    splits = split_examples(corpus)
    seed = int(config["seed"])
    set_seed(seed)

    manifest = {
        "experimentVersion": config["experimentVersion"],
        "seed": seed,
        "goldEvaluatedOnlyAfterSelection": True,
        "goldUsedForModelSelection": False,
        "cpedUsedForProductTraining": False,
        "emotionHeadTrained": False,
        "biLstmRetested": False,
        "environment": {
            "python": platform.python_version(),
            "numpy": np.__version__,
            "scikitLearn": sklearn.__version__,
            "torch": torch.__version__,
            "platform": platform.system(),
            "machine": platform.machine(),
        },
        "dataset": {
            "count": len(corpus),
            "splitCounts": {name: len(rows) for name, rows in splits.items()},
        },
        "config": config,
    }

    runs: list[RunOutput] = []
    for input_form in config["inputForms"]:
        print(f"START linear {input_form}", flush=True)
        runs.append(run_linear(input_form, splits, config, output_root, seed))
        print(f"DONE linear {input_form}", flush=True)
        print(f"START textcnn {input_form}", flush=True)
        runs.append(run_textcnn(input_form, splits, config, output_root, seed))
        print(f"DONE textcnn {input_form}", flush=True)

    selected = max(runs, key=_selection_key)
    gold = build_gold_v2()
    assert_gold_isolation(splits, gold)
    manifest["dataset"].update(
        {
            "goldCount": len(gold),
            "goldReviewStatus": sorted({row.review_status for row in gold}),
        }
    )
    _json_write(output_root / "run-manifest.json", manifest)
    gold_results = {run.key: _gold_metrics(run, gold) for run in runs}
    selected_gold = gold_results[selected.key]["ruleAndLearnedGate"]
    selected_test = selected.metrics["ruleAndLearnedGate"]["test"]
    target = config["qualityGate"]
    test_pass = (
        float(selected_test["precision"]) >= float(target["minimumPrecision"])
        and float(selected_test["recall"]) >= float(target["minimumRecall"])
        and int(selected.metrics["ruleAndLearnedGate"]["testSafetyCriticalFalsePositive"])
        <= int(target["maximumSafetyCriticalFalsePositives"])
    )
    gold_pass = (
        float(selected_gold["precision"]) >= float(target["minimumPrecision"])
        and float(selected_gold["recall"]) >= float(target["minimumRecall"])
        and int(selected_gold["safetyCriticalFalsePositive"])
        <= int(target["maximumSafetyCriticalFalsePositives"])
    )
    data_adequate = bool(
        _effective_input_counts(gold, selected.input_form)["meetsGoldV2Target"]
    )
    gold_human_review_complete = all(
        row.review_status == "human_reviewed_approved" for row in gold
    )
    numerical_metrics_pass = test_pass and gold_pass
    product_quality_gate_pass = (
        numerical_metrics_pass and data_adequate and gold_human_review_complete
    )
    summary = {
        "selectionRule": "Dev-only rule-and-learned F0.5, precision, recall, then smaller artifact",
        "selectedBeforeGold": selected.key,
        "ruleOnly": {
            "dev": _rule_metrics(splits["dev"]),
            "test": _rule_metrics(splits["test"]),
            "gold": _rule_metrics(gold),
        },
        "runs": {run.key: run.metrics for run in runs},
        "goldPostSelectionDiagnostics": gold_results,
        "goldEffectiveInputCounts": {
            run.input_form: _effective_input_counts(gold, run.input_form)
            for run in runs
        },
        "qualityGate": {
            "testPassed": test_pass,
            "goldPassed": gold_pass,
            "numericalMetricsPassed": numerical_metrics_pass,
            "selectedInputDataAdequate": data_adequate,
            "goldFullyHumanReviewed": gold_human_review_complete,
            "productQualityGatePassed": product_quality_gate_pass,
            "deploymentWorkAuthorized": product_quality_gate_pass,
        },
    }
    _json_write(output_root / "metrics-summary.json", summary)
    print(f"SELECTED {selected.key}", flush=True)
    print(f"NUMERICAL_METRICS_PASSED {numerical_metrics_pass}", flush=True)
    print(f"PRODUCT_QUALITY_GATE_PASSED {product_quality_gate_pass}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
