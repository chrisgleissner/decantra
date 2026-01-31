# Decantra Critical Integrity Fix Plan

## Plan - Fix Level 10 + Solvability + Export Language (2026-01-31)
- [x] Audit Level 10 generation + reproduction seed, identify root cause of unsolvability. (Validate: run EditMode Level 10 regression test once added)
- [x] Add Level 10 regression test asserting at least one legal move and solvability. (Validate: EditMode Level 10 regression test)
- [x] Enforce generator validation for initial move + sink constraints + deterministic regeneration. (Validate: EditMode Generator invariants tests)
- [x] Add pour legality invariant unit tests (non-sink, sink behavior, determinism). (Validate: EditMode Pour legality tests)
- [x] Implement level export language model + serializer + parser + validation. (Validate: EditMode serialization tests)
- [x] Add hidden share UI under level indicator with Android intent + Editor fallback capture. (Validate: PlayMode export UI tests + EditMode share payload tests)
- [x] Add level language spec doc with example JSON. (Validate: doc review + serialization tests)
- [x] Integrate new logo into app icon + intro + header with safe-area layout. (Validate: PlayMode UI tests + Android build launch)
- [x] Run full EditMode test suite. (Validate: Unity Test Runner EditMode)
- [x] Run full PlayMode test suite. (Validate: Unity Test Runner PlayMode)
- [x] Run full test suite 3x or 1x + flaky reruns if time-limited. (Validate: consecutive full passes)

Status: Complete

Single source of truth: This file governs all work for the critical solvability, reset, and scoring fixes.

## Scope summary
- Guarantee solvable-by-construction levels with stored optimal move counts.
- Enforce capacity-consistent win conditions (including sink bottles).
- Add reset-for-retry UI and per-attempt score integrity.
- Unify rules engine for gameplay, solver, and generator.
- Add exhaustive deterministic tests (unit, solver, fuzz/property, PlayMode).
- Build a working Android APK after all tests pass.

## Phase 0: Recon + constraints
- [x] Confirm Unity version, packages, and project settings.
- [x] Audit domain model for bottle capacities, sink rules, solver, and generator invariants.
- [x] Locate scoring persistence + per-attempt logic in app/presentation.
- [x] Identify UI layout anchor for Reset button near "MAX LV".

## Phase 1: Tests first (TDD)
- [x] Add unit tests for pour legality, capacity enforcement, and sink behavior.
- [x] Add unit tests for win condition under variable capacities.
- [x] Add generator invariant tests (volume conservation, capacity compatibility, solvable end state).
- [x] Add solver tests for known optimal move counts and determinism.
- [x] Add deterministic fuzz tests (>=200 levels across early/mid/high levels).
- [x] Add PlayMode tests for reset behavior and score rollback/commit.

## Phase 2: Implementation
- [x] Refactor to single authoritative rules engine for gameplay/solver/generator.
- [x] Fix win condition to align with capacity and sink constraints.
- [x] Implement solvable-by-construction generation + optimal solver validation.
- [x] Store and propagate optimalMoves + allowedMoves from solver results.
- [x] Implement reset-for-retry UI + semantics next to "MAX LV".
- [x] Implement provisional score tracking with commit on win + rollback on reset/fail.
- [x] Add development assertions for illegal states and capacity mismatches.

## Phase 3: Test + verify
- [x] Run EditMode tests.
- [x] Run PlayMode tests.
- [x] Verify coverage >= 80% for domain logic.
- [x] Validate generator + solver determinism across multiple seeds.

## Phase 4: Build
- [x] Build Android APK via batchmode.
- [x] Confirm build success and APK output path.

Notes:
- Android build succeeded with external SDK/NDK paths set (ANDROID_SDK_ROOT=/home/chris/Android/Sdk, ANDROID_NDK_ROOT=/home/chris/Android/Sdk/ndk/27.2.12479018).
