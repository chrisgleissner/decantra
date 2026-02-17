# PLANS — Procedural Background Stage System Audit & Fix

Last updated: 2026-02-17

## Objective
Audit the procedural background stage system, fix defects, and ensure:
- A new background archetype activates every 10 levels (10, 20, 30, …).
- Slight color/parameter variation within each 10-level block.
- All 16 generator archetypes are reachable and wired.

---

## 1. Stage Logic Summary

### Level Progression
- `_currentLevel` starts at 1, increments by 1 on each completion.
- Set in `ApplyLoadedState()` at `GameController.cs:1436`.

### Zone Formula (`BackgroundRules.GetZoneIndex`)
```
levels  1-9  → zone 0
levels 10-19 → zone 1
levels 20-29 → zone 2
levels 30-39 → zone 3  …etc (10 levels per zone after zone 0)
```
File: `Assets/Decantra/Domain/Rules/BackgroundRules.cs:56-62`

### Archetype Selection (`SelectArchetypeForLevel`) — **BEFORE FIX**
```csharp
if (levelIndex <= 24) return DomainWarpedClouds;
int remainingCount = AllowedArchetypesOrdered.Length - 1; // 13
int offset = (uint)globalSeed % (uint)remainingCount;
int index = (levelIndex - 2 + offset) % remainingCount;
return AllowedArchetypesOrdered[1 + index];
```
**Defect:** Archetype changes EVERY LEVEL (not every 10). Levels 1-24 are
hardcoded to `DomainWarpedClouds` with no archetype transition at level 10 or 20.

---

## 2. Theme Inventory (Pre-fix)

16 distinct generator archetypes exist in the enum. All 16 have unique
implementations in `Assets/Decantra/Domain/Background/`.

| # | Theme Name | File | Type | Was Reachable |
|---|------------|------|------|---------------|
| 0 | DomainWarpedClouds | DomainWarpedCloudsGenerator.cs | Procedural | Levels 1-24 only |
| 1 | CurlFlowAdvection | CurlFlowAdvectionGenerator.cs | Procedural | Yes (post-24) |
| 2 | **AtmosphericWash** | AtmosphericWashGenerator.cs | Procedural | **NO — dead code** |
| 3 | FractalEscapeDensity | FractalEscapeDensityGenerator.cs | Procedural | Yes (post-24) |
| 4 | BotanicalIFS | BotanicalIFSGenerator.cs | Procedural | Yes (post-24) |
| 5 | ImplicitBlobHaze | ImplicitBlobHazeGenerator.cs | Procedural | Yes (post-24) |
| 6 | MarbledFlow | MarbledFlowGenerator.cs | Procedural | Yes (post-24) |
| 7 | ConcentricRipples | ConcentricRipplesGenerator.cs | Procedural | Yes (post-24) |
| 8 | NebulaGlow | NebulaGlowGenerator.cs | Procedural | Yes (post-24) |
| 9 | **OrganicCells** | OrganicCellsGenerator.cs | Procedural | **NO — dead code** |
| 10 | CrystallineFrost | CrystallineFrostGenerator.cs | Procedural | Yes (post-24) |
| 11 | BranchingTree | BranchingTreeGenerator.cs | Procedural | Yes (post-24) |
| 12 | VineTendrils | VineTendrilsGenerator.cs | Procedural | Yes (post-24) |
| 13 | RootNetwork | RootNetworkGenerator.cs | Procedural | Yes (post-24) |
| 14 | CanopyDapple | CanopyDappleGenerator.cs | Procedural | Yes (post-24) |
| 15 | FloralMandala | FloralMandalaGenerator.cs | Procedural | Yes (post-24) |

### Dead Code
- `AtmosphericWash` and `OrganicCells`: registered in generator dict but missing
  from `AllowedArchetypesOrdered` → never selected.
- `SelectArchetypeForZone()`: declared but never called anywhere.
- `DomainWarpedClouds` never reused post-24 (`AllowedArchetypesOrdered[0]`
  skipped by `1 + index`).

---

## 3. Defects Found

