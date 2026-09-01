from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def script_args() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Import an MMD PMX model and write a rig report.")
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--blend", type=Path)
    return parser.parse_args(script_args())


def clean_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (
        bpy.data.armatures,
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.cameras,
        bpy.data.lights,
    ):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)


def world_bounds(objects: list[bpy.types.Object]) -> dict[str, list[float]]:
    corners: list[Vector] = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not corners:
        return {"minimum": [0.0, 0.0, 0.0], "maximum": [0.0, 0.0, 0.0]}
    return {
        "minimum": [min(point[index] for point in corners) for index in range(3)],
        "maximum": [max(point[index] for point in corners) for index in range(3)],
    }


def bone_record(pose_bone: bpy.types.PoseBone) -> dict[str, object]:
    mmd = getattr(pose_bone, "mmd_bone", None)
    return {
        "name": pose_bone.name,
        "mmdNameJapanese": getattr(mmd, "name_j", "") if mmd else "",
        "mmdNameEnglish": getattr(mmd, "name_e", "") if mmd else "",
        "parent": pose_bone.parent.name if pose_bone.parent else None,
        "head": list(pose_bone.head),
        "tail": list(pose_bone.tail),
        "rotationMode": pose_bone.rotation_mode,
        "ikConstraintCount": sum(1 for constraint in pose_bone.constraints if constraint.type == "IK"),
    }


def main() -> int:
    args = parse_args()
    model = args.model.resolve()
    report = args.report.resolve()
    if not model.is_file():
        raise FileNotFoundError(model)

    clean_scene()
    result = bpy.ops.mmd_tools.import_model(
        filepath=str(model),
        types={"MESH", "ARMATURE", "MORPHS"},
        scale=0.08,
        clean_model=False,
        remove_doubles=False,
        fix_bone_order=True,
        rename_bones=False,
        use_mipmap=True,
        log_level="INFO",
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"PMX import failed: {result}")

    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    payload = {
        "blenderVersion": bpy.app.version_string,
        "model": str(model),
        "objectCount": len(objects),
        "meshCount": len(meshes),
        "armatureCount": len(armatures),
        "worldBounds": world_bounds(objects),
        "armatures": [
            {
                "name": armature.name,
                "boneCount": len(armature.pose.bones),
                "bones": [bone_record(bone) for bone in armature.pose.bones],
            }
            for armature in armatures
        ],
        "meshes": [
            {
                "name": mesh.name,
                "vertexCount": len(mesh.data.vertices),
                "materials": [slot.material.name if slot.material else None for slot in mesh.material_slots],
                "shapeKeys": list(mesh.data.shape_keys.key_blocks.keys()) if mesh.data.shape_keys else [],
            }
            for mesh in meshes
        ],
    }
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    if args.blend:
        blend = args.blend.resolve()
        blend.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=str(blend))

    print(
        f"Imported {model.name}: {len(meshes)} meshes, "
        f"{len(armatures)} armatures, report={report}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
