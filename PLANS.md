# PLANS — Bottle Volume Visual Consistency (2026-02-09)

## PHASE 1: Visual model alignment
- [x] Define a single linear height mapping using slot capacity as the only input.
- [x] Remove any per-capacity width scaling to enforce uniform bottle width.
- [x] Ensure liquid height derives from occupied slots and scaled bottle height only.

## PHASE 2: Automated tests
- [x] Add PlayMode tests validating bottle height ratios for 4/6/8/10-slot bottles.
- [x] Add PlayMode tests validating liquid height matches slot counts across capacities.
- [x] Add EditMode tests validating exact-fit pours across mixed capacities.

## PHASE 3: Documentation
- [x] Document the slot-canonical volume model and linear height rules in the developer guide.

## PHASE 4: Verification
- [ ] Run/confirm test coverage for new tests and verify no visual regressions are introduced. (Blocked: Unity executable not found in this environment.)

# PLANS — CI Recovery (2026-02-07)

## PHASE 1: Android signing failure
- [ ] Root cause: Unity release build runs without keystore env vars inside the unity-builder container, so AndroidBuild fails release signing.
	Fix: Pass prepared keystore values into unity-builder inputs and explicit step env, using outputs from the keystore prep step.
	Verify: CI Build Decantra job completes Build Android APK/AAB and logs show signing configured without missing env vars.

- [ ] Root cause: Keystore prep step only writes to GITHUB_ENV, which may not propagate into the container.
	Fix: Emit keystore values as step outputs and consume them directly in unity-builder inputs and step env.
	Verify: `gh run view` for the push shows release build succeeded and APK/AAB artifacts uploaded.

## PHASE 1B: Coverage artifact emission
- [ ] Root cause: EditMode tests do not write coverage to Coverage/**, so artifact upload warns about missing files.
	Fix: Add explicit coverage output path and options to EditMode test runner parameters.
	Verify: CI test job uploads Decantra-Coverage artifacts without path warnings.

## PHASE 2: End-to-end CI confirmation
- [ ] Root cause: CI status not confirmed after changes.
	Fix: Push workflow update and confirm green pipeline via `gh run list` + `gh run view`.
	Verify: Build Decantra workflow shows success for test and build jobs on main.

# PLANS — Coverage Correctness + Deterministic Generator Tests

## PHASE 1: codecov.io Coverage Correctness
- [x] Audit how coverage reports are generated (Unity args, report format, output paths) and captured for Codecov upload.
- [x] Verify Codecov upload path and config, then ensure only production assemblies are included.
- [x] Exclude all test assemblies from coverage (Unity coverage filters + Codecov excludes).
- [x] Validate locally that test assemblies no longer appear in coverage reports.
- [x] Record exact config changes and rationale in this phase.
Notes:
- Unity coverage filters now use assemblyFilters:+Decantra.Domain in CI and tools/test.sh.
- Codecov ignore config added in codecov.yml for Assets/Decantra/Tests/**.

## PHASE 2: Assembly and Boundary Audit
- [x] Inventory all Decantra .asmdef files and map production vs test boundaries.
- [x] Ensure Domain/App assemblies are testable in isolation without UnityEngine references.
- [x] Ensure test assemblies reference only needed production assemblies.
- [x] Introduce seams only where required to enable deterministic tests (document each).
- [x] Record assembly changes and justification in this phase.

## PHASE 3: Deterministic Test Strategy Definition
- [x] Define a repeatable generator test strategy (fixed seeds, bounded domains, invariant checks).
- [x] Define shared test inputs (width/height, parameter presets, fixed seed list, invariant thresholds).
- [x] Shared inputs detail: 48x48 fields, seeds A/B, FieldParameters.Default + FieldParameters.Macro.
- [x] Shared invariants detail: length match, values in [0,1], min/max span >= 0.05, mean in (0.05, 0.95).
- [x] BotanicalIFSGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, min coverage).
- [x] BranchingTreeGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, blur stability).
- [x] CanopyDappleGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, shadow contrast).
- [x] ConcentricRipplesGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, ripple variance).
- [x] CrystallineFrostGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, frost density).
- [x] FloralMandalaGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, petal structure variance).
- [x] FractalEscapeDensityGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, density inversion).
- [x] ImplicitBlobHazeGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, blob coverage).
- [x] MarbledFlowGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, vein variance).
- [x] NebulaGlowGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, glow hotspots).
- [x] OrganicCellsGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, cell edge variance).
- [x] RootNetworkGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, root density).
- [x] VineTendrilsGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, tendril variance).
- [x] Document DeterministicRng invariants and boundary expectations.
- [x] Record the strategy in this phase before writing tests.

## PHASE 4: Generator Test Implementation
- [x] BotanicalIFSGenerator: deterministic output + structural invariants.
- [x] BranchingTreeGenerator: deterministic output + structural invariants.
- [x] CanopyDappleGenerator: deterministic output + structural invariants.
- [x] ConcentricRipplesGenerator: deterministic output + structural invariants.
- [x] CrystallineFrostGenerator: deterministic output + structural invariants.
- [x] FloralMandalaGenerator: deterministic output + structural invariants.
- [x] FractalEscapeDensityGenerator: deterministic output + structural invariants.
- [x] ImplicitBlobHazeGenerator: deterministic output + structural invariants.
- [x] MarbledFlowGenerator: deterministic output + structural invariants.
- [x] NebulaGlowGenerator: deterministic output + structural invariants.
- [x] OrganicCellsGenerator: deterministic output + structural invariants.
- [x] RootNetworkGenerator: deterministic output + structural invariants.
- [x] VineTendrilsGenerator: deterministic output + structural invariants.
Notes:
- Deterministic + invariant coverage tests added in Assets/Decantra/Tests/EditMode/BackgroundGeneratorCoverageTests.cs.

## PHASE 5: Registry and Factory Coverage
- [x] Cover BackgroundGeneratorRegistry registration and lookup.
- [x] Assert all generators are discoverable and instantiable.
- [x] Cover error/edge paths in registry behavior.
Notes:
- Registry coverage tests added in Assets/Decantra/Tests/EditMode/BackgroundGeneratorRegistryCoverageTests.cs.

## PHASE 6: DeterministicRng Deep Coverage
- [x] Cover seed reproducibility for all public random methods.
- [x] Cover boundary conditions (min/max, zero ranges, invalid ranges).
- [x] Add distribution sanity checks with deterministic expectations.
Notes:
- DeterministicRng tests added in Assets/Decantra/Tests/EditMode/DeterministicRngTests.cs.

## PHASE 7: Final Verification and CI
- [x] Run coverage locally and inspect report artifacts for test exclusion.
- [x] Confirm Domain coverage increases and all listed generators show coverage.
- [x] Run one full local build after coverage improvements.
- [x] Push changes once after local build success.
- [x] Verify CI and Codecov are green with corrected coverage.
- [x] Record before/after coverage deltas and any justified gaps here.
Notes:
- CI PlayMode crash traced to coverage state; workflow now runs PlayMode before EditMode coverage.
- Coverage before change was not comparable due to mixed test/prod assemblies; after change Domain line coverage is 91.8% (Coverage/Report/Summary.xml).
