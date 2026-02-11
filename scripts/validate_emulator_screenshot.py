#!/usr/bin/env python3
"""Validate an Android emulator screenshot is not a black or near-black screen.

Usage:
    python3 scripts/validate_emulator_screenshot.py <screenshot.png>

Exit codes:
    0 - Screenshot shows visible gameplay content (PASS)
    1 - Screenshot is black / near-black / lacks structure (FAIL)

Thresholds (designed to catch the IntroBanner overlay regression):
    - Median luminance must be >= 15 (0-255 scale)
    - No more than 95% of pixels may be near-black (R+G+B < 30)
"""

import sys
import os
import math

def main():
    if len(sys.argv) < 2:
        print("Usage: validate_emulator_screenshot.py <screenshot.png>", file=sys.stderr)
        sys.exit(1)

    path = sys.argv[1]
    if not os.path.isfile(path):
        print(f"FAIL: File not found: {path}", file=sys.stderr)
        sys.exit(1)

    try:
        from PIL import Image
    except ImportError:
        print("FAIL: Pillow is required. Install with: pip install Pillow", file=sys.stderr)
        sys.exit(1)

    img = Image.open(path).convert("RGB")
    width, height = img.size
    pixels = list(img.getdata())
    total = len(pixels)

    if total == 0:
        print("FAIL: Image has no pixel data.", file=sys.stderr)
        sys.exit(1)

    # Calculate luminance values (Rec. 709)
    lumas = []
    near_black_count = 0
    NEAR_BLACK_SUM_THRESHOLD = 30  # R+G+B < 30

    for r, g, b in pixels:
        luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
        lumas.append(luma)
        if (r + g + b) < NEAR_BLACK_SUM_THRESHOLD:
            near_black_count += 1

    lumas.sort()
    median_luma = lumas[total // 2]
    mean_luma = sum(lumas) / total
    near_black_pct = near_black_count / total

    # Thresholds
    MEDIAN_LUMA_MIN = 15.0
    NEAR_BLACK_PCT_MAX = 0.95

    print(f"Image: {os.path.basename(path)} ({width}x{height})")
    print(f"  Median luminance:  {median_luma:.1f} (min: {MEDIAN_LUMA_MIN})")
    print(f"  Mean luminance:    {mean_luma:.1f}")
    print(f"  Near-black pixels: {near_black_pct*100:.1f}% (max: {NEAR_BLACK_PCT_MAX*100:.0f}%)")

    failed = False

    if median_luma < MEDIAN_LUMA_MIN:
        print(f"  FAIL: Median luminance {median_luma:.1f} < {MEDIAN_LUMA_MIN}")
        failed = True

    if near_black_pct > NEAR_BLACK_PCT_MAX:
        print(f"  FAIL: {near_black_pct*100:.1f}% near-black pixels > {NEAR_BLACK_PCT_MAX*100:.0f}%")
        failed = True

    if failed:
        print("RESULT: FAIL - Screenshot appears to be a black screen.")
        sys.exit(1)
    else:
        print("RESULT: PASS - Screenshot shows visible content.")
        sys.exit(0)


if __name__ == "__main__":
    main()
