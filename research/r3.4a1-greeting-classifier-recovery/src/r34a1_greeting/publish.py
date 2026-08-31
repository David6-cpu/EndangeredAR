from __future__ import annotations

import argparse
import json
from pathlib import Path


def _write_json(path: Path, payload: object) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def _format(value: object) -> str:
    return f"{float(value):.4f}"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", type=Path, required=True)
    parser.add_argument("--reports", type=Path, required=True)
    args = parser.parse_args()
    args.reports.mkdir(parents=True, exist_ok=True)
    summary = json.loads((args.run / "metrics-summary.json").read_text(encoding="utf-8"))
    manifest = json.loads((args.run / "run-manifest.json").read_text(encoding="utf-8"))
    _write_json(args.reports / "metrics-summary.json", summary)
    _write_json(args.reports / "reproducibility-manifest.json", manifest)

    lines = [
        "# R3.4A.1 Model Comparison",
        "",
        "All thresholds and candidate selection use Dev only. Test is evaluated",
        "after selection logic is fixed; Gold v2 is read after the candidate has",
        "been locked and does not alter model or input selection.",
        "",
        "## Rule baseline",
        "",
        "| Split | Precision | Recall | F0.5 | Accepted | Safety-critical FP |",
        "| --- | ---: | ---: | ---: | ---: | ---: |",
    ]
    for split in ("dev", "test", "gold"):
        row = summary["ruleOnly"][split]
        lines.append(
            f"| {split} | {_format(row['precision'])} | {_format(row['recall'])} | "
            f"{_format(row['f0.5'])} | {row['accepted']} | "
            f"{row['safetyCriticalFalsePositive']} |"
        )

    lines.extend(
        [
            "",
            "## Learned candidates",
            "",
            "The table reports the required deterministic rule AND learned gate.",
            "",
            "| Candidate | Test P/R | Gold P/R | Test hard FP | Gold hard FP | Dev confidence/margin | Parameters | Artifact bytes | Mac mean ms |",
            "| --- | --- | --- | ---: | ---: | --- | ---: | ---: | ---: |",
        ]
    )
    for key, run in summary["runs"].items():
        test = run["ruleAndLearnedGate"]["test"]
        gold = summary["goldPostSelectionDiagnostics"][key]["ruleAndLearnedGate"]
        thresholds = run["ruleAndLearnedGate"]["thresholds"]
        size = run.get("artifactBytes", run.get("checkpointBytes", 0))
        lines.append(
            f"| {key} | {_format(test['precision'])}/{_format(test['recall'])} | "
            f"{_format(gold['precision'])}/{_format(gold['recall'])} | "
            f"{run['ruleAndLearnedGate']['testSafetyCriticalFalsePositive']} | "
            f"{gold['safetyCriticalFalsePositive']} | "
            f"{_format(thresholds['confidence'])}/{_format(thresholds['margin'])} | "
            f"{run['parameterCount']} | {size} | "
            f"{_format(run['macBatchOneLatency']['meanMs'])} |"
        )

    lines.extend(
        [
            "",
            "## Effective independent inputs",
            "",
            "User-only and Reply-only interaction counts contain repeated inputs",
            "because one fixed message may pair with multiple assistant replies.",
            "They are therefore reported but are not eligible to satisfy the Gold",
            "v2 size gate. Pair has 110 unique positive and 210 unique safety-critical",
            "negative Gold inputs.",
            "",
            "## Selection and gate",
            "",
            f"- Dev-selected candidate: `{summary['selectedBeforeGold']}`.",
            f"- Numerical Test and Gold thresholds passed: `{str(summary['qualityGate']['numericalMetricsPassed']).lower()}`.",
            f"- Selected input data adequacy passed: `{str(summary['qualityGate']['selectedInputDataAdequate']).lower()}`.",
            f"- Gold fully human-reviewed: `{str(summary['qualityGate']['goldFullyHumanReviewed']).lower()}`.",
            f"- Product quality gate passed: `{str(summary['qualityGate']['productQualityGatePassed']).lower()}`.",
            "- BiLSTM was not retrained because Linear and TextCNN already reached the",
            "  numerical target and no evidence justified reopening the heavier model.",
            "- ONNX, Unity, and signed iPhone work remain blocked by the product",
            "  quality gate and were not started in this recovery run.",
            "",
        ]
    )
    (args.reports / "model-comparison.md").write_text("\n".join(lines), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
