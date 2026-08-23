import json
import unittest
from pathlib import Path

from server import action_intent


ROOT = Path(__file__).resolve().parents[2]
VECTORS_PATH = ROOT / "content" / "quality" / "sensen-action-intent-vectors.json"


class ActionIntentContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = json.loads(VECTORS_PATH.read_text(encoding="utf-8"))

    def test_fixture_has_versioned_balanced_security_cases(self):
        self.assertEqual(self.fixture["schemaVersion"], 1)
        vectors = self.fixture["vectors"]
        allowed = [vector for vector in vectors if vector["expected"] == "taunt"]
        denied = [vector for vector in vectors if vector["expected"] == "none"]

        self.assertGreaterEqual(len(allowed), 10)
        self.assertGreaterEqual(len(denied), 20)
        self.assertEqual(len({vector["id"] for vector in vectors}), len(vectors))

    def test_python_resolver_matches_shared_vectors(self):
        failures = []
        for vector in self.fixture["vectors"]:
            actual = action_intent.resolve_action_suggestion(vector["message"])
            if actual != vector["expected"]:
                failures.append(
                    f"{vector['id']}: expected {vector['expected']!r}, got {actual!r}"
                )

        self.assertEqual(failures, [])

    def test_python_contract_exposes_only_none_and_taunt(self):
        self.assertEqual(action_intent.NONE, "none")
        self.assertEqual(action_intent.TAUNT, "taunt")
        self.assertEqual(action_intent.ALLOWED_ACTIONS, frozenset({"none", "taunt"}))


if __name__ == "__main__":
    unittest.main()
