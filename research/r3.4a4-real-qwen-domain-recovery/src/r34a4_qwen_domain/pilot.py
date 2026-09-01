from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any
from urllib import request
from urllib.parse import urlparse


PRODUCTION_SYSTEM_ROLE = (
    "你是珍稀及受保护野生动物科普角色森森。只用应用提供的可信上下文组织简洁、自然、适合青少年的中文回答。"
    "不得补造科学事实、业务状态、长期记忆、聊天历史或动作权限；没有提供的事实必须明确说不知道。"
)

ALLOWED_CATEGORIES = ("greeting", "hard_negative", "product_negative")
ALLOWED_SPLITS = ("train", "dev", "test")
ALLOWED_AUTHORITIES = {
    "none": "",
    "canonical_knowledge": "CANONICAL EVIDENCE",
    "current_progress": "CURRENT READ-ONLY STATE",
    "character_memory": "PAST CHARACTER MEMORY",
    "system_policy": "SYSTEM POLICY",
}
RIGHTS_STATUS = "project_authored_prompt_local_qwen_output_local_only"
REVIEW_STATUS = "agent_reviewed_pilot_not_project_reviewed"


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def load_prompt_manifest(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ValueError("prompt manifest must be an object")
    return payload


def validate_prompt_manifest(manifest: dict[str, Any]) -> None:
    if manifest.get("schemaVersion") != "r3.4a4-pilot-prompts-v1":
        raise ValueError("unknown schemaVersion")
    if manifest.get("rightsStatus") != RIGHTS_STATUS:
        raise ValueError("rightsStatus is not approved")
    if manifest.get("reviewStatus") != REVIEW_STATUS:
        raise ValueError("reviewStatus is not approved")
    profile = manifest.get("generationProfile")
    if not isinstance(profile, dict) or profile.get("cloudAllowed") is not False:
        raise ValueError("generationProfile must fail closed against Cloud")

    rows = manifest.get("prompts")
    if not isinstance(rows, list):
        raise ValueError("prompts must be a list")
    expected_counts = {
        "greeting": 30,
        "hard_negative": 30,
        "product_negative": 20,
    }
    actual_counts = {
        category: sum(
            isinstance(row, dict) and row.get("category") == category for row in rows
        )
        for category in ALLOWED_CATEGORIES
    }
    if actual_counts != expected_counts or len(rows) != 80:
        raise ValueError(f"unexpected Pilot category counts: {actual_counts}")

    ids: set[str] = set()
    messages: set[str] = set()
    family_splits: dict[str, set[str]] = {}
    for index, row in enumerate(rows):
        if not isinstance(row, dict):
            raise ValueError(f"prompt row {index} must be an object")
        if "assistantReply" in row:
            raise ValueError("generated replies cannot enter the tracked prompt manifest")
        required = (
            "promptId",
            "userMessage",
            "label",
            "category",
            "scenarioFamily",
            "promptTemplate",
            "splitGroup",
            "split",
            "generationSeed",
            "contentAuthority",
            "answerMode",
            "authorityContext",
            "safetyCritical",
        )
        missing = [key for key in required if key not in row]
        if missing:
            raise ValueError(f"prompt row {index} is missing: {', '.join(missing)}")
        prompt_id = str(row["promptId"]).strip()
        user_message = str(row["userMessage"]).strip()
        if not prompt_id or prompt_id in ids:
            raise ValueError("promptId values must be non-empty and unique")
        if not user_message or user_message in messages:
            raise ValueError("userMessage values must be non-empty and unique")
        ids.add(prompt_id)
        messages.add(user_message)
        category = str(row["category"])
        expected_label = "Greeting" if category == "greeting" else "NotGreeting"
        if row["label"] != expected_label:
            raise ValueError("category and label disagree")
        split = str(row["split"])
        if split not in ALLOWED_SPLITS:
            raise ValueError("split must be assigned before generation")
        family = str(row["scenarioFamily"]).strip()
        template = str(row["promptTemplate"]).strip()
        split_group = str(row["splitGroup"]).strip()
        if not family or not template or not split_group:
            raise ValueError("scenario and split metadata must be non-empty")
        family_splits.setdefault(family, set()).add(split)
        if not isinstance(row["generationSeed"], int):
            raise ValueError("generationSeed must be an integer")
        authority = str(row["contentAuthority"])
        if authority not in ALLOWED_AUTHORITIES:
            raise ValueError("unknown contentAuthority")
        context = str(row["authorityContext"]).strip()
        if authority == "none" and context:
            raise ValueError("authorityContext must be empty for none")
        if authority != "none" and not context:
            raise ValueError("selected contentAuthority requires controlled context")
        if not isinstance(row["safetyCritical"], bool):
            raise ValueError("safetyCritical must be a boolean")

    leaked = sorted(family for family, splits in family_splits.items() if len(splits) > 1)
    if leaked:
        raise ValueError("scenario family leakage across splits: " + ", ".join(leaked))


def ensure_local_only_output(output: Path, project_root: Path) -> Path:
    resolved = output.expanduser().resolve()
    root = project_root.expanduser().resolve()
    if resolved == root or root in resolved.parents:
        raise ValueError("generated Qwen text must remain outside the repository")
    return resolved


def production_messages(
    user_message: str,
    content_authority: str,
    authority_context: str,
) -> list[dict[str, str]]:
    authority = str(content_authority)
    if authority not in ALLOWED_AUTHORITIES:
        raise ValueError("unknown contentAuthority")
    system = PRODUCTION_SYSTEM_ROLE
    label = ALLOWED_AUTHORITIES[authority]
    context = str(authority_context).strip()
    if label:
        if not context:
            raise ValueError("selected contentAuthority requires controlled context")
        system += f"\n\n<{label}>\n{context}\n</{label}>"
    elif context:
        raise ValueError("authorityContext must be empty for none")
    message = str(user_message).strip()
    if not message:
        raise ValueError("userMessage must not be empty")
    return [
        {"role": "system", "content": system},
        {"role": "user", "content": message},
    ]


def _loopback_endpoint(endpoint: str) -> str:
    parsed = urlparse(endpoint)
    if parsed.scheme != "http" or parsed.hostname not in {"127.0.0.1", "localhost"}:
        raise ValueError("Pilot generation is restricted to a loopback HTTP endpoint")
    if parsed.path.rstrip("/") != "/v1/chat/completions":
        raise ValueError("endpoint must target /v1/chat/completions")
    return endpoint


def _generate_reply(
    endpoint: str,
    model: str,
    row: dict[str, Any],
    profile: dict[str, Any],
    timeout_seconds: float,
) -> str:
    payload = {
        "model": model,
        "messages": production_messages(
            str(row["userMessage"]),
            str(row["contentAuthority"]),
            str(row["authorityContext"]),
        ),
        "temperature": float(profile["temperature"]),
        "top_p": float(profile["topP"]),
        "repeat_penalty": float(profile["repeatPenalty"]),
        "seed": int(row["generationSeed"]),
        "max_tokens": int(profile["maxTokens"]),
        "stream": False,
    }
    http_request = request.Request(
        _loopback_endpoint(endpoint),
        data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with request.urlopen(http_request, timeout=timeout_seconds) as response:
        result = json.loads(response.read().decode("utf-8"))
    try:
        reply = str(result["choices"][0]["message"]["content"]).strip()
    except (KeyError, IndexError, TypeError) as error:
        raise ValueError("llama.cpp response did not contain a completion") from error
    if not reply:
        raise ValueError("llama.cpp returned an empty completion")
    return reply


def generate_pilot(
    manifest_path: Path,
    output_path: Path,
    project_root: Path,
    endpoint: str,
    model: str,
    *,
    timeout_seconds: float = 120.0,
) -> dict[str, Any]:
    manifest = load_prompt_manifest(manifest_path)
    validate_prompt_manifest(manifest)
    output = ensure_local_only_output(output_path, project_root)
    output.parent.mkdir(parents=True, exist_ok=True)
    profile = manifest["generationProfile"]
    rows: list[dict[str, Any]] = []
    for index, prompt in enumerate(manifest["prompts"], start=1):
        reply = _generate_reply(endpoint, model, prompt, profile, timeout_seconds)
        rows.append(
            {
                "promptId": prompt["promptId"],
                "userMessage": prompt["userMessage"],
                "assistantReply": reply,
                "label": prompt["label"],
                "category": prompt["category"],
                "scenarioFamily": prompt["scenarioFamily"],
                "promptTemplate": prompt["promptTemplate"],
                "generationProfile": profile["profileId"],
                "generationSeed": prompt["generationSeed"],
                "sourceRuntime": "mac_reference_llama_cpp",
                "reviewStatus": manifest["reviewStatus"],
                "rightsStatus": manifest["rightsStatus"],
                "splitGroup": prompt["splitGroup"],
                "split": prompt["split"],
                "contentAuthority": prompt["contentAuthority"],
                "answerMode": prompt["answerMode"],
                "safetyCritical": prompt["safetyCritical"],
                "userSha256": sha256_text(str(prompt["userMessage"])),
                "replySha256": sha256_text(reply),
                "pairSha256": sha256_text(str(prompt["userMessage"]) + "\n" + reply),
            }
        )
        print(f"generated {index:02d}/{len(manifest['prompts'])}: {prompt['promptId']}", flush=True)
    with output.open("w", encoding="utf-8") as stream:
        for row in rows:
            stream.write(json.dumps(row, ensure_ascii=False, sort_keys=True) + "\n")
    return {
        "rowCount": len(rows),
        "outputSha256": hashlib.sha256(output.read_bytes()).hexdigest(),
        "modelSha256": profile["modelSha256"],
        "generationProfile": profile["profileId"],
    }


def load_generated_pilot(path: Path) -> list[dict[str, Any]]:
    rows = [
        json.loads(line)
        for line in path.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    if len(rows) != 80:
        raise ValueError("generated Pilot must contain exactly 80 rows")
    if len({row.get("promptId") for row in rows}) != 80:
        raise ValueError("generated Pilot promptId values must be unique")
    return rows
