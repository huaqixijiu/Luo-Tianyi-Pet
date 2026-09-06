#!/usr/bin/env python3
"""Build deterministic settings artwork and the Windows application icon."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[2]
JOIN_SOURCE = (
    ROOT
    / "候选素材_官方"
    / "03_官方表情包"
    / "5582-心律共鸣动态表情包"
    / "心律共鸣动态表情包_加入我们.gif"
)
PORTRAIT_SOURCE = (
    ROOT
    / "assets"
    / "animations"
    / "runtime"
    / "user-chibi-crystal-full-body-idle.atlas.png"
)
UI_OUTPUT = ROOT / "assets" / "ui" / "settings-encouragement.png"
ICON_PNG_OUTPUT = ROOT / "assets" / "app" / "luotianyi-pet.png"
ICON_OUTPUT = ROOT / "assets" / "app" / "luotianyi-pet.ico"
META_OUTPUT = ROOT / "assets" / "app" / "luotianyi-pet.meta.json"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build_encouragement_art() -> None:
    with Image.open(JOIN_SOURCE) as source:
        source.seek(0)
        frame = source.convert("RGBA")

    # Frame zero contains the complete character pose before the source's
    # "Join Us!" lettering appears, so no retouching is required.
    frame.save(UI_OUTPUT, optimize=True)


def build_icon() -> None:
    with Image.open(PORTRAIT_SOURCE) as source:
        portrait = source.convert("RGBA")

    size = 512
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    gradient = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gradient_pixels = gradient.load()
    top = (236, 250, 255)
    bottom = (99, 204, 248)
    for y in range(size):
        t = y / (size - 1)
        color = tuple(round(a + ((b - a) * t)) for a, b in zip(top, bottom))
        for x in range(size):
            gradient_pixels[x, y] = (*color, 255)

    rounded_mask = Image.new("L", (size, size), 0)
    mask_draw = ImageDraw.Draw(rounded_mask)
    mask_draw.rounded_rectangle((5, 5, 506, 506), radius=104, fill=255)
    gradient.putalpha(rounded_mask)
    canvas.alpha_composite(gradient)

    border = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    border_draw = ImageDraw.Draw(border)
    border_draw.rounded_rectangle(
        (7, 7, 504, 504),
        radius=102,
        outline=(255, 255, 255, 205),
        width=10,
    )
    canvas.alpha_composite(border)

    # A square crop from the full-body source keeps both ears, the complete
    # face, and just enough shoulders to read as a portrait at tray-icon size.
    head_crop = portrait.crop((60, 0, 420, 360))
    head_crop = head_crop.resize((476, 476), Image.Resampling.LANCZOS)

    alpha = head_crop.getchannel("A")
    halo_alpha = alpha.filter(ImageFilter.MaxFilter(11)).filter(
        ImageFilter.GaussianBlur(2.2)
    )
    halo = Image.new("RGBA", head_crop.size, (255, 255, 255, 0))
    halo.putalpha(halo_alpha.point(lambda value: round(value * 0.9)))
    canvas.alpha_composite(halo, (18, 22))
    canvas.alpha_composite(head_crop, (18, 22))

    canvas.putalpha(Image.composite(canvas.getchannel("A"), Image.new("L", (size, size), 0), rounded_mask))
    canvas.save(ICON_PNG_OUTPUT, optimize=True)
    canvas.save(
        ICON_OUTPUT,
        format="ICO",
        sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)],
    )


def main() -> None:
    UI_OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    ICON_OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    build_encouragement_art()
    build_icon()

    metadata = {
        "schemaVersion": 1,
        "settingsArtwork": {
            "source": str(JOIN_SOURCE.relative_to(ROOT)).replace("\\", "/"),
            "sourceSha256": sha256(JOIN_SOURCE),
            "frameIndex": 0,
            "output": str(UI_OUTPUT.relative_to(ROOT)).replace("\\", "/"),
            "outputSha256": sha256(UI_OUTPUT),
            "transformation": "extract-frame-zero-before-source-lettering",
        },
        "applicationIcon": {
            "source": str(PORTRAIT_SOURCE.relative_to(ROOT)).replace("\\", "/"),
            "sourceSha256": sha256(PORTRAIT_SOURCE),
            "crop": [60, 0, 420, 360],
            "outputPng": str(ICON_PNG_OUTPUT.relative_to(ROOT)).replace("\\", "/"),
            "outputPngSha256": sha256(ICON_PNG_OUTPUT),
            "outputIco": str(ICON_OUTPUT.relative_to(ROOT)).replace("\\", "/"),
            "outputIcoSha256": sha256(ICON_OUTPUT),
            "transformation": "portrait-crop-on-rounded-tianyi-blue-background",
        },
    }
    META_OUTPUT.write_text(
        json.dumps(metadata, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
