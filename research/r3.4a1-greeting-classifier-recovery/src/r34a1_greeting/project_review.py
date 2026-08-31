from __future__ import annotations

import csv
from dataclasses import replace
from datetime import datetime
import hashlib
import json
from pathlib import Path
import re

from .blind_review import CSV_COLUMNS, REVIEW_SCHEMA_VERSION
from .data import load_examples
from .schema import GreetingExample


PROJECT_REVIEW_SCHEMA_VERSION = "r3.4a2-gold-v2-project-review-v1"
PROJECT_REVIEW_DATASET_VERSION = "r3.4a2-greeting-gold-v2-project-reviewed-v1"
FINAL_REVIEW_LABELS = ("greeting", "not_greeting")
REVIEW_CONFIDENCES = ("", "high", "medium", "low")
REVIEW_STATUS = "project_member_reviewed"
_REVIEWER_ROLE_PATTERN = re.compile(r"project_(?:owner|member_[1-9][0-9]*)\Z")
_SHA256_PATTERN = re.compile(r"[0-9a-f]{64}\Z")
_PRIVATE_REVIEW_PATTERNS = (
    re.compile(r"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", re.IGNORECASE),
    re.compile(r"(?:/Users/|file://|[A-Z]:\\Users\\)", re.IGNORECASE),
    re.compile(r"\bBearer\s+[A-Za-z0-9._~+/=-]+", re.IGNORECASE),
    re.compile(r"\b(?:api[_ -]?key|authorization)\s*[:=]", re.IGNORECASE),
)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_text(value: str) -> str:
    return _sha256_bytes(value.encode("utf-8"))


def _serialize_examples(examples: list[GreetingExample]) -> bytes:
    lines = [json.dumps(row.to_json(), ensure_ascii=False, sort_keys=True) for row in examples]
    return ("\n".join(lines) + "\n").encode("utf-8")


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


def _validate_private_reviewer_fields(row: dict[str, str]) -> None:
    reviewer_text = "\n".join((row["reviewerConfidence"], row["reviewerNote"]))
    if "\0" in reviewer_text or any(pattern.search(reviewer_text) for pattern in _PRIVATE_REVIEW_PATTERNS):
        raise ValueError(f"private information found in reviewer fields for {row['reviewId']}")


