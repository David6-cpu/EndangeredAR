#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
REPOSITORY_ROOT = RESEARCH_ROOT.parents[1]
sys.path.insert(0, str(RESEARCH_ROOT / "src"))

from r34a1_greeting.blind_review import build_blind_review_package  # noqa: E402


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Build the local, label-blind Gold v2 project review package."
    )
    parser.add_argument(
        "--gold",
        type=Path,
        default=RESEARCH_ROOT / "data" / "endangeredar_gold_v2.jsonl",
        help="Gold v2 JSONL input (defaults to the tracked recovery Gold v2).",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        required=True,
        help="New local directory outside the repository; it must not already exist.",
    )
    args = parser.parse_args(argv)
    package = build_blind_review_package(
        gold_path=args.gold,
        output_directory=args.output_dir,
        repository_root=REPOSITORY_ROOT,
        expected_row_count=320,
    )
    print(f"Blind review rows: {package.row_count}")
    print(f"Randomization seed: {package.randomization_seed}")
    print(f"Review CSV: {package.csv_path}")
    print(f"Review instructions: {package.instructions_path}")
    print(f"Internal mapping: {package.mapping_path}")
    print(f"Mapping validation: passed ({package.row_count}/{package.row_count})")
    print("Reviewer fields: blank")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
