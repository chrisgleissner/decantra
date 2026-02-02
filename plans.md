# CI Build Time Optimization Plan

## Status: ACTIVE

This plan documents the optimization of Unity Android build time on GitHub-hosted runners
to achieve >= 50% reduction in wall-clock time while preserving merge confidence.

---

## 1. Baseline Measurement

### Run Details
- **Timestamp**: 2026-02-02T17:47:58Z
- **Commit SHA**: 0ef49f6
- **Branch**: fix/ensure-level-solvability
- **Runner Image**: ubuntu-latest
- **Runner Type**: GitHub-hosted
- **Workflow Name**: Build Decantra
- **Run ID**: 21601068991
- **Event**: pull_request

### Job Durations (Baseline)

| Job | Start | End | Duration |
|-----|-------|-----|----------|
| Check Unity license secrets | 17:48:02 | 17:48:05 | **3s** |
| Unity tests (EditMode + PlayMode) | 17:48:08 | 17:53:09 | **5m 1s** |
| Build Android APK/AAB | 17:53:12 | 18:29:20 | **36m 8s** |
| **Total Wall Time** | 17:48:02 | 18:29:20 | **~41m 18s** |

### Key Step Timings (Build Android Job)
| Step | Duration |
|------|----------|
| Checkout | 2s |
| Free disk space | 1m 16s |
| Cache restore | 13s |
| Unity Builder (Release APK) | 18m 28s |
| Unity Builder (Release AAB) | 1m 34s |
| Unity Builder (Debug APK) | 14m 19s |
| Artifact upload | 8s |

### Observations
1. Android build job dominates: **36+ minutes** (87% of total)
2. Cache hit status: Unknown (no explicit logging)
3. Three separate Unity builds executed sequentially
4. No concurrency controls - stale runs not cancelled

---

## 2. Optimization Hypotheses

| # | Hypothesis | Expected Impact | Risk |
|---|------------|-----------------|------|
| 1 | Improve Unity Library cache key and restore | **-5 to -10 min** (avoid reimport) | Low |
| 2 | Add Gradle wrapper/caches caching | **-1 to -2 min** | Low |
| 3 | Split fast tests from heavy builds (two-tier CI) | **-36 min on PR updates** | Low |
| 4 | Add concurrency controls (cancel stale) | **Prevent wasted builds** | Low |
| 5 | Path filtering for docs-only changes | **Skip builds entirely** | Low |
| 6 | Cache IL2CPP build outputs | **-3 to -8 min** | Medium |

---

## 3. Implementation Checklist

- [x] Add reliable timing instrumentation
- [x] Improve Unity Library cache (better key, restore-keys)
- [x] Add Gradle caching
- [x] Add concurrency controls (cancel-in-progress)
- [x] Split workflow into Tier 1 (fast checks) and Tier 2 (heavy builds)
- [x] Add path filtering for docs-only changes
- [ ] Verify cache hits in logs
- [ ] Measure improved run times
- [ ] Document results

---

## 4. Verification Protocol

### Acceptance Criteria
1. **Tier 1 (fast checks)** completes in < 6 minutes
2. **Tier 2 (Android build)** runs only on main/tags/manual trigger
3. Workflow demonstrates >= 50% time reduction for PR updates
4. Cache hit logs visible in improved runs
5. All existing functionality preserved

### Test Procedure
1. Push workflow changes to PR branch
2. Record Tier 1 timing
3. Manually trigger Tier 2 build
4. Compare with baseline

---

## 5. Post-Change Measurement

*To be filled after optimization runs complete*

| Metric | Baseline | Improved | Change |
|--------|----------|----------|--------|
| PR build time | ~41m 18s | TBD | TBD |
| Cache hit rate | Unknown | TBD | TBD |
| Stale builds cancelled | N/A | TBD | TBD |

---

# Decantra Level Difficulty Improvement Plan

## Status: ACTIVE

