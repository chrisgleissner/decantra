#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APK_DEFAULT="${ROOT_DIR}/Builds/Android/Decantra.apk"
DECANTRA_ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT:-5039}"
DECANTRA_ANDROID_SERIAL="${DECANTRA_ANDROID_SERIAL:-${ANDROID_SERIAL:-}}"
UNITY_PATH_OVERRIDE="${UNITY_PATH:-}"

export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"

RUN_TESTS=true
RUN_BUILD=true
RUN_INSTALL=false

DEVICE_ID=""
APK_PATH=""

APT_UPDATED=false

usage() {
  cat <<'EOF'
build.sh - Decantra Android build helper

Usage:
  ./build.sh [options]

Default (no options):
  - Run EditMode + PlayMode tests
  - Build debug APK

Options:
  --install           Install APK to first connected Android device (via adb)
  --device <id>        Specific device ID for adb (optional)
  --adb-port <port>    ADB server port for Decantra (default: 5039)
  --serial <id>        Device serial to target (env: DECANTRA_ANDROID_SERIAL)
  --apk-path <path>    APK path to install (default: Builds/Android/Decantra.apk)
  --skip-tests         Skip Unity tests
  --skip-build         Skip APK build
  -h, --help           Show this help
EOF
}

log() {
  printf "\n==> %s\n" "$1"
}

have_cmd() {
  command -v "$1" >/dev/null 2>&1
}

run_with_sudo_if_needed() {
  if [[ ${EUID:-0} -ne 0 ]]; then
    if have_cmd sudo; then
      sudo "$@"
      return
    fi

    echo "Missing sudo for installing dependencies." >&2
    return 1
  fi

  "$@"
}

apt_install_once() {
  local pkg="$1"

  if ! have_cmd apt-get; then
    echo "apt-get not available to install ${pkg}. Install it manually." >&2
    return 1
  fi

  if [[ "${APT_UPDATED}" == "false" ]]; then
    run_with_sudo_if_needed apt-get update -y
    APT_UPDATED=true
  fi

  run_with_sudo_if_needed apt-get install -y "${pkg}"
}

ensure_command() {
  local cmd="$1"
  local pkg="$2"

  if have_cmd "${cmd}"; then
    return 0
  fi

  log "Installing missing dependency: ${cmd}"
  apt_install_once "${pkg}"

  if ! have_cmd "${cmd}"; then
    echo "Dependency still missing: ${cmd}" >&2
    return 1
  fi
}

is_writable_dir() {
  local path="$1"
  [[ -n "${path}" && -d "${path}" && -w "${path}" ]]
}

run_sdkmanager() {
  local sdk_root="$1"
  local java_home="$2"
  local component="$3"

  if is_writable_dir "${sdk_root}"; then
    env ANDROID_SDK_ROOT="${sdk_root}" ANDROID_HOME="${sdk_root}" JAVA_HOME="${java_home}" \
      bash -c "yes | sdkmanager --sdk_root='${sdk_root}' '${component}'"
  else
    run_with_sudo_if_needed env ANDROID_SDK_ROOT="${sdk_root}" ANDROID_HOME="${sdk_root}" JAVA_HOME="${java_home}" \
      bash -c "yes | sdkmanager --sdk_root='${sdk_root}' '${component}'"
  fi
}

ensure_sdk_component() {
  local sdk_root="$1"
  local component="$2"
  local marker_dir="$3"
  local java_home="${4:-}"

  if [[ -n "${marker_dir}" && -d "${marker_dir}" ]]; then
    return 0
  fi

  if ! have_cmd sdkmanager; then
    echo "sdkmanager not found. Cannot install ${component}." >&2
    return 1
  fi

  log "Installing ${component} via sdkmanager"
  run_sdkmanager "${sdk_root}" "${java_home}" "${component}"
}

ensure_cmdline_tools_latest() {
  local sdk_root="$1"
  local version="$2"
  local target_dir="${sdk_root}/cmdline-tools/${version}"
  local latest_link="${sdk_root}/cmdline-tools/latest"

  if [[ ! -d "${target_dir}" ]]; then
    return 0
  fi

  if is_writable_dir "${sdk_root}/cmdline-tools"; then
    ln -sfn "${target_dir}" "${latest_link}"
  else
    run_with_sudo_if_needed ln -sfn "${target_dir}" "${latest_link}"
  fi
}

