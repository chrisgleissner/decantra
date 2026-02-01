#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
APK_PATH="${PROJECT_ROOT}/Builds/Android/Decantra.apk"
OUTPUT_DIR="${PROJECT_ROOT}/doc/play-store-assets/screenshots/phone"
DEVICE_ID=""
SCREENSHOTS_ONLY=false
DECANTRA_SCREENSHOT_TIMEOUT="${DECANTRA_SCREENSHOT_TIMEOUT:-120}"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

usage() {
  cat <<'EOF'
Usage: tools/capture_screenshots.sh [OPTIONS]

Options:
  --apk <path>           APK to install before capture
  --device <id>          Specific adb device ID
  --output-dir <path>    Output directory for screenshots
  --screenshots-only     Pass screenshots-only flag to the app
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

adb -s "${DEVICE_ID}" install -r "${APK_PATH}"

extras=(--ez decantra_screenshots true)
if [[ "${SCREENSHOTS_ONLY}" == "true" ]]; then
  extras+=(--ez decantra_screenshots_only true)
fi

adb -s "${DEVICE_ID}" shell am start -n uk.gleissner.decantra/com.unity3d.player.UnityPlayerActivity "${extras[@]}" >/dev/null

expected=(
  "screenshot-01-launch.png"
  "screenshot-02-initial-level.png"
  "screenshot-03-interstitial.png"
  "screenshot-04-advanced-level.png"
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
  if [[ "${run_as_ok}" == "true" ]]; then
    adb -s "${DEVICE_ID}" exec-out run-as uk.gleissner.decantra cat "${remote_dir}/${file}" > "${dest}"
  else
    adb -s "${DEVICE_ID}" pull "/sdcard/Android/data/uk.gleissner.decantra/${remote_dir}/${file}" "${dest}" >/dev/null
  fi

  if [[ ! -s "${dest}" ]]; then
    echo "Screenshot missing or empty: ${dest}" >&2
    exit 1
  fi
  echo "Captured ${dest}"
  done

adb -s "${DEVICE_ID}" shell am force-stop uk.gleissner.decantra >/dev/null 2>&1 || true

printf "\nScreenshots captured to %s\n" "${OUTPUT_DIR}"
