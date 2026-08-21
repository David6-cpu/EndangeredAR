import unittest

from server import animal_knowledge


class AnimalKnowledgeSchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.document = animal_knowledge.load_animal_knowledge("sensen")

    def test_canonical_document_is_valid(self):
        self.assertEqual(animal_knowledge.validate_document(self.document), [])

    def test_species_identity_is_reviewed_tufted_gray_langur(self):
        identity = self.document["identity"]

        self.assertEqual(identity["chineseName"], "缨冠灰叶猴")
        self.assertEqual(identity["englishName"], "Tufted Gray Langur")
        self.assertEqual(identity["scientificName"], "Semnopithecus priam")
        self.assertEqual(identity["taxonomy"]["family"], "Cercopithecidae")
        self.assertEqual(identity["taxonomy"]["genus"], "Semnopithecus")

    def test_every_fact_has_stable_sources_or_explicit_non_scientific_scope(self):
        source_ids = {source["sourceId"] for source in self.document["sources"]}

        for fact in self.document["facts"]:
            with self.subTest(fact_id=fact["factId"]):
                self.assertTrue(fact["factId"].startswith("sensen."))
                self.assertIn(fact["evidenceStatus"], ("evidence_found", "known_unknown"))
                self.assertTrue(fact["sourceIds"])
                self.assertTrue(set(fact["sourceIds"]).issubset(source_ids))
                self.assertRegex(fact["lastVerified"], r"^\d{4}-\d{2}-\d{2}$")

    def test_population_is_a_cited_known_unknown(self):
        fact = animal_knowledge.get_fact(self.document, "sensen.population.global")

        self.assertEqual(fact["topic"], "population")
        self.assertEqual(fact["evidenceStatus"], "known_unknown")
        self.assertIn("未知", fact["claim"])
        self.assertIn("iucn-2020-s-priam", fact["sourceIds"])

    def test_sources_have_traceable_metadata_and_fact_links(self):
        fact_ids = {fact["factId"] for fact in self.document["facts"]}

        for source in self.document["sources"]:
            with self.subTest(source_id=source["sourceId"]):
                self.assertTrue(source["title"])
                self.assertTrue(source["organization"])
                self.assertTrue(source["sourceType"])
                self.assertTrue(source["url"].startswith("https://"))
                self.assertRegex(source["projectVerifiedDate"], r"^\d{4}-\d{2}-\d{2}$")
                self.assertTrue(set(source["appliesToFactIds"]).issubset(fact_ids))


if __name__ == "__main__":
    unittest.main()
