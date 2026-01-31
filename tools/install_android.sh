#!/usr/bin/env bash
set -euo pipefail

APK_PATH="${1:-$(pwd)/Builds/Android/Decantra.apk}"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

ADB_LOG_PATH="/tmp/adb.${UID}.log"

ensure_adb_daemon() {
  local attempt=1
  local max_attempts=4
  local inotify_limit=""
  local desired_inotify=512

  if ! command -v adb >/dev/null 2>&1; then
    echo "adb not found in PATH" >&2
    return 1
  fi

  inotify_limit="$(cat /proc/sys/fs/inotify/max_user_instances 2>/dev/null || echo "")"
  if [[ -n "${inotify_limit}" && "${inotify_limit}" -lt ${desired_inotify} ]]; then
    if command -v sudo >/dev/null 2>&1; then
      sudo sysctl -w "fs.inotify.max_user_instances=${desired_inotify}" >/dev/null 2>&1 || true
    fi
  fi

  while [[ ${attempt} -le ${max_attempts} ]]; do
    ulimit -n 8192 >/dev/null 2>&1 || true
    adb kill-server >/dev/null 2>&1 || true
    pkill -f "[a]db" >/dev/null 2>&1 || true
    rm -f "${ADB_LOG_PATH}" >/dev/null 2>&1 || true
    adb start-server >/dev/null 2>&1 || true
    sleep 0.6

    if adb devices >/dev/null 2>&1; then
      return 0
    fi

    echo "adb daemon failed to start (attempt ${attempt}/${max_attempts})." >&2
    if [[ -f "${ADB_LOG_PATH}" ]]; then
      tail -n 5 "${ADB_LOG_PATH}" >&2 || true
    fi
    attempt=$((attempt + 1))
    sleep 0.8
  done

  echo "adb daemon failed to start after ${max_attempts} attempts." >&2
  if [[ -f "${ADB_LOG_PATH}" ]]; then
    tail -n 20 "${ADB_LOG_PATH}" >&2 || true
  fi
  return 1
}

if ! command -v adb >/dev/null 2>&1; then
  echo "adb not found in PATH"
  exit 1
fi

ensure_adb_daemon

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
