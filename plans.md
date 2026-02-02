# Release engineering plan (Decantra)

## CI trigger investigation
- [x] Inventory existing GitHub Actions workflows and triggers
- [x] Compare release/tag triggers with C64Commander
- [x] Identify why Release UI tag does not trigger Decantra
- [x] Add/adjust tag and release triggers (v*)
- [x] Verify tag-based run triggers CI

## Tag-trigger fixes
- [x] Implement workflow trigger fixes
- [x] Validate semantic version tags (v*) are matched
- [x] Document trigger behavior in this plan

Notes:
- Release UI can create tags without a `push` event; adding `create` tag triggers ensures tag creation runs CI even when no push occurs.
- `release` event remains for published releases, while `push` and `create` catch tag-only cases.
	- Release-triggered run verified: Run ID 21561419201 (event: release, tag: v0.0.0-ci.20260201-2) completed successfully.

## APK generation
- [x] Ensure debug APK built in CI
- [x] Ensure release APK built in CI
- [x] Validate signing/variant configuration

## AAB generation
- [x] Ensure bundleRelease is executed
- [x] Validate AAB exists and is non-empty
- [x] Ensure AAB corresponds to release variant
- [x] Ensure missing secrets do not fail build prematurely

## Release asset upload
- [x] Upload debug APK to GitHub Release assets
- [x] Upload release APK to GitHub Release assets
- [x] Upload release AAB to GitHub Release assets
- [x] Ensure names are explicit and unambiguous

## CI polling and verification
- [x] Trigger tag-based build (real/simulated)
- [x] Poll run to completion via gh or GitHub API
- [x] Verify all three artifacts exist
- [x] Confirm AAB generation step ran successfully

## Hardening and non-failure guarantees
- [x] Ensure missing Play Console config does not fail CI
- [x] Ensure AAB creation failures fail CI
- [x] Ensure Play upload failures are non-fatal and logged
- [x] Document hardening behaviors here

Notes:
- Play upload runs only when `PLAY_SERVICE_ACCOUNT_JSON` is present, and is marked non-fatal.
- AAB creation is verified with a non-empty file check and will fail the job if missing.
- Keystore handling is optional: workflow decodes `KEYSTORE_STORE_FILE` (path or base64) and Unity applies signing only when the file + passwords are present.
- Tag-based CI verification:
	- Run ID: 21560960875 (event: push, tag: v0.0.0-ci.20260201) completed successfully.
	- Artifacts: Decantra-Android-Release (APK), Decantra-Android-Debug (APK), Decantra-Android-Release-AAB (AAB).
	- Release assets uploaded for tag v0.0.0-ci.20260201 (debug APK, release APK, release AAB).
	- Release event verification:
		- Run ID: 21561419201 (event: release, tag: v0.0.0-ci.20260201-2) completed successfully.
		- Artifacts: Decantra-Android-Release (APK), Decantra-Android-Debug (APK), Decantra-Android-Release-AAB (AAB).
		- Release assets uploaded for tag v0.0.0-ci.20260201-2 (debug APK, release APK, release AAB).

## Play Store Screenshot Capture (Phone)
- [x] Capture launch/loading screen (screenshot-01-launch.png)
- [x] Capture initial level gameplay (screenshot-02-initial-level.png)
- [x] Capture interstitial with bonus/score increase (screenshot-03-interstitial.png)
- [x] Capture advanced level showing full features (screenshot-04-advanced-level.png)

## Bottle Visual System
- [x] Implement layered bottle composite (glass back/front, mask, liquid, highlight, rim/base)
- [x] Add liquid gradient + curved surface rendering
- [x] Remove rectangular reflections in favor of curved highlight
- [x] Verify bottle visuals in runtime

## Sink-Only Bottle Anchoring
- [x] Add anchor collar and anchor shadow visuals
- [x] Add resistance feedback on interaction attempts
- [x] Confirm sink bottles never lift

## Background Theme Families
- [x] Implement deterministic theme family grouping (every 10 levels)
- [x] Add family parameter ranges with per-level variation
- [x] Add crossfade transition between theme families
- [x] Validate deterministic rendering across seeds

## Feature Graphic Overlay
- [x] Implement hero-scale start overlay with shared dimmer
- [x] Dismiss overlay on first interaction
- [x] Confirm overlay behaves correctly in runtime

---

# Production Polishing Pass (2026-02-01)

