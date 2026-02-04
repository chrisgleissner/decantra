# PLANS.md - Android AAB Release Signing Enforcement

## Problem Statement

Unity builds succeed but Google Play rejects AABs as **debug signed**. Investigation confirms:
- The AAB at `Builds/Android/decantra-0.9.0.aab` is signed with `CN=Android Debug`
- The release keystore exists at `release.keystore` with valid credentials in `.env`
- Unity silently falls back to debug signing when custom keystore configuration fails

## Root Cause Analysis

Unity's `PlayerSettings.Android` signing fields **must persist to disk** via `AssetDatabase.SaveAssets()` 
**before** `BuildPipeline.BuildPlayer` is invoked. The existing code configures signing but:
1. Does not explicitly call `AssetDatabase.SaveAssets()` after setting keystore fields
2. Does not verify that `useCustomKeystore` actually persisted to `true`
3. Verification runs but Unity has already output a debug-signed bundle

## Plan - ✅ COMPLETED

- [x] Audit current Android build pipeline code paths that configure signing and output AABs.
- [x] Identify root cause: keystore settings not persisted before build invocation.
- [x] **Fix 1**: Add explicit `AssetDatabase.SaveAssets()` immediately after setting all keystore fields.
- [x] **Fix 2**: Add post-save verification that `PlayerSettings.Android.useCustomKeystore == true`.
- [x] **Fix 3**: Add pre-build logging of all signing settings to confirm configuration.
- [x] **Fix 4**: Verify keystore is readable and contains expected alias before build.
- [x] **Fix 5**: Ensure post-build jarsigner verification fails hard on debug signing.
- [x] **Fix 6**: Create standalone shell script for AAB signing verification.
- [x] **Fix 7**: Update build scripts to support --aab flag and auto-verify.
- [x] **Build**: Run Unity batchmode AAB build with `.env` loaded.
- [x] **Verify**: Confirm AAB is signed with release keystore (CN=decantra, not CN=Android Debug).
- [x] **Document**: Record verification output in this plan.

## Implementation Details

### A. Pre-Build Guardrails (Implemented)
1. ✅ Resolve keystore path to absolute path
2. ✅ Verify keystore file exists and is readable
3. ✅ Verify keystore contains expected alias via `keytool -list`
4. ✅ Log all signing config values (except passwords)
5. ✅ Fail immediately if any validation fails

### B. Signing Configuration Persistence (Implemented)
1. ✅ Set `PlayerSettings.Android.useCustomKeystore = true`
2. ✅ Set `PlayerSettings.Android.keystoreName` (absolute path)
3. ✅ Set `PlayerSettings.Android.keystorePass`
4. ✅ Set `PlayerSettings.Android.keyaliasName`
5. ✅ Set `PlayerSettings.Android.keyaliasPass`
6. ✅ **Call `AssetDatabase.SaveAssets()` immediately after all fields set**
7. ✅ **Re-read `useCustomKeystore` to verify persistence succeeded**
8. ✅ Fail if verification shows `useCustomKeystore == false`

### C. Post-Build Verification (Implemented)
1. ✅ Run `jarsigner -verify -verbose -certs <aab>`
2. ✅ Extract RSA certificate from AAB via `unzip`
3. ✅ Extract SHA-256 fingerprint via `keytool -printcert`
4. ✅ Parse output for `CN=Android Debug` → FAIL
5. ✅ Compare fingerprint against release keystore → PASS/FAIL
6. ✅ Exit with non-zero code on any verification failure

### D. Standalone Verification Script (Implemented)
- ✅ Created `tools/verify_aab_signing.sh`
- ✅ Detects Android Debug signing
- ✅ Extracts AAB signing certificate fingerprint
- ✅ Verifies SHA-256 fingerprint matches release keystore
- ✅ Exits with code 1 on failure

### E. Build Script Updates (Implemented)
- ✅ Added `DECANTRA_BUILD_FORMAT=aab` environment variable support
- ✅ Loads `.env` automatically
- ✅ Validates keystore config before AAB builds
- ✅ Auto-runs verification after AAB build