This plan addresses the requirement to produce monotonically increasing difficulty
from levels 1-100, constant maximum difficulty from 101-1000, with explicit bottle
size diversity, sink bottle pressure, and color fragmentation.

---

## 1. Problem Analysis

### Current State (from solver-solutions-debug.txt)
- **Monotonicity violations**: 5 levels violate monotonicity (7, 8, 10, 12, 15)
- **Difficulty range**: 33-67 (insufficient spread)
- **98.8% of levels**: difficulty 50-60 (severe plateau from level 18 onward)
- **Bottle sizes**: Only 2 sizes used (4 and 5), violating diversity requirement
- **Root cause**: Generator difficulty parameters do not scale linearly with level

### Required Outcomes
1. Linear difficulty increase from level 1 to 100
2. Maximum difficulty at level 100
3. Constant maximum difficulty from level 101 to 1000
4. At least 3 distinct bottle capacities in mid-levels, 4-5 in high levels
5. Zero monotonicity violations
6. All levels solvable
7. Completion enforces single-color + maximally merged

---

## 2. Difficulty Definition (BINDING)

**Difficulty** = How hard it is for a HUMAN PLAYER to solve the level.

This is NOT:
- The level index
- An artificial renormalization
- A proxy metric

Difficulty emerges from real gameplay constraints:
1. **Bottle size variation** - asymmetric capacities create planning puzzles
2. **Sink bottles** - restrict pour options, force sequencing
3. **Color fragmentation** - scattered colors require more moves

---

## 3. Linear Scaling Functions (Levels 1-100)

All difficulty-driving parameters scale linearly from level 1 to 100 using:

```
value(level) = minVal + (maxVal - minVal) * (level - 1) / 99
```

### 3.1 Bottle Capacity Diversity

| Parameter | Level 1 | Level 100 | Notes |
|-----------|---------|-----------|-------|
| Distinct capacities | 2 | 5 | Min capacities in use |
| Small capacity min | 4 | 2 | Smallest bottle size |
| Large capacity max | 5 | 10 | Largest bottle size |
| Capacity entropy | 0.0 | 1.0 | Normalized Shannon entropy |

**Capacity pools by level tier:**
- Level 1-20: [3, 4, 5]
- Level 21-50: [2, 3, 4, 5, 6]
- Level 51-80: [2, 3, 4, 5, 7, 8]
- Level 81-100: [2, 3, 4, 5, 6, 8, 10]

**Selection algorithm:**
1. Determine tier based on level
2. Randomly select capacities from tier pool
3. Ensure min distinct count met
4. Ensure at least one "small" (≤3) and one "large" (≥6) in levels ≥50

### 3.2 Sink Bottle Pressure

| Parameter | Level 1 | Level 100 | Notes |
|-----------|---------|-----------|-------|
| Sink count | 0 | 2 | Number of sink bottles |
| Sink capacity | 4 | 6 | Capacity of sink bottles |
| Sink placement | - | Strategic | Late levels bias sinks toward larger capacities |

**Sink introduction schedule:**
- Level 1-17: 0 sinks
- Level 18-50: 1 sink
- Level 51-100: 1-2 sinks (scaled linearly)

### 3.3 Color Fragmentation

| Parameter | Level 1 | Level 100 | Notes |
|-----------|---------|-----------|-------|
| Color count | 3 | 7 | Number of distinct colors |
| Avg fragments/color | 1.0 | 3.0 | How scattered each color is |
| Fragment size variance | 0.0 | 1.0 | Normalized variance in fragment sizes |

**Fragmentation algorithm:**
- During scramble, track fragment count per color
- Reject scrambles that don't meet fragmentation floor
- Higher levels require more scattered distributions

### 3.4 Scramble Depth

| Parameter | Level 1 | Level 100 | Notes |
|-----------|---------|-----------|-------|
| Reverse moves | 10 | 60 | Scramble move count |
| Min optimal | 3 | 15 | Minimum solution length |

---