## Task 1: BEST/MAX Panels Match LEVEL/MOVES/SCORE Styling
- [x] Scope: Apply identical dark glass treatment to BEST and MAX HUD panels as LEVEL/MOVES/SCORE
- [x] Files: Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs
- [x] Risks: Low - UI-only change, does not affect game logic
- [x] Validation: Visual inspection in screenshots, run test suite

## Task 2: Vivid Liquid Rendering - Remove Washed-Out Glass Overlays
- [x] Scope: Ensure liquids are fully saturated, reduce/remove white overlays on glass
- [x] Files: Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs
- [x] Risks: Medium - Changes to composite rendering may affect visual appearance
- [x] Validation: Screenshots show vibrant colors, no milky haze

## Task 3: Bottle Neck Reads as 3D Opening
- [x] Scope: Make bottle neck longer and cylindrical with rim shading
- [x] Files: Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs
- [x] Risks: Medium - Hitbox alignment verified - no change to hitbox
- [x] Validation: Visual inspection confirms clear bottle opening

## Task 4: Correct App Icon in Emulator
- [x] Scope: Verify launcher/adaptive icons use doc/play-store-assets/icons/app-icon-512x512.png
- [x] Files: ProjectSettings/ProjectSettings.asset, Assets/Decantra/Branding/
- [x] Risks: Low - Verified icons already match
- [x] Validation: Icons already correct per ProjectSettings GUIDs

## Task 5: Stronger Interstitial Dimming (~80% Black Scrim)
- [x] Scope: Increase dimmer alpha from 0.4 to 0.8 on interstitials
- [x] Files: Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs
- [x] Risks: Low - Simple alpha value change
- [x] Validation: LevelCompleteBanner and OutOfMovesBanner dimmer alpha set to 0.8f

## Task 6: Background System - 100+ Zone Themes, Deterministic, Non-Repeating
- [x] 6.1: Added ZoneTheme struct with comprehensive parameters
- [x] 6.2: Implemented deterministic languageId = (levelIndex-1) / 10 mapping
- [x] 6.3: Each language defines distinct motif/palette/layer recipes (16 motif families)
- [x] 6.4: No exact background repeats via unique seed per level
- [x] 6.5: Added BackgroundSignature function for collision detection
- [x] Files: Assets/Decantra/Domain/Rules/BackgroundRules.cs
- [x] Risks: High - Core system change, determinism verified by tests
- [x] Validation: 15 comprehensive tests pass for determinism, grouping, no collisions

## Task 7: Multi-Layer Parallax Backgrounds with Macro/Micro Structures
- [x] Scope: Ensure 3+ depth bands, macro shapes + micro particles, controlled contrast
- [x] Files: Assets/Decantra/Domain/Rules/BackgroundRules.cs (LayerCount 3-5)
- [x] Risks: Medium - Validated by Zone Theme layer-count parameter
- [x] Validation: ZoneTheme_LayerCountWithinRange test passes

## Task 8: Instant Level Transitions via Precomputation
- [x] 8.1: Verified precompute system already caches next level (StartPrecomputeNextLevel)
- [x] 8.2: Background generation uses cached palette index
- [x] 8.3: Existing system handles transitions off UI thread
- [x] Files: Assets/Decantra/Presentation/Controller/GameController.cs
- [x] Risks: Low - System already implemented and working
- [x] Validation: Level transitions use precomputed state

## Task 9: Add/Update Tests for Background Determinism
- [x] 9.1: Test determinism per levelIndex (ZoneTheme_IsDeterministicForSameZoneAndSeed)
- [x] 9.2: Test language grouping every 10 levels (GetLanguageId_ConsecutiveLevelsGroupCorrectly)
- [x] 9.3: Test no language repetition for first 1000 levels (NoLanguageRepetitionForFirst1000Levels)
- [x] 9.4: Test no background signature collisions for 2000+ levels (GetBackgroundSignature_NoDuplicatesForFirst2000Levels)
- [x] Files: Assets/Decantra/Tests/EditMode/BackgroundRulesTests.cs
- [x] Risks: Low - Test additions only
- [x] Validation: All 15 tests pass

## Task 10: Regenerate Screenshots
- [x] Scope: Run ./build --screenshots to capture updated visuals
- [x] Files: doc/play-store-assets/screenshots/
- [x] Risks: Low - Capture only
- [x] Validation: Screenshots reflect all visual fixes

## Completion Criteria
- [x] All tasks 1-10 marked complete
- [x] All tests pass (./tools/test.sh)
- [x] Screenshots regenerated and committed
- [x] Emulator icon verified correct

---

