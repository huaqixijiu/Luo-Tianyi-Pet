"""Prepare the exact one-minute idle countdown and the stable singing loop."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageSequence


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


def save_gif(path: Path, frames: list[Image.Image], durations: list[int]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        path,
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        disposal=2,
        optimize=False,
        transparency=0,
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
    save_gif(
        fishing_output,
        [fishing_frames[index] for index in fishing_indices],
        countdown_durations,
    )

    singing_frames, singing_durations = read_gif(singing_source)
    if len(singing_frames) != 16:
        raise ValueError(f"Expected 16 singing frames, got {len(singing_frames)}.")

    # Frames 0..6 are the one-click entrance. Frames 7..15 are the stable vocal
    # motion and music-note cycle, so only that range belongs in a continuous loop.
    singing_indices = list(range(7, 16))
    singing_output = output_directory / "元旦祝福_一键唱歌_无缝循环.gif"
    save_gif(
        singing_output,
        [singing_frames[index] for index in singing_indices],
        [singing_durations[index] for index in singing_indices],
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
            "transformation": "remove-five-second-01:00-hold-and-normalize-00:00-to-one-second",
        },
        "oneClickSinging": {
            "source": singing_source.as_posix(),
            "sourceSha256": sha256(singing_source),
            "output": singing_output.as_posix(),
            "outputSha256": sha256(singing_output),
            "sourceFrameIndices": singing_indices,
            "frameDurationsMilliseconds": [singing_durations[index] for index in singing_indices],
            "totalDurationMilliseconds": sum(singing_durations[index] for index in singing_indices),
            "transformation": "remove-one-click-entrance-and-loop-stable-singing-frames",
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
