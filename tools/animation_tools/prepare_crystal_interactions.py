"""Prepare the user-approved crystal-dress interaction frame sequences.

The 720x720 source PNGs stay in the candidate archive and are intentionally
ignored by Git. This script creates compact, deterministic runtime atlases,
picker previews and provenance metadata from those local source sequences.
"""

from __future__ import annotations

import argparse
import bisect
import hashlib
import json
import math
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


@dataclass(frozen=True)
class Action:
    order: int
    source_parts: tuple[str, ...]
    animation_id: str
    title: str
    expected_frames: int
    preview_name: str
    runtime: bool = True


ACTIONS = (
    Action(
        1,
        ("区域标注", "第二模型", "动作动画png", "捂嘴", "1"),
        "crystal-cover-mouth",
        "捂嘴",
        145,
        "01_捂嘴.webp",
    ),
    Action(2, ("模式二新添加动作", "新比心"), "crystal-hand-heart", "新比心", 121, "02_新比心.webp"),
    Action(
        3,
        ("区域标注", "第二模型", "动作动画png", "摸腿脚", "3"),
        "crystal-touch-leg",
        "摸腿脚",
        145,
        "03_摸腿脚.webp",
    ),
    Action(
        4,
        ("区域标注", "第二模型", "动作动画png", "捂肚子", "4"),
        "crystal-hold-belly",
        "捂肚子",
        145,
        "04_捂肚子.webp",
    ),
    Action(
        5,
        ("区域标注", "第二模型", "动作动画png", "摸摸头", "5"),
        "crystal-headpat",
        "摸摸头",
        145,
        "05_摸摸头.webp",
    ),
    Action(
        6,
        ("区域标注", "第二模型", "动作动画png", "遮眼睛", "6"),
        "crystal-cover-eyes",
        "遮眼睛",
        145,
        "06_遮眼睛.webp",
    ),
    Action(
        7,
        ("区域标注", "第二模型", "动作动画png", "捏脸", "7"),
        "crystal-pinch-cheeks",
        "捏脸",
        145,
        "07_捏脸.webp",
    ),
    Action(
        8,
        ("模式二新添加动作", "摸胸"),
        "crystal-touch-chest",
        "摸胸",
        145,
        "08_摸胸.webp",
    ),
    Action(
        9,
        ("模式二新添加动作", "摸裙边"),
        "crystal-touch-skirt",
        "摸裙边",
        145,
        "09_摸裙边.webp",
    ),
    Action(
        10,
        ("模式二新添加动作", "打哈欠"),
        "crystal-yawn",
        "打哈欠",
        169,
        "10_打哈欠.webp",
    ),
    Action(
        11,
        ("模式二新添加动作", "鸭子坐"),
        "",
        "鸭子坐（待接入）",
        217,
        "11_鸭子坐_待接入.webp",
        runtime=False,
    ),
    Action(
        12,
        ("模式二新添加动作", "睡觉"),
        "",
        "睡觉（待接入）",
        361,
        "12_睡觉_待接入.webp",
        runtime=False,
    ),
)

IDLE_DISPLAY_WIDTH = 220
IDLE_DISPLAY_HEIGHT = 238
SOURCE_ACTION_DISPLAY_SIZE = 244
RUNTIME_FRAME_WIDTH = 360
RUNTIME_FRAME_HEIGHT = 390
PREVIEW_FRAME_SIZE = (240, 260)
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


def build_luminance_lut(
    source_reference: Image.Image,
    target_reference: Image.Image,
) -> list[int]:
    """Match source midtones to the real idle artwork without shifting chroma."""
    source_rgba = source_reference.convert("RGBA")
    target_rgba = target_reference.convert("RGBA")
    source_y = source_rgba.convert("RGB").convert("YCbCr").getchannel("Y")
    target_y = target_rgba.convert("RGB").convert("YCbCr").getchannel("Y")
    source_mask = source_rgba.getchannel("A").point(
        [255 if value > 128 else 0 for value in range(256)]
    )
    target_mask = target_rgba.getchannel("A").point(
        [255 if value > 128 else 0 for value in range(256)]
    )
    source_histogram = source_y.histogram(mask=source_mask)
    target_histogram = target_y.histogram(mask=target_mask)
    source_total = sum(source_histogram)
    target_total = sum(target_histogram)
    if source_total == 0 or target_total == 0:
        raise ValueError("Crystal color reference contains no visible pixels")

    target_cdf: list[float] = []
    cumulative = 0
    for count in target_histogram:
        cumulative += count
        target_cdf.append(cumulative / target_total)

    lut: list[int] = []
    cumulative = 0
    for count in source_histogram:
        cumulative += count
        percentile = cumulative / source_total
        lut.append(min(255, bisect.bisect_left(target_cdf, percentile)))
    return lut


