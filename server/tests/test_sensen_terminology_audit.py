import json
import re
import unittest
from pathlib import Path
from unittest import mock

from server import animal_knowledge, dev_server


ROOT = Path(__file__).resolve().parents[2]
TERMINOLOGY_CASES = json.loads(
    (ROOT / "content" / "quality" / "sensen-r2.1-terminology-questions.json").read_text(encoding="utf-8")
)["cases"]


class SensenScientificTerminologyAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = animal_knowledge.load_animal_knowledge("sensen")

    def test_canonical_identity_and_conservation_systems_are_explicit(self):
        identity = self.document["identity"]
        self.assertEqual(identity["chineseName"], "缨冠灰叶猴")
        self.assertEqual(identity["nickname"], "森森")
        self.assertEqual(identity["englishName"], "Tufted Gray Langur")
        self.assertEqual(identity["scientificName"], "Semnopithecus priam")

        status = next(
            fact for fact in self.document["facts"]
            if fact["factId"] == "sensen.conservation_status"
        )
        self.assertEqual(status["displayValue"], "近危（NT）")
        self.assertIn("IUCN：近危（NT）", status["items"])
        self.assertIn("CITES：附录 I", status["items"])
        self.assertIn("不是濒危（EN）", status["approvedAnswer"])
        self.assertIn("不等于", status["approvedAnswer"])

        population = next(
            fact for fact in self.document["facts"]
            if fact["factId"] == "sensen.population.global"
        )
        self.assertEqual(population["evidenceStatus"], "known_unknown")
        self.assertNotRegex(population["approvedAnswer"], r"\d+\s*只")

    def test_runtime_prompt_uses_broad_protected_wildlife_positioning(self):
        prompt = dev_server.make_system_prompt(self.document)

        self.assertIn("珍稀及受保护野生动物科普 App", prompt)
        self.assertNotIn("你是濒危动物科普 App 中的角色", prompt)

    def test_ten_positioning_questions_use_expected_grounded_facts(self):
        self.assertEqual(len(TERMINOLOGY_CASES), 10)

        for case in TERMINOLOGY_CASES:
            with self.subTest(case_id=case["id"], question=case["question"]):
                result = animal_knowledge.retrieve(
                    self.document,
                    case["question"],
                    animal_id="sensen",
                )
                fact_ids = [fact["factId"] for fact in result.facts]

                self.assertEqual(result.answer_mode, "grounded_fact")
                self.assertEqual(result.evidence_status, case["evidenceStatus"])
                self.assertIn(case["factId"], fact_ids)
                self.assertTrue(result.citations)
                for expected in case["mustContain"]:
                    self.assertIn(expected, result.approved_answer)
                self.assertNotIn("IUCN：濒危", result.approved_answer)
                self.assertNotIn("评为濒危（EN）", result.approved_answer)

    def test_local_and_cloud_share_the_same_scientific_answer_and_citations(self):
        with mock.patch("server.dev_server.get_animal", return_value=self.document), mock.patch(
            "server.dev_server.call_local_llm",
            return_value=dev_server.ProviderResult(reply="IUCN 是濒危 EN，全球有 1234 只。"),
        ), mock.patch(
            "server.dev_server.call_moonshot",
            return_value="CITES 附录 I 就等于 IUCN 濒危。",
        ):
            for case in TERMINOLOGY_CASES:
                with self.subTest(case_id=case["id"]):
                    request = {"animalId": "sensen", "message": case["question"], "history": []}
                    local_payload, local_status = dev_server.process_chat_request("/chat/local", request)
                    cloud_payload, cloud_status = dev_server.process_chat_request("/chat", request)

                    self.assertEqual((local_status, cloud_status), (200, 200))
                    self.assertEqual(local_payload["reply"], cloud_payload["reply"])
                    self.assertEqual(local_payload["answerMode"], "grounded_fact")
                    self.assertEqual(local_payload["evidenceStatus"], case["evidenceStatus"])
                    self.assertEqual(local_payload["citations"], cloud_payload["citations"])
                    self.assertNotRegex(local_payload["reply"], re.compile(r"全球有\s*1234\s*只"))
                    if case["evidenceStatus"] == "insufficient_evidence":
                        self.assertEqual(local_payload["source"], "server_knowledge")
                        self.assertEqual(cloud_payload["source"], "server_knowledge")
                        self.assertEqual(local_payload["routeReason"], "deterministic_insufficient_evidence")
                        self.assertEqual(cloud_payload["routeReason"], "deterministic_insufficient_evidence")
                    else:
                        self.assertEqual(local_payload["source"], "local_llm")
                        self.assertEqual(cloud_payload["source"], "cloud_llm")
                        self.assertEqual(local_payload["routeReason"], "local_provider_succeeded")
                        self.assertEqual(cloud_payload["routeReason"], "cloud_provider_succeeded")


if __name__ == "__main__":
    unittest.main()
