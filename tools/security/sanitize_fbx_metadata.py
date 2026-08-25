#!/usr/bin/env python3
import argparse
import array
import copy
import hashlib
import importlib.util
import inspect
import json
import sys
from collections import Counter, namedtuple
from pathlib import Path


REQUIRED_BLENDER_VERSION = (5, 2, 0)
REQUIRED_BLENDER_VERSION_STRING = "5.2.0 LTS"
REQUIRED_BLENDER_BUILD_HASH = "fbe6228777e7"
REQUIRED_PARSE_FBX_SHA256 = "c976b04da54a50051df68834df09a675e43ddef616c8226becbf1f9671af7f93"
REQUIRED_ENCODE_BIN_SHA256 = "b0e42ec731e1cb232e2bc1d88f23eb2f31bea4a6ece9eabddc3b83c30b903c15"
REQUIRED_FBX_VERSION = 7400

Rule = namedtuple(
    "Rule",
    (
        "name",
        "element_path",
        "property_types",
        "property_index",
        "descriptor",
        "expected_count",
        "value_kind",
    ),
)


class SanitizationError(RuntimeError):
    pass


def _rule(name, path, property_types, property_index, descriptor=None, value_kind="absolute"):
    return Rule(
        name,
        tuple(part.encode("utf-8") for part in path.split("/")),
        property_types.encode("ascii"),
        property_index,
        descriptor.encode("utf-8") if descriptor else None,
        1,
        value_kind,
    )


RULES = {
    "rigged": (
        _rule("texture-file-name", "Objects/Texture/FileName", "S", 0, value_kind="local"),
        _rule("video-path", "Objects/Video/Properties70/P", "SSSSS", 4, descriptor="Path", value_kind="local"),
        _rule("video-file-name", "Objects/Video/Filename", "S", 0, value_kind="local"),
        _rule("document-url", "FBXHeaderExtension/SceneInfo/Properties70/P", "SSSSS", 4, descriptor="DocumentUrl"),
        _rule("source-document-url", "FBXHeaderExtension/SceneInfo/Properties70/P", "SSSSS", 4, descriptor="SrcDocumentUrl"),
        _rule("original-file-name", "FBXHeaderExtension/SceneInfo/Properties70/P", "SSSSS", 4, descriptor="Original|FileName"),
    ),
    "eat": (
        _rule("document-url", "FBXHeaderExtension/SceneInfo/Properties70/P", "SSSSS", 4, descriptor="DocumentUrl"),
        _rule("source-document-url", "FBXHeaderExtension/SceneInfo/Properties70/P", "SSSSS", 4, descriptor="SrcDocumentUrl"),
        _rule("original-file-name", "FBXHeaderExtension/SceneInfo/Properties70/P", "SSSSS", 4, descriptor="Original|FileName"),
        _rule(
            "native-document-source",
            "FBXHeaderExtension/SceneInfo/Properties70/P",
            "SSSSS",
            4,
            descriptor="Original|ApplicationNativeFile",
        ),
    ),
}

_NORMALIZED_WRITER_ROOT_ELEMENTS = {b"FileId", b"CreationTime"}
_COUNTED_ELEMENT_IDS = (
    b"Model",
    b"Geometry",
    b"Deformer",
    b"Material",
    b"Texture",
    b"Video",
    b"AnimationStack",
    b"AnimationLayer",
    b"AnimationCurveNode",
    b"AnimationCurve",
    b"Pose",
    b"GlobalSettings",
    b"Connections",
)


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _decode_build_hash(value):
    return value.decode("ascii") if isinstance(value, bytes) else str(value)


