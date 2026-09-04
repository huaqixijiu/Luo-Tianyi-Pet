#!/usr/bin/env python3
"""Build a transparent Windows cursor and auditable preview from a PNG source."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

from PIL import Image


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def normalize(
    source: Image.Image,
    size: int,
    margin: int,
    hotspot_source: tuple[int, int],
) -> tuple[Image.Image, tuple[int, int], tuple[int, int, int, int], tuple[int, int]]:
    rgba = source.convert("RGBA")
    alpha_bounds = rgba.getchannel("A").getbbox()
    if alpha_bounds is None:
        raise RuntimeError("Cursor source has no visible pixels.")

    subject = rgba.crop(alpha_bounds)
    available = size - margin * 2
    scale = min(available / subject.width, available / subject.height)
    resized_size = (
        max(1, round(subject.width * scale)),
        max(1, round(subject.height * scale)),
    )
    subject = subject.resize(resized_size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    paste = ((size - resized_size[0]) // 2, (size - resized_size[1]) // 2)
    canvas.alpha_composite(subject, paste)

    hotspot = (
        round((hotspot_source[0] - alpha_bounds[0]) * scale) + paste[0],
        round((hotspot_source[1] - alpha_bounds[1]) * scale) + paste[1],
    )
    hotspot = (
        min(max(hotspot[0], 0), size - 1),
        min(max(hotspot[1], 0), size - 1),
    )
    return canvas, hotspot, alpha_bounds, resized_size


def write_cur(image: Image.Image, hotspot: tuple[int, int], output: Path) -> None:
    """Write one 32-bit DIB frame in the Windows CUR container."""
    rgba = image.convert("RGBA")
    width, height = rgba.size
    if width > 255 or height > 255:
        raise ValueError("CUR dimensions must be at most 255 pixels.")

    xor_rows = bytearray()
    pixels = rgba.load()
    for y in range(height - 1, -1, -1):
        for x in range(width):
            red, green, blue, alpha = pixels[x, y]
            xor_rows.extend((blue, green, red, alpha))

    mask_stride = ((width + 31) // 32) * 4
    and_rows = bytearray(mask_stride * height)
    for output_row, y in enumerate(range(height - 1, -1, -1)):
        row_offset = output_row * mask_stride
        for x in range(width):
            if pixels[x, y][3] == 0:
                and_rows[row_offset + x // 8] |= 0x80 >> (x % 8)

    bitmap_header = struct.pack(
        "<IiiHHIIiiII",
        40,
        width,
        height * 2,
        1,
        32,
        0,
        len(xor_rows),
        0,
        0,
        0,
        0,
    )
    image_bytes = bitmap_header + xor_rows + and_rows
    directory = struct.pack("<HHH", 0, 2, 1)
    entry = struct.pack(
        "<BBBBHHII",
        width,
        height,
        0,
        0,
        hotspot[0],
        hotspot[1],
        len(image_bytes),
        22,
    )
    output.write_bytes(directory + entry + image_bytes)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    parser.add_argument("--cursor", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument("--hotspot-x", type=int, required=True)
    parser.add_argument("--hotspot-y", type=int, required=True)
    parser.add_argument("--size", type=int, default=64, choices=range(16, 256))
    parser.add_argument("--margin", type=int, default=2)
    args = parser.parse_args()

    root = args.root.resolve()
    source = args.source.resolve()
    preview = args.preview.resolve()
    cursor = args.cursor.resolve()
    metadata = args.metadata.resolve()
    for path in (preview, cursor, metadata):
        path.parent.mkdir(parents=True, exist_ok=True)

    with Image.open(source) as loaded:
        source_size = loaded.size
        normalized, hotspot, alpha_bounds, resized_size = normalize(
            loaded,
            args.size,
            args.margin,
            (args.hotspot_x, args.hotspot_y),
        )

    normalized.save(preview, format="PNG", optimize=False, compress_level=9)
    write_cur(normalized, hotspot, cursor)
    payload = {
        "schemaVersion": 1,
        "source": source.relative_to(root).as_posix(),
        "sourceSha256": sha256(source),
        "preview": preview.relative_to(root).as_posix(),
        "previewSha256": sha256(preview),
        "cursor": cursor.relative_to(root).as_posix(),
        "cursorSha256": sha256(cursor),
        "sourceSize": list(source_size),
        "sourceAlphaBounds": list(alpha_bounds),
        "cursorSize": [args.size, args.size],
        "resizedSize": list(resized_size),
        "sourceHotspot": [args.hotspot_x, args.hotspot_y],
        "cursorHotspot": list(hotspot),
        "transformation": "alpha-bounds-crop-fit-and-32-bit-windows-cur",
    }
    metadata.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(payload, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
