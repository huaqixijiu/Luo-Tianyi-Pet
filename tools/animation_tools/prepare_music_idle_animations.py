"""Prepare the exact one-minute idle countdown and the stable singing loop."""

from __future__ import annotations

import argparse
import hashlib
import json
from collections import deque
from pathlib import Path

from PIL import Image, ImageSequence


EXTERIOR_SEARCH_DISTANCE = 96
TRANSPARENT_DISTANCE = 32


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_gif(path: Path) -> tuple[list[Image.Image], list[int]]:
    with Image.open(path) as image:
        frames = [frame.convert("RGBA") for frame in ImageSequence.Iterator(image)]
        durations = [
            int(frame.info.get("duration", image.info.get("duration", 100)) or 100)
            for frame in ImageSequence.Iterator(image)
        ]
    return frames, durations


def save_gif(
    path: Path,
    frames: list[Image.Image],
    durations: list[int],
    transparency_index: int | None = None,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    transparency_options = (
        {"transparency": transparency_index}
        if transparency_index is not None
        else {}
    )
    frames[0].save(
        path,
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        disposal=2,
        optimize=False,
        **transparency_options,
    )


def exterior_near_white_mask(frame: Image.Image) -> list[bool]:
    """Find only near-white pixels connected to the canvas boundary.

    The source has an opaque white canvas, but the character also contains
    white clothing and highlights.  A global colour key destroys those inner
    details, so traversal is constrained to the exterior component.
    """

    rgb = frame.convert("RGB")
    width, height = rgb.size
    pixels = rgb.load()
    maximum_distance_squared = EXTERIOR_SEARCH_DISTANCE**2
    exterior = [False] * (width * height)
    pending: deque[tuple[int, int]] = deque()

    for x in range(width):
        pending.append((x, 0))
        pending.append((x, height - 1))
    for y in range(height):
        pending.append((0, y))
        pending.append((width - 1, y))

    while pending:
        x, y = pending.popleft()
        if x < 0 or x >= width or y < 0 or y >= height:
            continue
        offset = y * width + x
        if exterior[offset]:
            continue
        red, green, blue = pixels[x, y]
        distance_squared = (
            (255 - red) ** 2 +
            (255 - green) ** 2 +
            (255 - blue) ** 2
        )
        if distance_squared > maximum_distance_squared:
            continue

        exterior[offset] = True
        pending.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))

    return exterior


def remove_exterior_white_background(frame: Image.Image) -> tuple[Image.Image, list[bool]]:
    rgba = frame.convert("RGBA")
    width, height = rgba.size
    exterior = exterior_near_white_mask(rgba)
    pixels = rgba.load()
    maximum_transparent_distance_squared = TRANSPARENT_DISTANCE**2

    for y in range(height):
        for x in range(width):
            if not exterior[y * width + x]:
                continue
            red, green, blue, _ = pixels[x, y]
            distance_squared = (
                (255 - red) ** 2 +
                (255 - green) ** 2 +
                (255 - blue) ** 2
            )
            if distance_squared <= maximum_transparent_distance_squared:
                pixels[x, y] = (red, green, blue, 0)

    return rgba, exterior


def validate_fishing_transparency(
    source_frames: list[Image.Image],
    output_path: Path,
) -> None:
    output_frames, _ = read_gif(output_path)
    if len(output_frames) != len(source_frames):
        raise ValueError("Fishing output frame count changed during encoding.")

    for index, (source, output) in enumerate(zip(source_frames, output_frames, strict=True)):
        exterior = exterior_near_white_mask(source)
        alpha = output.getchannel("A").tobytes()
        if alpha[0] != 0:
            raise ValueError(f"Fishing frame {index} still has an opaque white canvas.")
        if any(value == 0 and not exterior[offset] for offset, value in enumerate(alpha)):
            raise ValueError(
                f"Fishing frame {index} lost pixels inside the protected character region."
            )


