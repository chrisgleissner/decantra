#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
APK_PATH="${PROJECT_ROOT}/Builds/Android/Decantra.apk"
OUTPUT_DIR="${PROJECT_ROOT}/doc/play-store-assets/screenshots/phone"
DEVICE_ID=""
SCREENSHOTS_ONLY=false
CAPTURE_MOTION=false
DECANTRA_SCREENSHOT_TIMEOUT="${DECANTRA_SCREENSHOT_TIMEOUT:-120}"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

usage() {
  cat <<'EOF'
Usage: scripts/capture_screenshots.sh [OPTIONS]

Options:
  --apk <path>           APK to install before capture
  --device <id>          Specific adb device ID
  --output-dir <path>    Output directory for screenshots
  --screenshots-only     Pass screenshots-only flag to the app
  --motion-capture       Capture starfield motion frames after screenshots
  --timeout <seconds>    Capture timeout (default: 120)
  -h, --help             Show help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --apk)
      APK_PATH="${2:-}"
      shift 2
      ;;
    --device)
      DEVICE_ID="${2:-}"
      shift 2
      ;;
    --output-dir)
      OUTPUT_DIR="${2:-}"
      shift 2
      ;;
    --screenshots-only)
      SCREENSHOTS_ONLY=true
      shift
      ;;
    --motion-capture)
      CAPTURE_MOTION=true
      shift
      ;;
    --timeout)
      DECANTRA_SCREENSHOT_TIMEOUT="${2:-120}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if ! command -v adb >/dev/null 2>&1; then
  echo "adb not found in PATH" >&2
  exit 1
fi

APK_PATH="${APK_PATH:-${PROJECT_ROOT}/Builds/Android/Decantra.apk}"
if [[ ! -f "${APK_PATH}" ]]; then
  echo "APK not found: ${APK_PATH}" >&2
  exit 1
fi

PACKAGE_NAME="uk.gleissner.decantra"
ACTIVITY_NAME="com.unity3d.player.UnityPlayerActivity"
if command -v aapt >/dev/null 2>&1; then
  apk_info="$(aapt dump badging "${APK_PATH}" 2>/dev/null || true)"
  if [[ -n "${apk_info}" ]]; then
    pkg_line="$(printf "%s" "${apk_info}" | grep -m1 "^package: " || true)"
    if [[ -n "${pkg_line}" ]]; then
      PACKAGE_NAME="$(printf "%s" "${pkg_line}" | sed -n "s/^package: name='\([^']\+\)'.*/\1/p")"
    fi
    activity_line="$(printf "%s" "${apk_info}" | grep -m1 "launchable-activity" || true)"
    if [[ -n "${activity_line}" ]]; then
      ACTIVITY_NAME="$(printf "%s" "${activity_line}" | sed -n "s/.*name='\([^']\+\)'.*/\1/p")"
    fi
  fi
fi

if [[ -z "${DEVICE_ID}" ]]; then
  DEVICE_ID="${DECANTRA_ANDROID_SERIAL:-${ANDROID_SERIAL:-}}"
fi
if [[ -z "${DEVICE_ID}" ]]; then
  DEVICE_ID="$(adb devices | awk 'NR>1 && $2=="device" {print $1; exit}')"
fi
if [[ -z "${DEVICE_ID}" ]]; then
  echo "No adb devices found. Connect a device or pass --device <id>." >&2
  exit 1
fi

