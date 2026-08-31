from __future__ import annotations

import csv
import importlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from r34a1_greeting.data import write_examples
from r34a1_greeting.schema import GreetingExample


EXPECTED_COLUMNS = (
    "reviewId",
    "userMessage",
    "assistantReply",
    "reviewerLabel",
    "reviewerConfidence",
    "reviewerNote",
)


def example(index: int) -> GreetingExample:
    positive = index % 2 == 0
    return GreetingExample(
        source_id=f"source-{'positive' if positive else 'negative'}-{index:02d}",
        user=f"测试用户消息 {index}",
        reply=f"测试回复 {index}",
        label="Greeting" if positive else "NotGreeting",
        source_type="project_authored_synthetic",
        generation_method="curated_semantic_composition",
        scenario_family="gold_fixture",
        prompt_template="template:gold_fixture",
        generation_batch="fixture-batch",
        review_status="agent_reviewed_pending_project_review",
        rights_status="project_controlled_no_third_party_text",
        split_group="gold_fixture|template:gold_fixture|fixture-batch",
        assigned_split="gold",
        safety_critical=not positive,
    )


class BlindReviewPackageTests(unittest.TestCase):
    def _module(self):
        name = "r34a1_greeting.blind_review"
        self.assertIsNotNone(
            importlib.util.find_spec(name),
            "blind-review package builder has not been implemented",
        )
        return importlib.import_module(name)

    def _build_fixture(self, root: Path):
        module = self._module()
        build = getattr(module, "build_blind_review_package", None)
        self.assertTrue(callable(build), "build_blind_review_package is missing")
        repository = root / "repository"
        repository.mkdir(parents=True)
        gold_path = repository / "gold.jsonl"
        rows = [example(index) for index in range(12)]
        write_examples(gold_path, rows)
        output = root / "review-package"
        result = build(
            gold_path=gold_path,
            output_directory=output,
            repository_root=repository,
        )
        return module, rows, result

    def test_csv_contains_only_blind_columns_and_empty_review_fields(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            _module, source_rows, result = self._build_fixture(Path(directory))
            with result.csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
                review_rows = list(csv.DictReader(handle))

            self.assertEqual(EXPECTED_COLUMNS, tuple(review_rows[0]))
            self.assertEqual(len(source_rows), len(review_rows))
            self.assertEqual(
                {row.user for row in source_rows},
                {row["userMessage"] for row in review_rows},
            )
            self.assertTrue(
                all(
                    not row["reviewerLabel"]
                    and not row["reviewerConfidence"]
                    and not row["reviewerNote"]
                    for row in review_rows
                )
            )
            serialized = result.csv_path.read_text(encoding="utf-8-sig")
            for hidden_field in (
                "label",
                "scenarioFamily",
                "splitGroup",
                "promptTemplate",
                "generationBatch",
                "sourceId",
                "confidence",
                "margin",
                "logits",
            ):
                self.assertNotIn(hidden_field, review_rows[0])
            for row in source_rows:
                self.assertNotIn(row.source_id, serialized)

    def test_order_ids_and_mapping_are_stable_and_complete(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            module, source_rows, first = self._build_fixture(root / "first")

            second_root = root / "second"
            second_root.mkdir()
            repository = second_root / "repository"
            repository.mkdir()
            gold_path = repository / "gold.jsonl"
            write_examples(gold_path, source_rows)
            second = module.build_blind_review_package(
                gold_path=gold_path,
                output_directory=second_root / "review-package",
                repository_root=repository,
            )

            self.assertEqual(first.csv_path.read_bytes(), second.csv_path.read_bytes())
            self.assertEqual(first.mapping_path.read_bytes(), second.mapping_path.read_bytes())
            mapping = json.loads(first.mapping_path.read_text(encoding="utf-8"))
            self.assertEqual(len(source_rows), mapping["rowCount"])
            self.assertEqual(
                {row.source_id for row in source_rows},
                {row["sourceId"] for row in mapping["rows"]},
            )
            review_ids = [row["reviewId"] for row in mapping["rows"]]
            self.assertEqual(len(review_ids), len(set(review_ids)))
            self.assertTrue(all(value.startswith("r34a2-") for value in review_ids))

    def test_verifier_rejects_modified_review_text(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            module, _source_rows, result = self._build_fixture(Path(directory))
            verify = getattr(module, "verify_blind_review_package", None)
            self.assertTrue(callable(verify), "verify_blind_review_package is missing")
            with result.csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
                rows = list(csv.DictReader(handle))
            rows[0]["userMessage"] = "被修改的文本"
            with result.csv_path.open("w", encoding="utf-8-sig", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=EXPECTED_COLUMNS)
                writer.writeheader()
                writer.writerows(rows)

            with self.assertRaisesRegex(ValueError, "userMessage"):
                verify(result.csv_path, result.mapping_path)

    def test_output_inside_repository_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            module = self._module()
            repository = Path(directory) / "repository"
            repository.mkdir()
            gold_path = repository / "gold.jsonl"
            write_examples(gold_path, [example(index) for index in range(4)])

            with self.assertRaisesRegex(ValueError, "outside the repository"):
                module.build_blind_review_package(
                    gold_path=gold_path,
                    output_directory=repository / "review-package",
                    repository_root=repository,
                )

    def test_instructions_define_pair_judgment_and_allowed_values(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            _module, _source_rows, result = self._build_fixture(Path(directory))
            self.assertTrue(
                result.mapping_path.name.startswith("."),
                "internal mapping must be hidden from the reviewer by default",
            )
            instructions = result.instructions_path.read_text(encoding="utf-8")
            for required in (
                "user message and assistant reply together",
                "Wave eligibility",
                "greeting",
                "not_greeting",
                "ambiguous",
                "invalid",
                "high",
                "medium",
                "low",
                "Review all rows",
                "Do not edit userMessage or assistantReply",
            ):
                self.assertIn(required, instructions)


if __name__ == "__main__":
    unittest.main()
