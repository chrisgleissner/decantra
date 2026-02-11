#!/usr/bin/env python3
import os
import sys
import glob
import math
import argparse
from PIL import Image

# Configuration / Constants
GATE_A_EDGE_DELTA_THRESHOLD = 0x40  # 64 decimal
GATE_B_MEAN_LUMA_MIN = 18.0
GATE_B_STDDEV_MIN = 6.0
GATE_B_DARK_PIXEL_PCT_MAX = 0.85
GATE_B_DARK_PIXEL_THRESHOLD = 16
GATE_C_SIMILARITY_MAX = 0.10  # Arbitrary threshold to ensure themes are different (1.0 = identical)

def calculate_luma(r, g, b):
    # Rec.709 luma approximation
    return int(round(0.2126 * r + 0.7152 * g + 0.0722 * b))

def check_gate_a_edge_strip(img_path, img):
    """
    Gate A: Edge-strip brightness transition check.
    FAIL if any absolute delta between consecutive pixels in the border strip > 64.
    """
    width, height = img.size
    pixels = img.load()
    
    # Extract border strip coordinates in clockwise order
    coords = []
    # Top: x=0..W-1 at y=0
    for x in range(width):
        coords.append((x, 0))
    # Right: y=1..H-2 at x=W-1
    for y in range(1, height - 1):
        coords.append((width - 1, y))
    # Bottom: x=W-1..0 at y=H-1
    for x in range(width - 1, -1, -1):
        coords.append((x, height - 1))
    # Left: y=H-2..1 at x=0
    for y in range(height - 2, 0, -1):
        coords.append((0, y))
        
    border_lumas = []
    for x, y in coords:
        r, g, b = pixels[x, y][:3]
        border_lumas.append(calculate_luma(r, g, b))
        
    max_delta = 0
    max_delta_idx = -1
    
    for i in range(len(border_lumas) - 1):
        delta = abs(border_lumas[i] - border_lumas[i+1])
        if delta > max_delta:
            max_delta = delta
            max_delta_idx = i
            
    # Check wraparound (first vs last)
    delta_wrap = abs(border_lumas[-1] - border_lumas[0])
    if delta_wrap > max_delta:
        max_delta = delta_wrap
        max_delta_idx = len(border_lumas) - 1

    passed = max_delta <= GATE_A_EDGE_DELTA_THRESHOLD
    print(f"[Gate A] {os.path.basename(img_path)}: Max Delta = {max_delta} (0x{max_delta:02X}) at index {max_delta_idx}. Result: {'PASS' if passed else 'FAIL'}")
    return passed

def check_gate_b_darkness(img_path, img):
    """
    Gate B: Not totally black or near-uniform.
    """
    width, height = img.size
    pixels = img.getdata()
    total_pixels = width * height
    
    luma_values = []
    dark_pixel_count = 0
    
    for p in pixels:
        l = calculate_luma(p[0], p[1], p[2])
        luma_values.append(l)
        if l < GATE_B_DARK_PIXEL_THRESHOLD:
            dark_pixel_count += 1
            
    mean_luma = sum(luma_values) / total_pixels
    variance = sum([(l - mean_luma) ** 2 for l in luma_values]) / total_pixels
    stddev = math.sqrt(variance)
    dark_pct = dark_pixel_count / total_pixels
    
    fail_condition_1 = (mean_luma < GATE_B_MEAN_LUMA_MIN and stddev < GATE_B_STDDEV_MIN)
    fail_condition_2 = (dark_pct > GATE_B_DARK_PIXEL_PCT_MAX)
    
    passed = not (fail_condition_1 or fail_condition_2)
    
    print(f"[Gate B] {os.path.basename(img_path)}: Mean={mean_luma:.2f}, StdDev={stddev:.2f}, DarkPct={dark_pct*100:.1f}%. Result: {'PASS' if passed else 'FAIL'}")
    if not passed:
        if fail_condition_1: print(f"  -> FAILED: Too dark and flat (Mean < {GATE_B_MEAN_LUMA_MIN} and StdDev < {GATE_B_STDDEV_MIN})")
        if fail_condition_2: print(f"  -> FAILED: Too many dark pixels (> {GATE_B_DARK_PIXEL_PCT_MAX*100}%)")
        
    return passed

