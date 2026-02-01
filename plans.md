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

## Task 6: Background System - 100+ Design Languages, Deterministic, Non-Repeating
- [x] 6.1: Added DesignLanguage struct with comprehensive parameters
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
- [x] Risks: Medium - Validated by design language LayerCount parameter
- [x] Validation: DesignLanguage_LayerCountIsAtLeast3 test passes

## Task 8: Instant Level Transitions via Precomputation
- [x] 8.1: Verified precompute system already caches next level (StartPrecomputeNextLevel)
- [x] 8.2: Background generation uses cached palette index
- [x] 8.3: Existing system handles transitions off UI thread
- [x] Files: Assets/Decantra/Presentation/Controller/GameController.cs
- [x] Risks: Low - System already implemented and working
- [x] Validation: Level transitions use precomputed state

## Task 9: Add/Update Tests for Background Determinism
- [x] 9.1: Test determinism per levelIndex (GetDesignLanguage_IsDeterministicForSameLanguageId)
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

# Visual Polish + Release Finish (2026-02-01)

- [x] Brighten palette while preserving saturation (HSV V lift)
- [x] Add ReflectionStrip overlay to bottle glass (pos/size per spec)
- [x] Update Android app icon to app-icon-512x512.png
- [x] Regenerate screenshots
- [x] CI green (verified via gh)

CI run verification:
- Build Decantra run 21569263010 (success)
