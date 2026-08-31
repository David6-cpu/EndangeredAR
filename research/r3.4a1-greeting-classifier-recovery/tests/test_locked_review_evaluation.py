from __future__ import annotations

import numpy as np
import unittest

from r34a1_greeting.locked_review_evaluation import (
    LOCKED_CANDIDATE,
    evaluate_locked_partition,
    quality_gate,
)
from r34a1_greeting.schema import GreetingExample


def row(index: int, *, greeting: bool, safety_critical: bool = False) -> GreetingExample:
    return GreetingExample(
        source_id=f"locked-row-{index}",
        user="你好" if greeting else "请解释你好是什么意思",
        reply="你也好呀" if greeting else "这是一个问候词。",
        label="Greeting" if greeting else "NotGreeting",
        source_type="project_authored_synthetic",
        generation_method="curated_semantic_composition",
        scenario_family=f"locked-family-{index}",
        prompt_template=f"locked-template-{index}",
        generation_batch="locked-fixture",
        review_status="project_member_reviewed",
        rights_status="project_controlled_no_third_party_text",
        split_group=f"locked-group-{index}",
        assigned_split="gold",
        safety_critical=safety_critical,
    )


class LockedReviewEvaluationTests(unittest.TestCase):
    def test_candidate_values_are_dev_locked(self) -> None:
        self.assertEqual("textcnn-user_reply_pair", LOCKED_CANDIDATE["name"])
        self.assertEqual("user_reply_pair", LOCKED_CANDIDATE["inputForm"])
        self.assertEqual(0.5, LOCKED_CANDIDATE["temperature"])
        self.assertEqual(0.5, LOCKED_CANDIDATE["confidenceThreshold"])
        self.assertEqual(0.0, LOCKED_CANDIDATE["marginThreshold"])

    def test_partition_metrics_apply_rule_and_locked_thresholds(self) -> None:
        rows = [
            row(0, greeting=True),
            row(1, greeting=True),
            row(2, greeting=False, safety_critical=True),
            row(3, greeting=False, safety_critical=True),
        ]
        probabilities = np.asarray([0.99, 0.90, 0.99, 0.01], dtype=np.float64)
        rule_mask = np.asarray([True, False, True, False])

        result = evaluate_locked_partition(rows, probabilities, rule_mask)

        self.assertEqual(1.0 / 2.0, result["gate"]["precision"])
        self.assertEqual(1.0 / 2.0, result["gate"]["recall"])
        self.assertEqual([[1, 1], [1, 1]], result["gate"]["confusionMatrix"])
        self.assertEqual(1, result["gate"]["safetyCriticalFalsePositive"])

    def test_quality_gate_requires_test_gold_and_zero_safety_false_positives(self) -> None:
        passing = {
            "precision": 0.95,
            "recall": 0.70,
            "safetyCriticalFalsePositive": 0,
        }
        self.assertTrue(quality_gate(passing, passing)["passed"])
        failing = dict(passing, safetyCriticalFalsePositive=1)
        self.assertFalse(quality_gate(passing, failing)["passed"])


if __name__ == "__main__":
    unittest.main()