def check_gate_c_themes(img_paths_dict):
    """
    Gate C: Theme switching sanity check.
    Compares Level 1 vs Level 12 vs Level 24.
    """
    # Simply using histogram comparisons for difference
    # A low distance score means images are similar.
    # We want them to be different.
    
    def get_hist(path):
        with Image.open(path) as im:
            return im.histogram()

    def compare_hist(h1, h2):
        # Chi-Square distance
        if len(h1) != len(h2): return 0.0
        score = 0.0
        for i in range(len(h1)):
            if h1[i] + h2[i] > 0:
                score += ((h1[i] - h2[i]) ** 2) / (h1[i] + h2[i])
        return score 

    # We need at least 2 images to compare
    keys = sorted(img_paths_dict.keys())
    if len(keys) < 2:
        print("[Gate C] Not enough images to compare for theme variation. SKIPPING.")
        return True

    print(f"[Gate C] Comparing themes: {keys}")
    
    # Load hashes/histograms
    hists = {}
    for k in keys:
        try:
            hists[k] = get_hist(img_paths_dict[k])
        except Exception as e:
            print(f"  -> Error loading {k}: {e}")
            return False

    all_passed = True
    
    # Pairwise comparison
    for i in range(len(keys)):
        for j in range(i + 1, len(keys)):
            k1 = keys[i]
            k2 = keys[j]
            
            # Simple Normalized MSE could also work if sizes are same
            # Let's rely on histogram difference being large enough.
            # But "Meaningfully different" is tricky with histogram if the distribution is similar but content is rotated.
            # The prompt suggests "perceptual hash distance, SSIM, or normalized MSE".
            # For simplicity and no extra deps, let's use a simplified pixel-wise MSE after resizing to thumbnail.
            
            with Image.open(img_paths_dict[k1]) as im1, Image.open(img_paths_dict[k2]) as im2:
                t1 = im1.resize((64, 64)).convert("RGB")
                t2 = im2.resize((64, 64)).convert("RGB")
                
                diff_sum = 0
                pixels1 = t1.getdata()
                pixels2 = t2.getdata()
                
                for p1, p2 in zip(pixels1, pixels2):
                    # Euler distance in RGB space
                    d = math.sqrt((p1[0]-p2[0])**2 + (p1[1]-p2[1])**2 + (p1[2]-p2[2])**2)
                    diff_sum += d
                
                avg_diff_per_pixel = diff_sum / (64*64)
                # Max diff per pixel is sqrt(255^2 * 3) ~= 441.
                normalized_diff = avg_diff_per_pixel / 441.0
                
                # Threshold: If they are too similar (diff < threshold), correct theme logic might be broken
                # However, if the bug is "black screen", they will be very similar (black vs black).
                # If they are different themes, they should have different colors/structures.
                
                passed = normalized_diff > GATE_C_SIMILARITY_MAX
                print(f"  -> {k1} vs {k2}: Similarity Gap = {normalized_diff:.4f} (Threshold > {GATE_C_SIMILARITY_MAX}). Result: {'PASS' if passed else 'FAIL'}")
                if not passed:
                    all_passed = False

    return all_passed

def main():
    parser = argparse.ArgumentParser(description="Verify Decantra screenshots")
    parser.add_argument("--dir", default="doc/play-store-assets/screenshots/phone", help="Directory containing screenshots")
    args = parser.parse_args()
    
    target_levels = {
        "Level 01": "screenshot-03-level-01.png",
        "Level 12": "screenshot-04-level-12.png",
        "Level 24": "screenshot-05-level-24.png"
    }
    
    found_images = {}
    
    gate_a_passed = True
    gate_b_passed = True
    
    print("=== STARTING VERIFICATION ===")
    
    for label, filename in target_levels.items():
        path = os.path.join(args.dir, filename)
        if not os.path.exists(path):
            print(f"ERROR: file not found: {path}")
            gate_a_passed = False
            continue
            
        found_images[label] = path
        
        try:
            with Image.open(path) as img:
                img = img.convert("RGB")
                if not check_gate_a_edge_strip(path, img):
                    gate_a_passed = False
                if not check_gate_b_darkness(path, img):
                    gate_b_passed = False
        except Exception as e:
            print(f"ERROR processing {path}: {e}")
            gate_a_passed = False
            gate_b_passed = False
            
    gate_c_passed = check_gate_c_themes(found_images)
    
    print("=== RESULTS ===")
    print(f"Gate A (Edges): {'PASS' if gate_a_passed else 'FAIL'}")
    print(f"Gate B (Dark):  {'PASS' if gate_b_passed else 'FAIL'}")
    print(f"Gate C (Theme): {'PASS' if gate_c_passed else 'FAIL'}")
    
    if gate_a_passed and gate_b_passed and gate_c_passed:
        sys.exit(0)
    else:
        sys.exit(1)

if __name__ == "__main__":
    main()
