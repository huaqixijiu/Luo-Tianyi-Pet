"""Convert the approved AI bun animation frames into runtime atlases.

The preferred input is the user-reviewed transparent PNG sequence exported
from After Effects.  Its alpha channel is used verbatim: this tool only crops,
scales, positions and packs frames, so it cannot re-key the mouth, tooth, hair
or feet.  The legacy automatic colour-key path remains available solely for
reproducing older builds.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


DEFAULT_FRAME_SIZE = 384
ATLAS_COLUMNS = 8
FRAME_DURATION_MS = 42
SMOOTHED_FRAME_DURATION_MS = 21
SIXTY_FPS_FRAME_DURATION_MS = 16
MINIMUM_RUN_FRAME_COUNT = 60


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256_sequence(paths: list[Path]) -> str:
    """Hash the ordered filenames and bytes as one reproducible source."""
    digest = hashlib.sha256()
    for path in paths:
        digest.update(path.name.encode("utf-8"))
        digest.update(b"\0")
        with path.open("rb") as stream:
            for block in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(block)
    return digest.hexdigest()


def key_character(frame: Image.Image) -> Image.Image:
    rgb = np.asarray(frame.convert("RGB"), dtype=np.uint8)
    height, width, _ = rgb.shape
    yy, xx = np.mgrid[0:height, 0:width]
    x_norm = xx.astype(np.float32) / max(1, width - 1)
    y_norm = yy.astype(np.float32) / max(1, height - 1)
    border_mask = (xx < 18) | (xx >= width - 18) | (yy < 18) | (yy >= height - 18)
    design = np.column_stack(
        (x_norm[border_mask], y_norm[border_mask], np.ones(np.count_nonzero(border_mask)))
    )
    coefficients, _, _, _ = np.linalg.lstsq(
        design,
        rgb[border_mask].astype(np.float32),
        rcond=None,
    )
    background = (
        np.column_stack((x_norm.ravel(), y_norm.ravel(), np.ones(height * width)))
        @ coefficients
    ).reshape(height, width, 3)
    distance = np.sqrt(np.sum((rgb.astype(np.float32) - background) ** 2, axis=2))

    alpha = np.clip((distance - 34.0) / 24.0 * 255.0, 0, 255).astype(np.uint8)
    # The generator moves its detached logo between opposite corners.  Those
    # regions never intersect the centered character and can be removed before
    # the union crop without expensive per-frame component labelling.
    corner_width = int(rgb.shape[1] * 0.28)
    corner_height = int(rgb.shape[0] * 0.15)
    alpha[:corner_height, :corner_width] = 0
    alpha[-corner_height:, -corner_width:] = 0
    # Colour-distance keying alone cannot distinguish the salmon background
    # from deliberately warm details inside the outlined character (most
    # visibly the open mouth).  Preserve every low-confidence region enclosed
    # by the character silhouette and only treat low-confidence pixels that
    # remain connected to the canvas border as background.  Gaps between the
    # arms, legs and hair remain transparent because they are border-connected.
    # Close tiny antialiasing gaps in the dark outline before the exterior
    # flood.  Video compression otherwise leaves one-pixel passages that make
    # the mouth and irises appear connected to the keyed background.
    barrier = alpha >= 112
    for _ in range(2):
        padded = np.pad(barrier, 1, mode="constant", constant_values=False)
        barrier = np.logical_or.reduce(
            [
                padded[dy : dy + height, dx : dx + width]
                for dy in range(3)
                for dx in range(3)
            ]
        )
    traversable = ~barrier
    exterior = np.zeros_like(traversable)
    queue: deque[tuple[int, int]] = deque()
    for x in range(width):
        if traversable[0, x]:
            exterior[0, x] = True
            queue.append((0, x))
        if traversable[height - 1, x] and not exterior[height - 1, x]:
            exterior[height - 1, x] = True
            queue.append((height - 1, x))
    for y in range(height):
        if traversable[y, 0] and not exterior[y, 0]:
            exterior[y, 0] = True
            queue.append((y, 0))
        if traversable[y, width - 1] and not exterior[y, width - 1]:
            exterior[y, width - 1] = True
            queue.append((y, width - 1))
    while queue:
        y, x = queue.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = y + dy, x + dx
            if (
                0 <= ny < height
                and 0 <= nx < width
                and traversable[ny, nx]
                and not exterior[ny, nx]
            ):
                exterior[ny, nx] = True
                queue.append((ny, nx))
    # Erode the sealed silhouette back past the temporary outline dilation.
    # This keeps enclosed facial colours opaque without turning the salmon
    # pixels immediately outside the character into a coloured fringe.
    protected_interior = ~exterior
    for _ in range(3):
        padded = np.pad(protected_interior, 1, mode="constant", constant_values=False)
        protected_interior = np.logical_and.reduce(
            [
                padded[dy : dy + height, dx : dx + width]
                for dy in range(3)
                for dx in range(3)
            ]
        )
    alpha[protected_interior] = 255
    # The generated clip contains a detached dark ellipse under the feet. It
    # is a luminance-only darkening of the fitted background, unlike the blue
    # shoes and their near-black outline. Remove only warm, background-colour
    # pixels in the lower part of the frame so the actual feet stay intact.
    safe_background = np.maximum(background, 1.0)
    background_ratio = rgb.astype(np.float32) / safe_background
    ratio_mean = np.mean(background_ratio, axis=2)
    ratio_spread = np.ptp(background_ratio, axis=2)
    floor_shadow = (
        (y_norm >= 0.80)
        & (ratio_mean >= 0.55)
        & (ratio_mean <= 0.95)
        & (ratio_spread <= 0.16)
    )
    alpha[floor_shadow] = 0
    # Undo the salmon colour mixed into antialiased edge pixels. Without this
    # decontamination a visible red halo remains when WPF composites the
    # transparent atlas over a dark or blue desktop.
    alpha_float = alpha.astype(np.float32) / 255.0
    safe_alpha = np.maximum(alpha_float, 0.08)[:, :, None]
    foreground = (
        rgb.astype(np.float32) -
        (1.0 - alpha_float[:, :, None]) * background
    ) / safe_alpha
    rgb = np.clip(foreground, 0, 255).astype(np.uint8)
    rgb[alpha < 8] = 0
    alpha_image = Image.fromarray(alpha, mode="L").filter(ImageFilter.GaussianBlur(0.45))
    rgba = Image.fromarray(rgb, mode="RGB").convert("RGBA")
    rgba.putalpha(alpha_image)
    return rgba


def union_bounds(frames: list[Image.Image]) -> tuple[int, int, int, int]:
    boxes = [frame.getbbox() for frame in frames]
    boxes = [box for box in boxes if box is not None]
    left = max(0, min(box[0] for box in boxes) - 8)
    top = max(0, min(box[1] for box in boxes) - 8)
    right = min(frames[0].width, max(box[2] for box in boxes) + 8)
    bottom = min(frames[0].height, max(box[3] for box in boxes) + 8)
    return left, top, right, bottom


def normalize_frame(
    frame: Image.Image,
    crop: tuple[int, int, int, int],
    frame_size: int,
) -> Image.Image:
    cropped = frame.crop(crop)
    cropped.thumbnail((frame_size - 12, frame_size - 12), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (frame_size, frame_size), (0, 0, 0, 0))
    x = (frame_size - cropped.width) // 2
    y = frame_size - cropped.height - 6
    canvas.alpha_composite(cropped, (x, y))
    return canvas


def clear_between_feet_floor_residue(frame: Image.Image) -> Image.Image:
    """Clear the last compressed shadow island in the stable shoe gap."""
    result = frame.copy()
    # The unwanted island stays in the same normalized shoe-gap region.  Scale
    # the historical 256 px cleanup box with the selected runtime resolution.
    scale = frame.width / 256
    box = tuple(round(value * scale) for value in (140, 228, 149, 240))
    result.paste((0, 0, 0, 0), box)
    return result


def optical_flow_interpolate(
    left: Image.Image,
    right: Image.Image,
    fractions: list[float],
) -> list[Image.Image]:
    """Interpolate RGBA frames with one shared bidirectional flow solve."""
    try:
        import cv2
    except ImportError as exc:
        raise RuntimeError(
            "optical-flow smoothing requires opencv-python-headless; install "
            "tools/animation_tools/requirements.txt"
        ) from exc

    left_rgba = np.asarray(left, dtype=np.float32) / 255.0
    right_rgba = np.asarray(right, dtype=np.float32) / 255.0
    gray_frames: list[np.ndarray] = []
    for rgba in (left_rgba, right_rgba):
        alpha = rgba[:, :, 3:4]
        # A neutral matte gives optical flow a stable silhouette while avoiding
        # the false high-contrast motion introduced by transparent RGB data.
        composited = rgba[:, :, :3] * alpha + 0.5 * (1.0 - alpha)
        gray_frames.append(
            cv2.cvtColor(
                np.clip(composited * 255.0, 0, 255).astype(np.uint8),
                cv2.COLOR_RGB2GRAY,
            )
        )

    forward = cv2.calcOpticalFlowFarneback(
        gray_frames[0], gray_frames[1], None, 0.5, 5, 25, 5, 7, 1.5, 0
    )
    backward = cv2.calcOpticalFlowFarneback(
        gray_frames[1], gray_frames[0], None, 0.5, 5, 25, 5, 7, 1.5, 0
    )
    yy, xx = np.mgrid[0 : left.height, 0 : left.width].astype(np.float32)

    def warp(rgba: np.ndarray, flow: np.ndarray, amount: float) -> np.ndarray:
        alpha = rgba[:, :, 3:4]
        premultiplied = np.concatenate((rgba[:, :, :3] * alpha, alpha), axis=2)
        return cv2.remap(
            premultiplied,
            xx - amount * flow[:, :, 0],
            yy - amount * flow[:, :, 1],
            cv2.INTER_CUBIC,
            borderMode=cv2.BORDER_CONSTANT,
            borderValue=0,
        )

    output: list[Image.Image] = []
    for fraction in fractions:
        left_warped = warp(left_rgba, forward, fraction)
        right_warped = warp(right_rgba, backward, 1.0 - fraction)
        blended = left_warped * (1.0 - fraction) + right_warped * fraction
        alpha = blended[:, :, 3:4]
        rgb = np.divide(
            blended[:, :, :3],
            alpha,
            out=np.zeros_like(blended[:, :, :3]),
            where=alpha > (1.0 / 255.0),
        )
        rgba = np.concatenate((rgb, alpha), axis=2)
        output.append(
            Image.fromarray(
                np.clip(rgba * 255.0 + 0.5, 0, 255).astype(np.uint8),
                "RGBA",
            )
        )
    return output


def optical_flow_midpoint(left: Image.Image, right: Image.Image) -> Image.Image:
    """Move both RGBA frames halfway before a halo-free composite."""
    return optical_flow_interpolate(left, right, [0.5])[0]


def smooth_frames_2x(frames: list[Image.Image], loop: bool) -> list[Image.Image]:
    """Double temporal samples without changing the animation's total time."""
    if not frames:
        return []
    smoothed: list[Image.Image] = []
    for index, frame in enumerate(frames):
        smoothed.append(frame)
        if index + 1 < len(frames):
            smoothed.append(optical_flow_midpoint(frame, frames[index + 1]))
        elif loop:
            smoothed.append(optical_flow_midpoint(frame, frames[0]))
        else:
            # Keep the final pose for one additional half-frame.  This makes
            # 2N frames at 21 ms exactly match N source frames at 42 ms.
            smoothed.append(frame.copy())
    return smoothed


