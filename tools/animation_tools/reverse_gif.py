"""Create a reproducible reversed GIF and a provenance sidecar."""

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


def reverse_gif(source: Path, output: Path, metadata: Path) -> None:
    with Image.open(source) as image:
        frames = [frame.convert("RGBA") for frame in ImageSequence.Iterator(image)]
        durations = [
            int(frame.info.get("duration", image.info.get("duration", 100)))
            for frame in ImageSequence.Iterator(image)
        ]
        loop = int(image.info.get("loop", 0))
        canvas = image.size

    if len(frames) < 2:
        raise ValueError("The source GIF must contain at least two frames.")

    output.parent.mkdir(parents=True, exist_ok=True)
    reversed_frames = list(reversed(frames))
    reversed_durations = list(reversed(durations))
    reversed_frames[0].save(
        output,
        save_all=True,
        append_images=reversed_frames[1:],
        duration=reversed_durations,
        loop=loop,
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
        "transformation": "reverse-frame-order",
        "frameCount": len(reversed_frames),
        "canvasWidth": canvas[0],
        "canvasHeight": canvas[1],
        "frameDurationsMilliseconds": reversed_durations,
        "loop": loop,
    }
    metadata.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("metadata", type=Path)
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    reverse_gif(args.source, args.output, args.metadata)
