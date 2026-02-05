#!/usr/bin/env python3
"""
Background Visibility Verification Tool for Decantra

Implements hard gates A–F for background cloud visibility, starfield
preservation, non-black dominance, motion presence, and theme separation.
"""

from __future__ import annotations

import argparse
import glob
import os
import re
import sys
from dataclasses import dataclass
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

try:
    import numpy as np
    from PIL import Image
    import cv2
except ImportError:
    print("ERROR: Missing dependencies. Install with: pip install pillow numpy opencv-python")
    sys.exit(1)


LUMA_WEIGHTS = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)

# Gate thresholds (fixed)
GATE_A_CONTRAST_MIN = 0.35
GATE_B_TRANSITION_RATIO = 0.35
GATE_B_TOTAL_TRANSITIONS_MIN = 12
GATE_B_ROI_TRANSITIONS_MIN = 4
GATE_C_STAR_LUMA_MIN = 200.0
GATE_C_STAR_DENSITY_MIN = 0.0005
GATE_D_P50_MIN = 14.0
GATE_D_P05_MIN = 6.0
GATE_E_MOTION_DELTA_MIN = 2.0
GATE_E_MOVING_RATIO_MIN = 0.002
GATE_F_CORRELATION_MAX = 0.75


@dataclass
class Roi:
    name: str
    x0: int
    x1: int
    y0: int
    y1: int

    def width(self) -> int:
        return max(0, self.x1 - self.x0)

    def height(self) -> int:
        return max(0, self.y1 - self.y0)

    def area(self) -> int:
        return self.width() * self.height()


@dataclass
class GateResult:
    name: str
    passed: bool
    details: List[str]


@dataclass
class MotionMetrics:
    per_pair_left: List[float]
    per_pair_right: List[float]
    avg_left: float
    avg_right: float
    passed_left: bool
    passed_right: bool


LEVEL_RE = re.compile(r"level-(\d+)", re.IGNORECASE)


def calculate_luminance(rgb: np.ndarray) -> np.ndarray:
    return (rgb.astype(np.float32) * LUMA_WEIGHTS).sum(axis=2)


def gaussian_blur(luma: np.ndarray, sigma: float) -> np.ndarray:
    return cv2.GaussianBlur(luma, ksize=(0, 0), sigmaX=sigma, sigmaY=sigma)


def get_rois(width: int, height: int) -> Tuple[Roi, Roi]:
    y0 = int(round(0.25 * height))
    y1 = int(round(0.75 * height))
    left = Roi("Left", 0, int(round(0.12 * width)), y0, y1)
    right = Roi("Right", int(round(0.88 * width)), width, y0, y1)
    return left, right


def roi_slice(arr: np.ndarray, roi: Roi) -> np.ndarray:
    return arr[roi.y0:roi.y1, roi.x0:roi.x1]


def gate_a_cloud_visibility(blurred_luma: np.ndarray, roi: Roi) -> Tuple[float, float, float, float, bool]:
    region = roi_slice(blurred_luma, roi)
    if region.size == 0:
        return 0.0, 0.0, 0.0, 0.0, False
    p05 = float(np.percentile(region, 5))
    p50 = float(np.percentile(region, 50))
    p95 = float(np.percentile(region, 95))
    contrast = (p95 - p05) / max(p50, 1.0)
    passed = contrast >= GATE_A_CONTRAST_MIN
    return p05, p50, p95, contrast, passed


def average_window(luma: np.ndarray, roi: Roi, x: int, y: int, window: int = 5) -> float:
    half = window // 2
    x0 = max(roi.x0, x - half)
    x1 = min(roi.x1 - 1, x + half)
    y0 = max(roi.y0, y - half)
    y1 = min(roi.y1 - 1, y + half)
    if x1 < x0 or y1 < y0:
        return float(luma[y, x])
    patch = luma[y0 : y1 + 1, x0 : x1 + 1]
    return float(np.mean(patch))


def gate_b_transition_count(blurred_luma: np.ndarray, roi: Roi) -> Tuple[int, bool, List[float]]:
    inset = 3
    left = roi.x0 + inset
    right = roi.x1 - 1 - inset
    top = roi.y0 + inset
    bottom = roi.y1 - 1 - inset

    if right <= left or bottom <= top:
        return 0, False, []

    path: List[Tuple[int, int]] = []
    for x in range(left, right + 1):
        path.append((x, top))
    for y in range(top + 1, bottom + 1):
        path.append((right, y))
    for x in range(right - 1, left - 1, -1):
        path.append((x, bottom))
    for y in range(bottom - 1, top, -1):
        path.append((left, y))

    if len(path) < 2:
        return 0, False, []

    values: List[float] = []
    for x, y in path:
        values.append(average_window(blurred_luma, roi, x, y, window=5))

    transitions = 0
    for prev, curr in zip(values, values[1:]):
        if abs(curr - prev) / max(prev, 1.0) >= GATE_B_TRANSITION_RATIO:
            transitions += 1
    if abs(values[0] - values[-1]) / max(values[-1], 1.0) >= GATE_B_TRANSITION_RATIO:
        transitions += 1

    passed = transitions >= GATE_B_ROI_TRANSITIONS_MIN
    return transitions, passed, values


