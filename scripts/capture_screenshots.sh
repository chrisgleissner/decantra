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
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-${ADB_SERVER_PORT:-5037}}"
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
ACTIVITY_NAME="com.unity3d.player.UnityPlayerGameActivity"
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

prepare_device_for_capture() {
  adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_WAKEUP >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_MENU >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell wm dismiss-keyguard >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell input swipe 540 1800 540 320 220 >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell cmd statusbar collapse >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell service call statusbar 2 >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_BACK >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_HOME >/dev/null 2>&1 || true
}

nudge_device_awake() {
  adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_WAKEUP >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell wm dismiss-keyguard >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell cmd statusbar collapse >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell service call statusbar 2 >/dev/null 2>&1 || true
}

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

prepare_device_for_capture

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

core_extras=("${extras[@]}" --es decantra_screenshot_phase core)
ui_extras=("${extras[@]}" --es decantra_screenshot_phase ui)

launch_timeout_secs="${DECANTRA_LAUNCH_TIMEOUT:-60}"
resolve_device_activity() {
  local resolved=""
  resolved="$(adb -s "${DEVICE_ID}" shell cmd package resolve-activity --brief "${PACKAGE_NAME}" 2>/dev/null | tr -d '\r' | tail -n 1 || true)"
  if [[ -z "${resolved}" ]]; then
    return 1
  fi

  if [[ "${resolved}" == "No activity found" || "${resolved}" == *"No activity"* ]]; then
    return 1
  fi

  if [[ "${resolved}" == *"/"* ]]; then
    ACTIVITY_NAME="${resolved#*/}"
    return 0
  fi

  return 1
}

launch_app() {
  local -n launch_extras_ref=$1
  local launch_output=""
  local launch_status=0

  launch_output="$(timeout "${launch_timeout_secs}" adb -s "${DEVICE_ID}" shell am start -S -n "${PACKAGE_NAME}/${ACTIVITY_NAME}" "${launch_extras_ref[@]}" 2>&1)" || launch_status=$?
  if [[ ${launch_status} -ne 0 ]]; then
    echo "Failed to launch ${PACKAGE_NAME}/${ACTIVITY_NAME} on device ${DEVICE_ID}." >&2
    if [[ -n "${launch_output}" ]]; then
      echo "${launch_output}" >&2
    fi
    return 1
  fi

  if [[ "${launch_output}" == *"Error type"* || "${launch_output}" == *"does not exist"* || "${launch_output}" == *"Exception occurred"* ]]; then
    if resolve_device_activity; then
      launch_status=0
      launch_output="$(timeout "${launch_timeout_secs}" adb -s "${DEVICE_ID}" shell am start -S -n "${PACKAGE_NAME}/${ACTIVITY_NAME}" "${launch_extras_ref[@]}" 2>&1)" || launch_status=$?
      if [[ ${launch_status} -eq 0 && "${launch_output}" != *"Error type"* && "${launch_output}" != *"does not exist"* && "${launch_output}" != *"Exception occurred"* ]]; then
        return 0
      fi
    fi

    echo "Launch command reported an activity error for ${PACKAGE_NAME}/${ACTIVITY_NAME}." >&2
    echo "${launch_output}" >&2
    return 1
  fi

  return 0
}

wait_for_capture_phase() {
  local phase_name="$1"
  local remote_dir="files/DecantraScreenshots"
  local complete_marker="${remote_dir}/capture.complete"
  local start_time
  local now
  local elapsed
  local last_progress_log=0
  local run_as_ok=false

  if adb -s "${DEVICE_ID}" shell run-as uk.gleissner.decantra ls "${remote_dir}" >/dev/null 2>&1; then
    run_as_ok=true
  fi

  start_time=$(date +%s)
  while true; do
    nudge_device_awake
    if [[ "${run_as_ok}" == "true" ]]; then
      if adb -s "${DEVICE_ID}" shell run-as uk.gleissner.decantra ls "${complete_marker}" >/dev/null 2>&1; then
        break
      fi
    else
      if adb -s "${DEVICE_ID}" shell ls "/sdcard/Android/data/${PACKAGE_NAME}/${complete_marker}" >/dev/null 2>&1; then
        break
      fi
    fi

    now=$(date +%s)
    elapsed=$((now - start_time))

    if (( elapsed - last_progress_log >= 15 )); then
      focus_line="$(adb -s "${DEVICE_ID}" shell dumpsys activity activities | grep -m1 'mCurrentFocus=' || true)"
      echo "Waiting for screenshot capture marker (${phase_name})... elapsed=${elapsed}s focus='${focus_line}'" >&2
      last_progress_log=${elapsed}
    fi

    if (( elapsed > 30 )); then
      focus_line="$(adb -s "${DEVICE_ID}" shell dumpsys activity activities | grep -m1 'mCurrentFocus=' || true)"
      if [[ "${focus_line}" == *"Application Not Responding"* ]]; then
        echo "ANR dialog detected during ${phase_name}; attempting dismissal and relaunch." >&2
        adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_BACK >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_ENTER >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_HOME >/dev/null 2>&1 || true
      fi

      if [[ "${focus_line}" == *"NotificationShade"* ]]; then
        adb -s "${DEVICE_ID}" shell cmd statusbar collapse >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell service call statusbar 2 >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_WAKEUP >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell wm dismiss-keyguard >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell input swipe 540 1800 540 320 220 >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_BACK >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_HOME >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell am start -n "${PACKAGE_NAME}/com.unity3d.player.UnityPlayerGameActivity" >/dev/null 2>&1 || true
      fi

      if [[ "${focus_line}" == *"ImmersiveModeConfirmation"* ]]; then
        echo "Immersive mode confirmation detected during ${phase_name}; attempting dismissal." >&2
        adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_ENTER >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell input keyevent KEYCODE_BACK >/dev/null 2>&1 || true
        adb -s "${DEVICE_ID}" shell input tap 540 960 >/dev/null 2>&1 || true
      fi
    fi

    if (( elapsed > DECANTRA_SCREENSHOT_TIMEOUT )); then
      echo "Timed out waiting for screenshot capture (${phase_name})." >&2
      return 1
    fi
    sleep 1
  done

  return 0
}

