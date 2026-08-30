from __future__ import annotations

from collections import Counter
from dataclasses import dataclass
import json
from pathlib import Path
from typing import Iterable

import numpy as np

from .schema import AffectExample


SPECIAL_TOKENS = ("<PAD>", "<UNK>", "<USER>", "<ASSISTANT>", "<SEP>", "<MODE>", "<AUTHORITY>")


@dataclass
class CharacterTokenizer:
    token_to_id: dict[str, int]
    max_length: int = 96

    @classmethod
    def build(
        cls,
        examples: Iterable[AffectExample],
        vocab_size: int,
        max_length: int,
        input_form: str,
    ) -> "CharacterTokenizer":
        counts: Counter[str] = Counter()
        for item in examples:
            if input_form == "user_reply_pair":
                counts.update(item.user)
            elif input_form != "reply_only":
                raise ValueError(f"unsupported input form: {input_form}")
            counts.update(item.reply)
        ordered = sorted(counts.items(), key=lambda pair: (-pair[1], ord(pair[0])))
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

    def _character_ids(self, text: str) -> list[int]:
        return [self.token_to_id.get(character, self.unk_id) for character in text]

    def encode(self, user: str, reply: str, input_form: str) -> np.ndarray:
        assistant_id = self.token_to_id["<ASSISTANT>"]
        if input_form == "reply_only":
            ids = [assistant_id] + self._character_ids(reply)[: self.max_length - 1]
        elif input_form == "user_reply_pair":
            user_id = self.token_to_id["<USER>"]
            sep_id = self.token_to_id["<SEP>"]
            reply_ids = self._character_ids(reply)
            if len(reply_ids) > self.max_length - 1:
                ids = [assistant_id] + reply_ids[: self.max_length - 1]
            else:
                user_budget = max(0, self.max_length - len(reply_ids) - 3)
                user_ids = self._character_ids(user)[-user_budget:] if user_budget else []
                ids = [user_id] + user_ids + [sep_id, assistant_id] + reply_ids
        else:
            raise ValueError(f"unsupported input form: {input_form}")
        ids.extend([self.pad_id] * (self.max_length - len(ids)))
        return np.asarray(ids, dtype=np.int64)

    def encode_many(self, examples: Iterable[AffectExample], input_form: str) -> np.ndarray:
        return np.stack([self.encode(item.user, item.reply, input_form) for item in examples])

    def save(self, path: Path) -> None:
        payload = {
            "version": "r3.4a-char-v1",
            "maxLength": self.max_length,
            "paddingSide": "right",
            "truncation": "preserve_assistant_reply_then_user_tail",
            "tokenToId": self.token_to_id,
        }
        path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    @classmethod
    def load(cls, path: Path) -> "CharacterTokenizer":
        payload = json.loads(path.read_text(encoding="utf-8"))
        return cls(dict(payload["tokenToId"]), int(payload["maxLength"]))
