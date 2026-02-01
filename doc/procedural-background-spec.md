# Procedural Background Generation Specification

## 1. System overview

- Deterministic, multi-layer, multi-scale procedural background per Level.
- Zone Theme defines structure only; Level Variant defines colour and minor modulation only.
- Outputs are pure functions of `globalSeed`, `zoneIndex`, and `levelIndex`.
- Zone Theme cached per Zone; Level Variant computed per Level.

## 2. Formal Zone indexing and seeding model

- Zone indexing:
  - Levels 1–9 → `zoneIndex = 0`.
  - Levels ≥10 → `zoneIndex = floor(levelIndex / 10)`.
- Zone ranges:
  - Zone 1: Levels 10–19
  - Zone 2: Levels 20–29
  - Zone N: Levels (N*10)–(N*10 + 9)
- Seeds:
  - `zoneSeed = hash64(globalSeed, zoneIndex)`
  - `levelSeed = hash64(globalSeed, levelIndex)`
  - All randomness derives only from deterministic PRNGs seeded with `zoneSeed` or `levelSeed`.

## 3. Canonical data models

### 3.1 Zone Theme

- **Identity**
  - `zoneIndex: int`
  - `zoneSeed: uint64`
- **Structure**
  - `geometryVocabulary: enum` (exactly one)
  - `primaryGeneratorFamily: enum` (exactly one)
  - `secondaryGeneratorFamily: enum | None` (optional, compatible, non-dominant)
  - `symmetryClass: enum {none, axial, radial, grid}`
  - `densityProfile: enum {sparse, medium, dense}`
  - `focalRule: struct` (e.g., center bias, ring focus, grid intersection focus)
- **Layering**
  - `layerCount: int` in [4..20]
  - `scaleBandCounts: (macroCount, mesoCount, microCount)`
  - `layers: LayerSpec[]` ordered by `depthIndex` ascending
- **Depth model**
  - `depthOrderingRule: enum | function`
  - `contrastFalloff: function(depthIndex) -> float`
  - `opacityFalloff: function(depthIndex) -> float`
  - `parallaxModel: struct | None` (scale shift, phase offset, drift)
- **Motion model (optional)**
  - `animatedLayerIds: int[]` length ≤ 2
  - `motionParams: MotionSpec` for each animated layer
- **Fingerprint**
  - `geometryVocabulary`
  - `primaryGeneratorFamily`
  - `symmetryClass`
  - `layerCount`
  - `motionPresence: bool`
  - `scaleBandDistributionSignature: (macroCount, mesoCount, microCount)`
  - `compositingSignature: set<BlendFamily>`

### 3.2 LayerSpec

- `id: int`
- `role: enum {base, macro, meso, micro, accent, atmosphere}`
- `scaleBand: enum {macro, meso, micro}`
- `generatorVariant: enum | id`
- `params:`
  - `frequencyOrScale: float`
  - `density: float`
  - `edgeSoftness: enum {soft, medium, crisp}`
  - `shapeSizeDistribution: struct`
  - `placementRule: enum`
- `compositing:`
  - `blendMode: enum {normal, add, multiply, screen, overlay, softLight}`
  - `opacity: float [0..1]`
- `depth:`
  - `depthIndex: int`
  - `contrastMultiplier: float`
- `crispnessModel:`
  - `edgeSoftness: enum {soft, medium, crisp}`
  - `blurRadiusOrFeather: float`

### 3.3 Level Variant

- **Identity**
  - `levelIndex: int`
  - `levelSeed: uint64`
  - `zoneIndex: int`
- **Colour**
  - `palette: GradientSet | DiscretePalette`
  - `hueShift: float`
  - `saturationEnvelope: struct`
  - `valueEnvelope: struct`
  - `gradientDirection: float`
  - `gradientIntensity: float`
  - `accentStrength: float`