## 4. Maximum Caps (Levels 101-1000)

All parameters clamp at level 100 values:

```csharp
public static int GetEffectiveLevel(int levelIndex)
{
    return Math.Min(levelIndex, 100);
}
```

| Parameter | Clamped Value |
|-----------|---------------|
| Distinct capacities | 5 |
| Capacity pool | [2, 3, 4, 5, 6, 8, 10] |
| Sink count | 2 |
| Color count | 7 |
| Avg fragments/color | 3.0 |
| Reverse moves | 60 |
| Min optimal | 15 |

Levels 101+ differ only by seed-based layout randomness.

---

## 5. Completion Rules (BINDING)

A level is complete if and only if:

### 5.1 Single-Color Condition
Every bottle is either empty OR contains liquid of exactly one color.

```csharp
for each bottle:
    if (!bottle.IsEmpty && !bottle.IsMonochrome)
        return false;
```

### 5.2 Maximally Merged Condition
No legal move exists that reduces the bottle count for any color.

```csharp
for each source bottle (non-empty, non-sink):
    for each target bottle (non-empty, same color, has space):
        if (source.Count <= target.FreeSpace)
            return false; // Can fully pour source into target
```

---

## 6. Implementation Steps

### 6.1 Solver Modifications (LevelState.IsWin)
✓ Already correctly implements:
- Single-color check (IsMonochrome)
- Maximally merged check (no full consolidation possible)

**Add unit tests for:**
- [ ] Invalid: multi-color bottle
- [ ] Invalid: mergeable same-color bottles (can consolidate)
- [ ] Valid: unmergeable due to capacity constraints
- [ ] Valid: unmergeable due to sink constraints

### 6.2 Level Generator Modifications

#### 6.2.1 New Classes
```csharp
// CapacityProfile.cs - Bottle capacity configuration by level
public sealed class CapacityProfile
{
    public int[] CapacityPool { get; }
    public int MinDistinct { get; }
    public int MinSmall { get; }  // Capacities ≤ 3
    public int MinLarge { get; }  // Capacities ≥ 6
}

// FragmentationProfile.cs - Color fragmentation requirements
public sealed class FragmentationProfile
{
    public float MinAvgFragments { get; }
    public float MinFragmentVariance { get; }
}
```

#### 6.2.2 LevelDifficultyEngine Changes
```csharp
public static CapacityProfile GetCapacityProfile(int levelIndex)
{
    int eff = Math.Min(levelIndex, 100);
    // Return tier-appropriate capacity pool
}

public static int ResolveSinkCount(int levelIndex)
{
    int eff = Math.Min(levelIndex, 100);
    if (eff < 18) return 0;
    if (eff < 51) return 1;
    return 1 + (eff - 51) / 50; // Scales to 2 at level 100
}

public static FragmentationProfile GetFragmentationProfile(int levelIndex)
{
    int eff = Math.Min(levelIndex, 100);
    float avgFragments = 1.0f + (eff - 1) * 2.0f / 99f;
    float variance = (eff - 1) / 99f;
    return new FragmentationProfile(avgFragments, variance);
}
```

#### 6.2.3 LevelGenerator.BuildColorCapacities Changes
Replace fixed 4/5 capacities with:
```csharp
private static List<int> BuildColorCapacities(int levelIndex, int colorCount, Random rng)
{
    var profile = LevelDifficultyEngine.GetCapacityProfile(levelIndex);
    var capacities = new List<int>(colorCount);
    
    // Ensure minimum distinct capacities
    var distinctUsed = new HashSet<int>();
    
    // First, add required small and large bottles
    if (profile.MinSmall > 0)
        AddFromRange(capacities, distinctUsed, profile.CapacityPool, 2, 3, profile.MinSmall, rng);
    if (profile.MinLarge > 0)
        AddFromRange(capacities, distinctUsed, profile.CapacityPool, 6, 10, profile.MinLarge, rng);
    
    // Fill remaining with variety
    while (capacities.Count < colorCount)
    {
        int cap = profile.CapacityPool[rng.Next(profile.CapacityPool.Length)];
        capacities.Add(cap);
        distinctUsed.Add(cap);
    }
    
    // Validate distinct count
    if (distinctUsed.Count < profile.MinDistinct)
        throw new InvalidOperationException("Insufficient capacity diversity");
    
    Shuffle(capacities, rng);
    return capacities;
}
```