def load_blender_runtime():
    try:
        import bpy
        from io_scene_fbx import data_types, encode_bin, parse_fbx
    except ImportError as error:
        raise SanitizationError("Run this tool with the pinned Blender Python runtime.") from error

    parse_path = Path(parse_fbx.__file__).resolve()
    encode_path = Path(encode_bin.__file__).resolve()
    contract = {
        "blenderVersion": bpy.app.version_string,
        "blenderVersionTuple": list(bpy.app.version),
        "blenderBuildHash": _decode_build_hash(bpy.app.build_hash),
        "parseFbxSha256": sha256_file(parse_path),
        "encodeBinSha256": sha256_file(encode_path),
        "parseFbxModulePath": str(parse_path),
        "encodeBinModulePath": str(encode_path),
    }
    if tuple(bpy.app.version) != REQUIRED_BLENDER_VERSION:
        raise SanitizationError("The Blender version does not match the pinned sanitizer runtime.")
    if bpy.app.version_string != REQUIRED_BLENDER_VERSION_STRING:
        raise SanitizationError("The Blender version label does not match the pinned sanitizer runtime.")
    if contract["blenderBuildHash"] != REQUIRED_BLENDER_BUILD_HASH:
        raise SanitizationError("The Blender build hash does not match the pinned sanitizer runtime.")
    if contract["parseFbxSha256"] != REQUIRED_PARSE_FBX_SHA256:
        raise SanitizationError("The built-in FBX parser hash does not match the audited module.")
    if contract["encodeBinSha256"] != REQUIRED_ENCODE_BIN_SHA256:
        raise SanitizationError("The built-in FBX encoder hash does not match the audited module.")
    if getattr(parse_fbx.FBXElem, "_fields", ()) != ("id", "props", "props_type", "elems"):
        raise SanitizationError("The FBX parser element contract has changed.")
    if tuple(inspect.signature(parse_fbx.parse).parameters) != ("fn", "use_namedtuple"):
        raise SanitizationError("The FBX parser signature has changed.")
    if tuple(inspect.signature(encode_bin.write).parameters) != ("fn", "elem_root", "version"):
        raise SanitizationError("The FBX encoder signature has changed.")
    required_slots = {"id", "props", "props_type", "elems"}
    if not required_slots.issubset(set(getattr(encode_bin.FBXElem, "__slots__", ()))):
        raise SanitizationError("The FBX encoder element contract has changed.")
    if not callable(getattr(encode_bin.FBXElem, "enable_multithreading_cm", None)):
        raise SanitizationError("The FBX encoder threading contract has changed.")
    return bpy, parse_fbx, encode_bin, data_types, contract


def _decode_string(value):
    if not isinstance(value, bytes):
        return None
    try:
        return value.decode("utf-8")
    except UnicodeDecodeError:
        return None


def classify_local_absolute_path(value):
    text = _decode_string(value)
    if not text:
        return None
    if text.startswith(("/Users/", "/home/")):
        return "user-home path"
    if text.startswith(("/tmp/", "/private/tmp/", "/var/folders/", "/private/var/folders/")):
        return "temporary working path"
    if text.startswith("/Applications/"):
        return "application installation path"
    return None


def _is_absolute_path(value):
    text = _decode_string(value)
    return bool(
        text
        and len(text) > 1
        and (text.startswith("/") or (len(text) > 2 and text[1:3] in (":/", ":\\")))
    )


def _walk(root, path=(), parent=None):
    for child in root.elems:
        child_path = path + (child.id,)
        yield parent if parent is not None else root, child, child_path
        yield from _walk(child, child_path, child)


def _path_label(path):
    return "/" + "/".join(part.decode("utf-8", "replace") for part in path)


def _matches_descriptor(node, rule):
    if rule.descriptor is None:
        return True
    return bool(node.props and node.props[0] == rule.descriptor)


def _collect_rule_matches(root, rule):
    return [
        (parent, node, path)
        for parent, node, path in _walk(root)
        if path == rule.element_path and _matches_descriptor(node, rule)
    ]


def _iter_string_properties(root):
    for _parent, node, path in _walk(root):
        for index, (type_code, value) in enumerate(zip(node.props_type, node.props)):
            if type_code == ord("S") and isinstance(value, bytes):
                yield node, path, index, value