- **Structural modulation (allowed only)**
  - `phaseOffset: float | vec2`
  - `minorAmplitudeMod: float`
  - `minorDensityMod: float`
  - `smallPositionalJitter: float`
- **Forbidden**
  - Generator changes, vocabulary changes, layer count changes, symmetry changes, topology changes.

## 4. Explicit generation and async execution algorithm

1. **Zone indexing and seed**
   - Compute `zoneIndex` from `levelIndex`.
   - Compute `zoneSeed`.

2. **Zone Theme generation (once per Zone)**
   - Select `geometryVocabulary` (weighted), excluding adjacent Zones.
   - Select `primaryGeneratorFamily` different from previous Zone.
   - Select compatible `symmetryClass`.
   - Select `layerCount` in [4..20] with progression-aware distribution.
     - Zone 0 biased to 4–7.
     - Later Zones bias to 10–20.
   - Allocate scale bands with richness constraints:
     - If `layerCount >= 8`:
       - Macro 2–6, Meso 2–10, Micro 1–8
       - At least 1 gradient-only layer
       - At least 2 soft layers (macro)
       - At least 2 crisp layers (micro or high-frequency meso)
     - If `layerCount in [4..7]`:
       - At least 1 macro, 1 meso, 1 micro/crisp, 1 gradient-only/base
   - Build `LayerSpec[]`:
     - Choose generator variant compatible with primary family and vocabulary.
     - Assign deterministic parameters from `zoneSeed`.
     - Assign compositing rule, opacity, depth index, contrast falloff, crispness.
   - Optionally assign motion to ≤2 layers:
     - Only sine drift, slow rotation, or phase oscillation.
     - Fixed amplitudes and periods derived from `zoneSeed`.
   - Compute fingerprint and compare to previous N Zones (N ≥ 3).
     - Regenerate if similarity threshold exceeded.
   - Cache Zone Theme.

3. **Level Variant generation (per Level, lightweight)**
   - Compute `levelSeed`.
   - Derive palette and gradients from `levelSeed` only.
   - Apply bounded phase offsets and minor amplitude/density/jitter.

4. **Async execution**
   - While Level k is active:
     - Generate Zone Theme for Level k+1 if needed (worker thread).
     - Generate Level Variant for Level k+1 (worker thread).
     - Precompute render buffers/textures for Level k+1 (worker thread).
   - On level switch:
     - Swap references only; no heavy computation.

5. **Rendering**
   - Render layers in deterministic depth order.
   - Apply compositing and contrast/opacity falloffs.
   - Apply motion only on designated layers using analytic functions.

## 5. Invariants and validation checklist

### 5.1 Layer-count and scale-band checks

- `layerCount` in [4..20].
- Scale-band counts meet constraints based on `layerCount`.
- At least 1 gradient-only/base layer.
- If `layerCount >= 8`, at least 2 soft layers and 2 crisp layers.

### 5.2 Grayscale recognisability checks

- Luminance-only render remains structurally recognisable.
- Reject if grayscale edge/shape visibility below threshold.

### 5.3 Uniqueness fingerprint checks

- Compare fingerprint to previous N Zones (N ≥ 3).
- Fail if ≥4/7 fingerprint dimensions match, or if generator family, symmetry, layer count, and scale-band distribution match together.

### 5.4 Time-budget checks

- Zone Theme generation < 200 ms on 4-core 2 GHz class device.
- Level Variant + precompute < 50 ms target; must complete within 6 s budget.
- No blocking work on Level switch.

### 5.5 Determinism checks

- All random choices derived only from `zoneSeed` or `levelSeed`.
- No per-frame random calls; motion is purely time-based.

### 5.6 Performance checks

- All generators O(n) over screen space.
- Max recursion depth ≤ 3.
- No dynamic allocation in render loop.

### 5.7 Zone 0 special rules

- `layerCount` biased to 4–7.
- Avoid fractal-lite and dense micro-detail.
- Must still satisfy all determinism and richness constraints.
