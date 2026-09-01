"""Build preview animations for the file-eating interaction.

The previews intentionally keep the official source artwork intact and only
crop, reposition, composite, and retime it.  They are review assets, not yet
runtime assets.
"""

from __future__ import annotations

import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw


PROJECT_ROOT = Path(__file__).resolve().parents[2]
SOURCE_RUN = (
    PROJECT_ROOT
    / "候选素材_官方"
    / "03_官方表情包"
    / "5283-洛天依·收藏集表情包"
    / "洛天依·收藏集表情包_来喽.png"
)
DEFAULT_EAT_SOURCE = (
    PROJECT_ROOT
    / "候选素材_官方"
    / "10_新添加_文件吞食组合候选"
    / "源素材_洛天依11周年QQ表情_干饭.gif"
)
DEFAULT_OUTPUT = (
    PROJECT_ROOT / "候选素材_官方" / "10_新添加_文件吞食组合候选"
)


def _document_icon(size: int) -> Image.Image:
    icon = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(icon)
    pad = max(2, size // 12)
    fold = max(6, size // 4)
    outline = max(2, size // 16)
    body = (pad, pad, size - pad, size - pad)
    draw.rounded_rectangle(
        body,
        radius=max(3, size // 9),
        fill=(247, 253, 255, 255),
        outline=(29, 192, 255, 255),
        width=outline,
    )
    draw.polygon(
        [
            (size - pad - fold, pad),
            (size - pad, pad + fold),
            (size - pad - fold, pad + fold),
        ],
        fill=(174, 233, 255, 255),
    )
    line_left = pad + max(4, size // 8)
    line_right = size - pad - max(4, size // 8)
    for ratio in (0.50, 0.66, 0.82):
        y = int(size * ratio)
        draw.line(
            (line_left, y, line_right, y),
            fill=(91, 154, 183, 235),
            width=max(1, size // 22),
        )
    return icon


def _fit(image: Image.Image, width: int, height: int) -> Image.Image:
    result = image.copy()
    result.thumbnail((width, height), Image.Resampling.LANCZOS)
    return result


def _save_animation(
    frames: list[Image.Image], output: Path, durations: list[int]
) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(
        output,
        format="WEBP",
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        lossless=True,
        method=4,
    )


def make_run_preview(output: Path) -> None:
    source = Image.open(SOURCE_RUN).convert("RGBA")
    source = _fit(source, 176, 176)
    icon = _document_icon(52)
    frames: list[Image.Image] = []
    durations: list[int] = []
    frame_count = 34

    for index in range(frame_count):
        frame = Image.new("RGBA", (420, 250), (0, 0, 0, 0))
        target_x, target_y = 338, 135
        frame.alpha_composite(icon, (target_x, target_y))

        t = index / (frame_count - 1)
        eased = 1 - (1 - t) ** 2.2
        x = int(-25 + eased * 214)
        y = int(53 + math.sin(index * math.pi / 2) * 7)
        angle = math.sin(index * math.pi / 2) * 3.5
        sprite = source.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
        frame.alpha_composite(sprite, (x, y))
        frames.append(frame)
        durations.append(82 if index < frame_count - 4 else 125)

    _save_animation(frames, output, durations)


def _eat_source_frames(source_path: Path) -> list[Image.Image]:
    source = Image.open(source_path)
    frames: list[Image.Image] = []
    # A single hand-to-mouth cycle.  The original contains several meals and
    # long pauses; this range is the shortest self-contained eating gesture.
    for index in range(38, 61):
        source.seek(index)
        frame = source.convert("RGBA")
        # Keep the complete composition: the “干饭” caption is semantically
        # appropriate here and looks cleaner than leaving a clipped glyph beside
        # the hair.  The temporal crop is what removes the long source pauses.
        frames.append(frame)
    return frames


def make_eat_preview(source_path: Path, output: Path) -> None:
    source_frames = _eat_source_frames(source_path)
    frames: list[Image.Image] = []
    durations: list[int] = []

    for index, source in enumerate(source_frames):
        frame = Image.new("RGBA", (320, 300), (0, 0, 0, 0))
        sprite = _fit(source, 252, 300)
        frame.alpha_composite(sprite, ((320 - sprite.width) // 2 + 8, 0))

        t = index / max(1, len(source_frames) - 1)
        if t < 0.58:
            progress = t / 0.58
            icon_size = max(15, int(48 - progress * 28))
            icon = _document_icon(icon_size)
            x = int(155 - icon_size / 2 + math.sin(index * 0.8) * 3)
            y = int(226 - progress * 107 - icon_size / 2)
            frame.alpha_composite(icon, (x, y))

        frames.append(frame)
        durations.append(118)

    for _ in range(4):
        frames.append(frames[-1].copy())
        durations.append(120)

    _save_animation(frames, output, durations)


def make_flow_preview(run_path: Path, eat_path: Path, output: Path) -> None:
    run = Image.open(run_path)
    eat = Image.open(eat_path)
    frames: list[Image.Image] = []
    durations: list[int] = []

    def compose_run(index: int) -> Image.Image:
        run.seek(index)
        src = _fit(run.convert("RGBA"), 420, 300)
        frame = Image.new("RGBA", (420, 300), (0, 0, 0, 0))
        frame.alpha_composite(src, ((420 - src.width) // 2, (300 - src.height) // 2))
        return frame

    def compose_eat(index: int) -> Image.Image:
        eat.seek(index)
        src = _fit(eat.convert("RGBA"), 320, 300)
        frame = Image.new("RGBA", (420, 300), (0, 0, 0, 0))
        # Keep the eater close to the runner's final screen position so the
        # character does not jump from the target back to the canvas centre.
        frame.alpha_composite(src, (100, (300 - src.height) // 2))
        return frame

    run_frames = [compose_run(index) for index in range(min(run.n_frames, 34))]
    eat_frames = [compose_eat(index) for index in range(eat.n_frames)]
    frames.extend(run_frames)
    durations.extend([82] * len(run_frames))

    for index in range(1, 7):
        alpha = index / 7
        frames.append(Image.blend(run_frames[-1], eat_frames[0], alpha))
        durations.append(45)

    frames.extend(eat_frames[1:])
    durations.extend([118] * (len(eat_frames) - 1))

    _save_animation(frames, output, durations)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--eat-source", type=Path, default=DEFAULT_EAT_SOURCE)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    args.output_dir.mkdir(parents=True, exist_ok=True)
    run_output = args.output_dir / "01_奔向文件_来喽姿势预演.webp"
    eat_output = args.output_dir / "02_吞文件_十一周年干饭裁剪预演.webp"
    flow_output = args.output_dir / "03_奔向并吞下文件_完整流程预演.webp"

    make_run_preview(run_output)
    make_eat_preview(args.eat_source, eat_output)
    make_flow_preview(run_output, eat_output, flow_output)

    print(run_output)
    print(eat_output)
    print(flow_output)


if __name__ == "__main__":
    main()
