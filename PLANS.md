# PLANS.md - Intro Splash + Banner Corrections

## Plan

- [x] Audit runtime UI construction and intro logic to locate banner injection and intro timing.
- [x] Implement corrected intro splash overlay (black background, centered logo, 0.5s fade-in / 1.0s hold / 0.5s fade-out).
- [x] Remove incorrect startup banner overlay paths and banner sprite usage from intro/startup code.
- [x] Replace in-game top banner with Decantra logo and compute width from the three stat buttons.
- [x] Add deterministic layout component to size and align the in-game logo to the button row bounds.
- [x] Import Decantra logo asset for the top banner (Resources/Decantra.png) with Unity meta.
- [x] Adjust top banner logo placement above the button row with gap tied to Reset button spacing and widen by 3%.
- [x] Regenerate ALL screenshots (intro + gameplay + interstitial) using the build pipeline.
- [x] Run full local test suite (EditMode + PlayMode).
- [ ] Verify CI is green for the active branch/PR.
- [ ] Document verification results (intro timing, no banner flash, logo sizing) and commands run.

## Verification Notes

- [ ] Confirm no startup banner overlay appears or flashes during scene load or intro transition.
- [ ] Confirm intro shows only centered logo on true black with exact 2.0s timing.
- [ ] Confirm intro plays only on cold start and never on level restart.
- [ ] Confirm in-game top banner uses Decantra logo and width equals the full button-row width.
- [ ] Confirm logo aspect ratio preserved and no stretching across portrait aspect ratios.
- [ ] Confirm no per-frame allocations or jitter in logo sizing.
- [ ] Confirm top logo sits above button row with 2x gap relative to button-to-reset spacing and ~3% wider than row.

## Commands / Outputs

- [x] `./build --screenshots`
	- Result: OK (tests + screenshots). Logs: Logs/test_editmode.log, Logs/test_playmode.log, Logs/test_bootstrap.log
	- Screenshots: doc/play-store-assets/screenshots/phone/screenshot-01-launch.png through screenshot-07-level-36.png
- [ ] CI status checked on the active PR (status currently pending).

## Verification Results

- Visual verification of intro timing, banner suppression, and logo alignment is pending on-device review.
- CI status is pending for PR #5 (no completed checks reported).
