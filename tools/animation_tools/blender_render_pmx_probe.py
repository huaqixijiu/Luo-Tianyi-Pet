from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Render front/back probes for an imported PMX scene.")
    parser.add_argument("--output", type=Path, required=True)
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


def setup_scene() -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = True
    scene.render.resolution_x = 480
    scene.render.resolution_y = 520
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.render.image_settings.color_mode = "RGBA"
    scene.world.color = (0.8, 0.8, 0.8)

    camera_data = bpy.data.cameras.new("PetCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 1.18
    camera = bpy.data.objects.new("PetCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    scene.camera = camera

    add_area_light("Key", (-2.5, -4.0, 4.5), 180.0)
    add_area_light("Fill", (2.5, -2.5, 2.0), 80.0)
    return camera


def main() -> int:
    args = parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    camera = setup_scene()
    target = Vector((0.0, 0.02, 0.50))
    for name, y in (("negative-y", -3.0), ("positive-y", 3.0)):
        camera.location = (0.0, y, 0.50)
        point_at(camera, target)
        bpy.context.scene.render.filepath = str(output / f"probe-{name}.png")
        bpy.ops.render.render(write_still=True)
    print(f"Rendered PMX probes to {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
