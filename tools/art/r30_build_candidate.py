import argparse
import hashlib
import json
import math
import os
import sys

import bpy
from mathutils import Quaternion


EXPORT_SETTINGS = {
    "use_selection": False,
    "object_types": {"ARMATURE", "MESH", "EMPTY"},
    "apply_unit_scale": True,
    "apply_scale_options": "FBX_SCALE_ALL",
    "use_space_transform": True,
    "bake_space_transform": False,
    "use_mesh_modifiers": True,
    "mesh_smooth_type": "OFF",
    "add_leaf_bones": False,
    "use_armature_deform_only": False,
    "bake_anim": True,
    "bake_anim_use_all_bones": True,
    "bake_anim_use_nla_strips": False,
    "bake_anim_use_all_actions": True,
    "bake_anim_force_startend_keying": True,
    "bake_anim_step": 1.0,
    "bake_anim_simplify_factor": 0.0,
    "path_mode": "STRIP",
    "use_custom_props": False,
    "embed_textures": False,
}


HAND_BONES = {
    "LeftHand", "LeftHandIndex1", "LeftHandIndex2", "LeftHandIndex3",
    "RightHand", "RightHandIndex1", "RightHandIndex2", "RightHandIndex3",
}

FOOT_BONES = {"LeftFoot", "LeftToeBase", "RightFoot", "RightToeBase"}

JOINT_PAIRS = (
    ("LeftShoulder", "LeftArm"),
    ("LeftArm", "LeftForeArm"),
    ("RightShoulder", "RightArm"),
    ("RightArm", "RightForeArm"),
    ("LeftUpLeg", "LeftLeg"),
    ("LeftLeg", "LeftFoot"),
    ("RightUpLeg", "RightLeg"),
    ("RightLeg", "RightFoot"),
    ("Spine1", "Spine2"),
    ("Neck", "Head"),
)


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--texture-dir", required=True)
    parser.add_argument("--target-triangles", type=int, required=True)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return parser.parse_args(argv)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def suffix(name):
    return name.split(":")[-1]


def triangle_count(mesh):
    return sum(max(1, len(poly.vertices) - 2) for poly in mesh.polygons)


def zero_weight_count(obj):
    deform_indices = {
        group.index for group in obj.vertex_groups
        if suffix(group.name) not in {"R3_Preserve"}
    }
    count = 0
    for vertex in obj.data.vertices:
        total = sum(
            item.weight for item in vertex.groups
            if item.group in deform_indices
        )
        if total <= 1e-6:
            count += 1
    return count


def collect_metrics(armature, mesh_obj):
    mesh = mesh_obj.data
    return {
        "vertices": len(mesh.vertices),
        "triangles": triangle_count(mesh),
        "polygons": len(mesh.polygons),
        "uvLayers": [layer.name for layer in mesh.uv_layers],
        "materialSlots": [
            slot.material.name if slot.material else None
            for slot in mesh_obj.material_slots
        ],
        "vertexGroups": len(mesh_obj.vertex_groups),
        "zeroWeightVertices": zero_weight_count(mesh_obj),
        "modifiers": [modifier.type for modifier in mesh_obj.modifiers],
        "boneCount": len(armature.data.bones),
        "rootBones": [bone.name for bone in armature.data.bones if bone.parent is None],
        "boneNames": [bone.name for bone in armature.data.bones],
        "actions": [
            {
                "name": action.name,
                "frameRange": [float(action.frame_range[0]), float(action.frame_range[1])],
            }
            for action in bpy.data.actions
        ],
    }


def extract_texture(texture_dir):
    os.makedirs(texture_dir, exist_ok=True)
    images = [image for image in bpy.data.images if image.type == "IMAGE" and image.size[0] > 0]
    if not images:
        return {"status": "missing"}

    image = max(images, key=lambda candidate: candidate.size[0] * candidate.size[1])
    original_size = [int(image.size[0]), int(image.size[1])]
    master_path = os.path.join(texture_dir, "sensen-basecolor-2048.png")
    runtime_path = os.path.join(texture_dir, "sensen-basecolor-1024.png")

    image.colorspace_settings.name = "sRGB"
    image.file_format = "PNG"
    if tuple(image.size) != (2048, 2048):
        image.scale(2048, 2048)
    image.filepath_raw = master_path
    image.save()
    image.scale(1024, 1024)
    image.filepath_raw = runtime_path
    image.save()
    image.pack()

    return {
        "status": "recovered",
        "imageName": image.name,
        "wasPacked": image.packed_file is not None,
        "originalSize": original_size,
        "master2048": master_path,
        "runtime1024": runtime_path,
        "masterSha256": sha256(master_path),
        "runtimeSha256": sha256(runtime_path),
    }