def absolute_string_property_metadata(root):
    findings = []
    for node, path, index, value in _iter_string_properties(root):
        if not _is_absolute_path(value):
            continue
        descriptor = None
        if node.id == b"P" and node.props and isinstance(node.props[0], bytes):
            descriptor = node.props[0].decode("utf-8", "replace")
        findings.append(
            {
                "elementPath": _path_label(path),
                "propertyIndex": index,
                "propertyTypes": bytes(node.props_type).decode("ascii", "replace"),
                "descriptor": descriptor,
            }
        )
    return findings


def remove_authorized_path_nodes(root, asset_type):
    rules = RULES.get(asset_type)
    if rules is None:
        raise SanitizationError("The FBX asset type is unsupported.")

    validated = []
    authorized_properties = set()
    for rule in rules:
        matches = _collect_rule_matches(root, rule)
        if len(matches) != rule.expected_count:
            raise SanitizationError(f"Rule {rule.name} expected count {rule.expected_count} but found {len(matches)}.")
        for parent, node, path in matches:
            if bytes(node.props_type) != rule.property_types:
                raise SanitizationError(f"Rule {rule.name} property type does not match the audited contract.")
            if len(node.props) != len(rule.property_types):
                raise SanitizationError(f"Rule {rule.name} property count does not match the audited contract.")
            if node.elems:
                raise SanitizationError(f"Rule {rule.name} is not an authorized leaf node.")
            value = node.props[rule.property_index]
            if rule.value_kind == "local" and classify_local_absolute_path(value) is None:
                raise SanitizationError(f"Rule {rule.name} does not contain an audited local absolute path.")
            if rule.value_kind == "absolute" and not _is_absolute_path(value):
                raise SanitizationError(f"Rule {rule.name} does not contain an audited absolute source path.")
            validated.append((rule, parent, node, path))
            authorized_properties.add((id(node), rule.property_index))

    unexpected = []
    for node, path, index, value in _iter_string_properties(root):
        if _is_absolute_path(value) and (id(node), index) not in authorized_properties:
            descriptor = None
            if node.id == b"P" and node.props and isinstance(node.props[0], bytes):
                descriptor = node.props[0].decode("utf-8", "replace")
            unexpected.append(
                {
                    "elementPath": _path_label(path),
                    "propertyIndex": index,
                    "propertyTypes": bytes(node.props_type).decode("ascii", "replace"),
                    "descriptor": descriptor,
                }
            )
    if unexpected:
        raise SanitizationError(
            "The FBX contains an unexpected local absolute path: "
            + json.dumps(unexpected, sort_keys=True)
        )

    removed = []
    for rule, parent, node, path in validated:
        parent.elems.remove(node)
        removed.append(
            {
                "rule": rule.name,
                "elementPath": _path_label(path),
                "propertyIndex": rule.property_index,
                "propertyTypes": rule.property_types.decode("ascii"),
            }
        )

    if any(_is_absolute_path(value) for _node, _path, _index, value in _iter_string_properties(root)):
        raise SanitizationError("A local absolute path remains after the authorized removals.")
    return {
        "assetType": asset_type,
        "removedCount": len(removed),
        "removed": removed,
    }


def _values_equal(expected, actual):
    if isinstance(expected, array.array) or isinstance(actual, array.array):
        return (
            isinstance(expected, array.array)
            and isinstance(actual, array.array)
            and expected.typecode == actual.typecode
            and len(expected) == len(actual)
            and expected == actual
        )
    return type(expected) is type(actual) and expected == actual


