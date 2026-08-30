#!/usr/bin/env python3
"""Generate deterministic MSIX logo assets from an official runtime atlas frame."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


SIZES = {
    "StoreLogo.png": 50,
    "Square44x44Logo.png": 44,
    "Square150x150Logo.png": 150,
}


def render_logo(character: Image.Image, size: int) -> Image.Image:
    scale = 4
    canvas_size = size * scale
    top = (236, 250, 255, 255)
    bottom = (102, 204, 255, 255)
    canvas = Image.new("RGBA", (canvas_size, canvas_size))
    pixels = canvas.load()
    for y in range(canvas_size):
        mix = y / max(canvas_size - 1, 1)
        color = tuple(round(top[index] * (1 - mix) + bottom[index] * mix) for index in range(4))
        for x in range(canvas_size):
            pixels[x, y] = color

    draw = ImageDraw.Draw(canvas)
    inset = round(canvas_size * 0.055)
    draw.rounded_rectangle(
        (inset, inset, canvas_size - inset - 1, canvas_size - inset - 1),
        radius=round(canvas_size * 0.24),
        outline=(255, 255, 255, 225),
        width=max(2, round(canvas_size * 0.035)),
    )

    target_width = round(canvas_size * 0.92)
    target_height = round(character.height * target_width / character.width)
    resized = character.resize((target_width, target_height), Image.Resampling.LANCZOS)
    x = (canvas_size - target_width) // 2
    y = round(canvas_size * 0.055)

    shadow = Image.new("RGBA", canvas.size)
    shadow_alpha = resized.getchannel("A").filter(ImageFilter.GaussianBlur(max(1, size // 20) * scale))
    shadow_color = Image.new("RGBA", resized.size, (25, 92, 145, 115))
    shadow_color.putalpha(shadow_alpha.point(lambda alpha: round(alpha * 0.42)))
    shadow.alpha_composite(shadow_color, (x, y + round(canvas_size * 0.035)))
    canvas.alpha_composite(shadow)
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

    with Image.open(args.source) as atlas:
        rgba_atlas = atlas.convert("RGBA")
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
