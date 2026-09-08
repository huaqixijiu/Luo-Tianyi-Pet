"""Create the text-free drag expansion animation from the official GIF."""

from __future__ import annotations

import argparse
import colorsys
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageSequence


SOURCE = Path(
    "候选素材_官方/03_官方表情包/5582-心律共鸣动态表情包/"
    "心律共鸣动态表情包_膨胀.gif"
)
OUTPUT = Path("assets/animations/processed/心律共鸣_膨胀_无文字.png")
METADATA = Path("assets/animations/processed/心律共鸣_膨胀_无文字.meta.json")

ERASE_BOTTOM = 68
MINIMUM_RED = 100
MINIMUM_HUE_DEGREES = 12
MAXIMUM_HUE_DEGREES = 42
MINIMUM_SATURATION = 0.22


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def is_orange_text(red: int, green: int, blue: int, alpha: int) -> bool:
    if alpha == 0 or red <= MINIMUM_RED:
        return False

    hue, saturation, _ = colorsys.rgb_to_hsv(
        red / 255,
        green / 255,
        blue / 255,
    )
    hue_degrees = hue * 360
    return (
        MINIMUM_HUE_DEGREES <= hue_degrees <= MAXIMUM_HUE_DEGREES
        and saturation >= MINIMUM_SATURATION
    )


def prepare(root: Path) -> None:
    source = root / SOURCE
    output = root / OUTPUT
    metadata = root / METADATA
    frames: list[Image.Image] = []
    durations: list[int] = []
    removed_pixels: list[int] = []

    with Image.open(source) as image:
        for source_frame in ImageSequence.Iterator(image):
            frame = source_frame.convert("RGBA")
            pixels = frame.load()
            removed = 0
            for y in range(min(ERASE_BOTTOM, frame.height)):
                for x in range(frame.width):
                    pixel = pixels[x, y]
                    if is_orange_text(*pixel):
                        pixels[x, y] = (*pixel[:3], 0)
                        removed += 1

            frames.append(frame)
            durations.append(
                int(source_frame.info.get("duration", image.info.get("duration", 100)) or 100)
            )
            removed_pixels.append(removed)

    output.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        output,
        format="PNG",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        disposal=0,
        blend=0,
        optimize=False,
        compress_level=9,
    )
    payload = {
        "schemaVersion": 1,
        "source": SOURCE.as_posix(),
        "sourceSha256": sha256(source),
        "output": OUTPUT.as_posix(),
        "outputSha256": sha256(output),
        "frameCount": len(frames),
        "frameSize": list(frames[0].size),
        "frameDurationsMilliseconds": durations,
        "transformation": {
            "kind": "erase-orange-title-pixels-in-top-region",
            "eraseRegion": [0, 0, frames[0].width, ERASE_BOTTOM],
            "minimumRed": MINIMUM_RED,
            "hueDegrees": [MINIMUM_HUE_DEGREES, MAXIMUM_HUE_DEGREES],
            "minimumSaturation": MINIMUM_SATURATION,
            "removedPixelsPerFrame": removed_pixels,
        },
    }
    metadata.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    args = parser.parse_args()
    prepare(args.root.resolve())


if __name__ == "__main__":
    main()
