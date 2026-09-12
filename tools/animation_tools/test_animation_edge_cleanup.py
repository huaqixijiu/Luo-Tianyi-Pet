import unittest
import numpy as np
from PIL import Image
from animation_edge_cleanup import clean_edges


class EdgeCleanupTests(unittest.TestCase):
    def test_silhouette_and_interior_including_white_are_preserved(self):
        data = np.zeros((12, 12, 4), dtype=np.uint8)
        data[2:10, 2:10] = [255, 255, 255, 255]
        data[4:8, 4:8] = [90, 180, 40, 255]
        data[0, 0] = [250, 220, 20, 255]  # Isolated punctuation.
        result = np.asarray(clean_edges(Image.fromarray(data), "coverage"))
        np.testing.assert_array_equal(data[:, :, 3] > 0, result[:, :, 3] > 0)
        np.testing.assert_array_equal(data[3:9, 3:9], result[3:9, 3:9])
        np.testing.assert_array_equal(data[:, :, :3], result[:, :, :3])
        self.assertGreater(result[2, 2, 3], 0)
        self.assertLess(result[2, 2, 3], 255)

    def test_dark_contour_matte_recomposes_over_white(self):
        data = np.zeros((7, 7, 4), dtype=np.uint8)
        data[2:5, 2:5] = [20, 20, 20, 255]
        data[1, 3] = [192, 192, 192, 255]
        data[1, 2] = [255, 255, 255, 255]
        result = np.asarray(clean_edges(Image.fromarray(data), "dark-contour-white-matte"))
        for y, x in [(1, 3), (1, 2)]:
            pixel = result[y, x].astype(float)
            composite = pixel[:3]*pixel[3]/255+255-pixel[3]
            self.assertLessEqual(max(abs(composite-data[y, x, :3])), 1)
            self.assertLess(pixel[3], 255)
            self.assertGreater(pixel[3], 0)

    def test_white_sticker_stroke_keeps_original_rgb(self):
        data = np.zeros((7, 7, 4), dtype=np.uint8)
        data[2:5, 2:5] = [20, 20, 20, 255]
        data[1, 3] = [255, 255, 255, 255]
        result = np.asarray(clean_edges(Image.fromarray(data), "coverage-white-matte"))
        np.testing.assert_array_equal(result[1, 3, :3], [255, 255, 255])
        self.assertGreater(result[1, 3, 3], 128)

    def test_soft_alpha_and_unknown_profiles_fail_closed(self):
        with self.assertRaises(ValueError):
            clean_edges(Image.new("RGBA", (4, 4), (80, 90, 100, 120)), "coverage")
        with self.assertRaises(ValueError):
            clean_edges(Image.new("RGBA", (4, 4)), "guess")


if __name__ == "__main__":
    unittest.main()
