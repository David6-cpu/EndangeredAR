#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RESEARCH_ROOT / "src"))

from r34a1_greeting.locked_review_evaluation import (  # noqa: E402
    reevaluate_locked_candidate,
)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Re-evaluate the Dev-locked TextCNN Pair on Test and human-reviewed Gold v2."
    )
    parser.add_argument("--corpus", type=Path, required=True)
    parser.add_argument("--checkpoint", type=Path, required=True)
    parser.add_argument("--vocab", type=Path, required=True)
    parser.add_argument("--baseline-metrics", type=Path, required=True)
    parser.add_argument(
        "--gold",
        type=Path,
        default=RESEARCH_ROOT / "data" / "endangeredar_gold_v2.jsonl",
    )
    parser.add_argument(
        "--review-manifest",
        type=Path,
        default=RESEARCH_ROOT / "manifests" / "gold-v2-project-review-manifest.json",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=RESEARCH_ROOT / "reports" / "project-reviewed-quality-gate.json",
    )
    args = parser.parse_args(argv)

    report = reevaluate_locked_candidate(
        corpus_path=args.corpus,
        gold_path=args.gold,
        checkpoint_path=args.checkpoint,
        vocab_path=args.vocab,
        review_manifest_path=args.review_manifest,
        baseline_metrics_path=args.baseline_metrics,
    )
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    test = report["test"]["gate"]
    gold = report["humanReviewedGoldV2"]["gate"]
    print(f"Test Greeting P/R: {test['precision']:.6f}/{test['recall']:.6f}")
    print(f"Gold Greeting P/R: {gold['precision']:.6f}/{gold['recall']:.6f}")
    print(f"Test safety-critical FP: {test['safetyCriticalFalsePositive']}")
    print(f"Gold safety-critical FP: {gold['safetyCriticalFalsePositive']}")
    print(f"Quality gate: {'passed' if report['qualityGate']['passed'] else 'failed'}")
    return 0 if report["qualityGate"]["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