def smooth_frames_60fps(
    frames: list[Image.Image],
    loop: bool,
    minimum_frame_count: int = 0,
) -> list[Image.Image]:
    """Resample to 62.5 FPS, optionally slowing a short loop to 60 frames."""
    if not frames:
        return []

    source_duration = len(frames) * FRAME_DURATION_MS
    output_count = max(
        minimum_frame_count,
        round(source_duration / SIXTY_FPS_FRAME_DURATION_MS),
    )
    positions = [index * len(frames) / output_count for index in range(output_count)]
    grouped: dict[int, list[tuple[int, float]]] = {}
    output: list[Image.Image | None] = [None] * output_count
    for output_index, position in enumerate(positions):
        left_index = min(len(frames) - 1, int(math.floor(position)))
        fraction = position - left_index
        right_index = (left_index + 1) % len(frames) if loop else min(
            len(frames) - 1,
            left_index + 1,
        )
        if fraction <= 1e-6 or right_index == left_index:
            output[output_index] = frames[left_index].copy()
            continue
        grouped.setdefault(left_index, []).append((output_index, fraction))

    for left_index, requests in grouped.items():
        right_index = (left_index + 1) % len(frames) if loop else min(
            len(frames) - 1,
            left_index + 1,
        )
        interpolated = optical_flow_interpolate(
            frames[left_index],
            frames[right_index],
            [fraction for _, fraction in requests],
        )
        for (output_index, _), frame in zip(requests, interpolated, strict=True):
            output[output_index] = frame

    if any(frame is None for frame in output):
        raise RuntimeError("60 FPS optical-flow resampling left an empty frame.")
    return [frame for frame in output if frame is not None]


