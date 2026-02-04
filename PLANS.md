# PLANS.md - Android AAB Release Signing Enforcement

## Plan

- [x] Audit current Android build pipeline code paths that configure signing and output AABs.
- [x] Implement explicit release signing configuration sourced from environment variables.
- [x] Add mandatory pre-build diagnostics and fail-fast validation for signing inputs.
- [x] Add deterministic post-build AAB signing verification with `jarsigner` and fail on debug signing.
- [ ] Run local build/verification workflow and capture results.
- [ ] Document verification outputs and completion status in this plan.

## Verification Notes

- [ ] Confirm batchmode build sets `useCustomKeystore = true` and all signing fields.
- [ ] Confirm build fails if keystore path, passwords, or alias are missing/invalid.
- [ ] Confirm log includes "ANDROID RELEASE SIGNING CONFIGURED" marker before build.
- [ ] Confirm AAB verification rejects debug signing and validates expected signer.
- [ ] Confirm local developer run performs the same verification automatically.

## Commands / Outputs

- [x] `keytool -list -v -keystore release.keystore -alias $KEYSTORE_KEY_ALIAS -storepass $KEYSTORE_STORE_PASSWORD | grep -E "Owner:|Issuer:|SHA-256|SHA256"`
	- Result: Owner/Issuer show CN=decantra; SHA256 fingerprint is 28:17:2D:55:C4:09:01:9D:92:5E:05:E3:E0:2A:98:D2:D0:15:F6:7E:B0:89:5F:04:70:C3:9C:6D:DC:05:0B:C9
- [x] `jarsigner -verify -verbose -certs Builds/Android/decantra-0.9.0.aab | grep -E "Owner:|Issuer:|SHA-256|SHA256|Android Debug"`
	- Result: AAB is signed by Android Debug (verification failed; release signing not applied).
- [ ] Unity batchmode build for Release AAB executed with new signing enforcement (Unity not available on PATH in this environment).
