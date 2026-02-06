# PLANS — Options Overlay with Starfield Configuration

## Scope
Add an Options button and modal overlay that exposes user-configurable controls for the animated starfield background effect (enable/disable, density, speed, brightness). Settings apply in real-time and persist across sessions.

## Naming Decision
**"Starfield"** is used throughout the UI and codebase because:
- The shader is already named `Decantra/BackgroundStars`.
- The existing code uses `backgroundStars`, `CreateStarfield()`, `UpdateStarfieldState()`.
- "Starfield" is universally understood and matches the space theme.

## Assumptions
- The 3×3 bottle grid and all existing gameplay logic remain untouched.
- The current shader defaults (density 0.05–0.07, speed 0.12–0.30, brightness multiplier 1.6) define the "default ON" appearance.
- Settings persist in `PlayerPrefs` via the existing `SettingsStore` pattern (key prefix `decantra.starfield.*`).
- The Options button lives in `SecondaryHud` beside the existing Reset button.
- The overlay is a new full-screen UI child of `Canvas_UI`, consistent with `LevelJumpOverlay` and `RestartDialog`.

## Architecture

### Domain (pure C#, no UnityEngine)
- `StarfieldConfig` — immutable value type: `Enabled`, `Density`, `Speed`, `Brightness`, validation, defaults, clamping.
- Located at `Assets/Decantra/Domain/Model/StarfieldConfig.cs`.

### App/Services
- Extend `SettingsStore` with `LoadStarfieldConfig()` / `SaveStarfieldConfig(StarfieldConfig)`.
- Keys: `decantra.starfield.enabled`, `decantra.starfield.density`, `decantra.starfield.speed`, `decantra.starfield.brightness`.

### Presentation/Runtime
- **Shader** (`BackgroundStars.shader`): Add uniforms `_StarDensity`, `_StarSpeed`, `_StarBrightness` with default values matching current hardcoded constants.
- **SceneBootstrap.cs**: `CreateOptionsButton()`, `CreateOptionsOverlay()`, `WireOptionsOverlay()`.
- **GameController.cs**: new fields and methods to load/apply starfield config, methods called from overlay controls.

### Tests
- **EditMode** (`StarfieldConfigTests.cs`): model validation, clamping, defaults, persistence round-trip.
- **PlayMode** (`StarfieldOptionsTests.cs`): overlay open/close, toggle, slider changes, game state unaffected.

## Value Ranges
| Parameter  | Min  | Max  | Default | PlayerPrefs Key                    |
|------------|------|------|---------|------------------------------------|
| Enabled    | —    | —    | true    | `decantra.starfield.enabled`       |
| Density    | 0.01 | 1.0  | 0.35    | `decantra.starfield.density`       |
| Speed      | 0.01 | 1.0  | 0.40    | `decantra.starfield.speed`         |
| Brightness | 0.05 | 1.0  | 0.50    | `decantra.starfield.brightness`    |

- **Density 0.35** maps shader layer densities to ~[0.07, 0.06, 0.05] (current defaults).
- **Speed 0.40** maps shader layer speeds to ~[0.12, 0.21, 0.30] (current defaults).
- **Brightness 0.50** maps to multiplier ~1.6 (current default).
- All values normalized 0–1 for slider UI; shader mapping applies scaling internally.

## Tasks

### Phase 1: Domain Model + Unit Tests
- [x] 1. Create `StarfieldConfig` in Domain (pure C#, zero Unity deps).
- [x] 2. Create `StarfieldConfigTests` EditMode tests: defaults, clamping, equality.
- [x] 3. Run EditMode tests — confirm new tests pass.

### Phase 2: Persistence
- [x] 4. Extend `SettingsStore` with starfield load/save.
- [x] 5. Add persistence round-trip tests in `StarfieldConfigTests`.
- [x] 6. Run EditMode tests — confirm all pass.

### Phase 3: Shader Uniforms
- [x] 7. Add `_StarDensity`, `_StarSpeed`, `_StarBrightness` properties to `BackgroundStars.shader`.
- [x] 8. Replace hardcoded frag values with uniform-based computation.

### Phase 4: Presentation — Options Button
- [x] 9. Create `CreateOptionsButton()` in SceneBootstrap matching Reset button style.
- [x] 10. Place in SecondaryHud to the right of Reset button.
- [x] 11. Verify horizontal centering of Reset+Options button group.

### Phase 5: Presentation — Options Overlay
- [x] 12. Create `CreateOptionsOverlay()` — full-screen modal with semi-transparent dimmer.
- [x] 13. Add starfield toggle, density/speed/brightness sliders.
- [x] 14. Wire close button, back button, tap-outside dismiss.
- [x] 15. Wire controls to `GameController` methods for live preview.
- [x] 16. Apply `StarfieldConfig` to shader material properties in real-time.

### Phase 6: Integration + PlayMode Tests
- [x] 17. Create `StarfieldOptionsTests` PlayMode tests.
- [x] 18. Test overlay open/close does not affect game state.
- [x] 19. Test toggle on/off updates starfield GameObject active state.
- [x] 20. Test slider changes propagate to material uniforms.

### Phase 7: Final Verification
- [x] 21. Run full local test suite (EditMode + PlayMode).
- [x] 22. Verify no existing tests broken.
- [x] 23. Update PLANS.md with final checkmarks.

## Prohibitions Checklist
- [x] No new visual styles inconsistent with dark space theme.
- [x] No hardcoded magic numbers without justification.
- [x] No weakened, skipped, or deleted existing tests.
- [x] No gameplay logic changes.

## Final Verification
- **EditMode tests**: 210 passed, 0 failed.
- **PlayMode tests**: 47 passed, 0 failed.
- **Coverage**: 91.4% (gate: 80%).
- **New tests added**: 20 EditMode (StarfieldConfigTests) + 11 PlayMode (StarfieldOptionsTests) = 31 new tests.