def protection_thresholds(target_triangles):
    if target_triangles >= 140000:
        return {"faceZ": -0.15, "distalWeight": 0.97, "jointWeight": 0.40}
    if target_triangles >= 95000:
        return {"faceZ": -0.16, "distalWeight": 0.985, "jointWeight": 0.45}
    return {"faceZ": -0.165, "distalWeight": 0.99, "jointWeight": 0.475}


def create_preserve_group(mesh_obj, target_triangles):
    index_name = {group.index: suffix(group.name) for group in mesh_obj.vertex_groups}
    thresholds = protection_thresholds(target_triangles)

    preserve = mesh_obj.vertex_groups.new(name="R3_Preserve")
    category_counts = {"face": 0, "hands": 0, "feet": 0, "joints": 0}
    for vertex in mesh_obj.data.vertices:
        weights = {index_name[item.group]: item.weight for item in vertex.groups}
        face = weights.get("Head", 0.0) >= 0.50 and vertex.co.z < thresholds["faceZ"]
        hands = max((weights.get(name, 0.0) for name in HAND_BONES), default=0.0) >= thresholds["distalWeight"]
        feet = max((weights.get(name, 0.0) for name in FOOT_BONES), default=0.0) >= thresholds["distalWeight"]
        joints = any(
            weights.get(first, 0.0) >= thresholds["jointWeight"] and
            weights.get(second, 0.0) >= thresholds["jointWeight"]
            for first, second in JOINT_PAIRS
        )
        if face:
            category_counts["face"] += 1
        if hands:
            category_counts["hands"] += 1
        if feet:
            category_counts["feet"] += 1
        if joints:
            category_counts["joints"] += 1
        if face or hands or feet or joints:
            preserve.add([vertex.index], 1.0, "REPLACE")
    assigned = sum(1 for vertex in mesh_obj.data.vertices if any(item.group == preserve.index for item in vertex.groups))
    return preserve, {
        "assignedVertices": assigned,
        "categories": category_counts,
        "thresholds": thresholds,
    }


