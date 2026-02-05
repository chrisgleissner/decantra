import os
import math
from dataclasses import dataclass
from typing import Tuple, Dict
import numpy as np
from PIL import Image
from skimage.metrics import structural_similarity as ssim
from skimage.feature import canny
from skimage.transform import hough_line, hough_line_peaks
from skimage.filters import sobel, gaussian

@dataclass
class ImageMetrics:
    mae: float
    rmse: float
    ssim: float
    hist_l1: float
    edge_density: float
    hough_peak_ratio: float

@dataclass
class EdgeMetrics:
    variance: float
    gradient_mean: float


def load_gray(path: str) -> np.ndarray:
    img = Image.open(path).convert("RGB")
    arr = np.asarray(img).astype(np.float32) / 255.0
    # luminance
    gray = 0.2126 * arr[:, :, 0] + 0.7152 * arr[:, :, 1] + 0.0722 * arr[:, :, 2]
    return gray


def compare_images(baseline: np.ndarray, current: np.ndarray) -> ImageMetrics:
    if baseline.shape != current.shape:
        raise ValueError(f"Shape mismatch {baseline.shape} vs {current.shape}")
    diff = current - baseline
    mae = float(np.mean(np.abs(diff)))
    rmse = float(np.sqrt(np.mean(diff ** 2)))
    ssim_val = float(ssim(baseline, current, data_range=1.0))

    # Histogram L1 distance
    hist_bins = 256
    b_hist, _ = np.histogram(baseline, bins=hist_bins, range=(0, 1), density=True)
    c_hist, _ = np.histogram(current, bins=hist_bins, range=(0, 1), density=True)
    hist_l1 = float(np.sum(np.abs(b_hist - c_hist)) / hist_bins)

    # Edge density + Hough peak ratio
    edges = canny(current, sigma=1.2)
    edge_density = float(np.mean(edges))
    if edges.any():
        hspace, angles, dists = hough_line(edges)
        peaks = hough_line_peaks(hspace, angles, dists, num_peaks=8, threshold=0.3 * np.max(hspace))
        peak_strength = float(np.sum(peaks[0])) if len(peaks[0]) else 0.0
        hough_peak_ratio = peak_strength / (np.sum(edges) + 1e-6)
    else:
        hough_peak_ratio = 0.0

    return ImageMetrics(mae, rmse, ssim_val, hist_l1, edge_density, hough_peak_ratio)


def border_regions(gray: np.ndarray, border_frac: float = 0.18) -> Dict[str, np.ndarray]:
    h, w = gray.shape
    by = int(h * border_frac)
    bx = int(w * border_frac)
    return {
        "top": gray[:by, :],
        "bottom": gray[h - by:, :],
        "left": gray[:, :bx],
        "right": gray[:, w - bx:]
    }


def compare_border_metrics(baseline: np.ndarray, current: np.ndarray, border_frac: float = 0.18) -> ImageMetrics:
    base_regions = border_regions(baseline, border_frac)
    curr_regions = border_regions(current, border_frac)
    metrics = []
    for key in base_regions.keys():
        metrics.append(compare_images(base_regions[key], curr_regions[key]))

    mae = float(np.mean([m.mae for m in metrics]))
    rmse = float(np.mean([m.rmse for m in metrics]))
    ssim_val = float(np.mean([m.ssim for m in metrics]))
    hist_l1 = float(np.mean([m.hist_l1 for m in metrics]))
    edge_density = float(np.mean([m.edge_density for m in metrics]))
    hough_peak_ratio = float(np.mean([m.hough_peak_ratio for m in metrics]))
    return ImageMetrics(mae, rmse, ssim_val, hist_l1, edge_density, hough_peak_ratio)


def edge_strip_metrics(gray: np.ndarray, strip: int = 16) -> Dict[str, EdgeMetrics]:
    h, w = gray.shape
    strips = {
        "top": gray[:strip, :],
        "bottom": gray[h - strip:, :],
        "left": gray[:, :strip],
        "right": gray[:, w - strip:]
    }
    result = {}
    for key, arr in strips.items():
        variance = float(np.var(arr))
        grad = float(np.mean(sobel(arr)))
        result[key] = EdgeMetrics(variance, grad)
    return result


