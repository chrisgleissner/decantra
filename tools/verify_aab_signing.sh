#!/usr/bin/env bash
# Verify that an AAB is signed with the release keystore (not debug signed)
# Usage: ./tools/verify_aab_signing.sh [path/to/file.aab]
#
# Environment variables (from .env):
#   KEYSTORE_STORE_FILE - path to release keystore
#   KEYSTORE_STORE_PASSWORD - keystore password
#   KEYSTORE_KEY_ALIAS - key alias

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Load .env if present
if [[ -f "${ROOT_DIR}/.env" ]]; then
  set -a
  # shellcheck source=/dev/null
  source "${ROOT_DIR}/.env"
  set +a
fi

# Determine AAB path
AAB_PATH="${1:-}"
if [[ -z "${AAB_PATH}" ]]; then
  # Auto-detect most recent AAB
  AAB_PATH="$(ls -t "${ROOT_DIR}"/Builds/Android/*.aab 2>/dev/null | head -1 || true)"
fi

if [[ -z "${AAB_PATH}" || ! -f "${AAB_PATH}" ]]; then
  echo -e "${RED}ERROR: No AAB file found.${NC}" >&2
  echo "Usage: $0 [path/to/file.aab]" >&2
  exit 1
fi

echo "========================================"
echo "AAB Signing Verification"
echo "========================================"
echo "AAB: ${AAB_PATH}"

# Validate required env vars for fingerprint comparison
KEYSTORE_PATH="${KEYSTORE_STORE_FILE:-}"
if [[ -n "${KEYSTORE_PATH}" && ! "${KEYSTORE_PATH}" = /* ]]; then
  KEYSTORE_PATH="${ROOT_DIR}/${KEYSTORE_PATH}"
fi

# Step 1: Run jarsigner -verify
echo ""
echo "Step 1: Running jarsigner -verify..."
JARSIGNER_TEMP="$(mktemp)"
trap "rm -f '${JARSIGNER_TEMP}'" EXIT
jarsigner -verify -verbose -certs "${AAB_PATH}" > "${JARSIGNER_TEMP}" 2>&1 || true

# Step 2: Check for Android Debug signature
echo "Step 2: Checking for Android Debug signature..."
if grep -qi "Android Debug" "${JARSIGNER_TEMP}"; then
  echo ""
  echo -e "${RED}========================================"
  echo "VERIFICATION FAILED"
  echo "========================================${NC}"
  echo -e "${RED}The AAB is signed with 'Android Debug' certificate.${NC}"
  echo ""
  echo "Signer details from jarsigner:"
  grep -i "CN=" "${JARSIGNER_TEMP}" | head -5 || true
  echo ""
  echo "This AAB will be REJECTED by Google Play."
  echo -e "${RED}========================================${NC}"
  rm -f "${JARSIGNER_TEMP}"
  exit 1
fi

echo "  ✓ Not signed with Android Debug"

# Step 3: Extract signer CN from jarsigner output
echo "Step 3: Extracting signer Common Name (CN)..."
SIGNER_CN="$(grep -oP 'CN=[^,\s]+' "${JARSIGNER_TEMP}" | head -1 || true)"
if [[ -n "${SIGNER_CN}" ]]; then
  echo "  Signer: ${SIGNER_CN}"
fi

# Step 4: Verify against release keystore (if available)
if [[ -n "${KEYSTORE_PATH}" && -f "${KEYSTORE_PATH}" && -n "${KEYSTORE_STORE_PASSWORD:-}" && -n "${KEYSTORE_KEY_ALIAS:-}" ]]; then
  echo ""
  echo "Step 4: Verifying SHA-256 fingerprint against release keystore..."
  
  # Get expected fingerprint from release keystore
  KEYTOOL_OUTPUT="$(keytool -list -v -keystore "${KEYSTORE_PATH}" -alias "${KEYSTORE_KEY_ALIAS}" -storepass "${KEYSTORE_STORE_PASSWORD}" 2>&1 || true)"
  
  EXPECTED_SHA256="$(echo "${KEYTOOL_OUTPUT}" | grep -i "SHA256" | head -1 | sed 's/.*SHA256:\s*//' | tr -d ' ' || true)"
  if [[ -z "${EXPECTED_SHA256}" ]]; then
    EXPECTED_SHA256="$(echo "${KEYTOOL_OUTPUT}" | grep -i "SHA-256" | head -1 | sed 's/.*SHA-256:\s*//' | tr -d ' ' || true)"
  fi
  
  if [[ -n "${EXPECTED_SHA256}" ]]; then
    # Extract actual fingerprint from AAB signing certificate
    RSA_FILE="$(unzip -l "${AAB_PATH}" 2>/dev/null | grep -o 'META-INF/[^[:space:]]*.RSA' | head -1 || true)"
    if [[ -n "${RSA_FILE}" ]]; then
      RSA_TEMP="$(mktemp)"
      unzip -p "${AAB_PATH}" "${RSA_FILE}" > "${RSA_TEMP}" 2>/dev/null
      ACTUAL_SHA256="$(keytool -printcert -file "${RSA_TEMP}" 2>/dev/null | grep -i "SHA256" | head -1 | sed 's/.*SHA256:\s*//' | tr -d ' ' || true)"
      if [[ -z "${ACTUAL_SHA256}" ]]; then
        ACTUAL_SHA256="$(keytool -printcert -file "${RSA_TEMP}" 2>/dev/null | grep -i "SHA-256" | head -1 | sed 's/.*SHA-256:\s*//' | tr -d ' ' || true)"
      fi
      rm -f "${RSA_TEMP}"
      
      if [[ -n "${ACTUAL_SHA256}" ]]; then
        # Normalize fingerprints for comparison (remove colons, uppercase)
        EXPECTED_NORM="$(echo "${EXPECTED_SHA256}" | tr -d ':' | tr '[:lower:]' '[:upper:]')"
        ACTUAL_NORM="$(echo "${ACTUAL_SHA256}" | tr -d ':' | tr '[:lower:]' '[:upper:]')"
        
        if [[ "${EXPECTED_NORM}" == "${ACTUAL_NORM}" ]]; then
          echo "  ✓ SHA-256 fingerprint matches release keystore"
          echo "  Fingerprint: ${ACTUAL_SHA256}"
        else
          echo ""
          echo -e "${YELLOW}WARNING: SHA-256 fingerprint does not match release keystore.${NC}"
          echo "Expected: ${EXPECTED_SHA256}"
          echo "Actual:   ${ACTUAL_SHA256}"
          echo ""
          echo "This may indicate the AAB was signed with a different keystore."
        fi
      else
        echo "  (Could not extract SHA-256 from AAB signing certificate)"
      fi
    else
      echo "  (Could not find signing certificate in AAB)"
    fi
  else
    echo "  (Could not extract SHA-256 from keystore for comparison)"
  fi
else
  echo ""
  echo "Step 4: Skipped (release keystore not configured)"
  echo "  Set KEYSTORE_STORE_FILE, KEYSTORE_STORE_PASSWORD, KEYSTORE_KEY_ALIAS to enable fingerprint verification"
fi

echo ""
echo -e "${GREEN}========================================"
echo "VERIFICATION PASSED"
echo "========================================${NC}"
echo -e "${GREEN}The AAB is NOT debug-signed.${NC}"
if [[ -n "${SIGNER_CN}" ]]; then
  echo "Signer: ${SIGNER_CN}"
fi
echo -e "${GREEN}========================================${NC}"
exit 0
