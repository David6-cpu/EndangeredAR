from __future__ import annotations

import csv
import json
from pathlib import Path
import tempfile
import unittest

from r34a5_greeting_review.project_review import import_project_review
from r34a5_greeting_review.review_package import build_review_package


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


class GoldV1ProjectReviewImportTests(unittest.TestCase):
    def _fixture(self, root: Path) -> dict[str, Path]:
        repository = root / "repository"
        repository.mkdir()
        package = build_review_package(
            CANDIDATES_PATH,
            root / "review-package",
            repository,
            PILOT_PATH,
            GOLD_V2_PATH,
        )
        with Path(package["reviewCsv"]).open(
            "r", encoding="utf-8-sig", newline=""
        ) as stream:
            rows = list(csv.DictReader(stream))
        mapping = json.loads(Path(package["mappingJson"]).read_text(encoding="utf-8"))
        mapping_by_id = {row["reviewId"]: row for row in mapping["items"]}
        for row in rows:
            item_index = int(mapping_by_id[row["reviewId"]]["itemId"].rsplit("-", 1)[1])
            row["reviewerLabel"] = "greeting" if item_index <= 50 else "not_greeting"
            row["reviewerConfidence"] = 0.9
        extraction = {
            "sheetName": "gold-v1-blind-review",
            "rowCount": 151,
            "columnCount": 5,
            "values": [list(rows[0])] + [[row[key] for key in rows[0]] for row in rows],
            "formulas": [],
        }
        extraction_path = root / "review-extraction.json"
        extraction_path.write_text(
            json.dumps(extraction, ensure_ascii=False), encoding="utf-8"
        )
        source_artifact = root / "completed-review.xlsx"
        source_artifact.write_bytes(b"synthetic completed workbook")
        return {
            "repository": repository,
            "extraction": extraction_path,
            "sourceArtifact": source_artifact,
            "mapping": Path(package["mappingJson"]),
            "reviewedGold": repository / "gold-reviewed.json",
            "manifest": repository / "review-manifest.json",
        }

    def _import(self, fixture: dict[str, Path], confirmed: bool = True):
        return import_project_review(
            review_extraction_path=fixture["extraction"],
            source_review_artifact_path=fixture["sourceArtifact"],
            mapping_path=fixture["mapping"],
            candidate_manifest_path=CANDIDATES_PATH,
            reviewed_gold_path=fixture["reviewedGold"],
            review_manifest_path=fixture["manifest"],
            reviewer_role="project_owner",
            reviewed_at_utc="2026-09-01T14:15:31Z",
            confirm_numeric_confidence_09_as_high=confirmed,
        )

    def test_import_normalizes_confirmed_numeric_confidence_and_preserves_source(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self._fixture(Path(directory))
            original_candidates = CANDIDATES_PATH.read_bytes()

            manifest = self._import(fixture)

            self.assertEqual(original_candidates, CANDIDATES_PATH.read_bytes())
            reviewed = json.loads(fixture["reviewedGold"].read_text(encoding="utf-8"))
            self.assertEqual(150, len(reviewed["items"]))
            self.assertTrue(reviewed["fullyHumanReviewed"])
            self.assertTrue(
                all(row["reviewerConfidence"] == "high" for row in reviewed["items"])
            )
            self.assertEqual(150, manifest["completedCount"])
            self.assertEqual({"high": 150}, manifest["reviewerConfidenceCounts"])
            self.assertEqual(
                "numeric_0.9_confirmed_as_high",
                manifest["confidenceNormalization"]["method"],
            )
            self.assertTrue(manifest["fullyHumanReviewed"])
            self.assertEqual(
                manifest,
                json.loads(fixture["manifest"].read_text(encoding="utf-8")),
            )

    def test_import_rejects_numeric_confidence_without_explicit_confirmation(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self._fixture(Path(directory))

            with self.assertRaisesRegex(ValueError, "numeric reviewerConfidence"):
                self._import(fixture, confirmed=False)

    def test_import_accepts_reordered_rows_but_rejects_modified_text_or_duplicate_id(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            fixture = self._fixture(Path(directory))
            extraction = json.loads(fixture["extraction"].read_text(encoding="utf-8"))
            extraction["values"] = [extraction["values"][0], *reversed(extraction["values"][1:])]
            fixture["extraction"].write_text(
                json.dumps(extraction, ensure_ascii=False), encoding="utf-8"
            )
            self._import(fixture)

        for mutation, expected in (
            (lambda rows: rows[1].__setitem__(1, "被修改的用户消息"), "userMessage"),
            (lambda rows: rows[2].__setitem__(0, rows[1][0]), "duplicate reviewId"),
        ):
            with self.subTest(expected=expected), tempfile.TemporaryDirectory() as directory:
                fixture = self._fixture(Path(directory))
                extraction = json.loads(fixture["extraction"].read_text(encoding="utf-8"))
                mutation(extraction["values"])
                fixture["extraction"].write_text(
                    json.dumps(extraction, ensure_ascii=False), encoding="utf-8"
                )
                with self.assertRaisesRegex(ValueError, expected):
                    self._import(fixture)

    def test_import_rejects_unresolved_label_or_private_reviewer_note(self) -> None:
        cases = (
            ("ambiguous", "", "unresolved reviewerLabel"),
            ("greeting", "owner@example.com", "private information"),
        )
        for label, note, expected in cases:
            with self.subTest(label=label), tempfile.TemporaryDirectory() as directory:
                fixture = self._fixture(Path(directory))
                extraction = json.loads(fixture["extraction"].read_text(encoding="utf-8"))
                extraction["values"][1][2] = label
                extraction["values"][1][4] = note
                fixture["extraction"].write_text(
                    json.dumps(extraction, ensure_ascii=False), encoding="utf-8"
                )
                with self.assertRaisesRegex(ValueError, expected):
                    self._import(fixture)


if __name__ == "__main__":
    unittest.main()
