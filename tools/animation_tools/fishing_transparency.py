"""Lossless, source-specific matte for the official 240px fishing animation.

Background seeds are manually audited empty areas, not all border pixels:
the character's white skirt touches the bottom border and must stay opaque.
The transparent palette entry is separate from opaque white.
"""

from collections import deque

from PIL import Image


BACKGROUND_SEEDS = ((0, 0), (167, 90), (207, 112), (41, 238))
TEXT_BOTTOM = 78


def fishing_background_mask(frame: Image.Image) -> bytes:
    rgb = frame.convert("RGB")
    if rgb.size != (240, 240):
        raise ValueError("Fishing matte is only audited for the official 240x240 source.")
    pixels = rgb.load()
    mask = bytearray(240 * 240)
    queue = deque(BACKGROUND_SEEDS)
    # All pure-white holes in the typography are canvas, not painted highlights.
    queue.extend((x, y) for y in range(TEXT_BOTTOM) for x in range(240)
                 if pixels[x, y] == (255, 255, 255))
    for point in BACKGROUND_SEEDS:
        if pixels[point] != (255, 255, 255):
            raise ValueError(f"Audited background seed {point} changed in this source.")
    while queue:
        x, y = queue.popleft()
        if not (0 <= x < 240 and 0 <= y < 240):
            continue
        offset = y * 240 + x
        if mask[offset] or pixels[x, y] != (255, 255, 255):
            continue
        mask[offset] = 255
        queue.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
    return bytes(mask)


def prepare_fishing_frame(frame: Image.Image) -> Image.Image:
    rgb = frame.convert("RGB")
    background = fishing_background_mask(rgb)
    pixels = list(rgb.getdata())
    colors = sorted({pixel for i, pixel in enumerate(pixels) if not background[i]})
    if len(colors) > 255:
        raise ValueError("Lossless GIF palette cannot fit; do not quantize original colors.")
    # Index zero belongs exclusively to transparent pixels. Opaque white has its
    # own nonzero index, so GIF decoding cannot erase clothing or text by color.
    indices = {color: index + 1 for index, color in enumerate(colors)}
    result = Image.new("P", rgb.size)
    palette = [0, 0, 0] + [channel for color in colors for channel in color]
    result.putpalette(palette + [0] * (768 - len(palette)))
    result.putdata([0 if background[i] else indices[pixel] for i, pixel in enumerate(pixels)])
    result.info["transparency"] = 0
    return result


def prepare_fishing_rgba(frame: Image.Image) -> Image.Image:
    """Unmatte the complete antialias fringe next to audited empty canvas.

    Keep solid artwork byte-exact. For a fringe pixel, find a nearby ink color
    that explains it as ink composited over white (maximum fitting error 8).
    Recover fractional coverage rather than deleting the pixel or retaining a
    white halo. This requires lossless RGBA output, not GIF's one-bit alpha.
    """
    source = frame.convert("RGB")
    pixels = source.load()
    mask = fishing_background_mask(source)
    result = source.convert("RGBA")
    output = result.load()
    for y in range(240):
        for x in range(240):
            if mask[y * 240 + x]:
                output[x, y] = (0, 0, 0, 0)
                continue
            color = pixels[x, y]
            if color == (255, 255, 255):
                continue
            if not any(mask[b * 240 + a] for b in range(max(0, y - 1), min(240, y + 2))
                       for a in range(max(0, x - 1), min(240, x + 2))):
                continue
            candidates = []
            for b in range(max(0, y - 3), min(240, y + 4)):
                for a in range(max(0, x - 3), min(240, x + 4)):
                    ink = pixels[a, b]
                    if mask[b * 240 + a] or min(ink) >= min(color) - 15:
                        continue
                    ink_distance = [255 - c for c in ink]
                    alpha = sum((255 - c) * d for c, d in zip(color, ink_distance)) / sum(d*d for d in ink_distance)
                    if not 0.01 < alpha < 0.95:
                        continue
                    error = max(abs(c - (255 - alpha*d)) for c, d in zip(color, ink_distance))
                    if error > 8:
                        continue
                    # Prefer solid ink over another nearly-white fringe sample;
                    # using a fringe as ink would leave dotted white halos.
                    candidates.append((min(ink), (a-x)**2 + (b-y)**2 + error, ink, alpha))
            if candidates:
                _, _, ink, alpha = min(candidates)
                # Undo the white matte per channel instead of copying the
                # reference color. The local ink estimates coverage only;
                # the source pixel determines its recovered foreground color.
                alpha_byte = max(1, round(alpha * 255), 255 - min(color))
                recovered = tuple(max(0, min(255, round(
                    255 + (channel - 255) * 255 / alpha_byte))) for channel in color)
                output[x, y] = (*recovered, alpha_byte)
    return result


def validate_fishing_pixels(source: Image.Image, output: Image.Image) -> None:
    source_rgb = source.convert("RGB")
    rgba = output.convert("RGBA")
    if rgba.size != source_rgb.size:
        raise ValueError("Fishing frame geometry changed.")
    mask = fishing_background_mask(source_rgb)
    for i, (original, actual) in enumerate(zip(source_rgb.getdata(), rgba.getdata(), strict=True)):
        if mask[i]:
            if actual[3] != 0:
                raise ValueError(f"Background pixel {i} is not transparent.")
        elif actual != (*original, 255):
            raise ValueError(f"Foreground pixel {i} changed color or opacity.")
