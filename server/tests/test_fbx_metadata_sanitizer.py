import array
import copy
import importlib.util
import tempfile
import unittest
from collections import namedtuple
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SANITIZER_PATH = ROOT / "tools" / "security" / "sanitize_fbx_metadata.py"
Elem = namedtuple("Elem", ("id", "props", "props_type", "elems"))


def load_sanitizer():
    spec = importlib.util.spec_from_file_location("sanitize_fbx_metadata", SANITIZER_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def elem(identifier, props=(), prop_types=b"", children=()):
    return Elem(identifier, list(props), bytearray(prop_types), list(children))


def scene_info_property(name, value):
    return elem(b"P", (name, b"KString", b"Url", b"", value), b"SSSSS")


def rigged_tree(extra_scene_property=None):
    scene_properties = [
        scene_info_property(b"DocumentUrl", b"/untitled.blend"),
        scene_info_property(b"SrcDocumentUrl", b"/untitled.blend"),
        scene_info_property(b"Original|FileName", b"/untitled.blend"),
        scene_info_property(b"Keep", b"relative-value"),
    ]
    if extra_scene_property is not None:
        scene_properties.append(extra_scene_property)
    return elem(
        b"",
        children=(
            elem(
                b"FBXHeaderExtension",
                children=(
                    elem(
                        b"SceneInfo",
                        children=(elem(b"Properties70", children=scene_properties),),
                    ),
                ),
            ),
            elem(
                b"Objects",
                children=(
                    elem(b"Texture", children=(elem(b"FileName", (b"/private/tmp/work/texture.png",), b"S"),)),
                    elem(
                        b"Video",
                        children=(
                            elem(
                                b"Properties70",
                                children=(
                                    elem(
                                        b"P",
                                        (b"Path", b"KString", b"Url", b"", b"/private/tmp/work/texture.png"),
                                        b"SSSSS",
                                    ),
                                    elem(b"P", (b"Keep", b"KString", b"", b"", b"relative"), b"SSSSS"),
                                ),
                            ),
                            elem(b"Filename", (b"/private/tmp/work/texture.png",), b"S"),
                        ),
                    ),
                    elem(b"Geometry", (array.array("d", (1.0, 2.0, 3.0)),), b"d"),
                ),
            ),
            elem(b"Connections", children=(elem(b"C", (b"OO", 12, 34), b"SLL"),)),
        ),
    )


class FbxMetadataSanitizerTests(unittest.TestCase):
    def test_rigged_profile_removes_only_six_authorized_leaf_nodes(self):
        sanitizer = load_sanitizer()
        tree = rigged_tree()

        report = sanitizer.remove_authorized_path_nodes(tree, "rigged")

        self.assertEqual(report["removedCount"], 6)
        self.assertEqual(
            [child.props[0] for child in tree.elems[0].elems[0].elems[0].elems],
            [b"Keep"],
        )
        texture = tree.elems[1].elems[0]
        video = tree.elems[1].elems[1]
        self.assertEqual(texture.elems, [])
        self.assertEqual([child.props[0] for child in video.elems[0].elems], [b"Keep"])
        self.assertEqual([child.id for child in video.elems], [b"Properties70"])
        self.assertEqual(tree.elems[1].elems[2].props[0].tolist(), [1.0, 2.0, 3.0])
        self.assertEqual(tree.elems[2].elems[0].props, [b"OO", 12, 34])

    def test_wrong_property_type_fails_closed(self):
        sanitizer = load_sanitizer()
        tree = rigged_tree()
        tree.elems[1].elems[0].elems[0].props_type[:] = b"R"

        with self.assertRaisesRegex(sanitizer.SanitizationError, "property type"):
            sanitizer.remove_authorized_path_nodes(tree, "rigged")

    def test_missing_or_duplicate_target_fails_closed(self):
        sanitizer = load_sanitizer()
        missing = rigged_tree()
        missing.elems[1].elems[0].elems.clear()
        duplicate = rigged_tree()
        duplicate.elems[1].elems[0].elems.append(
            elem(b"FileName", (b"/private/tmp/work/second.png",), b"S")
        )

        with self.assertRaisesRegex(sanitizer.SanitizationError, "expected count"):
            sanitizer.remove_authorized_path_nodes(missing, "rigged")
        with self.assertRaisesRegex(sanitizer.SanitizationError, "expected count"):
            sanitizer.remove_authorized_path_nodes(duplicate, "rigged")

    def test_unapproved_local_path_fails_closed(self):
        sanitizer = load_sanitizer()
        unexpected = scene_info_property(b"UnexpectedPath", b"/Users/fixture/private.blend")

        with self.assertRaisesRegex(sanitizer.SanitizationError, "unexpected local absolute path"):
            sanitizer.remove_authorized_path_nodes(rigged_tree(unexpected), "rigged")

    def test_unapproved_absolute_path_on_an_unusual_mount_fails_closed(self):
        sanitizer = load_sanitizer()
        unexpected = scene_info_property(b"UnexpectedPath", b"/Volumes/private/source.blend")

        with self.assertRaisesRegex(sanitizer.SanitizationError, "UnexpectedPath") as raised:
            sanitizer.remove_authorized_path_nodes(rigged_tree(unexpected), "rigged")
        self.assertNotIn("Volumes", str(raised.exception))
        self.assertNotIn("source.blend", str(raised.exception))

    def test_eat_profile_only_removes_native_document_source(self):
        sanitizer = load_sanitizer()
        tree = elem(
            b"",
            children=(
                elem(
                    b"FBXHeaderExtension",
                    children=(
                        elem(
                            b"SceneInfo",
                            children=(
                                elem(
                                    b"Properties70",
                                    children=(
                                        scene_info_property(
                                            b"Original|ApplicationNativeFile",
                                            b"/Users/fixture/source.blend",
                                        ),
                                        scene_info_property(b"DocumentUrl", b"/untitled.blend"),
                                        scene_info_property(b"SrcDocumentUrl", b"/untitled.blend"),
                                        scene_info_property(b"Original|FileName", b"/untitled.blend"),
                                        scene_info_property(b"Keep", b"relative-value"),
                                    ),
                                ),
                            ),
                        ),
                    ),
                ),
            ),
        )

        report = sanitizer.remove_authorized_path_nodes(tree, "eat")

        self.assertEqual(report["removedCount"], 4)
        remaining = tree.elems[0].elems[0].elems[0].elems
        self.assertEqual([child.props[0] for child in remaining], [b"Keep"])

    def test_semantic_diff_detects_array_and_connection_changes(self):
        sanitizer = load_sanitizer()
        expected = rigged_tree()
        actual = copy.deepcopy(expected)
        actual.elems[1].elems[2].props[0][1] = 99.0

        differences = sanitizer.semantic_differences(expected, actual)

        self.assertTrue(any("array content" in item for item in differences))

        actual = copy.deepcopy(expected)
        actual.elems[2].elems[0].props[2] = 99
        differences = sanitizer.semantic_differences(expected, actual)
        self.assertTrue(any("property value" in item for item in differences))

    def test_only_root_file_id_and_creation_time_can_be_normalized(self):
        sanitizer = load_sanitizer()
        expected = elem(
            b"",
            children=(
                elem(b"FileId", (b"old",), b"R"),
                elem(b"CreationTime", (b"old",), b"S"),
                elem(b"Geometry", (1,), b"I"),
            ),
        )
        actual = copy.deepcopy(expected)
        actual.elems[0].props[0] = b"writer-file-id"
        actual.elems[1].props[0] = b"writer-time"

        self.assertEqual(sanitizer.semantic_differences(expected, actual), [])

        actual.elems[2].props[0] = 2
        self.assertNotEqual(sanitizer.semantic_differences(expected, actual), [])

    def test_blender_runtime_contract_is_version_locked(self):
        sanitizer = load_sanitizer()

        self.assertEqual(sanitizer.REQUIRED_BLENDER_VERSION, (5, 2, 0))
        self.assertEqual(sanitizer.REQUIRED_BLENDER_BUILD_HASH, "fbe6228777e7")
        self.assertEqual(len(sanitizer.REQUIRED_PARSE_FBX_SHA256), 64)
        self.assertEqual(len(sanitizer.REQUIRED_ENCODE_BIN_SHA256), 64)

    def test_output_scanner_loads_from_the_sanitizer_directory(self):
        sanitizer = load_sanitizer()
        with tempfile.TemporaryDirectory() as directory:
            candidate = Path(directory) / "candidate.fbx"
            candidate.write_bytes(b"safe-binary")
            self.assertEqual(sanitizer._scan_output_bytes(candidate), {})

            candidate.write_bytes(b"binary\0/Users/fixture/private.blend")
            with self.assertRaisesRegex(sanitizer.SanitizationError, "still contains"):
                sanitizer._scan_output_bytes(candidate)


if __name__ == "__main__":
    unittest.main()
