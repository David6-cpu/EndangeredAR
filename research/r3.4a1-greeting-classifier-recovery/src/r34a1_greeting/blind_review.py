from __future__ import annotations

import csv
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path

from .data import load_examples
from .schema import GreetingExample


REVIEW_SCHEMA_VERSION = "r3.4a2-gold-v2-blind-review-v1"
DATASET_VERSION = "r3.4a1-greeting-data-v1-gold-v2"
RANDOMIZATION_SEED = 34201
CSV_NAME = "gold-v2-blind-review.csv"
INSTRUCTIONS_NAME = "gold-v2-review-instructions.md"
MAPPING_NAME = ".gold-v2-blind-review.internal-map.json"
CSV_COLUMNS = (
    "reviewId",
    "userMessage",
    "assistantReply",
    "reviewerLabel",
    "reviewerConfidence",
    "reviewerNote",
)
REVIEWER_LABELS = ("greeting", "not_greeting", "ambiguous", "invalid")
REVIEWER_CONFIDENCES = ("high", "medium", "low")


@dataclass(frozen=True)
class BlindReviewPackage:
    csv_path: Path
    instructions_path: Path
    mapping_path: Path
    row_count: int
    randomization_seed: int


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_text(value: str) -> str:
    return _sha256_bytes(value.encode("utf-8"))


def _stable_digest(domain: str, row: GreetingExample) -> str:
    payload = "\0".join(
        (
            domain,
            str(RANDOMIZATION_SEED),
            DATASET_VERSION,
            row.source_id,
            row.user,
            row.reply,
        )
    )
    return _sha256_text(payload)


def _is_within(path: Path, directory: Path) -> bool:
    try:
        path.relative_to(directory)
    except ValueError:
        return False
    return True


def _validate_source_rows(rows: list[GreetingExample], expected_row_count: int | None) -> None:
    if expected_row_count is not None and len(rows) != expected_row_count:
        raise ValueError(
            f"Gold v2 row count mismatch: expected {expected_row_count}, found {len(rows)}"
        )
    if not rows:
        raise ValueError("Gold v2 is empty")
    source_ids = [row.source_id for row in rows]
    if len(source_ids) != len(set(source_ids)):
        raise ValueError("Gold v2 contains duplicate source IDs")
    pairs = [(row.user, row.reply) for row in rows]
    if len(pairs) != len(set(pairs)):
        raise ValueError("Gold v2 contains duplicate User/Reply pairs")
    for row in rows:
        if row.assigned_split != "gold":
            raise ValueError(f"non-Gold row found: {row.source_id}")
        if row.review_status != "agent_reviewed_pending_project_review":
            raise ValueError(f"unexpected review status: {row.source_id}")


def _instructions(row_count: int) -> str:
    return f"""# Gold v2 项目成员盲审说明

## 审核目标

本次判断的是 **Wave eligibility**，不是关键词分类，也不是情绪分类。
审核单位是 **user message and assistant reply together**：必须把用户消息和森森最终回复作为一个完整互动判断，不能只看其中一侧。

请不要猜测规则或模型会怎样预测，也不要根据积极、开心、感谢或安慰语气自动判断为问候。

## reviewerLabel 允许值

- `greeting`：双方构成直接、自然的问候或重逢互动；此时播放一次 Wave 在语义上自然且不会误导用户；不是引用、解释、否定或技术讨论。
- `not_greeting`：不适合播放 Wave 的互动，包括科学事实、当前任务、Memory Recall、History Boundary、Diet/Eat、Taunt、普通情绪交流、问候词定义或引用、否定问候、禁止挥手、Wave/Animator 技术文本、Prompt injection、`system_status` 和 off-domain。
- `ambiguous`：上下文不足，或 Greeting 与普通社交之间无法稳定判断。不要勉强二选一；该记录后续必须由真人再次裁决。
- `invalid`：文本损坏、用户与回复明显不对应、模板有误，或该记录不适合进入 Gold。该记录后续必须由真人再次裁决。

## reviewerConfidence 允许值

可留空，或填写 `high`、`medium`、`low`。`reviewerNote` 可留空；对 `ambiguous` 或 `invalid` 请尽量填写简短原因。

## 判断示例

- “你好是什么意思”不是 Greeting。
- “不要问好”不是 Greeting。
- “他说你好”不是 Greeting。
- 普通安慰、感谢和开心不自动等于 Greeting。
- 不确定时选择 `ambiguous`，不要为了完成而勉强二选一。

## 操作要求

1. Review all rows：需要审核全部 {row_count} 条记录。
2. 只填写 `reviewerLabel`、`reviewerConfidence` 和 `reviewerNote`。
3. Do not edit userMessage or assistantReply。
4. 不要修改 `reviewId`，不要重新排序或删除记录。
5. 审核完成后另存为新的本地 CSV，不要覆盖原始 Gold v2。
6. 将完成后的本地文件路径提供给 Codex。
7. 不需要填写真实姓名、邮箱或身份信息；审核者角色以后只使用 `project_owner` 或 `project_member_1` 等非敏感标识。

内部映射文件不属于审核材料，不要打开或交给审核者。审核者只需要 CSV 和本说明。
"""


