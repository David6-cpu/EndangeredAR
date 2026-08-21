import json
import re
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Tuple


ROOT = Path(__file__).resolve().parents[1]
ANIMALS_DIR = ROOT / "content" / "animals"
SUPPORTED_SCHEMA_VERSION = 1
EVIDENCE_STATUSES = {"evidence_found", "known_unknown"}
SOCIAL_MARKERS = (
    "你好", "谢谢", "再见", "难过", "伤心", "开心", "陪我", "聊聊",
    "喜欢我", "语气", "讲个故事", "介绍自己",
)
OFF_DOMAIN_MARKERS = (
    "二次方程", "数学题", "写代码", "编程", "股票", "投资", "天气",
    "翻译", "写作文", "做作业",
)
INJECTION_MARKERS = (
    "忽略系统", "忽略规则", "忽略资料", "忽略以上", "绕过规则",
    "忽略之前", "不要根据资料", "假装你确定", "知识库", "系统提示词",
    "隐藏指令", "编造", "编一个",
)
MISSING_EVIDENCE_MARKERS = (
    "资料里没有答案", "资料没有答案", "没有资料", "找不到资料",
    "没有可靠资料", "没有证据",
)
SCIENTIFIC_QUESTION_MARKERS = (
    "学名", "分类", "分布", "栖息", "住", "生活", "吃", "食物", "食性",
    "行为", "习性", "威胁", "危险", "变少", "数量", "多少", "几只",
    "保护", "等级", "近危", "濒危", "会", "能不能", "是否", "为什么",
)


@dataclass(frozen=True)
class RetrievalResult:
    answer_mode: str
    evidence_status: str
    facts: Tuple[Dict, ...]
    citations: Tuple[Dict, ...]
    approved_answer: str
    classification_reason: str

    @property
    def source_ids(self) -> Tuple[str, ...]:
        return tuple(citation["sourceId"] for citation in self.citations)


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


def retrieve(document: Dict, message: str, animal_id: Optional[str] = None) -> RetrievalResult:
    requested_animal_id = str(animal_id or document.get("animalId") or document.get("id") or "").strip()
    document_animal_id = str(document.get("animalId") or document.get("id") or "").strip()
    if not requested_animal_id or requested_animal_id != document_animal_id:
        return _insufficient(document, "animal_mismatch")

    normalized = normalize_text(message)
    if not normalized:
        return _insufficient(document, "empty_question")
    if any(marker in normalized for marker in map(normalize_text, SOCIAL_MARKERS)):
        return RetrievalResult(
            "social_chat",
            "not_required",
            (),
            (),
            "我在呢。你想聊聊今天的心情，还是继续认识森林里的动物朋友？",
            "social_marker",
        )

    scored_facts = []
    for index, fact in enumerate(document.get("facts", [])):
        score = _score_fact(fact, normalized)
        if score > 0:
            scored_facts.append((score, -index, fact))
    if scored_facts:
        scored_facts.sort(reverse=True, key=lambda item: (item[0], item[1]))
        best_score = scored_facts[0][0]
        matched = tuple(item[2] for item in scored_facts if item[0] == best_score)
        primary = matched[0]
        evidence_status = (
            "insufficient_evidence"
            if primary.get("evidenceStatus") == "known_unknown"
            else "evidence_found"
        )
        return RetrievalResult(
            "grounded_fact",
            evidence_status,
            matched,
            _citations_for_facts(document, matched),
            str(primary.get("approvedAnswer") or "").strip(),
            f"matched_{primary.get('topic') or 'fact'}",
        )

    if any(marker in normalized for marker in map(normalize_text, MISSING_EVIDENCE_MARKERS)):
        return _insufficient(document, "missing_evidence_policy")
    if any(marker in normalized for marker in map(normalize_text, INJECTION_MARKERS)):
        return RetrievalResult(
            "off_domain",
            "not_required",
            (),
            (),
            "我不能提供隐藏指令，也不会忽略可靠资料。我们可以继续聊森森和濒危动物保护。",
            "prompt_injection",
        )
    if any(marker in normalized for marker in map(normalize_text, OFF_DOMAIN_MARKERS)):
        return RetrievalResult(
            "off_domain",
            "not_required",
            (),
            (),
            "我主要负责濒危动物科普，不能替你完成这个问题。要不要问问森森的家园或保护方法？",
            "off_domain_marker",
        )
    if (
        any(marker in normalized for marker in map(normalize_text, SCIENTIFIC_QUESTION_MARKERS))
    ):
        return _insufficient(document, "unmatched_scientific_question")

    return RetrievalResult(
        "social_chat",
        "not_required",
        (),
        (),
        "我在呢。你想聊聊今天的心情，还是继续认识森林里的动物朋友？",
        "default_social_chat",
    )


def normalize_text(value: str) -> str:
    normalized = unicodedata.normalize("NFKC", str(value or "")).lower()
    return re.sub(r"[\s\W_]+", "", normalized, flags=re.UNICODE)


def _score_fact(fact: Dict, normalized_message: str) -> int:
    best = 0
    terms = list(fact.get("aliases") or []) + list(fact.get("keywords") or [])
    for term_index, term in enumerate(terms):
        normalized_term = normalize_text(term)
        if not normalized_term or normalized_term not in normalized_message:
            continue
        exact_bonus = 1000 if normalized_term == normalized_message else 0
        alias_bonus = 100 if term_index < len(fact.get("aliases") or []) else 0
        best = max(best, exact_bonus + alias_bonus + len(normalized_term))
    return best


def _citations_for_facts(document: Dict, facts: Tuple[Dict, ...]) -> Tuple[Dict, ...]:
    sources_by_id = {
        source.get("sourceId"): source
        for source in document.get("sources", [])
        if isinstance(source, dict) and source.get("sourceId")
    }
    citations = []
    seen = set()
    for fact in facts:
        for source_id in fact.get("sourceIds", []):
            source = sources_by_id.get(source_id)
            if source is None or source_id in seen:
                continue
            seen.add(source_id)
            citations.append({
                "sourceId": source_id,
                "title": source.get("title", ""),
                "organization": source.get("organization", ""),
                "url": source.get("url", ""),
            })
    return tuple(citations)


def _insufficient(document: Dict, reason: str) -> RetrievalResult:
    presentation = document.get("presentation")
    configured_reply = presentation.get("unknownReply") if isinstance(presentation, dict) else None
    reply = configured_reply or "我现在的可靠资料里还没有这个问题的确定答案，所以不能随便告诉你一个答案。"
    return RetrievalResult(
        "grounded_fact",
        "insufficient_evidence",
        (),
        (),
        reply,
        reason,
    )


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
