# PLANS — Release Integrity Investigation & Corrective Release (2026-02-15)

## Mission

Conclusively diagnose and fix the mismatch where the officially deployed/tagged artifact (`1.0.1`, installed `versionCode=4430`) still shows first-interaction row shift, while a newer main-built APK appears fixed.

## Assumptions (validated against repo as work proceeds)

1. Unity project + Android CI pipeline exist in this repo.
2. Release path is GitHub Actions -> AAB upload to Play internal + GitHub release artifact.
3. Shift can be deterministically detected via row position metrics and screenshot deltas.
4. Play Console is not directly available from this environment; operator checklist is required for upload verification.

## Non-negotiable constraints

- No partial "likely fixed" conclusion.
- No test weakening.
- Preserve historical tags; prefer corrective tag (`1.0.2`) over moving `1.0.1`.
- Every conclusion must be backed by reproducible commands + artifacts.

## Execution Contract

### 1) Reproduce + characterize
- [ ] Capture evidence for official broken behavior (before/after first interaction) from installed official build path.
- [ ] Record measured dy for rows 1 and 2 and store screenshots + JSON report.

**Done criteria:** a reproducible artifact set exists with `S0`, `S1`, `diff`, and metric JSON showing pass/fail.

### 2) Establish provenance
- [ ] Resolve `1.0.1 -> commit SHA` and record commit metadata.
- [ ] Record current main commit SHA and relation to release tag.
- [ ] Capture build inputs used by release path (Unity version, player settings, build methods, workflow logic).

**Done criteria:** report maps tag/commit/build-inputs and identifies whether fix commits are included in the official tag.

### 3) Determine mismatch root cause
- [ ] Evaluate tag mismatch vs config drift vs stale artifact.
- [ ] Produce evidence-backed root-cause statement.

**Done criteria:** exactly one primary root cause (or ranked causes) is documented with evidence.

### 4) Implement corrective changes
- [ ] Ensure first-interaction shift fix is in canonical release path.
- [ ] Harden pipeline against dirty-tree / wrong-commit release ambiguity.
- [ ] Embed non-sensitive build provenance in app payload.

**Done criteria:** code + workflow changes guarantee reproducible release inputs and runtime provenance visibility.

### 5) Add automated regression verification
- [ ] Add deterministic regression test for row 1+2 dy on first interaction.
- [ ] Emit `S0.png`, `S1.png`, `diff.png`, and JSON metric report.
- [ ] Run test locally and in CI; publish artifacts.

**Done criteria:** failing behavior is caught automatically; corrected behavior passes.

### 6) Release artifact verification
- [ ] Generate canonical release artifact (APK/AAB path).
- [ ] Compute SHA-256 hashes and store machine-readable hash manifest.
- [ ] Verify artifact provenance metadata (commit SHA, timestamp, Unity version, pipeline ID).
- [ ] If Play upload is external, provide exact operator checklist including expected versionCode.

**Done criteria:** verified artifact + provenance + operator steps are reproducible and auditable.

### 7) Documentation deliverables
- [ ] Check in Release Provenance Report under `doc/`.
- [ ] Include commands, outputs, hashes, and final conclusion.

**Done criteria:** report is complete, reproducible, and suitable for release sign-off.

## Current findings (in-progress)

- Confirmed: `1.0.1` tag resolves to `cd6d2a4`.
- Confirmed: shift-stabilization commits are post-`1.0.1` on main (starting at `3a19483` and follow-ups).
- Working hypothesis: official deployed `1.0.1` artifact predates final stabilization fixes; latest-main APK includes them.

## Verification ledger (to be updated live)

- [ ] Local deterministic regression test run complete.
- [ ] CI run includes regression artifacts.
- [ ] Artifact hash manifest generated.
- [ ] Release provenance report committed.
