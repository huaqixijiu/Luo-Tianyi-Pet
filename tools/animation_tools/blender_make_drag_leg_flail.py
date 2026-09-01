from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


FRAME_COUNT = 32
FPS = 24
RENDER_WIDTH = 960
RENDER_HEIGHT = 1040


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Author and render a looping drag leg-flail action.")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--blend", type=Path, required=True)
    parser.add_argument("--preview-frames", action="store_true")
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(args)


def point_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_area_light(name: str, location: tuple[float, float, float], energy: float) -> None:
    data = bpy.data.lights.new(name=name, type="AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = 4.0
    light = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(light)
    light.location = location
    point_at(light, Vector((0.0, 0.0, 0.48)))


def setup_render() -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = True
    scene.render.resolution_x = RENDER_WIDTH
    scene.render.resolution_y = RENDER_HEIGHT
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.fps = FPS
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.world.color = (0.8, 0.8, 0.8)

    for obj in list(scene.objects):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)

    camera_data = bpy.data.cameras.new("PetCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.18
    camera = bpy.data.objects.new("PetCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.0, -3.0, 0.50)
    point_at(camera, Vector((0.0, 0.02, 0.50)))
    scene.camera = camera

    add_area_light("Key", (-2.5, -4.0, 4.5), 180.0)
    add_area_light("Fill", (2.5, -2.5, 2.0), 80.0)


def armature() -> bpy.types.Object:
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def mesh() -> bpy.types.Object:
    return next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")


def reset_pose(rig: bpy.types.Object) -> None:
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)
        for constraint in bone.constraints:
            if constraint.type == "IK":
                constraint.influence = 0.0


def set_rotation(rig: bpy.types.Object, name: str, xyz_degrees: tuple[float, float, float]) -> None:
    bone = rig.pose.bones.get(name)
    if bone is None:
        raise KeyError(f"Required bone not found: {name}")
    bone.rotation_mode = "XYZ"
    bone.rotation_euler = tuple(math.radians(value) for value in xyz_degrees)
    bone.keyframe_insert(data_path="rotation_euler", group=name)


def set_location(rig: bpy.types.Object, name: str, xyz: tuple[float, float, float]) -> None:
    bone = rig.pose.bones.get(name)
    if bone is None:
        raise KeyError(f"Required bone not found: {name}")
    bone.location = xyz
    bone.keyframe_insert(data_path="location", group=name)


def set_morph(model: bpy.types.Object, name: str, value: float) -> None:
    if model.data.shape_keys is None:
        raise RuntimeError("Model has no shape keys")
    key = model.data.shape_keys.key_blocks.get(name)
    if key is None:
        raise KeyError(f"Required morph not found: {name}")
    key.value = value
    key.keyframe_insert(data_path="value", group="Face")


def author_action() -> None:
    rig = armature()
    model = mesh()
    reset_pose(rig)
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = FRAME_COUNT

    action = bpy.data.actions.new("Drag_Leg_Flail")
    rig.animation_data_create()
    rig.animation_data.action = action

    for frame in range(1, FRAME_COUNT + 2):
        phase = 2.0 * math.pi * (frame - 1) / FRAME_COUNT
        swing = math.sin(phase)
        counter = math.cos(phase)
        right_phase = phase + math.pi + 0.28
        right_swing = math.sin(right_phase)
        right_counter = math.cos(right_phase)
        bounce = 0.5 - 0.5 * math.cos(2.0 * phase)
        left_fold = 0.5 + 0.5 * math.sin(phase + math.pi / 3.0)
        right_fold = 0.5 + 0.5 * math.sin(right_phase + math.pi / 3.0)
        scene.frame_set(frame)

        set_location(rig, "センター", (0.013 * counter, 0.0, 0.028 + 0.014 * bounce))
        set_rotation(rig, "下半身", (3.0 * counter, 0.0, -7.0 * swing))
        set_rotation(rig, "上半身", (-3.5 * counter, 0.0, 5.5 * swing))
        set_rotation(rig, "上半身2", (2.0 * counter, 0.0, -7.0 * swing))
        set_rotation(rig, "頭", (-2.5 * counter, 0.0, 9.0 * swing))

        set_rotation(rig, "左足", (28.0 * counter, 0.0, 34.0 * swing - 7.0))
        set_rotation(rig, "左ひざ", (-52.0 * left_fold, 0.0, -24.0 * swing))
        set_rotation(rig, "左足首", (24.0 * left_fold, 0.0, 17.0 * swing))
        set_rotation(rig, "右足", (28.0 * right_counter, 0.0, 32.0 * right_swing + 7.0))
        set_rotation(rig, "右ひざ", (-52.0 * right_fold, 0.0, -24.0 * right_swing))
        set_rotation(rig, "右足首", (24.0 * right_fold, 0.0, 17.0 * right_swing))

        set_rotation(rig, "左腕", (-6.0 * counter, 0.0, -8.0 - 7.0 * swing))
        set_rotation(rig, "左ひじ", (0.0, 0.0, -8.0 + 6.0 * counter))
        set_rotation(rig, "右腕", (6.0 * counter, 0.0, 8.0 + 7.0 * swing))
        set_rotation(rig, "右ひじ", (0.0, 0.0, 8.0 - 6.0 * counter))

        set_rotation(rig, "左发_01_01", (0.0, 0.0, -6.0 * swing))
        set_rotation(rig, "右发_01_01", (0.0, 0.0, -6.0 * swing))
        set_rotation(rig, "左八字辫_01_01", (0.0, 0.0, -8.0 * swing))
        set_rotation(rig, "右八字辫_01_01", (0.0, 0.0, -8.0 * swing))

        blink = max(0.0, 1.0 - abs(frame - 17) / 1.5)
        set_morph(model, "困る", 0.42)
        set_morph(model, "あ", 0.28)
        set_morph(model, "まばたき", blink)

    for fcurve in action.fcurves:
        for keyframe in fcurve.keyframe_points:
            keyframe.interpolation = "LINEAR"


def render_frames(output: Path, preview_only: bool) -> None:
    output.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    frames = (1, 5, 9, 13, 17, 21, 25, 29) if preview_only else range(1, FRAME_COUNT + 1)
    for frame in frames:
        scene.frame_set(frame)
        scene.render.filepath = str(output / f"drag-leg-flail-{frame:03d}.png")
        bpy.ops.render.render(write_still=True)
        print(f"Rendered frame {frame}")


def main() -> int:
    args = parse_args()
    setup_render()
    author_action()
    blend = args.blend.resolve()
    blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    render_frames(args.output.resolve(), args.preview_frames)
    print(f"Created drag leg-flail action: {FRAME_COUNT} frames at {FPS} fps")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
