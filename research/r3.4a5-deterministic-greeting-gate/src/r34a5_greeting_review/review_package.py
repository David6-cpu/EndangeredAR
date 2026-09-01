from __future__ import annotations

import csv
import hashlib
import json
from pathlib import Path
import unicodedata


SCHEMA_VERSION = "r3.4a5-deterministic-greeting-gold-v1-review-v1"
DATASET_VERSION = "r3.4a5-deterministic-greeting-gold-v1"
RANDOMIZATION_SEED = 340501
ALLOWED_LABELS = ("greeting", "not_greeting", "ambiguous", "invalid")
ALLOWED_CONFIDENCE = ("high", "medium", "low")
CSV_COLUMNS = (
    "reviewId",
    "userMessage",
    "reviewerLabel",
    "reviewerConfidence",
    "reviewerNote",
)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_text(value: str) -> str:
    return _sha256_bytes(value.encode("utf-8"))


def normalize_for_deduplication(value: str) -> str:
    normalized = unicodedata.normalize("NFKC", value).strip().lower()
    return "".join(
        character
        for character in normalized
        if not character.isspace()
        and not unicodedata.category(character).startswith("P")
    )


def load_candidate_manifest(path: Path) -> dict[str, object]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ValueError("candidate manifest must be a JSON object")
    return payload


def _load_prior_messages(pilot_path: Path, gold_v2_path: Path) -> set[str]:
    pilot = json.loads(pilot_path.read_text(encoding="utf-8"))
    prior = {
        normalize_for_deduplication(str(row["userMessage"]))
        for row in pilot["prompts"]
    }
    for line in gold_v2_path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            prior.add(normalize_for_deduplication(str(json.loads(line)["user"])))
    return prior


def validate_candidate_manifest(
    manifest: dict[str, object],
    pilot_path: Path,
    gold_v2_path: Path,
) -> None:
    if manifest.get("schemaVersion") != SCHEMA_VERSION:
        raise ValueError("unexpected candidate manifest schema")
    if manifest.get("datasetVersion") != DATASET_VERSION:
        raise ValueError("unexpected candidate dataset version")
    if manifest.get("fullyHumanReviewed") is not False:
        raise ValueError("unreviewed candidate manifest must remain fullyHumanReviewed=false")

    rows = manifest.get("items")
    if not isinstance(rows, list) or len(rows) != 150:
        raise ValueError("candidate manifest must contain exactly 150 rows")
    target = manifest.get("designTarget")
    if not isinstance(target, dict):
        raise ValueError("candidate design target is missing")
    if target.get("greetingCandidateCount") != 50:
        raise ValueError("candidate design target must include 50 greeting candidates")
    if target.get("notGreetingCandidateCount") != 100:
        raise ValueError("candidate design target must include 100 non-greeting candidates")

    required = {
        "itemId",
        "userMessage",
        "scenarioFamily",
        "safetyCritical",
        "sourceType",
        "reviewStatus",
        "rightsStatus",
        "splitGroup",
    }
    forbidden = {
        "assistantReply",
        "reply",
        "expectedLabel",
        "label",
        "ruleResult",
        "reasonCode",
        "prediction",
        "confidence",
        "margin",
    }
    ids: list[str] = []
    normalized_messages: list[str] = []
    safety_count = 0
    for row in rows:
        if not isinstance(row, dict) or not required.issubset(row):
            raise ValueError("candidate row does not match the required schema")
        if forbidden & set(row):
            raise ValueError("candidate row contains review-hidden result data")
        ids.append(str(row["itemId"]))
        normalized = normalize_for_deduplication(str(row["userMessage"]))
        if not normalized:
            raise ValueError("candidate userMessage is empty after normalization")
        normalized_messages.append(normalized)
        safety_count += int(row["safetyCritical"] is True)
        if row["sourceType"] != "project_authored_non_private":
            raise ValueError("candidate source is outside the approved rights boundary")
        if row["rightsStatus"] != "project_controlled_no_third_party_text":
            raise ValueError("candidate rights status is not approved")
        if row["reviewStatus"] != "pending_project_member_blind_review":
            raise ValueError("candidate review status is not pending blind review")

    if len(ids) != len(set(ids)):
        raise ValueError("candidate manifest contains duplicate itemId")
    if len(normalized_messages) != len(set(normalized_messages)):
        raise ValueError("candidate manifest contains normalized duplicate messages")
    if safety_count < 60:
        raise ValueError("candidate manifest has fewer than 60 safety-critical negatives")
    prior = _load_prior_messages(pilot_path, gold_v2_path)
    overlap = sorted(set(normalized_messages) & prior)
    if overlap:
        raise ValueError(f"candidate manifest overlaps prior evidence: {overlap[:3]}")


