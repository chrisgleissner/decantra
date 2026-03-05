# Final Verification v3 — Cork Stoppers & Floating-Indicator Removal

Verification artifacts for Plan 21 (PLANS.md section 21).

## Changes verified in this pass

1. **Cork stopper only on completed bottles** — cork is hidden by default and becomes
   visible only when a bottle is fully solved (`IsSolvedBottle()` = full + monochrome).
2. **Floating indicators removed** — the neutral-beige placeholder cylinder that appeared
   on every bottle is now suppressed; bottles with no cork simply show their glass body.
3. **Spec-compliant cork geometry** — StopperRadius = NeckRadius × 1.05 (≈ 0.147 wu),
   70% inside the neck, 30% protruding above the rim.
4. **Lit cork material** — URP Lit / Standard shader instead of Unlit/Color, so the cork
   responds to scene directional lights (key + rim lights added in Plan 18).
5. **Correct completion condition** — `IsSolvedBottle()` requires `IsFull`, fixing the
   earlier bug where a partial-fill monochrome bottle would show a tinted stopper.

## Expected artifacts (populated by `./build --screenshots`)

| File | Verifies |
|------|----------|
| `level-20.png` | Level 20 — no overlap, bottles within HUD bounds |
| `level-36.png` | Level 36 — 3×3 grid, all bottles visible, no HUD intrusion |
| `level-3x3.png` | Representative 3×3 level — full-grid layout check |
| `completed-bottle-cork.png` | Completed bottle with physical cork stopper visible |

## JSON report

[cork-layout-report.json](../cork-layout-report.json) is generated at runtime by
`Bottle3DView.WriteReport()` and pulled from the device after a screenshots run.

### Required values

```json
{
  "overlapDetected": false,
  "hudIntrusionDetected": false,
  "completedBottleCount": N,
  "corkCount": N
}
```

Constraint: `corkCount == completedBottleCount` (every completed bottle has exactly one cork).

## Cork geometry proof

| Dimension | Value | Spec |
|-----------|-------|------|
| Cork radius | NeckRadius × 1.05 = 0.147 wu | ≈ neck_diameter × 1.05 / 2 ✓ |
| Cork thickness | 2 × NeckRadius × 0.2 = 0.056 wu | neck_diameter × 0.2 ✓ |
| Inside depth | 0.056 × 0.70 = 0.039 wu | 60–80% inside → 70% ✓ |
| Peek height | 0.056 × 0.30 = 0.017 wu | 20–40% outside → 30% ✓ |
