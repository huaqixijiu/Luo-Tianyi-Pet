#!/usr/bin/env python3
"""Prepare a user-supplied full-body illustration for the existing idle slot.

The source is intentionally kept unchanged.  This script removes only the
near-white region connected to the canvas border, normalizes the character to
the established 480x520 runtime canvas, and records enough metadata to audit
or reproduce the conversion.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


CANVAS_SIZE = (480, 520)
MAX_SUBJECT_SIZE = (456, 500)
BACKGROUND_MINIMUM = 248
BACKGROUND_CHANNEL_SPREAD = 12
EDGE_ALPHA_DISTANCE = 24.0


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def exterior_background_mask(
    rgb: np.ndarray,
    background_minimum: int = BACKGROUND_MINIMUM,
    interior_background_boxes: tuple[tuple[int, int, int, int], ...] = (),
) -> tuple[np.ndarray, tuple[int, int, int]]:
    border = np.concatenate(
        (rgb[0, :, :], rgb[-1, :, :], rgb[:, 0, :], rgb[:, -1, :]), axis=0
    )
    background_rgb = tuple(int(value) for value in np.median(border, axis=0))

    channel_spread = rgb.max(axis=2) - rgb.min(axis=2)
    candidate = (rgb.min(axis=2) >= background_minimum) & (
        channel_spread <= BACKGROUND_CHANNEL_SPREAD
    )
    flood_image = Image.fromarray(candidate.astype(np.uint8) * 255, mode="L")

    # The supplied picture has one continuous white canvas.  Seeding every
    # candidate border run also makes the method deterministic for split
    # exterior regions without ever touching enclosed white costume details.
    draw = ImageDraw.Draw(flood_image)
    width, height = flood_image.size
    seeds: set[tuple[int, int]] = set()
    for x in range(width):
        seeds.add((x, 0))
        seeds.add((x, height - 1))
    for y in range(height):
        seeds.add((0, y))
        seeds.add((width - 1, y))

    for seed in seeds:
        if flood_image.getpixel(seed) == 255:
            ImageDraw.floodfill(flood_image, seed, 128, thresh=0)

    exterior = np.asarray(flood_image) == 128
    # Some supplied artwork has a checkerboard flattened into otherwise empty
    # gaps enclosed by hair or translucent decoration lines.  Those areas are
    # not reachable from the canvas border, so callers may mark tightly scoped
    # source rectangles in which near-white checker pixels are also background.
    for left, top, right, bottom in interior_background_boxes:
        exterior[top:bottom, left:right] |= candidate[top:bottom, left:right]
    return exterior, background_rgb


def build_rgba(
    rgb: np.ndarray,
    background_minimum: int = BACKGROUND_MINIMUM,
    interior_background_boxes: tuple[tuple[int, int, int, int], ...] = (),
) -> tuple[Image.Image, tuple[int, int, int], tuple[int, int, int, int]]:
    exterior, background_rgb = exterior_background_mask(
        rgb,
        background_minimum,
        interior_background_boxes,
    )
    foreground = ~exterior

    exterior_image = Image.fromarray(exterior.astype(np.uint8) * 255, mode="L")
    dilated_exterior = np.asarray(exterior_image.filter(ImageFilter.MaxFilter(5))) > 0
    edge_foreground = foreground & dilated_exterior

    alpha = foreground.astype(np.float32) * 255.0
    background = np.asarray(background_rgb, dtype=np.float32)
    color_distance = np.linalg.norm(rgb.astype(np.float32) - background, axis=2)
    soft_alpha = np.clip((color_distance - 1.0) / EDGE_ALPHA_DISTANCE, 0.0, 1.0) * 255.0
    alpha[edge_foreground] = np.minimum(alpha[edge_foreground], soft_alpha[edge_foreground])
    alpha_u8 = np.rint(alpha).astype(np.uint8)

    rgba_rgb = rgb.astype(np.float32)
    semitransparent = (alpha_u8 > 0) & (alpha_u8 < 255)
    if np.any(semitransparent):
        opacity = alpha_u8[semitransparent].astype(np.float32)[:, None] / 255.0
        cleaned = (
            rgba_rgb[semitransparent] - (1.0 - opacity) * background[None, :]
        ) / np.maximum(opacity, 1.0 / 255.0)
        rgba_rgb[semitransparent] = np.clip(cleaned, 0.0, 255.0)

    rgba = np.dstack((np.rint(rgba_rgb).astype(np.uint8), alpha_u8))
    image = Image.fromarray(rgba, mode="RGBA")
    alpha_bbox = image.getchannel("A").getbbox()
    if alpha_bbox is None:
        raise RuntimeError("Background removal produced an empty image.")
    return image, background_rgb, alpha_bbox


def normalize(image: Image.Image, alpha_bbox: tuple[int, int, int, int]) -> tuple[Image.Image, dict[str, object]]:
    margin = 8
    crop_box = (
        max(0, alpha_bbox[0] - margin),
        max(0, alpha_bbox[1] - margin),
        min(image.width, alpha_bbox[2] + margin),
        min(image.height, alpha_bbox[3] + margin),
    )
    subject = image.crop(crop_box)
    scale = min(
        MAX_SUBJECT_SIZE[0] / subject.width,
        MAX_SUBJECT_SIZE[1] / subject.height,
    )
    resized_size = (
        max(1, round(subject.width * scale)),
        max(1, round(subject.height * scale)),
    )
    subject = subject.resize(resized_size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    paste_x = (CANVAS_SIZE[0] - resized_size[0]) // 2
    paste_y = CANVAS_SIZE[1] - 10 - resized_size[1]
    canvas.alpha_composite(subject, (paste_x, paste_y))

    normalized_bbox = canvas.getchannel("A").getbbox()
    if normalized_bbox is None:
        raise RuntimeError("Normalized image is empty.")
    return canvas, {
        "sourceAlphaBounds": list(alpha_bbox),
        "cropBox": list(crop_box),
        "scale": scale,
        "resizedSize": list(resized_size),
        "pasteOffset": [paste_x, paste_y],
        "normalizedAlphaBounds": list(normalized_bbox),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--atlas", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument(
        "--background-minimum",
        type=int,
        default=BACKGROUND_MINIMUM,
        choices=range(0, 256),
        metavar="0-255",
        help="Minimum RGB channel value eligible for border-connected background removal.",
    )
    parser.add_argument(
        "--interior-background-box",
        action="append",
        default=[],
        metavar="LEFT,TOP,RIGHT,BOTTOM",
        help=(
            "Also clear near-white baked checker pixels inside this source-pixel box. "
            "May be repeated; coloured outlines and shading remain untouched."
        ),
    )
    args = parser.parse_args()

    interior_background_boxes: list[tuple[int, int, int, int]] = []
    for value in args.interior_background_box:
        try:
            box = tuple(int(part.strip()) for part in value.split(","))
        except ValueError as exc:
            raise RuntimeError(f"Invalid --interior-background-box: {value}") from exc
        if len(box) != 4:
            raise RuntimeError(f"Invalid --interior-background-box: {value}")
        left, top, right, bottom = box
        if left < 0 or top < 0 or right <= left or bottom <= top:
            raise RuntimeError(f"Invalid --interior-background-box: {value}")
        interior_background_boxes.append((left, top, right, bottom))

    root = args.root.resolve()
    source = args.source.resolve()
    output = args.output.resolve()
    atlas = args.atlas.resolve()
    metadata = args.metadata.resolve()
    for path in (output, atlas, metadata):
        path.parent.mkdir(parents=True, exist_ok=True)

    with Image.open(source) as loaded:
        rgb_image = loaded.convert("RGB")
    rgb = np.asarray(rgb_image)
    if any(
        right > rgb_image.width or bottom > rgb_image.height
        for _, _, right, bottom in interior_background_boxes
    ):
        raise RuntimeError("An --interior-background-box is outside the source image.")
    rgba, background_rgb, alpha_bbox = build_rgba(
        rgb,
        args.background_minimum,
        tuple(interior_background_boxes),
    )
    normalized, geometry = normalize(rgba, alpha_bbox)

    normalized.save(output, format="PNG", optimize=False, compress_level=9)
    normalized.save(atlas, format="PNG", optimize=False, compress_level=9)

    payload = {
        "schemaVersion": 1,
        "source": source.relative_to(root).as_posix(),
        "sourceSha256": sha256(source),
        "output": output.relative_to(root).as_posix(),
        "outputSha256": sha256(output),
        "runtimeAtlas": atlas.relative_to(root / "assets").as_posix(),
        "runtimeAtlasSha256": sha256(atlas),
        "transformation": (
            "border-and-marked-interior-near-white-background-to-alpha-and-fit-canvas"
            if interior_background_boxes
            else "border-connected-near-white-background-to-alpha-and-fit-canvas"
        ),
        "backgroundRemoval": {
            "method": (
                "binary near-white candidates flood-filled from canvas border plus "
                "explicit enclosed-background boxes"
                if interior_background_boxes
                else "binary near-white candidates flood-filled only from canvas border"
            ),
            "backgroundRgbMedian": list(background_rgb),
            "minimumChannelValue": args.background_minimum,
            "maximumChannelSpread": BACKGROUND_CHANNEL_SPREAD,
            "softEdgeWidthPixels": 2,
            "softEdgeColorDistance": EDGE_ALPHA_DISTANCE,
            "edgeDecontamination": "remove estimated white matte from semitransparent boundary pixels",
            "interiorBackgroundBoxes": [list(box) for box in interior_background_boxes],
        },
        "sourceSize": [rgb_image.width, rgb_image.height],
        "canvasSize": list(CANVAS_SIZE),
        "geometry": geometry,
        "frameCount": 1,
        "frameDurationMilliseconds": 1000,
        "loop": 0,
    }
    metadata.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )

    print(json.dumps(payload, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