def _load_completed_review(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != CSV_COLUMNS:
            raise ValueError("project-review CSV columns do not match the blind-review schema")
        rows = list(reader)
    for row in rows:
        row.update({key: (value or "").strip() for key, value in row.items()})
    return rows


def import_project_review(
    *,
    review_csv_path: Path,
    mapping_path: Path,
    gold_path: Path,
    history_path: Path,
    manifest_path: Path,
    reviewer_role: str,
    reviewed_at_utc: str,
    source_review_artifact_sha256: str,
) -> dict[str, object]:
    _validate_reviewer_role(reviewer_role)
    _validate_reviewed_at_utc(reviewed_at_utc)
    if not _SHA256_PATTERN.fullmatch(source_review_artifact_sha256):
        raise ValueError("source review artifact SHA-256 is invalid")

    review_csv_path = review_csv_path.resolve()
    mapping_path = mapping_path.resolve()
    gold_path = gold_path.resolve()
    history_path = history_path.resolve()
    manifest_path = manifest_path.resolve()

    original_gold_bytes = gold_path.read_bytes()
    mapping = json.loads(mapping_path.read_text(encoding="utf-8"))
    if mapping.get("reviewSchemaVersion") != REVIEW_SCHEMA_VERSION:
        raise ValueError("unknown blind-review schema")
    if _sha256_bytes(original_gold_bytes) != mapping.get("sourceGoldSha256"):
        raise ValueError("source Gold v2 SHA-256 does not match the blind mapping")

    mapping_rows = mapping.get("rows")
    if not isinstance(mapping_rows, list):
        raise ValueError("blind-review mapping rows are missing")
    mapping_by_id = {str(row.get("reviewId", "")): row for row in mapping_rows}
    if len(mapping_by_id) != len(mapping_rows) or "" in mapping_by_id:
        raise ValueError("blind-review mapping has duplicate or empty reviewId")

    review_rows = _load_completed_review(review_csv_path)
    if len(review_rows) != int(mapping.get("rowCount", -1)):
        raise ValueError("project-review row count does not match the blind mapping")
    review_ids = [row["reviewId"] for row in review_rows]
    if len(review_ids) != len(set(review_ids)):
        raise ValueError("project-review CSV has duplicate reviewId")
    if set(review_ids) != set(mapping_by_id):
        raise ValueError("project-review reviewId coverage does not match the blind mapping")

    source_rows = load_examples(gold_path)
    source_by_id = {row.source_id: row for row in source_rows}
    if len(source_by_id) != len(source_rows):
        raise ValueError("source Gold v2 has duplicate source IDs")
    if any(row.review_status != "agent_reviewed_pending_project_review" for row in source_rows):
        raise ValueError("source Gold v2 is not the pending project-review snapshot")
    if {str(row.get("sourceId", "")) for row in mapping_rows} != set(source_by_id):
        raise ValueError("source Gold v2 coverage does not match the blind mapping")
    mapping_by_source_index = {
        int(row.get("sourceIndex", -1)): row for row in mapping_rows
    }
    if set(mapping_by_source_index) != set(range(len(source_rows))):
        raise ValueError("blind-review sourceIndex coverage is invalid")

    decisions_by_source_id: dict[str, str] = {}
    confidence_counts = {value: 0 for value in REVIEW_CONFIDENCES}
    note_count = 0
    for review_row in review_rows:
        review_id = review_row["reviewId"]
        expected = mapping_by_id[review_id]
        if _sha256_text(review_row["userMessage"]) != expected.get("userMessageSha256"):
            raise ValueError(f"userMessage was modified for {review_id}")
        if _sha256_text(review_row["assistantReply"]) != expected.get("assistantReplySha256"):
            raise ValueError(f"assistantReply was modified for {review_id}")

        label = review_row["reviewerLabel"]
        if label in ("ambiguous", "invalid"):
            raise ValueError(f"unresolved reviewerLabel for {review_id}: {label}")
        if label not in FINAL_REVIEW_LABELS:
            raise ValueError(f"invalid reviewerLabel for {review_id}: {label or '<blank>'}")
        confidence = review_row["reviewerConfidence"]
        if confidence not in REVIEW_CONFIDENCES:
            raise ValueError(f"invalid reviewerConfidence for {review_id}: {confidence}")
        _validate_private_reviewer_fields(review_row)
        confidence_counts[confidence] += 1
        note_count += int(bool(review_row["reviewerNote"]))
        decisions_by_source_id[str(expected["sourceId"])] = label

    reviewed_rows: list[GreetingExample] = []
    changed = 0
    greeting_to_not_greeting = 0
    not_greeting_to_greeting = 0
    for source_index, source in enumerate(source_rows):
        expected_mapping = mapping_by_source_index[source_index]
        if int(expected_mapping.get("sourceIndex", -1)) != source_index:
            raise ValueError("blind-review sourceIndex mapping is not stable")
        if str(expected_mapping.get("sourceId", "")) != source.source_id:
            raise ValueError("blind-review sourceId mapping is not stable")
        if _sha256_text(source.user) != expected_mapping.get("userMessageSha256"):
            raise ValueError(f"source userMessage mismatch for {source.source_id}")
        if _sha256_text(source.reply) != expected_mapping.get("assistantReplySha256"):
            raise ValueError(f"source assistantReply mismatch for {source.source_id}")

        final_label = (
            "Greeting"
            if decisions_by_source_id[source.source_id] == "greeting"
            else "NotGreeting"
        )
        if source.label != final_label:
            changed += 1
            if source.label == "Greeting":
                greeting_to_not_greeting += 1
            else:
                not_greeting_to_greeting += 1
        reviewed_rows.append(replace(source, label=final_label, review_status=REVIEW_STATUS))

    final_gold_bytes = _serialize_examples(reviewed_rows)
    final_greeting_count = sum(row.label == "Greeting" for row in reviewed_rows)
    final_not_greeting_count = len(reviewed_rows) - final_greeting_count
    manifest: dict[str, object] = {
        "reviewSchemaVersion": PROJECT_REVIEW_SCHEMA_VERSION,
        "datasetVersion": PROJECT_REVIEW_DATASET_VERSION,
        "rowCount": len(review_rows),
        "reviewerRole": reviewer_role,
        "reviewedAtUtc": reviewed_at_utc,
        "completedCount": len(review_rows),
        "disagreementCount": changed,
        "changedLabelCount": changed,
        "greetingToNotGreetingCount": greeting_to_not_greeting,
        "notGreetingToGreetingCount": not_greeting_to_greeting,
        "finalGreetingCount": final_greeting_count,
        "finalNotGreetingCount": final_not_greeting_count,
        "ambiguousResolvedCount": 0,
        "ambiguousCount": 0,
        "invalidCount": 0,
        "reviewerConfidenceCounts": {
            key or "blank": value for key, value in confidence_counts.items() if value
        },
        "reviewerNoteCount": note_count,
        "reviewFileSha256": _sha256_bytes(review_csv_path.read_bytes()),
        "sourceReviewArtifactSha256": source_review_artifact_sha256,
        "originalGoldSha256": _sha256_bytes(original_gold_bytes),
        "finalGoldSha256": _sha256_bytes(final_gold_bytes),
        "reviewIdMappingVerified": True,
        "messagesVerified": True,
        "privacyScanPassed": True,
        "blindToOriginalLabelsAndModelOutputs": True,
        "automatedLabelingUsed": False,
        "labelsIdenticalToAgentBaseline": changed == 0,
        "fullyHumanReviewed": True,
    }

    if history_path.exists() and history_path.read_bytes() != original_gold_bytes:
        raise ValueError("existing agent-reviewed Gold history does not match the source")
    if not history_path.exists():
        _atomic_write(history_path, original_gold_bytes)
    _atomic_write(gold_path, final_gold_bytes)
    _atomic_write(
        manifest_path,
        (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8"),
    )
    return manifest
