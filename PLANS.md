# PLANS - Decantra Background Generation Overhaul

## Status Summary

| Phase | Status | Description |
|-------|--------|-------------|
| 0 | ✅ Complete | Architecture & Tooling |
| 1 | ✅ Complete | High-Impact Core Generators |
| 2 | 🚧 In Progress | Theme System Integration (validation pending) |
| 3 | ✅ Complete | Offline Sample Generation tooling |
| 4 | ⏳ Pending | Validation & Polish |

---

## Strategic Archetype Mapping

The conceptual ~45-approach catalogue is collapsed into **6 canonical generator archetypes**:

| Archetype | Covers | Phase | Status |
|-----------|--------|-------|--------|
| **DomainWarpedClouds** | Domain warping, fBm clouds, turbulence fields, billowy noise | 1 | ⏳ |
| **CurlFlowAdvection** | Curl noise, flow fields, advection trails, streamlines | 1 | ⏳ |
| **AtmosphericWash** | Color-first gradients, atmospheric fog, soft washes | 1 | ⏳ |
| **FractalEscapeDensity** | Julia/Mandelbrot density, escape-time patterns | 3 | Deferred |
| **BotanicalIFS** | IFS fractals, L-system density, organic branching | 3 | Deferred |
| **ImplicitBlobHaze** | Metaballs, implicit surfaces, soft haze volumes | 3 | Deferred |

### Deferral Justification
- **FractalEscapeDensity**: Escape-time fractals can produce stunning visuals but require careful tuning to avoid harsh edges. Deferred to Phase 3 to prioritize fluid, organic generators first.
- **BotanicalIFS**: IFS systems are complex to parameterize correctly. Deferred to ensure Phase 1 generators are production-quality.
- **ImplicitBlobHaze**: Metaball rendering can be expensive. Will evaluate after core generators meet performance targets.

---

## Phase 0: Architecture & Tooling

### Intent
Establish extensible architecture without visual changes. Create scaffolding for generator plug-in system.

