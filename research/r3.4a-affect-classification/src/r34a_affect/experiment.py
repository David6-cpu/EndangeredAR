from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import platform
import sys
import time
from typing import Any, Sequence

import joblib
import numpy as np
import sklearn
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression
from sklearn.multiclass import OneVsRestClassifier
import torch

from .data import CPED_SPLITS, load_cped, load_gold
from .evaluation import (
    classification_metrics,
    evaluate_greeting_gate,
    fit_temperature,
    tune_greeting_gate,
)
from .exporting import export_and_verify, sha256
from .models import DualHeadBiLSTM, DualHeadTextCNN, parameter_count
from .schema import AffectExample, LabelSchema
from .tokenizer import CharacterTokenizer
from .training import benchmark_batch_one, predict_logits, set_seed, train_neural_model


@dataclass
class RunOutput:
    key: str
    model_name: str
    input_form: str
    metrics: dict[str, Any]
    predict_gold: Any


def _json_write(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _labels(
    examples: Sequence[AffectExample], schema: LabelSchema
) -> tuple[np.ndarray, np.ndarray]:
    dialogue = np.asarray([schema.dialogue_id(item.dialogue_act) for item in examples], dtype=np.int64)
    emotion = np.asarray([schema.emotion_id(item.emotion_tone) for item in examples], dtype=np.int64)
    return dialogue, emotion


def _linear_text(example: AffectExample, input_form: str) -> str:
    if input_form == "reply_only":
        return f"[ASSISTANT]\n{example.reply}"
    if input_form == "user_reply_pair":
        return f"[USER]\n{example.user}\n[ASSISTANT]\n{example.reply}"
    raise ValueError(f"unsupported input form: {input_form}")


def _head_bundle(
    dialogue_logits: np.ndarray,
    emotion_logits: np.ndarray,
    dialogue_labels: np.ndarray,
    emotion_labels: np.ndarray,
    schema: LabelSchema,
    dialogue_temperature: float,
    emotion_temperature: float,
) -> dict[str, object]:
    return {
        "dialogue": classification_metrics(
            dialogue_logits, dialogue_labels, schema.dialogue_acts, dialogue_temperature
        ),
        "emotion": classification_metrics(
            emotion_logits, emotion_labels, schema.emotion_tones, emotion_temperature
        ),
    }


def _complete_metrics(
    dev_logits: tuple[np.ndarray, np.ndarray],
    test_logits: tuple[np.ndarray, np.ndarray],
    dev_labels: tuple[np.ndarray, np.ndarray],
    test_labels: tuple[np.ndarray, np.ndarray],
    schema: LabelSchema,
) -> dict[str, object]:
    dialogue_temperature = fit_temperature(dev_logits[0], dev_labels[0])
    emotion_temperature = fit_temperature(dev_logits[1], dev_labels[1])
    gate = tune_greeting_gate(
        dev_logits[0], dev_labels[0], schema.dialogue_acts, dialogue_temperature
    )
    return {
        "calibration": {
            "dialogueTemperature": dialogue_temperature,
            "emotionTemperature": emotion_temperature,
            "greetingGate": gate,
        },
        "dev": _head_bundle(
            *dev_logits,
            *dev_labels,
            schema,
            dialogue_temperature,
            emotion_temperature,
        ),
        "test": _head_bundle(
            *test_logits,
            *test_labels,
            schema,
            dialogue_temperature,
            emotion_temperature,
        ),
        "greetingGate": {
            "dev": evaluate_greeting_gate(
                dev_logits[0],
                dev_labels[0],
                schema.dialogue_acts,
                dialogue_temperature,
                gate,
            ),
            "test": evaluate_greeting_gate(
                test_logits[0],
                test_labels[0],
                schema.dialogue_acts,
                dialogue_temperature,
                gate,
            ),
        },
    }


def _benchmark_linear(
    vectorizer: TfidfVectorizer,
    dialogue_model: OneVsRestClassifier,
    emotion_model: OneVsRestClassifier,
    examples: Sequence[AffectExample],
    input_form: str,
    iterations: int = 200,
) -> dict[str, float]:
    timings: list[float] = []
    for index in range(min(len(examples), iterations) + 10):
        text = [_linear_text(examples[index % len(examples)], input_form)]
        started = time.perf_counter()
        features = vectorizer.transform(text)
        dialogue_model.predict_proba(features)
        emotion_model.predict_proba(features)
        elapsed = (time.perf_counter() - started) * 1000.0
        if index >= 10:
            timings.append(elapsed)
    return {
        "meanMs": float(np.mean(timings)),
        "p50Ms": float(np.percentile(timings, 50)),
        "p95Ms": float(np.percentile(timings, 95)),
    }


def run_linear(
    input_form: str,
    splits: dict[str, list[AffectExample]],
    gold: list[AffectExample],
    schema: LabelSchema,
    config: dict[str, Any],
    output_root: Path,
    seed: int,
) -> RunOutput:
    set_seed(seed)
    model_root = output_root / f"linear-{input_form}"
    model_root.mkdir(parents=True, exist_ok=True)
    vectorizer = TfidfVectorizer(
        analyzer="char",
        ngram_range=(1, 3),
        max_features=int(config["linear"]["maxFeatures"]),
        min_df=2,
        sublinear_tf=True,
        dtype=np.float32,
    )
    train_text = [_linear_text(item, input_form) for item in splits["train"]]
    train_features = vectorizer.fit_transform(train_text)
    dev_features = vectorizer.transform([_linear_text(item, input_form) for item in splits["dev"]])
    test_features = vectorizer.transform([_linear_text(item, input_form) for item in splits["test"]])
    train_labels = _labels(splits["train"], schema)
    dev_labels = _labels(splits["dev"], schema)
    test_labels = _labels(splits["test"], schema)

    def fit_head(labels: np.ndarray) -> OneVsRestClassifier:
        return OneVsRestClassifier(
            LogisticRegression(
                class_weight="balanced",
                max_iter=int(config["linear"]["maxIterations"]),
                random_state=seed,
                solver="liblinear",
            )
        ).fit(train_features, labels)

    dialogue_model = fit_head(train_labels[0])
    emotion_model = fit_head(train_labels[1])
    dev_logits = (
        np.log(np.clip(dialogue_model.predict_proba(dev_features), 1e-12, 1.0)),
        np.log(np.clip(emotion_model.predict_proba(dev_features), 1e-12, 1.0)),
    )
    test_logits = (
        np.log(np.clip(dialogue_model.predict_proba(test_features), 1e-12, 1.0)),
        np.log(np.clip(emotion_model.predict_proba(test_features), 1e-12, 1.0)),
    )
    metrics = _complete_metrics(dev_logits, test_logits, dev_labels, test_labels, schema)
    parameter_total = int(
        sum(model.coef_.size + model.intercept_.size for model in dialogue_model.estimators_)
        + sum(model.coef_.size + model.intercept_.size for model in emotion_model.estimators_)
    )
    artifact = model_root / "linear.joblib"
    joblib.dump((vectorizer, dialogue_model, emotion_model), artifact)
    metrics.update(
        {
            "model": "linear_logistic_regression",
            "inputForm": input_form,
            "featureCount": len(vectorizer.vocabulary_),
            "parameterCount": parameter_total,
            "artifactBytes": artifact.stat().st_size,
            "artifactSha256": sha256(artifact),
            "macBatchOneLatency": _benchmark_linear(
                vectorizer, dialogue_model, emotion_model, splits["test"], input_form
            ),
        }
    )

    def predict_gold() -> tuple[tuple[np.ndarray, np.ndarray], tuple[np.ndarray, np.ndarray]]:
        features = vectorizer.transform([_linear_text(item, input_form) for item in gold])
        return (
            (
                np.log(np.clip(dialogue_model.predict_proba(features), 1e-12, 1.0)),
                np.log(np.clip(emotion_model.predict_proba(features), 1e-12, 1.0)),
            ),
            _labels(gold, schema),
        )

    _json_write(model_root / "metrics.json", metrics)
    return RunOutput(f"linear-{input_form}", "linear", input_form, metrics, predict_gold)


def _build_neural_model(
    model_name: str,
    vocab_size: int,
    schema: LabelSchema,
    config: dict[str, Any],
) -> torch.nn.Module:
    if model_name == "textcnn":
        values = config["textCnn"]
        return DualHeadTextCNN(
            vocab_size,
            len(schema.dialogue_acts),
            len(schema.emotion_tones),
            embedding_dimension=int(values["embeddingDimension"]),
            channels_per_kernel=int(values["channelsPerKernel"]),
            kernel_sizes=tuple(int(value) for value in values["kernelSizes"]),
            dropout=float(values["dropout"]),
        )
    if model_name == "bilstm":
        values = config["biLstm"]
        return DualHeadBiLSTM(
            vocab_size,
            len(schema.dialogue_acts),
            len(schema.emotion_tones),
            embedding_dimension=int(values["embeddingDimension"]),
            hidden_dimension=int(values["hiddenDimension"]),
            dropout=float(values["dropout"]),
        )
    raise ValueError(f"unsupported neural model: {model_name}")


def run_neural(
    model_name: str,
    input_form: str,
    splits: dict[str, list[AffectExample]],
    gold: list[AffectExample],
    schema: LabelSchema,
    config: dict[str, Any],
    output_root: Path,
    seed: int,
) -> RunOutput:
    set_seed(seed)
    model_root = output_root / f"{model_name}-{input_form}"
    model_root.mkdir(parents=True, exist_ok=True)
    token_config = config["tokenizer"]
    tokenizer = CharacterTokenizer.build(
        splits["train"],
        int(token_config["vocabSize"]),
        int(token_config["maxSequenceLength"]),
        input_form,
    )
    tokenizer.save(model_root / "vocab.json")
    encoded = {
        split: tokenizer.encode_many(rows, input_form) for split, rows in splits.items()
    }
    labels = {split: _labels(rows, schema) for split, rows in splits.items()}
    model = _build_neural_model(model_name, len(tokenizer.token_to_id), schema, config)
    training_config = config["training"]
    trained = train_neural_model(
        model,
        encoded["train"],
        *labels["train"],
        encoded["dev"],
        *labels["dev"],
        len(schema.dialogue_acts),
        len(schema.emotion_tones),
        seed,
        float(training_config["learningRate"]),
        int(training_config["batchSize"]),
        int(training_config["epochs"]),
        int(training_config["earlyStoppingPatience"]),
    )
    dev_logits = (trained.dev_dialogue_logits, trained.dev_emotion_logits)
    test_logits = predict_logits(trained.model, encoded["test"])
    metrics = _complete_metrics(dev_logits, test_logits, labels["dev"], labels["test"], schema)
    checkpoint = model_root / "checkpoint.pt"
    torch.save(trained.model.state_dict(), checkpoint)
    onnx_path = model_root / f"{model_name}.onnx"
    onnx_result = export_and_verify(
        trained.model,
        encoded["test"],
        onnx_path,
        int(config["onnx"]["opset"]),
        config["onnx"]["outputNames"],
    )
    metrics.update(
        {
            "model": model_name,
            "inputForm": input_form,
            "vocabSize": len(tokenizer.token_to_id),
            "maxSequenceLength": tokenizer.max_length,
            "parameterCount": parameter_count(trained.model),
            "parameterBytesFloat32": parameter_count(trained.model) * 4,
            "checkpointBytes": checkpoint.stat().st_size,
            "checkpointSha256": sha256(checkpoint),
            "bestEpoch": trained.best_epoch,
            "trainingHistory": trained.history,
            "macBatchOneLatency": benchmark_batch_one(trained.model, encoded["test"]),
            "onnx": onnx_result,
        }
    )

    def predict_gold() -> tuple[tuple[np.ndarray, np.ndarray], tuple[np.ndarray, np.ndarray]]:
        gold_inputs = tokenizer.encode_many(gold, input_form)
        return predict_logits(trained.model, gold_inputs), _labels(gold, schema)

    _json_write(model_root / "metrics.json", metrics)
    return RunOutput(f"{model_name}-{input_form}", model_name, input_form, metrics, predict_gold)


def _selection_key(run: RunOutput) -> tuple[float, float, float, int]:
    gate = run.metrics["greetingGate"]["dev"]
    dev = run.metrics["dev"]
    combined_macro = (dev["dialogue"]["macroF1"] + dev["emotion"]["macroF1"]) / 2.0
    size = int(run.metrics.get("onnx", {}).get("bytes", run.metrics.get("artifactBytes", 0)))
    return float(gate["precision"]), float(combined_macro), float(gate["recall"]), -size


def _gold_metrics(run: RunOutput, schema: LabelSchema) -> dict[str, object]:
    logits, labels = run.predict_gold()
    calibration = run.metrics["calibration"]
    dialogue_temperature = float(calibration["dialogueTemperature"])
    emotion_temperature = float(calibration["emotionTemperature"])
    gate = calibration["greetingGate"]
    return {
        **_head_bundle(
            *logits,
            *labels,
            schema,
            dialogue_temperature,
            emotion_temperature,
        ),
        "greetingGate": evaluate_greeting_gate(
            logits[0], labels[0], schema.dialogue_acts, dialogue_temperature, gate
        ),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cped-root", type=Path, required=True)
    parser.add_argument("--project-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument(
        "--models", nargs="+", choices=("linear", "textcnn", "bilstm"), default=("linear", "textcnn", "bilstm")
    )
    parser.add_argument(
        "--input-forms", nargs="+", choices=("reply_only", "user_reply_pair"), default=("reply_only", "user_reply_pair")
    )
    args = parser.parse_args()

    research_root = args.project_root / "research/r3.4a-affect-classification"
    config = json.loads((research_root / "configs/research.json").read_text(encoding="utf-8"))
    schema = LabelSchema.load(research_root / "labels/label_mapping.json")
    gold = load_gold(research_root / "data/endangeredar_gold.jsonl")
    splits, dataset_manifest = load_cped(args.cped_root, schema, gold)
    args.output.mkdir(parents=True, exist_ok=True)
    seed = int(config["seed"])
    manifest = {
        "experimentVersion": config["experimentVersion"],
        "seed": seed,
        "cpedFiles": {
            split: {
                "name": filename,
                "sha256": _file_sha256(args.cped_root / filename),
            }
            for split, filename in CPED_SPLITS.items()
        },
        "dataset": dataset_manifest,
        "goldCount": len(gold),
        "environment": {
            "python": platform.python_version(),
            "numpy": np.__version__,
            "scikitLearn": sklearn.__version__,
            "torch": torch.__version__,
            "platform": platform.system(),
            "machine": platform.machine(),
        },
        "config": config,
    }
    _json_write(args.output / "run-manifest.json", manifest)

    runs: list[RunOutput] = []
    for input_form in args.input_forms:
        for model_name in args.models:
            print(f"START {model_name} {input_form}", flush=True)
            if model_name == "linear":
                run = run_linear(input_form, splits, gold, schema, config, args.output, seed)
            else:
                run = run_neural(
                    model_name, input_form, splits, gold, schema, config, args.output, seed
                )
            runs.append(run)
            print(f"DONE {run.key}", flush=True)

    selected = max(runs, key=_selection_key)
    selected_gold = _gold_metrics(selected, schema)
    summary = {
        "selectionRule": "dev gated Greeting precision, then combined dual-head Macro-F1, Greeting recall, then smaller artifact",
        "selected": selected.key,
        "goldEvaluatedOnlyAfterSelection": True,
        "gold": selected_gold,
        "runs": {run.key: run.metrics for run in runs},
    }
    _json_write(args.output / "metrics-summary.json", summary)
    print(f"SELECTED {selected.key}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