def prepare(
    fishing_source: Path,
    singing_source: Path,
    output_directory: Path,
) -> None:
    fishing_frames, fishing_durations = read_gif(fishing_source)
    if len(fishing_frames) != 70:
        raise ValueError(f"Expected 70 fishing frames, got {len(fishing_frames)}.")

    # Source frame 0 holds 01:00 for five seconds. Frames 1..68 cover 59 seconds,
    # and frame 69 is 00:00. Keeping the final frame for one second makes the
    # visual countdown exactly 60 seconds and aligns the state change to 嘿嘿.
    fishing_indices = list(range(1, 70))
    countdown_durations = fishing_durations[1:69] + [1000]
    if sum(countdown_durations) != 60_000:
        raise ValueError("Fishing countdown must be exactly 60 seconds.")
    fishing_output = output_directory / "十周年生日_摸鱼一分钟_精确60秒.gif"
    fishing_output_frames = [
        remove_exterior_white_background(fishing_frames[index])[0]
        for index in fishing_indices
    ]
    save_gif(
        fishing_output,
        fishing_output_frames,
        countdown_durations,
    )
    validate_fishing_transparency(
        [fishing_frames[index] for index in fishing_indices],
        fishing_output,
    )

    singing_frames, singing_durations = read_gif(singing_source)
    if len(singing_frames) != 16:
        raise ValueError(f"Expected 16 singing frames, got {len(singing_frames)}.")

    # Frames 0..6 are the one-click entrance. Frames 7..15 contain the stable
    # vocal motion, but source frame 15 is visually far from frame 7.  Returning
    # through the original neighbouring frames creates a deterministic ping-pong
    # loop with no generated ghost frames: 7..15, 14..8, then 7 again.
    singing_indices = list(range(7, 16)) + list(range(14, 7, -1))
    if any(
        abs(current - following) != 1
        for current, following in zip(
            singing_indices,
            singing_indices[1:] + singing_indices[:1],
            strict=True,
        )
    ):
        raise ValueError("Singing loop must only cross adjacent source frames.")
    singing_output_durations = [100] * len(singing_indices)
    singing_output = output_directory / "元旦祝福_一键唱歌_无缝循环.gif"
    save_gif(
        singing_output,
        [singing_frames[index] for index in singing_indices],
        singing_output_durations,
        transparency_index=0,
    )

    metadata = {
        "schemaVersion": 1,
        "fishingCountdown": {
            "source": fishing_source.as_posix(),
            "sourceSha256": sha256(fishing_source),
            "output": fishing_output.as_posix(),
            "outputSha256": sha256(fishing_output),
            "sourceFrameIndices": fishing_indices,
            "frameDurationsMilliseconds": countdown_durations,
            "totalDurationMilliseconds": sum(countdown_durations),
            "transformation": (
                "remove-five-second-01:00-hold, normalize-00:00-to-one-second, "
                "and-remove-only-border-connected-white-canvas"
            ),
            "backgroundRemoval": {
                "method": "near-white flood fill seeded only from the canvas boundary",
                "exteriorSearchDistance": EXTERIOR_SEARCH_DISTANCE,
                "transparentDistance": TRANSPARENT_DISTANCE,
                "protectedRegion": "every pixel not connected to the canvas boundary",
            },
        },
        "oneClickSinging": {
            "source": singing_source.as_posix(),
            "sourceSha256": sha256(singing_source),
            "output": singing_output.as_posix(),
            "outputSha256": sha256(singing_output),
            "sourceFrameIndices": singing_indices,
            "frameDurationsMilliseconds": singing_output_durations,
            "totalDurationMilliseconds": sum(singing_output_durations),
            "loopBoundarySourceFrameIndices": [singing_indices[-1], singing_indices[0]],
            "transformation": (
                "remove-one-click-entrance-and-ping-pong-stable-singing-frames-"
                "using-only-adjacent-source-transitions"
            ),
        },
    }
    metadata_path = output_directory / "待机与音乐动画派生.meta.json"
    metadata_path.write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("fishing_source", type=Path)
    parser.add_argument("singing_source", type=Path)
    parser.add_argument("output_directory", type=Path)
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    prepare(args.fishing_source, args.singing_source, args.output_directory)