# Level Generation Decision-Density Overhaul (2026-02-01)

Reference: doc/level-generation-research.md (binding specification)

## Objective
Eliminate linear "undo-the-scramble" gameplay by implementing research-backed techniques to increase decision density, branching, and strategic risk while preserving solvability, determinism, and performance guarantees.

## Phase 1: Core Metrics Infrastructure

### Step 1.1: Add LevelMetrics Data Model
- [x] Create LevelMetrics class in Domain/Generation with fields:
  - ForcedMoveRatio (float): fraction of optimal path states with exactly one legal move
  - AverageBranchingFactor (float): mean legal moves along optimal path
  - DecisionDepth (int): steps until first state with >=2 legal moves
  - EmptyBottleUsageRatio (float): fraction of optimal moves that pour into empty bottles
  - TrapScore (float): fraction of non-optimal moves that lead to harder/unsolvable states
  - SolutionMultiplicity (int): estimated count of optimal/near-optimal solutions
- Files: Assets/Decantra/Domain/Generation/LevelMetrics.cs (new)
- Validation: Unit test for data model instantiation

### Step 1.2: Extend BfsSolver for Metrics Collection
- [x] Add SolveWithMetrics method that returns SolverResultWithMetrics
- [x] Track along optimal path: legal move counts per state, empty-bottle pour count
- [x] Compute forced-move ratio, branching factor, decision depth, empty-bottle usage
- Files: Assets/Decantra/Domain/Solver/BfsSolver.cs, MetricsComputer.cs
- Validation: Unit tests for metrics computation on known states

### Step 1.3: Implement Solution Multiplicity Estimation (Requirement B)
- [x] Add CountOptimalSolutions method with cap (max 3)
- [x] Alternative: Implement divergence test - check if N distinct prefixes of length K solve within +1/+2 moves
- [x] Return multiplicity count in SolverResultWithMetrics
- Files: Assets/Decantra/Domain/Solver/MetricsComputer.cs
- Validation: Unit test with known multi-solution puzzles

## Phase 2: Trap Scoring and Dead-End Risk (Requirement C)

### Step 2.1: Implement Trap Score Computation
- [x] Add ComputeTrapScore method to MetricsComputer class
- [x] From initial state, sample M non-optimal legal moves (M=10-20)
- [x] For each, attempt solve with tight node budget (1000-5000 nodes)
- [x] TrapScore = fraction that are harder (longer) or unsolved within budget
- Files: Assets/Decantra/Domain/Solver/MetricsComputer.cs
- Validation: Unit test verifying trap score increases for states with dead-ends

### Step 2.2: Integrate Trap Score into Metrics
- [x] Call trap scorer during level validation
- [x] Store trap score in LevelMetrics
- Files: Assets/Decantra/Domain/Generation/LevelGenerator.cs
- Validation: Integration test showing trap score populated

## Phase 3: Acceptance Gates (Requirements A, B, C, G)

### Step 3.1: Define Difficulty Band Thresholds
- [x] Add QualityThresholds class with per-band configuration:
  - MaxForcedMoveRatio: 0.60 (Band A-B), 0.50 (Band C+)
  - MaxDecisionDepth: 3 (Band A), 2 (Band B+)
  - MinBranchingFactor: 1.3 (Band A-B), 1.5 (Band C+)
  - MinTrapScore: 0.10 (Band A-B), 0.20 (Band C+)
  - MinSolutionMultiplicity: 1 (Band A), 2 (Band B+)
- Files: Assets/Decantra/Domain/Generation/QualityThresholds.cs (new)
- Validation: Threshold retrieval tests per band

### Step 3.2: Implement Quality Gate in LevelGenerator
- [x] After solving, compute full LevelMetrics
- [x] Check against QualityThresholds for current band
- [x] Reject levels that fail any threshold
- [x] Log rejection reasons for tuning
- Files: Assets/Decantra/Domain/Generation/LevelGenerator.cs
- Validation: Test that levels failing thresholds are rejected

### Step 3.3: Wire IsStructurallyComplex into Acceptance Gate (Requirement G)
- [x] Call existing IsStructurallyComplex in acceptance checks
- [x] Extend to enforce: min mixed bottles, min distinct signatures, min top-position color variety
- Files: Assets/Decantra/Domain/Generation/LevelGenerator.cs
- Validation: Test that structurally simple levels are rejected

## Phase 4: Empty-Bottle Chain Suppression (Requirement D)