def decimate(mesh_obj, target_triangles):
    before = triangle_count(mesh_obj.data)
    preserve, preserve_report = create_preserve_group(mesh_obj, target_triangles)
    preserve_name = preserve.name
    modifier = mesh_obj.modifiers.new("R3_ProtectedDecimate", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = max(0.001, min(1.0, target_triangles / float(before)))
    modifier.vertex_group = preserve.name
    modifier.invert_vertex_group = True
    modifier.vertex_group_factor = 1.0
    modifier.use_collapse_triangulate = True

    while mesh_obj.modifiers.find(modifier.name) > 0:
        bpy.context.view_layer.objects.active = mesh_obj
        bpy.ops.object.modifier_move_up(modifier=modifier.name)

    bpy.context.view_layer.objects.active = mesh_obj
    mesh_obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh_obj.select_set(False)
    remaining_preserve = mesh_obj.vertex_groups.get(preserve_name)
    if remaining_preserve is not None:
        mesh_obj.vertex_groups.remove(remaining_preserve)
    mesh_obj.data.update()

    after = triangle_count(mesh_obj.data)
    return {
        "sourceTriangles": before,
        "targetTriangles": target_triangles,
        "ratio": target_triangles / float(before),
        "actualTriangles": after,
        "actualRatio": after / float(before),
        "vertexGroupFactor": 1.0,
        "invertedVertexGroup": True,
        "preserveGroup": preserve_report,
    }


def action_curves(action):
    curves = []
    for layer in getattr(action, "layers", []):
        for strip in getattr(layer, "strips", []):
            for bag in getattr(strip, "channelbags", []):
                curves.extend(list(getattr(bag, "fcurves", [])))
    return curves


def key_bone(bone, frame):
    bone.keyframe_insert("location", frame=frame, group=bone.name)
    bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
    bone.keyframe_insert("scale", frame=frame, group=bone.name)


def make_idle(armature):
    scene = bpy.context.scene
    scene.render.fps = 30
    taunt = armature.animation_data.action
    taunt.name = "Sensen_Taunt"
    taunt.use_fake_user = True

    scene.frame_set(1)
    base = {}
    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"
        base[bone.name] = {
            "location": bone.location.copy(),
            "rotation": bone.rotation_quaternion.copy(),
            "scale": bone.scale.copy(),
        }

    idle = bpy.data.actions.new("Sensen_Idle")
    idle.use_fake_user = True
    armature.animation_data.action = idle
    frames = (1, 46, 91)
    for frame in frames:
        scene.frame_set(frame)
        for bone in armature.pose.bones:
            state = base[bone.name]
            bone.location = state["location"]
            bone.rotation_quaternion = state["rotation"]
            bone.scale = state["scale"]

        if frame == 46:
            rotations = {
                "Spine": ((1.0, 0.0, 0.0), math.radians(0.8)),
                "Spine1": ((1.0, 0.0, 0.0), math.radians(1.2)),
                "Spine2": ((1.0, 0.0, 0.0), math.radians(-0.7)),
                "Neck": ((0.0, 0.0, 1.0), math.radians(1.2)),
                "Head": ((0.0, 0.0, 1.0), math.radians(-1.8)),
            }
            for wanted, (axis, angle) in rotations.items():
                bone = next(
                    (item for item in armature.pose.bones if suffix(item.name) == wanted),
                    None,
                )
                if bone is not None:
                    bone.rotation_quaternion = bone.rotation_quaternion @ Quaternion(axis, angle)

        for bone in armature.pose.bones:
            key_bone(bone, frame)

    for curve in action_curves(idle):
        for point in curve.keyframe_points:
            point.interpolation = "BEZIER"

    armature.animation_data.action = taunt
    scene.frame_set(1)
    return {
        "tauntName": taunt.name,
        "tauntFrames": [float(taunt.frame_range[0]), float(taunt.frame_range[1])],
        "idleName": idle.name,
        "idleFrames": [float(idle.frame_range[0]), float(idle.frame_range[1])],
        "fps": scene.render.fps,
        "rootMotionAuthored": False,
        "animationEventsAuthored": 0,
    }


def main():
    args = parse_args()
    input_path = os.path.abspath(args.input)
    output_path = os.path.abspath(args.output)
    report_path = os.path.abspath(args.report)
    texture_dir = os.path.abspath(args.texture_dir)
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    os.makedirs(os.path.dirname(report_path), exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    import_result = bpy.ops.import_scene.fbx(filepath=input_path, use_anim=True)
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(armatures) != 1 or len(meshes) != 1:
        raise RuntimeError(f"Expected one armature and one mesh, got {len(armatures)} / {len(meshes)}")
    armature = armatures[0]
    mesh_obj = meshes[0]

    before = collect_metrics(armature, mesh_obj)
    texture = extract_texture(texture_dir)
    reduction = decimate(mesh_obj, args.target_triangles)
    animation = make_idle(armature)
    after = collect_metrics(armature, mesh_obj)

    export_result = bpy.ops.export_scene.fbx(filepath=output_path, **EXPORT_SETTINGS)
    report = {
        "blenderVersion": bpy.app.version_string,
        "input": input_path,
        "inputSha256": sha256(input_path),
        "output": output_path,
        "outputSha256": sha256(output_path),
        "inputBytes": os.path.getsize(input_path),
        "outputBytes": os.path.getsize(output_path),
        "importResult": sorted(import_result),
        "exportResult": sorted(export_result),
        "before": before,
        "after": after,
        "reduction": reduction,
        "texture": texture,
        "animation": animation,
        "tailRigLimitation": "No tail bones in source asset; non-blocking for R3.0.",
    }
    with open(report_path, "w", encoding="utf-8") as handle:
        json.dump(report, handle, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
