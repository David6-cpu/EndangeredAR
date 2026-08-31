from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
import json
from pathlib import Path
from typing import Iterable

import numpy as np

from .schema import GreetingExample


SPECIAL_TOKENS = ("<PAD>", "<UNK>", "<USER>", "<ASSISTANT>", "<SEP>")
INPUT_FORMS = ("user_only", "reply_only", "user_reply_pair")


@dataclass
class CharacterTokenizer:
    token_to_id: dict[str, int]
    max_length: int

    @classmethod
    def build(
        cls,
        examples: Iterable[GreetingExample],
        vocab_size: int,
        max_length: int,
        input_form: str,
    ) -> "CharacterTokenizer":
        if input_form not in INPUT_FORMS:
            raise ValueError(f"unsupported input form: {input_form}")
        counts: Counter[str] = Counter()
        for row in examples:
            if input_form in ("user_only", "user_reply_pair"):
                counts.update(row.user)
            if input_form in ("reply_only", "user_reply_pair"):
                counts.update(row.reply)
        ordered = sorted(counts.items(), key=lambda item: (-item[1], ord(item[0])))
        tokens = list(SPECIAL_TOKENS) + [
            token for token, _ in ordered[: max(0, vocab_size - len(SPECIAL_TOKENS))]
        ]
        return cls({token: index for index, token in enumerate(tokens)}, max_length)

    @property
    def pad_id(self) -> int:
        return self.token_to_id["<PAD>"]

    @property
    def unk_id(self) -> int:
        return self.token_to_id["<UNK>"]

    def _ids(self, text: str) -> list[int]:
        return [self.token_to_id.get(character, self.unk_id) for character in text]

    def encode(self, user: str, reply: str, input_form: str) -> np.ndarray:
        if input_form == "user_only":
            ids = [self.token_to_id["<USER>"]] + self._ids(user)[: self.max_length - 1]
        elif input_form == "reply_only":
            ids = [self.token_to_id["<ASSISTANT>"]] + self._ids(reply)[: self.max_length - 1]
        elif input_form == "user_reply_pair":
            reply_ids = self._ids(reply)
            assistant_id = self.token_to_id["<ASSISTANT>"]
            if len(reply_ids) >= self.max_length - 1:
                ids = [assistant_id] + reply_ids[: self.max_length - 1]
            else:
                user_budget = max(0, self.max_length - len(reply_ids) - 3)
                user_ids = self._ids(user)[-user_budget:] if user_budget else []
                ids = [self.token_to_id["<USER>"]] + user_ids + [
                    self.token_to_id["<SEP>"],
                    assistant_id,
                ] + reply_ids
        else:
            raise ValueError(f"unsupported input form: {input_form}")
        ids.extend([self.pad_id] * (self.max_length - len(ids)))
        return np.asarray(ids, dtype=np.int64)

    def encode_many(
        self, examples: Iterable[GreetingExample], input_form: str
    ) -> np.ndarray:
        return np.stack([self.encode(row.user, row.reply, input_form) for row in examples])

    def save(self, path: Path) -> None:
        payload = {
            "version": "r3.4a1-char-v1",
            "maxLength": self.max_length,
            "paddingSide": "right",
            "truncation": "preserve_reply_then_user_tail",
            "tokenToId": self.token_to_id,
        }
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
