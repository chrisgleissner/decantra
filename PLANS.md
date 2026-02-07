# PLANS — CI Enforcement and RC Automation

## 1. G1 Codecov Enforcement
- [ ] Update test job to verify coverage artifacts exist and fail clearly if missing.
- [ ] Add explicit retry with backoff around Codecov upload using codecov/codecov-action@v5.
- [ ] Make Codecov upload mandatory and fail the job if all attempts fail.
- [ ] Ensure build-android only runs after confirmed Codecov success (test job fails otherwise).

## 2. G2 Google Play Upload Enforcement
- [ ] Hard-set Play package name to uk.gleissner.decantra.
- [ ] Fail release job if PLAY_SERVICE_ACCOUNT_JSON is missing or empty.
- [ ] Make Play upload mandatory with explicit retry + backoff and validation.
- [ ] Reorder steps so GitHub Release publishes only after Play upload success.
- [ ] Make track and status explicit and deterministic (internal, draft).

## 3. G3 RC Tagging and Validation
- [ ] Add workflow_dispatch inputs and job to create next RC tag safely using git + gh.
- [ ] Ensure tag triggers include *.*.* and *.*.*-rc*.
- [ ] Add gh-based gate on final tags that requires a successful RC run.
- [ ] Keep RC behavior non-production while still uploading to Play internal track.

## 4. G4 Autonomous git + gh Operations
- [ ] Create working branch, apply edits, commit with clear message, and push via git.
- [ ] Use gh to validate auth status and query workflow runs.
- [ ] Create and push an RC tag via git and observe the run via gh.

## 5. G5 Deterministic, Self-Enforcing CI
- [ ] Remove skip logic and continue-on-error from mandatory steps.
- [ ] Add clear logs for retries and backoff timings.
- [ ] Verify CI green for RC run and enforce final-tag gate failure when RC missing.
