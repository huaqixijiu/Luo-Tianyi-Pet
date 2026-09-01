"""Extract every frame from a video through Blender's bundled decoder."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy


def main() -> None:
    args = sys.argv[sys.argv.index("--") + 1 :]
    video_path = Path(args[0]).resolve()
    output_dir = Path(args[1]).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    clip = bpy.data.movieclips.load(str(video_path))
    width, height = clip.size
    frame_count = clip.frame_duration
    fps = clip.fps

    scene = bpy.context.scene
    editor = scene.sequence_editor_create()
    for strip in list(editor.strips):
        editor.strips.remove(strip)
    editor.strips.new_movie("source", str(video_path), channel=1, frame_start=1)

    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.use_file_extension = True
    scene.render.use_sequencer = True

    for frame in range(1, frame_count + 1):
        scene.frame_set(frame)
        scene.render.filepath = str(output_dir / f"frame_{frame:04d}.png")
        bpy.ops.render.render(write_still=True)

    print(
        "VIDEO_FRAMES="
        + json.dumps(
            {
                "source": str(video_path),
                "width": width,
                "height": height,
                "frameCount": frame_count,
                "fps": fps,
                "outputDirectory": str(output_dir),
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