run_capture_phase() {
  local phase_name="$1"
  local -n phase_extras_ref=$2
  local remote_dir="files/DecantraScreenshots"
  local complete_marker="/sdcard/Android/data/${PACKAGE_NAME}/${remote_dir}/capture.complete"

  adb -s "${DEVICE_ID}" shell rm -f "${complete_marker}" >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell am force-stop "${PACKAGE_NAME}" >/dev/null 2>&1 || true

  if ! launch_app phase_extras_ref; then
    return 1
  fi

  wait_for_capture_phase "${phase_name}"
}

if ! run_capture_phase core core_extras; then
  exit 1
fi

if ! run_capture_phase ui ui_extras; then
  exit 1
fi

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
  "completed_bottle_topper.png"
  "scene_3x3_bottles.png"
  "empty_bottles_scene.png"
  "v2-layout-report.json"
  "cork-layout-report.json"
  "solved-layout-report.json"
  "layout-report.json"
  "screenshot-01-launch.png"
  "screenshot-02-intro.png"
  "screenshot-03-level-01.png"
  "screenshot-08-level-10.png"
  "screenshot-04-level-12.png"
  "screenshot-09-level-20.png"
  "screenshot-05-level-24.png"
  "screenshot-06-interstitial.png"
  "screenshot-07-level-36.png"
  "screenshot-level-506.png"
  "screenshot-10-options.png"
)

remote_dir="files/DecantraScreenshots"
complete_marker="${remote_dir}/capture.complete"

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
    if [[ "${file}" == "completed_bottle_topper.png" && -s "${OUTPUT_DIR}/auto_solve_complete.png" ]]; then
      cp -f "${OUTPUT_DIR}/auto_solve_complete.png" "${dest}"
      pulled=true
      echo "Using auto_solve_complete.png as fallback for completed_bottle_topper.png" >&2
    fi
  fi

  if [[ "${pulled}" != "true" || ! -s "${dest}" ]]; then
    echo "Screenshot missing or empty: ${dest}" >&2
    exit 1
  fi
  echo "Captured ${dest}"
  done

tutorial_count=0
tutorial_output_dir="${OUTPUT_DIR}/Tutorial"
mkdir -p "${tutorial_output_dir}"
adb -s "${DEVICE_ID}" pull "/sdcard/Android/data/${PACKAGE_NAME}/${remote_dir}/Tutorial" "${OUTPUT_DIR}" >/dev/null 2>&1 || true

if [[ -d "${tutorial_output_dir}" ]]; then
  tutorial_count="$(find "${tutorial_output_dir}" -type f -name 'tutorial_step_*.png' | wc -l | tr -d ' ')"
fi

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

    if [[ "${pour_pulled}" == "true" ]]; then
      break
    fi
    sleep 0.2
  done

  if [[ "${pour_pulled}" == "true" ]]; then
    auto_step_count=$((auto_step_count + 1))
    if [[ "${drag_pulled}" == "true" ]]; then
      echo "Captured ${drag_dest}"
    fi
    echo "Captured ${pour_dest}"
    continue
  fi

  rm -f "${drag_dest}" "${pour_dest}" >/dev/null 2>&1 || true
  if [[ ${auto_step_count} -gt 0 ]]; then
    break
  fi
done

if [[ ${auto_step_count} -lt 1 ]]; then
  echo "Expected at least one auto-solve step screenshot, captured ${auto_step_count}." >&2
  exit 1
fi

if [[ ${tutorial_count} -lt 8 ]]; then
  echo "Expected at least 8 tutorial step screenshots, captured ${tutorial_count}." >&2
  exit 1
fi

