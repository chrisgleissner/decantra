# PLANS - Decantra Difficulty & Background Fixes

## Part A: Difficulty Progression & Solver Validation Fixes

### Intent
- Relax strict monotonic difficulty constraints.
- Allow local difficulty fluctuations.
- Ensure validation passes if general trend is correct, allowing regressions.

### Steps
- [x] [A1] Analyze `MonotonicLevelSelector.cs` and `Reproduction/Program.cs` validation logic.
- [x] [A2] Modify `MonotonicLevelSelector` to relax strict monotonicity checks.
- [x] [A3] Update `Reproduction/Program.cs` to allow difficulty regressions in validation.
- [x] [A4] Verify changes by running `dotnet run -- monotonic 100` (or similar command).
- [x] [A5] Ensure solver verifies solvability without enforcing strict difficulty increase.

## Part B: Background Rendering Fixes & Artifact Regeneration

### Intent
- Fix background rendering issues (inferred from context).
- Regenerate derived artifacts (screenshots, etc.).

### Steps
- [x] [B1] Investigate background rendering code (based on user request title).
- [x] [B2] Apply necessary fixes to background generation/rendering.
- [x] [B3] Regenerate artifacts (solutions/monotonic logs).

## Part C: Final Verification

### Intent
- Ensure all criteria are met.

### Steps
- [x] [C1] Verify Level 1-200 progression is generally increasing but allows fluctuation.
- [x] [C2] Verify build and tests pass.
