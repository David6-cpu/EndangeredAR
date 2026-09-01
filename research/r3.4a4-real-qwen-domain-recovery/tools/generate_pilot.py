from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys


PROJECT_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(PROJECT_ROOT / "research/r3.4a4-real-qwen-domain-recovery/src"))

from r34a4_qwen_domain.pilot import generate_pilot


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--endpoint", default="http://127.0.0.1:18081/v1/chat/completions")
    parser.add_argument("--model", default="qwen2.5-1.5b-instruct-q4_k_m")
    args = parser.parse_args()
    summary = generate_pilot(
        args.manifest,
        args.output,
        PROJECT_ROOT,
        args.endpoint,
        args.model,
    )
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