### D1: Archetype changes every level, not every 10
- **Root cause:** `SelectArchetypeForLevel` uses `(levelIndex - 2 + offset) % 13`
  with no grouping by zone/stage.
- **Fix:** Use `zoneIndex` (from `GetZoneIndex`) to select archetype, so all
  levels within a 10-level block share the same theme.

### D2: Levels 1-24 hardcoded to single archetype
- **Root cause:** `if (levelIndex <= 24) return DomainWarpedClouds;`
- **Fix:** Remove the hardcoded intro range, let zone logic apply from level 1.

### D3: AtmosphericWash and OrganicCells unreachable
- **Root cause:** Missing from `AllowedArchetypesOrdered` array.
- **Fix:** Add both to the array → 16 allowed archetypes.

### D4: Dead method `SelectArchetypeForZone`
- **Fix:** Remove it.

### D5: No intra-block variation
- **Root cause:** When archetype is the same for a 10-level block, the visual
  difference between levels comes only from palette/seed jitter, which is subtle.
- **Fix:** Already handled by `LevelVariant` (hue shift, saturation, value,
  gradient direction, accent strength). Verify it provides sufficient variation.

---

## 4. Validation Result (Pre-fix)

| Question | Answer |
|----------|--------|
| New theme every 10 levels? | **NO** — changes every level post-24, never pre-24 |
| ≥15 distinct themes? | **YES** — 16 exist, but only 14 reachable |
| All themes reachable? | **NO** — AtmosphericWash and OrganicCells dead |
| Repetition patterns? | Cycle of 13 repeats post level 24 |

## 5. Final Verdict (Pre-fix): **FAIL**

---

## 6. Fix Plan

1. Add `AtmosphericWash` + `OrganicCells` to `AllowedArchetypesOrdered` (16 total).
2. Rewrite `SelectArchetypeForLevel` to use zone-based selection: same archetype
   for all levels in a 10-level zone, cycling through all 16.
3. Remove hardcoded `levelIndex <= 24` guard in archetype selection.
4. Remove dead `SelectArchetypeForZone` method.
5. Update `GameController.ApplyBackgroundVariation` levels ≤ 24 special colors:
   keep per-zone color theming but derive it from the shared zone system.
6. Starfield visibility: enable stars on all cosmic/hazy/cloud-like archetypes
   (majority: 9 of 16). Disable only on clearly terrestrial/botanical/crystalline
   themes (7 of 16). Simplify from three-category system to binary.
   Stars YES: DomainWarpedClouds, CurlFlowAdvection, AtmosphericWash, NebulaGlow,
              MarbledFlow, ConcentricRipples, ImplicitBlobHaze, OrganicCells,
              FractalEscapeDensity.
   Stars NO:  BotanicalIFS, BranchingTree, RootNetwork, VineTendrils,
              CanopyDapple, FloralMandala, CrystallineFrost.
7. **Deterministic shuffle**: The 16 archetypes are shuffled per cycle using a
   seeded Fisher-Yates (Knuth) shuffle so the progression order varies by seed.
   - The shuffle is fully deterministic: given the same seed, every replay from
     level 1 produces the exact same background for every level.
   - No two consecutive zones ever share the same archetype, including at the
     boundary between cycles (e.g. zone 15 → zone 16).
8. **Intro theme (levels 1-9)**: Apply a fixed "Midnight Ocean" deep blue/indigo
   color palette for levels 1-9 (zone 0). This is a targeted color override in
   `ApplyBackgroundVariation` that sets the gradient and layer tints to specific
   deep indigo values. The archetype itself is still selected by the shuffle —
   only the colors are overridden to ensure a consistent first impression.
9. Add/update tests verifying:
   - Same archetype within each 10-level block
   - Different archetype at each 10-level boundary (up to cycle length)
   - All 16 archetypes reachable
   - Determinism for same seed
   - No consecutive zones share the same archetype (including cross-cycle)
   - Different seeds produce different orderings
   - Starfield enabled on 9 cosmic archetypes, disabled on 7 terrestrial
9. Run tests, build, push.
