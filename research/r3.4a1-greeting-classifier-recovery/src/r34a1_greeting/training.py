from __future__ import annotations

from copy import deepcopy
from dataclasses import dataclass
import random
import time

import numpy as np
import torch
from sklearn.metrics import fbeta_score
from torch import nn
from torch.utils.data import DataLoader, Dataset


def set_seed(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    torch.use_deterministic_algorithms(True)


class GreetingTensorDataset(Dataset):
    def __init__(self, input_ids: np.ndarray, labels: np.ndarray) -> None:
        self.input_ids = torch.from_numpy(input_ids).long()
        self.labels = torch.from_numpy(labels).long()

    def __len__(self) -> int:
        return len(self.input_ids)

    def __getitem__(self, index: int) -> tuple[torch.Tensor, torch.Tensor]:
        return self.input_ids[index], self.labels[index]


@dataclass
class TrainingResult:
    model: nn.Module
    best_epoch: int
    history: list[dict[str, float | int]]
    dev_logits: np.ndarray


def predict_logits(
    model: nn.Module, input_ids: np.ndarray, batch_size: int = 512
) -> np.ndarray:
    model.eval()
    outputs: list[np.ndarray] = []
    with torch.inference_mode():
        for offset in range(0, len(input_ids), batch_size):
            batch = torch.from_numpy(input_ids[offset : offset + batch_size]).long()
            outputs.append(model(batch).numpy())
    return np.concatenate(outputs)


def train_textcnn(
    model: nn.Module,
    train_inputs: np.ndarray,
    train_labels: np.ndarray,
    dev_inputs: np.ndarray,
    dev_labels: np.ndarray,
    *,
    seed: int,
    learning_rate: float,
    batch_size: int,
    epochs: int,
    patience: int,
) -> TrainingResult:
    set_seed(seed)
    dataset = GreetingTensorDataset(train_inputs, train_labels)
    generator = torch.Generator().manual_seed(seed)
    loader = DataLoader(dataset, batch_size=batch_size, shuffle=True, generator=generator)
    counts = np.bincount(train_labels, minlength=2).astype(np.float64)
    weights = 1.0 / np.sqrt(np.maximum(counts, 1.0))
    weights *= 2.0 / weights.sum()
    loss_function = nn.CrossEntropyLoss(torch.tensor(weights, dtype=torch.float32))
    optimizer = torch.optim.AdamW(model.parameters(), lr=learning_rate)

    best_score = -1.0
    best_epoch = 0
    best_state = deepcopy(model.state_dict())
    stale = 0
    history: list[dict[str, float | int]] = []
    for epoch in range(1, epochs + 1):
        model.train()
        total_loss = 0.0
        started = time.perf_counter()
        for input_ids, labels in loader:
            optimizer.zero_grad(set_to_none=True)
            logits = model(input_ids)
            loss = loss_function(logits, labels)
            loss.backward()
            optimizer.step()
            total_loss += float(loss.item()) * len(input_ids)
        dev_logits = predict_logits(model, dev_inputs)
        predictions = dev_logits.argmax(axis=1)
        f05 = fbeta_score(dev_labels, predictions, beta=0.5, zero_division=0)
        history.append(
            {
                "epoch": epoch,
                "loss": total_loss / len(dataset),
                "devF0.5": float(f05),
                "seconds": time.perf_counter() - started,
            }
        )
        if f05 > best_score + 1e-6:
            best_score = float(f05)
            best_epoch = epoch
            best_state = deepcopy(model.state_dict())
            stale = 0
        else:
            stale += 1
            if stale >= patience:
                break
    model.load_state_dict(best_state)
    return TrainingResult(model, best_epoch, history, predict_logits(model, dev_inputs))


def benchmark_batch_one(
    model: nn.Module, input_ids: np.ndarray, iterations: int = 200
) -> dict[str, float]:
    model.eval()
    timings: list[float] = []
    with torch.inference_mode():
        for index in range(iterations + 10):
            offset = index % len(input_ids)
            sample = torch.from_numpy(input_ids[offset : offset + 1]).long()
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
