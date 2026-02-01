# Continuation Prompt for Next LLM (Decantra)

You are continuing work in `/home/chris/dev/decantra`.

## Goal
Fix critical level generation/solvability/capacity/reset/scoring issues and deliver exhaustive tests + passing test suite + Android APK build. Must follow Plan → Implement → Test → Verify → Build loop. Do not stop early.

## Current Status (as of 2026-01-31)
- EditMode tests were passing after generator changes.
- PlayMode tests failing: `ResetButton_RestoresInitialStateAndScore` in `Assets/Decantra/Tests/PlayMode/GameControllerPlayModeTests.cs`.
  - Failure message: `Expected: greater than 0 But was: 0` (assert on `ProvisionalScore > 0`).
  - Root cause: Level 1 can be generated already solved (optimal 0), so no valid moves / no provisional score. Need to prevent solved/optimal=0 levels in generator (ensure optimalMoves >= 2). Implemented generator change but tests not rerun due to user abort.

## Recent Key Changes
- **Generator** (`Assets/Decantra/Domain/Generation/LevelGenerator.cs`):
  - Reverse-scramble algorithm via bottle moves.
  - Added `ReduceEmptyCount` post-process to cap empty bottles.
  - Added `GetMaxReverseAmount` to prevent creating immediately-solved bottles.
  - `ScrambleState` now returns applied moves count; generator stores that in `ScrambleMoves`.
  - **New**: generator now validates and solves inside generation loop; rejects if solver fails or optimalMoves < 2. MovesAllowed computed there; state returned precomputed.
- **Solver** (`Assets/Decantra/Domain/Solver/BfsSolver.cs`):
  - `SolveWithPath` added, path reconstruction support.
- **Rules/Integrity**: `Assets/Decantra/Domain/Rules/LevelIntegrity.cs` added; validates capacity, volume, sink sealing.
- **Score session** (`Assets/Decantra/Domain/Scoring/ScoreSession.cs`):
  - Added `AttemptStartTotalScore`, `BeginAttempt`, `ResetAttempt` semantics; commit/rollback enforced.
- **Reset Button**: `Assets/Decantra/Presentation/Runtime/SceneBootstrap.cs` adds Reset button bottom-right; `WireResetButton` hooks to `GameController.ResetCurrentLevel`.
- **GameController**: reset semantics implemented; `BeginAttempt` on load; `ResetAttempt` on reset/fail; `CommitLevel` on win. Also unlocks input at end of `LoadLevel`.

## Known Issues / Next Steps
1. **Re-run tests**:
   - `UNITY_PATH=/home/chris/Unity/Hub/Editor/6000.3.5f2/Editor/Unity ./tools/test.sh`.
   - Expect PlayMode test to pass after generator fix; confirm.
2. **If PlayMode still fails**:
   - Ensure generator **never returns optimalMoves < 2** and never returns a solved state.
   - If level can still be solved initially, check `IsAcceptableStart` or `LevelIntegrity` for additional constraints.
3. **If tests pass**:
   - Run coverage gate `./tools/coverage_gate.sh`.
   - Build APK: `./tools/build_android.sh`.

## Build/Test Notes
- Unity attempts to scan ADB; it may log errors about ADB/inotify, but this did not block tests earlier.
- Last test runs:
  - EditMode: passing after generator changes.
  - PlayMode: failing only on reset/provisional test because provisional score was 0.

## Files Most Recently Edited
- `Assets/Decantra/Domain/Generation/LevelGenerator.cs`
- `Assets/Decantra/Presentation/Controller/GameController.cs`
- `Assets/Decantra/Domain/Solver/BfsSolver.cs`
- `Assets/Decantra/Domain/Rules/LevelIntegrity.cs`
- `Assets/Decantra/Domain/Scoring/ScoreSession.cs`
- Tests: `Assets/Decantra/Tests/EditMode/*.cs`, `Assets/Decantra/Tests/PlayMode/GameControllerPlayModeTests.cs`

## Requested Behavior Constraints
- Solvability guaranteed with optimalMoves stored.
- Capacity / sink constraints must be consistent.
- Reset button bottom-right near Max LV.
- Scoring provisional with commit on win and rollback on reset/fail.
- Exhaustive deterministic tests (>=200 levels fuzz)
- Build Android APK after tests pass.

Proceed immediately: re-run tests, fix any failures, then build APK and update `plans.md`.
