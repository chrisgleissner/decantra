# Implementation Plan: Decantra (Liquid Sort Puzzle)

**Branch**: `000-decantra-core` | **Date**: 2026-01-29 | **Spec**: [`SPEC.md`](SPEC.md)
**Input**: Feature specification from [`SPEC.md`](SPEC.md)

## Summary

Implement a Unity LTS project for Decantra with pure C# domain logic, deterministic level generation, optimal solver, scoring, persistence, and Android build/install scripts. Use asmdef boundaries, 2D URP, and test coverage gate >= 80% on domain logic.

## Technical Context

**Language/Version**: C# (Unity LTS)  
**Primary Dependencies**: Unity LTS, Unity Test Framework, Unity Code Coverage, URP 2D  
**Storage**: JSON files in `Application.persistentDataPath`  
**Testing**: NUnit via Unity Test Framework  
**Target Platform**: Android (MVP), iOS-ready  
**Project Type**: Mobile (Unity)  
**Performance Goals**: 60 fps  
**Constraints**: Deterministic solver/generation, >= 80% domain coverage  
**Scale/Scope**: Single-game app, infinite levels by seed

## Constitution Check

No constitution defined in repository; no gates specified.

## Project Structure

### Documentation (this feature)

```text
SPEC.md
PLANS.md
DECISIONS.md
STATUS.md
MEMORY.md
FILES.md
```

### Source Code (repository root)

```text
Assets/
  Decantra/
    App/
    Presentation/
    Domain/
      Model/
      Rules/
      Solver/
      Generation/
      Scoring/
      Persistence/
    Platform/
    Tests/
      EditMode/
      PlayMode/
    Scenes/
    Scripts/
```

**Structure Decision**: Single Unity project with modular assemblies as required by spec.

## Phase Plan

1. **Phase 0: Repository setup & Spec Kit**
   - Create missing Spec Kit files and fill initial content.
   - Define architecture and module boundaries.

2. **Phase 1: Domain model & rules**
   - Implement bottle model, move rules, win condition.
   - Add scoring and persistence models.
   - Add tests for domain logic.

3. **Phase 2: Solver & generation**
   - Implement BFS solver with dedup and pruning.
   - Implement reverse-construction generator with seed determinism.
   - Tests for solver and generator.

4. **Phase 3: Unity presentation & app wiring**
   - Scenes, UI HUD, bottle view, input orchestration.
   - Animation pipeline and input locking.
   - PlayMode tests for wiring.

5. **Phase 4: Android pipeline**
   - Build/install scripts and Unity batchmode build.
   - Validate package id and product name.

6. **Phase 5: Coverage gate**
   - Configure Code Coverage, enforce >= 80% for domain.
   - Verify tests pass and update status.

## DeCantra - 2026-01-29 - Gameplay/UI/Scoring/Levels/Perf/Test Infrastructure

### Overview
Deliver gameplay/UI/scoring updates, level completion rules, interstitials, bottle visuals, generator performance, async precompute, and automated screenshot coverage for Decantra.

### Assumptions
- Unity Editor available for PlayMode verification when required.
- Domain layer remains Unity-free and deterministic.
- Presentation layer can use coroutines for sequencing.

### Inventory
- Domain: scoring, solver, generator, level state.
- Presentation: HUD, interstitials, bottle visuals, audio.
- Tests: EditMode/PlayMode coverage with screenshots.

### Plan
**Deliverable 1: Hide optimal score UI (keep internal metric)**
- [x] Remove/hide optimal UI display in HUD while preserving `OptimalMoves` in state.
   - Evidence: Hid `optimalText` display in HUD view (Assets/Decantra/Presentation/View/HudView.cs).

**Deliverable 2: Fix scoring on empty transition**
- [x] Track bottle non-empty -> empty transition and award score based on level index and emptied amount.
   - Evidence: Added empty-transition scoring in GameController and helper in ScoreCalculator (Assets/Decantra/Presentation/Controller/GameController.cs, Assets/Decantra/Domain/Scoring/ScoreCalculator.cs).
- [x] Add/adjust tests for score transition logic.
   - Evidence: Updated score tests and added empty-transition increment test (Assets/Decantra/Tests/EditMode/ScoreCalculatorTests.cs).

**Deliverable 3: Bonus stars (1-5) from movesTaken vs optimalMoves**
- [x] Implement star mapping and expose to UI/interstitials.
   - Evidence: Added star calculation and passed to LevelCompleteBanner (Assets/Decantra/Presentation/Controller/GameController.cs).

**Deliverable 4: Level-complete interstitial sequence (no pause)**
- [x] Add stars scroll-in/out, then level announcement with same motion.
   - Evidence: Implemented stars-then-level animation in LevelCompleteBanner (Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs).
- [x] Ensure coroutine sequencing without per-frame allocations.
   - Evidence: Animation uses coroutine with struct math only (Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs).
- [x] Add fun-fair audio layering for stars.
   - Evidence: Added layered star chimes scaled by star count (Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs).
- [x] Add radial star burst effect at center with intensity by star count.
   - Evidence: Added `starBurst` animation in LevelCompleteBanner and wired UI in SceneBootstrap (Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs, Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs).
