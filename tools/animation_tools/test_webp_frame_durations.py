import tempfile
import unittest
from pathlib import Path

from PIL import Image

from compile_animation_atlases import read_frames


class WebpFrameDurationTests(unittest.TestCase):
    def test_nonuniform_webp_durations_do_not_repeat_final_frame_duration(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "durations.webp"
            frames = [Image.new("RGBA", (8, 8), color) for color in ["red", "green", "blue"]]
            frames[0].save(path, save_all=True, append_images=frames[1:],
                           duration=[500, 1000, 250], lossless=True, loop=0)
            actual_frames, durations = read_frames(path, 100)
            self.assertEqual(3, len(actual_frames))
            self.assertEqual([500, 1000, 250], durations)


if __name__ == "__main__":
    unittest.main()
