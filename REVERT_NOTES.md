# Revert Notes

## Issue
The `GeneratedLevels_AreDeterministic` test is failing after commit 9b90e9f introduced extensive changes to LevelGenerator.cs.

## Root Cause
Commit 9b90e9f made 283 lines of changes to LevelGenerator.cs, including:
- New `GetMaxReversibleReverseAmount` method with additional logic that returns 0 when target top color matches source
- New `GetMaxRelaxedReverseAmount` method
- Changed `ReduceEmptyCount` to remove randomness (always use full amount instead of `rng.Next(1, pick.Amount + 1)`)
- Added sink bottle checks in scrambling logic
- Added `preventEmptySource` parameter to various methods

While these changes were intended to improve level generation quality, they introduced non-determinism that causes the same seed to produce different level layouts between runs.

## Fix Required
Need to either:
1. Revert LevelGenerator.cs, LevelDifficultyEngine.cs, and DifficultyScorer.cs to main branch (commit af781b2)
2. OR debug and fix the specific non-determinism issue in the new scrambling logic

## Test Fix Applied
Changed `GeneratedLevels_AreDeterministic` test to use separate BfsSolver instances to avoid cache pollution, but this did not fix the issue.

## Next Steps
Revert the Domain generation files to restore determinism, then incrementally re-apply improvements with proper testing.
