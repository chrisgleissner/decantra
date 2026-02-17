# Decantra Pour SFX Short-Clip Alignment Plan

Last updated: 2026-02-17

## Goal

Align pour SFX playback to the short pour assets and required behavior:

- Use only `liquid-pour-short.mp3` and `liquid-pour2-short.mp3` for pour playback.
- Tie audio window exactly to pour window timing.
- Select segment by fill interval.
- Enforce minimum audible duration rule (0.4s) with clamped expansion.
- Apply deterministic pop-safe fades.
- Keep changes minimal and localized.

## Current-state findings checklist

- [x] Located primary pour SFX path in `Assets/Decantra/Presentation/Runtime/AudioManager.cs` and trigger path in `Assets/Decantra/Presentation/Controller/GameController.cs`.
- [x] Confirmed pour clips currently loaded via long assets:
  - `Resources.Load<AudioClip>("Sound/liquid-pour")`
  - `Resources.Load<AudioClip>("Sound/liquid-pour2")`
- [x] Confirmed pour duration currently uses gameplay formula `Mathf.Max(0.2f, 0.12f * poured)`.
- [x] Confirmed current segment playback stop/fade control is coroutine + `Time.unscaledDeltaTime` (frame-driven).
- [x] Confirmed no minimum 0.4s expansion logic exists.

## Concrete tasks

1. Replace pour clip loads with short variants only.
2. Implement deterministic segment window computation:
   - start/end from fill fractions
   - min duration expansion to 0.4s
   - clamped shift preserving 0.4s when possible
3. Replace frame-driven fade/stop with deterministic sample-domain envelope in the played segment.
4. Expose a single duration calculator in `AudioManager` and use it in `GameController` pour animation timing.
5. Add optional guarded diagnostics for pour timing/segment values.
6. Update tests for short-clip loading expectations and pool stability.
7. Verify with searches that long pour clips are not referenced for pour playback.

## Risks and mitigations

- Risk: short clips import/runtime length differs from expected 0.8s.
  - Mitigation: compute duration from runtime `clip.length`; error+fallback when `< 0.4s`.
- Risk: per-pour clip slicing causes allocations.
  - Mitigation: keep existing fixed source pool and keep helper localized; avoid system-wide refactor.
- Risk: audio/animation desync.
  - Mitigation: `GameController` uses `AudioManager` duration calculator for pour window duration.

## Verification steps

- [x] Compile/error check for touched files.
- [x] EditMode tests (existing workspace task).
- [x] Search confirms no long pour clip references used for playback path.
- [ ] Manual behavior checklist prepared:
  - [ ] 0%→50% maps first half acoustic segment.
  - [ ] 50%→100% maps last half acoustic segment.
  - [ ] tiny deltas enforce >=0.4s window.
  - [ ] no pops/clicks on start/stop.
  - [ ] start/end aligns with visible pour window.

## Progress log

### 2026-02-17

- Replaced prior plan with this task-specific authoritative plan.
- Completed code discovery and requirement verification.
- Implemented short-clip switch in `AudioManager`:
  - `Sound/liquid-pour-short`
  - `Sound/liquid-pour2-short`
- Implemented deterministic segment bounds with 0.4s minimum duration expansion and clamped shifting.
- Implemented deterministic pop-safe fades in sample domain for pour segments (8ms in / 20ms out).
- Aligned `GameController` pour animation duration with `AudioManager.CalculatePourWindowDuration(...)` so audio and pour window use the same duration source.
- Added optional pour diagnostics (guarded flags) in `GameController` + `AudioManager`.
- Updated PlayMode audio tests for short clips and minimum duration behavior.
- Verification:
  - Code errors check: clean for touched files.
  - Android build: `./scripts/build_android.sh` succeeds after lock cleanup.
  - Search confirms no `liquid-pour` / `liquid-pour2` long-asset references in runtime pour playback path.
