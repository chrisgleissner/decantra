# Final Verification v2 — 3D Bottle Visual Improvements

Verification artifacts for the three visual improvements implemented in section 20
of PLANS.md.

## Expected artifacts (populated by `./build --screenshots`)

| File | Verifies |
|------|----------|
| `level-20.png` | Level 20 — liquid clarity, no bottom stripe |
| `level-36.png` | Level 36 — layout within HUD bounds |
| `level-10-3x3.png` | Level 10 — 3×3 bottle grid, no ghost bottles |
| `sink-bottle.png` | Sink bottle — reflective dome highlight visible against dark bg |
| `completed-bottle-topper.png` | Completed bottle — coloured topper cap above neck |

## JSON report

[v2-layout-report.json](../v2-layout-report.json) is generated at runtime by
`Bottle3DView.WriteReport()` and pulled from the device after a screenshots run.

## Status

- [ ] Artifacts populated (requires `./build --screenshots` with connected device)