### Step 4.1: Tighten Empty Bottle Count Rules
- [x] Mid-band (levels 7-17): enforce 1 empty bottle strictly (already in place)
- [x] When 2 empties exist (levels 1-6, 18+): enforce capacity asymmetry (3 vs 4) - already in place
- [x] Add check for "chain risk": reject if multiple empties are immediately fillable by many sources
- Files: Assets/Decantra/Domain/Rules/LevelDifficultyEngine.cs, LevelGenerator.cs
- Validation: Test empty bottle constraints are enforced

### Step 4.2: Add Empty-Bottle Chain Risk Detector
- [x] Implement method to detect mechanical chain risk
- [x] Count how many sources can pour into each empty bottle
- [x] Reject if sum exceeds threshold (e.g., >4 immediate fill options across empties)
- Files: Assets/Decantra/Domain/Generation/LevelGenerator.cs
- Validation: Test detecting and rejecting chain-risk states

## Phase 5: Objective-Guided Scrambling (Requirement E)

### Step 5.1: Implement Difficulty Objective Function
- [x] Create DifficultyObjective class combining metrics:
  - Score = w1*(1-ForcedMoveRatio) + w2*BranchingFactor + w3*TrapScore + w4*(1/DecisionDepth) + w5*Multiplicity
  - Weights tunable per band
- Files: Assets/Decantra/Domain/Generation/DifficultyObjective.cs (new)
- Validation: Unit tests for objective scoring

### Step 5.2: Implement Hill-Climb Scramble Selection
- [x] Generate N candidate scrambles (N=3-5) per attempt
- [x] Score each with difficulty objective
- [x] Keep best-scoring scramble
- [x] Fallback to any valid scramble if all fail quality gates
- Files: Assets/Decantra/Domain/Generation/LevelGenerator.cs
- Validation: Test that hill-climb improves average objective score

### Step 5.3: (Optional) MCTS-Guided Scramble Search
- [ ] If hill-climb insufficient: implement lightweight MCTS in reverse-move space
- [ ] Score intermediate states by difficulty objective
- [ ] Budget: max 100 playouts per level generation
- Files: Assets/Decantra/Domain/Generation/MctsScrambler.cs (new, if needed)
- Validation: Performance test ensuring <50ms additional latency
- Note: Deferred - hill-climb provides sufficient improvement

## Phase 6: Telemetry Architecture (Requirement F)

### Step 6.1: Add Generation Stats Logging
- [x] Instrument LevelGenerator to log/expose:
  - Final LevelMetrics for each generated level
  - Rejection counts and reasons
  - Generation attempt count
- Files: Assets/Decantra/Domain/Generation/LevelGenerator.cs
- Validation: Log output verification

### Step 6.2: Define Telemetry-Ready Data Structure
- [x] Create LevelGenerationReport struct with all metrics
- [x] Include fields for future player telemetry correlation
- [x] Expose via public property on generator
- Files: Assets/Decantra/Domain/Generation/LevelGenerationReport.cs (new)
- Validation: Data structure instantiation test

## Phase 7: Performance Safeguards

### Step 7.1: Budget Metrics Computation
- [x] Cap trap score sampling to M=15 moves, budget=2000 nodes each
- [x] Cap solution multiplicity search to 3 solutions
- [x] Total metrics overhead target: <30ms per level
- Files: All solver/metrics code
- Validation: Performance test with timing assertions

### Step 7.2: Ensure Precompute Pipeline Compatibility
- [x] Verify all new code runs off-main-thread safely
- [x] No Unity API calls in metrics/solver code
- [x] Test precompute with new validation gates
- Files: Assets/Decantra/Presentation/Controller/GameController.cs
- Validation: Async generation test, no main-thread blocking

### Step 7.3: First-Level Generation Performance
- [x] Ensure level 1 generates in <100ms total
- [x] Relaxed mode ensures fast fallback if quality gates too strict
- Files: Assets/Decantra/Domain/Generation/LevelGenerator.cs
- Validation: Timing test for level 1 generation

## Phase 8: Testing and Validation

### Step 8.1: Unit Tests for Metrics
- [x] Test ForcedMoveRatio computation
- [x] Test BranchingFactor computation
- [x] Test DecisionDepth computation
- [x] Test EmptyBottleUsageRatio computation
- [x] Test TrapScore computation
- [x] Test SolutionMultiplicity counting
- Files: Assets/Decantra/Tests/EditMode/LevelMetricsTests.cs (new)

