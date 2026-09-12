"""Prepare the exact one-minute idle countdown and the stable singing loop."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageSequence
from fishing_transparency import BACKGROUND_SEEDS, TEXT_BOTTOM, prepare_fishing_frame, prepare_fishing_rgba, validate_fishing_pixels


SINGING_TEXT_BOUNDS = (0, 15, 33, 126)
SINGING_FRAME_DURATION_MILLISECONDS = 160


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


def remove_singing_text(frame: Image.Image) -> Image.Image:
    """Remove only the fixed vertical `一键唱歌` label from a singing frame.

    The official character and the animated music notes begin to the right of
    this narrow strip.  Keeping the operation spatial and deterministic avoids
    recolouring or regenerating any part of the character.
    """

    rgba = frame.convert("RGBA")
    pixels = rgba.load()
    left, top, right, bottom = SINGING_TEXT_BOUNDS
    for y in range(top, bottom):
        for x in range(left, right):
            red, green, blue, _ = pixels[x, y]
            pixels[x, y] = (red, green, blue, 0)
    return rgba


def validate_singing_text_removal(
    source_frames: list[Image.Image],
    output_frames: list[Image.Image],
) -> None:
    if len(source_frames) != len(output_frames):
        raise ValueError("Singing output frame count changed during preparation.")

    left, top, right, bottom = SINGING_TEXT_BOUNDS
    for index, (source, output) in enumerate(
        zip(source_frames, output_frames, strict=True)
    ):
        source_bytes = source.convert("RGBA").tobytes()
        output_rgba = output.convert("RGBA")
        output_bytes = output_rgba.tobytes()
        width, height = output_rgba.size
        for y in range(height):
            for x in range(width):
                offset = (y * width + x) * 4
                if left <= x < right and top <= y < bottom:
                    if output_bytes[offset + 3] != 0:
                        raise ValueError(
                            f"Singing frame {index} still contains the text label."
                        )
                elif output_bytes[offset:offset + 4] != source_bytes[offset:offset + 4]:
                    raise ValueError(
                        f"Singing frame {index} changed outside the text label."
                    )


def validate_fishing_original_pixels(
    source_frames: list[Image.Image],
    output_path: Path,
) -> None:
    output_frames, _ = read_gif(output_path)
    if len(output_frames) != len(source_frames):
        raise ValueError("Fishing output frame count changed during encoding.")
    for index, (source, output) in enumerate(zip(source_frames, output_frames, strict=True)):
        try:
            validate_fishing_pixels(source, output)
        except ValueError as error:
            raise ValueError(f"Fishing frame {index}: {error}") from error

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
        prepare_fishing_frame(fishing_frames[index])
        for index in fishing_indices
    ]
    save_gif(
        fishing_output,
        fishing_output_frames,
        countdown_durations,
        transparency_index=0,
    )
    validate_fishing_original_pixels(
        [fishing_frames[index] for index in fishing_indices],
        fishing_output,
    )
    rgba_frames = [prepare_fishing_rgba(fishing_frames[index]) for index in fishing_indices]
    fishing_rgba_output = output_directory / "十周年生日_摸鱼一分钟_透明无损.webp"
    rgba_frames[0].save(fishing_rgba_output, save_all=True, append_images=rgba_frames[1:],
                        duration=countdown_durations, loop=0, lossless=True, exact=True)
    decoded_rgba, _ = read_gif(fishing_rgba_output)
    if len(decoded_rgba) != len(rgba_frames) or any(
        original.tobytes() != decoded.tobytes()
        for original, decoded in zip(rgba_frames, decoded_rgba, strict=True)
    ):
        raise ValueError("Lossless fishing RGBA output changed during encoding.")

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
    singing_output_durations = [
        SINGING_FRAME_DURATION_MILLISECONDS
    ] * len(singing_indices)
    singing_output = output_directory / "元旦祝福_一键唱歌_无缝循环.gif"
    singing_output_frames = [
        remove_singing_text(singing_frames[index])
        for index in singing_indices
    ]
    validate_singing_text_removal(
        [singing_frames[index] for index in singing_indices],
        singing_output_frames,
    )
    save_gif(
        singing_output,
        singing_output_frames,
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
            "runtimeSource": fishing_rgba_output.as_posix(),
            "runtimeSourceSha256": sha256(fishing_rgba_output),
            "sourceFrameIndices": fishing_indices,
            "frameDurationsMilliseconds": countdown_durations,
            "totalDurationMilliseconds": sum(countdown_durations),
            "transformation": (
                "remove-five-second-01:00-hold, normalize-00:00-to-one-second, "
                "audited-pure-white-background-matte-with-lossless-foreground-palette"
            ),
            "backgroundRemoval": {
                "enabled": True,
                "method": "exact-white flood fill from audited background seeds; no near-white tolerance",
                "backgroundSeeds": BACKGROUND_SEEDS,
                "textRegionBottomExclusive": TEXT_BOTTOM,
                "transparentPaletteIndex": 0,
                "foregroundEncoding": "exact RGB lookup; opaque white uses a separate nonzero index; no quantization",
                "protectedRegion": "all pixels outside the audited background components, including bottom-border clothing",
                "runtimeEdgeMatte": "only light boundary antialias pixels; recover nearby ink and alpha with white-recomposition channel error <= 3; lossless RGBA WebP",
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
            "removedTextBounds": list(SINGING_TEXT_BOUNDS),
            "transformation": (
                "remove-one-click-entrance-and-vertical-text-label, then ping-pong-"
                "stable-singing-frames-using-only-adjacent-source-transitions-at-"
                "a-slower-mouth-motion-rate"
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
