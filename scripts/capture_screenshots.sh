#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
APK_PATH="${PROJECT_ROOT}/Builds/Android/Decantra.apk"
OUTPUT_DIR="${PROJECT_ROOT}/doc/play-store-assets/screenshots/phone"
DEVICE_ID=""
SCREENSHOTS_ONLY=false
CAPTURE_MOTION=false
DECANTRA_SCREENSHOT_TIMEOUT="${DECANTRA_SCREENSHOT_TIMEOUT:-240}"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"
SINK_COUNT_CSV="${PROJECT_ROOT}/doc/sink-bottles-levels-1-1000.csv"

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
  --timeout <seconds>    Capture timeout (default: 240)
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
  DEVICE_ID="$(adb devices | awk 'NR>1 && $2=="device" && $1 ~ /^emulator/ {print $1; exit}')"
fi
if [[ -z "${DEVICE_ID}" ]]; then
  DEVICE_ID="$(adb devices | awk 'NR>1 && $2=="device" {print $1; exit}')"
fi
if [[ -z "${DEVICE_ID}" ]]; then
  echo "No adb devices found. Connect a device or pass --device <id>." >&2
  exit 1
fi

ensure_sink_count_csv() {
  local repro_project="${PROJECT_ROOT}/Reproduction/Reproduction.csproj"
  if [[ ! -f "${repro_project}" ]]; then
    repro_project="${PROJECT_ROOT}/Reproduction"
  fi

  local need_generate=false
  if [[ ! -s "${SINK_COUNT_CSV}" ]]; then
    need_generate=true
  else
    for count in 1 2 3 4 5; do
      if ! awk -F, -v c="${count}" 'NR>1 && $3==c { found=1; exit } END { exit(found?0:1) }' "${SINK_COUNT_CSV}"; then
        need_generate=true
        break
      fi
    done
  fi

  if [[ "${need_generate}" == "true" ]]; then
    if ! command -v dotnet >/dev/null 2>&1; then
      echo "dotnet not found; cannot generate ${SINK_COUNT_CSV}" >&2
      exit 1
    fi

    echo "Generating sink count CSV for screenshot selection..."
    (cd "${PROJECT_ROOT}" && dotnet run --project "${repro_project}" -- sinkanalysis 2000 >/tmp/decantra_sinkanalysis.log 2>&1)
  fi

  if [[ ! -s "${SINK_COUNT_CSV}" ]]; then
    echo "Missing sink count CSV after generation: ${SINK_COUNT_CSV}" >&2
    exit 1
  fi
}

resolve_sink_target() {
  local sink_count="$1"
  awk -F, -v c="${sink_count}" 'NR>1 && $3==c && $1 ~ /^[0-9]+$/ && $2 ~ /^-?[0-9]+$/ { print $1 ":" $2; exit }' "${SINK_COUNT_CSV}"
}

