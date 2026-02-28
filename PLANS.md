# PLANS

Last updated: 2026-02-28 UTC  
Execution engineer: GitHub Copilot

## Scope (A–E)

A. WebGL fullscreen startup and landscape layout architecture  
B. GitHub Pages deterministic tag deployment (serve `1.4.1` and future tags)  
C. Unified Build UTC metadata injection for WebGL/Android/About  
D. Modern tutorial spotlight system replacing yellow oval  
E. Automated screenshots for every tutorial step with deterministic naming/location

## Invariants

- Domain gameplay rules and determinism remain unchanged.
- Level generation determinism remains unchanged.
- No increase beyond 3x3 grid and max 9 bottles.
- Existing modal flow and options overlays remain functional.
- Existing screenshot automation remains backward-compatible.

## Canvas Architecture Invariants

- Background always stretches to full viewport in all aspect ratios.
- Gameplay uses a centered portrait container in landscape displays.
- `CanvasScaler` uses `ScaleWithScreenSize` + `MatchWidthOrHeight` with `matchWidthOrHeight=1`.
- Gameplay container maintains portrait aspect ratio and full height; width derives from aspect ratio.
- Bottle grid is laid out inside gameplay container; no horizontal stretch and no tiny bottle rendering.

## Deployment Invariants

Decision: **Tags are source of truth for GitHub Pages production WebGL deployments**.

- Deploy job builds from exact git ref (tag push ref or manual `deploy_ref` input).
- Production deploy only runs for tag refs (or manually specified ref), never arbitrary branch heads.
- Pages artifact is rebuilt from scratch each run (`Builds/Pages` recreated).
- WebGL page contains explicit version marker (`tag`, optional short SHA) for live verification.
- Site content is deterministic for same tag/ref.

## Build Metadata Invariants

- Single generated source of truth: `BuildInfo` C# constants.
- `BuildUtc` is always ISO8601 UTC (`yyyy-MM-ddTHH:mm:ssZ` compatible).
- `Version` resolves from CI tag/ref/env or falls back to bundle version for local builds.
- WebGL and Android build entry points both call the same metadata generator.
- About section reads `BuildInfo` and never returns `unknown` for build timestamp.

## Tutorial Rendering Invariants

- Yellow oval highlight removed.
- Tutorial overlay hierarchy: `DimLayer` + shader-based inverted `FocusMask` + focused target enhancement.
- Works with any `RectTransform` target (bottles, HUD, logo, stars, generic controls).
- Transitions animate smoothly between steps.
- Raycasts blocked globally except focused target and tutorial controls.
- Rendering is resolution-independent and WebGL compatible.

## Screenshot Capture Invariants

- Each tutorial step captured after spotlight position/animation settle.
- Deterministic path: `.../DecantraScreenshots/Tutorial/<version>/`.
- File naming format:
  - `tutorial_step_<index>_<targetName>_<resolution>.png`
- Variants captured:
  - Portrait
  - Landscape
  - Fullscreen-WebGL-like resolution
- Summary log includes: filename, resolution, target, UTC timestamp.

## Implementation Steps

1. Add WebGL template with fullscreen startup logic, blocking click-to-start fallback, and fullscreen change resize handling.
2. Wire WebGL build pipeline to use template and embed build/version marker metadata.
3. Refactor runtime canvas/gameplay container setup to enforce centered portrait gameplay in landscape.
4. Add shared build metadata generator (`BuildInfo`) and replace per-platform divergence.
5. Update About footer to consume generated metadata (no `Build UTC unknown`).
6. Replace tutorial yellow ring with spotlight mask + target enhancement + input gating.
7. Extend tutorial manager automation API to expose step metadata/target naming for screenshots.
8. Extend runtime screenshot automation to capture every tutorial step in all required resolutions and emit capture summary log.
9. Update GitHub Pages workflow to deterministic tag-ref deployment and clean artifact publication.
10. Validate with PlayMode/EditMode tests and workflow-level sanity checks.

## Risk Register

- **R1 WebGL fullscreen policy variance** (Chrome/Firefox/user gesture): mitigated by compliant overlay click fallback and retry on first interaction.
- **R2 UI regression in existing tests**: mitigated by preserving object names and tutorial manager serialized fields where possible.
- **R3 Shader incompatibility on WebGL**: mitigated by simple UI shader path and fallback behavior.
- **R4 Tag deployment drift**: mitigated by explicit `ref` checkout and deploy gating.
- **R5 Screenshot timing flakiness**: mitigated by settle waits + deterministic frame boundaries.

## Test Plan

### A. Fullscreen
- Verify auto fullscreen attempt on desktop on startup.
- Verify click-to-start overlay appears when blocked, and enters fullscreen after first interaction.
- Verify `fullscreenchange` triggers resize handling.
- Verify ESC exit + manual fullscreen button continue to work.

### B. Landscape Layout
- Validate on 4K landscape, 1080p landscape, portrait monitor.
- Validate browser resize + fullscreen toggle transitions.
- Confirm background fills full viewport and gameplay remains portrait-centered.
- Confirm bottles retain practical visible size.

### C. Deployment
- Simulate/ref-check workflow logic for tags and `workflow_dispatch` with `deploy_ref=1.4.1`.
- Confirm version marker is present in built WebGL page.
- Confirm artifact publication replaces prior pages content.

### D. Build Metadata
- Verify generated `BuildInfo` values in local build.
- Verify About section displays non-unknown Build UTC in Editor/PlayMode.
- Verify WebGL build output carries version/build values.

### E. Tutorial Visuals + Screenshots
- Run tutorial capture automation and verify one image per step per resolution variant.
- Inspect generated spotlight style consistency for bottles and HUD targets.
- Validate summary log correctness.

## Rollback Strategy

- Revert WebGL template usage and fallback to Unity default template if fullscreen startup causes regressions.
- Revert tutorial overlay to previous ring-based implementation by restoring `TutorialManager` and `SceneBootstrap` overlay creation path.
- Revert workflow deployment strategy to prior main-based deployment if release process needs emergency continuity.
- Keep metadata generator additive so Android build can temporarily continue with old resource-based timestamp if required.

## Visual Review Checklist

- [ ] Desktop startup has no broken first-frame layout.
- [ ] Fullscreen fallback overlay text is clear and non-intrusive.
- [ ] Landscape mode shows full-screen background and centered portrait gameplay column.
- [ ] Bottle scale remains comfortable/readable in landscape.
- [ ] Spotlight edge is soft, modern, and non-cartoonish.
- [ ] Focused target pulse/glow is subtle (premium/minimal).
- [ ] Non-focused controls are correctly blocked.
- [ ] Tutorial instruction panel remains readable at all tested resolutions.
- [ ] All tutorial step screenshots are present with required naming.
- [ ] Summary capture log contains step index, target, resolution, and timestamp.
