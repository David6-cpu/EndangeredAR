from __future__ import annotations

from collections import defaultdict
import json
from pathlib import Path
import re
import unicodedata

from .schema import GreetingExample


SPLITS = ("train", "dev", "test")
GROUP_FIELDS = (
    "scenario_family",
    "prompt_template",
    "generation_batch",
    "split_group",
)


def normalized_pair(row: GreetingExample) -> str:
    def normalize(value: str) -> str:
        value = unicodedata.normalize("NFKC", value).lower()
        return re.sub(r"\W+", "", value, flags=re.UNICODE)

    return f"{normalize(row.user)}|{normalize(row.reply)}"


def load_examples(path: Path) -> list[GreetingExample]:
    examples: list[GreetingExample] = []
    for line_number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip():
            continue
        try:
            examples.append(GreetingExample.from_json(json.loads(line)))
        except (KeyError, TypeError, ValueError) as error:
            raise ValueError(f"invalid row at line {line_number}: {error}") from error
    return examples


def write_examples(path: Path, examples: list[GreetingExample]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [json.dumps(row.to_json(), ensure_ascii=False, sort_keys=True) for row in examples]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def split_examples(examples: list[GreetingExample]) -> dict[str, list[GreetingExample]]:
    output = {name: [] for name in SPLITS}
    for row in examples:
        row.validate()
        if row.assigned_split not in output:
            raise ValueError(f"training corpus cannot contain split {row.assigned_split}")
        output[row.assigned_split].append(row)
    for rows in output.values():
        rows.sort(key=lambda row: row.source_id)
    assert_group_isolation(output)
    assert_no_duplicate_pairs(output)
    return output


def assert_group_isolation(splits: dict[str, list[GreetingExample]]) -> None:
    for field in GROUP_FIELDS:
        owners: dict[str, set[str]] = defaultdict(set)
        for split, rows in splits.items():
            for row in rows:
                owners[str(getattr(row, field))].add(split)
        leaked = sorted(value for value, names in owners.items() if len(names) > 1)
        if leaked:
            raise ValueError(f"{field} leaks across splits: {leaked[:3]}")


def assert_no_duplicate_pairs(splits: dict[str, list[GreetingExample]]) -> None:
    owners: dict[str, list[str]] = defaultdict(list)
    for split, rows in splits.items():
        for row in rows:
            owners[normalized_pair(row)].append(f"{split}:{row.source_id}")
    duplicates = [locations for locations in owners.values() if len(locations) > 1]
    if duplicates:
        raise ValueError(f"duplicate normalized pairs found: {duplicates[:3]}")


def assert_gold_isolation(
    splits: dict[str, list[GreetingExample]], gold: list[GreetingExample]
) -> None:
    training_pairs = {normalized_pair(row) for rows in splits.values() for row in rows}
    gold_pairs = {normalized_pair(row) for row in gold}
    overlap = sorted(training_pairs & gold_pairs)
    if overlap:
        raise ValueError(f"Gold v2 overlaps training corpus: {overlap[:3]}")
