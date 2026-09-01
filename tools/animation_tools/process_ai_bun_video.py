"""Convert the approved AI bun-chase video into traceable transparent atlases.

The video model emitted a nearly uniform salmon background, a detached moving
watermark, a soft floor shadow, and one unwanted tooth. This tool keys the
background, clears the corner watermark, attenuates the floor shadow, fills the
small tooth region from the surrounding mouth colour, normalizes every frame to
a fixed transparent canvas, and builds separate running and eating atlases.
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


FRAME_SIZE = 256
ATLAS_COLUMNS = 8
FRAME_DURATION_MS = 42


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def remove_unwanted_tooth(rgb: np.ndarray) -> np.ndarray:
    result = rgb.copy()
    height, width, _ = result.shape
    x0, x1 = int(width * 0.38), int(width * 0.62)
    y0, y1 = int(height * 0.34), int(height * 0.60)
    roi = result[y0:y1, x0:x1]

    red = (
        (roi[:, :, 0] >= 105)
        & (roi[:, :, 0] <= 190)
        & (roi[:, :, 1] >= 45)
        & (roi[:, :, 1] <= 125)
        & (roi[:, :, 2] >= 40)
        & (roi[:, :, 2] <= 120)
    )
    ys, xs = np.where(red)
    if len(xs) < 350:
        return result

    mouth_left, mouth_right = int(xs.min()), int(xs.max())
    mouth_top, mouth_bottom = int(ys.min()), int(ys.max())
    mouth_height = mouth_bottom - mouth_top + 1
    if mouth_height < 24 or mouth_right - mouth_left < 28:
        return result

    mouth_width = mouth_right - mouth_left + 1
    if mouth_width < 45:
        return result

    mouth = roi[mouth_top : mouth_bottom + 1, mouth_left : mouth_right + 1]
    coral_pixels = mouth[red[mouth_top : mouth_bottom + 1, mouth_left : mouth_right + 1]]
    coral = tuple(int(value) for value in np.median(coral_pixels, axis=0))
    outline_pixels = coral_pixels[np.sum(coral_pixels, axis=1) < np.percentile(np.sum(coral_pixels, axis=1), 18)]
    outline = tuple(int(value) for value in np.median(outline_pixels, axis=0))

    # In the approved take the generated tooth is anchored to the upper-left
    # mouth rim.  Paint only that small relative triangle; broad colour-based
    # replacement would risk touching the light hair or skin.
    centre_x = int(mouth_width * 0.28)
    half_width = max(4, int(mouth_width * 0.07))
    tooth_height = max(6, int(mouth_height * 0.24))
    mouth_image = Image.fromarray(mouth, mode="RGB")
    draw = ImageDraw.Draw(mouth_image)
    draw.polygon(
        [
            (centre_x - half_width, 1),
            (centre_x + half_width, 1),
            (centre_x, tooth_height),
        ],
        fill=coral,
    )
    draw.line(
        (centre_x - half_width - 1, 1, centre_x + half_width + 1, 1),
        fill=outline,
        width=max(2, mouth_width // 80),
    )
    roi[mouth_top : mouth_bottom + 1, mouth_left : mouth_right + 1] = np.asarray(mouth_image)
    result[y0:y1, x0:x1] = roi
    return result


def key_character(frame: Image.Image) -> Image.Image:
    rgb = np.asarray(frame.convert("RGB"), dtype=np.uint8)
    rgb = remove_unwanted_tooth(rgb)
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


def normalize_frame(frame: Image.Image, crop: tuple[int, int, int, int]) -> Image.Image:
    cropped = frame.crop(crop)
    cropped.thumbnail((FRAME_SIZE - 8, FRAME_SIZE - 8), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (FRAME_SIZE, FRAME_SIZE), (0, 0, 0, 0))
    x = (FRAME_SIZE - cropped.width) // 2
    y = FRAME_SIZE - cropped.height - 4
    canvas.alpha_composite(cropped, (x, y))
    return canvas


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


def build_atlas(frames: list[Image.Image], output: Path) -> tuple[int, int]:
    rows = math.ceil(len(frames) / ATLAS_COLUMNS)
    atlas = Image.new(
        "RGBA",
        (FRAME_SIZE * ATLAS_COLUMNS, FRAME_SIZE * rows),
        (0, 0, 0, 0),
    )
    for index, frame in enumerate(frames):
        x = index % ATLAS_COLUMNS * FRAME_SIZE
        y = index // ATLAS_COLUMNS * FRAME_SIZE
        atlas.alpha_composite(frame, (x, y))
    output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(output, optimize=True)
    return ATLAS_COLUMNS, rows


def build_picker_preview(frames: list[Image.Image], output: Path) -> None:
    preview_frames: list[Image.Image] = []
    tile = 12
    for frame in frames[::2]:
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
        duration=FRAME_DURATION_MS * 2,
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
    parser.add_argument("--source-video", type=Path, required=True)
    parser.add_argument("--bun-source", type=Path, required=True)
    parser.add_argument("--assets-root", type=Path, required=True)
    parser.add_argument("--metadata", type=Path, required=True)
    parser.add_argument("--preview-output", type=Path)
    args = parser.parse_args()

    frame_paths = sorted(args.frames.glob("frame_*.png"))
    if len(frame_paths) != 121:
        raise RuntimeError(f"Expected 121 extracted frames, found {len(frame_paths)}.")

    keyed = [key_character(Image.open(path)) for path in frame_paths]
    crop = union_bounds(keyed)
    normalized = [normalize_frame(frame, crop) for frame in keyed]
    run_start, run_end = select_run_cycle(normalized, 12, 57)
    run_frames = normalized[run_start:run_end]
    eat_start = 55
    eat_frames = normalized[eat_start:]

    runtime = args.assets_root / "animations" / "runtime"
    run_atlas = runtime / "ai-bun-chase-run.atlas.png"
    eat_atlas = runtime / "ai-bun-eat.atlas.png"
    run_columns, run_rows = build_atlas(run_frames, run_atlas)
    eat_columns, eat_rows = build_atlas(eat_frames, eat_atlas)
    bun_output = args.assets_root / "objects" / "xiaolongbao.png"
    prepare_bun(args.bun_source, bun_output)
    if args.preview_output is not None:
        build_picker_preview(normalized, args.preview_output)

    metadata = {
        "sourceVideo": args.source_video.as_posix(),
        "sourceVideoSha256": sha256(args.source_video),
        "sourceFrameCount": len(frame_paths),
        "sourceFps": 24,
        "frameExtraction": {
            "decoder": "Blender 4.5 VSE",
            "inputColorSpace": "sRGB",
            "displayDevice": "sRGB",
            "viewTransform": "Standard",
            "look": "None",
            "exposure": 0,
            "gamma": 1,
        },
        "backgroundRemoval": {
            "method": "border-fitted colour plane plus sealed-outline interior preservation and corner watermark clearing",
            "transparentBelowDistance": 34,
            "opaqueAtDistance": 58,
            "removeCornerWatermark": True,
            "removeFloorShadow": True,
            "preserveEnclosedCharacterRegions": True,
        },
        "retouch": "remove unintended tooth from open-mouth frames",
        "normalizedFrameSize": [FRAME_SIZE, FRAME_SIZE],
        "run": {
            "sourceFramesOneBased": [run_start + 1, run_end],
            "frameCount": len(run_frames),
            "frameDurationMilliseconds": FRAME_DURATION_MS,
            "atlas": run_atlas.relative_to(args.assets_root).as_posix(),
            "atlasSha256": sha256(run_atlas),
            "columns": run_columns,
            "rows": run_rows,
        },
        "eat": {
            "sourceFramesOneBased": [eat_start + 1, len(frame_paths)],
            "frameCount": len(eat_frames),
            "frameDurationMilliseconds": FRAME_DURATION_MS,
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
    }
    if args.preview_output is not None:
        metadata["pickerPreview"] = {
            "output": args.preview_output.as_posix(),
            "outputSha256": sha256(args.preview_output),
            "frameCount": len(normalized[::2]),
            "frameDurationMilliseconds": FRAME_DURATION_MS * 2,
        }
    args.metadata.parent.mkdir(parents=True, exist_ok=True)
    args.metadata.write_text(json.dumps(metadata, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
