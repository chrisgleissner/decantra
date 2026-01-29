#!/usr/bin/env bash
set -euo pipefail

APK_PATH="${1:-$(pwd)/Builds/Android/Decantra.apk}"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

if ! command -v adb >/dev/null 2>&1; then
  echo "adb not found in PATH"
  exit 1
fi

DEVICE_ID="${DECANTRA_ANDROID_SERIAL:-${ANDROID_SERIAL:-}}"
if [ -z "${DEVICE_ID}" ]; then
  DEVICE_ID="$(adb devices | awk 'NR>1 && $2=="device" {print $1; exit}')"
fi
if [ -z "${DEVICE_ID}" ]; then
  echo "No ADB device connected"
  exit 1
fi

if [ ! -f "${APK_PATH}" ]; then
  echo "APK not found at ${APK_PATH}"
  exit 1
fi

adb -s "${DEVICE_ID}" install -r "${APK_PATH}"
echo "Installed ${APK_PATH} on ${DEVICE_ID}"