def gate_c_star_density(luma: np.ndarray, roi: Roi) -> Tuple[float, bool]:
    region = roi_slice(luma, roi)
    if region.size == 0:
        return 0.0, False
    star_count = int(np.sum(region >= GATE_C_STAR_LUMA_MIN))
    density = star_count / float(region.size)
    return density, density >= GATE_C_STAR_DENSITY_MIN


def gate_d_non_black(luma: np.ndarray, roi: Roi) -> Tuple[float, float, bool]:
    region = roi_slice(luma, roi)
    if region.size == 0:
        return 0.0, 0.0, False
    p50 = float(np.percentile(region, 50))
    p05 = float(np.percentile(region, 5))
    passed = p50 >= GATE_D_P50_MIN and p05 >= GATE_D_P05_MIN
    return p50, p05, passed


def compute_motion_metrics(frames: Sequence[np.ndarray], left: Roi, right: Roi) -> MotionMetrics:
    per_pair_left: List[float] = []
    per_pair_right: List[float] = []

    for idx in range(len(frames) - 1):
        luma_a = calculate_luminance(frames[idx])
        luma_b = calculate_luminance(frames[idx + 1])
        diff = np.abs(luma_b - luma_a)

        for roi, ratios in ((left, per_pair_left), (right, per_pair_right)):
            region = roi_slice(diff, roi)
            if region.size == 0:
                ratios.append(0.0)
                continue
            moving = np.sum(region >= GATE_E_MOTION_DELTA_MIN)
            ratios.append(moving / float(region.size))

    avg_left = float(np.mean(per_pair_left)) if per_pair_left else 0.0
    avg_right = float(np.mean(per_pair_right)) if per_pair_right else 0.0
    passed_left = avg_left >= GATE_E_MOVING_RATIO_MIN
    passed_right = avg_right >= GATE_E_MOVING_RATIO_MIN
    return MotionMetrics(
        per_pair_left=per_pair_left,
        per_pair_right=per_pair_right,
        avg_left=avg_left,
        avg_right=avg_right,
        passed_left=passed_left,
        passed_right=passed_right,
    )


def gate_f_theme_separation(level_paths: Dict[int, str]) -> Tuple[float, float, bool, bool]:
    required_levels = (1, 10, 20)
    missing = [lvl for lvl in required_levels if lvl not in level_paths]
    if missing:
        raise ValueError(f"Missing required level screenshots: {missing}")

    def blurred_hist(path: str) -> np.ndarray:
        img = Image.open(path).convert("RGB")
        arr = np.array(img)
        luma = calculate_luminance(arr)
        sigma = max(20, int(round(0.01 * arr.shape[0])))
        blurred = gaussian_blur(luma, sigma)
        hist, _ = np.histogram(blurred, bins=64, range=(0, 255), density=False)
        hist = hist.astype(np.float32)
        norm = np.linalg.norm(hist)
        if norm == 0:
            return hist
        return hist / norm

    def correlation(a: np.ndarray, b: np.ndarray) -> float:
        return float(np.dot(a, b))

    hist_1 = blurred_hist(level_paths[1])
    hist_10 = blurred_hist(level_paths[10])
    hist_20 = blurred_hist(level_paths[20])

    corr_1_10 = correlation(hist_1, hist_10)
    corr_10_20 = correlation(hist_10, hist_20)

    pass_1_10 = corr_1_10 <= GATE_F_CORRELATION_MAX
    pass_10_20 = corr_10_20 <= GATE_F_CORRELATION_MAX

    return corr_1_10, corr_10_20, pass_1_10, pass_10_20


def find_images_in_dir(directory: str) -> List[str]:
    return sorted(glob.glob(os.path.join(directory, "*.png")))


def parse_level_number(path: str) -> Optional[int]:
    match = LEVEL_RE.search(os.path.basename(path))
    if not match:
        return None
    try:
        return int(match.group(1))
    except ValueError:
        return None


