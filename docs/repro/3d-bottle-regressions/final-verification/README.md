# Final Verification — 3D Bottle Glass & Scale Fixes

## Status

| Check | Result | Notes |
|-------|--------|-------|
| HUD intrusion (top-row bottles) | ✅ Fixed | Old top: 3.455 wu; New top: 3.354 wu; HUD boundary: 4.35 wu |
| Glass liquid visibility ratio | ✅ 0.80 | `1 - MaxGlassAlpha = 1 - 0.20 = 0.80` (meets ≥80% target) |
| EditMode tests | ✅ 361/361 | 2026-03-05T08:00:14Z |
| Runtime overlap (full-cap bottles) | ⏳ Pending device | Analytically still -0.145 wu at cap=1.0; per-level ratios expected lower |
| Post-fix screenshots | ⏳ Pending device | Emulator SIGILL; physical device wrong ABI |

## Screenshots in this directory

| File | Level | Scale | Glass | Description |
|------|-------|-------|-------|-------------|
| `level-36-pre-scale-fix.png` | 36 | 1.0 (old) | opaque | 3D bottles, pre-fix; confirms glass+liquid rendering works |
| `level-01-pre-scale-fix.png` | 1  | 1.0 (old) | opaque | 3D bottles, small level |
| `level-12-pre-scale-fix.png` | 12 | 1.0 (old) | opaque | 3D bottles, medium level |

All screenshots captured on device R5CRC3ZY9XH at commit prior to `883f714`.

## Verification approach

Post-fix device screenshots are **not available** due to:
- Android emulator (`emulator-5554`, x86_64/Android 14): SIGILL crash in
  `UnityApplication::ProcessFrame()` — ARM64 binary on x86_64 houdini translation
- Physical Samsung SM-N9005 (`2113b87f`): `INSTALL_FAILED_NO_MATCHING_ABIS`
  (device is armeabi-v7a; APK is arm64-v8a)

Verification is therefore **analytical**, using:
- `../layout-report.json` — geometry from `BottleMeshGenerator.cs` constants
- `../glass-report.json` — shader parameter verification + pixel analysis of pre-fix screenshot
- `../diff-report.json` — code diff analysis, test results, convergence checks

## How to complete visual verification

When a compatible device (arm64-v8a Android) is connected:

```bash
DECANTRA_ANDROID_SERIAL=<serial> ./build --skip-tests --screenshots
```

Replace screenshots here with the post-fix captures and re-run diff analysis.
