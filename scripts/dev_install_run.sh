#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

"${SCRIPT_DIR}/build_android.sh"
"${SCRIPT_DIR}/install_android.sh" "${PROJECT_ROOT}/Builds/Android/Decantra.apk"

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

adb -s "${DEVICE_ID}" shell monkey -p uk.gleissner.decantra -c android.intent.category.LAUNCHER 1
echo "Launched uk.gleissner.decantra on ${DEVICE_ID}"
