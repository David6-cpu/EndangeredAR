from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import json


@dataclass(frozen=True)
class AffectExample:
    source_id: str
    group_id: str
    user: str
    reply: str
    dialogue_act: str
    emotion_tone: str


@dataclass(frozen=True)
class LabelSchema:
    dialogue_acts: tuple[str, ...]
    emotion_tones: tuple[str, ...]
    dialogue_mapping: dict[str, str]
    emotion_mapping: dict[str, str]

    @classmethod
    def load(cls, path: Path) -> "LabelSchema":
        payload = json.loads(path.read_text(encoding="utf-8"))
        return cls(
            dialogue_acts=tuple(payload["dialogueActs"]),
            emotion_tones=tuple(payload["emotionTones"]),
            dialogue_mapping=dict(payload["cpedDialogueActMapping"]),
            emotion_mapping=dict(payload["cpedEmotionMapping"]),
        )

    def dialogue_id(self, label: str) -> int:
        return self.dialogue_acts.index(label)

    def emotion_id(self, label: str) -> int:
        return self.emotion_tones.index(label)