def pick_motion_dir(base_dir: str, preferred: Optional[str]) -> Optional[str]:
    candidates: List[str] = []
    if preferred:
        candidates.append(preferred)
    candidates.extend(
        [
            os.path.join(base_dir, "motion"),
            os.path.join(base_dir, "DecantraScreenshots", "motion"),
            os.path.join(os.path.dirname(base_dir), "motion"),
            os.path.join(os.path.dirname(base_dir), "DecantraScreenshots", "motion"),
        ]
    )
    for candidate in candidates:
        if candidate and os.path.isdir(candidate):
            frames = sorted(glob.glob(os.path.join(candidate, "frame-*.png")))
            if len(frames) >= 3:
                return candidate
    return None


def load_motion_frames(motion_dir: str) -> List[np.ndarray]:
    frame_paths = sorted(glob.glob(os.path.join(motion_dir, "frame-*.png")))
    frames: List[np.ndarray] = []
    for path in frame_paths:
        frame = np.array(Image.open(path).convert("RGB"))
        frames.append(frame)
    return frames


def verify_image(path: str, require_motion: bool, motion_metrics: Optional[MotionMetrics]) -> List[GateResult]:
    img = Image.open(path).convert("RGB")
    arr = np.array(img)
    height, width = arr.shape[:2]
    left, right = get_rois(width, height)

    luma = calculate_luminance(arr)
    sigma = max(20, int(round(0.01 * height)))
    blurred = gaussian_blur(luma, sigma)

    results: List[GateResult] = []

    # Gate A
    a_left = gate_a_cloud_visibility(blurred, left)
    a_right = gate_a_cloud_visibility(blurred, right)
    gate_a_passed = a_left[4] and a_right[4]
    results.append(
        GateResult(
            "Gate A",
            gate_a_passed,
            [
                f"Left ROI contrast={a_left[3]:.4f} (P05={a_left[0]:.2f}, P50={a_left[1]:.2f}, P95={a_left[2]:.2f})",
                f"Right ROI contrast={a_right[3]:.4f} (P05={a_right[0]:.2f}, P50={a_right[1]:.2f}, P95={a_right[2]:.2f})",
                f"Threshold contrast >= {GATE_A_CONTRAST_MIN}",
            ],
        )
    )

    # Gate B
    b_left_transitions, b_left_pass, _ = gate_b_transition_count(blurred, left)
    b_right_transitions, b_right_pass, _ = gate_b_transition_count(blurred, right)
    total_transitions = b_left_transitions + b_right_transitions
    gate_b_passed = (
        b_left_pass
        and b_right_pass
        and total_transitions >= GATE_B_TOTAL_TRANSITIONS_MIN
    )
    results.append(
        GateResult(
            "Gate B",
            gate_b_passed,
            [
                f"Left ROI transitions={b_left_transitions} (threshold >= {GATE_B_ROI_TRANSITIONS_MIN})",
                f"Right ROI transitions={b_right_transitions} (threshold >= {GATE_B_ROI_TRANSITIONS_MIN})",
                f"Total transitions={total_transitions} (threshold >= {GATE_B_TOTAL_TRANSITIONS_MIN})",
            ],
        )
    )

    # Gate C
    c_left_density, c_left_pass = gate_c_star_density(luma, left)
    c_right_density, c_right_pass = gate_c_star_density(luma, right)
    gate_c_passed = c_left_pass and c_right_pass
    results.append(
        GateResult(
            "Gate C",
            gate_c_passed,
            [
                f"Left ROI star_density={c_left_density:.6f} (threshold >= {GATE_C_STAR_DENSITY_MIN})",
                f"Right ROI star_density={c_right_density:.6f} (threshold >= {GATE_C_STAR_DENSITY_MIN})",
            ],
        )
    )

    # Gate D
    d_left_p50, d_left_p05, d_left_pass = gate_d_non_black(luma, left)
    d_right_p50, d_right_p05, d_right_pass = gate_d_non_black(luma, right)
    gate_d_passed = d_left_pass and d_right_pass
    results.append(
        GateResult(
            "Gate D",
            gate_d_passed,
            [
                f"Left ROI P50={d_left_p50:.2f}, P05={d_left_p05:.2f} (thresholds >= {GATE_D_P50_MIN} / {GATE_D_P05_MIN})",
                f"Right ROI P50={d_right_p50:.2f}, P05={d_right_p05:.2f} (thresholds >= {GATE_D_P50_MIN} / {GATE_D_P05_MIN})",
            ],
        )
    )

    # Gate E (global motion metrics)
    if require_motion:
        if motion_metrics is None:
            results.append(
                GateResult(
                    "Gate E",
                    False,
                    [
                        "Motion frames missing or insufficient (need >= 3 frames).",
                        "Generate motion frames with decantra motion capture and rerun.",
                    ],
                )
            )
        else:
            gate_e_passed = motion_metrics.passed_left and motion_metrics.passed_right
            results.append(
                GateResult(
                    "Gate E",
                    gate_e_passed,
                    [
                        f"Left ROI moving_ratio_avg={motion_metrics.avg_left:.6f} (threshold >= {GATE_E_MOVING_RATIO_MIN})",
                        f"Right ROI moving_ratio_avg={motion_metrics.avg_right:.6f} (threshold >= {GATE_E_MOVING_RATIO_MIN})",
                        f"Left ROI per-pair ratios={', '.join(f'{v:.6f}' for v in motion_metrics.per_pair_left)}",
                        f"Right ROI per-pair ratios={', '.join(f'{v:.6f}' for v in motion_metrics.per_pair_right)}",
                    ],
                )
            )

    return results


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify Decantra background visibility gates")
    parser.add_argument("--image", help="Single image to verify")
    parser.add_argument("--dir", help="Directory of screenshots to verify")
    parser.add_argument("--motion-dir", help="Directory containing motion frames")
    parser.add_argument("--require-motion", dest="require_motion", action="store_true", default=True)
    parser.add_argument("--no-require-motion", dest="require_motion", action="store_false")
    args = parser.parse_args()

    if not args.image and not args.dir:
        print("ERROR: Provide --image or --dir")
        return 1

    if args.image and args.dir:
        print("ERROR: Use only one of --image or --dir")
        return 1

    if args.image:
        if not os.path.exists(args.image):
            print(f"ERROR: Image not found: {args.image}")
            return 1
        images = [args.image]
        base_dir = os.path.dirname(args.image)
    else:
        base_dir = args.dir
        if not base_dir or not os.path.isdir(base_dir):
            print(f"ERROR: Directory not found: {base_dir}")
            return 1
        images = find_images_in_dir(base_dir)
        if not images:
            print(f"ERROR: No PNG images found in: {base_dir}")
            return 1

    # Motion frames (Gate E)
    motion_metrics: Optional[MotionMetrics] = None
    if args.require_motion:
        motion_dir = pick_motion_dir(base_dir, args.motion_dir)
        if motion_dir is None:
            print("ERROR: Motion frames missing. Require >= 3 frames in motion directory.")
            print("Expected motion directory under screenshots (e.g., <dir>/motion).")
        else:
            frames = load_motion_frames(motion_dir)
            if len(frames) < 3:
                print("ERROR: Motion frames missing. Require >= 3 frames.")
            else:
                height, width = frames[0].shape[:2]
                left, right = get_rois(width, height)
                motion_metrics = compute_motion_metrics(frames, left, right)

    # Gate F (theme separation)
    level_paths: Dict[int, str] = {}
    for path in find_images_in_dir(base_dir):
        level_num = parse_level_number(path)
        if level_num is not None:
            level_paths[level_num] = path

    gate_f_passed = False
    gate_f_details: List[str] = []
    try:
        corr_1_10, corr_10_20, pass_1_10, pass_10_20 = gate_f_theme_separation(level_paths)
        gate_f_passed = pass_1_10 and pass_10_20
        gate_f_details = [
            f"Level 1 vs 10 correlation={corr_1_10:.4f} (threshold <= {GATE_F_CORRELATION_MAX})",
            f"Level 10 vs 20 correlation={corr_10_20:.4f} (threshold <= {GATE_F_CORRELATION_MAX})",
        ]
    except ValueError as exc:
        gate_f_details = [str(exc)]
        gate_f_passed = False

    all_passed = True

    for path in images:
        print("=" * 72)
        print(f"Verifying {os.path.basename(path)}")
        print("=" * 72)
        image_results = verify_image(path, args.require_motion, motion_metrics)
        for result in image_results:
            status = "PASS" if result.passed else "FAIL"
            print(f"{result.name}: {status}")
            for detail in result.details:
                print(f"  - {detail}")
            all_passed = all_passed and result.passed

        print("Gate F: " + ("PASS" if gate_f_passed else "FAIL"))
        for detail in gate_f_details:
            print(f"  - {detail}")
        all_passed = all_passed and gate_f_passed

    if args.require_motion and motion_metrics is None:
        all_passed = False

    print("=" * 72)
    print("BACKGROUND VISIBILITY VERIFICATION RESULT: " + ("PASS" if all_passed else "FAIL"))
    print("=" * 72)

    return 0 if all_passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
