from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from r34a5_greeting_review.review_package import (
    ALLOWED_CONFIDENCE,
    ALLOWED_LABELS,
    build_review_package,
    load_candidate_manifest,
    normalize_for_deduplication,
    validate_candidate_manifest,
)


PROJECT_ROOT = Path(__file__).resolve().parents[3]
RESEARCH_ROOT = PROJECT_ROOT / "research/r3.4a5-deterministic-greeting-gate"
CANDIDATES_PATH = RESEARCH_ROOT / "data/deterministic-greeting-gold-v1-candidates.json"
PILOT_PATH = (
    PROJECT_ROOT
    / "research/r3.4a4-real-qwen-domain-recovery/manifests/pilot-prompts.json"
)
GOLD_V2_PATH = (
    PROJECT_ROOT
    / "research/r3.4a1-greeting-classifier-recovery/data/endangeredar_gold_v2.jsonl"
)
POLICY_HASH_PATH = RESEARCH_ROOT / "policy/policy-sha256.json"
POLICY_SOURCE = (
    PROJECT_ROOT
    / "EndangeredAR/Assets/Scripts/AI/Greeting/DeterministicGreetingPolicy.cs"
)
SCOPE_SOURCE = (
    PROJECT_ROOT
    / "EndangeredAR/Assets/Scripts/AI/Greeting/GreetingProductScopeGate.cs"
)


class CandidateManifestTests(unittest.TestCase):
    def test_manifest_has_independent_user_only_review_boundary(self) -> None:
        manifest = load_candidate_manifest(CANDIDATES_PATH)
        validate_candidate_manifest(manifest, PILOT_PATH, GOLD_V2_PATH)
        rows = manifest["items"]

        self.assertEqual(150, len(rows))
        self.assertEqual(50, manifest["designTarget"]["greetingCandidateCount"])
        self.assertEqual(100, manifest["designTarget"]["notGreetingCandidateCount"])
        self.assertGreaterEqual(sum(row["safetyCritical"] for row in rows), 60)
        self.assertEqual(150, len({row["itemId"] for row in rows}))
        self.assertEqual(
            150,
            len({normalize_for_deduplication(row["userMessage"]) for row in rows}),
        )
        forbidden = {
            "assistantReply",
            "reply",
            "expectedLabel",
            "label",
            "ruleResult",
            "reasonCode",
            "prediction",
            "confidence",
            "margin",
        }
        self.assertTrue(all(forbidden.isdisjoint(row) for row in rows))
        self.assertTrue(
            all(row["reviewStatus"] == "pending_project_member_blind_review" for row in rows)
        )

    def test_manifest_is_normalized_distinct_from_pilot_and_gold_v2(self) -> None:
        manifest = load_candidate_manifest(CANDIDATES_PATH)
        candidate_values = {
            normalize_for_deduplication(row["userMessage"])
            for row in manifest["items"]
        }
        pilot = json.loads(PILOT_PATH.read_text(encoding="utf-8"))
        pilot_values = {
            normalize_for_deduplication(row["userMessage"])
            for row in pilot["prompts"]
        }
        gold_v2_values = {
            normalize_for_deduplication(json.loads(line)["user"])
            for line in GOLD_V2_PATH.read_text(encoding="utf-8").splitlines()
            if line.strip()
        }

        self.assertFalse(candidate_values & pilot_values)
        self.assertFalse(candidate_values & gold_v2_values)

    def test_policy_hash_manifest_matches_frozen_sources(self) -> None:
        manifest = json.loads(POLICY_HASH_PATH.read_text(encoding="utf-8"))
        self.assertEqual(
            hashlib.sha256(POLICY_SOURCE.read_bytes()).hexdigest(),
            manifest["greetingPolicySha256"],
        )
        self.assertEqual(
            hashlib.sha256(SCOPE_SOURCE.read_bytes()).hexdigest(),
            manifest["productScopeSha256"],
        )


class BlindReviewPackageTests(unittest.TestCase):
    def test_package_is_stable_blind_complete_and_unlabelled(self) -> None:
        with tempfile.TemporaryDirectory() as first_directory, tempfile.TemporaryDirectory() as second_directory:
            first = build_review_package(
                CANDIDATES_PATH,
                Path(first_directory),
                PROJECT_ROOT,
                PILOT_PATH,
                GOLD_V2_PATH,
            )
            second = build_review_package(
                CANDIDATES_PATH,
                Path(second_directory),
                PROJECT_ROOT,
                PILOT_PATH,
                GOLD_V2_PATH,
            )

            first_rows = read_csv(first["reviewCsv"])
            second_rows = read_csv(second["reviewCsv"])
            self.assertEqual(first_rows, second_rows)
            self.assertEqual(150, len(first_rows))
            self.assertEqual(
                [
                    "reviewId",
                    "userMessage",
                    "reviewerLabel",
                    "reviewerConfidence",
                    "reviewerNote",
                ],
                list(first_rows[0]),
            )
            self.assertEqual(150, len({row["reviewId"] for row in first_rows}))
            self.assertTrue(all(not row["reviewerLabel"] for row in first_rows))
            self.assertTrue(all(not row["reviewerConfidence"] for row in first_rows))
            self.assertTrue(all(not row["reviewerNote"] for row in first_rows))

            source_messages = [
                row["userMessage"] for row in load_candidate_manifest(CANDIDATES_PATH)["items"]
            ]
            self.assertNotEqual(source_messages, [row["userMessage"] for row in first_rows])

            mapping = json.loads(Path(first["mappingJson"]).read_text(encoding="utf-8"))
            self.assertEqual(150, mapping["rowCount"])
            self.assertEqual(150, len(mapping["items"]))
            self.assertEqual(
                {row["reviewId"] for row in first_rows},
                {row["reviewId"] for row in mapping["items"]},
            )
            self.assertTrue(
                all("expectedLabel" not in row and "ruleResult" not in row for row in mapping["items"])
            )

    def test_instructions_define_human_only_user_intent_review(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package = build_review_package(
                CANDIDATES_PATH,
                Path(directory),
                PROJECT_ROOT,
                PILOT_PATH,
                GOLD_V2_PATH,
            )
            instructions = Path(package["instructionsMarkdown"]).read_text(encoding="utf-8")

        for required in (
            "用户消息本身",
            "直接、自然的问候意图",
            "greeting",
            "not_greeting",
            "ambiguous",
            "invalid",
            "全部 150 条",
            "不要猜规则",
            "不要修改 userMessage",
            "真人",
        ):
            self.assertIn(required, instructions)
        self.assertEqual(
            ("greeting", "not_greeting", "ambiguous", "invalid"),
            ALLOWED_LABELS,
        )
        self.assertEqual(("high", "medium", "low"), ALLOWED_CONFIDENCE)

    def test_output_inside_repository_is_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "outside the repository"):
            build_review_package(
                CANDIDATES_PATH,
                RESEARCH_ROOT / "local-review-output",
                PROJECT_ROOT,
                PILOT_PATH,
                GOLD_V2_PATH,
            )


def read_csv(path: str) -> list[dict[str, str]]:
    with Path(path).open("r", encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


if __name__ == "__main__":
    unittest.main()