### 6.3 CI Gate Integration

#### 6.3.1 Monotonicity Gate (tools/monotonicity_gate.sh)
```bash
#!/usr/bin/env bash
# Parse solver-solutions-debug.txt, fail on any violation
grep -E "^level=" solver-solutions-debug.txt | \
  awk -F'[=,]' 'prev && $4 < prev { exit 1 } { prev=$4 }'
```

#### 6.3.2 Solvability Gate (tools/solvability_gate.sh)
```bash
#!/usr/bin/env bash
# Fail if any level has "UNSOLVABLE" or "ERROR"
if grep -qE "UNSOLVABLE|ERROR" solver-solutions-debug.txt; then
  exit 1
fi
```

#### 6.3.3 Bottle Size Diversity Gate (tools/diversity_gate.sh)
Parse each level's bottle capacities and fail if:
- Levels 50+ have fewer than 3 distinct capacities
- Levels 80+ have fewer than 4 distinct capacities
- Any level 50+ lacks both small (≤3) and large (≥6) bottles

#### 6.3.4 Build Script Integration
Add to `./build`:
```bash
if [[ "${RUN_TESTS}" == true ]]; then
  ./tools/monotonicity_gate.sh
  ./tools/solvability_gate.sh
  ./tools/diversity_gate.sh
fi
```

---

## 7. Validation Workflow

1. Run `./build --generate-solutions`
2. Parse `solver-solutions-debug.txt`
3. Verify monotonicity (diff[N+1] >= diff[N] for N < 100)
4. Verify plateau (diff[N] == 100 for N >= 100)
5. Verify solvability (no UNSOLVABLE/ERROR)
6. Verify diversity (capacity variety)
7. Run unit tests

---

## 8. Exit Criteria Checklist

- [ ] Linear monotone difficulty increase from level 1 to 100
- [ ] Maximum difficulty reached at level 100
- [ ] Constant maximum difficulty from level 101 to 1000
- [ ] Explicit bottle size diversity (3+ capacities mid, 4-5 high)
- [ ] solver-solutions-debug.txt shows ZERO monotonicity violations
- [ ] ALL levels solvable
- [ ] Completion enforces single-color + maximally merged
- [ ] Difficulty definition unchanged
- [ ] Structural generator changes only
- [ ] Monotonicity gate integrated into CI
- [ ] Solvability gate integrated into CI
- [ ] Size diversity gate integrated into CI
- [ ] All tests pass
- [ ] CI build is green

---

## 9. Risk Mitigation

### Risk: Over-constrained generation (unsolvable levels)
**Mitigation**: Relaxed mode already exists after 18 attempts. Add capacity
diversity to relaxed constraints.

### Risk: Solver timeout on complex levels
**Mitigation**: Keep A* solver with current 10M node limit. Monitor p95 solve time.

### Risk: Difficulty score doesn't reflect real human difficulty
**Mitigation**: Use solver metrics (optimal moves, trap score, branching) as proxies.
These are validated against research (doc/level-generation-research.md).

---

## 10. Implementation Order

1. **Phase 1**: Add CapacityProfile and modify BuildColorCapacities
2. **Phase 2**: Add FragmentationProfile and modify scramble rejection
3. **Phase 3**: Implement linear scaling in LevelDifficultyEngine
4. **Phase 4**: Add CI gates
5. **Phase 5**: Regenerate solutions and iterate
6. **Phase 6**: Final validation and documentation

---

## Changelog

- **2026-02-02**: Initial plan created
