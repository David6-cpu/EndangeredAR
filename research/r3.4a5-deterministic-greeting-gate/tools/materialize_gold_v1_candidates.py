#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path

from r34a5_greeting_review.candidates import (
    GREETING_CANDIDATES,
    NOT_GREETING_CANDIDATES,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Materialize the label-free deterministic Greeting Gold v1 candidates."
    )
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if len(GREETING_CANDIDATES) != 50 or len(NOT_GREETING_CANDIDATES) != 100:
        raise ValueError("candidate design must remain exactly 50 + 100 rows")
    specs = (*GREETING_CANDIDATES, *NOT_GREETING_CANDIDATES)
    items = []
    for index, spec in enumerate(specs, start=1):
        items.append(
            {
                "itemId": f"dg-gold-v1-{index:04d}",
                "userMessage": spec.user_message,
                "scenarioFamily": spec.scenario_family,
                "safetyCritical": spec.safety_critical,
                "sourceType": "project_authored_non_private",
                "reviewStatus": "pending_project_member_blind_review",
                "rightsStatus": "project_controlled_no_third_party_text",
                "splitGroup": f"gold-v1-{index:04d}",
            }
        )
    manifest = {
        "schemaVersion": "r3.4a5-deterministic-greeting-gold-v1-review-v1",
        "datasetVersion": "r3.4a5-deterministic-greeting-gold-v1",
        "purpose": "independent_user_only_project_member_blind_review",
        "fullyHumanReviewed": False,
        "sourcePolicy": "project_authored_non_private_inputs_only",
        "rightsStatus": "project_controlled_no_third_party_text",
        "designTarget": {
            "greetingCandidateCount": 50,
            "notGreetingCandidateCount": 100,
            "minimumSafetyCriticalNegativeCount": 60,
            "totalCount": 150,
        },
        "items": items,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
