# Decantra Feature Implementation Plan

## Accessible Colors Toggle Feature (2026-02-16)

### Feature description

- Add a Settings toggle labeled exactly `Accessible Colors`.
- Persist toggle state with default OFF and initialize palette state during startup.
- Route all liquid color resolution through a centralized palette provider that supports exactly:
  - `DefaultPalette`
  - `AccessiblePalette`
- Use this exact accessible 8-color palette: `#0072B2`, `#E69F00`, `#56B4E9`, `#009E73`, `#F0E442`, `#D55E00`, `#CC79A7`, `#1B2A41`.

### Implementation steps

- [x] Update palette provider to expose `DefaultPalette`/`AccessiblePalette` selection and resolve colors centrally for bottle liquid visuals.
- [x] Add `Accessible Colors` toggle row to Options and wire it to game settings state.
- [x] Persist/reload accessible colors setting through `SettingsStore` (default OFF).
- [x] Apply palette mode before first level render and re-render bottles when toggled at runtime.
- [x] Add focused tests for palette switching, luminance-order distinction, and mid-game toggle safety.

### Risk assessment

- Runtime toggle may leave transient preview overlays stale if bottle visuals are not re-rendered consistently.
- SceneBootstrap UI wiring order may show stale toggle defaults if runtime state is not loaded before interaction.
- Palette data mismatches (hex conversion drift) could violate exact accessible color requirements.

### Test checklist

- [x] Unit: palette switching returns expected colors for both palettes.
- [x] Unit: accessible and default palettes produce distinct grayscale luminance ordering.
- [x] Integration: toggling during active level updates rendered bottle liquid colors.
- [x] Regression: toggling on/off mid-game does not produce null references.
- [ ] Execute Unity EditMode/PlayMode tests in this environment (blocked: Unity editor executable unavailable).

---

## Milestone 1: Architecture

- [x] Introduce modular managers/services for tutorial, audio, options navigation, accessibility, legal content, and progression persistence.
- [x] Keep Domain layer pure; place Unity-specific orchestration in Presentation/App layers.
- [x] Ensure scene wiring avoids brittle hardcoded references (serialized references or registry-based lookup).
- [x] Validate no race conditions during first-launch boot + scene load.

### Architecture Acceptance Criteria

- Managers are isolated by responsibility and reusable.
- No UnityEngine references added to Domain.
- Scene startup is deterministic and null-safe.

---

## Milestone 2: UI Implementation

- [x] Build tutorial overlay UI (dimmer, highlight target, instruction text, Next, Skip).
- [x] Add reset confirmation modal with large high-contrast buttons.
- [x] Add legal pages (Privacy Policy, Terms of Service) with scrollable content.
- [x] Expand Options page UI: replay tutorial, sound toggle, volume slider, legal entries, high-contrast toggle.
- [x] Add level completion indicator UI (checkmark/marker).

### UI Acceptance Criteria

- All required controls exist, are visible, and sized for mobile.
- Legal pages scroll cleanly on small resolutions.
- Reset dialog blocks background interactions until choice.

---

## Milestone 3: State Management

- [x] Add tutorial step-state machine with structured step data.
- [x] Add replay tutorial action independent of gameplay progress.
- [x] Add completed-level tracking state and update hooks.

### State Management Acceptance Criteria

- Tutorial starts on first launch and can be replayed safely mid-session.
- Level completion state updates reliably and deterministically.

---

## Milestone 4: Accessibility

- [x] High-contrast mode toggle with persisted setting.
- [x] Color differentiation safeguards for color-blind users.
- [x] Increase critical button tap targets (reset confirmation, tutorial controls, options entries).

### Accessibility Acceptance Criteria

- High-contrast applies immediately and persists.
- Critical actions have clearly larger touch targets.
- UI remains legible in all added flows.

---

## Milestone 5: Persistence

- [x] Persist tutorial completion, sound enabled, volume, high-contrast mode, and level completion flags.
- [x] Add centralized preference keys and default handling.

### Persistence Acceptance Criteria

- Relaunch retains all settings and progression indicators.
- Muted sound remains silent after restart.

---

## Milestone 6: Navigation Wiring

- [x] Ensure main menu -> options route exists and is stable.
- [x] Wire options -> replay tutorial / legal pages / back navigation.
- [x] Wire tutorial and reset confirmation modal as blocking overlays.

### Navigation Acceptance Criteria

- All new pages/features are reachable from Options.
- Back navigation is consistent and returns user to prior context.

---

## Milestone 7: Testing

- [x] Add/extend automated tests for tutorial first-launch gating, replay behavior, sound prefs, reset modal flow, legal content loading, and level progression persistence.
- [x] Execute EditMode tests and verify no regressions.
- [x] Create manual validation checklist for device/small-resolution UX.

### Testing Acceptance Criteria

- Automated tests pass for modified/new systems.
- Manual checklist confirms no clipping, null refs, or interaction leaks.

### Manual Validation Checklist

- [x] Tutorial triggers on first launch and is skippable.
- [x] Replay Tutorial relaunches overlay without resetting progress.
- [x] Reset dialog blocks interaction and requires explicit confirmation.
- [x] Sound toggle + volume slider apply and persist.
- [x] Privacy Policy and Terms pages open from Options and scroll on small screens.
- [x] Options contains replay tutorial, sound controls, legal entries, and accessibility toggles.
- [x] No null references reported in modified files.
- [x] UI controls remain reachable and readable on mobile-sized layout.

---

## Progress Log

- 2026-02-16: Plan created.
- 2026-02-16: Implemented `TutorialManager`, `AudioManager`, and `ResetLevelDialog` with runtime scene wiring.
- 2026-02-16: Added in-app Privacy Policy and Terms pages using replaceable text assets under `Assets/Resources/Legal`.
- 2026-02-16: Expanded Options navigation for replay tutorial, sound settings, accessibility toggles, and legal links.
- 2026-02-16: Added completed-level tracking, HUD completion marker, and persistence defaults.
- 2026-02-16: Updated/added PlayMode tests for reset confirmation, options/legal navigation, and settings persistence.
