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
    # The source is ordinary sRGB artwork, not a scene-referred 3D render.
    # Blender 4.x defaults to AgX, which tone-maps and visibly darkens the
    # character when the video strip is rendered back to PNG. Standard keeps
    # the decoded video appearance intact for sprite extraction.
    scene.display_settings.display_device = "sRGB"
    scene.sequencer_colorspace_settings.name = "sRGB"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1

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
                "viewTransform": scene.view_settings.view_transform,
                "outputDirectory": str(output_dir),
            },
            ensure_ascii=False,
        )
    )


if __name__ == "__main__":
    main()