def frame_signature(frame: Image.Image) -> np.ndarray:
    preview = frame.resize((48, 48), Image.Resampling.BILINEAR)
    rgba = np.asarray(preview, dtype=np.float32) / 255.0
    return np.concatenate([rgba[:, :, :3] * rgba[:, :, 3:4], rgba[:, :, 3:4]], axis=2)


def select_run_cycle(frames: list[Image.Image], search_start: int, search_end: int) -> tuple[int, int]:
    signatures = [frame_signature(frame) for frame in frames]
    best: tuple[float, int, int] | None = None
    for start in range(search_start, search_end - 14):
        for end in range(start + 14, min(search_end, start + 31)):
            mse = float(np.mean((signatures[start] - signatures[end]) ** 2))
            # Prefer a useful full stride over a coincidentally similar short interval.
            score = mse + abs((end - start) - 20) * 0.00008
            if best is None or score < best[0]:
                best = (score, start, end)
    assert best is not None
    return best[1], best[2]


def build_atlas(
    frames: list[Image.Image],
    output: Path,
    frame_size: int,
) -> tuple[int, int]:
    rows = math.ceil(len(frames) / ATLAS_COLUMNS)
    atlas = Image.new(
        "RGBA",
        (frame_size * ATLAS_COLUMNS, frame_size * rows),
        (0, 0, 0, 0),
    )
    for index, frame in enumerate(frames):
        x = index % ATLAS_COLUMNS * frame_size
        y = index // ATLAS_COLUMNS * frame_size
        atlas.alpha_composite(frame, (x, y))
    output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(output, optimize=True)
    return ATLAS_COLUMNS, rows