def _is_within(path: Path, directory: Path) -> bool:
    try:
        path.relative_to(directory)
    except ValueError:
        return False
    return True


def _stable_digest(domain: str, item_id: str, message: str) -> str:
    payload = "\0".join(
        (domain, str(RANDOMIZATION_SEED), DATASET_VERSION, item_id, message)
    )
    return _sha256_text(payload)


def _review_instructions(row_count: int) -> str:
    return f"""# Deterministic Greeting Gate Gold v1 项目成员盲审说明

## 审核问题

请只判断 **用户消息本身** 是否构成直接、自然的问候意图。本次不是判断回复、情绪、模型预测或最终 Wave 产品资格。

- `greeting`：用户正在直接向森森问候、初次见面问候或重逢问候。
- `not_greeting`：不是直接问候，包括定义、解释、否定、引用、转述、技术语境、注入、科学事实、任务、Memory、普通情绪交流和告别。
- `ambiguous`：是否属于直接问候无法稳定判断，后续必须由真人二次裁决。
- `invalid`：文本损坏、含义不完整或不适合作为验收样本，后续必须由真人二次裁决。

`reviewerConfidence` 可留空，或填写 `high`、`medium`、`low`。`reviewerNote` 可留空；对 `ambiguous` 或 `invalid` 请尽量注明原因。

## 审核边界

1. 必须由真人项目成员审核全部 {row_count} 条，不能让 AI、模型或自动代理代填。
2. 不要猜规则、测试代码或系统会怎样分类。
3. 不要因句子积极、开心或友好就自动选 `greeting`。
4. “你好”的定义、引用或否定不是直接问候。
5. 只填写 `reviewerLabel`、`reviewerConfidence` 和 `reviewerNote`。
6. 不要修改 `reviewId`，不要修改 userMessage，不要删除或重新排序记录。
7. 请审核全部 {row_count} 条；不确定时选 `ambiguous`，不要勉强二选一。
8. 完成后另存为新的本地 CSV，并把该文件路径提供给 Codex。

审核者无需填写真实姓名、邮箱或其他身份信息。后续只记录 `project_owner` 或 `project_member_1` 等非敏感角色。
"""


