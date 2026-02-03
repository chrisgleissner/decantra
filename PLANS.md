# Execution Plan - Difficulty Invariant Enforcement

## Objective

Enforce and verify:
- Levels 1..200: difficulty == level
- Levels >= 201: difficulty == 100

## Plan

### Step 1: Inspect and document current difficulty path
- [x] Identify authoritative difficulty computation path
- [x] Identify all difficulty emitters (solver output, reports, tests, gates)

### Step 2: Implement explicit deterministic difficulty mapping
- [x] Add single authoritative difficulty mapping method
- [x] Route solver output difficulty through this mapping
- [x] Remove/avoid divergent mappings for emitted difficulty

### Step 3: Update validation gates and tests
- [x] Update solver output validation to enforce invariant
- [x] Update edit mode tests to enforce invariant
- [x] Ensure no conflicting expectations remain

### Step 4: Regenerate solver output and validate
- [x] Run ./build --generate-solutions
- [x] Verify solver-solutions-debug.txt difficulty invariant PASS
- [x] Verify monotonicity/linearity validation PASS

### Step 5: Full verification (exit conditions)
- [x] Run unit tests
- [x] Run integration tests
- [x] Regenerate screenshots
- [x] Build release APK
- [x] Confirm CI green

## Progress Log
- Step 1: COMPLETE
- Step 2: COMPLETE
- Step 3: COMPLETE
- Step 4: COMPLETE
- Step 5: COMPLETE
