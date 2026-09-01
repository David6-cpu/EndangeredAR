from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest

from r34a4_qwen_domain.pilot import (
    PRODUCTION_SYSTEM_ROLE,
    ensure_local_only_output,
    load_prompt_manifest,
    production_messages,
    validate_prompt_manifest,
)
from r34a4_qwen_domain.evaluation import reply_compatibility_guard


PROJECT_ROOT = Path(__file__).resolve().parents[3]
RESEARCH_ROOT = PROJECT_ROOT / "research/r3.4a4-real-qwen-domain-recovery"
PROMPT_MANIFEST = RESEARCH_ROOT / "manifests/pilot-prompts.json"


class PilotPromptManifestTests(unittest.TestCase):
    def test_manifest_has_exact_pilot_counts_and_no_generated_replies(self) -> None:
        manifest = load_prompt_manifest(PROMPT_MANIFEST)
        validate_prompt_manifest(manifest)
        rows = manifest["prompts"]
        counts = {
            category: sum(row["category"] == category for row in rows)
            for category in ("greeting", "hard_negative", "product_negative")
        }
        self.assertEqual(
            {"greeting": 30, "hard_negative": 30, "product_negative": 20},
            counts,
        )
        self.assertEqual(80, len(rows))
        self.assertTrue(all("assistantReply" not in row for row in rows))
        self.assertEqual(80, len({row["promptId"] for row in rows}))
        self.assertEqual(80, len({row["userMessage"] for row in rows}))

    def test_scenario_families_are_split_before_generation_without_leakage(self) -> None:
        rows = load_prompt_manifest(PROMPT_MANIFEST)["prompts"]
        family_splits: dict[str, set[str]] = {}
        for row in rows:
            family_splits.setdefault(row["scenarioFamily"], set()).add(row["split"])
            self.assertIn(row["split"], ("train", "dev", "test"))
            self.assertIsInstance(row["generationSeed"], int)
        self.assertTrue(all(len(splits) == 1 for splits in family_splits.values()))

    def test_generation_output_must_remain_outside_repository(self) -> None:
        with self.assertRaisesRegex(ValueError, "outside the repository"):
            ensure_local_only_output(PROJECT_ROOT / "pilot.jsonl", PROJECT_ROOT)
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "pilot.jsonl"
            self.assertEqual(output.resolve(), ensure_local_only_output(output, PROJECT_ROOT))

    def test_prompt_builder_uses_production_social_chat_semantics(self) -> None:
        messages = production_messages("你好", "none", "")
        self.assertEqual("system", messages[0]["role"])
        self.assertEqual(PRODUCTION_SYSTEM_ROLE, messages[0]["content"])
        self.assertEqual({"role": "user", "content": "你好"}, messages[1])

    def test_prompt_builder_appends_only_controlled_authority_context(self) -> None:
        messages = production_messages(
            "你的学名是什么？",
            "canonical_knowledge",
            "证据状态：evidence_found\n回答事实边界：我的学名是 Semnopithecus priam。",
        )
        system = messages[0]["content"]
        self.assertIn("<CANONICAL EVIDENCE>", system)
        self.assertIn("Semnopithecus priam", system)
        self.assertNotIn("memory", system.lower())


class PilotManifestMutationTests(unittest.TestCase):
    def test_validation_rejects_family_leakage(self) -> None:
        manifest = load_prompt_manifest(PROMPT_MANIFEST)
        clone = json.loads(json.dumps(manifest, ensure_ascii=False))
        family = clone["prompts"][0]["scenarioFamily"]
        source_split = clone["prompts"][0]["split"]
        target = next(
            row
            for row in clone["prompts"]
            if row["split"] != source_split
        )
        target["scenarioFamily"] = family
        with self.assertRaisesRegex(ValueError, "scenario family leakage"):
            validate_prompt_manifest(clone)

    def test_validation_rejects_unknown_rights_or_review_status(self) -> None:
        manifest = load_prompt_manifest(PROMPT_MANIFEST)
        clone = json.loads(json.dumps(manifest, ensure_ascii=False))
        clone["rightsStatus"] = "unknown"
        with self.assertRaisesRegex(ValueError, "rightsStatus"):
            validate_prompt_manifest(clone)


class ReplyCompatibilityGuardTests(unittest.TestCase):
    def test_guard_accepts_natural_greeting_and_rejects_obvious_incompatibility(self) -> None:
        self.assertTrue(reply_compatibility_guard("你好！很高兴见到你。"))
        for reply in (
            "我不能执行 Wave 动画。",
            "我的学名是 Semnopithecus priam。",
            "当前任务已经完成。",
            "回答没有通过校验。",
        ):
            with self.subTest(reply=reply):
                self.assertFalse(reply_compatibility_guard(reply))


if __name__ == "__main__":
    unittest.main()
