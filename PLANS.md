# PLANS.md - Decantra Background Generation Overhaul

**Status**: IN PROGRESS  
**Last Updated**: 2026-02-03  

## Audit of Previous Background Fix

### Diff-based reality check (completed)
- **BackgroundRules.cs**: No structural changes vs main (zone scheduling already correct, but no pattern families A–F, no enforcement of no-centre-bias invariant beyond a macro placement tweak).
- **Renderer/compositor (SceneBootstrap.cs)**: Previous changes were localized to parameter tweaks and a single fixed overlay; no zone-based pattern family system or deterministic macro/meso generation existed.
- **Conclusion**: Prior work was insufficient (no family coverage, no centre-bias enforcement, no zone-based geometry caching, and per-level structural variation still present).

## Root Cause of Centre Egg

### Confirmation (completed)
- Centre-weighted “egg” artefact is produced by **radial distance-to-centre** formulas in the renderer:
	- `CreateVignetteSprite()` uses `dist = Vector2.Distance(p, center)` to compute a radial falloff.
	- Prior renderer variants also used **soft circle** sprites (`CreateSoftCircleSprite()` / center-blur usage) stretched into non-square rects, amplifying an oval highlight.
- These effects are macro-scale, screen-centred and can be amplified by overlay blending.
- **Root cause**: Macro layers using radial distance-to-centre + non-square scaling -> centre-biased “egg”.

## Design: Pattern Families A–F

All families are implemented in `BackgroundPatternGenerator` (Presentation), driven by zone seed:

- **Mapping strategy**: `BackgroundRules` picks a primary family per zone using the zone seed and excludes the immediately previous family, ensuring adjacent zones don’t repeat. Optional secondary families provide accent overlays.

### Family A: Directional line fields
- Macro underlay + crisp meso overlay, 1–3 line families.
- Deterministic angles, spacing, thickness; optional screen-space sine warp.
- Full-screen, non-radial.

### Family B: Large band gradients
- Multi-band linear gradients with optional tiling/mirroring.
- Full-screen, non-radial; direction seeded per zone.

### Family C: Voronoi macro-regions
- Seed points distributed via jittered grid to avoid centre clustering.
- Low-res Voronoi assignment, feathered edges.

### Family D: Polygonal shards
- Deterministic jittered grid + per-cell triangulation.
- Triangle edge softening; no centre dominance.

### Family E: Wave interference fields
- Directional sine-wave interference in screen space.
- Optional banding for meso overlay.

### Family F: Fractal-lite / bounded noise
- fBm (4–5 octaves) on low-res buffers; hard-capped iteration count.
- Deterministic, non-radial.

## Caching and Performance Strategy

- Zone scheduling: **levels 1–9 => zone 0; 10–19 => zone 1; etc.** (already enforced by `BackgroundRules.GetZoneIndex`).
- ZoneSeed derived only from (globalSeed, zoneIndex).
- Geometry fixed per zone; per-level variation limited to color-only parameters.
- **Caching**: Zone pattern sprites are generated once per zone and cached via `ZonePatternCacheKey` (seed+zone).
- **Low-res generation**: macro/meso 192–256, accent 160–192, micro 128. Voronoi/fractal use smaller buffers.
- **Instrumentation**: `BackgroundPatternGenerator` measures generation time (ms) per zone and logs in dev/editor.

## Validation: No Centre Bias

- **Rule-level bans**:
	- `GeneratorFamily.RadialPolar` removed from selection (new families only).
	- `PlacementRule.Radial` never selected; macro enforcement in `EnforceNoCenterBias`.
	- `FocalRule.CenterBias` excluded (only Grid/Diagonal focal rules).
- **Renderer guardrails**:
	- Macro generation uses screen-space (non-radial) math only.
- **Deterministic centre-bias check**:
	- Implemented in `BackgroundPatternGenerator.ValidateNoCenterBias()`.
	- Samples centre vs edges/corners on low-res field; threshold ratio 1.15.
	- Asserts in editor to fail fast if centre bias is detected.

## Execution Checklist

- [x] Complete audit and root cause documentation.
- [x] Implement pattern families A–F in background generation and selection.
- [x] Implement zone scheduling, determinism, and caching.
- [x] Enforce no-centre-bias invariant in rules + renderer.
- [x] Add performance instrumentation.
- [ ] Confirm <= 4.0s worst-case (pending measurement run).
- [x] Regenerate background screenshots; verify no centre blob and full-screen coverage.
- [x] Run tests; ensure CI green.
- [x] Extend runtime screenshot capture for levels 1/12/24/36 and intro.
- [x] Run ./build --screenshots --generate-solutions.

## Performance Measurements

- [ ] Capture timing on target profile (4-core 2 GHz Android).
- [ ] Document timings and any resolution/iteration reductions.
- Instrumentation added: generation time logged per zone and apply time logged per level in dev/editor builds.

## Diffs and Design Decisions

- **New generator**: `BackgroundPatternGenerator` (Presentation) produces macro/meso/accent/micro sprites per zone.
- **Domain rules**: Updated generator families to A–F; enforced non-radial placement for macro.
- **Renderer**: Zone-based caching and no per-level geometry changes; per-level colors only.
- **Centre bias validation**: Low-res centre vs edges check with editor assertions.
- **Screenshot capture**: runtime capture sequence now includes intro + levels 1/12/24/36 + interstitial.
