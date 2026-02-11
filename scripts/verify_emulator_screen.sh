#!/usr/bin/env bash
# Capture a screenshot from the Android emulator via adb and validate it is not
# a black screen.  Exits non-zero if the screenshot fails validation.
#
# Usage:
#   scripts/verify_emulator_screen.sh [--output-dir DIR] [--delay SECONDS]
#
# Requirements: adb, python3, Pillow (pip install Pillow)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
OUTPUT_DIR="${PROJECT_ROOT}/Builds/Android/emulator-screenshots"
DELAY=8
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"
export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --output-dir) OUTPUT_DIR="$2"; shift 2 ;;
    --delay) DELAY="$2"; shift 2 ;;
    *) echo "Unknown arg: $1" >&2; exit 1 ;;
  esac
done

mkdir -p "${OUTPUT_DIR}"

echo "==> Waiting ${DELAY}s for gameplay to render after app launch..."
sleep "${DELAY}"

SCREENSHOT_PATH="${OUTPUT_DIR}/emulator-gameplay.png"
echo "==> Capturing emulator screenshot via adb..."
adb exec-out screencap -p > "${SCREENSHOT_PATH}"

if [[ ! -s "${SCREENSHOT_PATH}" ]]; then
  echo "FAIL: Screenshot file is empty or missing." >&2
  exit 1
fi

echo "==> Validating screenshot is not a black screen..."
python3 "${SCRIPT_DIR}/validate_emulator_screenshot.py" "${SCREENSHOT_PATH}"
