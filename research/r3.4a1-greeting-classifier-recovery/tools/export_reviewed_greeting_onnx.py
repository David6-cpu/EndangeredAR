#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys


RESEARCH_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = RESEARCH_ROOT.parents[1]
sys.path.insert(0, str(RESEARCH_ROOT / "src"))

from r34a1_greeting.onnx_export import (  # noqa: E402
    export_and_verify_locked_onnx,
)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Export the Dev-locked TextCNN Pair and verify PyTorch/ONNX Runtime "
            "logit parity on Test and human-reviewed Gold v2."
        )
    )
    parser.add_argument("--corpus", type=Path, required=True)
    parser.add_argument("--checkpoint", type=Path, required=True)
    parser.add_argument("--vocab", type=Path, required=True)
    parser.add_argument("--onnx-output", type=Path, required=True)
    parser.add_argument(
        "--gold",
        type=Path,
        default=RESEARCH_ROOT / "data" / "endangeredar_gold_v2.jsonl",
    )
    parser.add_argument(
        "--review-manifest",
        type=Path,
        default=RESEARCH_ROOT / "manifests" / "gold-v2-project-review-manifest.json",
    )
    parser.add_argument(
        "--report-output",
        type=Path,
        default=RESEARCH_ROOT / "reports" / "reviewed-onnx-parity.json",
    )
    args = parser.parse_args(argv)

    onnx_output = args.onnx_output.expanduser().resolve()
    if onnx_output.is_relative_to(REPO_ROOT.resolve()):
        parser.error("--onnx-output must remain outside the Git repository")

    report = export_and_verify_locked_onnx(
        corpus_path=args.corpus,
        gold_path=args.gold,
        checkpoint_path=args.checkpoint,
        vocab_path=args.vocab,
        review_manifest_path=args.review_manifest,
        output_path=onnx_output,
    )
    args.report_output.parent.mkdir(parents=True, exist_ok=True)
    args.report_output.write_text(
        json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(f"ONNX: {onnx_output}")
    print(f"SHA-256: {report['onnxSha256']}")
    print(f"Size: {report['onnxBytes']} bytes")
    print(f"Parity samples: {report['paritySampleCount']}")
    print(f"Max absolute error: {report['maxAbsoluteLogitError']:.10f}")
    print(f"Parity: {'passed' if report['parityPassed'] else 'failed'}")
    return 0 if report["parityPassed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