def _write_csv(path: Path, rows: list[tuple[str, GreetingExample]]) -> None:
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=CSV_COLUMNS, lineterminator="\n")
        writer.writeheader()
        for review_id, row in rows:
            writer.writerow(
                {
                    "reviewId": review_id,
                    "userMessage": row.user,
                    "assistantReply": row.reply,
                    "reviewerLabel": "",
                    "reviewerConfidence": "",
                    "reviewerNote": "",
                }
            )


def build_blind_review_package(
    *,
    gold_path: Path,
    output_directory: Path,
    repository_root: Path,
    expected_row_count: int | None = None,
) -> BlindReviewPackage:
    gold_path = gold_path.resolve()
    output_directory = output_directory.resolve()
    repository_root = repository_root.resolve()
    if _is_within(output_directory, repository_root):
        raise ValueError("blind-review output must remain outside the repository")

    rows = load_examples(gold_path)
    _validate_source_rows(rows, expected_row_count)
    indexed_rows = list(enumerate(rows))
    indexed_rows.sort(key=lambda item: _stable_digest("review-order", item[1]))

    review_rows: list[tuple[str, GreetingExample]] = []
    mapping_rows: list[dict[str, object]] = []
    seen_review_ids: set[str] = set()
    for source_index, row in indexed_rows:
        review_id = f"r34a2-{_stable_digest('review-id', row)[:16]}"
        if review_id in seen_review_ids:
            raise ValueError("blind review ID collision")
        seen_review_ids.add(review_id)
        review_rows.append((review_id, row))
        mapping_rows.append(
            {
                "reviewId": review_id,
                "sourceId": row.source_id,
                "sourceIndex": source_index,
                "userMessageSha256": _sha256_text(row.user),
                "assistantReplySha256": _sha256_text(row.reply),
            }
        )

    output_directory.mkdir(parents=True, exist_ok=False)
    csv_path = output_directory / CSV_NAME
    instructions_path = output_directory / INSTRUCTIONS_NAME
    mapping_path = output_directory / MAPPING_NAME
    _write_csv(csv_path, review_rows)
    instructions_path.write_text(_instructions(len(rows)), encoding="utf-8")

    mapping = {
        "reviewSchemaVersion": REVIEW_SCHEMA_VERSION,
        "datasetVersion": DATASET_VERSION,
        "rowCount": len(rows),
        "randomization": {
            "method": "sha256_seeded_sort",
            "seed": RANDOMIZATION_SEED,
        },
        "sourceGoldSha256": _sha256_bytes(gold_path.read_bytes()),
        "reviewCsvSha256": _sha256_bytes(csv_path.read_bytes()),
        "instructionsFile": INSTRUCTIONS_NAME,
        "instructionsSha256": _sha256_bytes(instructions_path.read_bytes()),
        "rows": mapping_rows,
    }
    mapping_path.write_text(
        json.dumps(mapping, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    verify_blind_review_package(csv_path, mapping_path, gold_path=gold_path)
    return BlindReviewPackage(
        csv_path=csv_path,
        instructions_path=instructions_path,
        mapping_path=mapping_path,
        row_count=len(rows),
        randomization_seed=RANDOMIZATION_SEED,
    )


def verify_blind_review_package(
    csv_path: Path,
    mapping_path: Path,
    *,
    gold_path: Path | None = None,
) -> dict[str, object]:
    mapping = json.loads(mapping_path.read_text(encoding="utf-8"))
    if mapping.get("reviewSchemaVersion") != REVIEW_SCHEMA_VERSION:
        raise ValueError("unknown blind-review schema")
    mapping_rows = mapping.get("rows")
    if not isinstance(mapping_rows, list):
        raise ValueError("blind-review mapping rows are missing")
    mapping_by_id = {str(row.get("reviewId", "")): row for row in mapping_rows}
    if len(mapping_by_id) != len(mapping_rows) or "" in mapping_by_id:
        raise ValueError("blind-review mapping has duplicate or empty reviewId")

    with csv_path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if tuple(reader.fieldnames or ()) != CSV_COLUMNS:
            raise ValueError("blind-review CSV columns do not match the schema")
        csv_rows = list(reader)
    if len(csv_rows) != int(mapping.get("rowCount", -1)):
        raise ValueError("blind-review CSV row count does not match the mapping")
    csv_ids = [row["reviewId"] for row in csv_rows]
    if len(csv_ids) != len(set(csv_ids)):
        raise ValueError("blind-review CSV has duplicate reviewId")
    if set(csv_ids) != set(mapping_by_id):
        raise ValueError("blind-review CSV reviewId coverage does not match the mapping")

    for row in csv_rows:
        expected = mapping_by_id[row["reviewId"]]
        if _sha256_text(row["userMessage"]) != expected.get("userMessageSha256"):
            raise ValueError(f"userMessage was modified for {row['reviewId']}")
        if _sha256_text(row["assistantReply"]) != expected.get("assistantReplySha256"):
            raise ValueError(f"assistantReply was modified for {row['reviewId']}")
        if row["reviewerLabel"] or row["reviewerConfidence"] or row["reviewerNote"]:
            raise ValueError("new blind-review package must have empty reviewer fields")

    if gold_path is not None:
        gold_path = gold_path.resolve()
        if _sha256_bytes(gold_path.read_bytes()) != mapping.get("sourceGoldSha256"):
            raise ValueError("source Gold v2 SHA-256 does not match the mapping")
        source_rows = load_examples(gold_path)
        source_by_id = {row.source_id: row for row in source_rows}
        mapped_source_ids = {str(row.get("sourceId", "")) for row in mapping_rows}
        if mapped_source_ids != set(source_by_id):
            raise ValueError("source Gold v2 coverage does not match the mapping")
        for row in mapping_rows:
            source = source_by_id[str(row["sourceId"])]
            if _sha256_text(source.user) != row.get("userMessageSha256"):
                raise ValueError(f"source userMessage mismatch for {row['reviewId']}")
            if _sha256_text(source.reply) != row.get("assistantReplySha256"):
                raise ValueError(f"source assistantReply mismatch for {row['reviewId']}")

    if _sha256_bytes(csv_path.read_bytes()) != mapping.get("reviewCsvSha256"):
        raise ValueError("blind-review CSV SHA-256 does not match the mapping")
    instructions_path = mapping_path.parent / str(mapping.get("instructionsFile", ""))
    if not instructions_path.is_file():
        raise ValueError("blind-review instructions are missing")
    if _sha256_bytes(instructions_path.read_bytes()) != mapping.get("instructionsSha256"):
        raise ValueError("blind-review instructions SHA-256 does not match the mapping")

    return {
        "rowCount": len(csv_rows),
        "uniqueReviewIdCount": len(set(csv_ids)),
        "mappingCount": len(mapping_rows),
        "reviewFieldsBlank": True,
        "sourceGoldVerified": gold_path is not None,
    }
