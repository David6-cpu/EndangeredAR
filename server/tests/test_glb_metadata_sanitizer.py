import copy
import hashlib
import importlib.util
import json
import struct
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SANITIZER_PATH = ROOT / "tools" / "security" / "sanitize_glb_metadata.py"


def load_sanitizer():
    spec = importlib.util.spec_from_file_location("sanitize_glb_metadata", SANITIZER_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def make_glb(document, extra_chunks=()):
    encoded = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    encoded += b" " * ((-len(encoded)) % 4)
    chunks = [(b"JSON", encoded), *extra_chunks]
    body = b"".join(struct.pack("<I4s", len(payload), kind) + payload for kind, payload in chunks)
    return struct.pack("<4sII", b"glTF", 2, 12 + len(body)) + body


class GlbMetadataSanitizerTests(unittest.TestCase):
    def test_only_target_metadata_is_removed_and_non_json_chunks_are_identical(self):
        sanitizer = load_sanitizer()
        private_path = "/" + "private" + "/tmp/converter/source.obj"
        document = {
            "asset": {"version": "2.0"},
            "nodes": [
                {"name": "Root"},
                {"name": "Mesh", "extras": {"file_path": private_path, "keep": "value"}},
            ],
            "buffers": [{"byteLength": 8}],
        }
        binary = b"\x01\x02\x03\x04\x05\x06\x07\x08"
        unknown = b"opaque-chunk"
        source = make_glb(document, ((b"BIN\x00", binary), (b"TEST", unknown)))

        sanitized, report = sanitizer.sanitize_glb_bytes(source, expected_hits=1)
        before = sanitizer.parse_glb(source)
        after = sanitizer.parse_glb(sanitized)

        expected = copy.deepcopy(document)
        del expected["nodes"][1]["extras"]["file_path"]
        self.assertEqual(after["document"], expected)
        self.assertEqual(after["document"]["nodes"][1]["extras"], {"keep": "value"})
        self.assertEqual([chunk["type"] for chunk in after["chunks"]], [b"JSON", b"BIN\x00", b"TEST"])
        self.assertEqual(after["chunks"][1]["payload"], before["chunks"][1]["payload"])
        self.assertEqual(after["chunks"][2]["payload"], before["chunks"][2]["payload"])
        self.assertEqual(report["removedCount"], 1)
        self.assertEqual(report["semanticDiff"], ["nodes[1].extras.file_path removed"])
        self.assertEqual(report["nonJsonChunksUnchanged"], True)
        self.assertNotIn("converter", json.dumps(report))

    def test_empty_extras_object_is_preserved(self):
        sanitizer = load_sanitizer()
        private_path = "/" + "tmp" + "/source.obj"
        source = make_glb({"asset": {"version": "2.0"}, "nodes": [{"extras": {"file_path": private_path}}]})

        sanitized, _report = sanitizer.sanitize_glb_bytes(source, expected_hits=1)

        self.assertEqual(sanitizer.parse_glb(sanitized)["document"]["nodes"][0]["extras"], {})

    def test_missing_or_unexpected_target_count_fails_closed(self):
        sanitizer = load_sanitizer()
        missing = make_glb({"asset": {"version": "2.0"}, "nodes": [{}]})
        private_path = "/" + "tmp" + "/source.obj"
        duplicate = make_glb(
            {
                "asset": {"version": "2.0"},
                "nodes": [
                    {"extras": {"file_path": private_path}},
                    {"extras": {"file_path": private_path}},
                ],
            }
        )

        with self.assertRaises(sanitizer.SanitizationError):
            sanitizer.sanitize_glb_bytes(missing, expected_hits=1)
        with self.assertRaises(sanitizer.SanitizationError):
            sanitizer.sanitize_glb_bytes(duplicate, expected_hits=1)

    def test_non_absolute_target_and_other_local_path_fail_closed(self):
        sanitizer = load_sanitizer()
        relative = make_glb(
            {"asset": {"version": "2.0"}, "nodes": [{"extras": {"file_path": "source.obj"}}]}
        )
        target = "/" + "tmp" + "/source.obj"
        unexpected = "/" + "Users" + "/artist/unexpected.blend"
        extra = make_glb(
            {
                "asset": {"version": "2.0", "generator": unexpected},
                "nodes": [{"extras": {"file_path": target}}],
            }
        )

        with self.assertRaises(sanitizer.SanitizationError):
            sanitizer.sanitize_glb_bytes(relative, expected_hits=1)
        with self.assertRaises(sanitizer.SanitizationError):
            sanitizer.sanitize_glb_bytes(extra, expected_hits=1)

    def test_invalid_header_or_length_fails_closed(self):
        sanitizer = load_sanitizer()
        valid = make_glb({"asset": {"version": "2.0"}, "nodes": []})
        invalid_magic = b"FAIL" + valid[4:]
        invalid_length = valid[:8] + struct.pack("<I", len(valid) + 4) + valid[12:]

        with self.assertRaises(sanitizer.SanitizationError):
            sanitizer.parse_glb(invalid_magic)
        with self.assertRaises(sanitizer.SanitizationError):
            sanitizer.parse_glb(invalid_length)

    def test_report_hashes_match_the_actual_candidate(self):
        sanitizer = load_sanitizer()
        private_path = "/" + "tmp" + "/source.obj"
        source = make_glb({"asset": {"version": "2.0"}, "nodes": [{"extras": {"file_path": private_path}}]})

        sanitized, report = sanitizer.sanitize_glb_bytes(source, expected_hits=1)

        self.assertEqual(report["beforeSha256"], hashlib.sha256(source).hexdigest())
        self.assertEqual(report["afterSha256"], hashlib.sha256(sanitized).hexdigest())


if __name__ == "__main__":
    unittest.main()
