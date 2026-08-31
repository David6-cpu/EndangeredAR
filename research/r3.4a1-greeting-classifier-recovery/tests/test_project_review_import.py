from __future__ import annotations

import csv
import json
from pathlib import Path
import tempfile
import unittest

from r34a1_greeting.blind_review import build_blind_review_package
from r34a1_greeting.data import load_examples, write_examples
from r34a1_greeting.project_review import import_project_review
from r34a1_greeting.schema import GreetingExample


def example(index: int) -> GreetingExample:
    positive = index % 2 == 0
    return GreetingExample(
        source_id=f"review-source-{index}",
        user=f"用户消息 {index}",
        reply=f"助手回复 {index}",
        label="Greeting" if positive else "NotGreeting",
        source_type="project_authored_synthetic",
        generation_method="curated_semantic_composition",
        scenario_family=f"review-family-{index}",
        prompt_template=f"review-template-{index}",
        generation_batch="review-fixture",
        review_status="agent_reviewed_pending_project_review",
        rights_status="project_controlled_no_third_party_text",
        split_group=f"review-group-{index}",
        assigned_split="gold",
        safety_critical=not positive,
    )


class ProjectReviewImportTests(unittest.TestCase):
    def _fixture(self, root: Path):
        repository = root / "repository"
        repository.mkdir()
        gold_path = repository / "gold.jsonl"
        source_rows = [example(index) for index in range(4)]
        write_examples(gold_path, source_rows)
        package = build_blind_review_package(
            gold_path=gold_path,
            output_directory=root / "review-package",
            repository_root=repository,
        )
        with package.csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
            review_rows = list(csv.DictReader(handle))
        labels_by_source_id = {
            "review-source-0": "greeting",
            "review-source-1": "greeting",
            "review-source-2": "not_greeting",
            "review-source-3": "not_greeting",
        }
        mapping = json.loads(package.mapping_path.read_text(encoding="utf-8"))
        source_by_review_id = {
            row["reviewId"]: row["sourceId"] for row in mapping["rows"]
        }
        for row in review_rows:
            row["reviewerLabel"] = labels_by_source_id[source_by_review_id[row["reviewId"]]]
            row["reviewerConfidence"] = "high"
        with package.csv_path.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=review_rows[0])
            writer.writeheader()
            writer.writerows(review_rows)
        return repository, gold_path, source_rows, package, review_rows

    def test_import_preserves_history_and_records_review_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository, gold_path, source_rows, package, _review_rows = self._fixture(root)
            history_path = repository / "history" / "gold-agent-reviewed.jsonl"
            manifest_path = repository / "gold-review-manifest.json"
            original_bytes = gold_path.read_bytes()

            manifest = import_project_review(
                review_csv_path=package.csv_path,
                mapping_path=package.mapping_path,
                gold_path=gold_path,
                history_path=history_path,
                manifest_path=manifest_path,
                reviewer_role="project_owner",
                reviewed_at_utc="2026-08-31T04:00:00Z",
                source_review_artifact_sha256="a" * 64,
            )

            self.assertEqual(original_bytes, history_path.read_bytes())
            reviewed = {row.source_id: row for row in load_examples(gold_path)}
            self.assertEqual("Greeting", reviewed["review-source-1"].label)
            self.assertEqual("NotGreeting", reviewed["review-source-2"].label)
            self.assertTrue(all(row.review_status == "project_member_reviewed" for row in reviewed.values()))
            self.assertEqual(2, manifest["disagreementCount"])
            self.assertEqual(2, manifest["changedLabelCount"])
            self.assertEqual(1, manifest["greetingToNotGreetingCount"])
            self.assertEqual(1, manifest["notGreetingToGreetingCount"])
            self.assertEqual(2, manifest["finalGreetingCount"])
            self.assertEqual(2, manifest["finalNotGreetingCount"])
            self.assertTrue(manifest["fullyHumanReviewed"])
            self.assertEqual(manifest, json.loads(manifest_path.read_text(encoding="utf-8")))

    def test_import_rejects_modified_text(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository, gold_path, _source_rows, package, review_rows = self._fixture(root)
            review_rows[0]["assistantReply"] = "被修改"
            self._rewrite(package.csv_path, review_rows)

            with self.assertRaisesRegex(ValueError, "assistantReply"):
                self._import(repository, gold_path, package)

    def test_import_rejects_missing_duplicate_or_unknown_review_ids(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repository, gold_path, _source_rows, package, review_rows = self._fixture(root)
            review_rows[-1]["reviewId"] = review_rows[0]["reviewId"]
            self._rewrite(package.csv_path, review_rows)

            with self.assertRaisesRegex(ValueError, "duplicate reviewId"):
                self._import(repository, gold_path, package)

    def test_import_rejects_unresolved_or_blank_labels(self) -> None:
        for invalid_label in ("", "ambiguous", "invalid"):
            with self.subTest(label=invalid_label), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                repository, gold_path, _source_rows, package, review_rows = self._fixture(root)
                review_rows[0]["reviewerLabel"] = invalid_label
                self._rewrite(package.csv_path, review_rows)

                with self.assertRaisesRegex(ValueError, "reviewerLabel|unresolved"):
                    self._import(repository, gold_path, package)

    def test_import_rejects_invalid_confidence_and_private_reviewer_note(self) -> None:
        cases = (("certain", "", "reviewerConfidence"), ("high", "owner@example.com", "private"))
        for confidence, note, message in cases:
            with self.subTest(confidence=confidence, note=note), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                repository, gold_path, _source_rows, package, review_rows = self._fixture(root)
                review_rows[0]["reviewerConfidence"] = confidence
                review_rows[0]["reviewerNote"] = note
                self._rewrite(package.csv_path, review_rows)

                with self.assertRaisesRegex(ValueError, message):
                    self._import(repository, gold_path, package)

    @staticmethod
    def _rewrite(path: Path, rows: list[dict[str, str]]) -> None:
        with path.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=rows[0])
            writer.writeheader()
            writer.writerows(rows)

    @staticmethod
    def _import(repository: Path, gold_path: Path, package):
        return import_project_review(
            review_csv_path=package.csv_path,
            mapping_path=package.mapping_path,
            gold_path=gold_path,
            history_path=repository / "history.jsonl",
            manifest_path=repository / "manifest.json",
            reviewer_role="project_owner",
            reviewed_at_utc="2026-08-31T04:00:00Z",
            source_review_artifact_sha256="a" * 64,
        )


if __name__ == "__main__":
    unittest.main()
