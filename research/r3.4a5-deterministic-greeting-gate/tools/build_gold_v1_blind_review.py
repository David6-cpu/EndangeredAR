#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path

from r34a5_greeting_review.review_package import build_review_package


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build the local-only deterministic Greeting Gold v1 blind-review package."
    )
    parser.add_argument("--project-root", required=True, type=Path)
    parser.add_argument("--output-directory", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.project_root.resolve()
    package = build_review_package(
        root
        / "research/r3.4a5-deterministic-greeting-gate/data/deterministic-greeting-gold-v1-candidates.json",
        args.output_directory,
        root,
        root
        / "research/r3.4a4-real-qwen-domain-recovery/manifests/pilot-prompts.json",
        root
        / "research/r3.4a1-greeting-classifier-recovery/data/endangeredar_gold_v2.jsonl",
    )
    printable = {key: str(value) for key, value in package.items()}
    print(json.dumps(printable, ensure_ascii=False, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