def summarize_edge_metrics(metrics: Dict[str, EdgeMetrics]) -> Tuple[float, float]:
    variances = [m.variance for m in metrics.values()]
    gradients = [m.gradient_mean for m in metrics.values()]
    return float(np.mean(variances)), float(np.mean(gradients))


def evaluate_pass(metrics: ImageMetrics, border_metrics: ImageMetrics, blur_metrics: ImageMetrics, edge_delta: Tuple[float, float], edge_abs: Tuple[float, float]) -> Tuple[bool, str]:
    edge_var_ratio, edge_grad_ratio = edge_delta
    edge_var_abs, edge_grad_abs = edge_abs
    # Pass criteria:
    # - Blurred SSIM below 0.90 OR blurred MAE above 0.02 (focus on low-frequency background)
    # - Histogram L1 distance above 0.02
    # - Edge straight-line dominance reduced (hough_peak_ratio below 0.12)
    # - Edge variance and gradient above minimal thresholds (non-uniform edges)
    ssim_ok = blur_metrics.ssim < 0.90 or blur_metrics.mae > 0.02
    hist_ok = metrics.hist_l1 > 0.02
    hough_ok = metrics.hough_peak_ratio < 0.12
    edge_ok = edge_var_abs > 0.0035 and edge_grad_abs > 0.003
    passed = ssim_ok and hist_ok and hough_ok and edge_ok
    details = f"ssim_ok={ssim_ok}, hist_ok={hist_ok}, hough_ok={hough_ok}, edge_ok={edge_ok}"
    return passed, details


def main():
    base_dir = "/home/chris/dev/decantra/doc/play-store-assets/screenshots/phone"
    baseline_dir = os.path.join(base_dir, "_baseline")

    pairs = {
        "level_01": (
            os.path.join(baseline_dir, "screenshot-03-level-01-baseline-2026-02-05.png"),
            os.path.join(base_dir, "screenshot-03-level-01.png")
        ),
        "level_12": (
            os.path.join(baseline_dir, "screenshot-04-level-12-baseline-2026-02-05.png"),
            os.path.join(base_dir, "screenshot-04-level-12.png")
        ),
        "level_24": (
            os.path.join(baseline_dir, "screenshot-05-level-24-baseline-2026-02-05.png"),
            os.path.join(base_dir, "screenshot-05-level-24.png")
        )
    }

    for level, (baseline_path, current_path) in pairs.items():
        if not os.path.exists(baseline_path):
            raise FileNotFoundError(baseline_path)
        if not os.path.exists(current_path):
            raise FileNotFoundError(current_path)

        baseline = load_gray(baseline_path)
        current = load_gray(current_path)
        metrics = compare_images(baseline, current)

        border_metrics = compare_border_metrics(baseline, current)

        blurred_base = gaussian(baseline, sigma=8, preserve_range=True)
        blurred_curr = gaussian(current, sigma=8, preserve_range=True)
        blur_metrics = compare_images(blurred_base, blurred_curr)

        base_edges = edge_strip_metrics(baseline)
        curr_edges = edge_strip_metrics(current)

        base_var, base_grad = summarize_edge_metrics(base_edges)
        curr_var, curr_grad = summarize_edge_metrics(curr_edges)

        edge_var_ratio = (curr_var / base_var) if base_var > 1e-6 else float("inf")
        edge_grad_ratio = (curr_grad / base_grad) if base_grad > 1e-6 else float("inf")

        passed, detail = evaluate_pass(metrics, border_metrics, blur_metrics, (edge_var_ratio, edge_grad_ratio), (curr_var, curr_grad))

        print(f"{level}:")
        print(f"  mae={metrics.mae:.4f} rmse={metrics.rmse:.4f} ssim={metrics.ssim:.4f} hist_l1={metrics.hist_l1:.4f}")
        print(f"  border_mae={border_metrics.mae:.4f} border_ssim={border_metrics.ssim:.4f}")
        print(f"  blur_mae={blur_metrics.mae:.4f} blur_ssim={blur_metrics.ssim:.4f}")
        print(f"  edge_density={metrics.edge_density:.4f} hough_peak_ratio={metrics.hough_peak_ratio:.4f}")
        print(f"  edge_var={curr_var:.4f} edge_grad={curr_grad:.4f} edge_var_ratio={edge_var_ratio:.3f} edge_grad_ratio={edge_grad_ratio:.3f}")
        print(f"  pass={passed} ({detail})\n")

if __name__ == "__main__":
    main()