tutorial_summary_count="$(find "${tutorial_output_dir}" -type f -name 'tutorial_capture_summary.log' | wc -l | tr -d ' ')"
if [[ ${tutorial_summary_count} -lt 1 ]]; then
  echo "Missing tutorial capture summary log under ${tutorial_output_dir}." >&2
  exit 1
fi

adb -s "${DEVICE_ID}" shell am force-stop uk.gleissner.decantra >/dev/null 2>&1 || true

printf "\nScreenshots captured to %s\n" "${OUTPUT_DIR}"

# ── Copy v2 verification artifacts to docs repo path ──────────────────────────
echo "==> Copying v2 verification artifacts..."
v2_dir="${PROJECT_ROOT}/docs/repro/3d-bottle-regressions/final-verification-v2"
mkdir -p "${v2_dir}"
cp -f "${OUTPUT_DIR}/screenshot-09-level-20.png"   "${v2_dir}/level-20.png"       2>/dev/null || true
cp -f "${OUTPUT_DIR}/screenshot-07-level-36.png"   "${v2_dir}/level-36.png"       2>/dev/null || true
cp -f "${OUTPUT_DIR}/screenshot-08-level-10.png"   "${v2_dir}/level-10-3x3.png"   2>/dev/null || true
cp -f "${OUTPUT_DIR}/sink_count_1.png"             "${v2_dir}/sink-bottle.png"    2>/dev/null || true
cp -f "${OUTPUT_DIR}/completed_bottle_topper.png"  "${v2_dir}/completed-bottle-topper.png" 2>/dev/null || true
cp -f "${OUTPUT_DIR}/v2-layout-report.json"        "${PROJECT_ROOT}/docs/repro/3d-bottle-regressions/v2-layout-report.json" 2>/dev/null || true
echo "   v2 verification artifacts at ${v2_dir}"

# ── Copy v3 verification artifacts to docs repo path (cork stoppers, Plan 21) ───
echo "==> Copying v3 verification artifacts (cork stoppers)..."
v3_dir="${PROJECT_ROOT}/docs/repro/3d-bottle-regressions/final-verification-v3"
mkdir -p "${v3_dir}"
cp -f "${OUTPUT_DIR}/screenshot-09-level-20.png"        "${v3_dir}/level-20.png"              2>/dev/null || true
cp -f "${OUTPUT_DIR}/screenshot-07-level-36.png"        "${v3_dir}/level-36.png"              2>/dev/null || true
cp -f "${OUTPUT_DIR}/screenshot-08-level-10.png"        "${v3_dir}/level-3x3.png"             2>/dev/null || true
cp -f "${OUTPUT_DIR}/completed_bottle_topper.png"       "${v3_dir}/completed-bottle-cork.png"  2>/dev/null || true
cp -f "${OUTPUT_DIR}/cork-layout-report.json"           "${PROJECT_ROOT}/docs/repro/3d-bottle-regressions/cork-layout-report.json" 2>/dev/null || true
echo "   v3 verification artifacts at ${v3_dir}"

# ── Copy canonical visual-verification artifacts (objective loop) ────────────
echo "==> Copying canonical visual verification artifacts..."
vv_root="${PROJECT_ROOT}/docs/repro/visual-verification"
vv_shots="${vv_root}/screenshots"
vv_reports="${vv_root}/reports"
mkdir -p "${vv_shots}" "${vv_reports}"

cp -f "${OUTPUT_DIR}/screenshot-09-level-20.png"       "${vv_shots}/level-20.png" 2>/dev/null || true
cp -f "${OUTPUT_DIR}/screenshot-05-level-24.png"       "${vv_shots}/level-24.png" 2>/dev/null || true
cp -f "${OUTPUT_DIR}/screenshot-07-level-36.png"       "${vv_shots}/level-36.png" 2>/dev/null || true
cp -f "${OUTPUT_DIR}/layout-report.json"               "${vv_reports}/layout-report.json" 2>/dev/null || true

if [[ ! -s "${vv_reports}/layout-report.json" ]]; then
  echo "Missing required layout report at ${vv_reports}/layout-report.json" >&2
  exit 1
fi

echo "   visual verification screenshots at ${vv_shots}"
echo "   visual verification report at ${vv_reports}/layout-report.json"

if [[ "${CAPTURE_MOTION}" == "true" ]]; then
  echo "\n==> Capturing starfield motion frames"
  motion_extras=(--ez decantra_motion_capture true)

  adb -s "${DEVICE_ID}" shell am force-stop "${PACKAGE_NAME}" >/dev/null 2>&1 || true
  adb -s "${DEVICE_ID}" shell am start -S -n "${PACKAGE_NAME}/${ACTIVITY_NAME}" "${motion_extras[@]}" >/dev/null

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

echo "\n==> Pruning pixel-identical screenshots against main"
python3 "${PROJECT_ROOT}/scripts/prune_duplicate_screenshots.py" --base main --mode apply
python3 "${PROJECT_ROOT}/scripts/prune_duplicate_screenshots.py" --base main --mode check
