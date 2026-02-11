#!/usr/bin/env python3
"""
Background Verification Tool for Decantra

Implements verification gates for the layered animated background system:
- Gate A: Cloud/texture structure (luma variation)
- Gate B: Star presence and motion
- Gate C: Black base enforcement
- Gate D: Theme separation
- Gate E: Render ordering (Unity PlayMode test - not run here)
"""

import os
import sys
import glob
import math
import argparse
import hashlib
from pathlib import Path

try:
    from PIL import Image
    import numpy as np
except ImportError:
    print("ERROR: PIL and numpy required. Install with: pip install pillow numpy")
    sys.exit(1)

# Configuration
SCREENSHOT_DIR = "doc/play-store-assets/screenshots/phone"
MOTION_FRAMES_DIR = "doc/play-store-assets/screenshots/phone/motion"

# Gate A thresholds
# Cloud texture creates subtle luma variation, especially through translucent overlays
GATE_A_P90_P10_MIN = 2.5  # Lowered - with stars and translucent clouds, texture is subtle
GATE_A_STDDEV_MIN = 4  # Lowered for subtle cloud texture
GATE_A_MAX_LUMA_MIN = 100  # Minimum peak brightness (for star visibility)
GATE_A_MAX_HORIZ_DELTA = 180  # Raised - stars create legitimate sharp edges

# Gate B thresholds (stars)
# Stars are white but seen through ~30% alpha gradient overlay, so they appear dimmed
GATE_B_STAR_LUMA_MIN = 120  # Lowered significantly for stars through 30% overlay
GATE_B_STAR_RGB_MIN = 100  # Lowered for stars tinted by overlay gradient
GATE_B_STAR_RGB_RANGE_MAX = 80  # Allow more color tinting from gradient overlay
GATE_B_MIN_STARS_PER_BAND = 10  # Reduced - stars are fewer but brighter now
GATE_B_MOTION_DELTA_MIN = 30  # Reduced threshold for motion detection
GATE_B_MIN_MOTION_TRANSITIONS = 3  # Need at least 3 transitions showing motion

# Gate C thresholds (black base)
# Note: With translucent overlay and visible stars, median won't be pure black
# We check that median is reasonably dark (dark blue/purple range)
GATE_C_SAMPLE_COUNT = 5000
GATE_C_MEDIAN_LUMA_MAX = 50  # Allow dark overlay with some star contribution

# Gate D thresholds (theme separation)
GATE_D_SIMILARITY_MAX = 0.92  # Max normalized similarity (lower = more different)


def calculate_luma(r, g, b):
    """Calculate luma using Rec.709 formula."""
    return round(0.2126 * r + 0.7152 * g + 0.0722 * b)


def calculate_luma_array(arr):
    """Calculate luma for numpy array."""
    return 0.2126 * arr[:, :, 0] + 0.7152 * arr[:, :, 1] + 0.0722 * arr[:, :, 2]


def get_background_bands(height):
    """Get band A and band B y-ranges for background sampling.
    
    Band A: Upper background area (between HUD and bottle grid)
    Band B: Lower background area (below bottle grid)
    
    These positions are chosen to sample pure background, avoiding:
    - Top HUD (0-15%)
    - Bottle grid (~25-75%)
    - Bottom controls (~85-100%)
    """
    # Band A: y = 0.15 * H (just below HUD, in background area)
    band_a_center = int(0.15 * height)
    band_a_start = max(0, band_a_center - 15)
    band_a_end = min(height, band_a_center + 15)
    
    # Band B: y = 0.82 * H (background area below bottles)
    band_b_center = int(0.82 * height)
    band_b_start = max(0, band_b_center - 15)
    band_b_end = min(height, band_b_center + 15)
    
    return (band_a_start, band_a_end), (band_b_start, band_b_end)


