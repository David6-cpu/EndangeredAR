import importlib.util
import io
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCANNER_PATH = ROOT / "tools" / "security" / "scan_tracked_local_paths.py"


def load_scanner():
    spec = importlib.util.spec_from_file_location("scan_tracked_local_paths", SCANNER_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class TrackedLocalPathScanTests(unittest.TestCase):
    def test_text_home_path_is_detected(self):
        scanner = load_scanner()
        private_path = "/" + "Users" + "/fixture-user/Documents/private-notes.txt"

        findings = scanner.scan_bytes(private_path.encode(), home=Path("/") / "Users" / "fixture-user")

        self.assertEqual(findings, {"user-home path": 1})

    def test_binary_path_is_detected_without_disclosing_its_value(self):
        scanner = load_scanner()
        private_path = b"/" + b"Users" + b"/fixture-user/Developer/private-source.blend"
        data = b"FBX binary prefix\x00" + private_path + b"\x00suffix"

        findings = scanner.scan_bytes(data, home=Path("/") / "Users" / "fixture-user")

        self.assertEqual(findings, {"user-home path": 1})
        rendered = scanner.format_findings({Path("asset.fbx"): findings})
        self.assertIn("asset.fbx: user-home path (1)", rendered)
        self.assertNotIn("fixture-user", rendered)
        self.assertNotIn("private-source.blend", rendered)

    def test_fbx_style_texture_path_is_detected(self):
        scanner = load_scanner()
        private_path = b"/" + b"Users" + b"/artist/exports/character.fbm/basecolor.png"
        data = b"Texture\x00FileName\x00S" + private_path + b"\x00Video\x00Filename"

        self.assertEqual(
            scanner.scan_bytes(data, home=Path("/") / "Users" / "fixture-user"),
            {"user-home path": 1},
        )

    def test_glb_json_extras_path_is_detected(self):
        scanner = load_scanner()
        private_path = b"/" + b"private" + b"/" + b"tmp/converter/source.obj"
        data = b'{"nodes":[{"extras":{"file_path":"' + private_path + b'"}}]}'

        self.assertEqual(
            scanner.scan_bytes(data, home=Path("/") / "Users" / "fixture-user"),
            {"temporary working path": 1},
        )

    def test_abstract_examples_are_not_reported_as_real_paths(self):
        scanner = load_scanner()
        samples = b"\n".join(
            (
                b"/" + b"Users" + b"/...",
                b"/" + b"Users" + b"/<username>/project",
                b"/" + b"private" + b"/" + b"tmp/...",
                b"/" + b"Applications" + b"/Unity/<version>/Unity.app",
            )
        )

        self.assertEqual(scanner.scan_bytes(samples, home=Path("/") / "Users" / "fixture-user"), {})

    def test_cli_returns_nonzero_and_only_prints_sanitized_categories(self):
        scanner = load_scanner()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            asset = root / "candidate.fbx"
            private_path = b"/" + b"private" + b"/" + b"tmp/asset-work/candidate.fbx"
            asset.write_bytes(b"binary\x00" + private_path)
            output = io.StringIO()

            with redirect_stdout(output):
                exit_code = scanner.scan_paths(root, [Path("candidate.fbx")])

        self.assertEqual(exit_code, 1)
        self.assertIn("temporary working path (1)", output.getvalue())
        self.assertNotIn("asset-work", output.getvalue())

    def test_exact_synthetic_invalid_endpoint_fixture_is_exempt(self):
        scanner = load_scanner()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fixture = root / "EndangeredAR" / "Assets" / "Tests" / "EditMode" / "AIProviderTests.cs"
            fixture.parent.mkdir(parents=True)
            fixture.write_bytes(b'endpoint = "file://' + b"/" + b"tmp/local-ai" + b'";')
            output = io.StringIO()

            with redirect_stdout(output):
                exit_code = scanner.scan_paths(root, [fixture.relative_to(root)])

        self.assertEqual(exit_code, 0)
        self.assertIn("0 findings", output.getvalue())

    def test_synthetic_fixture_value_in_product_binary_is_not_exempt(self):
        scanner = load_scanner()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            product = root / "EndangeredAR" / "Assets" / "product.bin"
            product.parent.mkdir(parents=True)
            product.write_bytes(b"binary\x00file://" + b"/" + b"tmp/local-ai")
            output = io.StringIO()

            with redirect_stdout(output):
                exit_code = scanner.scan_paths(root, [product.relative_to(root)])

        self.assertEqual(exit_code, 1)
        self.assertIn("temporary working path (1)", output.getvalue())

    def test_repository_relative_paths_are_not_reported(self):
        scanner = load_scanner()

        self.assertEqual(
            scanner.scan_bytes(b"EndangeredAR/Assets/Models/sensen.fbx"),
            {},
        )


if __name__ == "__main__":
    unittest.main()