mkdir -p "${OUTPUT_DIR}"
rm -f "${OUTPUT_DIR}"/*.png || true

adb -s "${DEVICE_ID}" shell pm clear "${PACKAGE_NAME}" >/dev/null 2>&1 || true
adb -s "${DEVICE_ID}" shell rm -rf "/sdcard/Android/data/${PACKAGE_NAME}/files/DecantraScreenshots" >/dev/null 2>&1 || true
adb -s "${DEVICE_ID}" shell am force-stop "${PACKAGE_NAME}" >/dev/null 2>&1 || true

install_output=""
install_status=0
if ! install_output=$(adb -s "${DEVICE_ID}" install -r "${APK_PATH}" 2>&1); then
  install_status=$?
fi

if [[ ${install_status} -ne 0 ]]; then
  if echo "${install_output}" | grep -q "INSTALL_FAILED_UPDATE_INCOMPATIBLE"; then
    echo "Install failed due to signature mismatch. Uninstalling ${PACKAGE_NAME} and retrying..." >&2
    adb -s "${DEVICE_ID}" uninstall "${PACKAGE_NAME}" >/dev/null 2>&1 || true
    adb -s "${DEVICE_ID}" install -r "${APK_PATH}"
  else
    echo "APK install failed: ${install_output}" >&2
    exit ${install_status}
  fi
fi
adb -s "${DEVICE_ID}" shell pm enable "${PACKAGE_NAME}" >/dev/null 2>&1 || true

extras=(--ez decantra_screenshots true)
if [[ "${SCREENSHOTS_ONLY}" == "true" ]]; then
  extras+=(--ez decantra_screenshots_only true)
fi

adb -s "${DEVICE_ID}" shell am start -S -W -n "${PACKAGE_NAME}/${ACTIVITY_NAME}" "${extras[@]}" >/dev/null

expected=(
  "startup_fade_in_midpoint.png"
  "help_overlay.png"
  "options_panel_typography.png"
  "screenshot-01-launch.png"
  "screenshot-02-intro.png"
  "screenshot-03-level-01.png"
  "screenshot-08-level-10.png"
  "screenshot-04-level-12.png"
  "screenshot-09-level-20.png"
  "screenshot-05-level-24.png"
  "screenshot-06-interstitial.png"
  "screenshot-07-level-36.png"
  "screenshot-10-options.png"
)

remote_dir="files/DecantraScreenshots"
complete_marker="${remote_dir}/capture.complete"

start_time=$(date +%s)
run_as_ok=false

if adb -s "${DEVICE_ID}" shell run-as uk.gleissner.decantra ls "${remote_dir}" >/dev/null 2>&1; then
  run_as_ok=true
fi

while true; do
  if [[ "${run_as_ok}" == "true" ]]; then
    if adb -s "${DEVICE_ID}" shell run-as uk.gleissner.decantra ls "${complete_marker}" >/dev/null 2>&1; then
      break
    fi
  else
    if adb -s "${DEVICE_ID}" shell ls "/sdcard/Android/data/uk.gleissner.decantra/${complete_marker}" >/dev/null 2>&1; then
      break
    fi
  fi

  now=$(date +%s)
  if (( now - start_time > DECANTRA_SCREENSHOT_TIMEOUT )); then
    echo "Timed out waiting for screenshot capture." >&2
    exit 1
  fi
  sleep 1
  done

for file in "${expected[@]}"; do
  dest="${OUTPUT_DIR}/${file}"
  pulled=false
  for attempt in 1 2 3; do
    adb -s "${DEVICE_ID}" pull "/sdcard/Android/data/${PACKAGE_NAME}/${remote_dir}/${file}" "${dest}" >/dev/null && pulled=true || true
    if [[ -s "${dest}" ]]; then
      pulled=true
      break
    fi
    sleep 0.5
  done

  if [[ "${pulled}" != "true" || ! -s "${dest}" ]]; then
    echo "Screenshot missing or empty: ${dest}" >&2
    exit 1
  fi
  echo "Captured ${dest}"
  done

adb -s "${DEVICE_ID}" shell am force-stop uk.gleissner.decantra >/dev/null 2>&1 || true

printf "\nScreenshots captured to %s\n" "${OUTPUT_DIR}"

if [[ "${CAPTURE_MOTION}" == "true" ]]; then
  echo "\n==> Capturing starfield motion frames"
  motion_extras=(--ez decantra_motion_capture true)

  adb -s "${DEVICE_ID}" shell am force-stop "${PACKAGE_NAME}" >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell am start -S -W -n "${PACKAGE_NAME}/${ACTIVITY_NAME}" "${motion_extras[@]}" >/dev/null

  motion_remote_dir="/sdcard/Android/data/${PACKAGE_NAME}/files/DecantraScreenshots/motion"
  motion_complete="${motion_remote_dir}/motion.complete"
  start_time=$(date +%s)
  while true; do
    if adb -s "${DEVICE_ID}" shell ls "${motion_complete}" >/dev/null 2>&1; then
      break
    fi
    now=$(date +%s)
    if (( now - start_time > DECANTRA_SCREENSHOT_TIMEOUT )); then
      echo "Timed out waiting for motion capture." >&2
      exit 1
    fi
    sleep 1
  done

  mkdir -p "${OUTPUT_DIR}/motion"
  adb -s "${DEVICE_ID}" pull "${motion_remote_dir}" "${OUTPUT_DIR}" >/dev/null
  adb -s "${DEVICE_ID}" shell am force-stop "${PACKAGE_NAME}" >/dev/null 2>&1 || true

  if ! ls "${OUTPUT_DIR}/motion"/frame-*.png >/dev/null 2>&1; then
    echo "Motion frames missing in ${OUTPUT_DIR}/motion" >&2
    exit 1
  fi
  echo "Motion frames captured to ${OUTPUT_DIR}/motion"
fi
