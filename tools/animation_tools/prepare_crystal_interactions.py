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
from collections import deque
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw
import numpy as np


@dataclass(frozen=True)
class Action:
    order: int
    source_parts: tuple[str, ...]
    animation_id: str
    title: str
    expected_frames: int
    preview_name: str
    runtime: bool = True
    long_idle_hold_frame: int | None = None


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
        "crystal-long-idle-duck-sit",
        "鸭子坐",
        217,
        "11_鸭子坐.webp",
        long_idle_hold_frame=120,
    ),
    Action(
        12,
        ("模式二新添加动作", "睡觉"),
        "crystal-long-idle-sleep",
        "睡觉",
        361,
        "12_睡觉.webp",
        long_idle_hold_frame=250,
    ),
)


@dataclass(frozen=True)
class Decoration:
    source_name: str
    animation_id: str
    title: str
    expected_frames: int
    sample_step: int
    frame_size: tuple[int, int]
    display_size: tuple[int, int]
    loop_count: int
    end_frame: int | None = None


DECORATIONS = (
    Decoration("zzz", "crystal-sleep-decoration-zzz", "zzz", 241, 2, (180, 180), (90, 90), 0),
    Decoration("梦见包子", "crystal-sleep-decoration-bun", "梦见包子", 145, 2, (240, 180), (108, 81), 0),
    Decoration("梦见乐正绫", "crystal-sleep-decoration-yuezhengling", "梦见乐正绫", 145, 2, (240, 180), (108, 81), 0),
    Decoration("云朵消散", "crystal-sleep-decoration-cloud-dissolve", "云朵消散", 241, 6, (240, 180), (108, 81), 1, end_frame=180),
)

IDLE_DISPLAY_WIDTH = 220
IDLE_DISPLAY_HEIGHT = 238
SOURCE_ACTION_DISPLAY_SIZE = 244
# Match the 220x238 DIP slot at the app's supported 200% pet scale. The 720 px
# masters still have enough source detail for this downsample, so the enlarged
# model no longer has to upscale a smaller intermediate atlas at runtime.
RUNTIME_FRAME_WIDTH = 440
RUNTIME_FRAME_HEIGHT = 476
PREVIEW_FRAME_SIZE = (240, 260)
IN_PLACE_TRANSITION_FRAMES = 6
RUNTIME_WEBP_QUALITY = 95
SLEEP_LETTER_FRAME_RANGE = range(147, 241)
SLEEP_LETTER_CLEAR_BOX = (250, 0, 520, 290)


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


def remove_sleep_letters(image: Image.Image, frame_index: int) -> Image.Image:
    """Remove the detached baked Z glyphs without touching the lying character."""
    rgba = image.convert("RGBA")
    if frame_index in SLEEP_LETTER_FRAME_RANGE:
        rgba.paste((0, 0, 0, 0), SLEEP_LETTER_CLEAR_BOX)
    # Source exports contain coloured RGB under fully transparent pixels. Zeroing
    # it prevents WebP decoders from ever exposing the deleted glyphs as fringe.
    transparent = rgba.getchannel("A").point(lambda value: 255 if value == 0 else 0)
    rgba.paste((0, 0, 0, 0), mask=transparent)
    return rgba


