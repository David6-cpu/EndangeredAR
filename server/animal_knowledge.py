import json
from pathlib import Path
from typing import Dict, List, Optional


ROOT = Path(__file__).resolve().parents[1]
ANIMALS_DIR = ROOT / "content" / "animals"
SUPPORTED_SCHEMA_VERSION = 1
EVIDENCE_STATUSES = {"evidence_found", "known_unknown"}


def load_animal_knowledge(animal_id: str) -> Dict:
    safe_id = "".join(
        character for character in str(animal_id)
        if character.isalnum() or character in "-_"
    )
    if not safe_id:
        raise ValueError("animal_id_required")
    path = ANIMALS_DIR / f"{safe_id}.json"
    if not path.exists():
        raise FileNotFoundError(path)
    document = json.loads(path.read_text(encoding="utf-8"))
    errors = validate_document(document)
    if errors:
        raise ValueError("invalid_animal_knowledge: " + "; ".join(errors))
    return document


def get_fact(document: Dict, fact_id: str) -> Optional[Dict]:
    for fact in document.get("facts", []):
        if fact.get("factId") == fact_id:
            return fact
    return None


def validate_document(document: Dict) -> List[str]:
    errors: List[str] = []
    if not isinstance(document, dict):
        return ["document must be an object"]
    if document.get("schemaVersion") != SUPPORTED_SCHEMA_VERSION:
        errors.append("unsupported schemaVersion")
    animal_id = _required_string(document, "animalId", errors, "document")

    identity = document.get("identity")
    if not isinstance(identity, dict):
        errors.append("identity must be an object")
    else:
        for field in ("chineseName", "nickname", "englishName", "scientificName"):
            _required_string(identity, field, errors, "identity")
        taxonomy = identity.get("taxonomy")
        if not isinstance(taxonomy, dict):
            errors.append("identity.taxonomy must be an object")
        else:
            for field in ("kingdom", "phylum", "className", "order", "family", "genus", "species"):
                _required_string(taxonomy, field, errors, "identity.taxonomy")

    sources = document.get("sources")
    facts = document.get("facts")
    if not isinstance(sources, list) or not sources:
        errors.append("sources must be a non-empty array")
        sources = []
    if not isinstance(facts, list) or not facts:
        errors.append("facts must be a non-empty array")
        facts = []

    source_ids = _validate_sources(sources, errors)
    fact_ids = _validate_facts(facts, animal_id, source_ids, errors)
    for source in sources:
        applies_to = source.get("appliesToFactIds")
        if not isinstance(applies_to, list):
            errors.append(f"source {source.get('sourceId')} appliesToFactIds must be an array")
            continue
        for fact_id in applies_to:
            if fact_id not in fact_ids:
                errors.append(f"source {source.get('sourceId')} references missing fact {fact_id}")
    return errors


def _validate_sources(sources: List[Dict], errors: List[str]) -> set:
    source_ids = set()
    for source in sources:
        if not isinstance(source, dict):
            errors.append("source must be an object")
            continue
        source_id = _required_string(source, "sourceId", errors, "source")
        if source_id in source_ids:
            errors.append(f"duplicate sourceId {source_id}")
        source_ids.add(source_id)
        for field in (
            "title", "organization", "sourceType", "url",
            "publishedOrUpdatedDate", "projectVerifiedDate",
        ):
            _required_string(source, field, errors, f"source {source_id}")
        if source.get("url") and not source["url"].startswith("https://"):
            errors.append(f"source {source_id} url must use https")
    return source_ids


def _validate_facts(facts: List[Dict], animal_id: str, source_ids: set, errors: List[str]) -> set:
    fact_ids = set()
    for fact in facts:
        if not isinstance(fact, dict):
            errors.append("fact must be an object")
            continue
        fact_id = _required_string(fact, "factId", errors, "fact")
        if fact_id in fact_ids:
            errors.append(f"duplicate factId {fact_id}")
        fact_ids.add(fact_id)
        if animal_id and not fact_id.startswith(f"{animal_id}."):
            errors.append(f"factId {fact_id} is outside animal {animal_id}")
        for field in ("topic", "claim", "approvedAnswer", "displayValue", "confidence", "lastVerified"):
            _required_string(fact, field, errors, f"fact {fact_id}")
        if fact.get("evidenceStatus") not in EVIDENCE_STATUSES:
            errors.append(f"fact {fact_id} has unsupported evidenceStatus")
        for field in ("keywords", "aliases", "items", "sourceIds"):
            values = fact.get(field)
            if not isinstance(values, list):
                errors.append(f"fact {fact_id} {field} must be an array")
        fact_sources = fact.get("sourceIds") if isinstance(fact.get("sourceIds"), list) else []
        if not fact_sources:
            errors.append(f"fact {fact_id} has no sources")
        for source_id in fact_sources:
            if source_id not in source_ids:
                errors.append(f"fact {fact_id} references missing source {source_id}")
    return fact_ids


def _required_string(container: Dict, field: str, errors: List[str], context: str) -> str:
    value = container.get(field)
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{context}.{field} must be a non-empty string")
        return ""
    return value.strip()
