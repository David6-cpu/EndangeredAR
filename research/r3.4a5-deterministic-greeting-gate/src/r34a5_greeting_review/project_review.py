from __future__ import annotations

from datetime import datetime
import hashlib
import json
import math
from pathlib import Path
import re

from .review_package import (
    ALLOWED_CONFIDENCE,
    CSV_COLUMNS,
    SCHEMA_VERSION,
    load_candidate_manifest,
)


PROJECT_REVIEW_SCHEMA_VERSION = "r3.4a5-deterministic-greeting-gold-v1-project-review-v1"
REVIEWED_GOLD_SCHEMA_VERSION = "r3.4a5-deterministic-greeting-gold-v1-project-reviewed-v1"
FINAL_REVIEW_LABELS = ("greeting", "not_greeting")
REVIEW_STATUS = "project_member_reviewed"
_REVIEWER_ROLE_PATTERN = re.compile(r"project_(?:owner|member_[1-9][0-9]*)\Z")
_PRIVATE_REVIEW_PATTERNS = (
    re.compile(r"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", re.IGNORECASE),
    re.compile(r"(?:/Users/|/private/|file://|[A-Z]:\\Users\\)", re.IGNORECASE),
    re.compile(r"\bBearer\s+[A-Za-z0-9._~+/=-]+", re.IGNORECASE),
    re.compile(r"\b(?:api[_ -]?key|authorization|team[_ -]?id|udid)\s*[:=]", re.IGNORECASE),
)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_text(value: str) -> str:
    return _sha256_bytes(value.encode("utf-8"))