def clear_connected_key_background(image: Image.Image, source_name: str) -> Image.Image:
    """Remove the PR key-colour rectangle only when it connects to a canvas edge."""
    rgba = image.convert("RGBA")
    if source_name == "zzz":
        transparent = rgba.getchannel("A").point(lambda value: 255 if value == 0 else 0)
        rgba.paste((0, 0, 0, 0), mask=transparent)
        return rgba

    key = (245, 245, 245) if source_name == "梦见包子" else (0, 0, 0)
    threshold = 18 if source_name == "梦见包子" else 20
    width, height = rgba.size
    seeds: list[tuple[int, int]] = []
    for x in range(0, width, 24):
        seeds.extend(((x, 0), (x, height - 1)))
    for y in range(0, height, 18):
        seeds.extend(((0, y), (width - 1, y)))

    for seed in seeds:
        red, green, blue, alpha = rgba.getpixel(seed)
        if alpha == 0:
            continue
        if max(abs(red - key[0]), abs(green - key[1]), abs(blue - key[2])) <= 28:
            ImageDraw.floodfill(rgba, seed, (0, 0, 0, 0), thresh=threshold)

    transparent = rgba.getchannel("A").point(lambda value: 255 if value == 0 else 0)
    rgba.paste((0, 0, 0, 0), mask=transparent)
    if source_name == "云朵消散":
        # The exported dissolve was composited over black. Recover its coverage
        # from the premultiplied RGB so fading clouds become transparent rather
        # than turning into dark grey blobs.
        pixels = np.asarray(rgba, dtype=np.float32).copy()
        coverage = pixels[:, :, :3].max(axis=2) / 255.0
        original_alpha = pixels[:, :, 3] / 255.0
        recovered_alpha = coverage * original_alpha
        safe = np.maximum(coverage, 1 / 255.0)
        pixels[:, :, :3] = np.clip(pixels[:, :, :3] / safe[:, :, None], 0, 255)
        pixels[:, :, 3] = np.clip(recovered_alpha * 255.0, 0, 255)
        rgba = Image.fromarray(pixels.astype(np.uint8), "RGBA")
    return rgba


def remove_tiny_alpha_islands(image: Image.Image, minimum_pixels: int = 8) -> Image.Image:
    """Discard isolated keying noise after the decoration is reduced."""
    rgba = image.convert("RGBA")
    pixels = np.asarray(rgba).copy()
    pixels[pixels[:, :, 3] < 10] = (0, 0, 0, 0)
    rgba = Image.fromarray(pixels, "RGBA")
    alpha = np.asarray(rgba.getchannel("A"))
    visible = alpha >= 10
    visited = np.zeros(visible.shape, dtype=bool)
    height, width = visible.shape
    remove = np.zeros(visible.shape, dtype=bool)
    for y in range(height):
        for x in range(width):
            if not visible[y, x] or visited[y, x]:
                continue
            component: list[tuple[int, int]] = []
            pending: deque[tuple[int, int]] = deque([(x, y)])
            visited[y, x] = True
            while pending:
                current_x, current_y = pending.popleft()
                component.append((current_x, current_y))
                for next_x, next_y in (
                    (current_x - 1, current_y),
                    (current_x + 1, current_y),
                    (current_x, current_y - 1),
                    (current_x, current_y + 1),
                ):
                    if (0 <= next_x < width and 0 <= next_y < height and
                        visible[next_y, next_x] and not visited[next_y, next_x]):
                        visited[next_y, next_x] = True
                        pending.append((next_x, next_y))
            if len(component) < minimum_pixels:
                for current_x, current_y in component:
                    remove[current_y, current_x] = True
    if remove.any():
        pixels = np.asarray(rgba).copy()
        pixels[remove] = (0, 0, 0, 0)
        rgba = Image.fromarray(pixels, "RGBA")
    return rgba


