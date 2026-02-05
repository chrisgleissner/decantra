# PLANS - Background Modernization & Verification

## Execution Plan
- [x] 1. Capture baseline screenshot backup and checksum.
- [x] 2. Record baseline path + checksum in this plan.
- [x] 3. Identify legacy background generators and selection logic (current branch).
- [x] 4. Identify historical main (≈1 week ago) and diff background logic.
- [x] 5. Document legacy vs modern generators + reachability conditions.
- [x] 6. Eliminate legacy generators from gameplay paths (level 1+).
- [x] 7. Ensure level 1 uses domain-warped clouds by default.
- [x] 8. Build project using existing build tool.
- [x] 9. Regenerate screenshots (levels 1, 12, 24).
- [x] 10. Run automated image comparison vs baseline.
- [x] 11. Run edge-based validation on regenerated images.
- [x] 12. Iterate until all checks pass.

## Baseline
- Baseline screenshot: doc/play-store-assets/screenshots/phone/screenshot-03-level-01.png
- Backup copy: doc/play-store-assets/screenshots/phone/_baseline/screenshot-03-level-01-baseline-2026-02-05.png
- Checksum (sha256): 84cb04378d92f2132b775770ecbdd752d7d2982864cc332586c558ac70cebedf
- Additional baselines (from git for comparison):
	- doc/play-store-assets/screenshots/phone/_baseline/screenshot-04-level-12-baseline-2026-02-05.png
		- sha256: 7491fc973131b1a29a8f73a883e198202a6f61c9a03dceee6da3b9fb11a75a64
	- doc/play-store-assets/screenshots/phone/_baseline/screenshot-05-level-24-baseline-2026-02-05.png
		- sha256: ff28cd2cddc5e57cbf6d30e32ad642a8d91349101f53d5834f30b5f94abc9634

## Background Inventory
- Legacy generators:
	- AtmosphericWash (linear gradient-based wash)
	- OrganicCells (Voronoi-like cellular structures)
- Modern generators (cloud-like/organic):
	- DomainWarpedClouds, CurlFlowAdvection, NebulaGlow, MarbledFlow, ConcentricRipples, ImplicitBlobHaze, BotanicalIFS, BranchingTree, RootNetwork, VineTendrils, CanopyDapple, FloralMandala, CrystallineFrost, FractalEscapeDensity
- Legacy reachability conditions (pre-fix):
	- AllowedArchetypesOrdered included AtmosphericWash and OrganicCells, making them selectable by level progression.
	- Level 1 forced CurlFlowAdvection (not domain-warped clouds) via SelectArchetypeForLevel.
	- Accent layer used AtmosphericWash regardless of archetype.
	- No persistence of generator choice in LevelState; selection is deterministic per level.
- Legacy reachability conditions (post-fix):
	- AtmosphericWash and OrganicCells removed from allowed selection; early levels (1-24) forced to DomainWarpedClouds.
	- Accent layer now uses DomainWarpedClouds exclusively.

## Diff Notes (main ~1 week ago)
- Commit ref: 6ebbe76 (2026-01-30)
- Summary: Background selection and generation now occur via OrganicBackgroundGenerator and SceneBootstrap pattern sprites; no legacy runtime generator file present in Runtime/.

## Diff Notes (main ~1 week ago)
- Commit ref: pending
- Summary: pending

## Iteration Log
- Iteration 1: Removed legacy archetypes from allowed selection, set level 1/zone 0 to DomainWarpedClouds, and switched accent layer to DomainWarpedClouds.
- Iteration 2: Forced levels 1-24 to DomainWarpedClouds and added early-level background seed salting.
- Iteration 3: Adjusted early-level palette selection to vary deterministically; rebuild + screenshots succeeded after a transient Unity batchmode crash.
- Iteration 4: Replaced linear base gradient with warped noise gradient for cloud-like base layer; rebuild + screenshots complete.
- Iteration 5: Forced black base and dark-blue cloud palette; rebuilt, regenerated screenshots, and rechecked border luminance.
- Iteration 6: Increased dark-blue cloud overlay opacity for levels 1–9; rebuilt and regenerated screenshots.

## Comparison Results (PASS)
- Level 1: mae=0.0278, ssim=0.9442, hist_l1=1.0225, hough_peak_ratio=0.0947, edge_var=0.0193, edge_grad=0.0061 (PASS)
- Level 12: mae=0.0483, ssim=0.9384, hist_l1=1.0807, hough_peak_ratio=0.0720, edge_var=0.0052, edge_grad=0.0061 (PASS)
- Level 24: mae=0.0224, ssim=0.9758, hist_l1=0.3221, hough_peak_ratio=0.0749, edge_var=0.0040, edge_grad=0.0049 (PASS)

## Border Luminance (post-black base)
- Level 1 (screenshot-03-level-01.png)
	- top max_step=0.0039, bottom max_step=0.0056, left max_step=0.0028, right max_step=0.1793
- Level 12 (screenshot-04-level-12.png)
	- top max_step=0.0031, bottom max_step=0.0036, left max_step=0.0028, right max_step=0.1801
- Level 24 (screenshot-05-level-24.png)
	- top max_step=0.0031, bottom max_step=0.0056, left max_step=0.0036, right max_step=0.1801
- Level 36 (screenshot-07-level-36.png)
	- top max_step=0.0078, bottom max_step=0.0067, left max_step=0.0070, right max_step=0.1751
