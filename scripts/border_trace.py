from PIL import Image
import numpy as np

path = "/home/chris/dev/decantra/doc/play-store-assets/screenshots/phone/screenshot-03-level-01.png"
img = Image.open(path).convert("RGB")
arr = np.asarray(img).astype(np.float32) / 255.0

gray = 0.2126 * arr[:, :, 0] + 0.7152 * arr[:, :, 1] + 0.0722 * arr[:, :, 2]

top = gray[0, :]
bottom = gray[-1, :]
left = gray[:, 0]
right = gray[:, -1]


def max_jump(values):
    return float(np.max(np.abs(np.diff(values))))

print("top_max_jump", max_jump(top))
print("bottom_max_jump", max_jump(bottom))
print("left_max_jump", max_jump(left))
print("right_max_jump", max_jump(right))
