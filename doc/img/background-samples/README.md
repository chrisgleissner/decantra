# Background Samples

This directory contains generated background samples for quality inspection.

## Generating Samples

### From Unity Editor
1. Open the project in Unity
2. Menu: **Decantra → Generate Background Samples**

### From Command Line
```bash
./build --generate-background-samples
```

## Sample Files

| File Pattern | Description |
|-------------|-------------|
| `AtmosphericWash_zone{N}_seed{HASH}.png` | Soft atmospheric gradient samples |
| `DomainWarpedClouds_zone{N}_seed{HASH}.png` | Organic cloud-like samples |
| `CurlFlowAdvection_zone{N}_seed{HASH}.png` | Flowing curl-noise samples |
| `theme_transitions.png` | Side-by-side comparison of all archetypes |

## Quality Criteria

Generated backgrounds should exhibit:
- ✅ Modern, organic aesthetics
- ✅ Smooth, continuous gradients
- ✅ No grid artifacts
- ✅ No polygonal structures
- ✅ No wallpaper-like repetition
- ✅ Distinct visual identity per archetype