def build_review_package(
    candidates_path: Path,
    output_directory: Path,
    repository_root: Path,
    pilot_path: Path,
    gold_v2_path: Path,
) -> dict[str, Path | int]:
    candidates_path = candidates_path.resolve()
    output_directory = output_directory.resolve()
    repository_root = repository_root.resolve()
    if _is_within(output_directory, repository_root):
        raise ValueError("blind-review output must remain outside the repository")

    manifest = load_candidate_manifest(candidates_path)
    validate_candidate_manifest(manifest, pilot_path, gold_v2_path)
    source_rows = list(manifest["items"])
    ordered_rows = sorted(
        source_rows,
        key=lambda row: _stable_digest(
            "review-order", str(row["itemId"]), str(row["userMessage"])
        ),
    )

    output_directory.mkdir(parents=True, exist_ok=True)
    review_csv = output_directory / "gold-v1-blind-review.csv"
    instructions = output_directory / "gold-v1-review-instructions.md"
    mapping_json = output_directory / ".gold-v1-blind-review.internal-map.json"
    package_manifest = output_directory / "gold-v1-review-package-manifest.json"
    for path in (review_csv, instructions, mapping_json, package_manifest):
        if path.exists():
            raise FileExistsError(f"refusing to overwrite existing review package file: {path.name}")

    review_rows: list[dict[str, str]] = []
    mapping_rows: list[dict[str, str]] = []
    review_ids: set[str] = set()
    for row in ordered_rows:
        item_id = str(row["itemId"])
        user_message = str(row["userMessage"])
        review_id = "DG1-" + _stable_digest("review-id", item_id, user_message)[:16].upper()
        if review_id in review_ids:
            raise ValueError("stable blind reviewId collision")
        review_ids.add(review_id)
        review_rows.append(
            {
                "reviewId": review_id,
                "userMessage": user_message,
                "reviewerLabel": "",
                "reviewerConfidence": "",
                "reviewerNote": "",
            }
        )
        mapping_rows.append(
            {
                "reviewId": review_id,
                "itemId": item_id,
                "userMessageSha256": _sha256_text(user_message),
            }
        )

    with review_csv.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=CSV_COLUMNS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(review_rows)
    instructions.write_text(_review_instructions(len(review_rows)), encoding="utf-8")

    mapping = {
        "reviewSchemaVersion": SCHEMA_VERSION,
        "datasetVersion": DATASET_VERSION,
        "rowCount": len(mapping_rows),
        "randomization": {
            "method": "sha256_seeded_sort",
            "seed": RANDOMIZATION_SEED,
        },
        "sourceManifestSha256": _sha256_bytes(candidates_path.read_bytes()),
        "items": mapping_rows,
    }
    mapping_json.write_text(
        json.dumps(mapping, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )

    public_manifest = {
        "reviewSchemaVersion": SCHEMA_VERSION,
        "datasetVersion": DATASET_VERSION,
        "rowCount": len(review_rows),
        "fullyHumanReviewed": False,
        "reviewerFieldsInitiallyBlank": True,
        "randomizationMethod": "sha256_seeded_sort",
        "randomizationSeed": RANDOMIZATION_SEED,
        "sourceManifestSha256": _sha256_bytes(candidates_path.read_bytes()),
        "reviewCsvSha256": _sha256_bytes(review_csv.read_bytes()),
        "instructionsSha256": _sha256_bytes(instructions.read_bytes()),
    }
    package_manifest.write_text(
        json.dumps(public_manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    verify_review_package(review_csv, mapping_json, candidates_path)
    return {
        "reviewCsv": review_csv,
        "instructionsMarkdown": instructions,
        "mappingJson": mapping_json,
        "packageManifestJson": package_manifest,
        "rowCount": len(review_rows),
    }


def verify_review_package(
    review_csv: Path,
    mapping_json: Path,
    candidates_path: Path,
) -> dict[str, object]:
    mapping = json.loads(mapping_json.read_text(encoding="utf-8"))
    source = load_candidate_manifest(candidates_path)
    source_by_id = {str(row["itemId"]): row for row in source["items"]}
    mapping_rows = mapping.get("items")
    if not isinstance(mapping_rows, list) or len(mapping_rows) != len(source_by_id):
        raise ValueError("blind mapping does not cover every candidate")
    mapping_by_review_id = {str(row.get("reviewId", "")): row for row in mapping_rows}
    if "" in mapping_by_review_id or len(mapping_by_review_id) != len(mapping_rows):
        raise ValueError("blind mapping has empty or duplicate reviewId")

    with review_csv.open("r", encoding="utf-8-sig", newline="") as stream:
        reader = csv.DictReader(stream)
        if tuple(reader.fieldnames or ()) != CSV_COLUMNS:
            raise ValueError("blind-review CSV columns do not match the schema")
        review_rows = list(reader)
    if len(review_rows) != len(source_by_id):
        raise ValueError("blind-review CSV row count does not match candidates")
    review_ids = [row["reviewId"] for row in review_rows]
    if len(review_ids) != len(set(review_ids)):
        raise ValueError("blind-review CSV contains duplicate reviewId")
    if set(review_ids) != set(mapping_by_review_id):
        raise ValueError("blind-review CSV coverage does not match mapping")

    mapped_item_ids: set[str] = set()
    for row in review_rows:
        mapped = mapping_by_review_id[row["reviewId"]]
        item_id = str(mapped["itemId"])
        mapped_item_ids.add(item_id)
        source_row = source_by_id.get(item_id)
        if source_row is None:
            raise ValueError("blind mapping references an unknown candidate")
        message = row["userMessage"]
        if message != source_row["userMessage"]:
            raise ValueError(f"userMessage mismatch for {row['reviewId']}")
        if _sha256_text(message) != mapped["userMessageSha256"]:
            raise ValueError(f"userMessage hash mismatch for {row['reviewId']}")
        if row["reviewerLabel"] or row["reviewerConfidence"] or row["reviewerNote"]:
            raise ValueError("new blind-review package must have empty reviewer fields")
    if mapped_item_ids != set(source_by_id):
        raise ValueError("blind mapping does not cover every source itemId")
    if mapping.get("sourceManifestSha256") != _sha256_bytes(candidates_path.read_bytes()):
        raise ValueError("candidate manifest SHA-256 does not match blind mapping")
    return {
        "rowCount": len(review_rows),
        "uniqueReviewIdCount": len(set(review_ids)),
        "mappingCount": len(mapping_rows),
        "reviewFieldsBlank": True,
        "sourceManifestVerified": True,
    }
