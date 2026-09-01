from __future__ import annotations

import argparse
import hashlib
import math
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from PIL import Image


TransformCurve = Callable[[float], tuple[float, float, float, float, float]]


@dataclass(frozen=True)
class ActionDefinition:
    filename: str
    frame_count: int
    frame_duration_ms: int
    curve: TransformCurve


def smooth_envelope(progress: float) -> float:
    return math.sin(math.pi * progress) ** 2


def gentle_bow(progress: float) -> tuple[float, float, float, float, float]:
    amount = smooth_envelope(progress)
    return 1.0 + 0.018 * amount, 1.0 - 0.065 * amount, 0.0, 0.0, 0.0


def happy_hop(progress: float) -> tuple[float, float, float, float, float]:
    lift = math.sin(math.pi * progress) ** 2
    impulse = math.sin(2.0 * math.pi * progress)
    return (
        1.0 - 0.012 * impulse + 0.015 * lift,
        1.0 + 0.015 * impulse - 0.015 * lift,
        1.8 * impulse,
        0.0,
        -15.0 * lift,
    )


def curious_lean(progress: float) -> tuple[float, float, float, float, float]:
    amount = math.sin(2.0 * math.pi * progress) * smooth_envelope(progress)
    return 1.0, 1.0, 5.5 * amount, 8.0 * amount, -2.0 * abs(amount)


def shy_sway(progress: float) -> tuple[float, float, float, float, float]:
    amount = math.sin(4.0 * math.pi * progress) * smooth_envelope(progress)
    return (
        1.0 + 0.012 * abs(amount),
        1.0 - 0.012 * abs(amount),
        2.8 * amount,
        7.0 * amount,
        0.0,
    )


def surprise_pop(progress: float) -> tuple[float, float, float, float, float]:
    if progress < 0.22:
        local = progress / 0.22
        squash = math.sin(math.pi * local) ** 2
        return 1.0 + 0.045 * squash, 1.0 - 0.07 * squash, 0.0, 0.0, 0.0
    if progress < 0.62:
        local = (progress - 0.22) / 0.40
        lift = math.sin(math.pi * local) ** 2
        stretch = math.sin(2.0 * math.pi * local)
        return (
            1.0 - 0.012 * stretch + 0.012 * lift,
            1.0 + 0.016 * stretch - 0.012 * lift,
            0.0,
            0.0,
            -12.0 * lift,
        )
    local = (progress - 0.62) / 0.38
    settle = math.sin(3.0 * math.pi * local) * (1.0 - local)
    return 1.0 + 0.012 * abs(settle), 1.0 - 0.018 * abs(settle), 1.2 * settle, 0.0, 0.0


ACTIONS = (
    ActionDefinition("完整全身_轻轻鞠躬.webp", 32, 105, gentle_bow),
    ActionDefinition("完整全身_开心跳跃.webp", 32, 90, happy_hop),
    ActionDefinition("完整全身_左右探身.webp", 40, 95, curious_lean),
    ActionDefinition("完整全身_害羞摇摆.webp", 48, 85, shy_sway),
    ActionDefinition("完整全身_惊喜弹起.webp", 28, 90, surprise_pop),
)


def load_frames(source: Path) -> list[Image.Image]:
    with Image.open(source) as image:
        return [
            image.seek(index) or image.convert("RGBA").copy()
            for index in range(getattr(image, "n_frames", 1))
        ]


def transform_frame(
    frame: Image.Image,
    scale_x: float,
    scale_y: float,
    angle_degrees: float,
    translate_x: float,
    translate_y: float,
) -> Image.Image:
    width, height = frame.size
    anchor_x = width / 2.0
    anchor_y = height - 18.0
    target_x = anchor_x + translate_x
    target_y = anchor_y + translate_y
    radians = math.radians(angle_degrees)
    cosine = math.cos(radians)
    sine = math.sin(radians)
    inverse = (
        cosine / scale_x,
        sine / scale_x,
        anchor_x - (cosine * target_x + sine * target_y) / scale_x,
        -sine / scale_y,
        cosine / scale_y,
        anchor_y + (sine * target_x - cosine * target_y) / scale_y,
    )
    return frame.transform(
        frame.size,
        Image.Transform.AFFINE,
        inverse,
        resample=Image.Resampling.BICUBIC,
        fillcolor=(0, 0, 0, 0),
    )


def make_action(source_frames: list[Image.Image], action: ActionDefinition) -> list[Image.Image]:
    frames: list[Image.Image] = []
    for index in range(action.frame_count):
        progress = index / (action.frame_count - 1)
        source_index = round(progress * len(source_frames)) % len(source_frames)
        frames.append(transform_frame(source_frames[source_index], *action.curve(progress)))
    return frames


def save_webp(frames: list[Image.Image], destination: Path, duration_ms: int) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        destination,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=duration_ms,
        loop=0,
        lossless=True,
        method=4,
    )


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Create deterministic whole-body action candidates from the official idle render."
    )
    parser.add_argument("--root", type=Path, default=Path("."))
    args = parser.parse_args()

    root = args.root.resolve()
    source = root / "候选素材_官方/04_官方模型派生动画/洛天依V4公式服Q版_完整全身待机.webp"
    output = root / "候选素材_官方/04_官方模型派生动画/动作候选"
    source_frames = load_frames(source)
    if not source_frames:
        raise ValueError(f"No source frames found in {source}")
    if any(frame.size != source_frames[0].size for frame in source_frames):
        raise ValueError("Source animation frames must have a consistent size")

    print(f"Source: {source}")
    print(f"Source SHA-256: {sha256(source)}")
    for action in ACTIONS:
        destination = output / action.filename
        save_webp(make_action(source_frames, action), destination, action.frame_duration_ms)
        print(
            f"Generated {destination}: {action.frame_count} frames, "
            f"{action.frame_duration_ms} ms/frame, SHA-256 {sha256(destination)}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
