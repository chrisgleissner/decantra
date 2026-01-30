#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE="uk.gleissner.decantra"
APK_PATH_DEFAULT="${ROOT_DIR}/Builds/Android/Decantra.apk"

DURATION_SECONDS=600
TARGET_LEVEL=0
TAPS_PER_SECOND=3
DEVICE_ID=""
EMULATOR_AVD=""
APK_PATH="${APK_PATH_DEFAULT}"
INSTALL_APK=true
STARTUP_TIMEOUT=20
STARTUP_GRACE=3

log() {
  printf "\n==> %s\n" "$1"
}

usage() {
  cat <<'EOF'
Usage: tools/soak_test.sh [options]

Options:
  --duration <seconds>      Total soak duration (default: 600)
  --target-level <level>    Stop when level reached (default: 0 = disabled)
  --taps-per-sec <n>        Tap pairs per second (default: 3)
  --device <id>             Specific adb device id
  --emulator <avd>          Start emulator AVD and use it
  --apk-path <path>         APK path (default: Builds/Android/Decantra.apk)
  --no-install              Skip APK install
  --startup-timeout <sec>   Seconds to wait for first level (default: 20)
  -h, --help                Show help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --duration) DURATION_SECONDS="${2:-}"; shift 2 ;;
    --target-level) TARGET_LEVEL="${2:-}"; shift 2 ;;
    --taps-per-sec) TAPS_PER_SECOND="${2:-}"; shift 2 ;;
    --device) DEVICE_ID="${2:-}"; shift 2 ;;
    --emulator) EMULATOR_AVD="${2:-}"; shift 2 ;;
    --apk-path) APK_PATH="${2:-}"; shift 2 ;;
    --no-install) INSTALL_APK=false; shift 1 ;;
    --startup-timeout) STARTUP_TIMEOUT="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown arg: $1" >&2; usage; exit 1 ;;
  esac
done

if [[ ! -f "${APK_PATH}" ]]; then
  echo "APK not found: ${APK_PATH}" >&2
  exit 1
fi

