from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path

from .data import assert_gold_isolation, split_examples, write_examples
from .generation import build_gold_v2, build_project_corpus


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--corpus-output", type=Path, required=True)
    parser.add_argument("--gold-output", type=Path, required=True)
    parser.add_argument("--manifest-output", type=Path, required=True)
    args = parser.parse_args()

    corpus = build_project_corpus()
    gold = build_gold_v2()
    splits = split_examples(corpus)
    assert_gold_isolation(splits, gold)
    write_examples(args.corpus_output, corpus)
    write_examples(args.gold_output, gold)
    manifest = {
        "version": "r3.4a1-greeting-data-v1",
        "sourceType": "project_authored_synthetic",
        "generationMethod": "curated_semantic_composition",
        "rightsStatus": "project_controlled_no_third_party_text",
        "reviewStatus": "agent_reviewed_pending_project_review",
        "containsThirdPartyText": False,
        "containsPrivateUserChat": False,
        "corpus": {
            "count": len(corpus),
            "labels": dict(sorted(Counter(row.label for row in corpus).items())),
            "splits": {name: len(rows) for name, rows in splits.items()},
            "splitLabels": {
                name: dict(sorted(Counter(row.label for row in rows).items()))
                for name, rows in splits.items()
            },
            "scenarioFamilies": {
                name: sorted({row.scenario_family for row in rows})
                for name, rows in splits.items()
            },
            "ordinaryNegative": sum(
                row.scenario_family.startswith("ordinary_") for row in corpus
            ),
            "stateNegative": sum(
                row.scenario_family.startswith("state_") for row in corpus
            ),
            "safetyCriticalNegative": sum(row.safety_critical for row in corpus),
            "sha256": sha256(args.corpus_output),
        },
        "goldV2": {
            "count": len(gold),
            "labels": dict(sorted(Counter(row.label for row in gold).items())),
            "safetyCriticalNegative": sum(row.safety_critical for row in gold),
            "fullyHumanReviewed": False,
            "sha256": sha256(args.gold_output),
        },
        "groupSplitFields": [
            "scenarioFamily",
            "promptTemplate",
            "generationBatch",
            "sourceType",
            "splitGroup"
        ],
        "goldUsedFor": "final acceptance evaluation only",
        "goldExcludedFrom": [
            "vocabulary",
            "early stopping",
            "threshold selection",
            "temperature calibration",
            "model selection"
        ]
    }
    args.manifest_output.parent.mkdir(parents=True, exist_ok=True)
    args.manifest_output.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
