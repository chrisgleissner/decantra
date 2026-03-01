# PLANS

Last updated: 2026-02-28 UTC  
Execution engineer: GitHub Copilot (Claude Opus 4.6)

---

## Active Track — Cinematic Level Transition Upgrade

### Goal

Transform the existing wave-based level transition in `LevelCompleteBanner` into a
layered, star-scaled, multi-phase cinematic sequence that scales excitement with
star count, remains performant and deterministic, and preserves accessibility.

### Invariants

- **No gameplay changes**: scoring logic, solver, star calculation unchanged.
- **Determinism**: style rotation, tier mapping, phase durations are all deterministic.
- **Cosmetic randomness only**: sparkle positions, particle jitter. Already present.
- **60 FPS**: particle counts capped per tier; no full-screen post-processing.
- **Duration cap**: total transition ≤ previous duration + 600 ms.
- **No scene reloads**.
- **Architecture boundaries**: Domain is pure C# (no UnityEngine). All transition
  code lives in Presentation layer.

### Existing System Summary

`LevelCompleteBanner.cs` already contains:
- `CelebrationProfile` struct with tier / pulseScale / emissionScale / sparkleDensity
  / freezeSeconds / shimmer / radialSweep / multiPhaseBurst.
- `BuildCelebrationProfile(stars, streakMilestone)` mapping stars → tiers 0–3.
- 4 style variants via `styleIndex = levelIndex % 4` (Burst / Spiral / Wave / Radiant).
- Animation coroutines: panel pulse, star burst, sparkles, flying stars, glisten.
- Audio layering: base jingle + repeat jingle for tier 3.
- Star icons with dynamic color/scale per brilliance.
- Streak milestone boost on tier-3 profile.

### Phase Plan

#### Phase 1 — Transition Phase Architecture (4-phase sequence)

Refactor `AnimateCelebration()` into four explicit sub-phases:

| Phase | Name | Duration | Behaviour |
|-------|------|----------|-----------|
| 1 | Completion Freeze | 0–150 ms (tier-scaled) | Vignette fade-in via dimmer alpha bump. Already uses `FreezeSeconds`. |
| 2 | Wave Expansion | Existing enter/burst timing | Star burst amplitude & brightness scale with tier. Wave thickness increase for ≥4★. Gold tint for 5★. |
| 3 | Star Reveal | Sequential pop | Stars animate upward from burst center with 60 ms stagger. Final star (5★) gets brighter flash + extra sparkle burst. |
| 4 | Resolution Sweep | Light sweep + UI pulse | Glisten sweep; panel scale pulse; fade to next level. |

Implementation: rewrite `AnimateCelebration` to call sub-phase coroutines
sequentially. Keep existing animation helpers.

#### Phase 2 — Wave / Burst Scaling Logic

Enhance `AnimateStarBurst` and introduce emission ramp:
- Burst max scale / alpha already scale with star count; add **wave thickness**
  parameter (burst rect height scales 1.0× → 1.3× for tier 3).
- Add depth shadow under burst (subtle darkened copy behind starBurst, offset -4 px).
- 5★ gold rim: tint starBurst color toward gold `(1, 0.92, 0.55)` for tier 3.
- Crest distortion: slight sine wobble on burst Y position for tiers ≥ 2.

#### Phase 3 — Star Material / Icon Upgrade

Replace static `ApplyStarIcons` with sequential reveal:
- Each star icon pops in with 60 ms delay, scale from 0.3 → glowScale with
  overshoot easing.
- Per-icon emission: color.a animates 0 → 1 with brief overshoot.
- 5★ final star: extra bright flash (scale 1.25× of normal, brief white overlay).
- Add shimmer animation: per-star continuous micro-rotation ±3° for 5★ only.

#### Phase 4 — Style Overlay Enhancements

Existing 4 style variants for flying stars. Enhance each:

