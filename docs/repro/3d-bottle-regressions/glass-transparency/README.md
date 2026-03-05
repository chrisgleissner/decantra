# Glass Transparency Regression Evidence

## Issue
Bottles appeared too milky/opaque — glass overlay washed out liquid colours.

## Fix (2026-03-05)

| Parameter | Before | After |
|---|---|---|
| `_GlassTint` alpha | 0.15 | 0.09 |
| `_FresnelColor` alpha | 0.38 | 0.22 |
| `_MaxGlassAlpha` | 0.35 | 0.20 |

**Effect**: Liquid colours now show through with ≥80% visibility (was ≥65%).
Fresnel edge brightening and Blinn-Phong specular are preserved for 3D cues.

## Expected screenshots (captured after Android build)

- `before_level_1.png` — milky glass obscuring liquid (pre-fix baseline)
- `after_level_1.png`  — vivid liquid visible through clear glass
- `after_level_36.png` — 3×3 grid with clear liquids

Screenshots are captured via `./build --screenshots`.