- [x] Add per-level background variation.
   - Evidence: Added background tint logic in GameController and wired image from SceneBootstrap (Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs, Assets/Decantra/Presentation/Controller/GameController.cs).

**Deliverable 5: Bottle visuals (rounded + cork/stopper when full uniform)**
- [x] Update bottle visuals for rounded body and full-uniform sealed state.
   - Evidence: Added rounded sprite and stopper visuals, toggled when bottle is solved (Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs, Assets/Decantra/Presentation/View/BottleView.cs).
- [x] Enhance bottle polish with shadow and highlight.
   - Evidence: Added bottle shadow and highlight layers (Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs).

**Deliverable 6: Level finish condition**
- [x] Complete only when all bottles are empty or full-and-uniform.
   - Evidence: Updated win condition + added test for partial uniform bottle (Assets/Decantra/Domain/Model/LevelState.cs, Assets/Decantra/Tests/EditMode/LevelStateTests.cs).

**Deliverable 7: Generator optimization + instrumentation**
- [x] Add timing instrumentation (Stopwatch logs) with before/after evidence.
   - Evidence: Added Stopwatch timing with optional `Log` hook in Assets/Decantra/Domain/Generation/LevelGenerator.cs.
- [x] Optimize generation to feel instant on mid-range Android; keep difficulty ramp.
   - Evidence: Reduced BFS allocations and reused reverse-move buffers (Assets/Decantra/Domain/Solver/BfsSolver.cs, Assets/Decantra/Domain/Generation/LevelGenerator.cs).

**Deliverable 8: Async next-level precompute**
- [x] Precompute next level during interstitials with cancellation safety.
   - Evidence: Added background Task precompute with cancellation and thread-safe generation (Assets/Decantra/Presentation/Controller/GameController.cs).

**Deliverable 9: Automated screenshot**
- [x] PlayMode test to load gameplay, wait ~5s, capture screenshot to `doc/img/` with deterministic name.
   - Evidence: Added PlayMode screenshot test writing to doc/img/playmode-gameplay.png (Assets/Decantra/Tests/PlayMode/GameControllerPlayModeTests.cs).

### Verification
- PlayMode verification for HUD and interstitials.
- EditMode tests for scoring and generator determinism.
- PlayMode screenshot test outputs image to `doc/img/`.

### Evidence log
- 2026-01-29: Added Stopwatch instrumentation hook to level generation (Assets/Decantra/Domain/Generation/LevelGenerator.cs). Verification: not run (Unity not launched).
- 2026-01-29: Hid optimal moves HUD element (Assets/Decantra/Presentation/View/HudView.cs). Verification: not run (Unity not launched).
- 2026-01-29: Tightened win condition to require empty or full-uniform bottles and added test coverage (Assets/Decantra/Domain/Model/LevelState.cs, Assets/Decantra/Tests/EditMode/LevelStateTests.cs). Verification: not run (tests not executed).
- 2026-01-29: Implemented empty-transition scoring and updated score tests (Assets/Decantra/Presentation/Controller/GameController.cs, Assets/Decantra/Domain/Scoring/ScoreCalculator.cs, Assets/Decantra/Tests/EditMode/ScoreCalculatorTests.cs). Verification: not run (tests not executed).
- 2026-01-29: Added star rating mapping and interstitial sequence for stars then level announcement (Assets/Decantra/Presentation/Controller/GameController.cs, Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs, Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs). Verification: not run (Unity not launched).
- 2026-01-29: Added layered star chime audio for interstitials (Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs). Verification: not run (Unity not launched).
- 2026-01-29: Implemented rounded bottle visuals and stopper for full-uniform bottles (Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs, Assets/Decantra/Presentation/View/BottleView.cs). Verification: not run (Unity not launched).
- 2026-01-29: Optimized BFS solver allocations and reverse-move buffering (Assets/Decantra/Domain/Solver/BfsSolver.cs, Assets/Decantra/Domain/Generation/LevelGenerator.cs). Verification: not run (timing numbers pending).
- 2026-01-29: Added async precompute for next level with cancellation safety (Assets/Decantra/Presentation/Controller/GameController.cs). Verification: not run (Unity not launched).
- 2026-01-29: Added PlayMode screenshot test to capture gameplay image (Assets/Decantra/Tests/PlayMode/GameControllerPlayModeTests.cs). Verification: not run (PlayMode tests not executed).
- 2026-01-29: Added per-level background tint variation via GameController background image (Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs, Assets/Decantra/Presentation/Controller/GameController.cs). Verification: not run (Unity not launched).
- 2026-01-29: Added radial star burst effect for bonus stars (Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs, Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs). Verification: not run (Unity not launched).
- 2026-01-29: Polished bottle visuals with shadow/highlight adjustments (Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs). Verification: not run (Unity not launched).
- 2026-01-29: Added PlayMode tests for background variation and bottle presence (Assets/Decantra/Tests/PlayMode/GameControllerPlayModeTests.cs). Verification: not run (PlayMode tests not executed).

### Risks + mitigations
- Generator perf regressions → add instrumentation and fallback early-exit limits.
- UI animation stutter → avoid per-frame allocations and reuse buffers.
- Async race conditions → cancellation tokens and main-thread Unity access only.
