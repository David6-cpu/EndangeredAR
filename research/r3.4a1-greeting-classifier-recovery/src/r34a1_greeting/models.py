from __future__ import annotations

import torch
from torch import nn


class GreetingTextCNN(nn.Module):
    def __init__(
        self,
        vocab_size: int,
        embedding_dimension: int = 32,
        channels_per_kernel: int = 32,
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
        self.head = nn.Linear(hidden, 2)

    def forward(self, input_ids: torch.Tensor) -> torch.Tensor:
        embedded = self.embedding(input_ids).transpose(1, 2)
        pooled = [torch.relu(layer(embedded)).amax(dim=2) for layer in self.convolutions]
        return self.head(self.dropout(torch.cat(pooled, dim=1)))


def parameter_count(model: nn.Module) -> int:
    return sum(parameter.numel() for parameter in model.parameters())
