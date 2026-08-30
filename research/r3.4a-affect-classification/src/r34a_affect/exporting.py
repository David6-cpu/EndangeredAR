from __future__ import annotations

import hashlib
from pathlib import Path
from typing import Sequence

import numpy as np
import onnx
import onnxruntime as ort
import torch
from torch import nn


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def export_and_verify(
    model: nn.Module,
    sample: np.ndarray,
    output_path: Path,
    opset: int,
    output_names: Sequence[str],
) -> dict[str, object]:
    model.eval()
    tensor = torch.from_numpy(sample[:1]).long()
    with torch.inference_mode():
        torch_outputs = [value.numpy() for value in model(tensor)]
    torch.onnx.export(
        model,
        tensor,
        output_path,
        input_names=["input_ids"],
        output_names=list(output_names),
        opset_version=opset,
        dynamic_axes=None,
        do_constant_folding=True,
    )
    graph = onnx.load(output_path)
    onnx.checker.check_model(graph)
    session = ort.InferenceSession(str(output_path), providers=["CPUExecutionProvider"])
    onnx_outputs = session.run(list(output_names), {"input_ids": sample[:1].astype(np.int64)})
    max_error = max(
        float(np.max(np.abs(expected - actual)))
        for expected, actual in zip(torch_outputs, onnx_outputs)
    )
    operators = sorted({node.op_type for node in graph.graph.node})
    return {
        "path": output_path.name,
        "bytes": output_path.stat().st_size,
        "sha256": sha256(output_path),
        "opset": opset,
        "inputDtype": "int64",
        "inputShape": list(sample[:1].shape),
        "outputNames": list(output_names),
        "operators": operators,
        "nodeCount": len(graph.graph.node),
        "maxAbsoluteLogitError": max_error,
        "parityPassed": max_error <= 1e-4,
    }