ensure_ndk_version() {
  local sdk_root="$1"
  local version="$2"
  local java_home="${3:-}"
  local ndk_dir="${sdk_root}/ndk/${version}"

  if [[ -d "${ndk_dir}" ]]; then
    echo "${ndk_dir}"
    return 0
  fi

  if ! have_cmd sdkmanager; then
    echo "sdkmanager not found. Cannot install NDK ${version}." >&2
    return 1
  fi

  log "Installing Android NDK ${version} via sdkmanager"
  run_sdkmanager "${sdk_root}" "${java_home}" "ndk;${version}"
  if is_writable_dir "${sdk_root}"; then
    env ANDROID_SDK_ROOT="${sdk_root}" ANDROID_HOME="${sdk_root}" JAVA_HOME="${java_home}" \
      bash -c "yes | sdkmanager --sdk_root='${sdk_root}' --licenses" || true
  else
    run_with_sudo_if_needed env ANDROID_SDK_ROOT="${sdk_root}" ANDROID_HOME="${sdk_root}" JAVA_HOME="${java_home}" \
      bash -c "yes | sdkmanager --sdk_root='${sdk_root}' --licenses" || true
  fi

  if [[ -d "${ndk_dir}" ]]; then
    echo "${ndk_dir}"
    return 0
  fi

  echo "Failed to install NDK ${version} to ${ndk_dir}." >&2
  return 1
}

detect_java_home() {
  if [[ -d "/usr/lib/jvm/java-17-openjdk-amd64" ]]; then
    echo "/usr/lib/jvm/java-17-openjdk-amd64"
    return 0
  fi

  if [[ -d "/usr/lib/jvm/java-11-openjdk-amd64" ]]; then
    echo "/usr/lib/jvm/java-11-openjdk-amd64"
    return 0
  fi

  local javac_path=""
  javac_path="$(command -v javac 2>/dev/null || true)"
  if [[ -n "${javac_path}" ]]; then
    readlink -f "${javac_path}" | sed 's:/bin/javac$::'
    return 0
  fi

  return 1
}

ensure_jdk_17() {
  if [[ -d "/usr/lib/jvm/java-17-openjdk-amd64" ]]; then
    echo "/usr/lib/jvm/java-17-openjdk-amd64"
    return 0
  fi

  apt_install_once openjdk-17-jdk

  if [[ -d "/usr/lib/jvm/java-17-openjdk-amd64" ]]; then
    echo "/usr/lib/jvm/java-17-openjdk-amd64"
    return 0
  fi

  return 1
}

detect_android_sdk_root() {
  local candidate=""
  for candidate in \
    "${ANDROID_SDK_ROOT:-}" \
    "${ANDROID_HOME:-}" \
    "${HOME}/Android/Sdk" \
    "/usr/lib/android-sdk" \
    "/opt/android-sdk" \
    "/usr/local/android-sdk"; do
    if [[ -n "${candidate}" && -d "${candidate}" ]]; then
      echo "${candidate}"
      return 0
    fi
  done

  return 1
}

