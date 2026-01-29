#!/usr/bin/env bash
set -euo pipefail

UNITY_PATH="${UNITY_PATH:-Unity}"
PROJECT_PATH="$(pwd)"
LOG_PATH="${PROJECT_PATH}/Logs/build_android.log"
BUILD_PATH="${PROJECT_PATH}/Builds/Android"
APK_PATH="${BUILD_PATH}/Decantra.apk"
UNITY_BUILD_TIMEOUT="${UNITY_BUILD_TIMEOUT:-20m}"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

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

timeout "${UNITY_BUILD_TIMEOUT}" "${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_PATH}" \
  -executeMethod Decantra.App.Editor.AndroidBuild.BuildDebugApk \
  -logFile "${LOG_PATH}" \
  -quit \
  -buildPath "${APK_PATH}"

if [ ! -f "${APK_PATH}" ]; then
  echo "APK not found at ${APK_PATH}"
  exit 1
fi

echo "Android APK built at ${APK_PATH}"
