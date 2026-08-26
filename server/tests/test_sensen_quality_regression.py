import json
import unittest
from pathlib import Path
from unittest import mock

from server import animal_knowledge, dev_server


ROOT = Path(__file__).resolve().parents[2]
QUALITY_CASES = json.loads(
    (ROOT / "content" / "quality" / "sensen-r1.5-questions.json").read_text(encoding="utf-8")
)["cases"]


class SensenTwentyQuestionRegressionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = animal_knowledge.load_animal_knowledge("sensen")

    def test_original_twenty_questions_have_expected_deterministic_classification(self):
        self.assertEqual(len(QUALITY_CASES), 20)

        for case in QUALITY_CASES:
            with self.subTest(case_id=case["id"], question=case["question"]):
                result = animal_knowledge.retrieve(self.document, case["question"], animal_id="sensen")
                fact_ids = [fact["factId"] for fact in result.facts]

                self.assertEqual(result.answer_mode, case["answerMode"])
                self.assertEqual(result.evidence_status, case["evidenceStatus"])
                if case["factId"] is None:
                    self.assertEqual(fact_ids, [])
                else:
                    self.assertIn(case["factId"], fact_ids)
                for expected in case["mustContain"]:
                    self.assertIn(expected, result.approved_answer)
                for forbidden in case["forbidden"]:
                    self.assertNotIn(forbidden, result.approved_answer)

    def test_all_retrieved_scientific_facts_have_canonical_citations(self):
        source_ids = {source["sourceId"] for source in self.document["sources"]}

        for case in QUALITY_CASES:
            if case["factId"] is None:
                continue
            with self.subTest(case_id=case["id"]):
                result = animal_knowledge.retrieve(self.document, case["question"], animal_id="sensen")
                self.assertTrue(result.citations)
                self.assertTrue(set(result.source_ids).issubset(source_ids))

    def test_local_and_cloud_cannot_override_grounded_twenty_question_answers(self):
        grounded_cases = [
            case for case in QUALITY_CASES
            if case["answerMode"] == "grounded_fact" and case["factId"] is not None
        ]
        with mock.patch("server.dev_server.get_animal", return_value=self.document), mock.patch(
            "server.dev_server.call_local_llm",
            return_value=dev_server.ProviderResult(reply="树洞里有 12345 只，来源 fake-source。"),
        ), mock.patch(
            "server.dev_server.call_moonshot",
            return_value="学名 Rhinolophus helenae，分布在云南、广西和贵州。",
        ):
            for case in grounded_cases:
                with self.subTest(case_id=case["id"]):
                    local_payload, local_status = dev_server.process_chat_request(
                        "/chat/local", {"animalId": "sensen", "message": case["question"]}
                    )
                    cloud_payload, cloud_status = dev_server.process_chat_request(
                        "/chat", {"animalId": "sensen", "message": case["question"]}
                    )

                    self.assertEqual((local_status, cloud_status), (422, 422))
                    self.assertEqual(local_payload, {"error": "ai_response_validation_failed"})
                    self.assertEqual(cloud_payload, {"error": "ai_response_validation_failed"})

    def test_all_twenty_questions_complete_through_both_http_route_handlers(self):
        def approved_local(animal, message, *args):
            result = animal_knowledge.retrieve(animal, message, animal_id="sensen")
            return dev_server.ProviderResult(reply=result.approved_answer)

        def approved_cloud(animal, message, *args):
            return animal_knowledge.retrieve(
                animal, message, animal_id="sensen"
            ).approved_answer

        with mock.patch("server.dev_server.get_animal", return_value=self.document), mock.patch(
            "server.dev_server.call_local_llm",
            side_effect=approved_local,
        ), mock.patch(
            "server.dev_server.call_moonshot",
            side_effect=approved_cloud,
        ):
            for case in QUALITY_CASES:
                with self.subTest(case_id=case["id"]):
                    local_payload, local_status = dev_server.process_chat_request(
                        "/chat/local", {"animalId": "sensen", "message": case["question"]}
                    )
                    cloud_payload, cloud_status = dev_server.process_chat_request(
                        "/chat", {"animalId": "sensen", "message": case["question"]}
                    )

                    self.assertEqual((local_status, cloud_status), (200, 200))
                    self.assertEqual(local_payload["answerMode"], case["answerMode"])
                    self.assertEqual(cloud_payload["answerMode"], case["answerMode"])
                    self.assertEqual(local_payload["evidenceStatus"], case["evidenceStatus"])
                    self.assertEqual(cloud_payload["evidenceStatus"], case["evidenceStatus"])


if __name__ == "__main__":
    unittest.main()