## Environment Variables (from .env)

- `KEYSTORE_STORE_FILE` - path to keystore (relative or absolute)
- `KEYSTORE_STORE_PASSWORD` - keystore password
- `KEYSTORE_KEY_ALIAS` - key alias name
- `KEYSTORE_KEY_PASSWORD` - key password (optional, defaults to store password)

## Verification Commands

```bash
# Check keystore validity
source .env && keytool -list -v -keystore release.keystore -alias "$KEYSTORE_KEY_ALIAS" -storepass "$KEYSTORE_STORE_PASSWORD" | grep -E "Owner:|SHA256"

# Verify AAB signing (after build)
./tools/verify_aab_signing.sh Builds/Android/*.aab

# Build release AAB with verification
DECANTRA_BUILD_FORMAT=aab ./tools/build_android.sh
```

## Test Results

### Verification Script Test (Debug-signed AAB)
```
$ ./tools/verify_aab_signing.sh Builds/Android/decantra-0.9.0.aab
========================================
AAB Signing Verification
========================================
AAB: Builds/Android/decantra-0.9.0.aab

Step 1: Running jarsigner -verify...
Step 2: Checking for Android Debug signature...

========================================
VERIFICATION FAILED
========================================
The AAB is signed with 'Android Debug' certificate.

Signer details from jarsigner:
      X.509, C=US, O=Android, CN=Android Debug

This AAB will be REJECTED by Google Play.
========================================
Exit code: 1
```
✅ Correctly detects and fails on debug-signed AAB

### Unity Build Test (Release-signed AAB) - ✅ SUCCESS
```
$ DECANTRA_BUILD_FORMAT=aab ./tools/build_android.sh
========================================
AAB SIGNING VERIFICATION PASSED
========================================
  Signer: NOT Android Debug
  SHA-256 fingerprint: MATCHES release keystore
  Fingerprint: 28:17:2D:55:C4:09:01:9D:92:5E:05:E3:E0:2A:98:D2:D0:15:F6:7E:B0:89:5F:04:70:C3:9C:6D:DC:05:0B:C9
========================================
```

### Standalone Verification (Release-signed AAB)
```
$ ./tools/verify_aab_signing.sh Builds/Android/Decantra.aab
========================================
AAB Signing Verification
========================================
AAB: /home/chris/dev/decantra/Builds/Android/Decantra.aab

Step 1: Running jarsigner -verify...
Step 2: Checking for Android Debug signature...
  ✓ Not signed with Android Debug
Step 3: Extracting signer Common Name (CN)...
  Signer: CN=decantra

Step 4: Verifying SHA-256 fingerprint against release keystore...
  ✓ SHA-256 fingerprint matches release keystore
  Fingerprint: 28:17:2D:55:C4:09:01:9D:92:5E:05:E3:E0:2A:98:D2:D0:15:F6:7E:B0:89:5F:04:70:C3:9C:6D:DC:05:0B:C9

========================================
VERIFICATION PASSED
========================================
The AAB is NOT debug-signed.
Signer: CN=decantra
========================================
```

## Completion Criteria - ✅ ALL MET

- [x] AAB verification script correctly detects debug signing
- [x] Build scripts support AAB format via `DECANTRA_BUILD_FORMAT=aab`
- [x] Build fails if keystore env vars missing
- [x] Build fails if keystore file missing or unreadable
- [x] Build fails if post-build verification detects debug signing
- [x] All verification automated (no manual steps required)
- [x] AAB built with `CN=decantra` (not `CN=Android Debug`) - **VERIFIED**
- [x] SHA-256 fingerprint matches release keystore: `28:17:2D:55:C4:09:01:9D:92:5E:05:E3:E0:2A:98:D2:D0:15:F6:7E:B0:89:5F:04:70:C3:9C:6D:DC:05:0B:C9`