def semantic_differences(expected, actual, limit=50):
    differences = []

    def add(message):
        if len(differences) < limit:
            differences.append(message)

    def compare(left, right, path=()):
        label = _path_label(path) if path else "/"
        if left.id != right.id:
            add(f"{label}: element id differs")
            return
        if bytes(left.props_type) != bytes(right.props_type):
            add(f"{label}: property types differ")
        if len(left.props) != len(right.props):
            add(f"{label}: property count differs")
        normalize_values = len(path) == 1 and left.id in _NORMALIZED_WRITER_ROOT_ELEMENTS
        if not normalize_values:
            for index, (left_value, right_value) in enumerate(zip(left.props, right.props)):
                if not _values_equal(left_value, right_value):
                    kind = "array content" if isinstance(left_value, array.array) or isinstance(right_value, array.array) else "property value"
                    add(f"{label}: {kind} differs at index {index}")
        if len(left.elems) != len(right.elems):
            add(f"{label}: child count differs")
        for index, (left_child, right_child) in enumerate(zip(left.elems, right.elems)):
            compare(left_child, right_child, path + (left_child.id,))

    compare(expected, actual)
    return differences


def _hash_property(digest, type_code, value, normalized=False):
    digest.update(bytes((type_code,)))
    if normalized:
        digest.update(b"<writer-normalized>")
    elif isinstance(value, array.array):
        digest.update(value.typecode.encode("ascii"))
        digest.update(len(value).to_bytes(8, "little"))
        digest.update(value.tobytes())
    elif isinstance(value, bytes):
        digest.update(len(value).to_bytes(8, "little"))
        digest.update(value)
    else:
        digest.update(repr(value).encode("ascii"))


def semantic_digest(root):
    digest = hashlib.sha256()

    def visit(node, path=()):
        digest.update(len(node.id).to_bytes(2, "little"))
        digest.update(node.id)
        normalized = len(path) == 1 and node.id in _NORMALIZED_WRITER_ROOT_ELEMENTS
        for type_code, value in zip(node.props_type, node.props):
            _hash_property(digest, type_code, value, normalized=normalized)
        digest.update(len(node.elems).to_bytes(8, "little"))
        for child in node.elems:
            visit(child, path + (child.id,))

    visit(root)
    return digest.hexdigest()


def element_counts(root):
    counts = Counter()
    for _parent, node, _path in _walk(root):
        counts[node.id] += 1
    return {
        identifier.decode("ascii"): counts.get(identifier, 0)
        for identifier in _COUNTED_ELEMENT_IDS
    }


def _root_property(root, identifier):
    node = next((item for item in root.elems if item.id == identifier), None)
    return None if node is None else tuple(node.props)


def convert_to_encoder_tree(parsed_root, encode_bin, data_types):
    method_by_type = {
        data_types.BOOL: "add_bool",
        data_types.CHAR: "add_char",
        data_types.INT8: "add_int8",
        data_types.INT16: "add_int16",
        data_types.INT32: "add_int32",
        data_types.INT64: "add_int64",
        data_types.FLOAT32: "add_float32",
        data_types.FLOAT64: "add_float64",
        data_types.BYTES: "add_bytes",
        data_types.STRING: "add_string",
        data_types.INT32_ARRAY: "add_int32_array",
        data_types.INT64_ARRAY: "add_int64_array",
        data_types.FLOAT32_ARRAY: "add_float32_array",
        data_types.FLOAT64_ARRAY: "add_float64_array",
        data_types.BOOL_ARRAY: "add_bool_array",
        data_types.BYTE_ARRAY: "add_byte_array",
    }

    def convert(node):
        result = encode_bin.FBXElem(node.id)
        if len(node.props) != len(node.props_type):
            raise SanitizationError("An FBX element has mismatched property values and types.")
        for type_code, value in zip(node.props_type, node.props):
            method_name = method_by_type.get(type_code)
            if method_name is None:
                raise SanitizationError("The FBX contains an unsupported property type.")
            getattr(result, method_name)(value)
        result.elems.extend(convert(child) for child in node.elems)
        return result

    with encode_bin.FBXElem.enable_multithreading_cm():
        return convert(parsed_root)