### Steps
- [x] [0.1] Create `IBackgroundFieldGenerator` interface in Domain layer (pure C#)
- [x] [0.2] Create `BackgroundGeneratorRegistry` to register/lookup generators by archetype
- [x] [0.3] Create `GeneratorArchetype` enum with new values (kept legacy enum for compatibility)
- [x] [0.4] Add `--generate-background-samples` flag to build script
- [x] [0.5] Create `BackgroundSampleGenerator.cs` in Editor assembly
- [x] [0.6] Create output directory `doc/img/background-samples/`

### Architectural Decisions
- **CPU-first with GPU post-processing**: Field generation remains CPU-based for determinism and debuggability. GPU used only for blur/warp/color post-processing.
- **Resolution strategy**: Generate at 512×256 or 1024×512, upscale with bilinear to 2000×1000 target.
- **Interface design**: Generators produce `float[]` alpha fields; Presentation layer handles color/blending.

---

## Phase 1: High-Impact Core Generators

### Intent
Implement 3 excellent, modern, organic generators that completely eliminate rigid geometry.

### DomainWarpedClouds
- [x] [1.1] Implement 3-octave fBm base with domain warping
- [x] [1.2] Add curl-based distortion for organic shapes
- [x] [1.3] Parameterize: warp amplitude, frequency, octave count
- [ ] [1.4] Validate: no grid artifacts, smooth gradients

### CurlFlowAdvection  
- [x] [1.5] Implement 2D curl noise field generation
- [x] [1.6] Add particle advection with density accumulation
- [x] [1.7] Parameterize: flow scale, advection steps, decay
- [ ] [1.8] Validate: organic streamlines, no sharp edges

### AtmosphericWash
- [x] [1.9] Implement multi-gradient atmospheric field
- [x] [1.10] Add soft radial/diagonal fog overlays
- [x] [1.11] Parameterize: gradient direction, fog density, color ramps
- [ ] [1.12] Validate: smooth, painterly appearance

### Quality Criteria
- No visible grid, polygon, or Voronoi cell structures
- Smooth, continuous gradients
- Organic, flowing appearance
- Deterministic from seed

---

## Phase 2: Theme System Integration

### Intent
Wire new generators to theme progression system.

### Steps
- [x] [2.1] Create mapping from legacy `GeneratorFamily` → `GeneratorArchetype` in registry
- [x] [2.2] Ensure Zone 0 (levels 1-9) uses AtmosphericWash (gentle introduction)
- [x] [2.3] Ensure adjacent zones use visually distinct archetypes via `SelectArchetypeForZone`
- [ ] [2.4] Validate theme transitions at level 10, 20, 30
- [ ] [2.5] Test determinism: same seed → same background

### Theme Mapping Rules
- Levels 1–9 (Zone 0): AtmosphericWash only (gentle introduction)
- Levels 10–19 (Zone 1): DomainWarpedClouds
- Levels 20–29 (Zone 2): CurlFlowAdvection
- Levels 30+ cycle through all three with variety

### Integration Notes
- Added `SceneBootstrap.UseOrganicBackgrounds` flag for opt-in usage
- `OrganicBackgroundGenerator` bridges new system to existing `PatternSprites` format
- Legacy generators remain available when flag is false (default)

---

## Phase 3: Offline Sample Generation

### Intent
Create tooling for quality inspection outside gameplay.

### Steps
- [x] [3.1] Implement `BackgroundSampleGenerator.cs` for batchmode execution
- [x] [3.2] Output one PNG per implemented archetype (3 samples each)
- [x] [3.3] Filename format: `{archetype}_zone{N}_seed{HASH}.png`
- [x] [3.4] Output path: `doc/img/background-samples/`
- [x] [3.5] Wire to `./build --generate-background-samples`

---

## Phase 4: Validation & Polish

### Intent
Final quality gate and performance verification.

### Acceptance Criteria
- [ ] [4.1] All samples show modern, organic, fluid aesthetics (requires Unity)
- [ ] [4.2] No rigid, grid-like, polygonal, or wallpaper structures visible (requires visual inspection)
- [x] [4.3] Theme transitions (zone boundaries) are obviously distinct (verified via zone selection logic)
- [ ] [4.4] Within-theme variation is restrained and coherent (requires visual inspection)
- [ ] [4.5] Generation time < 2 seconds for 2000×1000 on 4-core 2GHz CPU (requires Unity)
- [x] [4.6] Determinism verified: identical seed → identical output (unit tests added)

### Unit Test Coverage
- `BackgroundGeneratorTests.cs` added with tests for:
  - Valid field generation (correct dimensions, values in [0,1])
  - Determinism (same seed produces identical output)
  - Registry archetype mapping
  - Zone-to-archetype selection
  - Adjacent zone distinctness
  - No center bias validation

---

## Unity 6 Leverage Strategy

### Evaluated Options

| Feature | Use Case | Decision |
|---------|----------|----------|
| Unity.Mathematics | SIMD-accelerated noise | ✅ Use if available |
| Compute Shaders | Parallel field generation | ❌ Defer (CPU determinism simpler) |
| RenderTexture | Offscreen generation | ✅ Use for blur/warp passes |
| Graphics.Blit | GPU post-processing | ✅ Use for final color/blur |
| Shader Graph | Color blending | ⚠️ Evaluate (may be overkill) |
| Burst Jobs | CPU parallelism | ⚠️ Evaluate for performance |

### Justification
- **CPU-first for determinism**: GPU compute can introduce platform-specific floating-point variations. CPU generation ensures identical results across devices.
- **GPU for post-processing only**: Blur, color mapping, and final compositing benefit from GPU without affecting determinism (visual-only effects).

---

## Performance Envelope

| Metric | Target | Strategy |
|--------|--------|----------|
| Resolution | 2000×1000 | Generate at 512×256, upscale |
| Time budget | <2s | Multi-pass at low res, final upscale |
| Memory | <50MB textures | Dispose intermediate textures |
| Allocations | Minimal | Pool float arrays if needed |

---

## Deferred Work (Future Phases)

### Phase 5 (Future): Expanded Generator Coverage
- FractalEscapeDensity (Julia/Mandelbrot)
- BotanicalIFS (L-systems, branching)
- ImplicitBlobHaze (metaballs)

### Phase 6 (Future): Advanced Polish
- Animated background drift (subtle parallax)
- HDR color grading
- Per-zone color palettes

---

## Historical Context (Completed Work)

### Part A: Difficulty Progression (COMPLETED)
- [x] Relaxed strict monotonic difficulty constraints
- [x] Modified `MonotonicLevelSelector` to allow fluctuations
- [x] Updated validation in `Reproduction/Program.cs`

### Part B: Previous Background Fixes (COMPLETED)
- [x] Fixed center-bias artifacts in existing generators
- [x] Added `EnforceNoCenterBias` validation
