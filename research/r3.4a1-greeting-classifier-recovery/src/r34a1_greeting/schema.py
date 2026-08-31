from __future__ import annotations

from dataclasses import asdict, dataclass


LABELS = ("NotGreeting", "Greeting")
APPROVED_SOURCE_PREFIX = "project_"
APPROVED_RIGHTS_STATUS = "project_controlled_no_third_party_text"
VALID_SPLITS = ("train", "dev", "test", "gold")


@dataclass(frozen=True)
class GreetingExample:
    source_id: str
    user: str
    reply: str
    label: str
    source_type: str
    generation_method: str
    scenario_family: str
    prompt_template: str
    generation_batch: str
    review_status: str
    rights_status: str
    split_group: str
    assigned_split: str
    safety_critical: bool

    def validate(self) -> None:
        required = {
            "id": self.source_id,
            "user": self.user,
            "reply": self.reply,
            "generationMethod": self.generation_method,
            "scenarioFamily": self.scenario_family,
            "promptTemplate": self.prompt_template,
            "generationBatch": self.generation_batch,
            "reviewStatus": self.review_status,
            "splitGroup": self.split_group,
        }
        missing = [name for name, value in required.items() if not value.strip()]
        if missing:
            raise ValueError(f"missing required fields: {', '.join(missing)}")
        if self.label not in LABELS:
            raise ValueError(f"unknown label: {self.label}")
        if not self.source_type.startswith(APPROVED_SOURCE_PREFIX):
            raise ValueError(f"unapproved sourceType: {self.source_type}")
        if self.rights_status != APPROVED_RIGHTS_STATUS:
            raise ValueError(f"unapproved rightsStatus: {self.rights_status}")
        if self.assigned_split not in VALID_SPLITS:
            raise ValueError(f"unknown split: {self.assigned_split}")

    def to_json(self) -> dict[str, object]:
        self.validate()
        return {
            "id": self.source_id,
            "user": self.user,
            "reply": self.reply,
            "label": self.label,
            "sourceType": self.source_type,
            "generationMethod": self.generation_method,
            "scenarioFamily": self.scenario_family,
            "promptTemplate": self.prompt_template,
            "generationBatch": self.generation_batch,
            "reviewStatus": self.review_status,
            "rightsStatus": self.rights_status,
            "splitGroup": self.split_group,
            "split": self.assigned_split,
            "safetyCritical": self.safety_critical,
        }

    @classmethod
    def from_json(cls, payload: dict[str, object]) -> "GreetingExample":
        row = cls(
            source_id=str(payload.get("id", "")),
            user=str(payload.get("user", "")),
            reply=str(payload.get("reply", "")),
            label=str(payload.get("label", "")),
            source_type=str(payload.get("sourceType", "")),
            generation_method=str(payload.get("generationMethod", "")),
            scenario_family=str(payload.get("scenarioFamily", "")),
            prompt_template=str(payload.get("promptTemplate", "")),
            generation_batch=str(payload.get("generationBatch", "")),
            review_status=str(payload.get("reviewStatus", "")),
            rights_status=str(payload.get("rightsStatus", "")),
            split_group=str(payload.get("splitGroup", "")),
            assigned_split=str(payload.get("split", "")),
            safety_critical=bool(payload.get("safetyCritical", False)),
        )
        row.validate()
        return row
