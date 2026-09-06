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

IDLE_DISPLAY_WIDTH = 220
IDLE_DISPLAY_HEIGHT = 238
SOURCE_ACTION_DISPLAY_SIZE = 244
RUNTIME_FRAME_WIDTH = 240
RUNTIME_FRAME_HEIGHT = 260
IN_PLACE_TRANSITION_FRAMES = 6


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


def resize_premultiplied_to(
    image: Image.Image,
    size: tuple[int, int],
) -> Image.Image:
    return image.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")


def make_idle_reference(root: Path, frame_size: tuple[int, int]) -> Image.Image:
    """Resize the actual idle artwork into the identical runtime frame slot."""
    idle_path = (
        root
        / "assets"
        / "animations"
        / "processed"
        / "用户提供_Q版小人全身_透明.png"
    )
    with Image.open(idle_path) as idle:
        return resize_premultiplied_to(idle, frame_size)


def normalize_action_frame(
    image: Image.Image,
    frame_size: tuple[int, int],
) -> Image.Image:
    """Reframe a square source into the idle slot without changing DIP scale."""
    frame_width, frame_height = frame_size
    render_width = frame_width * SOURCE_ACTION_DISPLAY_SIZE / IDLE_DISPLAY_WIDTH
    render_height = frame_height * SOURCE_ACTION_DISPLAY_SIZE / IDLE_DISPLAY_HEIGHT
    render_size = round((render_width + render_height) / 2)
    resized = resize_premultiplied_to(image, (render_size, render_size))

    canvas = Image.new("RGBA", frame_size, (0, 0, 0, 0))
    canvas.alpha_composite(
        resized,
        ((frame_width - render_size) // 2, 1),
    )
    return canvas


def blend_premultiplied(
    first: Image.Image,
    second: Image.Image,
    second_weight: float,
) -> Image.Image:
    return Image.blend(
        first.convert("RGBa"),
        second.convert("RGBa"),
        second_weight,
    ).convert("RGBA")


def add_in_place_transitions(
    frames: list[Image.Image],
    idle_reference: Image.Image,
) -> list[Image.Image]:
    """Make frame zero/final exactly idle and blend the neighboring frames.

    Existing neutral lead-in/out frames are replaced rather than appended, so
    the source frame count, timing, atlas dimensions and action duration stay
    unchanged.
    """
    if len(frames) <= IN_PLACE_TRANSITION_FRAMES * 2:
        raise ValueError("Crystal action does not have enough frames for transitions")

    result = list(frames)
    for index in range(IN_PLACE_TRANSITION_FRAMES + 1):
        weight = index / IN_PLACE_TRANSITION_FRAMES
        result[index] = blend_premultiplied(idle_reference, frames[index], weight)

    outro_start = len(frames) - IN_PLACE_TRANSITION_FRAMES - 1
    for index in range(outro_start, len(frames)):
        weight = (index - outro_start) / IN_PLACE_TRANSITION_FRAMES
        result[index] = blend_premultiplied(frames[index], idle_reference, weight)

    return result


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


def prepare(
    root: Path,
    frame_width: int,
    frame_height: int,
    frame_duration_ms: int,
    columns: int,
) -> None:
    source_root = root / "候选素材_官方" / "区域标注" / "第二模型" / "动作动画png"
    preview_root = root / "候选素材_官方" / "区域标注" / "第二模型" / "动作动画归档"
    runtime_root = root / "assets" / "animations" / "runtime"
    metadata_path = root / "assets" / "animations" / "processed" / "晶蓝礼服_互动动作.meta.json"
    frame_size = (frame_width, frame_height)
    idle_reference = make_idle_reference(root, frame_size)

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
                normalized_frames.append(normalize_action_frame(image, frame_size))
        normalized_frames = add_in_place_transitions(normalized_frames, idle_reference)

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
                "inPlaceTransitionFramesPerEnd": IN_PLACE_TRANSITION_FRAMES,
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
                # Using the exact idle display slot prevents any WPF window
                # resize or desktop-coordinate rounding during the reaction.
                "displayWidth": IDLE_DISPLAY_WIDTH,
                "displayHeight": IDLE_DISPLAY_HEIGHT,
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
            "runtimeCanvasPolicy": (
                "reframe square source into 240x260 idle-aspect canvas; "
                "display at fixed 220x238 DIP without runtime offset"
            ),
            "retouch": (
                "replace six neutral frames at each end with premultiplied "
                "idle-to-action blends; keep 145-frame duration"
            ),
            "idleReference": (
                "assets/animations/processed/用户提供_Q版小人全身_透明.png"
            ),
            "inPlaceTransitionFramesPerEnd": IN_PLACE_TRANSITION_FRAMES,
        },
        "normalizedFrameSize": [frame_width, frame_height],
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
    parser.add_argument("--frame-width", type=int, default=RUNTIME_FRAME_WIDTH)
    parser.add_argument("--frame-height", type=int, default=RUNTIME_FRAME_HEIGHT)
    parser.add_argument("--frame-duration-ms", type=int, default=42)
    parser.add_argument("--columns", type=int, default=8)
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    prepare(
        args.root,
        args.frame_width,
        args.frame_height,
        args.frame_duration_ms,
        args.columns,
    )
