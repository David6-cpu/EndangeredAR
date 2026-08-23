import argparse
import hashlib
import json
import math
import os
import sys

import bpy
from mathutils import Quaternion, Vector


FPS = 30
END_FRAME = 106
KEY_FRAMES = (
    (1, "start"),
    (16, "lower-hand"),
    (34, "hand-to-mouth-1"),
    (52, "chew"),
    (68, "hand-to-mouth-2"),
    (86, "lower"),
    (106, "end"),
)
REQUIRED_BONES = {
    "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head",
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftUpLeg", "LeftLeg", "LeftFoot",
    "RightUpLeg", "RightLeg", "RightFoot",
}


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output-root", required=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv)


def suffix(name):
    return name.split(":")[-1]


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def action_curves(action):
    curves = []
    for layer in getattr(action, "layers", []):
        for strip in getattr(layer, "strips", []):
            for bag in getattr(strip, "channelbags", []):
                curves.extend(list(getattr(bag, "fcurves", [])))
    if not curves and hasattr(action, "fcurves"):
        curves.extend(list(action.fcurves))
    return curves


def find_action(ending):
    return next(action for action in bpy.data.actions if action.name.endswith(ending))


def bone_by_short_name(armature, short_name):
    return next(
        (bone for bone in armature.pose.bones if suffix(bone.name) == short_name),
        None,
    )


def snapshot_pose(armature):
    result = {}
    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"
        result[bone.name] = {
            "location": bone.location.copy(),
            "rotation": bone.rotation_quaternion.copy(),
            "scale": bone.scale.copy(),
        }
    return result


def restore_pose(armature, base):
    for bone in armature.pose.bones:
        state = base[bone.name]
        bone.location = state["location"]
        bone.rotation_quaternion = state["rotation"]
        bone.scale = state["scale"]


def rotation_delta(x=0.0, y=0.0, z=0.0):
    result = Quaternion()
    for axis, degrees in (((1.0, 0.0, 0.0), x), ((0.0, 1.0, 0.0), y), ((0.0, 0.0, 1.0), z)):
        if abs(degrees) > 1e-8:
            result = result @ Quaternion(axis, math.radians(degrees))
    return result


def apply_rotations(armature, rotations):
    for short_name, angles in rotations.items():
        bone = bone_by_short_name(armature, short_name)
        if bone is None:
            raise RuntimeError(f"Missing required pose bone: {short_name}")
        bone.rotation_quaternion = bone.rotation_quaternion @ rotation_delta(**angles)


