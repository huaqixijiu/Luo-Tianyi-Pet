"""Build compact QA contact sheets for crystal long-idle source sequences."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageSequence


def checker(size: tuple[int, int], cell: int = 12) -> Image.Image:
    image = Image.new("RGB", size, "white")
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill="#dce8ec")
    return image


def make_sheet(paths: list[Path], output: Path, step: int, label: str) -> None:
    sampled = list(enumerate(paths))[::step]
    tile = (180, 205)
    columns = 6
    rows = (len(sampled) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * tile[0], rows * tile[1]), "#17232c")
    draw = ImageDraw.Draw(sheet)
    for slot, (index, path) in enumerate(sampled):
        with Image.open(path) as source:
            frame = source.convert("RGBA")
        frame.thumbnail((168, 174), Image.Resampling.LANCZOS)
        board = checker((168, 174))
        board.paste(frame, ((168 - frame.width) // 2, (174 - frame.height) // 2), frame)
        x = slot % columns * tile[0] + 6
        y = slot // columns * tile[1] + 6
        sheet.paste(board, (x, y))
        draw.text((x, y + 178), f"{label} {index:03d}", fill="white")
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--include-source-sheets", action="store_true")
    args = parser.parse_args()
    root = args.root
    source = root / "候选素材_官方"
    output = root / "docs" / "validation" / "crystal-long-idle-source"
    jobs = (
        (source / "模式二新添加动作" / "睡觉", 10, "sleep"),
        (source / "模式二新添加动作" / "鸭子坐", 8, "duck"),
        (source / "睡觉小装饰" / "zzz", 12, "zzz"),
        (source / "睡觉小装饰" / "梦见包子", 8, "bun"),
        (source / "睡觉小装饰" / "梦见乐正绫", 8, "ling"),
        (source / "睡觉小装饰" / "云朵消散", 12, "fade"),
    )
    if args.include_source_sheets:
        for directory, step, label in jobs:
            paths = sorted(directory.glob("*.png"))
            make_sheet(paths, output / f"{label}.jpg", step, label)
            print(label, len(paths), paths[0].name, paths[-1].name)

    runtime_root = root / "assets" / "animations" / "runtime"
    runtime_jobs = (
        ("crystal-long-idle-sleep.frames.webp", (0, 160, 200, 220, 300, 360), "sleep"),
        ("crystal-long-idle-duck-sit.frames.webp", (0, 48, 120, 160, 192, 216), "duck"),
        ("crystal-sleep-decoration-zzz.frames.webp", (0, 30, 60, 90, 120), "zzz"),
        ("crystal-sleep-decoration-bun.frames.webp", (0, 18, 36, 54, 72), "bun"),
        ("crystal-sleep-decoration-yuezhengling.frames.webp", (0, 18, 36, 54, 72), "ling"),
        ("crystal-sleep-decoration-cloud-dissolve.frames.webp", (0, 8, 16, 24, 30), "fade"),
    )
    tiles: list[tuple[Image.Image, str]] = []
    for file_name, indices, label in runtime_jobs:
        with Image.open(runtime_root / file_name) as animation:
            frames = [frame.convert("RGBA") for frame in ImageSequence.Iterator(animation)]
        for index in indices:
            tiles.append((frames[min(index, len(frames) - 1)], f"{label} {index:03d}"))

    tile_size = (260, 290)
    columns = 6
    rows = (len(tiles) + columns - 1) // columns
    runtime_sheet = Image.new("RGB", (columns * tile_size[0], rows * tile_size[1]), "#102740")
    runtime_draw = ImageDraw.Draw(runtime_sheet)
    for slot, (frame, label) in enumerate(tiles):
        frame.thumbnail((244, 250), Image.Resampling.LANCZOS)
        board = Image.new("RGB", (244, 250), "#0877d8")
        board.paste(frame, ((244 - frame.width) // 2, (250 - frame.height) // 2), frame)
        x = slot % columns * tile_size[0] + 8
        y = slot // columns * tile_size[1] + 8
        runtime_sheet.paste(board, (x, y))
        runtime_draw.text((x, y + 256), label, fill="white")
    runtime_sheet.save(root / "docs" / "validation" / "crystal-long-idle-runtime-2026-09-12.png")

    with Image.open(runtime_root / "crystal-sleep-decoration-zzz.frames.webp") as animation:
        zzz_frames = [frame.convert("RGBA") for frame in ImageSequence.Iterator(animation)]
    zzz_indices = (0, 15, 30, 45, 60, 75, 90, 105, 120)
    zzz_sheet = Image.new("RGB", (3 * 200, 3 * 220), "#dce8ec")
    zzz_draw = ImageDraw.Draw(zzz_sheet)
    for slot, index in enumerate(zzz_indices):
        board = checker((180, 180), cell=12)
        frame = zzz_frames[min(index, len(zzz_frames) - 1)]
        board.paste(frame, (0, 0), frame)
        x = slot % 3 * 200 + 10
        y = slot // 3 * 220 + 10
        zzz_sheet.paste(board, (x, y))
        zzz_draw.text((x, y + 186), f"zzz {index:03d}", fill="#102740")
    zzz_sheet.save(
        root / "docs" / "validation" / "crystal-zzz-pale-blue-2026-09-12.png"
    )


if __name__ == "__main__":
    main()
