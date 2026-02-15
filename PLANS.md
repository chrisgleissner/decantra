# PLANS — Terminal State & Visual Gap Fix (2026-02-15)

## Objective

Three fixes ensuring solved levels display correctly and only accept fully-filled bottles as complete.

## Scope Constraints

- [x] No gameplay mechanics changes beyond tightened `IsWin()` predicate.
- [x] No bottle geometry redesign.
- [x] No neck-area changes.
- [x] Presentation changes are pixel-level corrections only.

## Issue 1 — Terminal State Accepts Partially Filled Bottles

### Problem
`LevelState.IsWin()` checked monochrome + irreducible but did NOT require `IsFull`. With variable-capacity bottles, the solver could find terminal states with partially-filled monochrome bottles accepted as "solved."

### Fix
Added `if (!bottle.IsFull) return false;` in `IsWin()` before the monochrome check. Every non-empty bottle must now be both full AND monochrome.

### Files Changed
- `Assets/Decantra/Domain/Model/LevelState.cs` — added `IsFull` guard

### Tests Updated
- `LevelCompletionTests.cs` — 6 tests flipped from `Assert.IsTrue` to `Assert.IsFalse` for partial-monochrome scenarios
- `LevelStateTests.cs` — 1 test flipped (`IsWin_PassesWithPartiallyFilledUniformBottle_IfIrreducible` → `IsWin_FailsWithPartiallyFilledUniformBottle_EvenIfIrreducible`)

### Tests Added
- `TerminalStateInvariantTests.cs` — 9 new tests:
  - 7 unit tests for `IsWin()` edge cases (partial, full, empty, mixed, regression)
  - 1 generator invariant test (color units match bottle capacity)
  - 1 integration test (25 levels: generate → solve → verify all bottles full or empty)

## Issue 2 — Visual Gap at Top of Full Bottles

### Problem
`RefSlotRootHeight` (300) was smaller than `liquidMask` height (320), creating a 20px gap. Plus `slotRoot` had `anchoredPosition.y = -2`, adding 2px more. Full bottles showed empty slivers at top of main body.

### Fix
- `BottleView.cs`: `RefSlotRootHeight` 300 → 320 (matches liquidMask height)
- `SceneBootstrap.cs`: `liquidRect.sizeDelta` (112,300) → (112,320); `anchoredPosition` (0,-2) → (0,0)

## Issue 3 — Top Row Bottles Overlap HUD

### Problem
`TopRowsDownwardOffsetPx = 35` didn't provide enough clearance from RESET/OPTIONS buttons.

### Fix
- `HudSafeLayout.cs`: `TopRowsDownwardOffsetPx` 35 → 50

## Execution Checklist

### Phase 1 — Investigation
- [x] Root cause analysis for all three issues.
- [x] Traced rendering pipeline: BottleView → slotRoot → liquidMask → segments.
- [x] Confirmed RefSlotRootHeight/liquidMask height mismatch (300 vs 320).

### Phase 2 — Fix
- [x] `LevelState.cs`: `IsFull` guard in `IsWin()`.
- [x] `BottleView.cs`: `RefSlotRootHeight` 300 → 320.
- [x] `SceneBootstrap.cs`: slotRoot size 300 → 320, offset -2 → 0.
- [x] `HudSafeLayout.cs`: `TopRowsDownwardOffsetPx` 35 → 50.

### Phase 3 — Tests
- [x] 7 flipped existing tests (6 in LevelCompletionTests, 1 in LevelStateTests).
- [x] 9 new tests in TerminalStateInvariantTests.
- [x] EditMode tests: **216 passed, 0 failed** (2026-02-15).

### Phase 4 — Regression Safety
- [x] All 216 EditMode tests passing.
- [ ] PlayMode tests (pending full script completion).
- [ ] On-device visual verification.

## Verification Log

- **2026-02-15 00:01**: EditMode tests 216/216 passed (including 9 new TerminalStateInvariantTests).
- **2026-02-15**: SolvabilityFuzzTests (60 levels) passed with tightened IsWin — solver finds fully-packed solutions within node limits.