def build_picker_preview(frames: list[Image.Image], output: Path) -> None:
    preview_frames: list[Image.Image] = []
    tile = 12
    for frame in frames:
        checker = Image.new("RGB", (192, 192), "#eafafa")
        draw = ImageDraw.Draw(checker)
        for y in range(0, 192, tile):
            for x in range(0, 192, tile):
                if (x // tile + y // tile) % 2:
                    draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill="#dff4f4")
        resized = frame.resize((192, 192), Image.Resampling.LANCZOS)
        checker.paste(resized, mask=resized.getchannel("A"))
        preview_frames.append(checker)
    output.parent.mkdir(parents=True, exist_ok=True)
    preview_frames[0].save(
        output,
        save_all=True,
        append_images=preview_frames[1:],
        duration=FRAME_DURATION_MS,
        loop=0,
        lossless=True,
        method=6,
    )


def prepare_bun(source: Path, output: Path) -> None:
    image = Image.open(source).convert("RGB")
    rgb = np.asarray(image, dtype=np.uint8)
    neutral_light = (rgb.min(axis=2) >= 220) & ((rgb.max(axis=2) - rgb.min(axis=2)) <= 20)
    background = np.zeros_like(neutral_light)
    height, width = neutral_light.shape
    queue: deque[tuple[int, int]] = deque()
    for x in range(width):
        if neutral_light[0, x]:
            background[0, x] = True
            queue.append((0, x))
        if neutral_light[height - 1, x]:
            background[height - 1, x] = True
            queue.append((height - 1, x))
    for y in range(height):
        if neutral_light[y, 0]:
            background[y, 0] = True
            queue.append((y, 0))
        if neutral_light[y, width - 1]:
            background[y, width - 1] = True
            queue.append((y, width - 1))
    while queue:
        y, x = queue.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < height and 0 <= nx < width and neutral_light[ny, nx] and not background[ny, nx]:
                background[ny, nx] = True
                queue.append((ny, nx))

    alpha = np.where(background, 0, 255).astype(np.uint8)
    rgba = image.convert("RGBA")
    rgba.putalpha(Image.fromarray(alpha, mode="L").filter(ImageFilter.GaussianBlur(0.35)))
    bbox = rgba.getbbox()
    if bbox is None:
        raise RuntimeError("The bun source did not contain a visible object.")
    rgba = rgba.crop(bbox)
    rgba.thumbnail((72, 72), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (80, 80), (0, 0, 0, 0))
    canvas.alpha_composite(rgba, ((80 - rgba.width) // 2, (80 - rgba.height) // 2))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--frames", type=Path, required=True)
    parser.add_argument(
        "--input-mode",
        choices=("transparent", "auto-key"),
        default="transparent",
        help="Use source alpha verbatim, or reproduce the legacy colour-key path.",
    )
    parser.add_argument("--source-video", type=Path)
    parser.add_argument("--bun-source", type=Path, required=True)
    parser.add_argument("--assets-root", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument("--preview-output", type=Path)
    parser.add_argument(
        "--runtime-stem",
        default="ai-bun",
        help="Stable filename/id prefix; use a new value to keep multiple styles side by side.",
    )
    parser.add_argument(
        "--frame-size",
        type=int,
        default=DEFAULT_FRAME_SIZE,
        help="Square runtime frame size. 384 stays sharp up to the app's 200%% scale.",
    )
    parser.add_argument(
        "--motion-smoothing",
        choices=("none", "flow2x", "flow60"),
        default="flow60",
        help="Use source timing, 2x midpoint smoothing, or 62.5 FPS optical-flow resampling.",
    )
    args = parser.parse_args()

    if args.frame_size < 192 or args.frame_size > 512:
        raise RuntimeError("--frame-size must be between 192 and 512 pixels.")

    frame_paths = sorted(args.frames.glob("*.png"))
    if len(frame_paths) != 121:
        raise RuntimeError(f"Expected 121 PNG frames, found {len(frame_paths)}.")

    if args.input_mode == "transparent":
        keyed = []
        for path in frame_paths:
            frame = Image.open(path)
            if "A" not in frame.mode:
                raise RuntimeError(f"Transparent input has no alpha channel: {path}")
            rgba = frame.convert("RGBA")
            if rgba.getchannel("A").getextrema() == (255, 255):
                raise RuntimeError(f"Transparent input is fully opaque: {path}")
            keyed.append(rgba)
    else:
        keyed = [key_character(Image.open(path)) for path in frame_paths]
    crop = union_bounds(keyed)
    normalized = [normalize_frame(frame, crop, args.frame_size) for frame in keyed]
    if args.input_mode == "auto-key":
        normalized = [clear_between_feet_floor_residue(frame) for frame in normalized]
    run_start, run_end = select_run_cycle(normalized, 12, 57)
    run_frames = normalized[run_start:run_end]
    eat_start = 55
    eat_frames = normalized[eat_start:]
    frame_duration_ms = FRAME_DURATION_MS
    if args.motion_smoothing == "flow2x":
        run_frames = smooth_frames_2x(run_frames, loop=True)
        eat_frames = smooth_frames_2x(eat_frames, loop=False)
        frame_duration_ms = SMOOTHED_FRAME_DURATION_MS
    elif args.motion_smoothing == "flow60":
        run_frames = smooth_frames_60fps(
            run_frames,
            loop=True,
            minimum_frame_count=MINIMUM_RUN_FRAME_COUNT,
        )
        eat_frames = smooth_frames_60fps(eat_frames, loop=False)
        frame_duration_ms = SIXTY_FPS_FRAME_DURATION_MS

    runtime = args.assets_root / "animations" / "runtime"
    run_id = f"{args.runtime_stem}-chase-run"
    eat_id = f"{args.runtime_stem}-eat"
    run_atlas = runtime / f"{run_id}.atlas.png"
    eat_atlas = runtime / f"{eat_id}.atlas.png"
    run_columns, run_rows = build_atlas(run_frames, run_atlas, args.frame_size)
    eat_columns, eat_rows = build_atlas(eat_frames, eat_atlas, args.frame_size)
    bun_output = args.assets_root / "objects" / "xiaolongbao.png"
    prepare_bun(args.bun_source, bun_output)
    if args.preview_output is not None:
        build_picker_preview(normalized, args.preview_output)

    metadata = {
        "sourceSequence": args.frames.as_posix(),
        "sourceSequenceSha256": sha256_sequence(frame_paths),
        "sourceFrameCount": len(frame_paths),
        "sourceFps": 24,
        "sourcePreparation": {
            "inputMode": args.input_mode,
            "backgroundRemoval": (
                "user-reviewed After Effects alpha channel; no runtime keying or cleanup"
                if args.input_mode == "transparent"
                else "automatic fitted-background colour key with enclosed-detail protection"
            ),
            "alphaPolicy": (
                "preserved verbatim before Lanczos normalization"
                if args.input_mode == "transparent"
                else "derived from colour distance; enclosed character details restored"
            ),
            "retouch": (
                "none; preserve the user-approved mouth and tooth"
                if args.input_mode == "transparent"
                else "clear detached corner mark and border-connected floor residue"
            ),
        },
        "normalizedFrameSize": [args.frame_size, args.frame_size],
        "temporalProcessing": {
            "mode": args.motion_smoothing,
            "description": (
                "bidirectional optical-flow midpoint frames with premultiplied alpha; "
                "source duration preserved"
                if args.motion_smoothing == "flow2x"
                else (
                    "bidirectional optical-flow resampling with premultiplied alpha at "
                    "62.5 FPS; run loop slowed to at least 60 frames, eating duration preserved"
                    if args.motion_smoothing == "flow60"
                    else "source frames unchanged"
                )
            ),
            "outputFrameDurationMilliseconds": frame_duration_ms,
        },
        "run": {
            "sourceFramesOneBased": [run_start + 1, run_end],
            "frameCount": len(run_frames),
            "frameDurationMilliseconds": frame_duration_ms,
            "atlas": run_atlas.relative_to(args.assets_root).as_posix(),
            "atlasSha256": sha256(run_atlas),
            "columns": run_columns,
            "rows": run_rows,
        },
        "eat": {
            "sourceFramesOneBased": [eat_start + 1, len(frame_paths)],
            "frameCount": len(eat_frames),
            "frameDurationMilliseconds": frame_duration_ms,
            "atlas": eat_atlas.relative_to(args.assets_root).as_posix(),
            "atlasSha256": sha256(eat_atlas),
            "columns": eat_columns,
            "rows": eat_rows,
        },
        "bun": {
            "source": args.bun_source.as_posix(),
            "sourceSha256": sha256(args.bun_source),
            "output": bun_output.relative_to(args.assets_root).as_posix(),
            "outputSha256": sha256(bun_output),
        },
        "cropBeforeResize": list(crop),
        "catalogAnimations": [
            {
                "id": run_id,
                "atlas": run_atlas.relative_to(args.assets_root).as_posix(),
                "frameCount": len(run_frames),
                "columns": run_columns,
                "rows": run_rows,
                "frameDurationMilliseconds": frame_duration_ms,
                "loopCount": 0,
                "displayWidth": 205,
                "displayHeight": 205,
            },
            {
                "id": eat_id,
                "atlas": eat_atlas.relative_to(args.assets_root).as_posix(),
                "frameCount": len(eat_frames),
                "columns": eat_columns,
                "rows": eat_rows,
                "frameDurationMilliseconds": frame_duration_ms,
                "loopCount": 1,
                "displayWidth": 205,
                "displayHeight": 205,
            },
        ],
    }
    if args.source_video is not None:
        metadata["legacySourceVideo"] = args.source_video.as_posix()
        metadata["legacySourceVideoSha256"] = sha256(args.source_video)
    if args.preview_output is not None:
        metadata["pickerPreview"] = {
            "output": args.preview_output.as_posix(),
            "outputSha256": sha256(args.preview_output),
            "frameCount": len(normalized),
            "frameDurationMilliseconds": FRAME_DURATION_MS,
        }
    args.metadata.parent.mkdir(parents=True, exist_ok=True)
    args.metadata.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