def _atomic_write(path: Path, value: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.tmp")
    temporary.write_bytes(value)
    temporary.replace(path)


def _validate_reviewer_role(value: str) -> None:
    if not _REVIEWER_ROLE_PATTERN.fullmatch(value):
        raise ValueError("reviewerRole must be a non-sensitive project role")


def _validate_reviewed_at_utc(value: str) -> None:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise ValueError("reviewedAtUtc must be an ISO-8601 timestamp") from error
    if parsed.utcoffset() is None or parsed.utcoffset().total_seconds() != 0:
        raise ValueError("reviewedAtUtc must use UTC")


def _validate_private_reviewer_fields(review_id: str, confidence: str, note: str) -> None:
    reviewer_text = "\n".join((confidence, note))
    if "\0" in reviewer_text or any(
        pattern.search(reviewer_text) for pattern in _PRIVATE_REVIEW_PATTERNS
    ):
        raise ValueError(f"private information found in reviewer fields for {review_id}")


def _load_review_extraction(path: Path) -> list[dict[str, object]]:
    extraction = json.loads(path.read_text(encoding="utf-8"))
    values = extraction.get("values")
    if not isinstance(values, list) or len(values) != 151:
        raise ValueError("review extraction must contain one header and 150 rows")
    if extraction.get("rowCount") != len(values) or extraction.get("columnCount") != 5:
        raise ValueError("review extraction dimensions do not match A1:E151")
    if tuple(values[0]) != CSV_COLUMNS:
        raise ValueError("project-review columns do not match the blind-review schema")
    formulas = extraction.get("formulas", [])
    if any(value not in (None, "") for row in formulas for value in row):
        raise ValueError("project-review workbook must not contain formulas")

    rows = []
    for values_row in values[1:]:
        if not isinstance(values_row, list) or len(values_row) != len(CSV_COLUMNS):
            raise ValueError("project-review row width does not match the schema")
        rows.append(dict(zip(CSV_COLUMNS, values_row, strict=True)))
    return rows


def _normalized_confidence(value: object, confirmed: bool, review_id: str) -> str:
    if isinstance(value, str):
        confidence = value.strip()
        if confidence in ALLOWED_CONFIDENCE:
            return confidence
        raise ValueError(f"invalid reviewerConfidence for {review_id}: {confidence or '<blank>'}")
    if isinstance(value, (int, float)) and math.isclose(
        float(value), 0.9, rel_tol=0.0, abs_tol=1e-12
    ):
        if not confirmed:
            raise ValueError(
                "numeric reviewerConfidence 0.9 requires explicit project-owner confirmation"
            )
        return "high"
    raise ValueError(f"invalid reviewerConfidence for {review_id}: {value!r}")


def import_project_review(
    *,
    review_extraction_path: Path,
    source_review_artifact_path: Path,
    mapping_path: Path,
    candidate_manifest_path: Path,
    reviewed_gold_path: Path,
    review_manifest_path: Path,
    reviewer_role: str,
    reviewed_at_utc: str,
    confirm_numeric_confidence_09_as_high: bool,
) -> dict[str, object]:
    _validate_reviewer_role(reviewer_role)
    _validate_reviewed_at_utc(reviewed_at_utc)
    review_extraction_path = review_extraction_path.resolve()
    source_review_artifact_path = source_review_artifact_path.resolve()
    mapping_path = mapping_path.resolve()
    candidate_manifest_path = candidate_manifest_path.resolve()
    reviewed_gold_path = reviewed_gold_path.resolve()
    review_manifest_path = review_manifest_path.resolve()

    candidate_bytes = candidate_manifest_path.read_bytes()
    candidates = load_candidate_manifest(candidate_manifest_path)
    candidate_rows = candidates.get("items")
    if not isinstance(candidate_rows, list) or len(candidate_rows) != 150:
        raise ValueError("candidate manifest must contain 150 rows")
    candidate_by_id = {str(row.get("itemId", "")): row for row in candidate_rows}
    if "" in candidate_by_id or len(candidate_by_id) != len(candidate_rows):
        raise ValueError("candidate manifest has empty or duplicate itemId")

    mapping_bytes = mapping_path.read_bytes()
    mapping = json.loads(mapping_bytes)
    if mapping.get("reviewSchemaVersion") != SCHEMA_VERSION:
        raise ValueError("unknown blind-review mapping schema")
    if mapping.get("sourceManifestSha256") != _sha256_bytes(candidate_bytes):
        raise ValueError("candidate manifest SHA-256 does not match blind mapping")
    mapping_rows = mapping.get("items")
    if not isinstance(mapping_rows, list) or len(mapping_rows) != 150:
        raise ValueError("blind mapping must contain 150 rows")
    mapping_by_id = {str(row.get("reviewId", "")): row for row in mapping_rows}
    if "" in mapping_by_id or len(mapping_by_id) != len(mapping_rows):
        raise ValueError("blind mapping has empty or duplicate reviewId")
    if {str(row.get("itemId", "")) for row in mapping_rows} != set(candidate_by_id):
        raise ValueError("blind mapping itemId coverage does not match candidates")

    review_rows = _load_review_extraction(review_extraction_path)
    review_ids = [str(row["reviewId"] or "").strip() for row in review_rows]
    if len(review_ids) != len(set(review_ids)):
        raise ValueError("project-review extraction has duplicate reviewId")
    if set(review_ids) != set(mapping_by_id):
        raise ValueError("project-review reviewId coverage does not match blind mapping")

    decisions_by_item_id: dict[str, dict[str, str]] = {}
    confidence_counts = {value: 0 for value in ALLOWED_CONFIDENCE}
    note_count = 0
    for row, review_id in zip(review_rows, review_ids, strict=True):
        mapped = mapping_by_id[review_id]
        item_id = str(mapped["itemId"])
        source = candidate_by_id[item_id]
        user_message = "" if row["userMessage"] is None else str(row["userMessage"])
        if user_message != source["userMessage"]:
            raise ValueError(f"userMessage was modified for {review_id}")
        if _sha256_text(user_message) != mapped.get("userMessageSha256"):
            raise ValueError(f"userMessage SHA-256 mismatch for {review_id}")

        label = "" if row["reviewerLabel"] is None else str(row["reviewerLabel"]).strip()
        if label in ("ambiguous", "invalid"):
            raise ValueError(f"unresolved reviewerLabel for {review_id}: {label}")
        if label not in FINAL_REVIEW_LABELS:
            raise ValueError(f"invalid reviewerLabel for {review_id}: {label or '<blank>'}")
        confidence = _normalized_confidence(
            row["reviewerConfidence"],
            confirm_numeric_confidence_09_as_high,
            review_id,
        )
        note = "" if row["reviewerNote"] is None else str(row["reviewerNote"]).strip()
        _validate_private_reviewer_fields(review_id, confidence, note)
        confidence_counts[confidence] += 1
        note_count += int(bool(note))
        decisions_by_item_id[item_id] = {
            "label": label,
            "confidence": confidence,
            "note": note,
        }

    greeting_design_count = int(candidates["designTarget"]["greetingCandidateCount"])
    design_labels = {
        str(row["itemId"]): "greeting" if index < greeting_design_count else "not_greeting"
        for index, row in enumerate(candidate_rows)
    }
    reviewed_items = []
    disagreement = 0
    greeting_to_not_greeting = 0
    not_greeting_to_greeting = 0
    for source in candidate_rows:
        item_id = str(source["itemId"])
        decision = decisions_by_item_id[item_id]
        design_label = design_labels[item_id]
        final_label = decision["label"]
        if final_label != design_label:
            disagreement += 1
            if design_label == "greeting":
                greeting_to_not_greeting += 1
            else:
                not_greeting_to_greeting += 1
        reviewed_items.append(
            {
                **source,
                "reviewStatus": REVIEW_STATUS,
                "reviewerLabel": final_label,
                "reviewerConfidence": decision["confidence"],
                "reviewerNote": decision["note"],
            }
        )

    reviewed_gold = {
        "schemaVersion": REVIEWED_GOLD_SCHEMA_VERSION,
        "datasetVersion": candidates["datasetVersion"],
        "reviewerRole": reviewer_role,
        "reviewedAtUtc": reviewed_at_utc,
        "fullyHumanReviewed": True,
        "sourceCandidateManifestSha256": _sha256_bytes(candidate_bytes),
        "items": reviewed_items,
    }
    reviewed_gold_bytes = (
        json.dumps(reviewed_gold, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    final_greeting_count = sum(
        row["reviewerLabel"] == "greeting" for row in reviewed_items
    )
    final_not_greeting_count = len(reviewed_items) - final_greeting_count
    manifest: dict[str, object] = {
        "reviewSchemaVersion": PROJECT_REVIEW_SCHEMA_VERSION,
        "datasetVersion": candidates["datasetVersion"],
        "rowCount": len(reviewed_items),
        "reviewerRole": reviewer_role,
        "reviewedAtUtc": reviewed_at_utc,
        "completedCount": len(reviewed_items),
        "disagreementCount": disagreement,
        "changedLabelCount": disagreement,
        "greetingToNotGreetingCount": greeting_to_not_greeting,
        "notGreetingToGreetingCount": not_greeting_to_greeting,
        "finalGreetingCount": final_greeting_count,
        "finalNotGreetingCount": final_not_greeting_count,
        "ambiguousCount": 0,
        "invalidCount": 0,
        "reviewerConfidenceCounts": {
            key: value for key, value in confidence_counts.items() if value
        },
        "reviewerNoteCount": note_count,
        "confidenceNormalization": {
            "method": "numeric_0.9_confirmed_as_high",
            "confirmedByRole": reviewer_role,
            "normalizedCount": confidence_counts["high"],
            "sourceArtifactPreserved": True,
        },
        "reviewFileSha256": _sha256_bytes(source_review_artifact_path.read_bytes()),
        "reviewExtractionSha256": _sha256_bytes(review_extraction_path.read_bytes()),
        "mappingSha256": _sha256_bytes(mapping_bytes),
        "originalCandidateManifestSha256": _sha256_bytes(candidate_bytes),
        "finalGoldSha256": _sha256_bytes(reviewed_gold_bytes),
        "reviewIdMappingVerified": True,
        "messagesVerified": True,
        "privacyScanPassed": True,
        "blindToRuleResultsAndMetrics": True,
        "automatedLabelingUsed": False,
        "fullyHumanReviewed": True,
    }

    _atomic_write(reviewed_gold_path, reviewed_gold_bytes)
    _atomic_write(
        review_manifest_path,
        (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode(
            "utf-8"
        ),
    )
    return manifest