if [[ -n "${EMULATOR_AVD}" ]]; then
  if ! command -v emulator >/dev/null 2>&1; then
    echo "emulator not found in PATH" >&2
    exit 1
  fi

  log "Starting emulator: ${EMULATOR_AVD}"
  nohup emulator -avd "${EMULATOR_AVD}" -no-snapshot-save -netfast >/tmp/decantra-emulator.log 2>&1 &

  log "Waiting for emulator boot (max 5 min)"
  start_time=$(date +%s)
  while true; do
    sleep 10
    if adb devices | grep -q "emulator-"; then
      booted=$(adb shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')
      if [[ "${booted}" == "1" ]]; then
        break
      fi
    fi

    now=$(date +%s)
    elapsed=$((now - start_time))
    if (( elapsed > 300 )); then
      echo "Emulator boot timeout" >&2
      exit 1
    fi
  done

  DEVICE_ID="${DEVICE_ID:-emulator-5554}"
fi

if [[ -z "${DEVICE_ID}" ]]; then
  DEVICE_ID=$(adb devices | awk 'NR>1 && $2=="device" {print $1; exit}')
fi

if [[ -z "${DEVICE_ID}" ]]; then
  echo "No adb device available" >&2
  exit 1
fi

ADB=(adb -s "${DEVICE_ID}")

if [[ "${INSTALL_APK}" == "true" ]]; then
  log "Installing APK"
  if ! timeout 2m "${ADB[@]}" install --no-streaming -r -d -g "${APK_PATH}"; then
    log "Install command timed out or failed, verifying package presence"
  fi
  if ! "${ADB[@]}" shell pm list packages --user 0 | grep -q "${PACKAGE}"; then
    echo "Package not present after install" >&2
    exit 1
  fi
fi

log "Launching app"
ACTIVITY=$(${ADB[@]} shell cmd package resolve-activity --brief "${PACKAGE}" | tail -n 1 | tr -d '\r')
if [[ -z "${ACTIVITY}" || "${ACTIVITY}" == "No activity found" ]]; then
  ACTIVITY="${PACKAGE}/.MainActivity"
fi
"${ADB[@]}" shell am start -W -n "${ACTIVITY}" >/dev/null
"${ADB[@]}" logcat -c

log "Waiting for first level (timeout ${STARTUP_TIMEOUT}s)"
start_time=$(date +%s)
while true; do
  line=$("${ADB[@]}" logcat -d -s Unity | grep "Decantra LevelLoaded" | tail -n 1 || true)
  if [[ -n "${line}" ]]; then
    break
  fi
  now=$(date +%s)
  if (( now - start_time > STARTUP_TIMEOUT )); then
    echo "First level not loaded within ${STARTUP_TIMEOUT}s; continuing soak" >&2
    break
  fi
  sleep 1
done

sleep "${STARTUP_GRACE}"

log "Running soak for ${DURATION_SECONDS}s"
start_time=$(date +%s)
last_health_check=$start_time
last_progress_log=$start_time
last_level=0

get_screen_size() {
  local size
  size=$("${ADB[@]}" shell wm size | grep -oE '[0-9]+x[0-9]+' | head -n 1)
  echo "${size}"
}

build_grid_points() {
  local w h size
  size=$(get_screen_size)
  w=${size%x*}
  h=${size#*x}

  if [[ -z "${w}" || -z "${h}" ]]; then
    w=1080
    h=1920
  fi

  local left=$((w * 15 / 100))
  local right=$((w * 85 / 100))
  local top=$((h * 25 / 100))
  local bottom=$((h * 75 / 100))

  local col0=$left
  local col1=$((left + (right - left) / 2))
  local col2=$right
  local row0=$top
  local row1=$((top + (bottom - top) / 2))
  local row2=$bottom

  echo "${col0},${row0} ${col1},${row0} ${col2},${row0} ${col0},${row1} ${col1},${row1} ${col2},${row1} ${col0},${row2} ${col1},${row2} ${col2},${row2}"
}

points=( $(build_grid_points) )

while true; do
  now=$(date +%s)
  elapsed=$((now - start_time))
  if (( elapsed >= DURATION_SECONDS )); then
    break
  fi

  line=$("${ADB[@]}" logcat -d -s Unity | grep "Decantra LevelLoaded" | tail -n 1 || true)
  if [[ -n "${line}" ]]; then
    level=$(echo "${line}" | sed -n 's/.*level=\([0-9]*\).*/\1/p')
    if [[ -n "${level}" ]]; then
      last_level=${level}
      if (( TARGET_LEVEL > 0 && level >= TARGET_LEVEL )); then
        log "Target level reached: ${level}"
        break
      fi
    fi
  fi

  if (( now - last_health_check >= 30 )); then
    last_health_check=$now
    if "${ADB[@]}" logcat -d | grep -E "FATAL EXCEPTION|ANR in|Application Not Responding" >/dev/null; then
      echo "Detected crash/ANR in logcat" >&2
      exit 1
    fi
  fi

  if (( now - last_progress_log >= 30 )); then
    last_progress_log=$now
    log "Soak progress: ${elapsed}s elapsed, last level=${last_level}"
  fi

  idx1=$((RANDOM % 9))
  idx2=$((RANDOM % 9))
  while [[ "${idx2}" == "${idx1}" ]]; do
    idx2=$((RANDOM % 9))
  done

  p1=${points[$idx1]}
  p2=${points[$idx2]}
  x1=${p1%,*}; y1=${p1#*,}
  x2=${p2%,*}; y2=${p2#*,}

  "${ADB[@]}" shell input tap "$x1" "$y1" >/dev/null
  sleep 0.05
  "${ADB[@]}" shell input tap "$x2" "$y2" >/dev/null

  sleep_time=$(awk "BEGIN {print 1/${TAPS_PER_SECOND}}")
  sleep "${sleep_time}"
done

log "Soak test complete"
exit 0