| Index | Name | Enhancement |
|-------|------|-------------|
| 0 | Burst | Add subtle radial lines behind burst (via thin elongated sparkle images) |
| 1 | Spiral | Increase rotation factor; add dust trail alpha |
| 2 | Wave | Add horizontal sweep band (reuse glisten, dual pass) |
| 3 | Radiant | Add vertical bloom (upward alpha gradient on glisten) |

Tier modifies intensity of each overlay via `EmissionScale`.

#### Phase 5 — Audio Layering Refinement

Current: base jingle + 2× repeat for tier 3.
Enhanced layering:
- Tier 0–1: single jingle (unchanged).
- Tier 1 (2–3★): add soft second jingle at lower gain after 120 ms.
- Tier 2 (4★): add percussion accent (third jingle at 80 ms offset, 0.35 gain).
- Tier 3 (5★): full layered cascade — 3 jingles + sparkle chime tail at end.

#### Phase 6 — Perfect Streak Integration

Already handled via `streakMilestone` boost in `BuildCelebrationProfile`.
Enhance:
- When `streakMilestone > 0`: add extra sparkle ring (reuse sparkle pool, wider radius).
- Brief outer glow on star burst (extend burst alpha duration by 80 ms max).
- No negative animations for broken streak.

### Tier Scaling Matrix

| Stars | Tier | Freeze | Pulse | Emission | Sparkle Density | Shimmer | Radial | MultiBurst | Wave Gold |
|-------|------|--------|-------|----------|-----------------|---------|--------|------------|-----------|
| 0–1 | 0 | 0 ms | 1.01 | 1.0 | 0.45 | no | no | no | no |
| 2–3 | 1 | 0 ms | 1.02 | 1.1 | 0.70 | yes | no | no | no |
| 4 | 2 | 0 ms | 1.03 | 1.25 | 1.0 | yes | yes | no | no |
| 5 | 3 | 150 ms | 1.05 | 1.5 | 1.25 | yes | yes | yes | yes |

### Performance Safeguards

- Sparkle count: max 12 (existing cap). Tier 0 uses ~2–5.
- Flying star count: max 8 (existing cap). Tier 0 uses ~2–3.
- No new GameObjects allocated per transition (pool reused).
- No full-screen blur beyond existing low-res downscale.
- No real-time post-processing effects.
- Material property reuse (no material duplication).

### Test Checklist

- [x] `StarTierMapping_IsCorrect` — existing, covers 0–5 stars → tiers 0–3.
- [x] `StyleRotation_IsDeterministic` — existing, covers `levelIndex % 4`.
- [ ] `PhaseOrder_IsCorrect` — new: verify 4 phases execute in order.
- [x] `StreakMilestone_BoostsCelebration` — new: verify milestone > 0 increases profile values.
- [x] `StarReveal_SequentialTiming` — new: verify 60 ms stagger per star.
- [x] `GoldTint_OnlyForTier3` — new: verify gold tint applied only at 5★.
- [x] `WaveThickness_ScalesWithTier` — new: verify burst height scaling.
- [x] `ParticleCounts_RespectCaps` — new: verify sparkle/flying star count ≤ max.

### Files Modified

- `Assets/Decantra/Presentation/Runtime/LevelCompleteBanner.cs` — main transition logic.
- `Assets/Decantra/Tests/PlayMode/LevelCompleteBannerMappingTests.cs` — additional tests.
- `PLANS.md` — this file.

### Definition of Done

- [ ] 4-phase transition architecture implemented.
- [ ] Star tier excitement scaling verified.
- [ ] Sequential star reveal with stagger.
- [ ] Wave thickness / gold tint for 5★.
- [ ] Style overlay enhancements.
- [ ] Audio layering per tier.
- [ ] Perfect streak extra sparkle ring.
- [ ] All tests green.
- [ ] Manual verification: 1★ minimal, 3★ energetic, 5★ grand.

---

## Completed Track — iOS Production Issues

### Summary (completed 2026-02-28)

- iOS display name fixed: `Decantra` enforced in build + plist.
- iOS audio fixed: `AVAudioSession` configured at startup/focus/unpause.
- EditMode guardrail tests added.
- Remaining: device verification (outside Linux workspace scope).
