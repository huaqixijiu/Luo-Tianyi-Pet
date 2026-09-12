import unittest

from PIL import Image, ImageDraw

from fishing_transparency import prepare_fishing_rgba


class FishingEdgeMatteTests(unittest.TestCase):
    def test_medium_gray_antialias_does_not_remain_an_opaque_white_speck(self):
        source = Image.new("RGB", (240, 240), "white")
        draw = ImageDraw.Draw(source)
        draw.rectangle((40, 101, 60, 120), fill="black")
        for x, gray in zip(range(43, 52), [80, 96, 111, 139, 145, 153, 168, 187, 210]):
            source.putpixel((x, 100), (gray, gray, gray))
        result = prepare_fishing_rgba(source)
        for x in range(43, 52):
            pixel = result.getpixel((x, 100))
            self.assertLess(pixel[3], 255)
            self.assertEqual((0, 0, 0), pixel[:3])
            self.assertEqual(source.getpixel((x, 100))[0], 255 - pixel[3])

    def test_white_inside_outline_and_solid_colors_remain_exact(self):
        source = Image.new("RGB", (240, 240), "white")
        draw = ImageDraw.Draw(source)
        draw.rectangle((80, 140, 130, 190), fill="black")
        draw.rectangle((83, 143, 127, 187), fill="white")
        draw.rectangle((90, 150, 110, 170), fill=(118, 161, 218))
        result = prepare_fishing_rgba(source)
        self.assertEqual((255, 255, 255, 255), result.getpixel((120, 180)))
        self.assertEqual((118, 161, 218, 255), result.getpixel((100, 160)))


if __name__ == "__main__":
    unittest.main()
