#!/usr/bin/env bash
set -euo pipefail

UNITY_PATH="${UNITY_PATH:-Unity}"
PROJECT_PATH="$(pwd)"
LOG_PATH="${PROJECT_PATH}/Logs/build_android.log"
BUILD_PATH="${PROJECT_PATH}/Builds/Android"
APK_PATH="${BUILD_PATH}/Decantra.apk"
AAB_PATH="${BUILD_PATH}/Decantra.aab"
UNITY_BUILD_TIMEOUT="${UNITY_BUILD_TIMEOUT:-20m}"
DECANTRA_BUILD_VARIANT="${DECANTRA_BUILD_VARIANT:-debug}"
DECANTRA_BUILD_FORMAT="${DECANTRA_BUILD_FORMAT:-apk}"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

# Load .env if present (for keystore configuration)
if [[ -f "${PROJECT_PATH}/.env" ]]; then
  echo "Loading environment from .env..."
  set -a
  # shellcheck source=/dev/null
  source "${PROJECT_PATH}/.env"
  set +a
fi

if [[ ! -x "${UNITY_PATH}" ]] && ! command -v "${UNITY_PATH}" >/dev/null 2>&1; then
  echo "Unity not found. Set UNITY_PATH to the Unity editor executable." >&2
  exit 1
fi

if ! command -v timeout >/dev/null 2>&1; then
  echo "timeout not found. Install coreutils." >&2
  exit 1
fi

mkdir -p "${PROJECT_PATH}/Logs"
mkdir -p "${BUILD_PATH}"

# Determine build method based on variant and format
if [[ "${DECANTRA_BUILD_FORMAT}" == "aab" ]]; then
  UNITY_BUILD_METHOD="Decantra.App.Editor.AndroidBuild.BuildReleaseAab"
  OUTPUT_PATH="${AAB_PATH}"
  OUTPUT_LABEL="AAB"
elif [[ "${DECANTRA_BUILD_VARIANT}" == "release" ]]; then
  UNITY_BUILD_METHOD="Decantra.App.Editor.AndroidBuild.BuildReleaseApk"
  OUTPUT_PATH="${APK_PATH}"
  OUTPUT_LABEL="APK"
else
  UNITY_BUILD_METHOD="Decantra.App.Editor.AndroidBuild.BuildDebugApk"
  OUTPUT_PATH="${APK_PATH}"
  OUTPUT_LABEL="APK"
fi

echo "========================================"
echo "Android Build Configuration"
echo "========================================"
echo "  Variant: ${DECANTRA_BUILD_VARIANT}"
echo "  Format: ${DECANTRA_BUILD_FORMAT}"
echo "  Method: ${UNITY_BUILD_METHOD}"
echo "  Output: ${OUTPUT_PATH}"
if [[ -n "${KEYSTORE_STORE_FILE:-}" ]]; then
  echo "  Keystore: ${KEYSTORE_STORE_FILE}"
  echo "  Key Alias: ${KEYSTORE_KEY_ALIAS:-<not set>}"
else
  echo "  Keystore: <not configured>"
fi
echo "========================================"

timeout "${UNITY_BUILD_TIMEOUT}" "${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_PATH}" \
  -executeMethod "${UNITY_BUILD_METHOD}" \
  -logFile "${LOG_PATH}" \
  -quit \
  -buildPath "${OUTPUT_PATH}"

BUILD_EXIT_CODE=$?

if [[ ${BUILD_EXIT_CODE} -ne 0 ]]; then
  echo "Unity build failed with exit code ${BUILD_EXIT_CODE}" >&2
  echo "Check log at: ${LOG_PATH}" >&2
  exit ${BUILD_EXIT_CODE}
fi

if [[ ! -f "${OUTPUT_PATH}" ]]; then
  echo "${OUTPUT_LABEL} not found at ${OUTPUT_PATH}" >&2
  echo "Check log at: ${LOG_PATH}" >&2
  exit 1
fi

echo "Android ${OUTPUT_LABEL} built at ${OUTPUT_PATH}"

# Post-build AAB signing verification (for release AABs only)
if [[ "${DECANTRA_BUILD_FORMAT}" == "aab" ]]; then
  echo ""
  echo "Running post-build signing verification..."
  if [[ -x "${PROJECT_PATH}/tools/verify_aab_signing.sh" ]]; then
    "${PROJECT_PATH}/tools/verify_aab_signing.sh" "${OUTPUT_PATH}"
  else
    echo "Warning: verify_aab_signing.sh not found or not executable" >&2
  fi
fi