def apply_luminance_lut(image: Image.Image, lut: list[int]) -> Image.Image:
    """Apply the calibrated Y channel while preserving Cb, Cr and alpha."""
    rgba = image.convert("RGBA")
    y_channel, cb_channel, cr_channel = rgba.convert("RGB").convert("YCbCr").split()
    corrected_rgb = Image.merge(
        "YCbCr",
        (y_channel.point(lut), cb_channel, cr_channel),
    ).convert("RGB")
    corrected_rgb.putalpha(rgba.getchannel("A"))
    return corrected_rgb


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


def make_preview_frames(frames: list[Image.Image]) -> list[Image.Image]:
    if frames[0].size == PREVIEW_FRAME_SIZE:
        return frames
    return [resize_premultiplied_to(frame, PREVIEW_FRAME_SIZE) for frame in frames]


def prepare(
    root: Path,
    frame_width: int,
    frame_height: int,
    frame_duration_ms: int,
    columns: int,
) -> None:
    candidate_root = root / "候选素材_官方"
    preview_root = root / "候选素材_官方" / "区域标注" / "第二模型" / "动作动画归档"
    runtime_root = root / "assets" / "animations" / "runtime"
    metadata_path = root / "assets" / "animations" / "processed" / "晶蓝礼服_互动动作.meta.json"
    frame_size = (frame_width, frame_height)
    idle_reference = make_idle_reference(root, frame_size)
    preview_idle_reference = make_idle_reference(root, PREVIEW_FRAME_SIZE)

    metadata_actions: list[dict[str, object]] = []
    catalog_animations: list[dict[str, object]] = []
    for action in ACTIONS:
        sequence_dir = candidate_root.joinpath(*action.source_parts)
        source_frames = sorted(sequence_dir.glob("*.png"))
        if len(source_frames) != action.expected_frames:
            raise ValueError(
                f"{action.title} must contain exactly {action.expected_frames} PNG frames; "
                f"found {len(source_frames)}"
            )

        action_frame_size = frame_size if action.runtime else PREVIEW_FRAME_SIZE
        action_idle_reference = idle_reference if action.runtime else preview_idle_reference
        normalized_frames: list[Image.Image] = []
        for path in source_frames:
            with Image.open(path) as image:
                if image.size != (720, 720):
                    raise ValueError(f"Unexpected frame size for {path}: {image.size}")
                normalized_frames.append(normalize_action_frame(image, action_frame_size))
        luminance_lut = build_luminance_lut(normalized_frames[0], action_idle_reference)
        normalized_frames = [
            apply_luminance_lut(frame, luminance_lut)
            for frame in normalized_frames
        ]
        normalized_frames = add_in_place_transitions(
            normalized_frames,
            action_idle_reference,
        )

        preview_path = preview_root / action.preview_name
        save_preview(make_preview_frames(normalized_frames), preview_path, frame_duration_ms)

        source_dir_relative = sequence_dir.relative_to(root).as_posix()
        preview_relative = preview_path.relative_to(root).as_posix()
        metadata_action: dict[str, object] = {
            "id": action.animation_id or None,
            "title": action.title,
            "status": "runtime" if action.runtime else "deferred",
            "sourceDirectory": source_dir_relative,
            "sourceFrameCount": len(source_frames),
            "sourceSequenceSha256": sha256_sequence(sequence_dir, source_frames),
            "preview": preview_relative,
            "previewSha256": sha256_file(preview_path),
            "luminanceLutSha256": hashlib.sha256(bytes(luminance_lut)).hexdigest(),
            "inPlaceTransitionFramesPerEnd": IN_PLACE_TRANSITION_FRAMES,
        }
        if not action.runtime:
            metadata_actions.append(metadata_action)
            continue

        atlas_path = runtime_root / f"{action.animation_id}.atlas.png"
        atlas_columns, atlas_rows = save_atlas(normalized_frames, atlas_path, columns)
        atlas_relative = atlas_path.relative_to(root / "assets").as_posix()
        metadata_action["atlas"] = atlas_relative
        metadata_action["atlasSha256"] = sha256_file(atlas_path)
        metadata_actions.append(metadata_action)
        catalog_animations.append(
            {
                "id": action.animation_id,
                "sourcePath": source_dir_relative,
                "sourceSha256": metadata_action["sourceSequenceSha256"],
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
        "sourceRoots": [
            "候选素材_官方/区域标注/第二模型/动作动画png",
            "候选素材_官方/模式二新添加动作",
        ],
        "sourcePreparation": {
            "inputMode": "user-supplied transparent PNG sequence",
            "sourceFrameSize": [720, 720],
            "sourceFps": 24,
            "alphaPolicy": "preserve source alpha; resize in premultiplied RGBA",
            "colorPolicy": (
                "per-action YCbCr luminance CDF matching from the first source "
                "frame to the actual idle artwork; preserve chroma and alpha"
            ),
            "runtimeCanvasPolicy": (
                f"reframe square source into {frame_width}x{frame_height} "
                "high-resolution idle-aspect canvas; display at fixed "
                "220x238 DIP without runtime offset; retain 240x260 picker previews"
            ),
            "retouch": (
                "match action luminance to idle, then replace six neutral frames "
                "at each end with premultiplied idle-to-action blends; keep "
                "source frame count and duration"
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