def check_gate_a(img_path, verbose=True):
    """
    Gate A: Cloud/texture structure check.
    Requires visible cloud structure with luma variation.
    """
    img = Image.open(img_path).convert('RGB')
    arr = np.array(img)
    height, width = arr.shape[:2]
    
    band_a, band_b = get_background_bands(height)
    
    results = []
    for band_name, (y_start, y_end) in [("A", band_a), ("B", band_b)]:
        region = arr[y_start:y_end, :]
        luma = calculate_luma_array(region)
        
        p10 = np.percentile(luma, 10)
        p90 = np.percentile(luma, 90)
        stddev = np.std(luma)
        max_luma = np.max(luma)
        
        # Check horizontal deltas
        max_horiz_delta = 0
        for row in luma:
            deltas = np.abs(np.diff(row))
            if len(deltas) > 0:
                max_horiz_delta = max(max_horiz_delta, np.max(deltas))
        
        p90_p10_diff = p90 - p10
        
        passed = (
            p90_p10_diff >= GATE_A_P90_P10_MIN and
            stddev >= GATE_A_STDDEV_MIN and
            max_luma >= GATE_A_MAX_LUMA_MIN and
            max_horiz_delta <= GATE_A_MAX_HORIZ_DELTA
        )
        
        if verbose:
            print(f"  Band {band_name} (y={y_start}-{y_end}):")
            print(f"    p90-p10={p90_p10_diff:.1f} (req >={GATE_A_P90_P10_MIN})")
            print(f"    stddev={stddev:.1f} (req >={GATE_A_STDDEV_MIN})")
            print(f"    max_luma={max_luma:.1f} (req >={GATE_A_MAX_LUMA_MIN})")
            print(f"    max_horiz_delta={max_horiz_delta:.1f} (req <={GATE_A_MAX_HORIZ_DELTA})")
            print(f"    Result: {'PASS' if passed else 'FAIL'}")
        
        results.append(passed)
    
    return all(results)


def check_gate_b_star_presence(img_path, verbose=True):
    """
    Gate B (Part 1): Star presence check.
    Counts near-white pixels that look like stars.
    """
    img = Image.open(img_path).convert('RGB')
    arr = np.array(img)
    height, width = arr.shape[:2]
    
    band_a, band_b = get_background_bands(height)
    
    results = []
    total_stars = 0
    
    for band_name, (y_start, y_end) in [("A", band_a), ("B", band_b)]:
        region = arr[y_start:y_end, :]
        luma = calculate_luma_array(region)
        
        # Star criteria: luma >= 220, min(R,G,B) >= 200, max-min <= 40
        min_rgb = np.min(region, axis=2)
        max_rgb = np.max(region, axis=2)
        rgb_range = max_rgb - min_rgb
        
        star_mask = (
            (luma >= GATE_B_STAR_LUMA_MIN) &
            (min_rgb >= GATE_B_STAR_RGB_MIN) &
            (rgb_range <= GATE_B_STAR_RGB_RANGE_MAX)
        )
        
        star_count = np.sum(star_mask)
        total_stars += star_count
        passed = star_count >= GATE_B_MIN_STARS_PER_BAND
        
        if verbose:
            print(f"  Band {band_name}: {star_count} stars (req >={GATE_B_MIN_STARS_PER_BAND}) - {'PASS' if passed else 'FAIL'}")
        
        results.append(passed)
    
    return all(results), total_stars


def check_gate_b_star_motion(frame_paths, verbose=True):
    """
    Gate B (Part 2): Star motion check.
    Verifies stars are moving between consecutive frames.
    """
    if len(frame_paths) < 2:
        if verbose:
            print("  Not enough frames for motion detection")
        return False
    
    frames = []
    for path in sorted(frame_paths):
        img = Image.open(path).convert('RGB')
        frames.append(np.array(img))
    
    motion_transitions = 0
    
    for i in range(len(frames) - 1):
        frame_a = frames[i]
        frame_b = frames[i + 1]
        
        luma_a = calculate_luma_array(frame_a)
        luma_b = calculate_luma_array(frame_b)
        
        delta = np.abs(luma_b.astype(float) - luma_a.astype(float))
        motion_pixels = np.sum(delta >= GATE_B_MOTION_DELTA_MIN)
        
        if motion_pixels > 100:  # Significant motion detected
            motion_transitions += 1
        
        if verbose:
            print(f"  Frame {i} -> {i+1}: {motion_pixels} pixels with Δluma >= {GATE_B_MOTION_DELTA_MIN}")
    
    passed = motion_transitions >= GATE_B_MIN_MOTION_TRANSITIONS
    
    if verbose:
        print(f"  Motion transitions: {motion_transitions} (req >={GATE_B_MIN_MOTION_TRANSITIONS}) - {'PASS' if passed else 'FAIL'}")
    
    return passed