def key_all_bones(armature, frame):
    for bone in armature.pose.bones:
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def candidate_rotations(style, frame):
    expressive = style == "expressive"
    amount = 1.0 if not expressive else 1.35
    poses = {
        1: {},
        16: {
            "Spine1": {"x": 2.0 * amount, "z": -1.0 * amount},
            "Spine2": {"x": 2.5 * amount},
            "Neck": {"x": -3.0 * amount, "z": 2.0 * amount},
            "Head": {"x": -4.0 * amount, "z": 3.0 * amount},
            "LeftShoulder": {"x": 18.0 * amount, "y": 8.0 * amount},
            "LeftArm": {"y": 28.0 * amount, "z": -18.0 * amount},
            "LeftForeArm": {"x": 52.0 * amount, "z": -8.0 * amount},
            "LeftHand": {"x": 14.0 * amount, "z": 6.0 * amount},
        },
        34: {
            "Spine": {"x": -1.5 * amount},
            "Spine1": {"x": -3.0 * amount, "z": 1.0 * amount},
            "Spine2": {"x": -4.0 * amount},
            "Neck": {"x": 5.0 * amount, "z": -2.0 * amount},
            "Head": {"x": 7.0 * amount, "z": -3.0 * amount},
            "LeftShoulder": {"x": -4.0 * amount, "z": 3.0 * amount},
            "LeftArm": {"x": -4.0 * amount, "z": 2.0 * amount},
            "LeftForeArm": {"x": -7.0 * amount, "z": 3.0 * amount},
            "LeftHand": {"x": -10.0 * amount, "z": -8.0 * amount},
        },
        52: {
            "Spine1": {"x": -2.0 * amount},
            "Spine2": {"x": -2.0 * amount},
            "Neck": {"x": 2.0 * amount, "z": 2.5 * amount},
            "Head": {"x": 3.0 * amount, "z": 4.0 * amount},
            "LeftShoulder": {"x": -2.0 * amount, "z": 2.0 * amount},
            "LeftArm": {"x": -2.0 * amount},
            "LeftForeArm": {"x": -4.0 * amount, "z": 2.0 * amount},
            "LeftHand": {"x": -8.0 * amount, "z": -6.0 * amount},
        },
        68: {
            "Spine": {"x": -1.0 * amount},
            "Spine1": {"x": -3.5 * amount, "z": -1.0 * amount},
            "Spine2": {"x": -4.5 * amount},
            "Neck": {"x": 5.5 * amount, "z": 2.0 * amount},
            "Head": {"x": 7.5 * amount, "z": 3.0 * amount},
            "LeftShoulder": {"x": -5.0 * amount, "z": -3.0 * amount},
            "LeftArm": {"x": -5.0 * amount, "z": -2.0 * amount},
            "LeftForeArm": {"x": -9.0 * amount, "z": -3.0 * amount},
            "LeftHand": {"x": -12.0 * amount, "z": 9.0 * amount},
        },
        86: {
            "Spine1": {"x": 1.0 * amount},
            "Neck": {"x": -2.0 * amount, "z": -1.0 * amount},
            "Head": {"x": -3.0 * amount, "z": -2.0 * amount},
            "LeftShoulder": {"x": 12.0 * amount, "y": 5.0 * amount},
            "LeftArm": {"y": 20.0 * amount, "z": -12.0 * amount},
            "LeftForeArm": {"x": 36.0 * amount, "z": -5.0 * amount},
            "LeftHand": {"x": 10.0 * amount},
        },
        106: {},
    }
    return poses[frame]


def make_candidate_action(armature, base, style):
    action = bpy.data.actions.new(f"Sensen_Eat_{style.title()}")
    action.use_fake_user = True
    armature.animation_data.action = action
    for frame, _ in KEY_FRAMES:
        bpy.context.scene.frame_set(frame)
        restore_pose(armature, base)
        apply_rotations(armature, candidate_rotations(style, frame))
        key_all_bones(armature, frame)
    for curve in action_curves(action):
        for point in curve.keyframe_points:
            point.interpolation = "BEZIER"
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = END_FRAME
    bpy.context.scene.render.fps = FPS
    return action


def angle_degrees(first, second):
    return math.degrees(first.rotation_difference(second).angle)


def collect_action_metrics(armature, action, base):
    armature.animation_data.action = action
    driven = set()
    root_locations = []
    root_rotations = []
    for frame, _ in KEY_FRAMES:
        bpy.context.scene.frame_set(frame)
        for bone in armature.pose.bones:
            state = base[bone.name]
            if (bone.location - state["location"]).length > 1e-7 or angle_degrees(bone.rotation_quaternion, state["rotation"]) > 1e-4:
                driven.add(suffix(bone.name))
        hips = bone_by_short_name(armature, "Hips")
        root_locations.append(hips.location.copy())
        root_rotations.append(hips.rotation_quaternion.copy())

    bpy.context.scene.frame_set(1)
    start_pose = snapshot_pose(armature)
    bpy.context.scene.frame_set(END_FRAME)
    end_pose = snapshot_pose(armature)
    max_end_location = 0.0
    max_end_angle = 0.0
    for name in start_pose:
        max_end_location = max(max_end_location, (end_pose[name]["location"] - start_pose[name]["location"]).length)
        max_end_angle = max(max_end_angle, angle_degrees(end_pose[name]["rotation"], start_pose[name]["rotation"]))
    return {
        "name": action.name,
        "frameRange": [1, END_FRAME],
        "durationSeconds": (END_FRAME - 1) / FPS,
        "fps": FPS,
        "curveCount": len(action_curves(action)),
        "drivenBones": sorted(driven),
        "drivenBoneCount": len(driven),
        "rootLocationMaxDelta": max((value - root_locations[0]).length for value in root_locations),
        "rootRotationMaxDeltaDegrees": max(angle_degrees(value, root_rotations[0]) for value in root_rotations),
        "startEndMaxLocationDelta": max_end_location,
        "startEndMaxRotationDeltaDegrees": max_end_angle,
        "animationEventCount": 0,
        "loop": False,
        "keyFrames": [{"frame": frame, "role": role} for frame, role in KEY_FRAMES],
    }


