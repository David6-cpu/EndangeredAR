from __future__ import annotations

from copy import deepcopy
from dataclasses import dataclass
import random
import time
from typing import Sequence

import numpy as np
import torch
from sklearn.metrics import f1_score
from torch import nn
from torch.utils.data import DataLoader, Dataset


def set_seed(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    torch.use_deterministic_algorithms(True)


class AffectTensorDataset(Dataset):
    def __init__(
        self, input_ids: np.ndarray, dialogue_labels: np.ndarray, emotion_labels: np.ndarray
    ) -> None:
        self.input_ids = torch.from_numpy(input_ids).long()
        self.dialogue_labels = torch.from_numpy(dialogue_labels).long()
        self.emotion_labels = torch.from_numpy(emotion_labels).long()

    def __len__(self) -> int:
        return len(self.input_ids)

    def __getitem__(self, index: int) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        return self.input_ids[index], self.dialogue_labels[index], self.emotion_labels[index]


def class_weights(labels: np.ndarray, class_count: int) -> torch.Tensor:
    counts = np.bincount(labels, minlength=class_count).astype(np.float64)
    weights = 1.0 / np.sqrt(np.maximum(counts, 1.0))
    weights *= class_count / weights.sum()
    return torch.tensor(weights, dtype=torch.float32)


@dataclass
class NeuralTrainingResult:
    model: nn.Module
    best_epoch: int
    history: list[dict[str, float | int]]
    dev_dialogue_logits: np.ndarray
    dev_emotion_logits: np.ndarray


def predict_logits(
    model: nn.Module, input_ids: np.ndarray, batch_size: int = 512
) -> tuple[np.ndarray, np.ndarray]:
    model.eval()
    dialogue: list[np.ndarray] = []
    emotion: list[np.ndarray] = []
    with torch.inference_mode():
        for offset in range(0, len(input_ids), batch_size):
            batch = torch.from_numpy(input_ids[offset : offset + batch_size]).long()
            dialogue_logits, emotion_logits = model(batch)
            dialogue.append(dialogue_logits.numpy())
            emotion.append(emotion_logits.numpy())
    return np.concatenate(dialogue), np.concatenate(emotion)


def train_neural_model(
    model: nn.Module,
    train_inputs: np.ndarray,
    train_dialogue: np.ndarray,
    train_emotion: np.ndarray,
    dev_inputs: np.ndarray,
    dev_dialogue: np.ndarray,
    dev_emotion: np.ndarray,
    dialogue_class_count: int,
    emotion_class_count: int,
    seed: int,
    learning_rate: float,
    batch_size: int,
    epochs: int,
    patience: int,
) -> NeuralTrainingResult:
    set_seed(seed)
    dataset = AffectTensorDataset(train_inputs, train_dialogue, train_emotion)
    generator = torch.Generator().manual_seed(seed)
    loader = DataLoader(dataset, batch_size=batch_size, shuffle=True, generator=generator)
    dialogue_loss = nn.CrossEntropyLoss(class_weights(train_dialogue, dialogue_class_count))
    emotion_loss = nn.CrossEntropyLoss(class_weights(train_emotion, emotion_class_count))
    optimizer = torch.optim.AdamW(model.parameters(), lr=learning_rate)

    best_score = -1.0
    best_epoch = 0
    best_state = deepcopy(model.state_dict())
    stale_epochs = 0
    history: list[dict[str, float | int]] = []

    for epoch in range(1, epochs + 1):
        model.train()
        epoch_loss = 0.0
        started = time.perf_counter()
        for input_ids, dialogue_labels, emotion_labels in loader:
            optimizer.zero_grad(set_to_none=True)
            dialogue_logits, emotion_logits = model(input_ids)
            loss = dialogue_loss(dialogue_logits, dialogue_labels) + emotion_loss(
                emotion_logits, emotion_labels
            )
            loss.backward()
            optimizer.step()
            epoch_loss += float(loss.item()) * len(input_ids)

        dev_dialogue_logits, dev_emotion_logits = predict_logits(model, dev_inputs)
        dialogue_macro = f1_score(
            dev_dialogue, dev_dialogue_logits.argmax(axis=1), average="macro", zero_division=0
        )
        emotion_macro = f1_score(
            dev_emotion, dev_emotion_logits.argmax(axis=1), average="macro", zero_division=0
        )
        combined = (dialogue_macro + emotion_macro) / 2.0
        history.append(
            {
                "epoch": epoch,
                "loss": epoch_loss / len(dataset),
                "dialogueMacroF1": float(dialogue_macro),
                "emotionMacroF1": float(emotion_macro),
                "combinedMacroF1": float(combined),
                "seconds": time.perf_counter() - started,
            }
        )
        if combined > best_score + 1e-5:
            best_score = combined
            best_epoch = epoch
            best_state = deepcopy(model.state_dict())
            stale_epochs = 0
        else:
            stale_epochs += 1
            if stale_epochs >= patience:
                break

    model.load_state_dict(best_state)
    dev_dialogue_logits, dev_emotion_logits = predict_logits(model, dev_inputs)
    return NeuralTrainingResult(
        model=model,
        best_epoch=best_epoch,
        history=history,
        dev_dialogue_logits=dev_dialogue_logits,
        dev_emotion_logits=dev_emotion_logits,
    )


def benchmark_batch_one(model: nn.Module, input_ids: np.ndarray, iterations: int = 200) -> dict[str, float]:
    model.eval()
    sample_count = min(len(input_ids), iterations)
    timings: list[float] = []
    with torch.inference_mode():
        for index in range(sample_count + 10):
            sample = torch.from_numpy(input_ids[index % len(input_ids) : index % len(input_ids) + 1]).long()
            started = time.perf_counter()
            model(sample)
            elapsed = (time.perf_counter() - started) * 1000.0
            if index >= 10:
                timings.append(elapsed)
    return {
        "meanMs": float(np.mean(timings)),
        "p50Ms": float(np.percentile(timings, 50)),
        "p95Ms": float(np.percentile(timings, 95)),
    }
