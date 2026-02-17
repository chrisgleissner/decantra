# Decantra Audio Replacement Plan

Last updated: 2026-02-17

## Objective

Deterministically replace existing in-game SFX with the provided assets in `Assets/Sound` and implement level-scoped pour selection plus safe segmented pour playback.

## Required mappings

- `bottle-full.mp3` → bottle becomes solved (single color + full).
- `button-click.mp3` → UI button click SFX path.
- `level-complete.mp3` → level completion.
- `stage-unlocked.mp3` → entering milestone levels (10, 20, 30, ...).
- `liquid-pour.mp3` / `liquid-pour2.mp3` → per-level selected pour clip, reused for all pours in that level.

## Determinism constraints

- Single pour clip selection point at level initialization.
- No per-pour re-randomization.
- Selection is deterministic from level context (`levelIndex`, `seed`) and uses `UnityEngine.Random` only at level init.

## Segmented pour playback constraints

- Map fill delta to clip segment:
  - `startTime = previousFillRatio * clip.length`
  - `endTime = newFillRatio * clip.length`
- Play only `[startTime, endTime]`.
- Apply short fade-in/out (~10ms target, clamped 5–15ms) to prevent clicks/pops.
- No looping, no pitch modulation.
- Preserve overlap safety via pooled `AudioSource` usage.

## Execution checklist

- [x] Replace `PLANS.md` with this authoritative plan.
- [x] Identify central SFX handling and trigger points.
- [ ] Replace clip sources with `Assets/Sound` assets.
- [ ] Add deterministic level-scoped pour clip selection.
- [ ] Add segmented pour playback with safety fades.
- [ ] Wire bottle-full and stage-unlocked triggers.
- [ ] Update/adjust tests for new audio behavior.
- [ ] Run EditMode/PlayMode checks as available and verify no missing refs.
- [ ] Confirm no legacy generated SFX references remain in code.

## Progress log

### 2026-02-17 00:00 (workspace local time)

- Replaced prior CI-focused `PLANS.md` with this audio migration plan.
- Located audio flow in:
  - `Assets/Decantra/Presentation/Runtime/AudioManager.cs`
  - `Assets/Decantra/Presentation/Controller/GameController.cs`
  - `Assets/Decantra/Tests/PlayMode/AudioManagerPlayModeTests.cs`
- Confirmed required files exist under `Assets/Sound`:
  - `bottle-full.mp3`
  - `button-click.mp3`
  - `level-complete.mp3`
  - `liquid-pour.mp3`
  - `liquid-pour2.mp3`
  - `stage-unlocked.mp3`
- Next: implement runtime clip loading and deterministic per-level pour clip selection, then wire new trigger methods.