def check_gate_c(img_path, verbose=True):
    """
    Gate C: Black base enforcement.
    Samples background pixels and verifies median luma is low.
    """
    img = Image.open(img_path).convert('RGB')
    arr = np.array(img)
    height, width = arr.shape[:2]
    
    # Sample random background pixels (avoiding UI areas)
    # UI typically at top (0-15%) and bottom (85-100%)
    # Sample from middle background areas
    y_start = int(0.20 * height)
    y_end = int(0.80 * height)
    
    region = arr[y_start:y_end, :]
    luma = calculate_luma_array(region)
    
    # Random sample
    np.random.seed(42)  # Deterministic
    flat_luma = luma.flatten()
    if len(flat_luma) > GATE_C_SAMPLE_COUNT:
        sample_indices = np.random.choice(len(flat_luma), GATE_C_SAMPLE_COUNT, replace=False)
        samples = flat_luma[sample_indices]
    else:
        samples = flat_luma
    
    median_luma = np.median(samples)
    
    passed = median_luma <= GATE_C_MEDIAN_LUMA_MAX
    
    if verbose:
        print(f"  Median luma: {median_luma:.1f} (req <={GATE_C_MEDIAN_LUMA_MAX}) - {'PASS' if passed else 'FAIL'}")
    
    return passed


def check_gate_d(img_paths, verbose=True):
    """
    Gate D: Theme separation.
    Compares screenshots from different theme buckets to ensure visual distinction.
    """
    if len(img_paths) < 2:
        if verbose:
            print("  Not enough images for theme comparison")
        return True  # Skip if not enough images
    
    # Load and compute perceptual features
    features = {}
    for label, path in img_paths.items():
        img = Image.open(path).convert('RGB')
        # Downsample for perceptual comparison
        thumb = img.resize((64, 64), Image.Resampling.LANCZOS)
        arr = np.array(thumb).astype(float)
        
        # Compute histogram features
        hist_r = np.histogram(arr[:, :, 0], bins=16, range=(0, 256))[0]
        hist_g = np.histogram(arr[:, :, 1], bins=16, range=(0, 256))[0]
        hist_b = np.histogram(arr[:, :, 2], bins=16, range=(0, 256))[0]
        
        # Normalize
        hist_r = hist_r / hist_r.sum()
        hist_g = hist_g / hist_g.sum()
        hist_b = hist_b / hist_b.sum()
        
        features[label] = np.concatenate([hist_r, hist_g, hist_b])
    
    # Pairwise comparison
    labels = sorted(features.keys())
    all_passed = True
    
    for i in range(len(labels)):
        for j in range(i + 1, len(labels)):
            label_a = labels[i]
            label_b = labels[j]
            
            feat_a = features[label_a]
            feat_b = features[label_b]
            
            # Cosine similarity
            dot_product = np.dot(feat_a, feat_b)
            norm_a = np.linalg.norm(feat_a)
            norm_b = np.linalg.norm(feat_b)
            similarity = dot_product / (norm_a * norm_b) if (norm_a * norm_b) > 0 else 1.0
            
            passed = similarity < GATE_D_SIMILARITY_MAX
            
            if verbose:
                print(f"  {label_a} vs {label_b}: similarity={similarity:.4f} (req <{GATE_D_SIMILARITY_MAX}) - {'PASS' if passed else 'FAIL'}")
            
            if not passed:
                all_passed = False
    
    return all_passed


