# Publish Android App (Decantra)

This is the authoritative, ordered checklist for preparing and publishing Decantra to Google Play. Follow top-to-bottom.

---

## 1) Pre-flight (local)

- [ ] Confirm Unity version: [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt)
- [ ] Confirm app id + name: [ProjectSettings/ProjectSettings.asset](ProjectSettings/ProjectSettings.asset)
- [ ] Confirm Play Store assets present in: [doc/play-store-assets/](doc/play-store-assets/)
- [ ] Capture Play Store screenshots:
  - [ ] `./build --screenshots`
- [ ] Verify screenshots exist:
  - [ ] `doc/play-store-assets/screenshots/phone/screenshot-01-launch.png`
  - [ ] `doc/play-store-assets/screenshots/phone/screenshot-02-intro.png`
  - [ ] `doc/play-store-assets/screenshots/phone/screenshot-03-level-01.png`
  - [ ] `doc/play-store-assets/screenshots/phone/screenshot-04-level-12.png`
  - [ ] `doc/play-store-assets/screenshots/phone/screenshot-05-level-24.png`
  - [ ] `doc/play-store-assets/screenshots/phone/screenshot-06-interstitial.png`
  - [ ] `doc/play-store-assets/screenshots/phone/screenshot-07-level-36.png`

### Screenshot navigation (deterministic)
- Launch screen: Intro banner shown, logo visible.
- Intro screen: `IntroBanner` playback.
- Level 1: `LoadLevel(1, 10991)`.
- Level 12: `LoadLevel(12, 473921)`.
- Level 24: `LoadLevel(24, 873193)`.
- Interstitial: `LevelCompleteBanner.Show(level=2, stars=4, score=280, grade=A)`.
- Level 36: `LoadLevel(36, 192731)`.

## 2) Build & test (local)

- [ ] Run tests:
  - [ ] `./build` (runs EditMode + PlayMode + coverage and builds APK)
- [ ] Build release APK (optional):
  - [ ] `DECANTRA_BUILD_VARIANT=release ./tools/build_android.sh`
- [ ] Build release AAB (recommended for Play):
  - [ ] Unity menu: Decantra/Build/Android Release AAB

## 3) Signing keys (irreversible)

> **Warning:** App signing is irreversible once enabled. Archive keys in a secure offline vault.

- **App signing key** (managed by Google Play)
  - Generated or uploaded at first production release.
- **Upload key** (your key)
  - Used to sign AAB/APK uploads.
  - Can be rotated later if compromised.

**Rules:**
- Service account JSON authenticates CI to Play APIs. It never signs apps.
- Upload key ≠ app signing key.
- Devices trust the app signing key, not the service account.

## 4) Google Cloud service account (CI)

1. Google Cloud Console → IAM & Admin → Service Accounts.
2. Create service account for Play publishing.
3. Generate JSON key and store as **CI secret** (never commit).
4. Google Play Console → Setup → API access.
5. Link the service account and grant **Release manager** or **Admin** for the app.

Reference: https://github.com/r0adkll/upload-google-play?tab=readme-ov-file#configure-access-via-service-account

## 5) Play Console app creation (first time)

Follow [doc/play-store-listing.md](doc/play-store-listing.md) for exact listing copy, assets, and required fields.

## 6) Compliance & policy checks

- [ ] Target SDK configured: Android 34 (ProjectSettings).
- [ ] Cleartext traffic explicitly disabled in manifest.
- [ ] No unused permissions declared.
- [ ] Privacy policy provided (link in listing).
- [ ] Data safety form: mark **No data collected** (if still true).
- [ ] Content rating questionnaire completed.

## 7) Testing tracks (policy-aware)

- **Internal testing:** optional, fast validation.
- **Closed testing:**
  - Some accounts (notably new personal developer accounts) must complete closed testing before production access.
  - Current requirement (subject to change): **20 testers for 14 days** with engagement.
  - Always verify the exact requirement in Play Console → Testing → Requirements.
- **Google review time:** variable (hours to several days). New accounts may take longer.

## 8) Release

- [ ] Upload **AAB** to Production (preferred) or to a staged rollout.
- [ ] Ensure versionName is updated; CI auto-derives a monotonic Android versionCode from `GITHUB_RUN_NUMBER`/`GITHUB_RUN_ATTEMPT`.
- [ ] Review and submit for review.

## 9) CI release flow

- [ ] CI uses service account JSON for Play API access.
- [ ] CI uses upload keystore for signing.
- [ ] CI uploads AAB + artifacts to release.

---

## Notes on corrected statements from prior writeup

- Closed testing requirements are **account-specific** and **policy-dependent**. The previously stated “12 testers for 14 days” is outdated for most new personal accounts; current guidance typically requires **20 testers for 14 days**. Always confirm the exact requirement in Play Console.
- Google review duration is **not fixed**. Expect hours to days, and potentially longer for new accounts or sensitive categories.
- Service accounts authenticate API access only; they never replace signing keys.