def mesh_world_bounds(mesh_obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = mesh_obj.evaluated_get(depsgraph)
    evaluated_mesh = evaluated.to_mesh()
    try:
        points = [evaluated.matrix_world @ vertex.co for vertex in evaluated_mesh.vertices]
        low = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
        high = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
        return (low + high) * 0.5, (high - low) * 0.5
    finally:
        evaluated.to_mesh_clear()


def prepare_render_scene(mesh_obj):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("R32B_World")
    scene.world.color = (0.74, 0.82, 0.68)
    scene.render.image_settings.color_mode = "RGBA"

    material = bpy.data.materials.new("R32B_Neutral_Evidence")
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (0.34, 0.37, 0.40, 1.0)
    principled.inputs["Roughness"].default_value = 0.7
    mesh_obj.data.materials.clear()
    mesh_obj.data.materials.append(material)

    key_data = bpy.data.lights.new("R32B_Key", type="AREA")
    key_data.energy = 700
    key_data.shape = "DISK"
    key_data.size = 4.0
    key = bpy.data.objects.new("R32B_Key", key_data)
    scene.collection.objects.link(key)
    key.location = (4.0, -5.0, 6.0)

    fill_data = bpy.data.lights.new("R32B_Fill", type="AREA")
    fill_data.energy = 350
    fill_data.size = 5.0
    fill = bpy.data.objects.new("R32B_Fill", fill_data)
    scene.collection.objects.link(fill)
    fill.location = (-4.0, -2.0, 4.0)

    camera_data = bpy.data.cameras.new("R32B_Camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("R32B_Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def aim_camera(camera, center, extents, direction):
    size = max(extents.x, extents.y, extents.z)
    distance = max(size * 8.0, 0.05)
    camera.location = center + direction.normalized() * distance
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = max(extents.z * 2.35, extents.x * 2.35, extents.y * 2.35)
    camera.data.clip_start = max(distance * 0.001, 0.00001)
    camera.data.clip_end = distance * 4.0


def render_evidence(armature, mesh_obj, action, style, evidence_dir):
    os.makedirs(evidence_dir, exist_ok=True)
    camera = prepare_render_scene(mesh_obj)
    armature.animation_data.action = action
    outputs = []
    directions = {
        "front": Vector((0.0, -1.0, 0.0)),
        "side": Vector((1.0, 0.0, 0.0)),
    }
    for frame, role in KEY_FRAMES:
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        center, extents = mesh_world_bounds(mesh_obj)
        for view, direction in directions.items():
            aim_camera(camera, center, extents, direction)
            path = os.path.join(evidence_dir, f"{style}-{frame:03d}-{role}-{view}.png")
            bpy.context.scene.render.filepath = path
            bpy.ops.render.render(write_still=True)
            outputs.append(path)
    return outputs


def export_animation_only(armature, action, output_path):
    for obj in bpy.context.scene.objects:
        obj.select_set(False)
    hierarchy_anchors = [
        obj for obj in bpy.context.scene.objects
        if obj.type == "EMPTY" and obj.name == "world"
    ]
    for anchor in hierarchy_anchors:
        anchor.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    armature.animation_data.action = action
    result = bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=True,
        object_types={"ARMATURE", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        bake_space_transform=False,
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO",
    )
    return sorted(result)


def validate_hierarchy(armature):
    bones = list(armature.data.bones)
    missing = sorted(REQUIRED_BONES - {suffix(bone.name) for bone in bones})
    if missing:
        raise RuntimeError(f"Missing required bones: {missing}")
    return {
        "armatureName": armature.name,
        "boneCount": len(bones),
        "rootBones": [bone.name for bone in bones if bone.parent is None],
        "hierarchy": [
            {"name": bone.name, "parent": bone.parent.name if bone.parent else None}
            for bone in bones
        ],
        "requiredBonesMissing": missing,
    }


def main():
    args = parse_args()
    input_path = os.path.abspath(args.input)
    output_root = os.path.abspath(args.output_root)
    os.makedirs(output_root, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    import_result = bpy.ops.import_scene.fbx(filepath=input_path, use_anim=True)
    armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    mesh_obj = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
    idle = find_action("Sensen_Idle")
    taunt = find_action("Sensen_Taunt")
    armature.animation_data.action = idle
    bpy.context.scene.frame_set(1)
    base = snapshot_pose(armature)
    hierarchy = validate_hierarchy(armature)
    source_actions = {
        "idle": {"name": idle.name, "frames": [float(value) for value in idle.frame_range]},
        "taunt": {"name": taunt.name, "frames": [float(value) for value in taunt.frame_range]},
    }
    original_armature_transform = {
        "location": list(armature.location),
        "rotationEuler": list(armature.rotation_euler),
        "scale": list(armature.scale),
    }

    reports = []
    for style in ("subtle", "expressive"):
        candidate_dir = os.path.join(output_root, "candidates", style)
        evidence_dir = os.path.join(output_root, "evidence", style)
        report_dir = os.path.join(output_root, "reports")
        os.makedirs(candidate_dir, exist_ok=True)
        os.makedirs(report_dir, exist_ok=True)

        action = make_candidate_action(armature, base, style)
        metrics = collect_action_metrics(armature, action, base)
        blend_path = os.path.join(candidate_dir, f"sensen-eat-{style}.blend")
        fbx_path = os.path.join(candidate_dir, f"sensen-eat-{style}-animation.fbx")
        evidence = render_evidence(armature, mesh_obj, action, style, evidence_dir)
        bpy.ops.wm.save_as_mainfile(filepath=blend_path)
        export_result = export_animation_only(armature, action, fbx_path)

        report = {
            "schemaVersion": 1,
            "blenderVersion": bpy.app.version_string,
            "source": input_path,
            "sourceSha256": sha256(input_path),
            "sourceBytes": os.path.getsize(input_path),
            "importResult": sorted(import_result),
            "candidate": style,
            "design": "One-hand leaf-to-mouth gesture with two clear bites, head follow-through, and return to the formal Idle pose.",
            "action": metrics,
            "rig": hierarchy,
            "sourceActions": source_actions,
            "armatureTransform": original_armature_transform,
            "standaloneAnimationFbx": fbx_path,
            "standaloneAnimationFbxBytes": os.path.getsize(fbx_path),
            "standaloneAnimationFbxSha256": sha256(fbx_path),
            "blendSource": blend_path,
            "blendSourceBytes": os.path.getsize(blend_path),
            "blendSourceSha256": sha256(blend_path),
            "exportResult": export_result,
            "evidence": evidence,
            "limitations": [
                "No prop is included by design.",
                "No tail bones exist in the accepted source rig.",
                "Visual deformation and Unity Generic Avatar compatibility require independent review after export.",
            ],
        }
        report_path = os.path.join(report_dir, f"sensen-eat-{style}-blender-report.json")
        with open(report_path, "w", encoding="utf-8") as handle:
            json.dump(report, handle, ensure_ascii=False, indent=2)
        reports.append(report_path)

    print("R32B_REPORTS=" + json.dumps(reports))


if __name__ == "__main__":
    main()
