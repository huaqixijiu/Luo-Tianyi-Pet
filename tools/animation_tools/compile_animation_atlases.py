"""Compile selected GIF, WebP and PNG sources into deterministic PNG atlases."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
from typing import Any

from PIL import Image, ImageSequence


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def webp_frame_durations(path: Path) -> list[int]:
    data = path.read_bytes()
    if data[:4] != b"RIFF" or data[8:12] != b"WEBP":
        return []

    durations: list[int] = []
    position = 12
    while position + 8 <= len(data):
        tag = data[position : position + 4]
        length = int.from_bytes(data[position + 4 : position + 8], "little")
        payload = data[position + 8 : position + 8 + length]
        if tag == b"ANMF" and len(payload) >= 16:
            durations.append(int.from_bytes(payload[12:15], "little"))
        position += 8 + length + (length & 1)

    return durations


def read_frames(source: Path, default_duration: int) -> tuple[list[Image.Image], list[int]]:
    with Image.open(source) as image:
        frames = [frame.convert("RGBA") for frame in ImageSequence.Iterator(image)]
        durations = [
            int(frame.info.get("duration", image.info.get("duration", 0)) or 0)
            for frame in ImageSequence.Iterator(image)
        ]

    if source.suffix.lower() == ".webp" and not all(durations):
        parsed = webp_frame_durations(source)
        if len(parsed) == len(frames):
            durations = parsed

    durations = [duration if duration > 0 else default_duration for duration in durations]
    return frames, durations


def union_alpha_bounds(frames: list[Image.Image]) -> list[int]:
    left = frames[0].width
    top = frames[0].height
    right = 0
    bottom = 0
    found = False
    for frame in frames:
        bounds = frame.getchannel("A").getbbox()
        if bounds is None:
            continue
        found = True
        left = min(left, bounds[0])
        top = min(top, bounds[1])
        right = max(right, bounds[2])
        bottom = max(bottom, bounds[3])
    return [left, top, right, bottom] if found else [0, 0, 0, 0]


def compile_entry(root: Path, entry: dict[str, Any], maximum_columns: int) -> dict[str, Any]:
    source = root / entry["source"]
    atlas_path = root / entry["atlas"]
    default_duration = int(entry.get("defaultFrameDurationMilliseconds", 100))
    frames, durations = read_frames(source, default_duration)
    duration_scale = float(entry.get("durationScale", 1.0))
    if not math.isfinite(duration_scale) or duration_scale <= 0:
        raise ValueError(f"Invalid durationScale for {entry['id']}")
    durations = [max(1, round(duration * duration_scale)) for duration in durations]
    maximum_frames = int(entry.get("maximumFrames", 0))
    if maximum_frames > 0:
        frames = frames[:maximum_frames]
        durations = durations[:maximum_frames]
    if not frames:
        raise ValueError(f"No frames found in {source}")
    resize_width = int(entry.get("resizeWidth", 0))
    resize_height = int(entry.get("resizeHeight", 0))
    if resize_width > 0 and resize_height > 0:
        frames = [
            frame.resize((resize_width, resize_height), Image.Resampling.LANCZOS)
            for frame in frames
        ]
    if any(frame.size != frames[0].size for frame in frames):
        raise ValueError(f"Frame sizes differ in {source}")
    crop = entry.get("cropAfterResize")
    if crop is not None:
        if (
            not isinstance(crop, list)
            or len(crop) != 4
            or any(not isinstance(value, int) or isinstance(value, bool) for value in crop)
        ):
            raise ValueError(f"Invalid cropAfterResize for {entry['id']}")
        crop_box = tuple(crop)
        if (
            crop_box[0] < 0
            or crop_box[1] < 0
            or crop_box[2] <= crop_box[0]
            or crop_box[3] <= crop_box[1]
            or crop_box[2] > frames[0].width
            or crop_box[3] > frames[0].height
        ):
            raise ValueError(f"cropAfterResize is outside the frame for {entry['id']}")
        frames = [frame.crop(crop_box) for frame in frames]

    padding = entry.get("padAfterResize")
    if padding is not None:
        if (
            not isinstance(padding, list)
            or len(padding) != 4
            or any(not isinstance(value, int) or isinstance(value, bool) for value in padding)
        ):
            raise ValueError(f"Invalid padAfterResize for {entry['id']}")
        canvas_width, canvas_height, offset_x, offset_y = padding
        if (
            canvas_width <= 0
            or canvas_height <= 0
            or offset_x < 0
            or offset_y < 0
            or offset_x + frames[0].width > canvas_width
            or offset_y + frames[0].height > canvas_height
        ):
            raise ValueError(f"padAfterResize cannot contain the frame for {entry['id']}")
        padded_frames: list[Image.Image] = []
        for frame in frames:
            canvas = Image.new("RGBA", (canvas_width, canvas_height), (0, 0, 0, 0))
            canvas.alpha_composite(frame, (offset_x, offset_y))
            padded_frames.append(canvas)
        frames = padded_frames

    columns = min(maximum_columns, len(frames))
    rows = math.ceil(len(frames) / columns)
    frame_width, frame_height = frames[0].size
    atlas = Image.new("RGBA", (frame_width * columns, frame_height * rows), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        x = (index % columns) * frame_width
        y = (index // columns) * frame_height
        atlas.paste(frame, (x, y))

    atlas_path.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(atlas_path, format="PNG", optimize=False, compress_level=9)

    return {
        "id": entry["id"],
        "sourcePath": Path(entry["source"]).as_posix(),
        "sourceSha256": sha256(source),
        "atlasPath": Path(entry["atlas"]).relative_to("assets").as_posix(),
        "atlasSha256": sha256(atlas_path),
        "frameWidth": frame_width,
        "frameHeight": frame_height,
        "columns": columns,
        "rows": rows,
        "frameDurationsMilliseconds": durations,
        "loopCount": int(entry["loopCount"]),
        "displayWidth": int(entry["displayWidth"]),
        "displayHeight": int(entry["displayHeight"]),
        "anchorX": float(entry["anchorX"]),
        "anchorY": float(entry["anchorY"]),
        "alphaBounds": union_alpha_bounds(frames),
    }


def compile_catalog(root: Path, configuration: Path, output: Path) -> None:
    document = json.loads(configuration.read_text(encoding="utf-8"))
    maximum_columns = int(document.get("maximumAtlasColumns", 8))
    animations = [
        compile_entry(root, entry, maximum_columns)
        for entry in document["animations"]
    ]
    for metadata_path_value in document.get("prebuiltAnimationMetadata", []):
        metadata_path = root / metadata_path_value
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        frame_width, frame_height = metadata["normalizedFrameSize"]
        for entry in metadata.get("catalogAnimations", []):
            atlas_path = root / "assets" / entry["atlas"]
            animations.append(
                {
                    "id": entry["id"],
                    "sourcePath": metadata["sourceSequence"],
                    "sourceSha256": metadata["sourceSequenceSha256"],
                    "atlasPath": atlas_path.relative_to(root / "assets").as_posix(),
                    "atlasSha256": sha256(atlas_path),
                    "frameWidth": int(frame_width),
                    "frameHeight": int(frame_height),
                    "columns": int(entry["columns"]),
                    "rows": int(entry["rows"]),
                    "frameDurationsMilliseconds": [
                        int(entry["frameDurationMilliseconds"])
                    ]
                    * int(entry["frameCount"]),
                    "loopCount": int(entry["loopCount"]),
                    "displayWidth": int(entry["displayWidth"]),
                    "displayHeight": int(entry["displayHeight"]),
                    "anchorX": 0.5,
                    "anchorY": 1.0,
                    "alphaBounds": [0, 0, int(frame_width), int(frame_height)],
                }
            )
    payload = {"schemaVersion": 1, "animations": animations}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--config", type=Path, default=Path("config/animation-sources.json"))
    parser.add_argument("--output", type=Path, default=Path("assets/manifests/animations.json"))
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    root = args.root.resolve()
    configuration = args.config if args.config.is_absolute() else root / args.config
    output = args.output if args.output.is_absolute() else root / args.output
    compile_catalog(root, configuration, output)
