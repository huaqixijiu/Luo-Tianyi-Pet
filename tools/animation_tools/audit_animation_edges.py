"""Scan every runtime frame and render representative edge inspection sheets."""

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageSequence


def runtime_frames(root, entry):
    path = root / "assets" / entry["atlasPath"]
    with Image.open(path) as image:
        if path.suffix == ".webp":
            for frame in ImageSequence.Iterator(image):
                yield frame.convert("RGBA")
        else:
            for index in range(len(entry["frameDurationsMilliseconds"])):
                x = index % entry["columns"] * entry["frameWidth"]
                y = index // entry["columns"] * entry["frameHeight"]
                yield image.crop((x, y, x + entry["frameWidth"], y + entry["frameHeight"])).convert("RGBA")


def dilate(mask, size):
    radius = size // 2
    height, width = mask.shape
    padded = np.pad(mask, ((0, 0), (radius, radius)))
    horizontal = np.zeros_like(mask)
    for offset in range(size):
        horizontal |= padded[:, offset:offset+width]
    padded = np.pad(horizontal, ((radius, radius), (0, 0)))
    result = np.zeros_like(mask)
    for offset in range(size):
        result |= padded[offset:offset+height, :]
    return result


def edge_stats(frame):
    data = np.asarray(frame)
    alpha = data[:, :, 3]
    rgb = data[:, :, :3]
    low = rgb.min(axis=2)
    high = rgb.max(axis=2)
    edge = (alpha > 0) & dilate(alpha == 0, 3)
    dark_nearby = dilate((alpha >= 240) & (high <= 96), 7)
    suspect = edge & (alpha >= 240) & (low >= 150) & ((high-low) <= 28) & dark_nearby
    return {"opaqueBrightNearDark": int(suspect.sum()), "edgePixels": int(edge.sum()),
            "partialAlphaPixels": int(((alpha > 0) & (alpha < 255)).sum())}


def render_card(frame, label):
    card = Image.new("RGB", (380, 280), "#303844")
    draw = ImageDraw.Draw(card)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 13)
    draw.text((7, 5), label[:48], font=font, fill="white")
    for column, color in enumerate(["#102033", "#e8ebef"]):
        panel = Image.new("RGBA", (184, 245), color)
        size = (min(178, round(frame.width * 225 / frame.height)), min(225, round(frame.height * 178 / frame.width)))
        scaled = frame.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")
        panel.alpha_composite(scaled, ((184-size[0])//2, (245-size[1])//2))
        card.paste(panel.convert("RGB"), (4 + 190*column, 30))
    return card


def audit(root, output):
    output.mkdir(parents=True, exist_ok=True)
    catalog = json.loads((root / "assets/manifests/animations.json").read_text(encoding="utf-8"))["animations"]
    report = []
    cards = []
    for entry in catalog:
        totals = {"opaqueBrightNearDark": 0, "edgePixels": 0, "partialAlphaPixels": 0}
        best = None
        maximum = -1
        worst_index = 0
        count = 0
        for index, frame in enumerate(runtime_frames(root, entry)):
            stats = edge_stats(frame)
            count += 1
            for key in totals:
                totals[key] += stats[key]
            if stats["opaqueBrightNearDark"] > maximum:
                best = frame.copy()
                maximum = stats["opaqueBrightNearDark"]
                worst_index = index
        # WebP may losslessly merge identical adjacent logical frames.
        record = {"id": entry["id"], "frameCount": count,
                  "declaredFrameCount": len(entry["frameDurationsMilliseconds"]), "worstFrame": worst_index,
                  "maxOpaqueBrightNearDark": maximum, **totals}
        report.append(record)
        (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        best.save(output / (entry["id"] + ".png"))
        cards.append(render_card(best, entry["id"]))
        print(entry["id"], count, maximum, flush=True)
    for offset in range(0, len(cards), 9):
        page = Image.new("RGB", (1140, 840), "#303844")
        for index, card in enumerate(cards[offset:offset+9]):
            page.paste(card, (index % 3 * 380, index // 3 * 280))
        page.save(output / f"page-{offset//9+1:02}.png")
    (output / "report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    audit(Path.cwd(), args.output)
