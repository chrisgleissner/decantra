# PLANS — Coverage Correctness + Deterministic Generator Tests

## PHASE 1: codecov.io Coverage Correctness
- [ ] Audit how coverage reports are generated (Unity args, report format, output paths) and captured for Codecov upload.
- [ ] Verify Codecov upload path and config, then ensure only production assemblies are included.
- [ ] Exclude all test assemblies from coverage (Unity coverage filters + Codecov excludes).
- [ ] Validate locally that test assemblies no longer appear in coverage reports.
- [ ] Record exact config changes and rationale in this phase.
Notes:
- Unity coverage filters now include -Decantra.Domain.Tests and -Decantra.PlayMode.Tests in CI and tools/test.sh.
- Codecov ignore config added in codecov.yml for Assets/Decantra/Tests/**.

## PHASE 2: Assembly and Boundary Audit
- [ ] Inventory all Decantra .asmdef files and map production vs test boundaries.
- [ ] Ensure Domain/App assemblies are testable in isolation without UnityEngine references.
- [ ] Ensure test assemblies reference only needed production assemblies.
- [ ] Introduce seams only where required to enable deterministic tests (document each).
- [ ] Record assembly changes and justification in this phase.

## PHASE 3: Deterministic Test Strategy Definition
- [ ] Define a repeatable generator test strategy (fixed seeds, bounded domains, invariant checks).
- [ ] Define shared test inputs (width/height, parameter presets, fixed seed list, invariant thresholds).
- [ ] Shared inputs detail: 48x48 fields, seeds A/B, FieldParameters.Default + FieldParameters.Macro.
- [ ] Shared invariants detail: length match, values in [0,1], min/max span >= 0.05, mean in (0.05, 0.95).
- [ ] BotanicalIFSGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, min coverage).
- [ ] BranchingTreeGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, blur stability).
- [ ] CanopyDappleGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, shadow contrast).
- [ ] ConcentricRipplesGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, ripple variance).
- [ ] CrystallineFrostGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, frost density).
- [ ] FloralMandalaGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, petal structure variance).
- [ ] FractalEscapeDensityGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, density inversion).
- [ ] ImplicitBlobHazeGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, blob coverage).
- [ ] MarbledFlowGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, vein variance).
- [ ] NebulaGlowGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, glow hotspots).
- [ ] OrganicCellsGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, cell edge variance).
- [ ] RootNetworkGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, root density).
- [ ] VineTendrilsGenerator: inputs, determinism guarantee, invariants (range, non-uniformity, tendril variance).
- [ ] Document DeterministicRng invariants and boundary expectations.
- [ ] Record the strategy in this phase before writing tests.

## PHASE 4: Generator Test Implementation
- [ ] BotanicalIFSGenerator: deterministic output + structural invariants.
- [ ] BranchingTreeGenerator: deterministic output + structural invariants.
- [ ] CanopyDappleGenerator: deterministic output + structural invariants.
- [ ] ConcentricRipplesGenerator: deterministic output + structural invariants.
- [ ] CrystallineFrostGenerator: deterministic output + structural invariants.
- [ ] FloralMandalaGenerator: deterministic output + structural invariants.
- [ ] FractalEscapeDensityGenerator: deterministic output + structural invariants.
- [ ] ImplicitBlobHazeGenerator: deterministic output + structural invariants.
- [ ] MarbledFlowGenerator: deterministic output + structural invariants.
- [ ] NebulaGlowGenerator: deterministic output + structural invariants.
- [ ] OrganicCellsGenerator: deterministic output + structural invariants.
- [ ] RootNetworkGenerator: deterministic output + structural invariants.
- [ ] VineTendrilsGenerator: deterministic output + structural invariants.
Notes:
- Deterministic + invariant coverage tests added in Assets/Decantra/Tests/EditMode/BackgroundGeneratorCoverageTests.cs.

## PHASE 5: Registry and Factory Coverage
- [ ] Cover BackgroundGeneratorRegistry registration and lookup.
- [ ] Assert all generators are discoverable and instantiable.
- [ ] Cover error/edge paths in registry behavior.
Notes:
- Registry coverage tests added in Assets/Decantra/Tests/EditMode/BackgroundGeneratorRegistryCoverageTests.cs.

## PHASE 6: DeterministicRng Deep Coverage
- [ ] Cover seed reproducibility for all public random methods.
- [ ] Cover boundary conditions (min/max, zero ranges, invalid ranges).
- [ ] Add distribution sanity checks with deterministic expectations.
Notes:
- DeterministicRng tests added in Assets/Decantra/Tests/EditMode/DeterministicRngTests.cs.

## PHASE 7: Final Verification and CI
- [ ] Run coverage locally and inspect report artifacts for test exclusion.
- [ ] Confirm Domain coverage increases and all listed generators show coverage.
- [ ] Run one full local build after coverage improvements.
- [ ] Push changes once after local build success.
- [ ] Verify CI and Codecov are green with corrected coverage.
- [ ] Record before/after coverage deltas and any justified gaps here.
