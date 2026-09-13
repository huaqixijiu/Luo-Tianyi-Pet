#!/usr/bin/env python3
"""Generate deterministic MSIX logo assets from an official runtime frame."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


SIZES = {
    "StoreLogo.png": 50,
    "Square44x44Logo.png": 44,
    "Square150x150Logo.png": 150,
}


def render_logo(character: Image.Image, size: int) -> Image.Image:
    scale = 4
    canvas_size = size * scale
    canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    padding = round(canvas_size * 0.08)
    target_width = canvas_size - (padding * 2)
    target_height = round(character.height * target_width / character.width)
    if target_height > canvas_size - (padding * 2):
        target_height = canvas_size - (padding * 2)
        target_width = round(character.width * target_height / character.height)
    resized = character.resize((target_width, target_height), Image.Resampling.LANCZOS)
    x = (canvas_size - target_width) // 2
    y = (canvas_size - target_height) // 2
    canvas.alpha_composite(resized, (x, y))

    return canvas.resize((size, size), Image.Resampling.LANCZOS)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--frame-width", type=int, required=True)
    parser.add_argument("--frame-height", type=int, required=True)
    parser.add_argument("--frame-index", type=int, default=0)
    args = parser.parse_args()

    with Image.open(args.source) as source:
        if getattr(source, "n_frames", 1) > 1:
            source.seek(args.frame_index)
            character = source.convert("RGBA")
        else:
            rgba_atlas = source.convert("RGBA")
            columns = rgba_atlas.width // args.frame_width
            x = (args.frame_index % columns) * args.frame_width
            y = (args.frame_index // columns) * args.frame_height
            character = rgba_atlas.crop(
                (x, y, x + args.frame_width, y + args.frame_height))

    # Package logos keep only the official character artwork and place it inside
    # a deterministic Tianyi-blue tile.
    alpha_box = character.getchannel("A").getbbox()
    if alpha_box is None:
        raise RuntimeError("The source frame has no visible pixels.")
    character = character.crop(alpha_box)

    args.output.mkdir(parents=True, exist_ok=True)
    for name, size in SIZES.items():
        output_path = args.output / name
        render_logo(character, size).save(output_path, format="PNG", optimize=True)
        print(f"Generated {output_path} ({size}x{size})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
