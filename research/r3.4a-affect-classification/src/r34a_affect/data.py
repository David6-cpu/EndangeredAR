from __future__ import annotations

from collections import Counter
import csv
import json
from pathlib import Path
import re
import unicodedata

from .schema import AffectExample, LabelSchema


CPED_SPLITS = {
    "train": "train_split.csv",
    "dev": "valid_split.csv",
    "test": "test_split.csv",
}


def normalize_text(value: str) -> str:
    value = unicodedata.normalize("NFKC", value).lower()
    value = re.sub(r"[a-z]+", "a", value)
    value = re.sub(r"\d+", "0", value)
    return "".join(character for character in value if character.isalnum())


def reply_fingerprint(example: AffectExample) -> str:
    return normalize_text(example.reply)


def pair_fingerprint(example: AffectExample) -> str:
    return f"{normalize_text(example.user)}|{normalize_text(example.reply)}"


def load_gold(path: Path) -> list[AffectExample]:
    examples: list[AffectExample] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        row = json.loads(line)
        if row.get("source") != "project_authored":
            raise ValueError(f"gold line {line_number} has an unapproved source")
        examples.append(
            AffectExample(
                source_id=row["id"],
                group_id=row["id"],
                user=row["user"].strip(),
                reply=row["reply"].strip(),
                dialogue_act=row["dialogue_act"],
                emotion_tone=row["emotion_tone"],
            )
        )
    return examples


def load_cped_split(path: Path, schema: LabelSchema) -> list[AffectExample]:
    examples: list[AffectExample] = []
    previous_by_dialogue: dict[str, str] = {}
    with path.open(encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            group_id = f"{row['TV_ID']}:{row['Dialogue_ID']}"
            reply = row["Utterance"].strip()
            if not reply:
                continue
            examples.append(
                AffectExample(
                    source_id=f"{group_id}:{row['Utterance_ID']}",
                    group_id=group_id,
                    user=previous_by_dialogue.get(group_id, ""),
                    reply=reply,
                    dialogue_act=schema.dialogue_mapping[row["DA"]],
                    emotion_tone=schema.emotion_mapping[row["Emotion"]],
                )
            )
            previous_by_dialogue[group_id] = reply
    return examples


def _overlap_keys(examples: list[AffectExample]) -> set[str]:
    return {reply_fingerprint(item) for item in examples} | {
        pair_fingerprint(item) for item in examples
    }


def remove_contamination(
    splits: dict[str, list[AffectExample]], gold: list[AffectExample]
) -> tuple[dict[str, list[AffectExample]], dict[str, int]]:
    """Remove cross-split and gold overlap without exposing held-out labels."""

    output: dict[str, list[AffectExample]] = {}
    removed: dict[str, int] = {}
    blocked = _overlap_keys(gold)

    # Test has priority over dev and train; dev has priority over train. This is
    # a contamination screen only. Held-out text is not used to build features.
    for split in ("test", "dev", "train"):
        accepted: list[AffectExample] = []
        for item in splits[split]:
            keys = {reply_fingerprint(item), pair_fingerprint(item)}
            if keys & blocked:
                continue
            accepted.append(item)
        removed[split] = len(splits[split]) - len(accepted)
        output[split] = accepted
        blocked.update(_overlap_keys(accepted))
    return output, removed


def load_cped(
    root: Path, schema: LabelSchema, gold: list[AffectExample]
) -> tuple[dict[str, list[AffectExample]], dict[str, object]]:
    raw = {
        split: load_cped_split(root / filename, schema)
        for split, filename in CPED_SPLITS.items()
    }
    groups = {split: {item.group_id for item in rows} for split, rows in raw.items()}
    if groups["train"] & groups["dev"] or groups["train"] & groups["test"] or groups["dev"] & groups["test"]:
        raise ValueError("CPED dialogue groups overlap across splits")

    filtered, removed = remove_contamination(raw, gold)
    manifest: dict[str, object] = {
        "rawCounts": {split: len(rows) for split, rows in raw.items()},
        "filteredCounts": {split: len(rows) for split, rows in filtered.items()},
        "removedForCrossSplitOrGoldContamination": removed,
        "groupCounts": {split: len(groups[split]) for split in groups},
        "dialogueDistribution": {
            split: dict(sorted(Counter(item.dialogue_act for item in rows).items()))
            for split, rows in filtered.items()
        },
        "emotionDistribution": {
            split: dict(sorted(Counter(item.emotion_tone for item in rows).items()))
            for split, rows in filtered.items()
        },
    }
    return filtered, manifest