def fit_visible_content(
    image: Image.Image,
    union_bounds: tuple[int, int, int, int],
    frame_size: tuple[int, int],
) -> Image.Image:
    cropped = image.crop(union_bounds)
    available = (frame_size[0] - 8, frame_size[1] - 8)
    scale = min(available[0] / cropped.width, available[1] / cropped.height)
    resized = resize_premultiplied_to(
        cropped,
        (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale))),
    )
    canvas = Image.new("RGBA", frame_size, (0, 0, 0, 0))
    canvas.alpha_composite(
        resized,
        ((frame_size[0] - resized.width) // 2, (frame_size[1] - resized.height) // 2),
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


def save_runtime_animation(
    frames: list[Image.Image],
    path: Path,
    duration_ms: int,
    method: int = 3,
) -> tuple[int, int]:
    """Store full-resolution frames with temporal compression.

    Quality 95 keeps the 440x476 source density and exact alpha while removing
    the inter-frame redundancy that a tiled PNG atlas cannot exploit.
    """
    path.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        path,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=duration_ms,
        loop=0,
        lossless=False,
        quality=RUNTIME_WEBP_QUALITY,
        method=method,
        exact=True,
    )
    return 1, len(frames)


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
    start_order: int,
) -> None:
    candidate_root = root / "候选素材_官方"
    preview_root = root / "候选素材_官方" / "区域标注" / "第二模型" / "动作动画归档"
    runtime_root = root / "assets" / "animations" / "runtime"
    metadata_path = root / "assets" / "animations" / "processed" / "晶蓝礼服_互动动作.meta.json"
    frame_size = (frame_width, frame_height)
    idle_reference = make_idle_reference(root, frame_size)
    preview_idle_reference = make_idle_reference(root, PREVIEW_FRAME_SIZE)

    existing_payload = (
        json.loads(metadata_path.read_text(encoding="utf-8"))
        if start_order > 1 and metadata_path.exists()
        else {}
    )
    existing_actions = {
        item.get("id"): item
        for item in existing_payload.get("actions", [])
        if item.get("id")
    }
    existing_catalog = {
        item.get("id"): item
        for item in existing_payload.get("catalogAnimations", [])
        if item.get("id")
    }
    metadata_actions: list[dict[str, object]] = []
    catalog_animations: list[dict[str, object]] = []
    for action in ACTIONS:
        if action.order < start_order:
            if action.animation_id not in existing_actions or action.animation_id not in existing_catalog:
                raise ValueError(
                    f"Cannot preserve {action.animation_id}; existing metadata is incomplete"
                )
            metadata_actions.append(existing_actions[action.animation_id])
            catalog_animations.append(existing_catalog[action.animation_id])
            continue
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
        for frame_index, path in enumerate(source_frames):
            with Image.open(path) as image:
                if image.size != (720, 720):
                    raise ValueError(f"Unexpected frame size for {path}: {image.size}")
                source_image = (
                    remove_sleep_letters(image, frame_index)
                    if action.animation_id == "crystal-long-idle-sleep"
                    else image.convert("RGBA")
                )
                normalized_frames.append(normalize_action_frame(source_image, action_frame_size))
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
        if action.long_idle_hold_frame is not None:
            metadata_action["longIdle"] = {
                "enterStartFrame": 0,
                "holdFrame": action.long_idle_hold_frame,
                "wakeStartFrame": action.long_idle_hold_frame + 1,
                "wakeEndFrame": len(source_frames) - 1,
            }
        if action.animation_id == "crystal-long-idle-sleep":
            metadata_action["removedBakedSleepLetters"] = {
                "sourceFrameRange": [
                    SLEEP_LETTER_FRAME_RANGE.start,
                    SLEEP_LETTER_FRAME_RANGE.stop - 1,
                ],
                "sourcePixelBox": list(SLEEP_LETTER_CLEAR_BOX),
            }
        if not action.runtime:
            metadata_actions.append(metadata_action)
            continue

        atlas_path = runtime_root / f"{action.animation_id}.frames.webp"
        atlas_columns, atlas_rows = save_runtime_animation(
            normalized_frames,
            atlas_path,
            frame_duration_ms,
            method=1 if action.long_idle_hold_frame is not None else 3,
        )
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
                "frameWidth": action_frame_size[0],
                "frameHeight": action_frame_size[1],
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

    decoration_root = candidate_root / "睡觉小装饰"
    for decoration in DECORATIONS:
        sequence_dir = decoration_root / decoration.source_name
        all_paths = sorted(sequence_dir.glob("*.png"))
        if len(all_paths) != decoration.expected_frames:
            raise ValueError(
                f"{decoration.title} must contain exactly {decoration.expected_frames} PNG frames; "
                f"found {len(all_paths)}"
            )
        last_index = decoration.end_frame if decoration.end_frame is not None else len(all_paths) - 1
        selected_paths = all_paths[: last_index + 1 : decoration.sample_step]
        cleaned_frames: list[Image.Image] = []
        union_bounds: tuple[int, int, int, int] | None = None
        for path in selected_paths:
            with Image.open(path) as image:
                cleaned = clear_connected_key_background(image, decoration.source_name)
            bounds = cleaned.getchannel("A").getbbox()
            if bounds is None:
                cleaned_frames.append(cleaned)
                continue
            union_bounds = bounds if union_bounds is None else (
                min(union_bounds[0], bounds[0]),
                min(union_bounds[1], bounds[1]),
                max(union_bounds[2], bounds[2]),
                max(union_bounds[3], bounds[3]),
            )
            cleaned_frames.append(cleaned)
        if union_bounds is None:
            raise ValueError(f"{decoration.title} has no visible pixels after background cleanup")
        runtime_frames = [
            remove_tiny_alpha_islands(
                fit_visible_content(frame, union_bounds, decoration.frame_size)
            )
            for frame in cleaned_frames
        ]
        atlas_path = runtime_root / f"{decoration.animation_id}.frames.webp"
        duration_ms = frame_duration_ms * decoration.sample_step
        if decoration.loop_count == 1:
            # The dissolve is intentionally accelerated to roughly 1.3 seconds.
            duration_ms = frame_duration_ms
        atlas_columns, atlas_rows = save_runtime_animation(
            runtime_frames,
            atlas_path,
            duration_ms,
            method=1,
        )
        source_sequence_hash = sha256_sequence(sequence_dir, all_paths)
        atlas_relative = atlas_path.relative_to(root / "assets").as_posix()
        metadata_actions.append(
            {
                "id": decoration.animation_id,
                "title": decoration.title,
                "status": "runtime-overlay",
                "sourceDirectory": sequence_dir.relative_to(root).as_posix(),
                "sourceFrameCount": len(all_paths),
                "sourceSequenceSha256": source_sequence_hash,
                "selectedFrameCount": len(selected_paths),
                "sampleStep": decoration.sample_step,
                "connectedBackgroundCleanup": (
                    "none; zero hidden RGB" if decoration.source_name == "zzz" else
                    "edge-connected near-white PR block" if decoration.source_name == "梦见包子" else
                    "edge-connected near-black PR block"
                ),
                "sourceAlphaUnionBounds": list(union_bounds),
                "atlas": atlas_relative,
                "atlasSha256": sha256_file(atlas_path),
            }
        )
        catalog_animations.append(
            {
                "id": decoration.animation_id,
                "sourcePath": sequence_dir.relative_to(root).as_posix(),
                "sourceSha256": source_sequence_hash,
                "atlas": atlas_relative,
                "frameCount": len(runtime_frames),
                "frameWidth": decoration.frame_size[0],
                "frameHeight": decoration.frame_size[1],
                "columns": atlas_columns,
                "rows": atlas_rows,
                "frameDurationMilliseconds": duration_ms,
                "loopCount": decoration.loop_count,
                "displayWidth": decoration.display_size[0],
                "displayHeight": decoration.display_size[1],
            }
        )

    payload = {
        "schemaVersion": 1,
        "model": "full-body-crystal-dress",
        "sourceRoots": [
            "候选素材_官方/区域标注/第二模型/动作动画png",
            "候选素材_官方/模式二新添加动作",
            "候选素材_官方/睡觉小装饰",
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
            "runtimeEncoding": (
                f"animated WebP quality {RUNTIME_WEBP_QUALITY}; exact alpha; "
                "full frame count and full 440x476 resolution"
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
            "longIdlePolicy": (
                "duck sit holds source frame 120; sleep holds source frame 250; "
                "sleep Z glyphs are cleared only inside the audited detached-glyph ROI"
            ),
            "decorationPolicy": (
                "remove only key-colour pixels connected to the source canvas edge, "
                "then crop the visible union and resize in premultiplied RGBA; small "
                "overlays are temporally sampled at their original apparent speed"
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
    parser.add_argument(
        "--start-order",
        type=int,
        default=1,
        help="Preserve earlier generated actions from metadata; useful for focused QA rebuilds.",
    )
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    prepare(
        args.root,
        args.frame_width,
        args.frame_height,
        args.frame_duration_ms,
        args.columns,
        args.start_order,
    )
