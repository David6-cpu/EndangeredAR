#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path

from r34a5_greeting_review.project_review import import_project_review


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Import a completed deterministic Greeting Gold v1 project review."
    )
    parser.add_argument("--review-extraction", required=True, type=Path)
    parser.add_argument("--source-review-artifact", required=True, type=Path)
    parser.add_argument("--mapping", required=True, type=Path)
    parser.add_argument("--candidate-manifest", required=True, type=Path)
    parser.add_argument("--reviewed-gold", required=True, type=Path)
    parser.add_argument("--review-manifest", required=True, type=Path)
    parser.add_argument("--reviewer-role", required=True)
    parser.add_argument("--reviewed-at-utc", required=True)
    parser.add_argument(
        "--confirm-numeric-confidence-09-as-high",
        action="store_true",
        help="Require explicit project-owner confirmation before normalizing numeric 0.9 to high.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    manifest = import_project_review(
        review_extraction_path=args.review_extraction,
        source_review_artifact_path=args.source_review_artifact,
        mapping_path=args.mapping,
        candidate_manifest_path=args.candidate_manifest,
        reviewed_gold_path=args.reviewed_gold,
        review_manifest_path=args.review_manifest,
        reviewer_role=args.reviewer_role,
        reviewed_at_utc=args.reviewed_at_utc,
        confirm_numeric_confidence_09_as_high=(
            args.confirm_numeric_confidence_09_as_high
        ),
    )
    print(json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