### Step 8.2: Integration Tests for Quality Gates
- [x] Test levels passing all thresholds are accepted
- [x] Test levels failing each threshold individually are rejected
- [x] Test structural complexity gate
- Files: Assets/Decantra/Tests/EditMode/QualityGateTests.cs (new)

### Step 8.3: Regression Tests
- [x] Verify all existing tests pass
- [x] Verify determinism preserved (same seed = same level)
- [x] Verify solvability preserved (no unsolvable levels)
- [x] Verify performance targets met
- Files: Existing test files + new validation tests

### Step 8.4: Fuzz Testing for Solvability
- [x] Run 100+ levels through generator and solver
- [x] Verify all are solvable
- [x] Verify metrics are within expected ranges
- Files: Assets/Decantra/Tests/EditMode/GenerationSolvabilityTests.cs

## Exit Criteria (All Must Be True)
- [x] Requirement A (Decision-Density Metrics): Implemented and gated
- [x] Requirement B (Solution Multiplicity): Implemented and gated
- [x] Requirement C (Trap Potential): Implemented and gated
- [x] Requirement D (Empty-Bottle Suppression): Implemented and enforced
- [x] Requirement E (Objective-Guided Scrambling): Implemented
- [x] Requirement F (Telemetry Architecture): Data structures in place
- [x] Requirement G (Structural Complexity): Wired into acceptance gate
- [x] All tests pass (./tools/test.sh) - CI verified (run 21572520861)
- [x] Level transitions remain instant (no perceptible delay) - precompute unchanged
- [x] First level appears with no perceptible delay - relaxed mode ensures fast fallback
- [x] Determinism preserved (verified by GenerationSolvabilityTests)

---

# Visual Polish + Release Finish (2026-02-01)

- [x] Brighten palette while preserving saturation (HSV V lift)
- [x] Add ReflectionStrip overlay to bottle glass (pos/size per spec)
- [x] Update Android app icon to app-icon-512x512.png
- [x] Regenerate screenshots
- [x] CI green (verified via gh)

CI run verification:
- Build Decantra run 21569871324 (success)

---

# Procedural Background System Implementation Plan (2026-02-01)

## Step 1: Align terminology and mapping
- [x] Replace deprecated naming with Zone Theme in domain logic
- [x] Ensure Zone indexing rules match spec (Levels 1–9 → Zone 0, then 10-level Zones)

## Step 2: Define canonical data models
- [x] Add Zone Theme data model (geometry vocabulary, generator family, symmetry, layer stack, depth, motion, fingerprint)
- [x] Add LayerSpec data model (role, scale band, generator variant, compositing, crispness)
- [x] Add Level Variant data model (palette/gradients + minor modulation only)

## Step 3: Implement Zone Theme generation
- [x] Implement zoneSeed = hash(globalSeed, zoneIndex)
- [x] Implement weighted geometry vocabulary selection excluding adjacent Zones
- [x] Implement primary generator family selection different from previous Zone
- [x] Implement symmetry selection compatible with generator
- [x] Implement progression-aware layerCount distribution
- [x] Implement scale-band allocation with richness constraints
- [x] Implement per-layer parameter generation and deterministic depth ordering
- [x] Implement optional motion assignment (≤ 2 layers, cyclic only)

## Step 4: Implement Level Variant generation
- [x] Implement levelSeed = hash(globalSeed, levelIndex)
- [x] Implement palette/gradient derivation from levelSeed only
- [x] Implement minor phase/density/amplitude/jitter modulation only

## Step 5: Fingerprint and anti-repetition gate
- [x] Implement Zone Theme fingerprint
- [x] Implement similarity checks vs previous N Zones (N ≥ 3)
- [x] Implement regeneration on threshold exceed

## Step 6: Rendering integration
- [x] Implement layer compositing rules and opacity/contrast falloffs
- [x] Implement deterministic grayscale recognisability validation
- [x] Implement optional parallax and motion for designated layers

## Step 7: Async precompute pipeline
- [x] Precompute Zone Theme and Level Variant for next Level off main thread
- [x] Ensure swap-only on Level switch (no blocking work)
- [x] Ensure no dynamic allocation in render loop

## Step 8: Tests and validation
- [x] Add tests for Zone indexing, determinism, and caching
- [x] Add tests for layer count and scale-band constraints
- [x] Add tests for fingerprint uniqueness
- [x] Add performance guardrails for generation time budget

## Step 9: Documentation and review
- [x] Link spec in docs and confirm implementation matches all invariants
- [x] Review against grayscale recognisability and performance constraints
