"""Audit source, encoded RGBA, and actual runtime atlas; render background QA."""

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageSequence

from compile_animation_atlases import webp_frame_durations
from fishing_transparency import BACKGROUND_SEEDS


def validate(root: Path) -> None:
    source = root / "候选素材_官方/12_新添加_用户指定官方表情候选/洛天依十周年生日_摸鱼.gif"
    assert hashlib.sha256(source.read_bytes()).hexdigest() == "c74d708d9e5ed9c2f8aecd7a2f70a7349efbb813ff843506d0ee9e97e8966385"
    runtime_source = root / "assets/animations/processed/十周年生日_摸鱼一分钟_透明无损.webp"
    with Image.open(source) as image:
        originals = [frame.convert("RGBA") for frame in ImageSequence.Iterator(image)][1:]
    with Image.open(runtime_source) as image:
        frames = [frame.convert("RGBA") for frame in ImageSequence.Iterator(image)]
    assert len(frames) == len(originals) == 69
    assert sum(webp_frame_durations(runtime_source)) == 60_000
    manifest = next(entry for entry in json.loads((root / "assets/manifests/animations.json").read_text(encoding="utf-8"))["animations"]
                    if entry["id"] == "tenth-birthday-fishing-countdown")
    assert sum(manifest["frameDurationsMilliseconds"]) == 60_000
    atlas = Image.open(root / "assets" / manifest["atlasPath"]).convert("RGBA")
    stats = {"frames": 69, "durationMilliseconds": 60000, "opaquePixelsPreserved": 0,
             "opaqueWhitePixelsPreserved": 0, "antialiasPixels": 0, "backgroundPixelsRemoved": 0}
    for index, (original, frame) in enumerate(zip(originals, frames, strict=True)):
        assert frame.size == original.size == (240, 240)
        for point in BACKGROUND_SEEDS:
            assert frame.getpixel(point)[3] == 0, (index, "background seed", point)
        # Named, independently inspected white foreground areas. In particular,
        # the skirt touches the lower border and must not be flood-filled away.
        for point in [(118, 134), (105, 144), (112, 213), (110, 230), (115, 238)]:
            assert frame.getpixel(point) == original.getpixel(point), (index, "fish/eye/clothing", point)
        for position, (before, after) in enumerate(zip(original.getdata(), frame.getdata(), strict=True)):
            if after[3] == 0:
                assert before[:3] == (255, 255, 255), (index, position, "deleted colored artwork")
                stats["backgroundPixelsRemoved"] += 1
            elif after[3] == 255:
                assert before == after, (index, position, "recolored solid artwork")
                stats["opaquePixelsPreserved"] += 1
                stats["opaqueWhitePixelsPreserved"] += before[:3] == (255, 255, 255)
            else:
                assert min(before[:3]) >= 190, (index, position, "modified solid ink")
                reconstructed = [round(c * after[3] / 255 + 255 - after[3]) for c in after[:3]]
                assert max(abs(a-b) for a, b in zip(before[:3], reconstructed)) <= 4, (index, position, "edge color drift")
                stats["antialiasPixels"] += 1
        assert sum(1 for p in frame.crop((0, 0, 240, 78)).getdata() if p[3]) > 4500
        assert sum(1 for p in frame.crop((50, 185, 140, 240)).getdata() if p == (255, 255, 255, 255)) > 750
        x = (index % manifest["columns"]) * 240
        y = (index // manifest["columns"]) * 240
        assert atlas.crop((x, y, x+240, y+240)).tobytes() == frame.tobytes(), (index, "atlas encoding")
    assert stats["antialiasPixels"] > 1000
    directory = root / "docs/validation"
    directory.mkdir(exist_ok=True)
    review = Image.new("RGB", (960, 720))
    for row, color in enumerate(["#125baf", "#191d29", "#eeeeee"]):
        for column, index in enumerate([0, 22, 45, 68]):
            background = Image.new("RGBA", (240, 240), color)
            background.alpha_composite(frames[index])
            review.paste(background.convert("RGB"), (column * 240, row * 240))
    review.save(directory / "fishing-transparent-backgrounds-2026-09-12.png")
    (directory / "fishing-transparency-pixels-2026-09-12.json").write_text(json.dumps(stats, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(stats))
    print("PASS source hash, 69 frames, 60 seconds, foreground colors/white parts, antialias reconstruction, actual atlas")


if __name__ == "__main__":
    validate(Path.cwd())
