from __future__ import annotations

import torch
from torch import nn


class DualHeadTextCNN(nn.Module):
    def __init__(
        self,
        vocab_size: int,
        dialogue_classes: int,
        emotion_classes: int,
        embedding_dimension: int = 48,
        channels_per_kernel: int = 48,
        kernel_sizes: tuple[int, ...] = (2, 3, 4),
        dropout: float = 0.2,
    ) -> None:
        super().__init__()
        self.embedding = nn.Embedding(vocab_size, embedding_dimension, padding_idx=0)
        self.convolutions = nn.ModuleList(
            nn.Conv1d(embedding_dimension, channels_per_kernel, kernel)
            for kernel in kernel_sizes
        )
        hidden = channels_per_kernel * len(kernel_sizes)
        self.dropout = nn.Dropout(dropout)
        self.dialogue_head = nn.Linear(hidden, dialogue_classes)
        self.emotion_head = nn.Linear(hidden, emotion_classes)

    def forward(self, input_ids: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        embedded = self.embedding(input_ids).transpose(1, 2)
        pooled = [torch.relu(layer(embedded)).amax(dim=2) for layer in self.convolutions]
        features = self.dropout(torch.cat(pooled, dim=1))
        return self.dialogue_head(features), self.emotion_head(features)


class DualHeadBiLSTM(nn.Module):
    def __init__(
        self,
        vocab_size: int,
        dialogue_classes: int,
        emotion_classes: int,
        embedding_dimension: int = 48,
        hidden_dimension: int = 64,
        dropout: float = 0.2,
    ) -> None:
        super().__init__()
        self.embedding = nn.Embedding(vocab_size, embedding_dimension, padding_idx=0)
        self.lstm = nn.LSTM(
            embedding_dimension,
            hidden_dimension,
            batch_first=True,
            bidirectional=True,
        )
        self.dropout = nn.Dropout(dropout)
        self.dialogue_head = nn.Linear(hidden_dimension * 2, dialogue_classes)
        self.emotion_head = nn.Linear(hidden_dimension * 2, emotion_classes)

    def forward(self, input_ids: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        embedded = self.embedding(input_ids)
        sequence, _ = self.lstm(embedded)
        mask = input_ids.ne(0).unsqueeze(-1).to(sequence.dtype)
        pooled = (sequence * mask).sum(dim=1) / mask.sum(dim=1).clamp_min(1.0)
        features = self.dropout(pooled)
        return self.dialogue_head(features), self.emotion_head(features)


def parameter_count(model: nn.Module) -> int:
    return sum(parameter.numel() for parameter in model.parameters())
