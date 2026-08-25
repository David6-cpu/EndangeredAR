import ast
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
RIGGED_SCRIPT = ROOT / "tools" / "art" / "r30_build_candidate.py"
EAT_SCRIPT = ROOT / "tools" / "art" / "r32b_build_eat_animation.py"


def assigned_literal(path, variable_name):
    tree = ast.parse(path.read_text(encoding="utf-8"))
    for node in tree.body:
        if isinstance(node, ast.Assign) and any(
            isinstance(target, ast.Name) and target.id == variable_name
            for target in node.targets
        ):
            return ast.literal_eval(node.value)
    raise AssertionError(f"Assignment {variable_name!r} was not found")


def export_keywords(path, function_name):
    tree = ast.parse(path.read_text(encoding="utf-8"))
    function = next(
        node for node in tree.body
        if isinstance(node, ast.FunctionDef) and node.name == function_name
    )
    call = next(
        node for node in ast.walk(function)
        if isinstance(node, ast.Call)
        and isinstance(node.func, ast.Attribute)
        and node.func.attr == "fbx"
    )
    wanted = {"path_mode", "use_custom_props", "embed_textures"}
    return {
        keyword.arg: ast.literal_eval(keyword.value)
        for keyword in call.keywords
        if keyword.arg in wanted
    }


class ArtExportSanitizationTests(unittest.TestCase):
    def test_rigged_export_strips_paths_and_externalizes_textures(self):
        settings = assigned_literal(RIGGED_SCRIPT, "EXPORT_SETTINGS")

        self.assertEqual(settings["path_mode"], "STRIP")
        self.assertEqual(settings["use_custom_props"], False)
        self.assertEqual(settings["embed_textures"], False)

    def test_eat_export_strips_paths_and_custom_properties(self):
        settings = export_keywords(EAT_SCRIPT, "export_animation_only")

        self.assertEqual(settings["path_mode"], "STRIP")
        self.assertEqual(settings["use_custom_props"], False)
        self.assertEqual(settings["embed_textures"], False)

    def test_eat_fbx_is_exported_before_blend_source_is_saved(self):
        source = EAT_SCRIPT.read_text(encoding="utf-8")

        self.assertLess(
            source.index("export_result = export_animation_only"),
            source.index("bpy.ops.wm.save_as_mainfile"),
        )


if __name__ == "__main__":
    unittest.main()
