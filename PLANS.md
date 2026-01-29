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