def _scan_output_bytes(output_path):
    scanner_path = Path(__file__).with_name("scan_tracked_local_paths.py")
    spec = importlib.util.spec_from_file_location("scan_tracked_local_paths", scanner_path)
    if spec is None or spec.loader is None:
        raise SanitizationError("The tracked local-path scanner could not be loaded.")
    scanner = importlib.util.module_from_spec(spec)
    try:
        spec.loader.exec_module(scanner)
    except (ImportError, OSError) as error:
        raise SanitizationError("The tracked local-path scanner could not be loaded.") from error
    findings = scanner.scan_bytes(Path(output_path).read_bytes())
    if findings:
        raise SanitizationError("The sanitized FBX still contains a tracked local absolute path.")
    return findings


def sanitize_file(input_path, output_path, asset_type):
    input_path = Path(input_path)
    output_path = Path(output_path)
    if input_path.resolve() == output_path.resolve():
        raise SanitizationError("Input and output paths must differ.")

    _bpy, parse_fbx, encode_bin, data_types, runtime = load_blender_runtime()
    original, version = parse_fbx.parse(str(input_path))
    if version != REQUIRED_FBX_VERSION:
        raise SanitizationError("The FBX version does not match the audited candidate format.")

    sanitized_tree = copy.deepcopy(original)
    removal_report = remove_authorized_path_nodes(sanitized_tree, asset_type)
    expected_digest = semantic_digest(sanitized_tree)
    before_counts = element_counts(original)
    expected_counts = element_counts(sanitized_tree)
    before_file_id = _root_property(original, b"FileId")
    before_creation_time = _root_property(original, b"CreationTime")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    encoded_tree = convert_to_encoder_tree(sanitized_tree, encode_bin, data_types)
    encode_bin.write(str(output_path), encoded_tree, version)
    reparsed, output_version = parse_fbx.parse(str(output_path))
    if output_version != version:
        raise SanitizationError("The FBX version changed during round-trip encoding.")

    differences = semantic_differences(sanitized_tree, reparsed)
    if differences:
        raise SanitizationError("The FBX semantic tree changed outside the authorized normalizations: " + "; ".join(differences))
    actual_digest = semantic_digest(reparsed)
    if expected_digest != actual_digest:
        raise SanitizationError("The normalized FBX semantic digest changed during round-trip encoding.")
    output_counts = element_counts(reparsed)
    if output_counts != expected_counts:
        raise SanitizationError("The FBX structural category counts changed during round-trip encoding.")
    _scan_output_bytes(output_path)

    return {
        "schemaVersion": 1,
        "assetType": asset_type,
        "fbxVersion": version,
        "inputBytes": input_path.stat().st_size,
        "outputBytes": output_path.stat().st_size,
        "inputSha256": sha256_file(input_path),
        "outputSha256": sha256_file(output_path),
        "runtime": runtime,
        "removal": removal_report,
        "semanticEqualAfterNormalization": True,
        "semanticDigest": actual_digest,
        "nonTargetSemanticDifferences": [],
        "elementCountsBefore": before_counts,
        "elementCountsAfterAuthorizedRemoval": expected_counts,
        "elementCountsOutput": output_counts,
        "writerNormalizations": {
            "fileIdChanged": before_file_id != _root_property(reparsed, b"FileId"),
            "creationTimeChanged": before_creation_time != _root_property(reparsed, b"CreationTime"),
            "footerPaddingMayDiffer": True,
            "compressedArraysComparedAfterDecode": True,
        },
        "localPathFindings": {},
    }


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description="Remove audited local source-path metadata from a binary FBX.")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--asset-type", required=True, choices=sorted(RULES))
    parser.add_argument("--report", required=True, type=Path)
    if argv is None and "--" in sys.argv:
        argv = sys.argv[sys.argv.index("--") + 1:]
    return parser.parse_args(argv)


def main(argv=None):
    args = parse_args(argv)
    report = sanitize_file(args.input, args.output, args.asset_type)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(
        json.dumps(
            {
                "assetType": report["assetType"],
                "fbxVersion": report["fbxVersion"],
                "removedCount": report["removal"]["removedCount"],
                "semanticEqualAfterNormalization": report["semanticEqualAfterNormalization"],
                "localPathFindings": report["localPathFindings"],
                "outputSha256": report["outputSha256"],
            },
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
