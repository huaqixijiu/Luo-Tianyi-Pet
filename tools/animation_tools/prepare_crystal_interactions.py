"""Prepare the user-approved crystal-dress interaction frame sequences.

The 720x720 source PNGs stay in the candidate archive and are intentionally
ignored by Git. This script creates compact, deterministic runtime atlases,
picker previews and provenance metadata from those local source sequences.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


@dataclass(frozen=True)
class Action:
    order: int
    folder: str
    subfolder: str
    animation_id: str
    title: str


ACTIONS = (
    Action(1, "捂嘴", "1", "crystal-cover-mouth", "捂嘴"),
    Action(2, "手比心", "2", "crystal-hand-heart", "手比心"),
    Action(3, "摸腿脚", "3", "crystal-touch-leg", "摸腿脚"),
    Action(4, "捂肚子", "4", "crystal-hold-belly", "捂肚子"),
    Action(5, "摸摸头", "5", "crystal-headpat", "摸摸头"),
    Action(6, "遮眼睛", "6", "crystal-cover-eyes", "遮眼睛"),
    Action(7, "捏脸", "7", "crystal-pinch-cheeks", "捏脸"),
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_sequence(root: Path, paths: list[Path]) -> str:
    digest = hashlib.sha256()
    for path in paths:
        digest.update(path.relative_to(root).as_posix().encode("utf-8"))
        digest.update(b"\0")
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        digest.update(b"\0")
    return digest.hexdigest()


def resize_premultiplied(image: Image.Image, size: int) -> Image.Image:
    # Resizing premultiplied RGBA prevents hidden background RGB from bleeding
    # into anti-aliased hair, dress and hand edges.
    return (
        image.convert("RGBa")
        .resize((size, size), Image.Resampling.LANCZOS)
        .convert("RGBA")
    )


def save_atlas(frames: list[Image.Image], path: Path, columns: int) -> tuple[int, int]:
    rows = math.ceil(len(frames) / columns)
    width, height = frames[0].size
    atlas = Image.new("RGBA", (width * columns, height * rows), (0, 0, 0, 0))
    for index, frame in enumerate(frames):
        atlas.alpha_composite(frame, ((index % columns) * width, (index // columns) * height))
    path.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(path, format="PNG", optimize=False, compress_level=6)
    return columns, rows


def save_preview(frames: list[Image.Image], path: Path, duration_ms: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        path,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=duration_ms,
        loop=0,
        lossless=False,
        quality=90,
        method=3,
        exact=True,
    )


def prepare(root: Path, frame_size: int, frame_duration_ms: int, columns: int) -> None:
    source_root = root / "候选素材_官方" / "区域标注" / "第二模型" / "动作动画png"
    preview_root = root / "候选素材_官方" / "区域标注" / "第二模型" / "动作动画归档"
    runtime_root = root / "assets" / "animations" / "runtime"
    metadata_path = root / "assets" / "animations" / "processed" / "晶蓝礼服_互动动作.meta.json"

    metadata_actions: list[dict[str, object]] = []
    catalog_animations: list[dict[str, object]] = []
    for action in ACTIONS:
        sequence_dir = source_root / action.folder / action.subfolder
        source_frames = sorted(sequence_dir.glob("*.png"))
        if len(source_frames) != 145:
            raise ValueError(
                f"{action.folder} must contain exactly 145 PNG frames; found {len(source_frames)}"
            )

        normalized_frames: list[Image.Image] = []
        for path in source_frames:
            with Image.open(path) as image:
                if image.size != (720, 720):
                    raise ValueError(f"Unexpected frame size for {path}: {image.size}")
                normalized_frames.append(resize_premultiplied(image, frame_size))

        atlas_path = runtime_root / f"{action.animation_id}.atlas.png"
        preview_path = preview_root / f"{action.order:02d}_{action.title}.webp"
        atlas_columns, atlas_rows = save_atlas(normalized_frames, atlas_path, columns)
        save_preview(normalized_frames, preview_path, frame_duration_ms)

        source_dir_relative = sequence_dir.relative_to(root).as_posix()
        atlas_relative = atlas_path.relative_to(root / "assets").as_posix()
        preview_relative = preview_path.relative_to(root).as_posix()
        metadata_actions.append(
            {
                "id": action.animation_id,
                "title": action.title,
                "sourceDirectory": source_dir_relative,
                "sourceFrameCount": len(source_frames),
                "sourceSequenceSha256": sha256_sequence(sequence_dir, source_frames),
                "preview": preview_relative,
                "previewSha256": sha256_file(preview_path),
                "atlas": atlas_relative,
                "atlasSha256": sha256_file(atlas_path),
            }
        )
        catalog_animations.append(
            {
                "id": action.animation_id,
                "sourcePath": source_dir_relative,
                "sourceSha256": metadata_actions[-1]["sourceSequenceSha256"],
                "atlas": atlas_relative,
                "frameCount": len(source_frames),
                "columns": atlas_columns,
                "rows": atlas_rows,
                "frameDurationMilliseconds": frame_duration_ms,
                "loopCount": 1,
                "displayWidth": 238,
                "displayHeight": 238,
            }
        )

    payload = {
        "schemaVersion": 1,
        "model": "full-body-crystal-dress",
        "sourceRoot": source_root.relative_to(root).as_posix(),
        "sourcePreparation": {
            "inputMode": "user-supplied transparent PNG sequence",
            "sourceFrameSize": [720, 720],
            "sourceFps": 24,
            "alphaPolicy": "preserve source alpha; resize in premultiplied RGBA",
            "retouch": "none",
        },
        "normalizedFrameSize": [frame_size, frame_size],
        "actions": metadata_actions,
        "catalogAnimations": catalog_animations,
    }
    metadata_path.parent.mkdir(parents=True, exist_ok=True)
    metadata_path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--frame-size", type=int, default=256)
    parser.add_argument("--frame-duration-ms", type=int, default=42)
    parser.add_argument("--columns", type=int, default=8)
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    prepare(args.root, args.frame_size, args.frame_duration_ms, args.columns)
