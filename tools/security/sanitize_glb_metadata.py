#!/usr/bin/env python3
import argparse
import copy
import hashlib
import json
import struct
import tempfile
from pathlib import Path


MAGIC = b"glTF"
SUPPORTED_VERSION = 2
JSON_CHUNK = b"JSON"
HEADER = struct.Struct("<4sII")
CHUNK_HEADER = struct.Struct("<I4s")


class SanitizationError(RuntimeError):
    pass


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def _decode_json(payload):
    try:
        return json.loads(payload.rstrip(b" \t\r\n\0").decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as error:
        raise SanitizationError("The GLB JSON chunk is invalid.") from error


def parse_glb(data):
    if len(data) < HEADER.size:
        raise SanitizationError("The GLB header is truncated.")

    magic, version, declared_length = HEADER.unpack_from(data)
    if magic != MAGIC:
        raise SanitizationError("The GLB magic is invalid.")
    if version != SUPPORTED_VERSION:
        raise SanitizationError("The GLB version is unsupported.")
    if declared_length != len(data):
        raise SanitizationError("The declared GLB length does not match the file length.")

    chunks = []
    offset = HEADER.size
    while offset < len(data):
        if offset + CHUNK_HEADER.size > len(data):
            raise SanitizationError("A GLB chunk header is truncated.")
        length, chunk_type = CHUNK_HEADER.unpack_from(data, offset)
        offset += CHUNK_HEADER.size
        if length % 4 != 0 or offset + length > len(data):
            raise SanitizationError("A GLB chunk has an invalid length.")
        payload = data[offset:offset + length]
        offset += length
        chunks.append({"type": chunk_type, "payload": payload})

    json_chunks = [chunk for chunk in chunks if chunk["type"] == JSON_CHUNK]
    if len(json_chunks) != 1:
        raise SanitizationError("Exactly one GLB JSON chunk is required.")

    return {
        "magic": magic,
        "version": version,
        "declaredLength": declared_length,
        "chunks": chunks,
        "document": _decode_json(json_chunks[0]["payload"]),
    }


def _classify_local_path(value):
    if not isinstance(value, str):
        return None

    home_roots = ("/" + "Users" + "/", "/" + "home" + "/")
    temp_roots = (
        "/" + "tmp" + "/",
        "/" + "private" + "/" + "tmp" + "/",
        str(Path(tempfile.gettempdir())) + "/",
    )
    unity_root = "/" + "Applications" + "/" + "Unity" + "/"
    if value.startswith(home_roots):
        return "user-home path"
    if value.startswith(temp_roots):
        return "temporary working path"
    if value.startswith(unity_root):
        return "Unity installation path"
    return None


def _walk_strings(value, path=()):
    if isinstance(value, dict):
        for key, item in value.items():
            yield from _walk_strings(item, path + (key,))
    elif isinstance(value, list):
        for index, item in enumerate(value):
            yield from _walk_strings(item, path + (index,))
    elif isinstance(value, str):
        yield path, value


def _target_paths(document):
    nodes = document.get("nodes", [])
    if not isinstance(nodes, list):
        raise SanitizationError("The GLB nodes collection is invalid.")

    targets = []
    for index, node in enumerate(nodes):
        if not isinstance(node, dict):
            continue
        extras = node.get("extras")
        if isinstance(extras, dict) and "file_path" in extras:
            targets.append((("nodes", index, "extras", "file_path"), extras["file_path"]))
    return targets


def _encode_document(document):
    payload = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    return payload + (b" " * ((-len(payload)) % 4))


def _rebuild_glb(parsed, document):
    encoded_json = _encode_document(document)
    encoded_chunks = []
    for chunk in parsed["chunks"]:
        payload = encoded_json if chunk["type"] == JSON_CHUNK else chunk["payload"]
        encoded_chunks.append(CHUNK_HEADER.pack(len(payload), chunk["type"]) + payload)
    body = b"".join(encoded_chunks)
    return HEADER.pack(MAGIC, parsed["version"], HEADER.size + len(body)) + body


def sanitize_glb_bytes(data, expected_hits=1):
    parsed = parse_glb(data)
    document = parsed["document"]
    targets = _target_paths(document)
    if len(targets) != expected_hits:
        raise SanitizationError("The target metadata count does not match the expected count.")

    target_paths = {path for path, _value in targets}
    removed_types = []
    for _path, value in targets:
        category = _classify_local_path(value)
        if category is None:
            raise SanitizationError("A target metadata value is not a recognized local absolute path.")
        removed_types.append(category)

    unexpected = [
        path for path, value in _walk_strings(document)
        if _classify_local_path(value) is not None and path not in target_paths
    ]
    if unexpected:
        raise SanitizationError("The GLB contains an unexpected local absolute path.")

    sanitized_document = copy.deepcopy(document)
    semantic_diff = []
    for path, _value in targets:
        node_index = path[1]
        del sanitized_document["nodes"][node_index]["extras"]["file_path"]
        semantic_diff.append(f"nodes[{node_index}].extras.file_path removed")

    sanitized = _rebuild_glb(parsed, sanitized_document)
    reparsed = parse_glb(sanitized)
    if reparsed["document"] != sanitized_document:
        raise SanitizationError("The sanitized GLB JSON did not round-trip.")
    if any(_classify_local_path(value) for _path, value in _walk_strings(reparsed["document"])):
        raise SanitizationError("A local absolute path remains after sanitization.")

    before_non_json = [chunk for chunk in parsed["chunks"] if chunk["type"] != JSON_CHUNK]
    after_non_json = [chunk for chunk in reparsed["chunks"] if chunk["type"] != JSON_CHUNK]
    non_json_unchanged = before_non_json == after_non_json
    if not non_json_unchanged:
        raise SanitizationError("A non-JSON GLB chunk changed during sanitization.")

    before_json = next(chunk["payload"] for chunk in parsed["chunks"] if chunk["type"] == JSON_CHUNK)
    after_json = next(chunk["payload"] for chunk in reparsed["chunks"] if chunk["type"] == JSON_CHUNK)
    report = {
        "schemaVersion": 1,
        "magic": MAGIC.decode("ascii"),
        "version": parsed["version"],
        "beforeBytes": len(data),
        "afterBytes": len(sanitized),
        "beforeSha256": sha256(data),
        "afterSha256": sha256(sanitized),
        "beforeJsonSha256": sha256(before_json),
        "afterJsonSha256": sha256(after_json),
        "chunkCount": len(parsed["chunks"]),
        "removedCount": len(targets),
        "removedTypes": sorted(set(removed_types)),
        "semanticDiff": semantic_diff,
        "nonJsonChunksUnchanged": non_json_unchanged,
        "nonJsonChunks": [
            {
                "type": chunk["type"].decode("ascii", "replace").rstrip("\0"),
                "bytes": len(chunk["payload"]),
                "sha256": sha256(chunk["payload"]),
            }
            for chunk in before_non_json
        ],
    }
    return sanitized, report


def sanitize_file(input_path, output_path, expected_hits=1):
    input_path = Path(input_path)
    output_path = Path(output_path)
    if input_path.resolve() == output_path.resolve():
        raise SanitizationError("Input and output paths must differ.")
    sanitized, report = sanitize_glb_bytes(input_path.read_bytes(), expected_hits=expected_hits)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(sanitized)
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Remove validated local file metadata from a GLB JSON chunk.")
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--expected-hits", type=int, default=1)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args(argv)

    report = sanitize_file(args.input, args.output, expected_hits=args.expected_hits)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
