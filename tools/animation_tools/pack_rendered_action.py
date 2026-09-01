from __future__ import annotations

import argparse
import hashlib
from pathlib import Path

from PIL import Image


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(description="Pack transparent Blender PNG frames into WebP.")
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--pattern", default="*.png")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--width", type=int, default=480)
    parser.add_argument("--height", type=int, default=520)
    parser.add_argument("--duration-ms", type=int, default=42)
    parser.add_argument("--expected-frames", type=int)
    args = parser.parse_args()

    paths = sorted(args.input.resolve().glob(args.pattern))
    if not paths:
        raise FileNotFoundError(f"No frames matching {args.pattern!r} in {args.input}")
    if args.expected_frames is not None and len(paths) != args.expected_frames:
        raise ValueError(f"Expected {args.expected_frames} frames, found {len(paths)}")

    target_size = (args.width, args.height)
    frames: list[Image.Image] = []
    for path in paths:
        with Image.open(path) as image:
            frame = image.convert("RGBA")
            if frame.size != target_size:
                frame = frame.resize(target_size, Image.Resampling.LANCZOS)
            frames.append(frame)

    destination = args.output.resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        destination,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=args.duration_ms,
        loop=0,
        lossless=True,
        method=4,
    )
    print(
        f"Packed {len(frames)} frames to {destination} at {args.duration_ms} ms/frame; "
        f"SHA-256 {sha256(destination)}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