mkdir -p "${OUTPUT_DIR}"
rm -f "${OUTPUT_DIR}"/*.png || true

adb -s "${DEVICE_ID}" shell pm clear "${PACKAGE_NAME}" >/dev/null 2>&1 || true
adb -s "${DEVICE_ID}" shell rm -rf "/sdcard/Android/data/${PACKAGE_NAME}/files/DecantraScreenshots" >/dev/null 2>&1 || true
adb -s "${DEVICE_ID}" shell am force-stop "${PACKAGE_NAME}" >/dev/null 2>&1 || true
adb -s "${DEVICE_ID}" shell media volume --stream 3 --set 0 >/dev/null 2>&1 || true
adb -s "${DEVICE_ID}" shell cmd media_session volume --stream 3 --set 0 >/dev/null 2>&1 || true

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

extras=(--ez decantra_screenshots true --ez decantra_quiet true)
ensure_sink_count_csv
for count in 1 2 3 4 5; do
  target="$(resolve_sink_target "${count}")"
  if [[ -z "${target}" ]]; then
    echo "Could not resolve sink_count=${count} target from ${SINK_COUNT_CSV}" >&2
    exit 1
  fi

  level="${target%%:*}"
  seed="${target##*:}"
  extras+=(--ei "decantra_sink_count_${count}_level" "${level}")
  extras+=(--ei "decantra_sink_count_${count}_seed" "${seed}")
done

if [[ "${SCREENSHOTS_ONLY}" == "true" ]]; then
  extras+=(--ez decantra_screenshots_only true)
fi

adb -s "${DEVICE_ID}" shell am start -S -W -n "${PACKAGE_NAME}/${ACTIVITY_NAME}" "${extras[@]}" >/dev/null

expected=(
  "initial_render.png"
  "after_first_move.png"
  "startup_fade_in_midpoint.png"
  "help_overlay.png"
  "options_panel_typography.png"
  "options_audio_accessibility.png"
  "options_starfield_controls.png"
  "options_legal_privacy_terms.png"
  "star_trade_in_low_stars.png"
  "sink_count_1.png"
  "sink_count_2.png"
  "sink_count_3.png"
  "sink_count_4.png"
  "sink_count_5.png"
  "auto_solve_start.png"
  "auto_solve_complete.png"
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

tutorial_count=0
for idx in $(seq -w 1 12); do
  file="how_to_play_tutorial_page_${idx}.png"
  dest="${OUTPUT_DIR}/${file}"
  pulled=false
  for attempt in 1 2 3; do
    adb -s "${DEVICE_ID}" pull "/sdcard/Android/data/${PACKAGE_NAME}/${remote_dir}/${file}" "${dest}" >/dev/null && pulled=true || true
    if [[ -s "${dest}" ]]; then
      pulled=true
      break
    fi
    sleep 0.3
  done

  if [[ "${pulled}" == "true" && -s "${dest}" ]]; then
    tutorial_count=$((tutorial_count + 1))
    echo "Captured ${dest}"
    continue
  fi

  rm -f "${dest}" >/dev/null 2>&1 || true
  if [[ ${tutorial_count} -gt 0 ]]; then
    break
  fi
done

auto_step_count=0
for idx in $(seq -w 1 40); do
  drag_file="auto_solve_step_${idx}_drag.png"
  pour_file="auto_solve_step_${idx}_pour.png"
  drag_dest="${OUTPUT_DIR}/${drag_file}"
  pour_dest="${OUTPUT_DIR}/${pour_file}"

  drag_pulled=false
  pour_pulled=false

  for attempt in 1 2 3; do
    adb -s "${DEVICE_ID}" pull "/sdcard/Android/data/${PACKAGE_NAME}/${remote_dir}/${drag_file}" "${drag_dest}" >/dev/null && drag_pulled=true || true
    adb -s "${DEVICE_ID}" pull "/sdcard/Android/data/${PACKAGE_NAME}/${remote_dir}/${pour_file}" "${pour_dest}" >/dev/null && pour_pulled=true || true

    [[ -s "${drag_dest}" ]] && drag_pulled=true
    [[ -s "${pour_dest}" ]] && pour_pulled=true

    if [[ "${drag_pulled}" == "true" && "${pour_pulled}" == "true" ]]; then
      break
    fi
    sleep 0.2
  done

  if [[ "${drag_pulled}" == "true" && "${pour_pulled}" == "true" ]]; then
    auto_step_count=$((auto_step_count + 1))
    echo "Captured ${drag_dest}"
    echo "Captured ${pour_dest}"
    continue
  fi

  rm -f "${drag_dest}" "${pour_dest}" >/dev/null 2>&1 || true
  if [[ ${auto_step_count} -gt 0 ]]; then
    break
  fi
done

if [[ ${auto_step_count} -lt 1 ]]; then
  echo "Expected at least one auto-solve drag/pour step pair, captured ${auto_step_count}." >&2
  exit 1
fi

if [[ ${tutorial_count} -lt 7 ]]; then
  echo "Expected at least 7 tutorial page screenshots, captured ${tutorial_count}." >&2
  exit 1
fi

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
