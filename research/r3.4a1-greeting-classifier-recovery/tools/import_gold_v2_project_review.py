#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path
import sys


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(RESEARCH_ROOT / "src"))

from r34a1_greeting.project_review import import_project_review  # noqa: E402


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Import a completed Gold v2 project-member blind review."
    )
    parser.add_argument("--review-csv", type=Path, required=True)
    parser.add_argument("--mapping", type=Path, required=True)
    parser.add_argument(
        "--gold",
        type=Path,
        default=RESEARCH_ROOT / "data" / "endangeredar_gold_v2.jsonl",
    )
    parser.add_argument(
        "--history",
        type=Path,
        default=(
            RESEARCH_ROOT
            / "data"
            / "history"
            / "endangeredar_gold_v2_agent_reviewed.jsonl"
        ),
    )
    parser.add_argument(
        "--manifest",
        type=Path,
        default=RESEARCH_ROOT / "manifests" / "gold-v2-project-review-manifest.json",
    )
    parser.add_argument("--reviewer-role", default="project_owner")
    parser.add_argument("--reviewed-at-utc", required=True)
    parser.add_argument("--source-review-artifact-sha256", required=True)
    args = parser.parse_args(argv)

    manifest = import_project_review(
        review_csv_path=args.review_csv,
        mapping_path=args.mapping,
        gold_path=args.gold,
        history_path=args.history,
        manifest_path=args.manifest,
        reviewer_role=args.reviewer_role,
        reviewed_at_utc=args.reviewed_at_utc,
        source_review_artifact_sha256=args.source_review_artifact_sha256,
    )
    print(f"Imported rows: {manifest['completedCount']}/{manifest['rowCount']}")
    print(f"Changed labels: {manifest['changedLabelCount']}")
    print(
        "Final labels: "
        f"Greeting={manifest['finalGreetingCount']}, "
        f"NotGreeting={manifest['finalNotGreetingCount']}"
    )
    print(f"fullyHumanReviewed: {str(manifest['fullyHumanReviewed']).lower()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
