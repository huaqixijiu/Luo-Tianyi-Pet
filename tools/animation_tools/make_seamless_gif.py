"""Build a reproducible ping-pong GIF loop from a stable frame range."""

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


def make_seamless_gif(
    source: Path,
    output: Path,
    metadata: Path,
    start_frame: int,
    end_frame: int,
    duration_multiplier: float,
) -> None:
    with Image.open(source) as image:
        frames = [frame.convert("RGBA") for frame in ImageSequence.Iterator(image)]
        durations = [
            int(frame.info.get("duration", image.info.get("duration", 100)) or 100)
            for frame in ImageSequence.Iterator(image)
        ]
        canvas = image.size

    if not 0 <= start_frame < end_frame < len(frames):
        raise ValueError(
            f"Expected 0 <= start_frame < end_frame < {len(frames)}, got "
            f"{start_frame}..{end_frame}."
        )
    if duration_multiplier <= 0:
        raise ValueError("duration_multiplier must be greater than zero.")

    forward = list(range(start_frame, end_frame + 1))
    backward = list(range(end_frame - 1, start_frame, -1))
    frame_indices = forward + backward
    output_frames = [frames[index] for index in frame_indices]
    output_durations = [
        max(1, round(durations[index] * duration_multiplier))
        for index in frame_indices
    ]

    output.parent.mkdir(parents=True, exist_ok=True)
    output_frames[0].save(
        output,
        save_all=True,
        append_images=output_frames[1:],
        duration=output_durations,
        loop=0,
        disposal=2,
        optimize=False,
        transparency=0,
    )

    payload = {
        "schemaVersion": 1,
        "source": source.as_posix(),
        "sourceSha256": sha256(source),
        "output": output.as_posix(),
        "outputSha256": sha256(output),
        "transformation": "stable-range-ping-pong-loop",
        "sourceFrameCount": len(frames),
        "sourceFrameRangeInclusive": [start_frame, end_frame],
        "outputSourceFrameIndices": frame_indices,
        "durationMultiplier": duration_multiplier,
        "frameCount": len(output_frames),
        "canvasWidth": canvas[0],
        "canvasHeight": canvas[1],
        "frameDurationsMilliseconds": output_durations,
        "loop": 0,
    }
    metadata.parent.mkdir(parents=True, exist_ok=True)
    metadata.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("metadata", type=Path)
    parser.add_argument("--start-frame", type=int, required=True)
    parser.add_argument("--end-frame", type=int, required=True)
    parser.add_argument("--duration-multiplier", type=float, default=1.0)
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    make_seamless_gif(
        args.source,
        args.output,
        args.metadata,
        args.start_frame,
        args.end_frame,
        args.duration_multiplier,
    )
