#!/usr/bin/env bash
set -euo pipefail

UNITY_PATH="${UNITY_PATH:-Unity}"
PROJECT_PATH="$(pwd)"
BOOTSTRAP_LOG_PATH="${PROJECT_PATH}/Logs/test_bootstrap.log"
EDITMODE_LOG_PATH="${PROJECT_PATH}/Logs/test_editmode.log"
PLAYMODE_LOG_PATH="${PROJECT_PATH}/Logs/test_playmode.log"
REPORT_LOG_PATH="${PROJECT_PATH}/Logs/coverage_report.log"
UNITY_TIMEOUT="${UNITY_TIMEOUT:-10m}"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

ulimit -n 4096 >/dev/null 2>&1 || true

if [[ ! -x "${UNITY_PATH}" ]] && ! command -v "${UNITY_PATH}" >/dev/null 2>&1; then
  echo "Unity not found. Set UNITY_PATH to the Unity editor executable." >&2
  exit 1
fi

if ! command -v timeout >/dev/null 2>&1; then
  echo "timeout not found. Install coreutils." >&2
  exit 1
fi

mkdir -p "${PROJECT_PATH}/Logs"
mkdir -p "${PROJECT_PATH}/Coverage"

run_with_log() {
  local step_name="$1"
  local log_path="$2"
  shift 2

  echo "==> ${step_name}"
  : > "${log_path}"

  tail -n +1 -f "${log_path}" &
  local tail_pid=$!

  cleanup_tail() {
    kill "${tail_pid}" >/dev/null 2>&1 || true
  }

  trap cleanup_tail EXIT INT TERM

  set +e
  timeout "${UNITY_TIMEOUT}" "$@"
  local status=$?
  set -e

  cleanup_tail
  trap - EXIT INT TERM

  if [[ ${status} -ne 0 ]]; then
    echo "==> ${step_name} failed with exit code ${status}" >&2
    exit ${status}
  fi
}

run_with_log "Bootstrap Unity" "${BOOTSTRAP_LOG_PATH}" "${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_PATH}" \
  -logFile "${BOOTSTRAP_LOG_PATH}" \
  -quit

run_with_log "EditMode tests" "${EDITMODE_LOG_PATH}" "${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_PATH}" \
  -runTests \
  -executeMethod Decantra.App.Editor.CommandLineTests.RunEditMode \
  -testResults "${PROJECT_PATH}/Logs/TestResults.xml" \
  -debugCodeOptimization \
  -enableCodeCoverage \
  -coverageResultsPath "${PROJECT_PATH}/Coverage" \
  -coverageOptions "generateAdditionalMetrics;assemblyFilters:+Decantra.Domain;dontClear" \
  -logFile "${EDITMODE_LOG_PATH}"

run_with_log "Coverage report" "${REPORT_LOG_PATH}" "${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_PATH}" \
  -enableCodeCoverage \
  -coverageResultsPath "${PROJECT_PATH}/Coverage" \
  -coverageOptions "generateHtmlReport;generateAdditionalMetrics;assemblyFilters:+Decantra.Domain" \
  -logFile "${REPORT_LOG_PATH}" \
  -quit

run_with_log "PlayMode tests" "${PLAYMODE_LOG_PATH}" "${UNITY_PATH}" \
  -batchmode \
  -projectPath "${PROJECT_PATH}" \
  -buildTarget StandaloneLinux64 \
  -executeMethod Decantra.App.Editor.CommandLineTests.RunPlayMode \
  -testResults "${PROJECT_PATH}/Logs/PlayModeTestResults.xml" \
  -logFile "${PLAYMODE_LOG_PATH}"

"${PROJECT_PATH}/tools/coverage_gate.sh" "${PROJECT_PATH}/Coverage" "0.8"

echo "EditMode tests, PlayMode tests, and coverage completed. Logs: ${BOOTSTRAP_LOG_PATH}, ${EDITMODE_LOG_PATH}, ${PLAYMODE_LOG_PATH}"