detect_android_ndk_root() {
  local candidate=""

  if [[ -n "${ANDROID_NDK_ROOT:-}" && -d "${ANDROID_NDK_ROOT}" ]]; then
    echo "${ANDROID_NDK_ROOT}"
    return 0
  fi

  for candidate in /usr/lib/android-ndk* /opt/android-ndk* /usr/local/android-ndk*; do
    if [[ -d "${candidate}" ]]; then
      echo "${candidate}"
      return 0
    fi
  done

  if [[ -d "/usr/lib/android-sdk/ndk" ]]; then
    if [[ -d "/usr/lib/android-sdk/ndk/27.2.12479018" ]]; then
      echo "/usr/lib/android-sdk/ndk/27.2.12479018"
      return 0
    fi

    candidate=$(ls -d /usr/lib/android-sdk/ndk/* 2>/dev/null | sort -V | tail -n 1 || true)
    if [[ -n "${candidate}" && -d "${candidate}" ]]; then
      echo "${candidate}"
      return 0
    fi
  fi

  return 1
}

ensure_android_toolchain() {
  local unity_path="$1"
  local editor_dir=""
  local embedded_ndk=""
  local embedded_clang=""
  local sdk_root_pre=""
  local required_ndk="27.2.12479018"

  editor_dir="$(cd "$(dirname "${unity_path}")" && pwd)"
  embedded_ndk="${editor_dir}/Data/PlaybackEngines/AndroidPlayer/NDK"
  embedded_clang="${embedded_ndk}/toolchains/llvm/prebuilt/linux-x86_64/bin/clang++"

  if [[ -x "${embedded_clang}" ]]; then
    return 0
  fi

  log "Android NDK not found in Unity. Installing external Android toolchain."

  local ensured_java_home=""
  ensured_java_home="$(ensure_jdk_17 || true)"

  sdk_root_pre="$(detect_android_sdk_root || true)"
  if [[ -n "${sdk_root_pre}" && -d "${sdk_root_pre}/ndk/${required_ndk}" && -d "${sdk_root_pre}/platform-tools" && -d "${sdk_root_pre}/build-tools/36.0.0" && -d "${sdk_root_pre}/cmdline-tools/16.0" ]]; then
    log "Android SDK/NDK already installed at ${sdk_root_pre}. Skipping apt installs."
  else
    if ! have_cmd adb; then
      if dpkg -s google-android-platform-tools-installer >/dev/null 2>&1; then
        apt_install_once google-android-platform-tools-installer
      else
        apt_install_once android-sdk-platform-tools
      fi
    fi

    if dpkg -s google-android-platform-tools-installer >/dev/null 2>&1; then
      apt_install_once google-android-cmdline-tools-13.0-installer
      apt_install_once google-android-ndk-r25c-installer
    else
      apt_install_once android-sdk
      apt_install_once android-sdk-build-tools
      apt_install_once android-sdk-platforms
      apt_install_once android-sdk-platform-tools
    fi
  fi

  local sdk_root=""
  local ndk_root=""
  local java_home=""

  sdk_root="$(detect_android_sdk_root || true)"
  java_home="${ensured_java_home}"
  if [[ -z "${java_home}" ]]; then
    java_home="$(detect_java_home || true)"
  fi

  if [[ -z "${sdk_root}" ]]; then
    sdk_root="${HOME}/Android/Sdk"
    mkdir -p "${sdk_root}"
  fi

  ndk_root="$(detect_android_ndk_root || true)"

  if [[ -n "${sdk_root}" ]]; then
    local required_ndk="27.2.12479018"
    local ensured_ndk=""
    ensured_ndk="$(ensure_ndk_version "${sdk_root}" "${required_ndk}" "${java_home}" || true)"
    if [[ -n "${ensured_ndk}" ]]; then
      ndk_root="${ensured_ndk}"
    fi

    ensure_sdk_component "${sdk_root}" "cmake;3.22.1" "${sdk_root}/cmake/3.22.1" "${java_home}" || true
    ensure_sdk_component "${sdk_root}" "cmdline-tools;16.0" "${sdk_root}/cmdline-tools/16.0" "${java_home}" || true
    ensure_cmdline_tools_latest "${sdk_root}" "16.0" || true
    ensure_sdk_component "${sdk_root}" "platform-tools" "${sdk_root}/platform-tools" "${java_home}" || true
    ensure_sdk_component "${sdk_root}" "build-tools;36.0.0" "${sdk_root}/build-tools/36.0.0" "${java_home}" || true
    ensure_sdk_component "${sdk_root}" "platforms;android-34" "${sdk_root}/platforms/android-34" "${java_home}" || true
  fi

  if [[ -z "${sdk_root}" || -z "${ndk_root}" || -z "${java_home}" ]]; then
    echo "Android toolchain install did not provide required paths." >&2
    echo "Detected ANDROID_SDK_ROOT='${sdk_root}', ANDROID_NDK_ROOT='${ndk_root}', JAVA_HOME='${java_home}'." >&2
    return 1
  fi

  export ANDROID_SDK_ROOT="${sdk_root}"
  export ANDROID_HOME="${sdk_root}"
  export ANDROID_NDK_ROOT="${ndk_root}"
  export JAVA_HOME="${java_home}"
  if [[ -d "${ANDROID_SDK_ROOT}/cmdline-tools" ]]; then
    local cmdline_dir=""
    cmdline_dir=$(ls -d "${ANDROID_SDK_ROOT}/cmdline-tools"/*/bin 2>/dev/null | sort -V | tail -n 1 || true)
    if [[ -n "${cmdline_dir}" ]]; then
      export PATH="${cmdline_dir}:${PATH}"
    fi
  fi

  export PATH="${JAVA_HOME}/bin:${PATH}"

  return 0
}