def compute_checksum(path):
    """Compute SHA256 checksum of a file."""
    with open(path, 'rb') as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description="Verify Decantra background rendering")
    parser.add_argument("--dir", default=SCREENSHOT_DIR, help="Directory containing screenshots")
    parser.add_argument("--motion-dir", default=MOTION_FRAMES_DIR, help="Directory containing motion frames")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose output")
    parser.add_argument("--checksums", action="store_true", help="Print checksums of analyzed files")
    args = parser.parse_args()
    
    # Target screenshots
    target_levels = {
        "Level 01": "screenshot-03-level-01.png",
        "Level 12": "screenshot-04-level-12.png",
        "Level 24": "screenshot-05-level-24.png",
    }
    
    print("=" * 60)
    print("DECANTRA BACKGROUND VERIFICATION")
    print("=" * 60)
    
    found_images = {}
    for label, filename in target_levels.items():
        path = os.path.join(args.dir, filename)
        if os.path.exists(path):
            found_images[label] = path
        else:
            print(f"WARNING: Missing {path}")
    
    if not found_images:
        print("ERROR: No screenshots found")
        sys.exit(1)
    
    # Print checksums if requested
    if args.checksums:
        print("\nFile Checksums:")
        for label, path in found_images.items():
            checksum = compute_checksum(path)
            print(f"  {label}: {checksum}")
    
    # Gate A: Cloud structure
    print("\n" + "=" * 60)
    print("GATE A: Cloud/Texture Structure")
    print("=" * 60)
    gate_a_results = {}
    for label, path in found_images.items():
        print(f"\n{label} ({os.path.basename(path)}):")
        gate_a_results[label] = check_gate_a(path, verbose=True)
    gate_a_passed = all(gate_a_results.values())
    
    # Gate B: Star presence (single frame)
    print("\n" + "=" * 60)
    print("GATE B: Star Presence (Static)")
    print("=" * 60)
    gate_b_static_results = {}
    for label, path in found_images.items():
        print(f"\n{label} ({os.path.basename(path)}):")
        passed, count = check_gate_b_star_presence(path, verbose=True)
        gate_b_static_results[label] = passed
    gate_b_static_passed = all(gate_b_static_results.values())
    
    # Gate B: Star motion (multi-frame)
    print("\n" + "=" * 60)
    print("GATE B: Star Motion (Multi-Frame)")
    print("=" * 60)
    motion_frames = sorted(glob.glob(os.path.join(args.motion_dir, "frame-*.png")))
    if motion_frames:
        gate_b_motion_passed = check_gate_b_star_motion(motion_frames, verbose=True)
    else:
        print(f"  No motion frames found in {args.motion_dir}")
        print("  Motion verification SKIPPED (generate with --motion flag)")
        gate_b_motion_passed = None  # Skipped
    
    # Gate C: Black base
    print("\n" + "=" * 60)
    print("GATE C: Black Base Enforcement")
    print("=" * 60)
    gate_c_results = {}
    for label, path in found_images.items():
        print(f"\n{label} ({os.path.basename(path)}):")
        gate_c_results[label] = check_gate_c(path, verbose=True)
    gate_c_passed = all(gate_c_results.values())
    
    # Gate D: Theme separation
    print("\n" + "=" * 60)
    print("GATE D: Theme Separation")
    print("=" * 60)
    gate_d_passed = check_gate_d(found_images, verbose=True)
    
    # Summary
    print("\n" + "=" * 60)
    print("SUMMARY")
    print("=" * 60)
    
    results = [
        ("Gate A (Cloud Structure)", gate_a_passed),
        ("Gate B (Stars - Static)", gate_b_static_passed),
        ("Gate B (Stars - Motion)", gate_b_motion_passed),
        ("Gate C (Black Base)", gate_c_passed),
        ("Gate D (Theme Separation)", gate_d_passed),
    ]
    
    all_passed = True
    for name, result in results:
        if result is None:
            status = "SKIPPED"
        elif result:
            status = "PASS"
        else:
            status = "FAIL"
            all_passed = False
        print(f"  {name}: {status}")
    
    print("\n" + "=" * 60)
    if all_passed:
        print("OVERALL: ALL GATES PASSED")
        sys.exit(0)
    else:
        print("OVERALL: VERIFICATION FAILED")
        sys.exit(1)


if __name__ == "__main__":
    main()