discover_unity_path() {
  local candidate=""

  if [[ -n "${UNITY_PATH_OVERRIDE}" ]]; then
    if [[ -x "${UNITY_PATH_OVERRIDE}" ]]; then
      echo "${UNITY_PATH_OVERRIDE}"
      return 0
    fi

    if have_cmd "${UNITY_PATH_OVERRIDE}"; then
      command -v "${UNITY_PATH_OVERRIDE}"
      return 0
    fi
  fi

  if have_cmd Unity; then
    command -v Unity
    return 0
  fi

  if have_cmd unity; then
    command -v unity
    return 0
  fi

  candidate=$(ls -d "${HOME}/Unity/Hub/Editor"/*/Editor/Unity 2>/dev/null | sort -V | tail -n 1 || true)
  if [[ -n "${candidate}" && -x "${candidate}" ]]; then
    echo "${candidate}"
    return 0
  fi

  candidate=$(ls -d "/opt/Unity/Hub/Editor"/*/Editor/Unity 2>/dev/null | sort -V | tail -n 1 || true)
  if [[ -n "${candidate}" && -x "${candidate}" ]]; then
    echo "${candidate}"
    return 0
  fi

  for candidate in \
    "/opt/unity/Editor/Unity" \
    "/opt/Unity/Editor/Unity" \
    "/usr/local/Unity/Editor/Unity"; do
    if [[ -x "${candidate}" ]]; then
      echo "${candidate}"
      return 0
    fi
  done

  return 1
}

check_unity_lockfile() {
  local lock_file="${ROOT_DIR}/Library/UnityLockfile"
  if [[ -f "${lock_file}" ]]; then
    if [[ -s "${lock_file}" ]]; then
      echo "Unity appears to be running (lockfile present). Close Unity and retry." >&2
      return 1
    fi
  fi
  return 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --install)
      RUN_INSTALL=true
      shift
      ;;
    --device)
      DEVICE_ID="${2:-}"
      shift 2
      ;;
    --adb-port)
      DECANTRA_ADB_SERVER_PORT="${2:-}"
      export ADB_SERVER_PORT="${DECANTRA_ADB_SERVER_PORT}"
      shift 2
      ;;
    --serial)
      DECANTRA_ANDROID_SERIAL="${2:-}"
      shift 2
      ;;
    --apk-path)
      APK_PATH="${2:-}"
      shift 2
      ;;
    --skip-tests)
      RUN_TESTS=false
      shift
      ;;
    --skip-build)
      RUN_BUILD=false
      shift
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

if [[ "${RUN_TESTS}" == "true" || "${RUN_BUILD}" == "true" ]]; then
  ensure_command timeout coreutils
  ensure_command python3 python3

  UNITY_PATH_FOUND="$(discover_unity_path || true)"
  if [[ -z "${UNITY_PATH_FOUND}" ]]; then
    echo "Unity not found. Install Unity or set UNITY_PATH to the Unity editor executable." >&2
    exit 1
  fi
  export UNITY_PATH="${UNITY_PATH_FOUND}"

  if ! check_unity_lockfile; then
    exit 1
  fi

  if [[ "${RUN_BUILD}" == "true" ]]; then
    ensure_android_toolchain "${UNITY_PATH_FOUND}"
  fi
fi

if [[ "${RUN_TESTS}" == "true" ]]; then
  log "Running Unity tests"
  "${ROOT_DIR}/tools/test.sh"
fi

if [[ "${RUN_BUILD}" == "true" ]]; then
  log "Building debug APK"
  "${ROOT_DIR}/tools/build_android.sh"
fi

if [[ "${RUN_INSTALL}" == "true" ]]; then
  ensure_command adb android-tools-adb
  APK_PATH="${APK_PATH:-$APK_DEFAULT}"

  if [[ ! -f "$APK_PATH" ]]; then
    echo "APK not found: $APK_PATH" >&2
    exit 1
  fi

  if [[ -z "$DEVICE_ID" ]]; then
    DEVICE_ID="${DECANTRA_ANDROID_SERIAL}"
  fi

  if [[ -z "$DEVICE_ID" ]]; then
    DEVICE_ID="$(adb devices | awk 'NR>1 && $2=="device" {print $1; exit}')"
  fi

  if [[ -z "$DEVICE_ID" ]]; then
    echo "No adb devices found. Connect a device or pass --device <id>." >&2
    exit 1
  fi

  log "Installing APK to device: $DEVICE_ID"
  adb -s "$DEVICE_ID" install -r "$APK_PATH"

  log "Launching app"
  adb -s "$DEVICE_ID" shell monkey -p uk.gleissner.decantra -c android.intent.category.LAUNCHER 1
fi

log "Done"